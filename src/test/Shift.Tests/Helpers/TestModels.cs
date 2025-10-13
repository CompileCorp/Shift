using Compile.Shift.Model;

namespace Compile.Shift.Tests.Helpers;

public static class TestModels
{
    public static DatabaseModel BuildComprehensiveModel()
    {
        var model = new DatabaseModel();

        // User table with GUID PK
        var user = new TableModel
        {
            Name = "User",
            Fields =
            {
                new FieldModel { Name = "UserID", Type = "uniqueidentifier", IsPrimaryKey = true, IsIdentity = false },
                new FieldModel { Name = "Username", Type = "nvarchar", Precision = 50, IsNullable = false },
                new FieldModel { Name = "Email", Type = "nvarchar", Precision = 100, IsNullable = true },
                new FieldModel { Name = "CreatedAt", Type = "datetime2", IsNullable = false }
            }
        };
        model.Tables[user.Name] = user;

        // Product table with int identity PK and wide type coverage
        var product = new TableModel
        {
            Name = "Product",
            Fields =
            {
                new FieldModel { Name = "ProductID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                new FieldModel { Name = "IsActive", Type = "bit", IsNullable = false },
                new FieldModel { Name = "SmallNumber", Type = "tinyint", IsNullable = false },
                new FieldModel { Name = "ShortNumber", Type = "smallint", IsNullable = false },
                new FieldModel { Name = "LongNumber", Type = "bigint", IsNullable = false },
                new FieldModel { Name = "Price", Type = "decimal", Precision = 18, Scale = 2, IsNullable = false },
                new FieldModel { Name = "NumericOnly", Type = "numeric", Precision = 10, IsNullable = true },
                new FieldModel { Name = "MoneyValue", Type = "money", IsNullable = true },
                new FieldModel { Name = "FloatValue", Type = "float", IsNullable = true },
                new FieldModel { Name = "RealValue", Type = "real", IsNullable = true },
                new FieldModel { Name = "FixedAscii", Type = "char", Precision = 10, IsNullable = true },
                new FieldModel { Name = "Name", Type = "varchar", Precision = 50, IsNullable = false },
                new FieldModel { Name = "Description", Type = "varchar", Precision = -1, IsNullable = true },
                new FieldModel { Name = "FixedUnicode", Type = "nchar", Precision = 5, IsNullable = true },
                new FieldModel { Name = "UnicodeName", Type = "nvarchar", Precision = 50, IsNullable = false },
                new FieldModel { Name = "UnicodeBlob", Type = "nvarchar", Precision = -1, IsNullable = true },
                new FieldModel { Name = "WhenAvailable", Type = "date", IsNullable = true },
                new FieldModel { Name = "ShipTime", Type = "time", IsNullable = true },
                new FieldModel { Name = "OffsetTime", Type = "datetimeoffset", IsNullable = true },
                new FieldModel { Name = "BinaryFixed", Type = "binary", Precision = 50, IsNullable = true },
                new FieldModel { Name = "BinaryVar", Type = "varbinary", Precision = -1, IsNullable = true },
                new FieldModel { Name = "ExternalId", Type = "uniqueidentifier", IsNullable = true },
                new FieldModel { Name = "Metadata", Type = "xml", IsNullable = true }
            }
        };
        model.Tables[product.Name] = product;

        // Order table with bigint identity PK and FK to User
        var order = new TableModel
        {
            Name = "Order",
            Fields =
            {
                new FieldModel { Name = "OrderID", Type = "bigint", IsPrimaryKey = true, IsIdentity = true },
                new FieldModel { Name = "OrderDate", Type = "datetime2", IsNullable = false },
                new FieldModel { Name = "UserID", Type = "uniqueidentifier", IsNullable = false },
            },
            ForeignKeys =
            {
                new ForeignKeyModel { ColumnName = "UserID", TargetTable = "User", TargetColumnName = "UserID", IsNullable = false }
            }
        };
        model.Tables[order.Name] = order;

        // OrderItem table with FKs to Order and Product
        var orderItem = new TableModel
        {
            Name = "OrderItem",
            Fields =
            {
                new FieldModel { Name = "OrderItemID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                new FieldModel { Name = "OrderID", Type = "bigint", IsNullable = false },
                new FieldModel { Name = "ProductID", Type = "int", IsNullable = false },
                new FieldModel { Name = "Quantity", Type = "int", IsNullable = false },
                new FieldModel { Name = "UnitPrice", Type = "decimal", Precision = 18, Scale = 2, IsNullable = false }
            },
            ForeignKeys =
            {
                new ForeignKeyModel { ColumnName = "OrderID", TargetTable = "Order", TargetColumnName = "OrderID", IsNullable = false },
                new ForeignKeyModel { ColumnName = "ProductID", TargetTable = "Product", TargetColumnName = "ProductID", IsNullable = false }
            }
        };
        model.Tables[orderItem.Name] = orderItem;

        return model;
    }

    public static (DatabaseModel model, string mixinContent, string tableContent) BuildMixinModel()
    {
        var model = new DatabaseModel();

        var mixin = new MixinModel
        {
            Name = "Auditable",
            Fields =
            {
                new FieldModel { Name = "CreatedDateTime", Type = "datetime2", IsNullable = false },
                new FieldModel { Name = "LastModifiedDateTime", Type = "datetime2", IsNullable = true },
                new FieldModel { Name = "LockNumber", Type = "int", IsNullable = false }
            }
        };
        model.Mixins[mixin.Name] = mixin;

        var taskTable = new TableModel
        {
            Name = "Task",
            Fields =
            {
                new FieldModel { Name = "TaskID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                new FieldModel { Name = "Title", Type = "nvarchar", Precision = 200, IsNullable = false }
            }
        };
        // Apply mixin programmatically similar to Parser.ApplyMixin
        taskTable.Mixins.Add(mixin.Name);
        foreach (var field in mixin.Fields)
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
        model.Tables[taskTable.Name] = taskTable;

        return (model, string.Empty, string.Empty);
    }
}


