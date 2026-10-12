using IcasExercise;

namespace DeviceCfg.Tests;

public class DevicePollerTests
{
    // Tiny timings so the whole file runs in well under a second.
    private static readonly PollerOptions FastOptions = new()
    {
        AttemptTimeout = TimeSpan.FromMilliseconds(50),
        SecondAttemptTimeout = TimeSpan.FromMilliseconds(1),
        ThirdAttemptTimeout = TimeSpan.FromMilliseconds(1)
    };

    private static DeviceInfo Device(int id = 1) => new(id, $"Device {id}", "10.0.0.1", 8080);

    private static DevicePoller PollerFor(FakeDeviceClient client) => new(client, FastOptions);

    [Fact]
    public async Task AnswersFirstTime_IsOk_WithOneAttempt()
    {
        var client = new FakeDeviceClient((d, call, ct) => Task.FromResult(FakeDeviceClient.Reading(d, 21.5)));

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Ok, result.Outcome);
        Assert.Equal(1, result.Attempts);
        Assert.Equal("21.5", result.Detail);
    }

    [Fact]
    public async Task TimesOutTwiceThenAnswers_IsRetriedOk_WithThreeAttempts()
    {
        var client = new FakeDeviceClient((d, call, ct) =>
            call <= 2
                ? throw new TimeoutException("slow")
                : Task.FromResult(FakeDeviceClient.Reading(d, 22.1)));

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.RetriedOk, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task AlwaysTimesOut_IsTimeout_AfterThreeAttempts()
    {
        var client = new FakeDeviceClient((d, call, ct) => throw new TimeoutException("slow"));

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Timeout, result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, client.CallsFor(1));
    }

    [Fact]
    public async Task NeverAnswersButHonoursTheToken_IsTimeout_AfterThreeAttempts()
    {
        // Like simulated device 8: only the CancellationToken can end the call.
        var client = new FakeDeviceClient(async (d, call, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return FakeDeviceClient.Reading(d, 0);
        });

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Timeout, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task NeverAnswersAndIgnoresTheToken_IsStillTimeout()
    {
        // A badly written client that never looks at its token must not hang the poller.
        var never = new TaskCompletionSource<DeviceReading>();
        var client = new FakeDeviceClient((d, call, ct) => never.Task);

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Timeout, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task OtherException_IsError_AndIsNotRetried()
    {
        var client = new FakeDeviceClient((d, call, ct) => throw new FormatException("bad checksum"));

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Error, result.Outcome);
        Assert.Equal(1, result.Attempts);
        Assert.Equal("bad checksum", result.Detail);
        Assert.Equal(1, client.CallsFor(1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task NotANumber_IsBadReading_AndIsNotRetried(double value)
    {
        var client = new FakeDeviceClient((d, call, ct) => Task.FromResult(FakeDeviceClient.Reading(d, value)));

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.BadReading, result.Outcome);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, client.CallsFor(1));
    }

    [Fact]
    public async Task UserCancellation_Throws_InsteadOfReportingADeviceFault()
    {
        var client = new FakeDeviceClient(async (d, call, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return FakeDeviceClient.Reading(d, 0);
        });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PollerFor(client).PollAsync(Device(), cts.Token));
    }

    [Fact]
    public async Task PollAll_PollsDevicesAtTheSameTime_AndReturnsThemInIdOrder()
    {
        int inFlight = 0, maxInFlight = 0;
        var client = new FakeDeviceClient(async (d, call, ct) =>
        {
            int now = Interlocked.Increment(ref inFlight);
            int seen;
            while (now > (seen = Volatile.Read(ref maxInFlight)))
                Interlocked.CompareExchange(ref maxInFlight, now, seen);

            await Task.Delay(20, ct);
            Interlocked.Decrement(ref inFlight);
            return FakeDeviceClient.Reading(d, 20);
        });
        var devices = new[] { Device(5), Device(2), Device(4), Device(1), Device(3) };

        var results = await PollerFor(client).PollAllAsync(devices, CancellationToken.None);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results.Select(r => r.DeviceId).ToArray());
        Assert.Equal(5, maxInFlight);   // all five were in flight together
    }
}

/// <summary>A fake IDeviceClient: the test decides what each call does.</summary>
internal sealed class FakeDeviceClient : IDeviceClient
{
    private readonly Func<DeviceInfo, int, CancellationToken, Task<DeviceReading>> _behaviour;
    private readonly Dictionary<int, int> _calls = new();
    private readonly object _lock = new();

    public FakeDeviceClient(Func<DeviceInfo, int, CancellationToken, Task<DeviceReading>> behaviour) =>
        _behaviour = behaviour;

    public int CallsFor(int deviceId)
    {
        lock (_lock) return _calls.GetValueOrDefault(deviceId);
    }

    public Task<DeviceReading> ReadAsync(DeviceInfo device, CancellationToken cancellationToken)
    {
        int call;
        lock (_lock)
        {
            call = _calls.GetValueOrDefault(device.DeviceId) + 1;
            _calls[device.DeviceId] = call;
        }
        return _behaviour(device, call, cancellationToken);
    }

    public static DeviceReading Reading(DeviceInfo device, double value) =>
        new(device.DeviceId, value, DateTime.UtcNow);
}