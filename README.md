## Requirements
- .NET 8 SDK
- SQL Server: Microsoft SQL Server Express

## 1. Create the database
Run `01-setup.sql` against your SQL Server instance.
`dbo.DeviceConfig` created with 10 sample rows.

## 2. Connection string
The default is: Server=localhost\SQLEXPRESS;Database=DeviceConfigDB;Integrated Security=True;TrustServerCertificate=True
Set the `DEVICECFG_CONNECTION` environment variable to use a different server

## 3. Build and run
In the terminal:
    dotnet build
    dotnet run --project src/DeviceCfg

## 4. Run the tests
From the root:
    dotnet test tests/DeviceCfg.Tests