using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Tests.Integration;

/// <summary>
/// Integration tests for SQL data type round-trip conversions.
/// Tests the complete cycle: SQL table → SqlServerLoader → ModelExporter → Parser → SqlMigrationPlanRunner → verification.
/// Uses Verify() for DMD snapshot testing and FluentAssertions for round-trip verification.
/// </summary>
[Collection("SqlServer")]
public class SqlDataType_RoundTrip_Tests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<Shift> _logger;

    public SqlDataType_RoundTrip_Tests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
    }

    #region Boolean Type Tests

    [Fact]
    public async Task RoundTrip_BooleanType_ShouldPreserveType()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestBoolean (
                ID int IDENTITY(1,1) PRIMARY KEY,
                IsActive bit NOT NULL,
                IsOptional bit NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestBoolean", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("Boolean.dmd");

            // Verify round-trip preservation
            var isActiveField = finalModel.Tables["TestBoolean"].Fields.First(f => f.Name == "IsActive");
            isActiveField.Type.Should().Be("bit");
            isActiveField.IsNullable.Should().BeFalse();

            var isOptionalField = finalModel.Tables["TestBoolean"].Fields.First(f => f.Name == "IsOptional");
            isOptionalField.Type.Should().Be("bit");
            isOptionalField.IsNullable.Should().BeTrue();
        });
    }

    #endregion

    #region String Type Tests

    [Fact]
    public async Task RoundTrip_StringTypes_ShouldPreservePrecision()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestStringTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                UnicodeName nvarchar(100) NOT NULL,
                UnicodeDescription nvarchar(500) NULL,
                UnicodeCode nchar(10) NOT NULL,
                UnicodeText ntext NULL,
                AsciiName varchar(100) NOT NULL,
                AsciiDescription varchar(500) NULL,
                AsciiCode char(10) NOT NULL,
                AsciiText text NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestStringTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("StringTypes.dmd");

            // Verify round-trip preservation - DMD types are converted back to SQL types
            var unicodeNameField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "UnicodeName");
            unicodeNameField.Type.Should().Be("nvarchar");
            unicodeNameField.Precision.Should().Be(100);
            unicodeNameField.IsNullable.Should().BeFalse();

            var unicodeDescriptionField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "UnicodeDescription");
            unicodeDescriptionField.Type.Should().Be("nvarchar");
            unicodeDescriptionField.Precision.Should().Be(500);
            unicodeDescriptionField.IsNullable.Should().BeTrue();

            var unicodeCodeField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "UnicodeCode");
            unicodeCodeField.Type.Should().Be("nchar");
            unicodeCodeField.Precision.Should().Be(10);
            unicodeCodeField.IsNullable.Should().BeFalse();

            var unicodeTextField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "UnicodeText");
            unicodeTextField.Type.Should().Be("nvarchar", because: "NTEXT was deprecated in SQL Server 2005 and replaced with NVARCHAR(MAX)");
            unicodeTextField.Precision.Should().Be(-1, because: "-1 indicates MAX");
            unicodeTextField.IsNullable.Should().BeTrue();

            var asciiNameField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "AsciiName");
            asciiNameField.Type.Should().Be("varchar");
            asciiNameField.Precision.Should().Be(100);
            asciiNameField.IsNullable.Should().BeFalse();

            var asciiDescriptionField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "AsciiDescription");
            asciiDescriptionField.Type.Should().Be("varchar");
            asciiDescriptionField.Precision.Should().Be(500);
            asciiDescriptionField.IsNullable.Should().BeTrue();

            var asciiCodeField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "AsciiCode");
            asciiCodeField.Type.Should().Be("char");
            asciiCodeField.Precision.Should().Be(10);
            asciiCodeField.IsNullable.Should().BeFalse();

            var asciiTextField = finalModel.Tables["TestStringTypes"].Fields.First(f => f.Name == "AsciiText");
            asciiTextField.Type.Should().Be("varchar", because: "TEXT was deprecated in SQL Server 2005 and replaced with VARCHAR(MAX)");
            asciiTextField.Precision.Should().Be(-1, because: "-1 indicates MAX");
            asciiTextField.IsNullable.Should().BeTrue();
        });
    }

    [Fact]
    public async Task RoundTrip_AsciiStringTypes_ShouldPreservePrecision()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestAsciiStringTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                AsciiName varchar(100) NOT NULL,
                AsciiDescription varchar(500) NULL,
                AsciiCode char(10) NOT NULL,
                AsciiText text NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestAsciiStringTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("AsciiStringTypes.dmd");

            // Verify round-trip preservation
            var asciiNameField = finalModel.Tables["TestAsciiStringTypes"].Fields.First(f => f.Name == "AsciiName");
            asciiNameField.Type.Should().Be("varchar");
            asciiNameField.Precision.Should().Be(100);
            asciiNameField.IsNullable.Should().BeFalse();

            var asciiDescriptionField = finalModel.Tables["TestAsciiStringTypes"].Fields.First(f => f.Name == "AsciiDescription");
            asciiDescriptionField.Type.Should().Be("varchar");
            asciiDescriptionField.Precision.Should().Be(500);
            asciiDescriptionField.IsNullable.Should().BeTrue();

            var asciiCodeField = finalModel.Tables["TestAsciiStringTypes"].Fields.First(f => f.Name == "AsciiCode");
            asciiCodeField.Type.Should().Be("char");
            asciiCodeField.Precision.Should().Be(10);
            asciiCodeField.IsNullable.Should().BeFalse();

            var asciiTextField = finalModel.Tables["TestAsciiStringTypes"].Fields.First(f => f.Name == "AsciiText");
            asciiTextField.Type.Should().Be("varchar");
            asciiTextField.Precision.Should().Be(-1, because: "-1 indicates MAX");
            asciiTextField.IsNullable.Should().BeTrue();
        });
    }

    [Fact]
    public async Task RoundTrip_MaxLengthStrings_ShouldConvertToMax()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestMaxLengthStrings (
                ID int IDENTITY(1,1) PRIMARY KEY,
                UnicodeMax nvarchar(max) NULL,
                AsciiMax varchar(max) NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestMaxLengthStrings", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("MaxLengthStrings.dmd");

            // Verify round-trip preservation - DMD types are converted back to SQL types
            var unicodeMaxField = finalModel.Tables["TestMaxLengthStrings"].Fields.First(f => f.Name == "UnicodeMax");
            unicodeMaxField.Type.Should().Be("nvarchar");
            unicodeMaxField.Precision.Should().Be(-1, because: "-1 indicates MAX");
            unicodeMaxField.IsNullable.Should().BeTrue();

            var asciiMaxField = finalModel.Tables["TestMaxLengthStrings"].Fields.First(f => f.Name == "AsciiMax");
            asciiMaxField.Type.Should().Be("varchar");
            asciiMaxField.Precision.Should().Be(-1, because: "-1 indicates MAX");
            asciiMaxField.IsNullable.Should().BeTrue();
        });
    }

    [Fact]
    public async Task RoundTrip_AllPrecisionTypes_ShouldPreservePrecisionAndScale()
    {
        // Arrange - Test all SQL types that support precision/length/scale
        var tableSchema = @"
            CREATE TABLE TestAllPrecisionTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                -- String types with precision/length
                UnicodeVarChar nvarchar(100) NOT NULL,
                UnicodeChar nchar(10) NOT NULL,
                AsciiVarChar varchar(200) NOT NULL,
                AsciiChar char(5) NOT NULL,
                -- Decimal types with precision and scale
                Price decimal(18,2) NOT NULL,
                Weight decimal(8,3) NULL,
                TaxRate decimal(5,4) NULL,
                -- Numeric type (alias for decimal)
                Amount numeric(10,2) NULL,
                -- Money types (have fixed precision/scale - no explicit precision/scale allowed)
                MoneyValue money NULL,
                SmallMoneyValue smallmoney NULL,
                -- Float with optional precision
                Distance float(53) NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestAllPrecisionTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("AllPrecisionTypes.dmd");

            // Verify string types with precision
            var unicodeVarCharField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "UnicodeVarChar");
            unicodeVarCharField.Type.Should().Be("nvarchar");
            unicodeVarCharField.Precision.Should().Be(100);
            unicodeVarCharField.IsNullable.Should().BeFalse();

            var unicodeCharField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "UnicodeChar");
            unicodeCharField.Type.Should().Be("nchar");
            unicodeCharField.Precision.Should().Be(10);
            unicodeCharField.IsNullable.Should().BeFalse();

            var asciiVarCharField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "AsciiVarChar");
            asciiVarCharField.Type.Should().Be("varchar");
            asciiVarCharField.Precision.Should().Be(200);
            asciiVarCharField.IsNullable.Should().BeFalse();

            var asciiCharField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "AsciiChar");
            asciiCharField.Type.Should().Be("char");
            asciiCharField.Precision.Should().Be(5);
            asciiCharField.IsNullable.Should().BeFalse();

            // Verify decimal types with precision and scale
            var priceField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "Price");
            priceField.Type.Should().Be("decimal");
            priceField.Precision.Should().Be(18);
            priceField.Scale.Should().Be(2);
            priceField.IsNullable.Should().BeFalse();

            var weightField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "Weight");
            weightField.Type.Should().Be("decimal");
            weightField.Precision.Should().Be(8);
            weightField.Scale.Should().Be(3);
            weightField.IsNullable.Should().BeTrue();

            var taxRateField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "TaxRate");
            taxRateField.Type.Should().Be("decimal");
            taxRateField.Precision.Should().Be(5);
            taxRateField.Scale.Should().Be(4);
            taxRateField.IsNullable.Should().BeTrue();

            // Verify numeric type (converted to decimal)
            var amountField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "Amount");
            amountField.Type.Should().Be("decimal");
            amountField.Precision.Should().Be(10);
            amountField.Scale.Should().Be(2);
            amountField.IsNullable.Should().BeTrue();

            // Verify money types (converted to decimal in DMD representation)
            var moneyField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "MoneyValue");
            moneyField.Type.Should().Be("decimal");
            moneyField.IsNullable.Should().BeTrue();

            var smallMoneyField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "SmallMoneyValue");
            smallMoneyField.Type.Should().Be("decimal");
            smallMoneyField.IsNullable.Should().BeTrue();

            // Verify float types with precision
            var distanceField = finalModel.Tables["TestAllPrecisionTypes"].Fields.First(f => f.Name == "Distance");
            distanceField.Type.Should().Be("float");
            distanceField.IsNullable.Should().BeTrue();
        });
    }

    #endregion

    #region Numeric Type Tests

    [Fact]
    public async Task RoundTrip_NumericTypes_ShouldPreserveTypes()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestNumericTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                RegularNumber int NOT NULL,
                BigNumber bigint NOT NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestNumericTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("NumericTypes.dmd");

            // Verify round-trip preservation
            var regularNumberField = finalModel.Tables["TestNumericTypes"].Fields.First(f => f.Name == "RegularNumber");
            regularNumberField.Type.Should().Be("int");
            regularNumberField.IsNullable.Should().BeFalse();

            var bigNumberField = finalModel.Tables["TestNumericTypes"].Fields.First(f => f.Name == "BigNumber");
            bigNumberField.Type.Should().Be("bigint");
            bigNumberField.IsNullable.Should().BeFalse();
        });
    }

    [Fact]
    public async Task RoundTrip_DecimalTypes_ShouldPreservePrecisionAndScale()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestDecimalTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                Price decimal(18,2) NOT NULL,
                Weight decimal(8,3) NULL,
                MoneyValue money NULL,
                SmallMoneyValue smallmoney NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestDecimalTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("DecimalTypes.dmd");

            // Verify round-trip preservation
            var priceField = finalModel.Tables["TestDecimalTypes"].Fields.First(f => f.Name == "Price");
            priceField.Type.Should().Be("decimal");
            priceField.Precision.Should().Be(18);
            priceField.Scale.Should().Be(2);
            priceField.IsNullable.Should().BeFalse();

            var weightField = finalModel.Tables["TestDecimalTypes"].Fields.First(f => f.Name == "Weight");
            weightField.Type.Should().Be("decimal");
            weightField.Precision.Should().Be(8);
            weightField.Scale.Should().Be(3);
            weightField.IsNullable.Should().BeTrue();

            var moneyField = finalModel.Tables["TestDecimalTypes"].Fields.First(f => f.Name == "MoneyValue");
            moneyField.Type.Should().Be("decimal", because: "MONEY data type is replaced with DECIMAL(19,4)");
            moneyField.Precision.Should().Be(19);
            moneyField.Scale.Should().Be(4);
            moneyField.IsNullable.Should().BeTrue();

            var smallMoneyField = finalModel.Tables["TestDecimalTypes"].Fields.First(f => f.Name == "SmallMoneyValue");
            smallMoneyField.Type.Should().Be("decimal", because: "SMALLMONEY data type is replaced with DECIMAL(10,4)");
            smallMoneyField.Precision.Should().Be(10);
            smallMoneyField.Scale.Should().Be(4);
            smallMoneyField.IsNullable.Should().BeTrue();
        });
    }

    [Fact]
    public async Task RoundTrip_FloatTypes_ShouldPreservePrecision()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestFloatTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                FloatValue float NULL,
                FloatWithPrecision float NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestFloatTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("FloatTypes.dmd");

            // Verify round-trip preservation
            var floatField = finalModel.Tables["TestFloatTypes"].Fields.First(f => f.Name == "FloatValue");
            floatField.Type.Should().Be("float");
            floatField.IsNullable.Should().BeTrue();

            var floatWithPrecisionField = finalModel.Tables["TestFloatTypes"].Fields.First(f => f.Name == "FloatWithPrecision");
            floatWithPrecisionField.Type.Should().Be("float");
            floatWithPrecisionField.IsNullable.Should().BeTrue();
        });
    }

    #endregion

    #region DateTime Type Tests

    [Fact]
    public async Task RoundTrip_DateTimeType_ShouldPreserveType()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestDateTimeTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                CreatedAt datetime NOT NULL,
                UpdatedAt datetime NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestDateTimeTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing
            await Verify(dmdContent).UseTextForParameters("DateTimeTypes.dmd");

            // Verify round-trip preservation
            var createdAtField = finalModel.Tables["TestDateTimeTypes"].Fields.First(f => f.Name == "CreatedAt");
            createdAtField.Type.Should().Be("datetime");
            createdAtField.IsNullable.Should().BeFalse();

            var updatedAtField = finalModel.Tables["TestDateTimeTypes"].Fields.First(f => f.Name == "UpdatedAt");
            updatedAtField.Type.Should().Be("datetime");
            updatedAtField.IsNullable.Should().BeTrue();
        });
    }

    #endregion

    #region Unsupported Types Tests

    [Fact]
    public async Task RoundTrip_UnsupportedTypes_ShouldBeCommentedOut()
    {
        // Arrange
        var tableSchema = @"
            CREATE TABLE TestUnsupportedTypes (
                ID int IDENTITY(1,1) PRIMARY KEY,
                GuidValue uniqueidentifier NULL,
                DateTime2Value datetime2 NULL,
                DateValue date NULL,
                TimeValue time NULL,
                NumericValue numeric(18,2) NULL,
                GeometryValue geometry NULL
            );";

        // Act & Assert
        await ExecuteRoundTripTest(tableSchema, "TestUnsupportedTypes", async (dmdContent, finalModel) =>
        {
            // Verify DMD content using snapshot testing - should show commented out unsupported types
            await Verify(dmdContent).UseTextForParameters("UnsupportedTypes.dmd");

            // Verify that unsupported types are excluded from final schema
            finalModel.Tables["TestUnsupportedTypes"].Fields.Should().HaveCount(4); // TestUnsupportedTypesID (auto-generated), ID, GuidValue, and NumericValue fields should remain
            finalModel.Tables["TestUnsupportedTypes"].Fields.Should().Contain(f => f.Name == "TestUnsupportedTypesID");
            finalModel.Tables["TestUnsupportedTypes"].Fields.Should().Contain(f => f.Name == "ID");
            finalModel.Tables["TestUnsupportedTypes"].Fields.Should().Contain(f => f.Name == "GuidValue");
            finalModel.Tables["TestUnsupportedTypes"].Fields.Should().Contain(f => f.Name == "NumericValue");

            // Verify DMD content contains commented out fields for truly unsupported types
            dmdContent.Should().Contain("# datetime2 DateTime2Value");
            dmdContent.Should().Contain("# date DateValue");
            dmdContent.Should().Contain("# time TimeValue");
            dmdContent.Should().Contain("# geometry GeometryValue");
            
            // Verify that supported types appear correctly in DMD content
            dmdContent.Should().Contain("guid? GuidValue");
            dmdContent.Should().Contain("decimal(18,2)? NumericValue");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Executes a complete round-trip test: SQL → Load → Export → Parse → Apply → Verify
    /// </summary>
    private async Task ExecuteRoundTripTest(string sourceTableSchema, string tableName, Func<string, DatabaseModel, Task> verification)
    {
        var sourceDbName = SqlServerTestHelper.GenerateDatabaseName();
        var targetDbName = SqlServerTestHelper.GenerateDatabaseName();

        try
        {
            // Step 1: Create source database with table
            await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, sourceDbName);
            var sourceConnectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, sourceDbName);

            using (var connection = new SqlConnection(sourceConnectionString))
            {
                await connection.OpenAsync();
                using var command = new SqlCommand(sourceTableSchema, connection);
                await command.ExecuteNonQueryAsync();
            }

            // Step 2: Load schema via SqlServerLoader
            var shift = new Shift { Logger = _logger };
            var sourceModel = await shift.LoadFromSqlAsync(sourceConnectionString);

            // Step 3: Export to DMD string via ModelExporter
            var exporter = new ModelExporter();
            var dmdContent = exporter.GenerateDmdContent(sourceModel.Tables[tableName], sourceModel.Mixins.Values.ToList());

            // Step 4: Parse DMD back to DatabaseModel via Parser
            var parser = new Parser();
            var parsedModel = new DatabaseModel();
            parser.ParseTable(parsedModel, dmdContent);

            // Step 5: Apply to new empty database via MigrationPlanner + SqlMigrationPlanRunner
            await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, targetDbName);
            var targetConnectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, targetDbName);

            var planner = new MigrationPlanner();
            var emptyModel = new DatabaseModel();
            var plan = planner.GeneratePlan(parsedModel, emptyModel);
            var runner = new SqlMigrationPlanRunner(targetConnectionString, plan) { Logger = _logger };
            var failures = runner.Run();
            failures.Should().BeEmpty("Migration should complete without failures");

            // Step 6: Load final schema via SqlServerLoader
            var finalModel = await shift.LoadFromSqlAsync(targetConnectionString);

            // Step 7: Execute verification
            await verification(dmdContent, finalModel);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, sourceDbName);
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, targetDbName);
        }
    }

    /// <summary>
    /// Helper method to verify field type, precision, scale, and nullable status
    /// </summary>
    private static void VerifyFieldType(TableModel table, string fieldName, string expectedType, bool expectedNullable, int? precision = null, int? scale = null)
    {
        var field = table.Fields.FirstOrDefault(f => f.Name == fieldName);
        field.Should().NotBeNull($"Field {fieldName} should exist");

        field!.Type.Should().Be(expectedType, $"Field {fieldName} should have type {expectedType}");
        field.IsNullable.Should().Be(expectedNullable, $"Field {fieldName} should be {(expectedNullable ? "nullable" : "not nullable")}");

        if (precision.HasValue)
        {
            field.Precision.Should().Be(precision.Value, $"Field {fieldName} should have precision {precision.Value}");
        }

        if (scale.HasValue)
        {
            field.Scale.Should().Be(scale.Value, $"Field {fieldName} should have scale {scale.Value}");
        }
    }

    #endregion
}
