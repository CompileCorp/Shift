using Compile.Shift.Model;

namespace Compile.Shift.Tests.Helpers;

public static class TestModels
{
    public static DatabaseModel BuildComprehensiveModel()
    {
        return DatabaseModelBuilder.Create()
            // User table with GUID PK
            .WithTable("User", user => user
                .WithField("UserID", "uniqueidentifier", f => f.PrimaryKey().Identity(false))
                .WithField("Username", "nvarchar", f => f.Precision(50).Nullable(false))
                .WithField("Email", "nvarchar", f => f.Precision(100).Nullable(true))
                .WithField("CreatedAt", "datetime2", f => f.Nullable(false)))
            
            // Product table with comprehensive type coverage
            .WithTable("Product", product => product
                .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
                .WithField("IsActive", "bit", f => f.Nullable(false))
                .WithField("SmallNumber", "tinyint", f => f.Nullable(false))
                .WithField("ShortNumber", "smallint", f => f.Nullable(false))
                .WithField("LongNumber", "bigint", f => f.Nullable(false))
                .WithField("Price", "decimal", f => f.Precision(18, 2).Nullable(false))
                .WithField("NumericOnly", "numeric", f => f.Precision(10).Nullable(true))
                .WithField("MoneyValue", "money", f => f.Nullable(true))
                .WithField("FloatValue", "float", f => f.Nullable(true))
                .WithField("RealValue", "real", f => f.Nullable(true))
                .WithField("FixedAscii", "char", f => f.Precision(10).Nullable(true))
                .WithField("Name", "varchar", f => f.Precision(50).Nullable(false))
                .WithField("Description", "varchar", f => f.Precision(-1).Nullable(true)) // MAX
                .WithField("FixedUnicode", "nchar", f => f.Precision(5).Nullable(true))
                .WithField("UnicodeName", "nvarchar", f => f.Precision(50).Nullable(false))
                .WithField("UnicodeBlob", "nvarchar", f => f.Precision(-1).Nullable(true)) // MAX
                .WithField("WhenAvailable", "date", f => f.Nullable(true))
                .WithField("ShipTime", "time", f => f.Nullable(true))
                .WithField("OffsetTime", "datetimeoffset", f => f.Nullable(true))
                .WithField("BinaryFixed", "binary", f => f.Precision(50).Nullable(true))
                .WithField("BinaryVar", "varbinary", f => f.Precision(-1).Nullable(true)) // MAX
                .WithField("ExternalId", "uniqueidentifier", f => f.Nullable(true))
                .WithField("Metadata", "xml", f => f.Nullable(true)))
            
            // Order table with foreign key to User
            .WithTable("Order", order => order
                .WithField("OrderID", "bigint", f => f.PrimaryKey().Identity())
                .WithField("OrderDate", "datetime2", f => f.Nullable(false))
                .WithField("UserID", "uniqueidentifier", f => f.Nullable(false))
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany))
            
            // OrderItem table with foreign keys to Order and Product
            .WithTable("OrderItem", item => item
                .WithField("OrderItemID", "int", f => f.PrimaryKey().Identity())
                .WithField("OrderID", "bigint", f => f.Nullable(false))
                .WithField("ProductID", "int", f => f.Nullable(false))
                .WithField("Quantity", "int", f => f.Nullable(false))
                .WithField("UnitPrice", "decimal", f => f.Precision(18, 2).Nullable(false))
                .WithForeignKey("OrderID", "Order", "OrderID", RelationshipType.OneToMany)
                .WithForeignKey("ProductID", "Product", "ProductID", RelationshipType.OneToMany))
            .Build();
    }

    public static (DatabaseModel model, string mixinContent, string tableContent) BuildMixinModel()
    {
        var model = DatabaseModelBuilder.Create()
            .WithMixin("Auditable", mixin => mixin
                .WithField("CreatedDateTime", "datetime2", f => f.Nullable(false))
                .WithField("LastModifiedDateTime", "datetime2", f => f.Nullable(true))
                .WithField("LockNumber", "int", f => f.Nullable(false)))
            .WithTable("Task", task => task
                .WithField("TaskID", "int", f => f.PrimaryKey().Identity())
                .WithField("Title", "nvarchar", f => f.Precision(200).Nullable(false)))
            .Build();

        // Apply mixin programmatically similar to Parser.ApplyMixin
        var taskTable = model.Tables["Task"];
        var auditableMixin = model.Mixins["Auditable"];
        taskTable.Mixins.Add(auditableMixin.Name);
        foreach (var field in auditableMixin.Fields)
        {
            taskTable.Fields.Add(new FieldModel
            {
                Name = field.Name,
                Type = field.Type,
                IsNullable = field.IsNullable,
                IsOptional = field.IsOptional,
                Precision = field.Precision,
                Scale = field.Scale,
                IsPrimaryKey = field.IsPrimaryKey,
                IsIdentity = field.IsIdentity
            });
        }

        return (model, string.Empty, string.Empty);
    }
}


