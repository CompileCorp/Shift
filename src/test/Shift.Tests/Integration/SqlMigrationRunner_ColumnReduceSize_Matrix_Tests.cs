using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using Compile.Shift.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Tests.Integration;

[Collection("SqlServer")]
public class SqlMigrationRunner_ColumnReduceSize_Matrix_Tests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public SqlMigrationRunner_ColumnReduceSize_Matrix_Tests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
    }

    [Fact]
    public async Task Varchar_Reduce_Without_Annotation_Should_Not_Alter()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initial = TestModels.BuildComprehensiveModel();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var planner = new MigrationPlanner();
            var plan1 = planner.GeneratePlan(initial, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            Assert.Empty(runner1.Run());

            var updated = TestModels.BuildComprehensiveModel();
            var product = updated.Tables["Product"];
            var name = product.Fields.First(f => f.Name == "Name"); // varchar(50)
            name.Precision = 20; // shrink

            var actual = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(updated, actual);
            Assert.DoesNotContain(plan2.Steps, s => s.Action == MigrationAction.AlterColumn && s.TableName == "Product" && s.Fields.Any(f => f.Name == "Name"));
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Varchar_Reduce_ReduceSize_NoDataLoss_No_Violations_Should_Alter()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initial = TestModels.BuildComprehensiveModel();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var planner = new MigrationPlanner();
            var plan1 = planner.GeneratePlan(initial, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            Assert.Empty(runner1.Run());

            // Insert row with Name <= 20
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO [dbo].[Product] ([IsActive],[SmallNumber],[ShortNumber],[LongNumber],[Price],[Name],[UnicodeName]) VALUES (1,1,1,1,1.00,'short','ShortName');";
                await cmd.ExecuteNonQueryAsync();
            }

            var updated = TestModels.BuildComprehensiveModel();
            var product = updated.Tables["Product"];
            var name = product.Fields.First(f => f.Name == "Name");
            name.Precision = 20;
            name.Attributes["reducesize"] = true;

            var actual = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(updated, actual);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            Assert.Empty(runner2.Run());

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var nameReloaded = reloaded.Tables["Product"].Fields.First(f => f.Name == "Name");
            Assert.Equal(20, nameReloaded.Precision);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Varchar_Reduce_ReduceSize_NoDataLoss_With_Violations_Should_Fail()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initial = TestModels.BuildComprehensiveModel();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var planner = new MigrationPlanner();
            var plan1 = planner.GeneratePlan(initial, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            Assert.Empty(runner1.Run());

            // Insert row with Name > 20
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO [dbo].[Product] ([IsActive],[SmallNumber],[ShortNumber],[LongNumber],[Price],[Name],[UnicodeName]) VALUES (1,1,1,1,1.00,'this_name_is_longer_than_twenty','ShortName');";
                await cmd.ExecuteNonQueryAsync();
            }

            var updated = TestModels.BuildComprehensiveModel();
            var product = updated.Tables["Product"];
            var name = product.Fields.First(f => f.Name == "Name");
            name.Precision = 20;
            name.Attributes["reducesize"] = true;

            var actual = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(updated, actual);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.NotEmpty(failures);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Varchar_Reduce_ReduceSize_AllowDataLoss_Should_Truncate_And_Succeed()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initial = TestModels.BuildComprehensiveModel();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var planner = new MigrationPlanner();
            var plan1 = planner.GeneratePlan(initial, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            Assert.Empty(runner1.Run());

            // Insert row with Name > 20
            int newId;
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO [dbo].[Product] ([IsActive],[SmallNumber],[ShortNumber],[LongNumber],[Price],[Name],[UnicodeName]) OUTPUT INSERTED.ProductID VALUES (1,1,1,1,1.00,'this_name_is_longer_than_twenty','ShortName');";
                newId = System.Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            var updated = TestModels.BuildComprehensiveModel();
            var product = updated.Tables["Product"];
            var name = product.Fields.First(f => f.Name == "Name");
            name.Precision = 20;
            name.Attributes["reducesize"] = true;
            name.Attributes["allowdataloss"] = true;

            var actual = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(updated, actual);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            // Verify truncated value persisted
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT [Name] FROM [dbo].[Product] WHERE ProductID = @id";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = newId; cmd.Parameters.Add(p);
                var result = (string?)await cmd.ExecuteScalarAsync();
                Assert.Equal("this_name_is_longer_".Substring(0,20), result);
            }

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var nameReloaded = reloaded.Tables["Product"].Fields.First(f => f.Name == "Name");
            Assert.Equal(20, nameReloaded.Precision);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Varbinary_Reduce_ReduceSize_NoDataLoss_With_Violations_Should_Fail()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initial = TestModels.BuildComprehensiveModel();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var planner = new MigrationPlanner();
            var plan1 = planner.GeneratePlan(initial, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            Assert.Empty(runner1.Run());

            // Insert row with BinaryVar len 30
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO [dbo].[Product] ([IsActive],[SmallNumber],[ShortNumber],[LongNumber],[Price],[Name],[UnicodeName],[BinaryVar]) VALUES (1,1,1,1,1.00,'abc','Short',@data)";
                var p = cmd.CreateParameter(); p.ParameterName = "@data"; p.Value = Enumerable.Repeat((byte)0xAB, 30).ToArray(); cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }

            var updated = TestModels.BuildComprehensiveModel();
            var product = updated.Tables["Product"];
            var bin = product.Fields.First(f => f.Name == "BinaryVar"); // varbinary(max)
            bin.Precision = 20;
            bin.Attributes["reducesize"] = true;

            var actual = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(updated, actual);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.NotEmpty(failures);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    [Fact]
    public async Task Varbinary_Reduce_ReduceSize_AllowDataLoss_Should_Truncate_And_Succeed()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var shift = new Shift { Logger = _logger };
            var initial = TestModels.BuildComprehensiveModel();
            var actualEmpty = await shift.LoadFromSqlAsync(connectionString);
            var planner = new MigrationPlanner();
            var plan1 = planner.GeneratePlan(initial, actualEmpty);
            var runner1 = new SqlMigrationPlanRunner(connectionString, plan1) { Logger = _logger };
            Assert.Empty(runner1.Run());

            // Insert row with BinaryVar len 30
            int newId;
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO [dbo].[Product] ([IsActive],[SmallNumber],[ShortNumber],[LongNumber],[Price],[Name],[UnicodeName],[BinaryVar]) OUTPUT INSERTED.ProductID VALUES (1,1,1,1,1.00,'abc','Short',@data)";
                var p = cmd.CreateParameter(); p.ParameterName = "@data"; p.Value = Enumerable.Repeat((byte)0xCD, 30).ToArray(); cmd.Parameters.Add(p);
                newId = System.Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            var updated = TestModels.BuildComprehensiveModel();
            var product = updated.Tables["Product"];
            var bin = product.Fields.First(f => f.Name == "BinaryVar");
            bin.Precision = 20;
            bin.Attributes["reducesize"] = true;
            bin.Attributes["allowdataloss"] = true;

            var actual = await shift.LoadFromSqlAsync(connectionString);
            var plan2 = planner.GeneratePlan(updated, actual);
            var runner2 = new SqlMigrationPlanRunner(connectionString, plan2) { Logger = _logger };
            var failures = runner2.Run();
            Assert.Empty(failures);

            // Verify data length truncated to 20
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT DATALENGTH([BinaryVar]) FROM [dbo].[Product] WHERE ProductID = @id";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = newId; cmd.Parameters.Add(p);
                var len = System.Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Assert.Equal(20, len);
            }

            var reloaded = await shift.LoadFromSqlAsync(connectionString);
            var binReloaded = reloaded.Tables["Product"].Fields.First(f => f.Name == "BinaryVar");
            Assert.Equal(20, binReloaded.Precision);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }
}


