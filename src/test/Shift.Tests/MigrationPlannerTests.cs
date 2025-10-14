using Compile.Shift.Model;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Tests;

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

    #region Helper Methods

    private static DatabaseModel CreateTargetModelWithTables()
    {
        var model = new DatabaseModel();
        
        // User table
        var userTable = new TableModel { Name = "User" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "Username", Type = "nvarchar", Precision = 100 });
        model.Tables["User"] = userTable;

        // Product table
        var productTable = new TableModel { Name = "Product" };
        productTable.Fields.Add(new FieldModel { Name = "ProductID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        productTable.Fields.Add(new FieldModel { Name = "Name", Type = "nvarchar", Precision = 200 });
        model.Tables["Product"] = productTable;

        return model;
    }

    private static DatabaseModel CreateActualModelWithSameTables()
    {
        return CreateTargetModelWithTables(); // Same structure
    }

    private static DatabaseModel CreateActualModelWithCaseInsensitiveTables()
    {
        var model = new DatabaseModel();
        
        // User table with different case
        var userTable = new TableModel { Name = "USER" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "Username", Type = "nvarchar", Precision = 100 });
        model.Tables["USER"] = userTable;

        // Product table with different case
        var productTable = new TableModel { Name = "PRODUCT" };
        productTable.Fields.Add(new FieldModel { Name = "ProductID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        productTable.Fields.Add(new FieldModel { Name = "Name", Type = "nvarchar", Precision = 200 });
        model.Tables["PRODUCT"] = productTable;

        return model;
    }

    private static DatabaseModel CreateTargetModelWithExtraColumns()
    {
        var model = CreateTargetModelWithTables();
        
        // Add extra columns to User table
        var userTable = model.Tables["User"];
        userTable.Fields.Add(new FieldModel { Name = "Email", Type = "nvarchar", Precision = 256 });
        userTable.Fields.Add(new FieldModel { Name = "IsActive", Type = "bit" });

        return model;
    }

    private static DatabaseModel CreateActualModelWithTables()
    {
        return CreateTargetModelWithTables(); // Same as target for base case
    }

    private static DatabaseModel CreateActualModelWithCaseInsensitiveColumns()
    {
        var model = new DatabaseModel();
        
        // User table with case-insensitive column names
        var userTable = new TableModel { Name = "User" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "USERNAME", Type = "nvarchar", Precision = 100 }); // Different case
        model.Tables["User"] = userTable;

        // Product table
        var productTable = new TableModel { Name = "Product" };
        productTable.Fields.Add(new FieldModel { Name = "ProductID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        productTable.Fields.Add(new FieldModel { Name = "NAME", Type = "nvarchar", Precision = 200 }); // Different case
        model.Tables["Product"] = productTable;

        return model;
    }

    private static DatabaseModel CreateTargetModelWithForeignKeys()
    {
        var model = CreateTargetModelWithTables();
        
        // Add Order table with foreign key
        var orderTable = new TableModel { Name = "Order" };
        orderTable.Fields.Add(new FieldModel { Name = "OrderID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        orderTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int" });
        orderTable.ForeignKeys.Add(new ForeignKeyModel 
        { 
            ColumnName = "UserID", 
            TargetTable = "User", 
            TargetColumnName = "UserID",
            RelationshipType = RelationshipType.OneToMany
        });
        model.Tables["Order"] = orderTable;

        return model;
    }

    private static DatabaseModel CreateActualModelWithForeignKeys()
    {
        return CreateTargetModelWithForeignKeys(); // Same structure
    }

    private static DatabaseModel CreateTargetModelWithInvalidForeignKeys()
    {
        var model = CreateTargetModelWithTables();
        
        // Add Order table with foreign key to non-existent table
        var orderTable = new TableModel { Name = "Order" };
        orderTable.Fields.Add(new FieldModel { Name = "OrderID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        orderTable.Fields.Add(new FieldModel { Name = "CustomerID", Type = "int" });
        orderTable.ForeignKeys.Add(new ForeignKeyModel 
        { 
            ColumnName = "CustomerID", 
            TargetTable = "Customer", // This table doesn't exist in target model
            TargetColumnName = "CustomerID",
            RelationshipType = RelationshipType.OneToMany
        });
        model.Tables["Order"] = orderTable;

        return model;
    }

    #endregion
}