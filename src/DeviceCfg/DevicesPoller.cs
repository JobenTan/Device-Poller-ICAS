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

// 3 trys max
public class PollerOptions
{
    //3 attempts, before giving up
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan SecondAttemptTimeout { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan ThirdAttemptTimeout { get; set; } = TimeSpan.FromMilliseconds(400);
    public int MaxAttempts => 3;
}

// Polls devices
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
        var stopwatch = Stopwatch.StartNew(); // for the Elapsed ms 

        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            // If the user already pressed Ctrl+C, stop now.
            cancellationToken.ThrowIfCancellationRequested();

            // A token just for this attempt. It is cancelled by Ctrl+C, or after the timeout.
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(_options.AttemptTimeout);

            try
            {
                DeviceReading reading = await _client.ReadAsync(device, attemptCts.Token).WaitAsync(_options.AttemptTimeout, cancellationToken);

                // reading must return a number because it returns are int and double
                if (double.IsNaN(reading.Value) || double.IsInfinity(reading.Value))
                {
                    return MakeResult(device, PollOutcome.BadReading, attempt, stopwatch,"reading is not a number");
                }

                // Reading no issues
                PollOutcome outcome = attempt == 1 ? PollOutcome.Ok : PollOutcome.RetriedOk;
                string value = reading.Value.ToString(CultureInfo.InvariantCulture);
                return MakeResult(device, outcome, attempt, stopwatch, value);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw; // when ctrl c is pressed
                }
            }
            catch (TimeoutException)
            {
                // first two timeout fall through
            }
            catch (Exception ex)
            {
                // other errros will stop and exit
                return MakeResult(device, PollOutcome.Error, attempt, stopwatch, ex.Message);
            }

            // We only get here after a timeout. Tell the abandoned call to stop.
            attemptCts.Cancel();

            // attempt 1 & 2
            if (attempt < _options.MaxAttempts)
            {
                if (attempt == 1)
                {
                    attemptCts.CancelAfter(_options.SecondAttemptTimeout);
                }
                else if (attempt == 2)
                {
                    attemptCts.CancelAfter(_options.ThirdAttemptTimeout);
                }
            }
        }

        // mex attempt reached
        return MakeResult(device, PollOutcome.Timeout, _options.MaxAttempts, stopwatch, "max attempts reached :'(");
    }

// store each device results
    private static DeviceResult MakeResult(DeviceInfo device, PollOutcome outcome, int attempts, Stopwatch stopwatch, string detail)
    {
        return new DeviceResult(device.DeviceId, device.Name, outcome, attempts, stopwatch.ElapsedMilliseconds, detail);
    }
}