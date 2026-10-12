namespace IcasExercise
{
    /// <summary>
    /// Talks to one device and returns a reading.
    /// Real hardware can be slow, can fail, and can send rubbish.
    /// Honour the CancellationToken: a call that ignores it can hang forever.
    /// </summary>
    public interface IDeviceClient
    {
        Task<DeviceReading> ReadAsync(DeviceInfo device, CancellationToken cancellationToken);
    }
}
