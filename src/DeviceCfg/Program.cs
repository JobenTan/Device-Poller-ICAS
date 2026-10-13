using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace IcasExercise;

public class Program
{
    private const int ExitOk = 0;      
    private const int ExitConfigError = 1;
    private const int ExitDeviceProblems = 2;
    private const int ExitCancelled = 3;

    //db connection
    private const string DefaultConnectionString = @"Server=localhost\SQLEXPRESS;Database=DeviceConfigDB;Integrated Security=True;TrustServerCertificate=True";

    // ----------------------------------------------------------------------------------------------------------------

    public static async Task<int> Main()
    {
        // ---- Ctrl+C handling ----
        // Pressing Ctrl+C cancels this token. The token is passed down to the database
        // read and to the poller, so they stop quickly.
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            // The first Ctrl+C: do not kill the process, let it stop cleanly.
            // A second Ctrl+C is allowed to kill it, in case something is stuck.
            e.Cancel = !cts.IsCancellationRequested;
            cts.Cancel();
        };

        string connectionString =
            Environment.GetEnvironmentVariable("DEVICECFG_CONNECTION") ?? DefaultConnectionString;

        // read config
        List<EachDeviceRecord> rows;
        try
        {
            rows = await RetrieveAllDevices.ReadConfigAsync(connectionString, cts.Token);
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested)
            {
                Console.WriteLine("Cancelled by the user (Ctrl+C).");
                return ExitCancelled;
            }

            Console.WriteLine("Device Error or DB Connection Error" + ex);
            return ExitConfigError;
        }

        //filter only polling devices
        var results = new List<DeviceResult>();       // every device ends up here
        var devicesToPoll = new List<DeviceInfo>();   // only the valid ones

        foreach (EachDeviceRecord row in rows)
        {
            var (status, reasons) = DeviceValidator.Validate(row);

            if (status == Status.Skipped)
            {
                results.Add(new DeviceResult(row.DeviceId, row.DeviceName, PollOutcome.Skipped, 0, 0, string.Join("; ", reasons)));
            }
            else if (status == Status.Invalid)
            {
                results.Add(new DeviceResult(row.DeviceId, row.DeviceName, PollOutcome.InvalidConfig, 0, 0, string.Join("; ", reasons)));
            }
            else
            {
                devicesToPoll.Add(row.SimplifiedDeviceInfo()); //for devicemodel.cs
            }
        }

        //poll all the valid ones
        var poller = new DevicePoller(new SimulatedDeviceClient());
        var stopwatch = Stopwatch.StartNew();
        List<DeviceResult> polledResults;
        // keep running until user do a ctrl c
        try
        {
            polledResults = await poller.PollAllAsync(devicesToPoll, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Stopped, Ctrl C is pressed");
            return ExitCancelled;
        }
        stopwatch.Stop();

        results.AddRange(polledResults);
        results = results.OrderBy(r => r.DeviceId).ToList();

        PrintReport(results, devicesToPoll.Count, stopwatch.ElapsedMilliseconds); // call printreport function

        // exit if any of these outcome are found
        foreach (DeviceResult result in results)
        {
            if (result.Outcome == PollOutcome.Timeout || result.Outcome == PollOutcome.Error || result.Outcome == PollOutcome.BadReading)
            {
                return ExitDeviceProblems;
            }
        }
        return ExitOk;
    }

    // Print the results!!
    private static void PrintReport(List<DeviceResult> results, int polledCount, long totalMs)
    {
        Console.WriteLine(FormatLine("Id", "Name", "Result", "Attempts", "Elapsed ms", "Detail"));

        foreach (DeviceResult r in results)
        {
            Console.WriteLine(FormatLine(
                r.DeviceId.ToString(), r.Name, r.Label,
                r.Attempts.ToString(), r.ElapsedMs.ToString(), r.Detail));
        }

        Console.WriteLine();
        Console.WriteLine("Polled " + polledCount + " devices in " + totalMs + " ms.");
    }

    // Lines up the columns. The header and the rows use the same widths.
    private static string FormatLine(
        string id, string name, string result, string attempts, string elapsed, string detail)
    {
        return id.PadRight(4) + name.PadRight(24) + result.PadRight(16) +
               attempts.PadLeft(8) + elapsed.PadLeft(12) + "  " + detail;
    }
}