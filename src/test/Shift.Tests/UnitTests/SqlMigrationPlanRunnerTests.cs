using Compile.Shift.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Unit tests for SqlMigrationPlanRunner class.
/// Tests SQL generation methods without requiring database connections.
/// </summary>
public class SqlMigrationPlanRunnerTests : UnitTestContext<SqlMigrationPlanRunner>
{
    private readonly Mock<ILogger<SqlMigrationPlanRunner>> _mockLogger;

    public SqlMigrationPlanRunnerTests()
    {
        _mockLogger = GetMockFor<ILogger<SqlMigrationPlanRunner>>();
    }

    private SqlMigrationPlanRunner CreateRunner(MigrationPlan? plan = null)
    {
        return new SqlMigrationPlanRunner("dummy", plan ?? new MigrationPlan())
        {
            Logger = _mockLogger.Object
        };
    }

    private static string NormalizeSql(IEnumerable<string> sqlStatements)
    {
        // Normalize newlines - trim trailing whitespace from each statement and join with double newline
        return string.Join("\n\n", sqlStatements.Select(s => s.TrimEnd()));
    }

    #region GenerateCreateTableSql Tests

    /// <summary>
    /// Tests that GenerateCreateTableSql generates correct CREATE TABLE SQL with primary key.
    /// </summary>
    [Fact]
    public async Task GenerateCreateTableSql_WithPrimaryKey_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var fields = new List<FieldModel>
        {
            new() { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
            new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false },
            new() { Name = "Email", Type = "nvarchar", Precision = 256, IsNullable = true }
        };

        // Act
        var sql = runner.GenerateCreateTableSql("User", fields).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateCreateTableSql generates correct CREATE TABLE SQL without primary key.
    /// </summary>
    [Fact]
    public async Task GenerateCreateTableSql_WithoutPrimaryKey_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var fields = new List<FieldModel>
        {
            new() { Name = "Name", Type = "nvarchar", Precision = 100, IsNullable = false },
            new() { Name = "Description", Type = "nvarchar", Precision = 500, IsNullable = true }
        };

        // Act
        var sql = runner.GenerateCreateTableSql("TestTable", fields).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateCreateTableSql handles nullable and non-nullable fields correctly.
    /// </summary>
    [Fact]
    public async Task GenerateCreateTableSql_WithNullableFields_ShouldGenerateCorrectNullability()
    {
        // Arrange
        var runner = CreateRunner();
        var fields = new List<FieldModel>
        {
            new() { Name = "ID", Type = "int", IsPrimaryKey = true, IsIdentity = true, IsNullable = false },
            new() { Name = "RequiredField", Type = "nvarchar", Precision = 50, IsNullable = false },
            new() { Name = "OptionalField", Type = "nvarchar", Precision = 50, IsNullable = true }
        };

        // Act
        var sql = runner.GenerateCreateTableSql("TestTable", fields).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateCreateTableSql handles various field types correctly.
    /// </summary>
    [Fact]
    public async Task GenerateCreateTableSql_WithVariousTypes_ShouldGenerateCorrectTypes()
    {
        // Arrange
        var runner = CreateRunner();
        var fields = new List<FieldModel>
        {
            new() { Name = "ID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
            new() { Name = "Price", Type = "decimal", Precision = 18, Scale = 2, IsNullable = false },
            new() { Name = "IsActive", Type = "bit", IsNullable = false },
            new() { Name = "CreatedDate", Type = "datetime", IsNullable = false },
            new() { Name = "Content", Type = "nvarchar", Precision = -1, IsNullable = true }
        };

        // Act
        var sql = runner.GenerateCreateTableSql("Product", fields).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    #endregion

    #region CreateForeignKeySql Tests

    /// <summary>
    /// Tests that CreateForeignKeySql generates correct FOREIGN KEY SQL.
    /// </summary>
    [Fact]
    public async Task CreateForeignKeySql_WithValidForeignKey_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var foreignKey = new ForeignKeyModel
        {
            ColumnName = "UserID",
            TargetTable = "User",
            TargetColumnName = "UserID",
            IsNullable = false
        };

        // Act
        var sql = runner.CreateForeignKeySql("Order", foreignKey).ToList();

        // Assert
        sql.Should().HaveCount(2);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that CreateForeignKeySql generates correct constraint name.
    /// </summary>
    [Fact]
    public async Task CreateForeignKeySql_ShouldGenerateCorrectConstraintName()
    {
        // Arrange
        var runner = CreateRunner();
        var foreignKey = new ForeignKeyModel
        {
            ColumnName = "CreatedByUserID",
            TargetTable = "User",
            TargetColumnName = "UserID",
            IsNullable = true
        };

        // Act
        var sql = runner.CreateForeignKeySql("Document", foreignKey).ToList();

        // Assert
        sql.Should().HaveCount(2);
        await Verify(NormalizeSql(sql));
    }

    #endregion

    #region GenerateColumnSql Tests

    /// <summary>
    /// Tests that GenerateColumnSql generates correct ALTER TABLE ADD COLUMN SQL for non-nullable field.
    /// </summary>
    [Fact]
    public async Task GenerateColumnSql_WithNonNullableField_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Username",
            Type = "nvarchar",
            Precision = 100,
            IsNullable = false
        };

        // Act
        var sql = runner.GenerateColumnSql("User", field).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateColumnSql generates correct ALTER TABLE ADD COLUMN SQL for nullable field.
    /// </summary>
    [Fact]
    public async Task GenerateColumnSql_WithNullableField_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Email",
            Type = "nvarchar",
            Precision = 256,
            IsNullable = true
        };

        // Act
        var sql = runner.GenerateColumnSql("User", field).ToList();

        // Assert
        sql.Should().HaveCount(2);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateColumnSql generates correct default values for integer fields ending in ID.
    /// </summary>
    [Fact]
    public async Task GenerateColumnSql_WithIntegerFieldEndingInID_ShouldGenerateDefaultOne()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "UserID",
            Type = "int",
            IsNullable = false
        };

        // Act
        var sql = runner.GenerateColumnSql("Order", field).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateColumnSql generates correct default values for integer fields not ending in ID.
    /// </summary>
    [Fact]
    public async Task GenerateColumnSql_WithIntegerFieldNotEndingInID_ShouldGenerateDefaultZero()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Quantity",
            Type = "int",
            IsNullable = false
        };

        // Act
        var sql = runner.GenerateColumnSql("OrderItem", field).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateColumnSql generates correct default values for datetime fields.
    /// </summary>
    [Fact]
    public async Task GenerateColumnSql_WithDateTimeField_ShouldGenerateDefaultGetDate()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "CreatedDate",
            Type = "datetime",
            IsNullable = false
        };

        // Act
        var sql = runner.GenerateColumnSql("User", field).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateColumnSql generates correct default values for bit fields.
    /// </summary>
    [Fact]
    public async Task GenerateColumnSql_WithBitField_ShouldGenerateDefaultZero()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "IsActive",
            Type = "bit",
            IsNullable = false
        };

        // Act
        var sql = runner.GenerateColumnSql("User", field).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateColumnSql generates correct default values for uniqueidentifier fields.
    /// </summary>
    [Fact]
    public async Task GenerateColumnSql_WithUniqueidentifierField_ShouldGenerateDefaultNewId()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "ExternalID",
            Type = "uniqueidentifier",
            IsNullable = false
        };

        // Act
        var sql = runner.GenerateColumnSql("User", field).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    #endregion

    #region GenerateAlterColumnSql Tests

    /// <summary>
    /// Tests that GenerateAlterColumnSql generates correct ALTER COLUMN SQL.
    /// </summary>
    [Fact]
    public async Task GenerateAlterColumnSql_WithValidField_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Email",
            Type = "nvarchar",
            Precision = 256,
            IsNullable = true
        };

        // Act
        var sql = runner.GenerateAlterColumnSql("User", field).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateAlterColumnSql handles nullable and non-nullable fields correctly.
    /// </summary>
    [Fact]
    public async Task GenerateAlterColumnSql_WithNullableAndNonNullable_ShouldGenerateCorrectNullability()
    {
        // Arrange
        var runner = CreateRunner();
        var nullableField = new FieldModel
        {
            Name = "Email",
            Type = "nvarchar",
            Precision = 256,
            IsNullable = true
        };
        var nonNullableField = new FieldModel
        {
            Name = "Username",
            Type = "nvarchar",
            Precision = 100,
            IsNullable = false
        };

        // Act
        var nullableSql = runner.GenerateAlterColumnSql("User", nullableField).ToList();
        var nonNullableSql = runner.GenerateAlterColumnSql("User", nonNullableField).ToList();

        // Assert
        var combinedSql = $"Nullable:\n{NormalizeSql(nullableSql)}\n\nNon-Nullable:\n{NormalizeSql(nonNullableSql)}";
        await Verify(combinedSql);
    }

    #endregion

    #region GenerateIndexSql Tests

    /// <summary>
    /// Tests that GenerateIndexSql generates correct CREATE INDEX SQL.
    /// </summary>
    [Fact]
    public async Task GenerateIndexSql_WithSimpleIndex_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var index = new IndexModel
        {
            Fields = new List<string> { "Email" },
            IsUnique = false,
            Kind = IndexKind.NonClustered
        };

        // Act
        var sql = runner.GenerateIndexSql("User", index).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateIndexSql generates correct UNIQUE INDEX SQL.
    /// </summary>
    [Fact]
    public async Task GenerateIndexSql_WithUniqueIndex_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var index = new IndexModel
        {
            Fields = new List<string> { "Email" },
            IsUnique = true,
            Kind = IndexKind.NonClustered
        };

        // Act
        var sql = runner.GenerateIndexSql("User", index).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateIndexSql generates correct CLUSTERED INDEX SQL.
    /// </summary>
    [Fact]
    public async Task GenerateIndexSql_WithClusteredIndex_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var index = new IndexModel
        {
            Fields = new List<string> { "UserID" },
            IsUnique = false,
            Kind = IndexKind.Clustered
        };

        // Act
        var sql = runner.GenerateIndexSql("User", index).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateIndexSql generates correct composite index SQL.
    /// </summary>
    [Fact]
    public async Task GenerateIndexSql_WithCompositeIndex_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var index = new IndexModel
        {
            Fields = new List<string> { "Email", "Username" },
            IsUnique = false,
            Kind = IndexKind.NonClustered
        };

        // Act
        var sql = runner.GenerateIndexSql("User", index).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateIndexSql generates correct alternate key SQL with AK prefix.
    /// </summary>
    [Fact]
    public async Task GenerateIndexSql_WithAlternateKey_ShouldGenerateCorrectSql()
    {
        // Arrange
        var runner = CreateRunner();
        var index = new IndexModel
        {
            Fields = new List<string> { "Email" },
            IsUnique = true,
            IsAlternateKey = true,
            Kind = IndexKind.NonClustered
        };

        // Act
        var sql = runner.GenerateIndexSql("User", index).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    /// <summary>
    /// Tests that GenerateIndexSql resolves model names to column names when table is provided.
    /// </summary>
    [Fact]
    public async Task GenerateIndexSql_WithModelNameInFields_ShouldResolveToColumnName()
    {
        // Arrange
        var runner = CreateRunner();
        var table = new TableModel
        {
            Name = "Order",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "UserID", TargetTable = "User", TargetColumnName = "UserID" }
            }
        };
        var index = new IndexModel
        {
            Fields = new List<string> { "User" }, // Model name
            IsUnique = false,
            Kind = IndexKind.NonClustered
        };

        // Act
        var sql = runner.GenerateIndexSql("Order", index, table).ToList();

        // Assert
        sql.Should().HaveCount(1);
        await Verify(NormalizeSql(sql));
    }

    #endregion

    #region Auditable Mixin SQL Generation Tests

    /// <summary>
    /// Tests that SQL generation for Auditable mixin with Document model creates correct SQL statements.
    /// Verifies that !model User? as CreatedBy creates CreatedByUserID (int, nullable) and
    /// !model User? as LastModifiedBy creates LastModifiedByUserID (int, nullable).
    /// </summary>
    [Fact]
    public async Task GenerateSql_WithAuditableMixin_ShouldGenerateCorrectSQL()
    {
        // Arrange
        var parser = new Parser();
        var targetModel = new DatabaseModel();

        // Parse the Auditable mixin
        var mixinContent = @"
mixin Auditable {
  !model User? as CreatedBy
  !model User? as LastModifiedBy
  datetime CreatedDateTime
  datetime LastModifiedDateTime
  int LockNumber
}";
        var mixin = parser.ParseMixin(mixinContent);
        targetModel.Mixins.Add(mixin.Name, mixin);

        // Parse the User table (required for foreign key)
        var userContent = @"
model User {
  string(100) Username
  string(256) Email
}";
        parser.ParseTable(targetModel, userContent);

        // Parse the Document table with Auditable mixin
        var documentContent = @"
model Document with Auditable {
  string(200) Title
  string(max) Content
}";
        parser.ParseTable(targetModel, documentContent);

        // Create empty actual model to generate full migration plan
        var actualModel = new DatabaseModel();
        var planner = new MigrationPlanner();

        var plan = planner.GeneratePlan(targetModel, actualModel);

        // Generate SQL using direct method calls
        var runner = CreateRunner(plan);
        var sqlStatements = new List<string>();

        // Find the CreateTable step for Document
        var createDocumentStep =
            plan.Steps.FirstOrDefault(step =>
                step.Action == MigrationAction.CreateTable &&
                step.TableName == "Document");

        createDocumentStep.Should().NotBeNull("Document table should be created");

        // Generate CREATE TABLE SQL
        sqlStatements.AddRange(runner.GenerateCreateTableSql(createDocumentStep!.TableName, createDocumentStep.Fields));

        // Generate FOREIGN KEY SQL for each foreign key step
        var addForeignKeySteps =
            plan.Steps.Where(step =>
                step.Action == MigrationAction.AddForeignKey &&
                step.TableName == "Document").ToList();

        foreach (var fkStep in addForeignKeySteps)
        {
            sqlStatements.AddRange(runner.CreateForeignKeySql(fkStep.TableName, fkStep.ForeignKey!));

            // Generate index SQL for foreign key column
            var indexModel = new IndexModel
            {
                Fields = [fkStep.ForeignKey!.ColumnName],
                IsUnique = false,
                Kind = IndexKind.NonClustered
            };
            sqlStatements.AddRange(runner.GenerateIndexSql(fkStep.TableName, indexModel, targetModel.Tables["Document"]));
        }

        // Verify SQL snapshot
        await Verify(NormalizeSql(sqlStatements));
    }

    #endregion

    #region IsAlterColumnPotentiallyUnsafe Tests

    /// <summary>
    /// Tests that IsAlterColumnPotentiallyUnsafe returns false for non-string types.
    /// </summary>
    [Fact]
    public void IsAlterColumnPotentiallyUnsafe_WithNonStringType_ShouldReturnFalse()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Price",
            Type = "int",
            IsNullable = false
        };

        // Act
        var result = runner.IsAlterColumnPotentiallyUnsafe(null!, "TestTable", field);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that IsAlterColumnPotentiallyUnsafe returns false when precision is not set.
    /// </summary>
    [Fact]
    public void IsAlterColumnPotentiallyUnsafe_WithStringTypeWithoutPrecision_ShouldReturnFalse()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Description",
            Type = "nvarchar",
            Precision = null,
            IsNullable = true
        };

        // Act
        var result = runner.IsAlterColumnPotentiallyUnsafe(null!, "TestTable", field);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that IsAlterColumnPotentiallyUnsafe returns false when precision is max (-1).
    /// </summary>
    [Fact]
    public void IsAlterColumnPotentiallyUnsafe_WithStringTypeMaxPrecision_ShouldReturnFalse()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Content",
            Type = "nvarchar",
            Precision = -1,
            IsNullable = true
        };

        // Act
        var result = runner.IsAlterColumnPotentiallyUnsafe(null!, "TestTable", field);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Tests that IsAlterColumnPotentiallyUnsafe returns false for non-decimal types when checking decimal.
    /// </summary>
    [Fact]
    public void IsAlterColumnPotentiallyUnsafe_WithNonDecimalType_ShouldReturnFalse()
    {
        // Arrange
        var runner = CreateRunner();
        var field = new FieldModel
        {
            Name = "Price",
            Type = "int",
            IsNullable = false
        };

        // Act
        var result = runner.IsAlterColumnPotentiallyUnsafe(null!, "TestTable", field);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}