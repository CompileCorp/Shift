using Compile.Shift.Helpers;
using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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
    // Exact boundary: the positive maximum is 19 characters and must not be over-refused.
    [InlineData("bigint", "9223372036854775807", "varchar", 19, "9223372036854775807")]
    public async Task Converting_IntegerToVariableWidthString_ShouldApplyAndPreserveValue(
        string sourceType, string storedValue, string targetType, int targetWidth, string expectedText)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code {sourceType} NOT NULL)",
                $"INSERT INTO Widget (Code) VALUES ({storedValue})");

            var (plan, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", targetType, targetWidth, isNullable: false));

            Assert.Contains(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(failures);

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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", -1, isNullable: false));

            Assert.Empty(failures);

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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 20, isNullable: true));

            Assert.Empty(failures);

            var column = await GetColumnAsync(connectionString, "Widget", "Code");
            Assert.Equal("varchar", column.DataType, ignoreCase: true);
            Assert.Equal("1", await ScalarAsync(connectionString, "SELECT CAST(COUNT(*) AS varchar(10)) FROM Widget WHERE Code IS NULL"));
            Assert.Equal("7", await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget WHERE Code IS NOT NULL"));
        });
    }

    /// <summary>
    /// Tests that an empty table converts happily: there is no data to measure, so the alter is
    /// applied on the strength of the allow-list alone.
    /// </summary>
    [Fact]
    public async Task Converting_IntOnEmptyTable_ShouldApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NULL)");

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 2, isNullable: true));

            Assert.Empty(failures);
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

            var (plan, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", targetType, targetWidth, isNullable: true));

            Assert.DoesNotContain(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(failures);
            Assert.Equal(expectedType, (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    #endregion

    #region Conversions skipped by the live-data probe

    /// <summary>
    /// Tests that a target too narrow for the stored data is skipped rather than applied. This is
    /// the case that makes the probe load-bearing: SQL Server does not raise on this conversion,
    /// it silently stores '*' in place of the number, so without the probe the value would be
    /// destroyed and the apply would report success.
    /// </summary>
    [Theory]
    [InlineData("int", "123456", 2)]
    [InlineData("int", "-2147483648", 10)]
    // The negative minimum is the only bigint needing 20 characters; the positive maximum is 19.
    [InlineData("bigint", "-9223372036854775808", 19)]
    [InlineData("smallint", "-32768", 5)]
    public async Task Converting_IntegerWiderThanTarget_ShouldSkipAndLeaveColumnAndValueIntact(
        string sourceType, string storedValue, int targetWidth)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                $"CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code {sourceType} NOT NULL)",
                $"INSERT INTO Widget (Code) VALUES ({storedValue})");

            var (plan, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", targetWidth, isNullable: false));

            // The planner still emits the step; the runner is what refuses it.
            Assert.Contains(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(failures);

            Assert.Equal(sourceType, (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
            Assert.Equal(storedValue, await ScalarAsync(connectionString, "SELECT TOP 1 CAST(Code AS varchar(30)) FROM Widget"));
        });
    }

    /// <summary>
    /// Tests that the probe measures the widest row rather than an arbitrary one: a table where
    /// only one row is too wide must still be refused.
    /// </summary>
    [Fact]
    public async Task Converting_IntWhereOnlyOneRowIsTooWide_ShouldSkip()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (1), (22), (333), (4444), (55555)");

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 4, isNullable: false));

            Assert.Empty(failures);
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that the negative sign counts toward the width. -999 needs four characters, so a
    /// varchar(3) target must be refused even though the digits alone would fit.
    /// </summary>
    [Fact]
    public async Task Converting_NegativeIntWhereSignPushesItOverTheWidth_ShouldSkip()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (-999)");

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 3, isNullable: false));

            Assert.Empty(failures);
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests that a target narrower than the type's widest possible value is still applied when
    /// every stored value happens to fit, matching how string shrinks behave.
    /// </summary>
    [Fact]
    public async Task Converting_IntNarrowerThanTypeButWideEnoughForData_ShouldApply()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (7), (-999)");

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 4, isNullable: false));

            Assert.Empty(failures);
            Assert.Equal("varchar", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
            Assert.Equal("-999", await ScalarAsync(connectionString, "SELECT TOP 1 Code FROM Widget WHERE Code = '-999'"));
        });
    }

    #endregion

    #region Conversions skipped because another object depends on the column

    /// <summary>
    /// Tests that a conversion is skipped, and the dependency named, for every kind of object that
    /// makes SQL Server reject the ALTER. Each of these was confirmed against SQL Server 2022 to
    /// fail with error 4922 (or 2749 for identity) when attempted, so the alternative to skipping
    /// is a guaranteed runtime failure.
    /// </summary>
    [Theory]
    [InlineData("nonclustered index", "CREATE NONCLUSTERED INDEX IX_Widget_Code ON Widget(Code)")]
    [InlineData("unique index", "CREATE UNIQUE INDEX UX_Widget_Code ON Widget(Code)")]
    [InlineData("index include", "CREATE NONCLUSTERED INDEX IX_Widget_Other ON Widget(Id) INCLUDE (Code)")]
    [InlineData("check constraint", "ALTER TABLE Widget ADD CONSTRAINT CK_Widget_Code CHECK (Code > 0)")]
    [InlineData("default constraint", "ALTER TABLE Widget ADD CONSTRAINT DF_Widget_Code DEFAULT 7 FOR Code")]
    [InlineData("computed column", "ALTER TABLE Widget ADD Doubled AS (Code * 2)")]
    [InlineData("user statistics", "CREATE STATISTICS ST_Widget_Code ON Widget(Code)")]
    public async Task Converting_ColumnWithDependentObject_ShouldSkipAndLeaveColumnIntact(
        string _, string dependencySql)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Code int NOT NULL)",
                "INSERT INTO Widget (Code) VALUES (42)",
                dependencySql);

            var (plan, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            // The step is planned, then skipped by the runner rather than attempted and failed.
            Assert.Contains(plan.Steps, s => s.Action == MigrationAction.AlterColumn);
            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.Empty(failures);
            Assert.Equal("int", (await GetColumnAsync(connectionString, "Widget", "Code")).DataType, ignoreCase: true);
        });
    }

    /// <summary>
    /// Tests the primary key case. The PK's backing index is what blocks the alter.
    /// </summary>
    [Fact]
    public async Task Converting_PrimaryKeyColumn_ShouldSkipAndLeaveColumnIntact()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Code int NOT NULL PRIMARY KEY, Other int NULL)",
                "INSERT INTO Widget (Code) VALUES (42)");

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 50, isNullable: false));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            Assert.Empty(failures);

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
    [InlineData("default constraint", "ALTER TABLE Widget ADD CONSTRAINT DF_Widget_Amount DEFAULT 0 FOR Amount")]
    [InlineData("nonclustered index", "CREATE NONCLUSTERED INDEX IX_Widget_Amount ON Widget(Amount)")]
    [InlineData("check constraint", "ALTER TABLE Widget ADD CONSTRAINT CK_Widget_Amount CHECK (Amount >= 0)")]
    [InlineData("user statistics", "CREATE STATISTICS ST_Widget_Amount ON Widget(Amount)")]
    [InlineData("computed column", "ALTER TABLE Widget ADD Doubled AS (Amount * 2)")]
    public async Task Numeric_PrecisionChangeToDecimalWithDependentObject_ShouldSkip(
        string _, string dependencySql)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE TABLE Widget (Id int IDENTITY(1,1) PRIMARY KEY, Amount numeric(18,2) NOT NULL)",
                "INSERT INTO Widget (Amount) VALUES (1.23)",
                dependencySql);

            var (_, failures) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            Assert.Empty(failures);

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

            var (_, failures) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 0));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString, ModelWith("Widget", nullableIdentity));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString, DecimalModel("decimal", 19, 4));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 100, isNullable: false));

            Assert.Empty(failures);

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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 100, isNullable: false));

            Assert.Empty(failures);
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

            var (_, failures) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", "varchar", 5, isNullable: false));

            Assert.Empty(failures);
            Assert.Equal(50, (await GetColumnAsync(connectionString, "Widget", "Code")).MaxLength);
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

            var warnings = new List<string>();
            var (plan, _) = await PlanAndRunAsync(connectionString,
                SingleFieldModel("Widget", "Code", targetType, targetPrecision, isNullable: true),
                warnings);

            Assert.Empty(plan.Steps);
            Assert.DoesNotContain(warnings, w => w.Contains("Unmigrated type change"));
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

            var warnings = new List<string>();
            var (plan, _) = await PlanAndRunAsync(connectionString, ModelWith("Widget", field), warnings);

            Assert.Empty(plan.Steps);
            Assert.DoesNotContain(warnings, w => w.Contains("Unmigrated type change"));
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

            var warnings = new List<string>();
            var (plan, _) = await PlanAndRunAsync(connectionString, ModelWith("Widget", field), warnings);

            Assert.Empty(plan.Steps);
            Assert.Contains(warnings, w => w.Contains("Unmigrated type change") && w.Contains("Widget.Code"));
        });
    }

    #endregion

    #region Helpers

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
    /// sequence ApplyToSqlAsync performs.
    /// </summary>
    private async Task<(MigrationPlan Plan, List<(MigrationStep Step, Exception Exception)> Failures)> PlanAndRunAsync(
        string connectionString, DatabaseModel targetModel, List<string>? warnings = null)
    {
        var shift = new Shift { Logger = _logger };
        var actual = await shift.LoadFromSqlAsync(connectionString);

        var planner = new MigrationPlanner { Logger = warnings == null ? _logger : new CapturingLogger(warnings) };
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

    /// <summary>
    /// Collects formatted log messages so a test can assert on what the planner reported, not only
    /// on the steps it produced. Warnings are the only signal for a change the planner refuses to
    /// migrate, since by definition it emits no step for one.
    /// </summary>
    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    #endregion
}