namespace IcasExercise
{
    /// <summary>What the polling code needs to know about one device.</summary>
    public sealed record DeviceInfo(int DeviceId, string Name, string? IpAddress, int? Port);

    /// <summary>One successful reading from a device.</summary>
    public sealed record DeviceReading(int DeviceId, double Value, DateTime TakenAtUtc);
}
