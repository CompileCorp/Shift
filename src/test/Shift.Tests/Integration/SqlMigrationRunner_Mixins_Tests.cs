using Compile.Shift.Tests.Helpers;
using Compile.Shift.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Tests.Integration;

[Collection("SqlServer")]
public class SqlMigrationRunner_Mixins_Tests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public SqlMigrationRunner_Mixins_Tests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
    }

    [Fact]
    public async Task AppliesMixins_FieldsExistInSchema()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            var (model, _, _) = TestModels.BuildMixinModel();

            var shift = new Shift { Logger = _logger };
            var actual = await shift.LoadFromSqlAsync(connectionString);

            var planner = new MigrationPlanner();
            var plan = planner.GeneratePlan(model, actual);
            var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = _logger };
            var failures = runner.Run();
            Assert.Empty(failures);

            var reloaded = await shift.LoadFromSqlAsync(connectionString);

            Assert.True(reloaded.Tables.ContainsKey("Task"));
            var task = reloaded.Tables["Task"];
            var expectedFields = new[] { "CreatedDateTime", "LastModifiedDateTime", "LockNumber" };
            foreach (var f in expectedFields)
            {
                Assert.Contains(task.Fields, x => x.Name.Equals(f, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }
}


