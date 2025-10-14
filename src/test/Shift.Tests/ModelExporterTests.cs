using Compile.Shift.Model;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Tests;

public class ModelExporterTests : UnitTestContext<ModelExporter>
{
    [Fact]
    public async Task GenerateDmdContent_WithSimpleTable_ShouldGenerateCorrectContent()
    {
        // Arrange
        var model = CreateSimpleDatabaseModel();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithForeignKeys_ShouldGenerateCorrectRelationships()
    {
        // Arrange
        var model = CreateDatabaseModelWithForeignKeys();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithIndexes_ShouldGenerateCorrectIndexes()
    {
        // Arrange
        var model = CreateDatabaseModelWithIndexes();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithVariousTypes_ShouldMapTypesCorrectly()
    {
        // Arrange
        var model = CreateDatabaseModelWithVariousTypes();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithUnsupportedTypes_ShouldCommentOutUnsupportedFields()
    {
        // Arrange
        var model = CreateDatabaseModelWithUnsupportedTypes();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithTableAttributes_ShouldGenerateCorrectAttributes()
    {
        // Arrange
        var model = CreateDatabaseModelWithAttributes();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithMixins_ShouldGenerateCorrectMixinReferences()
    {
        // Arrange
        var model = CreateDatabaseModelWithMixins();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithMultipleTables_ShouldGenerateSeparateContent()
    {
        // Arrange
        var model = CreateDatabaseModelWithMultipleTables();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithNullableFields_ShouldGenerateCorrectNullableSyntax()
    {
        // Arrange
        var model = CreateDatabaseModelWithNullableFields();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    [Fact]
    public async Task GenerateDmdContent_WithPrecisionAndScale_ShouldGenerateCorrectPrecision()
    {
        // Arrange
        var model = CreateDatabaseModelWithPrecisionAndScale();

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            // Act
            var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());

            // Assert
            await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
        }
    }

    #region Helper Methods

    private static DatabaseModel CreateSimpleDatabaseModel()
    {
        var model = new DatabaseModel();
        
        var userTable = new TableModel { Name = "User" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "Username", Type = "nvarchar", Precision = 100 });
        model.Tables["User"] = userTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithMultipleTables()
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

    private static DatabaseModel CreateDatabaseModelWithForeignKeys()
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

        // Order table with foreign keys
        var orderTable = new TableModel { Name = "Order" };
        orderTable.Fields.Add(new FieldModel { Name = "OrderID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        orderTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int" });
        orderTable.Fields.Add(new FieldModel { Name = "ProductID", Type = "int" });
        orderTable.ForeignKeys.Add(new ForeignKeyModel 
        { 
            ColumnName = "UserID", 
            TargetTable = "User", 
            TargetColumnName = "UserID",
            RelationshipType = RelationshipType.OneToMany
        });
        orderTable.ForeignKeys.Add(new ForeignKeyModel 
        { 
            ColumnName = "ProductID", 
            TargetTable = "Product", 
            TargetColumnName = "ProductID",
            RelationshipType = RelationshipType.OneToMany
        });
        model.Tables["Order"] = orderTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithIndexes()
    {
        var model = new DatabaseModel();
        
        var userTable = new TableModel { Name = "User" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "Username", Type = "nvarchar", Precision = 100 });
        userTable.Fields.Add(new FieldModel { Name = "Email", Type = "nvarchar", Precision = 256 });
        
        // Add indexes
        userTable.Indexes.Add(new IndexModel { Fields = new List<string> { "Email" }, IsUnique = true });
        userTable.Indexes.Add(new IndexModel { Fields = new List<string> { "Username" }, IsUnique = false });
        
        model.Tables["User"] = userTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithMixins()
    {
        var model = new DatabaseModel();
        
        var userTable = new TableModel { Name = "User" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "Username", Type = "nvarchar", Precision = 100 });
        userTable.Fields.Add(new FieldModel { Name = "CreatedDate", Type = "datetime2" });
        userTable.Fields.Add(new FieldModel { Name = "ModifiedDate", Type = "datetime2" });
        model.Tables["User"] = userTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithVariousTypes()
    {
        var model = new DatabaseModel();
        
        var testTable = new TableModel { Name = "TypeTest" };
        testTable.Fields.Add(new FieldModel { Name = "ID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        testTable.Fields.Add(new FieldModel { Name = "IsActive", Type = "bit" });
        testTable.Fields.Add(new FieldModel { Name = "Name", Type = "nvarchar", Precision = 100 });
        testTable.Fields.Add(new FieldModel { Name = "Description", Type = "varchar", Precision = 500 });
        testTable.Fields.Add(new FieldModel { Name = "Count", Type = "bigint" });
        testTable.Fields.Add(new FieldModel { Name = "Price", Type = "decimal", Precision = 18, Scale = 2 });
        model.Tables["TypeTest"] = testTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithUnsupportedTypes()
    {
        var model = new DatabaseModel();
        
        var testTable = new TableModel { Name = "UnsupportedTest" };
        testTable.Fields.Add(new FieldModel { Name = "ID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        testTable.Fields.Add(new FieldModel { Name = "Location", Type = "geometry" }); // Unsupported type
        model.Tables["UnsupportedTest"] = testTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithAttributes()
    {
        var model = new DatabaseModel();
        
        var userTable = new TableModel { Name = "User" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "Username", Type = "nvarchar", Precision = 100 });
        userTable.Attributes["NoIdentity"] = true;
        model.Tables["User"] = userTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithNullableFields()
    {
        var model = new DatabaseModel();
        
        var userTable = new TableModel { Name = "User" };
        userTable.Fields.Add(new FieldModel { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        userTable.Fields.Add(new FieldModel { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false });
        userTable.Fields.Add(new FieldModel { Name = "Email", Type = "nvarchar", Precision = 256, IsNullable = true });
        userTable.Fields.Add(new FieldModel { Name = "Phone", Type = "nvarchar", Precision = 20, IsNullable = true });
        model.Tables["User"] = userTable;

        return model;
    }

    private static DatabaseModel CreateDatabaseModelWithPrecisionAndScale()
    {
        var model = new DatabaseModel();
        
        var productTable = new TableModel { Name = "Product" };
        productTable.Fields.Add(new FieldModel { Name = "ProductID", Type = "int", IsPrimaryKey = true, IsIdentity = true });
        productTable.Fields.Add(new FieldModel { Name = "Name", Type = "nvarchar", Precision = 200 });
        productTable.Fields.Add(new FieldModel { Name = "Price", Type = "decimal", Precision = 18, Scale = 2 });
        productTable.Fields.Add(new FieldModel { Name = "Weight", Type = "decimal", Precision = 8, Scale = 3 });
        productTable.Fields.Add(new FieldModel { Name = "Description", Type = "nvarchar", Precision = 1000 });
        model.Tables["Product"] = productTable;

        return model;
    }

    #endregion
}