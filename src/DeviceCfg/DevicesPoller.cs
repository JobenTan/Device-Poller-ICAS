using System.Diagnostics;
using System.Globalization;

namespace IcasExercise;

public enum PollOutcome { Ok, RetriedOk, Timeout, Error, BadReading, InvalidConfig, Skipped};

public sealed record DeviceResult(int DeviceId, string Name, PollOutcome Outcome, int Attempts, long ElapsedMs, string Detail)
{
    public string Label => Outcome switch
    {
        PollOutcome.Ok            => "OK",
        PollOutcome.RetriedOk     => "RETRIED-OK",
        PollOutcome.Timeout       => "TIMEOUT",
        PollOutcome.Error         => "ERROR",
        PollOutcome.BadReading    => "BAD READING",
        PollOutcome.InvalidConfig => "INVALID CONFIG",
        PollOutcome.Skipped       => "SKIPPED",
        _                         => Outcome.ToString()
    };
}

// The timing settings. Tests change them to tiny values so they run fast.
public class PollerOptions
{
    //3 attempts, before giving up
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan SecondAttemptTimeout { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan ThirdAttemptTimeout { get; set; } = TimeSpan.FromMilliseconds(400);
    public int MaxAttempts => 3;
}

// Polls devices. It knows nothing about SQL or the console, so tests can use a fake client.
public class DevicePoller
{
    private readonly IDeviceClient _client;
    private readonly PollerOptions _options;

    public DevicePoller(IDeviceClient client, PollerOptions? options = null)
    {
        _client = client;
        _options = options ?? new PollerOptions();
    }

    // Polls all devices at the same time and returns the results in DeviceId order.
    // Throws OperationCanceledException if the user presses Ctrl+C.
    public async Task<List<DeviceResult>> PollAllAsync(IEnumerable<DeviceInfo> devices, CancellationToken cancellationToken)
    {
        // Start one task per device. Nothing is awaited yet, so they all run together.
        var tasks = new List<Task<DeviceResult>>();
        foreach (var device in devices)
        {
            tasks.Add(PollAsync(device, cancellationToken));
        }

        // Now wait until every device has finished.
        DeviceResult[] results = await Task.WhenAll(tasks);

        return results.OrderBy(r => r.DeviceId).ToList();
    }

    // Polls one device: up to 3 attempts, each limited to 2 seconds.
    public async Task<DeviceResult> PollAsync(DeviceInfo device, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            // If the user already pressed Ctrl+C, stop now.
            cancellationToken.ThrowIfCancellationRequested();

            // A token just for this attempt. It is cancelled by Ctrl+C, or after the timeout.
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(_options.AttemptTimeout);

            try
            {
                // WaitAsync also stops waiting after the timeout, even if the client ignores
                // its token. It throws TimeoutException when that happens.
                DeviceReading reading = await _client
                    .ReadAsync(device, attemptCts.Token)
                    .WaitAsync(_options.AttemptTimeout, cancellationToken);

                // A reading that is not a real number is bad. Do not retry.
                if (double.IsNaN(reading.Value) || double.IsInfinity(reading.Value))
                {
                    return MakeResult(device, PollOutcome.BadReading, attempt, stopwatch,
                        "reading is not a usable number (" + reading.Value + ")");
                }

                // Got a good reading. OK if first try, RETRIED-OK if we had to retry.
                PollOutcome outcome = attempt == 1 ? PollOutcome.Ok : PollOutcome.RetriedOk;
                string value = reading.Value.ToString(CultureInfo.InvariantCulture);
                return MakeResult(device, outcome, attempt, stopwatch, value);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // The user pressed Ctrl+C. This is not a device fault, so pass it on.
                    throw;
                }
                // Otherwise our own 2 second limit fired. Fall through and retry.
            }
            catch (TimeoutException)
            {
                // The device said it timed out. Fall through and retry.
            }
            catch (Exception ex)
            {
                // Any other error is a hard failure. Do not retry.
                return MakeResult(device, PollOutcome.Error, attempt, stopwatch, ex.Message);
            }

            // We only get here after a timeout. Tell the abandoned call to stop.
            attemptCts.Cancel();

            // Wait a little before trying again (but not after the last attempt).
            if (attempt < _options.MaxAttempts)
            {
                await Task.Delay(_options.RetryDelays[attempt - 1], cancellationToken);
            }
        }

        // Every attempt timed out.
        return MakeResult(device, PollOutcome.Timeout, _options.MaxAttempts, stopwatch,
            "no answer after " + _options.MaxAttempts + " attempts");
    }

    private static DeviceResult MakeResult(
        DeviceInfo device, PollOutcome outcome, int attempts, Stopwatch stopwatch, string detail)
    {
        return new DeviceResult(
            device.DeviceId, device.Name, outcome, attempts, stopwatch.ElapsedMilliseconds, detail);
    }
}