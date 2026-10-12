namespace IcasExercise;

    public sealed record EachDeviceRecord(int DeviceId, int ZoneId, String DeviceType, String DeviceName, String? IpAddress, int? Port, int? Channel, int Enabled, double? FirmwareVersion, String? UpdateMode, int? PollIntervalMs, DateTime? LastSeenAt)
    {
        public DeviceInfo SimplifiedDeviceInfo() => new DeviceInfo(DeviceId, DeviceName, IpAddress, Port);
    }