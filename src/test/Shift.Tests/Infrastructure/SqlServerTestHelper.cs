using Microsoft.Data.SqlClient;

namespace Compile.Shift.Tests.Infrastructure;

public static class SqlServerTestHelper
{
    public static string GenerateDatabaseName()
    {
        var guid = Guid.NewGuid().ToString("N").Substring(0, 12);
        return $"ShiftTests_{guid}";
    }

    public static string BuildDbConnectionString(string masterConnectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ToString();
    }

    public static async Task CreateDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await using var conn = new SqlConnection(masterConnectionString);
        await conn.OpenAsync();
        var createCmdText = $"CREATE DATABASE [{databaseName}]";
        await using var cmd = new SqlCommand(createCmdText, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DropDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await using var conn = new SqlConnection(masterConnectionString);
        await conn.OpenAsync();
        var dropCmdText = $@"
IF DB_ID('{databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END";
        await using var cmd = new SqlCommand(dropCmdText, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}


