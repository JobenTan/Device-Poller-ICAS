namespace IcasExercise;

    public enum Status {Valid, Invalid, Skipped};
    
    public static class DeviceValidator
{
    public static (Status Status, List<string> Reasons) Validate (EachDeviceRecord row)
    {
        if (row.DeviceId == 0)
            return (Status.Skipped, new List<string> { "DeviceId is 0" });

        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(row.IpAddress))
            reasons.Add("IpAddress is missing or blank");
        if (!row.Port.HasValue || row.Port < 1 || row.Port > 65535)
            reasons.Add("Port is missing or outside of 1 to 65535");
        if (row.Channel < 0)
            reasons.Add("Channel is negative");
        if (!row.PollIntervalMs.HasValue || row.PollIntervalMs <= 0)
            reasons.Add("PollIntervalMs is missing or not greater than 0");
        if (reasons.Count == 0)
            return (Status.Valid, reasons);
        else
            return (Status.Invalid, reasons);
    }
}
