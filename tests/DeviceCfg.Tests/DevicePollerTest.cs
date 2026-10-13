using IcasExercise;

namespace DeviceCfg.Tests;

public class DevicePollerTests
{
    // ------------------------------------------------------------------
    // Setup that every test can use
    // ------------------------------------------------------------------

    // Tiny timings so the tests run in milliseconds instead of seconds.
    private static readonly PollerOptions FastOptions = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(1),
        SecondAttemptTimeout = TimeSpan.FromMilliseconds(50),
        ThirdAttemptTimeout = TimeSpan.FromMilliseconds(50)
    };

    // A device to poll. The id is the only thing that matters in the tests.
    private static DeviceInfo Device(int id = 1) => new(id, "Device " + id, "10.0.0.1", 8080);

    // A poller that talks to the fake client and uses the fast timings.
    private static DevicePoller PollerFor(FakeDeviceClient client) => new(client, FastOptions);

    // Every test follows the same three steps:
    //   1. Arrange: build a FakeDeviceClient that behaves the way the test needs.
    //   2. Act:     call the poller.
    //   3. Assert:  check the outcome (and usually the attempts).

    // ------------------------------------------------------------------
    // Worked example: read this one, then do the rest
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnswersFirstTime_IsOk_WithOneAttempt()
    {
        // Arrange: a device that answers straight away with 21.5.
        var client = new FakeDeviceClient((device, call, token) => Task.FromResult(FakeDeviceClient.Reading(device, 21.5)));

        // Act
        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        // Assert
        Assert.Equal(PollOutcome.Ok, result.Outcome);
        Assert.Equal(1, result.Attempts);
    }

    // ------------------------------------------------------------------
    // TODO: your turn. Each stub fails on purpose until you replace it.
    // ------------------------------------------------------------------

    [Fact]
public async Task TimesOutTwiceThenAnswers_IsRetriedOk_WithThreeAttempts()
{
    // Arrange: the first two calls time out, the third one answers.
    var client = new FakeDeviceClient((device, call, token) =>
    {
        if (call <= 2)
        {
            throw new TimeoutException("device timedout");
        }
        return Task.FromResult(FakeDeviceClient.Reading(device, 18.8));
    });

    // Act
    var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

    // Assert
    Assert.Equal(PollOutcome.RetriedOk, result.Outcome);
    Assert.Equal(3, result.Attempts);
}

    [Fact]
    public async Task AlwaysTimesOut_IsTimeout_AfterThreeAttempts()
    {
    var client = new FakeDeviceClient((device, call, token) =>
    {
        if (call <= 3)
        {
            throw new TimeoutException("device timedout");
        }
        return Task.FromResult(FakeDeviceClient.Reading(device, 20.8));
    });

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);
        Assert.Equal(PollOutcome.Timeout, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task NeverAnswersButHonoursTheToken_IsTimeout_AfterThreeAttempts() //infinite delay
    {
        var client = new FakeDeviceClient(async (device, call, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return FakeDeviceClient.Reading(device, 99.9);
        });
        var result  = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Timeout, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }


    [Fact]
    public async Task NeverAnswersAndIgnoresTheToken_IsStillTimeout()
    {
        // Hint: a badly written client that never looks at its token.
        //       var never = new TaskCompletionSource<DeviceReading>();
        //       the lambda just returns never.Task
        // Expect: PollOutcome.Timeout. If this hangs, the poller is not protecting itself.
        await Task.CompletedTask;
        Assert.True(false, "TODO: write this test");
    }

    [Fact]
    public async Task OtherException_IsError_AndIsNotRetried()
    {
        // Hint: throw new FormatException("bad checksum").
        // Expect: PollOutcome.Error, Attempts == 1, Detail == "bad checksum",
        //         and client.CallsFor(1) == 1 (it was NOT retried).
        await Task.CompletedTask;
        Assert.True(false, "TODO: write this test");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task NotANumber_IsBadReading_AndIsNotRetried(double value)
    {
        // Hint: the device "succeeds" but returns `value`. No exception is thrown.
        // Expect: PollOutcome.BadReading, Attempts == 1, and CallsFor(1) == 1.
        await Task.CompletedTask;
        Assert.True(false, "TODO: write this test");
    }

    [Fact]
    public async Task WaitsBetweenAttempts_BeforeTheSecondAndThirdTry()
    {
        // Hint: build your own PollerOptions here with a 100 ms and a 150 ms pause,
        //       and a device that throws TimeoutException instantly every time.
        //       The whole poll can only take about 250 ms if the pauses really happen.
        // Expect: Attempts == 3 and result.ElapsedMs >= 200.
        await Task.CompletedTask;
        Assert.True(false, "TODO: write this test");
    }

    [Fact]
    public async Task UserCancellation_Throws_InsteadOfReportingADeviceFault()
    {
        // Hint: a device that hangs (see the "honours the token" test), plus
        //       using var cts = new CancellationTokenSource();
        //       cts.CancelAfter(TimeSpan.FromMilliseconds(10));
        //       and poll with cts.Token.
        // Expect: await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ... PollAsync(..., cts.Token));
        await Task.CompletedTask;
        Assert.True(false, "TODO: write this test");
    }

    [Fact]
    public async Task PollAll_PollsDevicesAtTheSameTime_AndReturnsThemInIdOrder()
    {
        // Hint: pass devices in a jumbled order, e.g. 5, 2, 4, 1, 3, to PollAllAsync.
        //       To prove they run together, count how many calls are in flight at once:
        //         Interlocked.Increment(ref inFlight) at the start of the lambda,
        //         await Task.Delay(20, token), then Interlocked.Decrement(ref inFlight).
        //       Keep the highest value you ever saw.
        // Expect: ids come back as 1,2,3,4,5 and the highest in-flight count was 5.
        await Task.CompletedTask;
        Assert.True(false, "TODO: write this test");
    }
}

// ----------------------------------------------------------------------
// A fake IDeviceClient. You decide what each call does.
//
// The lambda you pass in receives:
//   device : which device is being read
//   call   : 1 for the first call to that device, 2 for the second, and so on
//   token  : the CancellationToken (a well-behaved client passes it to Task.Delay)
// and must return a Task<DeviceReading>. It may also throw.
// ----------------------------------------------------------------------
internal sealed class FakeDeviceClient : IDeviceClient
{
    private readonly Func<DeviceInfo, int, CancellationToken, Task<DeviceReading>> _behaviour;
    private readonly Dictionary<int, int> _calls = new();
    private readonly object _lock = new();

    public FakeDeviceClient(Func<DeviceInfo, int, CancellationToken, Task<DeviceReading>> behaviour)
    {
        _behaviour = behaviour;
    }

    // How many times this device has been read so far.
    public int CallsFor(int deviceId)
    {
        lock (_lock)
        {
            return _calls.GetValueOrDefault(deviceId);
        }
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

    // Shortcut for building a reading in a test.
    public static DeviceReading Reading(DeviceInfo device, double value) =>
        new(device.DeviceId, value, DateTime.UtcNow);
}