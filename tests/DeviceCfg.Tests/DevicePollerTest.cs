using System.Xml;
using IcasExercise;
using Newtonsoft.Json.Serialization;

namespace DeviceCfg.Tests;

internal sealed class FakeDeviceClient : IDeviceClient
{
    private readonly Func<DeviceInfo, CancellationToken, Task<DeviceReading>> _behaviour;

    public FakeDeviceClient(Func<DeviceInfo, CancellationToken, Task<DeviceReading>> behaviour)
    {
        _behaviour = behaviour;
    }

    public Task<DeviceReading> ReadAsync(DeviceInfo device, CancellationToken cancellationToken)
    {
        return _behaviour(device, cancellationToken);
    }

    public static DeviceReading Reading(DeviceInfo device, double value) => new(device.DeviceId, value, DateTime.UtcNow);
}

public class DevicePollerTests
{
    private static readonly PollerOptions FastOptions = new()
    {
        AttemptTimeout = TimeSpan.FromSeconds(1),
        SecondAttemptTimeout = TimeSpan.FromMilliseconds(50),
        ThirdAttemptTimeout = TimeSpan.FromMilliseconds(50)
    };

    private static DeviceInfo Device(int id = 1) => new(id, "Device " + id, "10.0.0.1", 8080);

    private static DevicePoller PollerFor(FakeDeviceClient client) => new(client, FastOptions);

    [Fact]
    public async Task AnswersFirstTime_Normally()
    {
        var client = new FakeDeviceClient((device, token) =>
            Task.FromResult(FakeDeviceClient.Reading(device, 21.5)));

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Ok, result.Outcome);
        Assert.Equal(1, result.Attempts);
    }

    [Fact]
    public async Task TimeoutTwice_ThenOk()
    {
        int calls = 0;

        var client = new FakeDeviceClient((device, token) =>
        {
            calls++;
            if (calls <= 2)
            {
                throw new TimeoutException("device timedout");
            }
            return Task.FromResult(FakeDeviceClient.Reading(device, 18.8));
        });

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.RetriedOk, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task AlwaysTimesOut()
    {
        int calls = 0;

        var client = new FakeDeviceClient((device, token) =>
        {
            calls++;
            throw new TimeoutException("device timedout");
        });

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Timeout, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Fact]
    public async Task HangsThePoller() // infinite delay
    {
        int calls = 0;

        var client = new FakeDeviceClient(async (device, token) =>
        {
            calls++;
            await Task.Delay(Timeout.Infinite, token);
            return FakeDeviceClient.Reading(device, 99.9);
        });

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.Timeout, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task NotANumber_IsBadReading(double value)
    {
        int calls = 0;

        var client = new FakeDeviceClient((device, token) =>
        {
            calls++;
            return Task.FromResult(FakeDeviceClient.Reading(device, value));
        });

        var result = await PollerFor(client).PollAsync(Device(), CancellationToken.None);

        Assert.Equal(PollOutcome.BadReading, result.Outcome);
        Assert.Equal(1, result.Attempts);
    }
}