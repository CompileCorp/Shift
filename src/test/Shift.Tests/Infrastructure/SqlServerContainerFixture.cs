using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;

namespace Compile.Shift.Tests.Infrastructure;

public class SqlServerContainerFixture : IAsyncLifetime
{
    private IContainer? _container;
    public string ConnectionStringMaster { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var password = "Your_strong_password123!";

        // Use generic container with simple wait (port availability) and do our own SQL readiness wait.
        _container = new ContainerBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", password)
            .WithEnvironment("MSSQL_PID", "Express")
            .WithPortBinding(0, 1433)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();

        await _container.StartAsync();

        // Build a master connection string for DB create/drop
        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(1433);
        ConnectionStringMaster = $"Server={host},{port};Database=master;User Id=sa;Password={password};TrustServerCertificate=True;";

        // Wait until SQL Server accepts connections
        await WaitForSqlReadyAsync(ConnectionStringMaster, TimeSpan.FromMinutes(2));
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    private static async Task WaitForSqlReadyAsync(string connectionString, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        Exception? last = null;
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = 5
                };
                await using var conn = new SqlConnection(builder.ToString());
                await conn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(1000);
            }
        }
        throw new TimeoutException("SQL Server did not become ready in time.", last);
    }
}

[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture> { }


