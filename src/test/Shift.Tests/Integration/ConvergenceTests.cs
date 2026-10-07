using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Integration;

/// <summary>
/// End-to-end convergence checks. The FK supporting-index defect (269119b) survived because every
/// test asserted what a single plan contained, and none asserted the property that actually matters:
/// after applying a model, the database matches it - so reloading and planning again yields nothing,
/// and drift introduced afterwards is repaired by the next apply.
/// </summary>
[Collection("SqlServer")]
public class ConvergenceTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public ConvergenceTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Shift>();
    }

    private const string UserDmd = """
        model User {
          string(100) Username
          string(256) Email
          key (Username)
        }
        """;

    private const string AuditableDmdx = """
        mixin Auditable {
          !model User? as CreatedBy
          !model User? as LastModifiedBy
          datetime CreatedDateTime
        }
        """;

    private const string TaskV1Dmd = """
        model Task {
          string(200) Title
          model User as AssignedUser
          index (Title)
        }
        """;

    // V2: the model gains a mixin that adds two more FKs to User on an already-existing table.
    private const string TaskV2Dmd = """
        model Task with Auditable {
          string(200) Title
          model User as AssignedUser
          index (Title)
        }
        """;

    [Fact]
    public async Task Apply_FromEmpty_ThenReplan_IsEmpty()
    {
        await WithDatabase(async cs =>
        {
            var model = await LoadDmd(("User.dmd", UserDmd), ("Auditable.dmdx", AuditableDmdx), ("Task.dmd", TaskV2Dmd));
            await Apply(model, cs);

            var plan = await Replan(model, cs);

            plan.Steps.Select(Describe).Should().BeEmpty("a freshly applied model must already match the database");
        });
    }

    [Fact]
    public async Task Apply_ExistingTableGainsMoreForeignKeysToSameTable_CreatesAllConstraints()
    {
        await WithDatabase(async cs =>
        {
            await Apply(await LoadDmd(("User.dmd", UserDmd), ("Task.dmd", TaskV1Dmd)), cs);

            var v2 = await LoadDmd(("User.dmd", UserDmd), ("Auditable.dmdx", AuditableDmdx), ("Task.dmd", TaskV2Dmd));
            await Apply(v2, cs);

            var fkColumns = await QueryStrings(cs, """
                SELECT c.name FROM sys.foreign_key_columns fkc
                JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                WHERE fkc.parent_object_id = OBJECT_ID('dbo.Task')
                """);

            fkColumns.Should().BeEquivalentTo("AssignedUserUserID", "CreatedByUserID", "LastModifiedByUserID");
        });
    }

    [Fact]
    public async Task Apply_AfterOneOfTwoForeignKeysToSameTableIsDropped_RestoresIt()
    {
        await WithDatabase(async cs =>
        {
            var model = await LoadDmd(("User.dmd", UserDmd), ("Auditable.dmdx", AuditableDmdx), ("Task.dmd", TaskV2Dmd));
            await Apply(model, cs);

            await Exec(cs, "ALTER TABLE [dbo].[Task] DROP CONSTRAINT [FK_Task_LastModifiedByUserID]");
            await Apply(model, cs);

            var count = await QueryStrings(cs,
                "SELECT name FROM sys.foreign_keys WHERE name = 'FK_Task_LastModifiedByUserID'");
            count.Should().ContainSingle("the dropped FK is part of the model and must be restored");
        });
    }

    private static string Describe(MigrationStep s) =>
        $"{s.Action} {s.TableName} {s.ForeignKey?.ColumnName}{(s.Index is null ? "" : string.Join(",", s.Index.Fields))}{string.Join(",", s.Fields.Select(f => f.Name))}";

    private async Task<DatabaseModel> LoadDmd(params (string Name, string Content)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("shift-convergence-");
        try
        {
            foreach (var (name, content) in files)
                await File.WriteAllTextAsync(Path.Combine(dir.FullName, name), content);
            return await new Shift { Logger = _logger }.LoadFromPathAsync([dir.FullName]);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private async Task Apply(DatabaseModel model, string cs)
    {
        var result = await new Shift { Logger = _logger }.ApplyToSqlAsync(model, cs);
        result.Failures.Select(f => $"{f.Item1.Action} {f.Item1.TableName}: {f.Item2.Message}").Should().BeEmpty();
    }

    private async Task<MigrationPlan> Replan(DatabaseModel model, string cs)
    {
        var actual = await new Shift { Logger = _logger }.LoadFromSqlAsync(cs);
        return new MigrationPlanner().GeneratePlan(model, actual);
    }

    private async Task WithDatabase(Func<string, Task> body)
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        try
        {
            await body(SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName));
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    private static async Task Exec(string cs, string sql)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> QueryStrings(string cs, string sql)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));
        return rows;
    }
}