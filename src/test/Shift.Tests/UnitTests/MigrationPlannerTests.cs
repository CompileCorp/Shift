using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;

namespace Compile.Shift.UnitTests;

public class MigrationPlannerTests : UnitTestContext<MigrationPlanner>
{
    #region Table Operation Tests

    /// <summary>
    /// Tests that MigrationPlanner detects new tables and creates CreateTable steps.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithNewTables_ShouldCreateTableSteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = new DatabaseModel(); // Empty actual model

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().NotBeEmpty();
        plan.Steps.Should().Contain(step =>
            step.Action == MigrationAction.CreateTable &&
            step.TableName == "User");
        plan.Steps.Should().Contain(step =>
            step.Action == MigrationAction.CreateTable &&
            step.TableName == "Product");
    }

    /// <summary>
    /// Tests that MigrationPlanner does not create steps when tables already exist.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithExistingTables_ShouldNotCreateTableSteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = CreateActualModelWithSameTables();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().NotContain(step => step.Action == MigrationAction.CreateTable);
    }

    /// <summary>
    /// Tests that MigrationPlanner handles case-insensitive table name matching.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithCaseInsensitiveTableNames_ShouldNotCreateTableSteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = CreateActualModelWithCaseInsensitiveTables();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().NotContain(step => step.Action == MigrationAction.CreateTable);
    }

    #endregion

    #region Column Operation Tests

    /// <summary>
    /// Tests that MigrationPlanner detects new columns and creates AddColumn steps.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithNewColumns_ShouldCreateAddColumnSteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithExtraColumns();
        var actualModel = CreateActualModelWithTables();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().Contain(step =>
            step.Action == MigrationAction.AddColumn &&
            step.TableName == "User" &&
            step.Fields.Any(f => f.Name == "Email"));
        plan.Steps.Should().Contain(step =>
            step.Action == MigrationAction.AddColumn &&
            step.TableName == "User" &&
            step.Fields.Any(f => f.Name == "IsActive"));
    }

    /// <summary>
    /// Tests that MigrationPlanner does not create AddColumn steps when columns already exist.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithExistingColumns_ShouldNotCreateAddColumnSteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = CreateActualModelWithSameTables();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().NotContain(step => step.Action == MigrationAction.AddColumn);
    }

    /// <summary>
    /// Tests that MigrationPlanner handles case-insensitive column name matching.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithCaseInsensitiveColumnNames_ShouldNotCreateAddColumnSteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = CreateActualModelWithCaseInsensitiveColumns();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().NotContain(step => step.Action == MigrationAction.AddColumn);
    }

    #endregion

    #region Foreign Key Tests

    /// <summary>
    /// Tests that MigrationPlanner detects new foreign keys and creates AddForeignKey steps.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithNewForeignKeys_ShouldCreateAddForeignKeySteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithForeignKeys();
        var actualModel = CreateActualModelWithTables();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().Contain(step =>
            step.Action == MigrationAction.AddForeignKey &&
            step.TableName == "Order" &&
            step.ForeignKey != null &&
            step.ForeignKey.TargetTable == "User");
    }

    /// <summary>
    /// Tests that MigrationPlanner does not create AddForeignKey steps when foreign keys already exist.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithExistingForeignKeys_ShouldNotCreateAddForeignKeySteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithForeignKeys();
        var actualModel = CreateActualModelWithForeignKeys();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().NotContain(step => step.Action == MigrationAction.AddForeignKey);
    }

    /// <summary>
    /// Tests that MigrationPlanner only creates foreign keys for tables that exist in target model.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithForeignKeysToNonExistentTables_ShouldSkipForeignKeys()
    {
        // Arrange
        var targetModel = CreateTargetModelWithInvalidForeignKeys();
        var actualModel = new DatabaseModel();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().Contain(step => step.Action == MigrationAction.CreateTable);
        plan.Steps.Should().NotContain(step => step.Action == MigrationAction.AddForeignKey);
    }

    #endregion

    #region Edge Cases and Error Handling

    /// <summary>
    /// Tests that MigrationPlanner handles empty target model.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithEmptyTargetModel_ShouldReturnEmptyPlan()
    {
        // Arrange
        var targetModel = new DatabaseModel();
        var actualModel = CreateActualModelWithTables();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that MigrationPlanner handles empty actual model.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithEmptyActualModel_ShouldCreateAllTables()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = new DatabaseModel();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().HaveCount(2); // Two tables
        plan.Steps.Should().OnlyContain(step => step.Action == MigrationAction.CreateTable);
    }

    /// <summary>
    /// Tests that MigrationPlanner handles identical models.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithIdenticalModels_ShouldReturnEmptyPlan()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = CreateTargetModelWithTables(); // Same as target

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that MigrationPlanner handles null models gracefully.
    /// </summary>
    [Fact]
    public void GeneratePlan_WithNullModels_ShouldThrowException()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => Sut.GeneratePlan(targetModel, null!));
        Assert.Throws<NullReferenceException>(() => Sut.GeneratePlan(null!, new DatabaseModel()));
    }

    #endregion

    #region Mixin Foreign Key Tests

    /// <summary>
    /// Tests that MigrationPlanner correctly handles Auditable mixin with foreign key relationships.
    /// Verifies that !model User? as CreatedBy creates CreatedByUserID (int, nullable) and
    /// !model User? as LastModifiedBy creates LastModifiedByUserID (int, nullable).
    /// </summary>
    [Fact]
    public void GeneratePlan_WithAuditableMixin_ShouldCreateNullableForeignKeyColumns()
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

        var actualModel = new DatabaseModel(); // Empty actual model

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        var createDocumentStep = plan.Steps.FirstOrDefault(step =>
            step.Action == MigrationAction.CreateTable &&
            step.TableName == "Document");

        createDocumentStep.Should().NotBeNull("Document table should be created");

        // Get the Document table from the target model to verify its structure
        var documentTable = targetModel.Tables["Document"];
        documentTable.Should().NotBeNull();

        // Verify CreatedByUserID field exists with correct type and nullability
        var createdByUserIDField = documentTable!.Fields.FirstOrDefault(f => f.Name == "CreatedByUserID");
        createdByUserIDField.Should().NotBeNull("CreatedByUserID field should exist");
        createdByUserIDField!.Type.Should().Be("int", "CreatedByUserID should be int type");
        createdByUserIDField.IsNullable.Should().BeTrue("CreatedByUserID should be nullable (User?)");
        createdByUserIDField.IsOptional.Should().BeTrue("CreatedByUserID should be optional (User?)");

        // Verify LastModifiedByUserID field exists with correct type and nullability
        var lastModifiedByUserIDField = documentTable.Fields.FirstOrDefault(f => f.Name == "LastModifiedByUserID");
        lastModifiedByUserIDField.Should().NotBeNull("LastModifiedByUserID field should exist");
        lastModifiedByUserIDField!.Type.Should().Be("int", "LastModifiedByUserID should be int type");
        lastModifiedByUserIDField.IsNullable.Should().BeTrue("LastModifiedByUserID should be nullable (User?)");

        // Verify foreign key relationships exist
        var createdByForeignKey = documentTable.ForeignKeys.FirstOrDefault(fk => fk.ColumnName == "CreatedByUserID");
        createdByForeignKey.Should().NotBeNull("CreatedByUserID foreign key should exist");
        createdByForeignKey!.TargetTable.Should().Be("User");
        createdByForeignKey.TargetColumnName.Should().Be("UserID");
        createdByForeignKey.IsNullable.Should().BeTrue();

        var lastModifiedByForeignKey = documentTable.ForeignKeys.FirstOrDefault(fk => fk.ColumnName == "LastModifiedByUserID");
        lastModifiedByForeignKey.Should().NotBeNull("LastModifiedByUserID foreign key should exist");
        lastModifiedByForeignKey!.TargetTable.Should().Be("User");
        lastModifiedByForeignKey.TargetColumnName.Should().Be("UserID");
        lastModifiedByForeignKey.IsNullable.Should().BeTrue();

        // Verify other mixin fields are present
        documentTable.Fields.Should().Contain(f => f.Name == "CreatedDateTime");
        documentTable.Fields.Should().Contain(f => f.Name == "LastModifiedDateTime");
        documentTable.Fields.Should().Contain(f => f.Name == "LockNumber");

        // Verify Document-specific fields are present
        documentTable.Fields.Should().Contain(f => f.Name == "Title");
        documentTable.Fields.Should().Contain(f => f.Name == "Content");
    }

    #endregion

    #region Index Operation Tests

    [Fact]
    public void GeneratePlan_WithMissingIndexes_ShouldAddIndexSteps()
    {
        // Arrange
        var targetModel = CreateModelWithIndexes();
        var actualModel = CreateModelWithoutIndexes();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().HaveCount(2);
        plan.Steps.Should().AllSatisfy(step => step.Action.Should().Be(MigrationAction.AddIndex));
        plan.Steps.Should().Contain(step => step.TableName == "User" && step.Index != null && step.Index.Fields.SequenceEqual(new[] { "Email" }) && step.Index.IsUnique == true);
        plan.Steps.Should().Contain(step => step.TableName == "User" && step.Index != null && step.Index.Fields.SequenceEqual(new[] { "Username" }) && step.Index.IsUnique == false);
    }

    [Fact]
    public void GeneratePlan_WithExtraIndexes_ShouldReportExtras()
    {
        // Arrange
        var targetModel = CreateModelWithoutIndexes();
        var actualModel = CreateModelWithIndexes();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().BeEmpty();
        plan.ExtrasInSqlServer.ExtraIndexes.Should().HaveCount(2);
        plan.ExtrasInSqlServer.ExtraIndexes.Should().Contain(extra =>
            extra.TableName == "User" &&
            extra.Fields.SequenceEqual(new[] { "Email" }) &&
            extra.IsUnique == true);
        plan.ExtrasInSqlServer.ExtraIndexes.Should().Contain(extra =>
            extra.TableName == "User" &&
            extra.Fields.SequenceEqual(new[] { "Username" }) &&
            extra.IsUnique == false);
    }

    [Fact]
    public void GeneratePlan_WithUniqueAndNonUniqueIndexes_ShouldDistinguish()
    {
        // Arrange
        var targetModel = CreateModelWithMixedIndexes();
        var actualModel = CreateModelWithoutIndexes();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().HaveCount(3);
        plan.Steps.Should().Contain(step => step.Index != null && step.Index.IsUnique == true);
        plan.Steps.Should().Contain(step => step.Index != null && step.Index.IsUnique == false);
        plan.Steps.Should().OnlyContain(step => step.Action == MigrationAction.AddIndex);
    }

    [Fact]
    public void GeneratePlan_WithCaseInsensitiveIndexFields_ShouldMatch()
    {
        // Arrange
        var targetModel = CreateModelWithIndexes();
        var actualModel = CreateModelWithCaseInsensitiveIndexes();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().BeEmpty();
        plan.ExtrasInSqlServer.ExtraIndexes.Should().BeEmpty();
    }

    [Fact]
    public void GeneratePlan_WithMultiColumnIndexes_ShouldMatchFieldOrder()
    {
        // Arrange
        var targetModel = CreateModelWithMultiColumnIndexes();
        var actualModel = CreateModelWithoutIndexes();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().HaveCount(2);
        plan.Steps.Should().Contain(step =>
            step.Index != null && step.Index.Fields.SequenceEqual(new[] { "Email", "Username" }));
        plan.Steps.Should().Contain(step =>
            step.Index != null && step.Index.Fields.SequenceEqual(new[] { "Username", "Email" }));
    }

    [Fact]
    public void GeneratePlan_WithSameTableMissingAndExtraIndexes_ShouldHandleBoth()
    {
        // Arrange
        var targetModel = CreateModelWithSpecificIndexes();
        var actualModel = CreateModelWithDifferentIndexes();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().HaveCount(1);
        plan.Steps.Should().Contain(step =>
            step.Action == MigrationAction.AddIndex &&
            step.Index != null && step.Index.Fields.SequenceEqual(new[] { "Email" }));
        plan.ExtrasInSqlServer.ExtraIndexes.Should().HaveCount(1);
        plan.ExtrasInSqlServer.ExtraIndexes.Should().Contain(extra =>
            extra.Fields.SequenceEqual(new[] { "Username" }));
    }

    #endregion

    #region Helper Methods

    private static DatabaseModel CreateTargetModelWithTables()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100)))
            .WithTable("Product", table => table
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("Name", "nvarchar", f => f.Precision(200)))
            .Build();
    }

    private static DatabaseModel CreateActualModelWithSameTables()
    {
        return CreateTargetModelWithTables(); // Same structure
    }

    private static DatabaseModel CreateActualModelWithCaseInsensitiveTables()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("USER", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100)))
            .WithTable("PRODUCT", table => table
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("Name", "nvarchar", f => f.Precision(200)))
            .Build();
    }

    private static DatabaseModel CreateTargetModelWithExtraColumns()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("IsActive", "bit"))
            .WithTable("Product", table => table
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("Name", "nvarchar", f => f.Precision(200)))
            .Build();
    }

    private static DatabaseModel CreateActualModelWithTables()
    {
        return CreateTargetModelWithTables(); // Same as target for base case
    }

    private static DatabaseModel CreateActualModelWithCaseInsensitiveColumns()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("USERNAME", "nvarchar", f => f.Precision(100))) // Different case
            .WithTable("Product", table => table
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("NAME", "nvarchar", f => f.Precision(200))) // Different case
            .Build();
    }

    private static DatabaseModel CreateTargetModelWithForeignKeys()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100)))
            .WithTable("Product", table => table
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("Name", "nvarchar", f => f.Precision(200)))
            .WithTable("Order", table => table
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int")
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();
    }

    private static DatabaseModel CreateActualModelWithForeignKeys()
    {
        return CreateTargetModelWithForeignKeys(); // Same structure
    }

    private static DatabaseModel CreateTargetModelWithInvalidForeignKeys()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100)))
            .WithTable("Product", table => table
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("Name", "nvarchar", f => f.Precision(200)))
            .WithTable("Order", table => table
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("CustomerID", "int")
                .WithForeignKey("CustomerID", "Customer", "CustomerID", RelationshipType.OneToMany)) // This table doesn't exist in target model
            .Build();
    }

    private static DatabaseModel CreateModelWithIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithIndex("IX_User_Email", "Email", isUnique: true)
                .WithIndex("IX_User_Username", "Username", isUnique: false))
            .Build();
    }

    private static DatabaseModel CreateModelWithoutIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("Username", "nvarchar", f => f.Precision(100)))
            .Build();
    }

    private static DatabaseModel CreateModelWithMixedIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithIndex("IX_User_Email", "Email", isUnique: true)
                .WithIndex("IX_User_Username", "Username", isUnique: false)
                .WithIndex("IX_User_Email_Unique", "Email", isUnique: true))
            .Build();
    }

    private static DatabaseModel CreateModelWithCaseInsensitiveIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithIndex("IX_User_EMAIL", "EMAIL", isUnique: true)
                .WithIndex("IX_User_USERNAME", "USERNAME", isUnique: false))
            .Build();
    }

    private static DatabaseModel CreateModelWithMultiColumnIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithIndex("IX_User_Email_Username", new[] { "Email", "Username" }, isUnique: false)
                .WithIndex("IX_User_Username_Email", new[] { "Username", "Email" }, isUnique: false))
            .Build();
    }

    private static DatabaseModel CreateModelWithSpecificIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithIndex("IX_User_Email", "Email", isUnique: true))
            .Build();
    }

    private static DatabaseModel CreateModelWithDifferentIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithIndex("IX_User_Username", "Username", isUnique: false))
            .Build();
    }

    #endregion
}