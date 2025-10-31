using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Linq;
using Xunit;

namespace Compile.Shift.Integration;

[Collection("SqlServer")]
public class SqlMigrationRunner_SafeShrink_Tests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public SqlMigrationRunner_SafeShrink_Tests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
    }

    private static DatabaseModel BuildSingleColumnModel(string tableName, string columnName, string type, int? precision = null, int? scale = null, bool nullable = true)
    {
        var model = new DatabaseModel();
        var table = new TableModel
        {
            Name = tableName,
            Fields =
            {
                new FieldModel { Name = columnName, Type = type, Precision = precision, Scale = scale, IsNullable = nullable }
            }
        };
        model.Tables[tableName] = table;
        return model;
    }

    private static async Task InsertStringAsync(string connectionString, string table, string column, string value)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = $"INSERT INTO [dbo].[{table}] ([{column}]) VALUES (@v)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertVarBinaryAsync(string connectionString, string table, string column, byte[] value)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = $"INSERT INTO [dbo].[{table}] ([{column}]) VALUES (@v)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertDecimalAsync(string connectionString, string table, string column, decimal value)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var sql = $"INSERT INTO [dbo].[{table}] ([{column}]) VALUES (@v)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task NVarChar_Shrink_Unsafe_Should_Skip()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };

            // Apply initial schema nvarchar(100)
            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "nvarchar", 100);
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var planner = new MigrationPlanner();
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            // Insert 80-char value
            await InsertStringAsync(connectionString, "ShrinkTest", "C", new string('x', 80));

            // Target shrink to nvarchar(60) -> unsafe, should skip
            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "nvarchar", 60);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(100, col.Precision);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task NVarChar_Shrink_Safe_Should_Apply()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };

            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "nvarchar", 100);
            var planner = new MigrationPlanner();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            // Insert 40-char value
            await InsertStringAsync(connectionString, "ShrinkTest", "C", new string('y', 40));

            // Shrink to 60 (safe)
            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "nvarchar", 60);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(60, col.Precision);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task VarBinary_Shrink_Unsafe_Should_Skip()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "varbinary", 100);
            var planner = new MigrationPlanner();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            await InsertVarBinaryAsync(connectionString, "ShrinkTest", "C", new byte[80]);

            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "varbinary", 60);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(100, col.Precision);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task VarBinary_Shrink_Safe_Should_Apply()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "varbinary", 100);
            var planner = new MigrationPlanner();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            await InsertVarBinaryAsync(connectionString, "ShrinkTest", "C", new byte[40]);

            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "varbinary", 60);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(60, col.Precision);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Decimal_Scale_Shrink_Unsafe_Should_Skip()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 18, 4);
            var planner = new MigrationPlanner();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            await InsertDecimalAsync(connectionString, "ShrinkTest", "C", 123.4567m);

            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 18, 2);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(18, col.Precision);
            Assert.Equal(4, col.Scale);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Decimal_Scale_Shrink_Safe_Should_Apply()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 18, 4);
            var planner = new MigrationPlanner();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            await InsertDecimalAsync(connectionString, "ShrinkTest", "C", 123.4500m);

            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 18, 2);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(18, col.Precision);
            Assert.Equal(2, col.Scale);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Decimal_Precision_Shrink_Unsafe_Should_Skip()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 18, 0);
            var planner = new MigrationPlanner();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            await InsertDecimalAsync(connectionString, "ShrinkTest", "C", 123456789012345678m);

            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 15, 0);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(18, col.Precision);
            Assert.Equal(0, col.Scale);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Decimal_Precision_Shrink_Safe_Should_Apply()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initialModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 18, 0);
            var planner = new MigrationPlanner();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var plan1 = planner.GeneratePlan(initialModel, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            runner1.Run();

            await InsertDecimalAsync(connectionString, "ShrinkTest", "C", 123456789012345m);

            var shrinkModel = BuildSingleColumnModel("ShrinkTest", "C", "decimal", 15, 0);
            var modelAfter = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(shrinkModel, modelAfter);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var table = reloaded.Tables["ShrinkTest"];
            var col = table.Fields.First(f => f.Name == "C");
            Assert.Equal(15, col.Precision);
            Assert.Equal(0, col.Scale);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }
}