using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
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
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false)))
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithMultipleTables()
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

    private static DatabaseModel CreateDatabaseModelWithForeignKeys()
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
                .WithField("ProductID", "int")
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany)
                .WithForeignKey("ProductID", "Product", "ProductID", RelationshipType.OneToMany))
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithIndexes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithIndex("IX_User_Email", "Email", isUnique: true)
                .WithIndex("IX_User_Username", "Username", isUnique: false))
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithMixins()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100))
                .WithField("CreatedDate", "datetime2")
                .WithField("ModifiedDate", "datetime2"))
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithVariousTypes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("TypeTest", table => table
                .WithField("ID", "int", f => f.PrimaryKey().Identity())
                .WithField("IsActive", "bit")
                .WithField("Name", "nvarchar", f => f.Precision(100))
                .WithField("Description", "varchar", f => f.Precision(500))
                .WithField("Count", "bigint")
                .WithField("Price", "decimal", f => f.Precision(18, 2))
                .WithField("GST", "money")
                .WithField("VAT", "smallmoney")
                .WithField("Temperature", "float", f => f.Precision(24))
                .WithField("Distance", "float")
                .WithField("ClientCode", "char", f => f.Precision(4))
                .WithField("UniCode", "nchar", f => f.Precision(8))
                .WithField("TextDescription", "text")
                .WithField("UniTextDescription", "ntext")
                .WithField("CreatedAt", "datetime"))
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithUnsupportedTypes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("UnsupportedTest", table => table
                .WithField("ID", "int", f => f.PrimaryKey().Identity())
                .WithField("Location", "geometry")        // Unsupported type
                .WithField("EventId", "uniqueidentifier") // Unsupported type
                .WithField("CreatedAt", "datetime2")      // Unsupported type
                .WithField("CheckInDate", "date")         // Unsupported type
                .WithField("CheckInTime", "time")         // Unsupported type
                .WithField("Tax", "numeric", f => f.Precision(18, 2))) // Unsupported type
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithAttributes()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
                .WithAttribute("NoIdentity", true))
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithNullableFields()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
                .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(true))
                .WithField("Phone", "nvarchar", f => f.Precision(20).Nullable(true)))
            .Build();
    }

    private static DatabaseModel CreateDatabaseModelWithPrecisionAndScale()
    {
        return DatabaseModelBuilder.Create()
            .WithTable("Product", table => table
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("Name", "nvarchar", f => f.Precision(200))
                .WithField("Price", "decimal", f => f.Precision(18, 2))
                .WithField("Weight", "decimal", f => f.Precision(8, 3))
                .WithField("Description", "nvarchar", f => f.Precision(1000)))
            .Build();
    }

    #endregion
}