using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;

namespace Compile.Shift.Integration;

/// <summary>
/// Exercises base-type changes end to end against a real SQL Server instance: which conversions
/// are planned, which are refused, which are skipped because the live data would not survive, and
/// which are skipped because another object depends on the column.
///
/// These run against a container rather than a mock on purpose. SQL Server's behaviour here is not
/// obvious from the documentation — converting an int to a varchar that is too narrow stores '*'
/// instead of raising, and widening an indexed string succeeds while shrinking the same column
/// fails — so the guards are only meaningful if they are verified against the real engine.
/// </summary>
[Collection("SqlServer")]
public class SqlMigrationRunner_TypeConversion_Tests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public SqlMigrationRunner_TypeConversion_Tests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
    }

    #region Conversions that are applied

    /// <summary>
    /// Tests that every integer type on the allow-list converts to both variable-width string
    /// types, and that the stored value survives as text. The values are the boundary values of
    /// each integer type, so the widths in the allow-list are verified against what SQL Server
    /// actually renders rather than against arithmetic done by hand.
    /// </summary>
    [Theory]
    [InlineData("tinyint", "255", "varchar", 3, "255")]
    [InlineData("tinyint", "0", "nvarchar", 3, "0")]
    [InlineData("smallint", "-32768", "varchar", 6, "-32768")]
    [InlineData("smallint", "32767", "nvarchar", 6, "32767")]
    [InlineData("int", "-2147483648", "varchar", 11, "-2147483648")]
    [InlineData("int", "2147483647", "nvarchar", 11, "2147483647")]
    [InlineData("bigint", "-9223372036854775808", "varchar", 20, "-9223372036854775808")]
    [InlineData("bigint", "9223372036854775807", "nvarchar", 20, "9223372036854775807")]
    public async Task Converting_IntegerToVariableWidthString_ShouldApplyAndPreserveValue(
        string sourceType, string storedValue, string targetType, int targetWidth, string expectedText)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code {sourceType} NOT NULL)",
                $"INSERT INTO Widget (Code) VALUES ({storedValue})");

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", targetType, targetWidth, isNullable: false));

            Assert.Contains(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(result.Failures);

            var column = await GetColumnAsync(connectionString, "Widget", "Code");
            Assert.Equal(targetType, column.DataType, ignoreCase: true);
            Assert.Equal(targetWidth, column.MaxLength);
            Assert.Equal(expectedText, await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget"));
        });
    }

    /// <summary>
    /// Tests that a MAX target is accepted, since it can hold any rendering of any integer.
    /// </summary>
    [Fact]
    public async Task Converting_IntToVarcharMax_ShouldApplyAndPreserveValue()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (4242)");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", -1, isNullable: false));

            Assert.Empty(result.Failures);

            var column = await GetColumnAsync(connectionString, "Widget", "Code");
            Assert.Equal("varchar", column.DataType, ignoreCase: true);
            Assert.Equal(-1, column.MaxLength);
            Assert.Equal("4242", await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget"));
        });
    }

    /// <summary>
    /// Tests that a nullable integer column converts with its NULLs intact.
    /// </summary>
    [Fact]
    public async Task Converting_NullableIntWithNulls_ShouldApplyAndKeepNulls()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NULL)",
                "INSERT INTO Widget (Code) VALUES (7), (NULL)");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 20, isNullable: true));

            Assert.Empty(result.Failures);

            var column = await GetColumnAsync(connectionString, "Widget", "Code");
            Assert.Equal("varchar", column.DataType, ignoreCase: true);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(COUNT(*) AS varchar(10)) FROM Widget WHERE Code IS NULL"));
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget WHERE Code IS NOT NULL"));
        });
    }

    /// <summary>
    /// Tests that an empty table converts happily, on the strength of the allow-list alone.
    /// </summary>
    [Fact]
    public async Task Converting_IntOnEmptyTable_ShouldApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NULL)");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 11, isNullable: true));

            Assert.Empty(result.Failures);
            Assert.Equal("varchar", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    #endregion

    #region Conversions that are refused at plan time

    /// <summary>
    /// Tests that conversions off the allow-list produce no step and leave the column alone, for
    /// every direction and target shape that could plausibly be attempted. Fixed-width targets are
    /// excluded because SQL Server right-pads them; the reverse direction is excluded because
    /// arbitrary text does not convert to a number.
    /// </summary>
    [Theory]
    [InlineData("int", "42", "char", 20)]              // right-pads with spaces
    [InlineData("int", "42", "nchar", 20)]             // right-pads with spaces
    [InlineData("varchar(50)", "'abc'", "int", null)]  // reverse direction
    [InlineData("nvarchar(50)", "N'abc'", "int", null)]
    [InlineData("datetime", "'2020-01-01'", "varchar", 50)]
    [InlineData("bit", "1", "varchar", 10)]
    [InlineData("uniqueidentifier", "NEWID()", "varchar", 36)]
    [InlineData("float", "1.5", "varchar", 50)]
    public async Task Converting_OffAllowList_ShouldNotPlanOrChangeColumn(
        string sourceDeclaration, string storedValue, string targetType, int? targetWidth)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code {sourceDeclaration} NULL)",
                $"INSERT INTO Widget (Code) VALUES ({storedValue})");

            var expectedType = sourceDeclaration.Split('(')[0];

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", targetType, targetWidth, isNullable: true));

            Assert.DoesNotContain(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(result.Failures);
            Assert.Equal(expectedType, (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    #endregion

    #region Conversions refused because the target is too narrow for the source type

    /// <summary>
    /// Tests that a target too narrow to hold every value the source type can represent is refused
    /// at plan time, whatever is stored. This is the case that has to fail closed: SQL Server does
    /// not raise on a too-narrow integer conversion, it silently stores '*' in place of the number,
    /// so anything that lets such an alter through destroys the value and reports success.
    /// </summary>
    [Theory]
    [InlineData("int", "123456", 2)]
    [InlineData("int", "-2147483648", 10)]
    [InlineData("bigint", "-9223372036854775808", 19)]
    [InlineData("smallint", "-32768", 5)]
    // The width required is the type's, not the row's. Only the negative minimum needs 20
    // characters, so a bigint holding nothing but its 19-character positive maximum is still
    // refused at varchar(19) — the column could hold a negative value tomorrow. This is the
    // deliberate cost of not deciding from live data.
    [InlineData("bigint", "9223372036854775807", 19)]
    public async Task Converting_IntegerWiderThanTarget_ShouldRefuseAndLeaveColumnAndValueIntact(
        string sourceType, string storedValue, int targetWidth)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code {sourceType} NOT NULL)",
                $"INSERT INTO Widget (Code) VALUES ({storedValue})");

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", targetWidth, isNullable: false));

            // No step is emitted at all, so nothing reaches the runner to be judged against data.
            Assert.DoesNotContain(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Contains(plan.Diagnostics, d =>
                d.Kind == MigrationDiagnosticKind.TargetTooNarrow &&
                d.TableName == "Widget" &&
                d.ColumnName == "Code" &&
                d.ActualType == sourceType);
            Assert.Empty(result.Failures);

            Assert.Equal(sourceType, (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
            Assert.Equal(storedValue, await ScalarAsync(connectionString, "SELECT TOP 1 CAST(Code AS varchar(30)) FROM Widget"));
        });
    }

    /// <summary>
    /// Tests that the refusal does not depend on what is stored: a target too narrow for the type
    /// is refused even when every row would fit today, and even when there are no rows at all.
    ///
    /// This is the case the old data-driven probe got wrong. Deciding from live data made the same
    /// model apply on one database and be skipped on another, and left the value's survival resting
    /// on a probe that cannot see rows locked by another transaction or inserted a moment later.
    /// </summary>
    [Theory]
    [InlineData("INSERT INTO Widget (Code) VALUES (7), (-999)")]  // every value fits varchar(4)
    [InlineData("INSERT INTO Widget (Code) VALUES (1), (22), (333), (4444), (55555)")]  // one row too wide
    [InlineData(null)]  // no rows at all
    public async Task Converting_IntNarrowerThanTypeEvenWhenDataFits_ShouldRefuse(string? insertSql)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)");

            if (insertSql != null)
                await ExecuteAsync(connectionString, insertSql);

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 4, isNullable: false));

            Assert.DoesNotContain(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Contains(plan.Diagnostics, d => d.Kind == MigrationDiagnosticKind.TargetTooNarrow);
            Assert.Empty(result.Failures);
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that the sign counts toward the required width. An int needs 11 characters, not 10,
    /// because of the negative minimum — so varchar(10) is refused even for a table of small
    /// positive numbers, and varchar(11) is accepted.
    /// </summary>
    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    public async Task Converting_IntAtTheSignBoundary_ShouldRefuseBelowElevenCharacters(
        int targetWidth, bool shouldApply)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (7)");

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", targetWidth, isNullable: false));

            Assert.Empty(result.Failures);
            Assert.Equal(shouldApply, plan.Steps.Any(s => s.Action == MigrationAction.AlterColumn));
            Assert.Equal(
                shouldApply ? "varchar" : "int",
                (await GetColumnAsync(connectionString, "Widget", "Code")).DataType,
                ignoreCase: true);
        });
    }

    #endregion

    #region Conversions skipped because another object depends on the column

    /// <summary>
    /// Tests that a conversion is skipped, and the offending object named, for every kind of
    /// dependency that makes SQL Server reject the ALTER. Each of these was confirmed against SQL
    /// Server 2022 to fail with error 4922 (or 2749 for identity) when attempted, so the
    /// alternative to skipping is a guaranteed runtime failure.
    ///
    /// The diagnostic's text is asserted, not just the fact of the skip: naming the dependency is
    /// the entire reason for checking rather than letting the ALTER fail, so a skip that named the
    /// wrong object would be a silent regression.
    /// </summary>
    [Theory]
    [InlineData("CREATE NONCLUSTERED INDEX IX_Widget_Code ON Widget(Code)", "index [IX_Widget_Code]")]
    [InlineData("CREATE UNIQUE INDEX UX_Widget_Code ON Widget(Code)", "index [UX_Widget_Code]")]
    [InlineData("CREATE NONCLUSTERED INDEX IX_Widget_Other ON Widget(Id) INCLUDE (Code)", "index [IX_Widget_Other]")]
    [InlineData("ALTER TABLE Widget ADD CONSTRAINT CK_Widget_Code CHECK (Code > 0)", "check constraint [CK_Widget_Code]")]
    [InlineData("ALTER TABLE Widget ADD CONSTRAINT DF_Widget_Code DEFAULT 7 FOR Code", "default constraint [DF_Widget_Code]")]
    [InlineData("ALTER TABLE Widget ADD Doubled AS (Code * 2)", "computed column [Doubled]")]
    [InlineData("CREATE STATISTICS ST_Widget_Code ON Widget(Code)", "statistics [ST_Widget_Code]")]
    public async Task Converting_ColumnWithDependentObject_ShouldSkipAndNameTheDependency(
        string dependencySql, string expectedBlocker)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (42)",
                dependencySql);

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            // The step is planned, then skipped by the runner rather than attempted and failed.
            Assert.Contains(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(result.Failures);

            var diagnostic = Assert.Single(result.Skipped);
            Assert.Equal(MigrationDiagnosticKind.BlockedByDependency, diagnostic.Kind);
            Assert.Equal("Widget", diagnostic.TableName);
            Assert.Equal("Code", diagnostic.ColumnName);
            Assert.Equal("int", diagnostic.ActualType);
            Assert.Equal("varchar", diagnostic.TargetType);
            Assert.Contains(expectedBlocker, diagnostic.Reason);

            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests the identity case, which SQL Server rejects with a different error (2749) and which
    /// the model loader cannot currently see, since it does not populate IsIdentity. Reading the
    /// live catalog rather than the model is what makes this detectable.
    /// </summary>
    [Fact]
    public async Task Converting_IdentityColumn_ShouldSkipAndLeaveColumnIntact()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Code int IDENTITY(1,1) NOT NULL, Other int NULL)",
                "INSERT INTO Widget (Other) VALUES (1)");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            AssertBlockedBy(result, "the IDENTITY property", "Widget", "Code", "int", "varchar");
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests the primary key case. The PK's backing index is what blocks the alter, so that is what
    /// the diagnostic has to name — SQL Server generates the index name, hence the prefix match.
    /// </summary>
    [Fact]
    public async Task Converting_PrimaryKeyColumn_ShouldSkipAndLeaveColumnIntact()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Code int NOT NULL PRIMARY KEY, Other int NULL)",
                "INSERT INTO Widget (Code) VALUES (42)");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            AssertBlockedBy(result, "index [PK__Widget", "Widget", "Code", "int", "varchar");
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests a column on the child side of a foreign key.
    /// </summary>
    [Fact]
    public async Task Converting_ForeignKeyChildColumn_ShouldSkipAndLeaveColumnIntact()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Parent (Id int NOT NULL PRIMARY KEY)",
                "INSERT INTO Parent (Id) VALUES (42)",
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL CONSTRAINT FK_Widget_Parent FOREIGN KEY REFERENCES Parent(Id))",
                "INSERT INTO Widget (Code) VALUES (42)");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            AssertBlockedBy(result, "foreign key [FK_Widget_Parent]", "Widget", "Code", "int", "varchar");
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests a column another table's foreign key points at. The dependency runs the other way
    /// here, so it is only visible by looking at referenced_column_id as well as parent_column_id.
    /// </summary>
    [Fact]
    public async Task Converting_ForeignKeyReferencedColumn_ShouldSkipAndLeaveColumnIntact()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Code int NOT NULL PRIMARY KEY)",
                "INSERT INTO Widget (Code) VALUES (42)",
                "CREATE TABLE Child (Id int IDENTITY(1,1) PRIMARY KEY, Ref int NOT NULL CONSTRAINT FK_Child_Widget FOREIGN KEY REFERENCES Widget(Code))");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            // The column is also this table's PK, so its backing index blocks too. What matters is
            // that the foreign key pointing *at* the column is among the objects named.
            AssertBlockedBy(result, "foreign key [FK_Child_Widget]", "Widget", "Code", "int", "varchar");
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests a schema-bound view over the column, which binds to the column's type and so blocks
    /// the alter.
    /// </summary>
    [Fact]
    public async Task Converting_ColumnUnderSchemaBoundView_ShouldSkipAndLeaveColumnIntact()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (42)");
            await ExecuteAsync(connectionString,
                "CREATE VIEW V_Widget WITH SCHEMABINDING AS SELECT Id, Code FROM dbo.Widget");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            AssertBlockedBy(result, "schema-bound object [V_Widget]", "Widget", "Code", "int", "varchar");
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that dependencies which do not actually block are not treated as blockers. A view
    /// without SCHEMABINDING and auto-created statistics both leave the alter free to proceed, and
    /// refusing on them would strand columns that can migrate perfectly well.
    /// </summary>
    [Fact]
    public async Task Converting_ColumnWithNonBlockingDependencies_ShouldStillApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (42)");
            await ExecuteAsync(connectionString,
                "CREATE VIEW V_Widget AS SELECT Id, Code FROM dbo.Widget");
            // Provoke an auto-created statistic on the column.
            await ExecuteAsync(connectionString, "SELECT * FROM Widget WHERE Code = 42");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            AssertApplied(result);
            Assert.Equal("varchar", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
            Assert.Equal("42", await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget"));
        });
    }

    /// <summary>
    /// Tests that an index on a different column of the same table does not block the conversion.
    /// </summary>
    [Fact]
    public async Task Converting_ColumnWhereIndexCoversADifferentColumn_ShouldStillApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL, Other int NOT NULL)",
                "CREATE NONCLUSTERED INDEX IX_Widget_Other ON Widget(Other)",
                "INSERT INTO Widget (Code, Other) VALUES (42, 1)");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            AssertApplied(result);
            Assert.Equal("varchar", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    #endregion

    #region decimal and numeric, which are one type with two spellings

    /// <summary>
    /// Tests that a precision change on a numeric column still applies when nothing depends on it.
    /// The planner treats decimal and numeric as compatible, but the two spellings differ as
    /// strings, so the runner classifies this as a base-type change and runs the dependency check
    /// over it. With no dependencies present it must still go through.
    /// </summary>
    [Fact]
    public async Task Numeric_PrecisionChangeToDecimal_ShouldApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Amount numeric(18,2) NOT NULL)",
                "INSERT INTO Widget (Amount) VALUES (1.23)");

            var (_, result) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            Assert.Empty(result.Failures);

            var column = await GetColumnAsync(connectionString, "Widget", "Amount");
            Assert.Equal("decimal", column.DataType, ignoreCase: true);
            Assert.Equal("1.2300", await ScalarAsync(connectionString, "SELECT TOP 1 CAST(Amount AS varchar(20)) FROM Widget"));
        });
    }

    /// <summary>
    /// Tests that a numeric-to-decimal precision change is skipped when any dependent object is
    /// present. SQL Server rejects this with error 4922 — changing the spelling counts as a type
    /// change to the engine, and even a bare default constraint is enough to block it — so
    /// skipping and naming the dependency is the informative outcome rather than a lost migration.
    /// A default constraint is included explicitly because it does *not* block a same-spelling
    /// precision change, which is what makes it the surprising case.
    /// </summary>
    [Theory]
    [InlineData("default constraint [DF_Widget_Amount]", "ALTER TABLE Widget ADD CONSTRAINT DF_Widget_Amount DEFAULT 0 FOR Amount")]
    [InlineData("index [IX_Widget_Amount]", "CREATE NONCLUSTERED INDEX IX_Widget_Amount ON Widget(Amount)")]
    [InlineData("check constraint [CK_Widget_Amount]", "ALTER TABLE Widget ADD CONSTRAINT CK_Widget_Amount CHECK (Amount >= 0)")]
    [InlineData("statistics [ST_Widget_Amount]", "CREATE STATISTICS ST_Widget_Amount ON Widget(Amount)")]
    [InlineData("computed column [Doubled]", "ALTER TABLE Widget ADD Doubled AS (Amount * 2)")]
    public async Task Numeric_PrecisionChangeToDecimalWithDependentObject_ShouldSkip(
        string expectedBlocker, string dependencySql)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Amount numeric(18,2) NOT NULL)",
                "INSERT INTO Widget (Amount) VALUES (1.23)",
                dependencySql);

            var (_, result) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            AssertBlockedBy(result, expectedBlocker, "Widget", "Amount", "numeric", "decimal");
            Assert.Equal("numeric", (await GetColumnAsync(connectionString, "Widget", "Amount")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that a same-spelling precision change is not subjected to the dependency check at all.
    /// SQL Server applies decimal(18,2) to decimal(19,4) with a default constraint in place, so
    /// treating the default as a blocker here would refuse a migration that works.
    /// </summary>
    [Fact]
    public async Task Decimal_PrecisionChangeWithDefaultConstraint_ShouldStillApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Amount decimal(18,2) NOT NULL CONSTRAINT DF_Widget_Amount DEFAULT 0)",
                "INSERT INTO Widget DEFAULT VALUES");

            var (_, result) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            Assert.Empty(result.Failures);

            var column = await GetColumnAsync(connectionString, "Widget", "Amount");
            Assert.Equal("decimal", column.DataType, ignoreCase: true);
            Assert.Equal("19", await ScalarAsync(connectionString,
                "SELECT CAST(NUMERIC_PRECISION AS varchar(10)) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Widget' AND COLUMN_NAME='Amount'"));
        });
    }

    /// <summary>
    /// Tests that an identity column converting to a type that can also carry an identity is not
    /// refused. SQL Server allows a numeric(18,0) identity to become decimal(19,0), because
    /// decimal with a scale of 0 is a legal identity type, so treating IDENTITY as an
    /// unconditional blocker would strand this column.
    /// </summary>
    [Fact]
    public async Task Numeric_IdentityColumnToDecimalWithScaleZero_ShouldApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Amount numeric(18,0) IDENTITY(1,1) NOT NULL, Other int NULL)",
                "INSERT INTO Widget (Other) VALUES (1)");

            var (_, result) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 0));

            AssertApplied(result);
            Assert.Equal("decimal", (await GetColumnAsync(connectionString, "Widget", "Amount")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that an identity column is still refused when the target cannot carry an identity, so
    /// narrowing the identity blocker did not open the door to a conversion SQL Server rejects
    /// with error 2749.
    /// </summary>
    [Fact]
    public async Task Numeric_IdentityColumnToDecimalWithNonZeroScale_ShouldSkip()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Amount numeric(18,0) IDENTITY(1,1) NOT NULL, Other int NULL)",
                "INSERT INTO Widget (Other) VALUES (1)");

            var (_, result) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            AssertBlockedBy(result, "the IDENTITY property", "Widget", "Amount", "numeric", "decimal");
            Assert.Equal("numeric", (await GetColumnAsync(connectionString, "Widget", "Amount")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that an identity column is refused when the target is nullable, even though its type
    /// would otherwise be identity-capable. SQL Server will not carry an IDENTITY on a nullable
    /// column and fails with error 8147, so a model declaring the column nullable must be caught
    /// by the dependency check rather than left to fail at execution time.
    /// </summary>
    [Fact]
    public async Task Numeric_IdentityColumnToNullableDecimal_ShouldSkip()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Amount numeric(18,0) IDENTITY(1,1) NOT NULL, Other int NULL)",
                "INSERT INTO Widget (Other) VALUES (1)");

            var nullableIdentity = new FieldModel
            {
                Name = "Amount",
                Type = "decimal",
                Precision = 19,
                Scale = 0,
                IsNullable = true
            };

            var (_, result) = await PlanAndRunAsync(connectionString, ModelWith("Widget", nullableIdentity));

            AssertBlockedBy(result, "the IDENTITY property", "Widget", "Amount", "numeric", "decimal");
            Assert.Equal("numeric", (await GetColumnAsync(connectionString, "Widget", "Amount")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that dependencies which do not block a base-type change do not block this one either.
    /// </summary>
    [Fact]
    public async Task Numeric_PrecisionChangeWithNonBlockingDependencies_ShouldApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Amount numeric(18,2) NOT NULL)",
                "INSERT INTO Widget (Amount) VALUES (1.23)");
            await ExecuteAsync(connectionString,
                "CREATE VIEW V_Widget AS SELECT Id, Amount FROM dbo.Widget");
            await ExecuteAsync(connectionString, "SELECT * FROM Widget WHERE Amount = 1.23");

            var (_, result) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            AssertApplied(result);
            Assert.Equal("decimal", (await GetColumnAsync(connectionString, "Widget", "Amount")).DataType, ignoreCase: true);
        });
    }

    #endregion

    #region Existing behaviour that must not regress

    /// <summary>
    /// Tests that widening an indexed string column still applies. SQL Server allows this even
    /// though it rejects a base-type change on the same column, so the dependency check must not
    /// be applied to plain resizes.
    /// </summary>
    [Fact]
    public async Task Widening_IndexedStringColumn_ShouldStillApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code varchar(50) NOT NULL)",
                "CREATE NONCLUSTERED INDEX IX_Widget_Code ON Widget(Code)",
                "INSERT INTO Widget (Code) VALUES ('abc')");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 100, isNullable: false));

            AssertApplied(result);

            var column = await GetColumnAsync(connectionString, "Widget", "Code");
            Assert.Equal("varchar", column.DataType, ignoreCase: true);
            Assert.Equal(100, column.MaxLength);
        });
    }

    /// <summary>
    /// Tests that widening a string column carrying a default constraint still applies, which SQL
    /// Server permits even though the same default blocks a base-type change.
    /// </summary>
    [Fact]
    public async Task Widening_StringColumnWithDefaultConstraint_ShouldStillApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code varchar(50) NOT NULL CONSTRAINT DF_Widget_Code DEFAULT 'z')",
                "INSERT INTO Widget DEFAULT VALUES");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 100, isNullable: false));

            AssertApplied(result);
            Assert.Equal(100, (await GetColumnAsync(connectionString, "Widget", "Code")).MaxLength);
        });
    }

    /// <summary>
    /// Tests that a string shrink which would truncate live data is still refused.
    /// </summary>
    [Fact]
    public async Task Shrinking_StringColumnHoldingLongerValue_ShouldStillSkip()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code varchar(50) NOT NULL)",
                "INSERT INTO Widget (Code) VALUES ('abcdefghij')");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 5, isNullable: false));

            // A resize, so the runner decides it from the data rather than the type - and says so.
            Assert.Empty(result.Failures);
            Assert.Empty(result.Applied);
            var diagnostic = Assert.Single(result.Skipped);
            Assert.Equal(MigrationDiagnosticKind.DataLossRisk, diagnostic.Kind);
            Assert.Equal("Widget", diagnostic.TableName);
            Assert.Equal("Code", diagnostic.ColumnName);

            Assert.Equal(50, (await GetColumnAsync(connectionString, "Widget", "Code")).MaxLength);
        });
    }

    /// <summary>
    /// Tests that varchar to nvarchar migrates end to end, values intact. This is the conversion a
    /// dmd field asks for when it changes from astring(n) to ustring(n).
    /// </summary>
    [Theory]
    [InlineData(50, 50)]
    [InlineData(50, 100)]
    public async Task Converting_VarcharToNvarchar_ShouldApplyAndPreserveValue(
        int actualWidth, int targetWidth)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code varchar({actualWidth}) NOT NULL)",
                "INSERT INTO Widget (Code) VALUES ('abc')");

            var (_, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "nvarchar", targetWidth, isNullable: false));

            AssertApplied(result);

            var column = await GetColumnAsync(connectionString, "Widget", "Code");
            Assert.Equal("nvarchar", column.DataType, ignoreCase: true);
            Assert.Equal(targetWidth, column.MaxLength);
            Assert.Equal("abc", await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget"));
        });
    }

    /// <summary>
    /// Tests that a narrowing varchar to nvarchar is decided from the live data rather than refused
    /// from the type. This conversion fails closed — SQL Server raises on truncation — so a column
    /// whose values all fit is migrated, and one holding a longer value is skipped.
    ///
    /// The skip is the case that used to be measured wrongly: the byte limit was derived from the
    /// target's unicode-ness (20 characters read as 40 bytes) while DATALENGTH was reporting the
    /// source's single-byte storage, so a 25-character value cleared a limit it does not fit.
    /// </summary>
    [Theory]
    [InlineData("'abc'", true)]
    [InlineData("'abcdefghijklmnopqrstuvwxy'", false)]  // 25 characters, 25 bytes as varchar
    public async Task Converting_VarcharToNarrowerNvarchar_ShouldBeDecidedFromTheData(
        string storedValue, bool shouldApply)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code varchar(50) NOT NULL)",
                $"INSERT INTO Widget (Code) VALUES ({storedValue})");

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "nvarchar", 20, isNullable: false));

            // Planned either way - the refusal, when it comes, is the runner's call on the rows.
            Assert.Contains(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(result.Failures);

            var column = await GetColumnAsync(connectionString, "Widget", "Code");

            if (shouldApply)
            {
                AssertApplied(result);
                Assert.Equal("nvarchar", column.DataType, ignoreCase: true);
                Assert.Equal(20, column.MaxLength);
            }
            else
            {
                Assert.Empty(result.Applied);
                Assert.Equal(MigrationDiagnosticKind.DataLossRisk, Assert.Single(result.Skipped).Kind);
                Assert.Equal("varchar", column.DataType, ignoreCase: true);
                Assert.Equal(50, column.MaxLength);
            }
        });
    }

    /// <summary>
    /// Tests that the reverse direction is refused. SQL Server performs nvarchar to varchar without
    /// complaint, replacing every character outside the target collation's code page with '?', so
    /// it is off the allow-list and reported instead.
    /// </summary>
    [Fact]
    public async Task Converting_NvarcharToVarchar_ShouldRefuseAndLeaveColumnIntact()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code nvarchar(50) NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (N'你好')");

            var (plan, result) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.DoesNotContain(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Contains(plan.Diagnostics, d =>
                d.Kind == MigrationDiagnosticKind.UnsupportedTypeChange &&
                d.ActualType == "nvarchar" &&
                d.TargetType == "varchar");
            Assert.Empty(result.Failures);

            Assert.Equal("nvarchar", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
            Assert.Equal("你好", await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget"));
        });
    }

    #endregion

    #region Round-trip equivalence against real column metadata

    /// <summary>
    /// Tests that a legacy type whose target is exactly Shift's own round-trip produces no plan and
    /// no drift report. These assertions run against metadata loaded from a real database, so they
    /// also pin down what SQL Server reports for text, ntext, money and smallmoney columns — the
    /// precisions the exemption depends on.
    /// </summary>
    [Theory]
    [InlineData("text", "varchar", -1)]
    [InlineData("ntext", "nvarchar", -1)]
    public async Task RoundTrip_LegacyStringTypeAtMaxWidth_ShouldNotPlanOrReport(
        string sourceType, string targetType, int targetPrecision)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code {sourceType} NULL)");

            var (plan, _) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", targetType, targetPrecision, isNullable: true));

            Assert.Empty(plan.Steps);
            Assert.Empty(plan.Diagnostics);
        });
    }

    /// <summary>
    /// Tests that money and smallmoney at their round-trip precision are likewise exempt.
    /// </summary>
    [Theory]
    [InlineData("money", 19, 4)]
    [InlineData("smallmoney", 10, 4)]
    public async Task RoundTrip_MoneyAtRoundTripPrecision_ShouldNotPlanOrReport(
        string sourceType, int targetPrecision, int targetScale)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Amount {sourceType} NULL)");

            var field = new FieldModel
            {
                Name = "Amount",
                Type = "decimal",
                Precision = targetPrecision,
                Scale = targetScale,
                IsNullable = true
            };

            var (plan, _) = await PlanAndRunAsync(connectionString, ModelWith("Widget", field));

            Assert.Empty(plan.Steps);
            Assert.Empty(plan.Diagnostics);
        });
    }

    /// <summary>
    /// Tests that the exemption is precision-aware: narrowing a legacy text column to a bounded
    /// varchar, or retyping money to a different decimal shape, is a real change of intent and is
    /// reported rather than swallowed with the round-trip noise.
    /// </summary>
    [Theory]
    [InlineData("text", "varchar", 50, null)]
    [InlineData("ntext", "nvarchar", 50, null)]
    [InlineData("money", "decimal", 18, 4)]
    [InlineData("smallmoney", "decimal", 19, 4)]
    public async Task RoundTrip_SameDmdTypeAtDifferentPrecision_ShouldBeReported(
        string sourceType, string targetType, int targetPrecision, int? targetScale)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code {sourceType} NULL)");

            var field = new FieldModel
            {
                Name = "Code",
                Type = targetType,
                Precision = targetPrecision,
                Scale = targetScale,
                IsNullable = true
            };

            var (plan, _) = await PlanAndRunAsync(connectionString, ModelWith("Widget", field));

            Assert.Empty(plan.Steps);
            Assert.Contains(plan.Diagnostics, d =>
                d.Kind == MigrationDiagnosticKind.UnsupportedTypeChange &&
                d.TableName == "Widget" &&
                d.ColumnName == "Code" &&
                d.ActualType == sourceType &&
                d.TargetType == targetType);
        });
    }

    #endregion

    #region Reporting the outcome through ApplyToSqlAsync

    /// <summary>
    /// Tests that one result describes everything the model asked for that did not happen, whichever
    /// stage declined it. The planner's refusals never became steps and the runner's skips are not
    /// failures, so this is the only place the two are visible together — and the only thing a
    /// caller (or the CLI, once it consumes this) can act on.
    /// </summary>
    [Fact]
    public async Task ApplyToSqlAsync_WithARefusalAndASkip_ShouldReportBothAsUnappliedWork()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Blocked int NOT NULL, TooNarrow int NOT NULL)",
                "CREATE NONCLUSTERED INDEX IX_Widget_Blocked ON Widget(Blocked)",
                "INSERT INTO Widget (Blocked, TooNarrow) VALUES (42, 42)");

            var model = new DatabaseModel();
            model.Tables["Widget"] = new TableModel
            {
                Name = "Widget",
                Fields =
                {
                    // Planned, then skipped by the runner: an index depends on the column.
                    new FieldModel { Name = "Blocked", Type = "varchar", Precision = 50, IsNullable = false },
                    // Never planned: varchar(4) cannot hold every int.
                    new FieldModel { Name = "TooNarrow", Type = "varchar", Precision = 4, IsNullable = false }
                }
            };

            var shift = new Shift { Logger = _logger };
            var result = await shift.ApplyToSqlAsync(model, connectionString);

            Assert.Empty(result.Failures);
            Assert.Empty(result.Applied);
            Assert.True(result.HasUnappliedWork);

            // The planner's refusals are carried onto the result ahead of the runner's skips.
            Assert.Equal(2, result.Skipped.Count);
            Assert.Contains(result.Skipped, d =>
                d.Kind == MigrationDiagnosticKind.TargetTooNarrow && d.ColumnName == "TooNarrow");
            Assert.Contains(result.Skipped, d =>
                d.Kind == MigrationDiagnosticKind.BlockedByDependency && d.ColumnName == "Blocked");

            // Both columns are untouched, which is what the result is claiming.
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Blocked")).DataType, ignoreCase: true);
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "TooNarrow")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that a run in which every step was skipped does not announce itself as a completed
    /// apply. The counts come from what the runner executed, not from what the plan asked for:
    /// reporting the plan's steps had the log say "AlterColumn 1" for a column it left alone,
    /// directly above the warning saying it had left it alone.
    /// </summary>
    [Fact]
    public async Task ApplyToSqlAsync_WhenEveryStepIsSkipped_ShouldNotLogApplyCompleted()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "CREATE NONCLUSTERED INDEX IX_Widget_Code ON Widget(Code)",
                "INSERT INTO Widget (Code) VALUES (42)");

            var (logger, messages) = CreateLoggerCapturingMessages();
            var shift = new Shift { Logger = logger };

            var result = await shift.ApplyToSqlAsync(
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false), connectionString);

            Assert.Empty(result.Applied);
            Assert.True(result.HasUnappliedWork);

            Assert.DoesNotContain(messages, m => m == "Apply completed");
            // The effects summary, which is "{action} {count}" exactly - not the runner's own
            // per-step progress lines, which also begin with the action name.
            Assert.DoesNotContain(messages, m => m == "AlterColumn 1");
            Assert.Contains(messages, m => m == "Apply made no changes");
            Assert.Contains(messages, m => m.Contains("1 column change(s) not applied"));
        });
    }

    /// <summary>
    /// Tests the positive case, so the assertions above cannot pass simply because the wording
    /// never appears: a run that does apply something reports it as applied and claims no
    /// unapplied work.
    /// </summary>
    [Fact]
    public async Task ApplyToSqlAsync_WhenTheConversionApplies_ShouldReportItAsApplied()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (42)");

            var (logger, messages) = CreateLoggerCapturingMessages();
            var shift = new Shift { Logger = logger };

            var result = await shift.ApplyToSqlAsync(
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false), connectionString);

            Assert.Empty(result.Failures);
            Assert.Empty(result.Skipped);
            Assert.False(result.HasUnappliedWork);
            Assert.Equal(MigrationAction.AlterColumn, Assert.Single(result.Applied).Action);

            Assert.Contains(messages, m => m == "Apply completed");
            Assert.Contains(messages, m => m == "AlterColumn 1");

            Assert.Equal("varchar", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that a model matching the database reports no unapplied work at all, so
    /// HasUnappliedWork stays a usable signal rather than one that is always true.
    /// </summary>
    [Fact]
    public async Task ApplyToSqlAsync_WithNothingToDo_ShouldReportNoUnappliedWork()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Code varchar(50) NULL)");

            var (logger, messages) = CreateLoggerCapturingMessages();
            var shift = new Shift { Logger = logger };

            var result = await shift.ApplyToSqlAsync(
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: true), connectionString);

            Assert.Empty(result.Applied);
            Assert.Empty(result.Skipped);
            Assert.Empty(result.Failures);
            Assert.False(result.HasUnappliedWork);

            Assert.Contains(messages, m => m == "Already up-to date");
        });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Asserts the run skipped exactly one alter because another object depends on the column, and
    /// that the diagnostic names the object responsible.
    ///
    /// Naming it is the entire reason for reading the catalog rather than letting the ALTER fail,
    /// so a skip naming the wrong object would be a silent regression. Asserting the column was
    /// left alone is not enough on its own: that also holds when the step was never planned, or was
    /// skipped for some other reason entirely.
    /// </summary>
    private static void AssertBlockedBy(
        MigrationRunResult result, string expectedBlocker, string table, string column, string actualType, string targetType)
    {
        Assert.Empty(result.Failures);
        Assert.Empty(result.Applied);

        var diagnostic = Assert.Single(result.Skipped);
        Assert.Equal(MigrationDiagnosticKind.BlockedByDependency, diagnostic.Kind);
        Assert.Equal(table, diagnostic.TableName);
        Assert.Equal(column, diagnostic.ColumnName);
        Assert.Equal(actualType, diagnostic.ActualType);
        Assert.Equal(targetType, diagnostic.TargetType);
        Assert.Contains(expectedBlocker, diagnostic.Reason);
    }

    /// <summary>
    /// Asserts the run applied its single step and declined nothing, so a test claiming a
    /// conversion went through cannot pass on a run that quietly skipped it.
    /// </summary>
    private static void AssertApplied(MigrationRunResult result)
    {
        Assert.Empty(result.Failures);
        Assert.Empty(result.Skipped);
        Assert.Single(result.Applied);
    }

    /// <summary>
    /// A logger that collects the messages it is given, so a test can assert on what an operator
    /// watching an apply would actually have seen.
    /// </summary>
    private static (ILogger Logger, List<string> Messages) CreateLoggerCapturingMessages()
    {
        var messages = new List<string>();
        var logger = new Mock<ILogger>();

        logger
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
            .Callback(new InvocationAction(invocation =>
                messages.Add(invocation.Arguments[2]?.ToString() ?? string.Empty)));

        return (logger.Object, messages);
    }

    /// <summary>
    /// Runs a test body against a freshly created database, dropping it afterwards whatever
    /// happens.
    /// </summary>
    private async Task WithDatabaseAsync(Func<string, Task> body)
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            await body(connectionString);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    /// <summary>
    /// Loads the live schema, plans against the supplied target, and runs the plan — the same
    /// sequence ApplyToSqlAsync performs. Both halves of the outcome are returned: the plan
    /// carries what the planner refused, the result what the runner refused.
    /// </summary>
    private async Task<(MigrationPlan Plan, MigrationRunResult Result)> PlanAndRunAsync(
        string connectionString, DatabaseModel targetModel)
    {
        var shift = new Shift { Logger = _logger };
        var actual = await shift.LoadFromSqlAsync(connectionString);

        var planner = new MigrationPlanner { Logger = _logger };
        var plan = planner.GeneratePlan(targetModel, actual);

        var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = _logger };
        return (plan, runner.Run());
    }

    private static DatabaseModel SingleFieldModel(string table, string column, string type, int? precision, bool isNullable) =>
        ModelWith(table, new FieldModel
        {
            Name = column,
            Type = type,
            Precision = precision,
            IsNullable = isNullable
        });

    private static DatabaseModel DecimalModel(string type, int precision, int scale) =>
        ModelWith("Widget", new FieldModel
        {
            Name = "Amount",
            Type = type,
            Precision = precision,
            Scale = scale,
            IsNullable = false
        });

    private static DatabaseModel ModelWith(string table, FieldModel field)
    {
        var model = new DatabaseModel();
        model.Tables[table] = new TableModel
        {
            Name = table,
            Fields = { field }
        };
        return model;
    }

    private static async Task ExecuteAsync(string connectionString, params string[] statements)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var statement in statements)
        {
            await using var command = new SqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<(string DataType, int? MaxLength)> GetColumnAsync(
        string connectionString, string table, string column)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @table AND COLUMN_NAME = @column";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@column", column);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"Column {table}.{column} was not found");

        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetInt32(1));
    }

    private static async Task<string?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    #endregion
}