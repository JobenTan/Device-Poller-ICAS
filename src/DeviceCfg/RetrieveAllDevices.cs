using Microsoft.Data.SqlClient;

namespace IcasExercise;

public sealed record EachDeviceRecord(int DeviceId, int ZoneId, String DeviceType, String DeviceName, String? IpAddress, int? Port, int? Channel, int Enabled, double? FirmwareVersion, String? UpdateMode, int? PollIntervalMs, DateTime? LastSeenAt)
{
    public DeviceInfo SimplifiedDeviceInfo() => new DeviceInfo(DeviceId, DeviceName, IpAddress, Port);
}

public static class RetrieveAllDevices
{
    private static string? NullableString(SqlDataReader reader, string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetString(i);
    }

    private static int? NullableInt(SqlDataReader reader, string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetInt32(i);
    }

    private static double? NullableDouble(SqlDataReader reader, string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetDouble(i);
    }

    private static DateTime? NullableDateTime(SqlDataReader reader, string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetDateTime(i);
    }

    public static async Task<List<EachDeviceRecord>> ReadConfigAsync(string connectionString, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT DeviceId, ZoneId, DeviceType, DeviceName, IpAddress, Port, Channel,
                   Enabled, FirmwareVersion, UpdateMode, PollIntervalMs, LastSeenAt
            FROM dbo.DeviceConfig
            ORDER BY DeviceId";

        var rows = new List<EachDeviceRecord>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            // Columns that can be NULL in the database need the "Nullable" helpers above.
            rows.Add(new EachDeviceRecord(
                DeviceId: reader.GetInt32(reader.GetOrdinal("DeviceId")),
                ZoneId: reader.GetInt32(reader.GetOrdinal("ZoneId")),
                DeviceType: reader.GetString(reader.GetOrdinal("DeviceType")),
                DeviceName: reader.GetString(reader.GetOrdinal("DeviceName")),
                IpAddress: NullableString(reader, "IpAddress"),
                Port: NullableInt(reader, "Port"),
                Channel: NullableInt(reader, "Channel"),
                Enabled: reader.GetInt16(reader.GetOrdinal("Enabled")),
                FirmwareVersion: NullableDouble(reader, "FirmwareVersion"),
                UpdateMode: NullableString(reader, "UpdateMode"),
                PollIntervalMs: NullableInt(reader, "PollIntervalMs"),
                LastSeenAt: NullableDateTime(reader, "LastSeenAt")));
        }

        return rows;
    }
}