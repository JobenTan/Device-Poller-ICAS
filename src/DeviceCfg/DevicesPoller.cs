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

