using System.Collections.Concurrent;

namespace IcasExercise
{
    /// <summary>
    /// Stands in for real hardware so you can run your program without any devices.
    /// Some devices answer quickly, some are slow, some fail. Your program must not
    /// depend on device numbers: it should cope with whatever a device does.
    /// </summary>
    public sealed class SimulatedDeviceClient : IDeviceClient
    {
        private readonly ConcurrentDictionary<int, int> _calls = new();

        /// <summary>How many times each device has been called so far.</summary>
        public IReadOnlyDictionary<int, int> CallCounts => _calls;

        public async Task<DeviceReading> ReadAsync(DeviceInfo device, CancellationToken cancellationToken)
        {
            int call = _calls.AddOrUpdate(device.DeviceId, 1, (_, n) => n + 1);

            switch (device.DeviceId)
            {
                case 1:
                    await Task.Delay(50, cancellationToken);
                    return Reading(device, 21.5);

                case 2:
                    await Task.Delay(1200, cancellationToken);
                    return Reading(device, 19.0);

                case 3:
                    if (call <= 2)
                    {
                        await Task.Delay(100, cancellationToken);
                        throw new TimeoutException("device did not answer in time");
                    }
                    await Task.Delay(80, cancellationToken);
                    return Reading(device, 22.1);

                case 8:
                    // Never answers. Only the CancellationToken can end this call.
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    throw new InvalidOperationException("unreachable");

                case 9:
                    await Task.Delay(60, cancellationToken);
                    throw new FormatException("payload checksum mismatch");

                case 10:
                    await Task.Delay(40, cancellationToken);
                    return new DeviceReading(device.DeviceId, double.NaN, DateTime.UtcNow);

                default:
                    await Task.Delay(100, cancellationToken);
                    return Reading(device, 20.0);
            }
        }

        private static DeviceReading Reading(DeviceInfo device, double value) =>
            new DeviceReading(device.DeviceId, value, DateTime.UtcNow);
    }
}
