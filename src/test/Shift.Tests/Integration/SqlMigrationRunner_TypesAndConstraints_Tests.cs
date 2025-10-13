using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using Compile.Shift.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Tests.Integration;

[Collection("SqlServer")]
public class SqlMigrationRunner_TypesAndConstraints_Tests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public SqlMigrationRunner_TypesAndConstraints_Tests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
    }

    [Fact]
    public async Task CreatesSchema_WithTypesPksFks_AsExpected()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Arrange target model with broad types and constraints
            var target = TestModels.BuildComprehensiveModel();

            // Load actual model from empty DB
            var shift = new Shift { Logger = _logger };
            var actual = await shift.LoadFromSqlAsync(connectionString);

            // Plan and apply
            var planner = new MigrationPlanner();
            var plan = planner.GeneratePlan(target, actual);
            var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = _logger };
            var failures = runner.Run();
            Assert.Empty(failures);

            // Reload and validate
            var reloaded = await shift.LoadFromSqlAsync(connectionString);

            // Tables exist
            foreach (var tableName in target.Tables.Keys)
            {
                Assert.True(reloaded.Tables.ContainsKey(tableName));
            }

            // Columns and types (including precision/scale) match expectations where applicable
            foreach (var (tableName, table) in target.Tables)
            {
                var actualTable = reloaded.Tables[tableName];
                foreach (var field in table.Fields)
                {
                    var actualField = actualTable.Fields.FirstOrDefault(f => f.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(actualField);
                    Assert.Equal(field.Type, actualField!.Type, ignoreCase: true);

                    // Precision/scale validation for strings/decimals
                    if (string.Equals(field.Type, "decimal", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(field.Precision, actualField.Precision);
                        Assert.Equal(field.Scale, actualField.Scale);
                    }
                    if ((string.Equals(field.Type, "varchar", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(field.Type, "nvarchar", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(field.Type, "char", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(field.Type, "nchar", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(field.Type, "binary", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(field.Type, "varbinary", StringComparison.OrdinalIgnoreCase))
                         && field.Precision.HasValue)
                    {
                        Assert.Equal(field.Precision, actualField.Precision);
                    }
                }
            }

            // Foreign keys
            foreach (var (tableName, table) in target.Tables)
            {
                var actualTable = reloaded.Tables[tableName];
                foreach (var fk in table.ForeignKeys)
                {
                    var actualFk = actualTable.ForeignKeys.FirstOrDefault(x => x.ColumnName.Equals(fk.ColumnName, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(actualFk);
                    Assert.Equal(fk.TargetTable, actualFk!.TargetTable, ignoreCase: true);
                    Assert.Equal(fk.ColumnName, actualFk.ColumnName, ignoreCase: true);
                }
            }
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }
}


