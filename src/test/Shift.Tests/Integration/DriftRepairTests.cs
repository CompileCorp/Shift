using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Integration;

/// <summary>
/// Applies a model, changes it (or the database), applies again, and checks the database - not the
/// plan - ended up where the model says, or that the change was refused and reported.
/// </summary>
[Collection("SqlServer")]
public class DriftRepairTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public DriftRepairTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Shift>();
    }

    private const string UserDmd = """
        model User {
          string(100) Username
        }
        """;

    private const string TaskDmd = """
        model Task {
          string(200) Title
          model User as AssignedUser
          index (Title)
        }
        """;

    [Fact]
    public async Task Apply_IndexChangedToUnique_MakesIndexUnique()
    {
        await WithDatabase(async cs =>
        {
            await ApplyCleanly(Files(TaskDmd), cs);

            var v2 = Files(TaskDmd.Replace("index (Title)", "index (Title) @unique"));
            await ApplyCleanly(v2, cs);

            (await Query(cs, "SELECT CAST(is_unique AS varchar(1)) FROM sys.indexes WHERE name = 'IX_Task_Title'"))
                .Should().Equal("1");
            (await Replan(v2, cs)).Steps.Should().BeEmpty();
        });
    }

    /// <summary>The rebuild must fail loudly, and keep the old index, when the data has duplicates.</summary>
    [Fact]
    public async Task Apply_IndexChangedToUniqueWithDuplicateData_FailsAndKeepsIndex()
    {
        await WithDatabase(async cs =>
        {
            await ApplyCleanly(Files(TaskDmd), cs);
            await Exec(cs, """
                INSERT [User] (Username) VALUES ('u');
                INSERT [Task] (Title, AssignedUserUserID) VALUES ('same', 1), ('same', 1);
                """);

            var result = await Apply(Files(TaskDmd.Replace("index (Title)", "index (Title) @unique")), cs);

            result.Failures.Should().ContainSingle(f => f.Item1.Action == MigrationAction.AddIndex);
            (await Query(cs, "SELECT CAST(is_unique AS varchar(1)) FROM sys.indexes WHERE name = 'IX_Task_Title'"))
                .Should().Equal("0");
        });
    }

    [Fact]
    public async Task Load_IndexWithIncludedColumn_ReportsOnlyKeyColumns()
    {
        await WithDatabase(async cs =>
        {
            await CreateThingWithIncludeIndex(cs);

            var loaded = await new Shift { Logger = _logger }.LoadFromSqlAsync(cs);

            loaded.Tables["Thing"].Indexes.Should().ContainSingle().Which.Fields.Should().Equal("Code");
        });
    }

    [Fact]
    public async Task Export_IndexWithIncludedColumn_WritesKeyColumnsOnly()
    {
        await WithDatabase(async cs =>
        {
            await CreateThingWithIncludeIndex(cs);

            var loaded = await new Shift { Logger = _logger }.LoadFromSqlAsync(cs);
            var dmd = new ModelExporter().GenerateDmdContent(loaded.Tables["Thing"], []);

            dmd.Should().Contain("index (Code)").And.NotContain("Name, Code");
        });
    }

    [Fact]
    public async Task Load_SameModelInAnotherSchema_DoesNotDuplicateForeignKeys()
    {
        await WithDatabase(async cs =>
        {
            await ApplyToTwoSchemas(cs);

            var loaded = await new Shift { Logger = _logger }.LoadFromSqlAsync(cs);

            loaded.Tables["Task"].ForeignKeys.Should().ContainSingle();
        });
    }

    [Fact]
    public async Task Export_SameModelInAnotherSchema_WritesEachRelationshipOnce()
    {
        await WithDatabase(async cs =>
        {
            await ApplyToTwoSchemas(cs);

            var loaded = await new Shift { Logger = _logger }.LoadFromSqlAsync(cs);
            var dmd = new ModelExporter().GenerateDmdContent(loaded.Tables["Task"], []);

            dmd.Split('\n').Count(l => l.TrimStart().StartsWith("model User")).Should().Be(1, dmd);
        });
    }

    [Fact]
    public async Task Apply_FieldMadeNullable_ColumnBecomesNullable()
    {
        await WithDatabase(async cs =>
        {
            await ApplyCleanly(Files(TaskDmd), cs);

            var v2 = Files(TaskDmd.Replace("string(200) Title", "string(200)? Title"));
            await ApplyCleanly(v2, cs);

            (await IsNullable(cs, "Title")).Should().Be("YES");
            (await Replan(v2, cs)).Steps.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task Apply_FieldMadeNotNull_ColumnBecomesNotNull()
    {
        await WithDatabase(async cs =>
        {
            var nullableDmd = TaskDmd.Replace("string(200) Title", "string(200)? Title").Replace("index (Title)", "");
            await ApplyCleanly(Files(nullableDmd), cs);

            await ApplyCleanly(Files(TaskDmd.Replace("index (Title)", "")), cs);

            (await IsNullable(cs, "Title")).Should().Be("NO");
        });
    }

    [Fact]
    public async Task Apply_FieldMadeNotNullWithNullData_IsSkippedAndReported()
    {
        await WithDatabase(async cs =>
        {
            var nullableDmd = TaskDmd.Replace("string(200) Title", "string(200)? Title").Replace("index (Title)", "");
            await ApplyCleanly(Files(nullableDmd), cs);
            await Exec(cs, "INSERT [User] (Username) VALUES ('u'); INSERT [Task] (Title, AssignedUserUserID) VALUES (NULL, 1);");

            var result = await ApplyCleanly(Files(TaskDmd.Replace("index (Title)", "")), cs);

            result.Skipped.Should().ContainSingle(d => d.Kind == MigrationDiagnosticKind.NullsPresent && d.ColumnName == "Title");
            (await IsNullable(cs, "Title")).Should().Be("YES");
        });
    }

    /// <summary>Shift indexes every FK column, and an index blocks making a column NOT NULL.</summary>
    [Fact]
    public async Task Apply_ForeignKeyMadeRequired_IsSkippedAsBlockedByIndex()
    {
        await WithDatabase(async cs =>
        {
            await ApplyCleanly(Files(TaskDmd.Replace("model User as AssignedUser", "model User? as AssignedUser")), cs);

            var result = await ApplyCleanly(Files(TaskDmd), cs);

            result.Skipped.Should().ContainSingle(d =>
                d.Kind == MigrationDiagnosticKind.BlockedByDependency && d.ColumnName == "AssignedUserUserID");
            (await IsNullable(cs, "AssignedUserUserID")).Should().Be("YES");
        });
    }

    private (string, string)[] Files(string taskDmd) => [("User.dmd", UserDmd), ("Task.dmd", taskDmd)];

    private async Task ApplyToTwoSchemas(string cs)
    {
        var model = await LoadDmd(Files(TaskDmd));
        await ApplyCleanly(model, cs);
        await Exec(cs, "EXEC('CREATE SCHEMA [audit]')");
        await new Shift { Logger = _logger }.ApplyToSqlAsync(model, cs, "audit");
    }

    private static Task CreateThingWithIncludeIndex(string cs) => Exec(cs, """
        CREATE TABLE [dbo].[Thing] ([ThingID] int IDENTITY PRIMARY KEY, [Code] nvarchar(20) NOT NULL, [Name] nvarchar(50) NULL);
        CREATE NONCLUSTERED INDEX [IX_Thing_Code] ON [dbo].[Thing]([Code]) INCLUDE ([Name]);
        """);

    private static async Task<string> IsNullable(string cs, string column) =>
        (await Query(cs, $"SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Task' AND COLUMN_NAME = '{column}'")).Single();

    private async Task<DatabaseModel> LoadDmd((string Name, string Content)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("shift-drift-");
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

    private async Task<MigrationRunResult> Apply((string, string)[] files, string cs) =>
        await new Shift { Logger = _logger }.ApplyToSqlAsync(await LoadDmd(files), cs);

    private async Task<MigrationRunResult> ApplyCleanly((string, string)[] files, string cs) =>
        await ApplyCleanly(await LoadDmd(files), cs);

    private async Task<MigrationRunResult> ApplyCleanly(DatabaseModel model, string cs)
    {
        var result = await new Shift { Logger = _logger }.ApplyToSqlAsync(model, cs);
        result.Failures.Select(f => $"{f.Item1.Action} {f.Item1.TableName}: {f.Item2.Message}").Should().BeEmpty();
        return result;
    }

    private async Task<MigrationPlan> Replan((string, string)[] files, string cs)
    {
        var actual = await new Shift { Logger = _logger }.LoadFromSqlAsync(cs);
        return new MigrationPlanner().GeneratePlan(await LoadDmd(files), actual);
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

    private static async Task<List<string>> Query(string cs, string sql)
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