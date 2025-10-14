# DatabaseModelBuilder

## Overview

The `DatabaseModelBuilder` is a fluent builder pattern designed to simplify the creation of `DatabaseModel` instances in unit and integration tests. It provides a clean, readable API for constructing complex database models without the verbosity of direct object instantiation.

## Purpose

- **Test Setup**: Streamline creation of `DatabaseModel` objects for testing
- **Readability**: Make test code more expressive and maintainable
- **Flexibility**: Support complex model configurations with minimal code
- **Consistency**: Standardize model creation patterns across test suites

## Architecture

The builder pattern consists of four main classes:

- **`DatabaseModelBuilder`**: Root builder for the entire database model
- **`TableModelBuilder`**: Builder for individual tables
- **`FieldModelBuilder`**: Builder for table fields/columns
- **`MixinModelBuilder`**: Builder for reusable field sets (mixins)

## API Reference

### DatabaseModelBuilder

The main entry point for creating database models.

```csharp
// Create a new database model
var model = DatabaseModelBuilder.Create()
    .WithTable("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
    .WithMixin("Auditable", mixin => mixin
        .WithField("CreatedAt", "datetime", f => f.Nullable(false))
        .WithField("UpdatedAt", "datetime", f => f.Nullable(false)))
    .Build();
```

#### Methods

- **`Create()`**: Creates a new `DatabaseModelBuilder` instance
- **`WithTable(string name, Action<TableModelBuilder> configure)`**: Adds a table to the model
- **`WithMixin(string name, Action<MixinModelBuilder> configure)`**: Adds a mixin to the model
- **`Build()`**: Returns the constructed `DatabaseModel`

### TableModelBuilder

Builds individual table models with fields, indexes, foreign keys, and attributes.

```csharp
.WithTable("User", table => table
    .WithField("UserID", "int", f => f.PrimaryKey().Identity())
    .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false))
    .WithField("Username", "nvarchar", f => f.Precision(100))
    .WithIndex("IX_User_Email", "Email", isUnique: true)
    .WithIndex("IX_User_Email_Username", new[] { "Email", "Username" }, isUnique: false)
    .WithForeignKey("UserID", "Profile", "UserID", RelationshipType.OneToOne)
    .WithAttribute("Auditable", true))
```

#### Methods

- **`WithField(string name, string type, Action<FieldModelBuilder>? configure = null)`**: Adds a field to the table
- **`WithIndex(string name, string columnName, bool isUnique = false)`**: Adds a single-column index
- **`WithIndex(string name, IEnumerable<string> columnNames, bool isUnique = false)`**: Adds a multi-column index
- **`WithForeignKey(string columnName, string targetTable, string targetColumnName, RelationshipType relationshipType)`**: Adds a foreign key
- **`WithAttribute(string key, bool value)`**: Adds a table attribute
- **`Build()`**: Returns the constructed `TableModel`

### FieldModelBuilder

Builds individual field models with type information, constraints, and properties.

```csharp
.WithField("Email", "nvarchar", f => f
    .Precision(256)
    .Nullable(false)
    .PrimaryKey(false)
    .Identity(false))
```

#### Methods

- **`PrimaryKey(bool isPrimaryKey = true)`**: Sets the primary key flag
- **`Identity(bool isIdentity = true)`**: Sets the identity flag
- **`Nullable(bool isNullable = true)`**: Sets the nullable flag
- **`Optional(bool isOptional = true)`**: Sets the optional flag
- **`Precision(int precision)`**: Sets the precision for string/binary types
- **`Precision(int precision, int scale)`**: Sets precision and scale for decimal types
- **`Scale(int scale)`**: Sets the scale for decimal types
- **`Build()`**: Returns the constructed `FieldModel`

### MixinModelBuilder

Builds reusable field sets that can be applied to multiple tables.

```csharp
.WithMixin("Auditable", mixin => mixin
    .WithField("CreatedAt", "datetime", f => f.Nullable(false))
    .WithField("UpdatedAt", "datetime", f => f.Nullable(false))
    .WithField("CreatedBy", "int", f => f.Nullable(false))
    .WithField("UpdatedBy", "int", f => f.Nullable(false)))
```

#### Methods

- **`WithField(string name, string type, Action<FieldModelBuilder>? configure = null)`**: Adds a field to the mixin
- **`Build()`**: Returns the constructed `MixinModel`

## Complete Examples

### Simple User Table

```csharp
var model = DatabaseModelBuilder.Create()
    .WithTable("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false))
        .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
        .WithField("IsActive", "bit", f => f.Nullable(false)))
    .Build();
```

### Complex E-Commerce Model

```csharp
var model = DatabaseModelBuilder.Create()
    .WithTable("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false))
        .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
        .WithIndex("IX_User_Email", "Email", isUnique: true)
        .WithIndex("IX_User_Username", "Username", isUnique: false))
    
    .WithTable("Product", table => table
        .WithField("ProductID", "int", f => f.PrimaryKey().Identity())
        .WithField("Name", "nvarchar", f => f.Precision(200).Nullable(false))
        .WithField("Price", "decimal", f => f.Precision(10, 2).Nullable(false))
        .WithField("CategoryID", "int", f => f.Nullable(false))
        .WithForeignKey("CategoryID", "Category", "CategoryID", RelationshipType.ManyToOne)
        .WithIndex("IX_Product_CategoryID", "CategoryID", isUnique: false))
    
    .WithTable("Order", table => table
        .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
        .WithField("UserID", "int", f => f.Nullable(false))
        .WithField("OrderDate", "datetime", f => f.Nullable(false))
        .WithField("TotalAmount", "decimal", f => f.Precision(10, 2).Nullable(false))
        .WithForeignKey("UserID", "User", "UserID", RelationshipType.ManyToOne)
        .WithIndex("IX_Order_UserID", "UserID", isUnique: false)
        .WithIndex("IX_Order_OrderDate", "OrderDate", isUnique: false))
    
    .WithMixin("Auditable", mixin => mixin
        .WithField("CreatedAt", "datetime", f => f.Nullable(false))
        .WithField("UpdatedAt", "datetime", f => f.Nullable(false))
        .WithField("CreatedBy", "int", f => f.Nullable(false))
        .WithField("UpdatedBy", "int", f => f.Nullable(false)))
    
    .Build();
```

### Multi-Column Indexes

```csharp
var model = DatabaseModelBuilder.Create()
    .WithTable("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256))
        .WithField("Username", "nvarchar", f => f.Precision(100))
        .WithField("Department", "nvarchar", f => f.Precision(50))
        .WithIndex("IX_User_Email_Username", new[] { "Email", "Username" }, isUnique: false)
        .WithIndex("IX_User_Department_Username", new[] { "Department", "Username" }, isUnique: true))
    .Build();
```

## Best Practices

### 1. Use Descriptive Names
```csharp
// Good
.WithField("UserID", "int", f => f.PrimaryKey().Identity())
.WithIndex("IX_User_Email", "Email", isUnique: true)

// Avoid
.WithField("ID", "int", f => f.PrimaryKey().Identity())
.WithIndex("IX1", "Email", isUnique: true)
```

### 2. Group Related Configuration
```csharp
// Good - group field configuration
.WithField("Email", "nvarchar", f => f
    .Precision(256)
    .Nullable(false)
    .PrimaryKey(false))

// Avoid - scattered configuration
.WithField("Email", "nvarchar", f => f.Precision(256))
// ... other code ...
.WithField("Email", "nvarchar", f => f.Nullable(false))
```

### 3. Use Mixins for Common Patterns
```csharp
// Define once
.WithMixin("Auditable", mixin => mixin
    .WithField("CreatedAt", "datetime", f => f.Nullable(false))
    .WithField("UpdatedAt", "datetime", f => f.Nullable(false)))

// Reuse across tables
.WithTable("User", table => table
    .WithField("UserID", "int", f => f.PrimaryKey().Identity())
    // Auditable fields would be applied here in real usage)
```

### 4. Test-Specific Models
```csharp
// Create focused models for specific test scenarios
private static DatabaseModel CreateModelWithIndexes()
{
    return DatabaseModelBuilder.Create()
        .WithTable("User", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Email", "nvarchar", f => f.Precision(256))
            .WithIndex("IX_User_Email", "Email", isUnique: true))
        .Build();
}
```

## Integration with Tests

### MigrationPlanner Tests
```csharp
[Fact]
public void GeneratePlan_WithMissingIndexes_ShouldAddIndexSteps()
{
    // Arrange
    var targetModel = DatabaseModelBuilder.Create()
        .WithTable("User", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Email", "nvarchar", f => f.Precision(256))
            .WithIndex("IX_User_Email", "Email", isUnique: true))
        .Build();
    
    var actualModel = DatabaseModelBuilder.Create()
        .WithTable("User", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Email", "nvarchar", f => f.Precision(256)))
        .Build();

    // Act
    var plan = Sut.GeneratePlan(targetModel, actualModel);

    // Assert
    plan.Steps.Should().HaveCount(1);
    plan.Steps.Should().Contain(step => step.Action == MigrationAction.AddIndex);
}
```

### ModelExporter Tests
```csharp
[Fact]
public async Task GenerateDmdContent_WithSimpleTable_ShouldGenerateCorrectContent()
{
    // Arrange
    var model = DatabaseModelBuilder.Create()
        .WithTable("User", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
        .Build();

    // Act & Assert
    foreach (var table in model.Tables.Values)
    {
        var dmdContent = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());
        await Verify(dmdContent).UseTextForParameters($"{table.Name}.dmd");
    }
}
```

## Limitations

1. **No Support for Complex Constraints**: The builder doesn't support check constraints, computed columns, or triggers
2. **No Schema Support**: All objects are created in the default schema
3. **No Partitioning**: Table partitioning is not supported
4. **No Custom Attributes**: Field-level custom attributes are not supported (only table-level boolean attributes)

## When to Use

### ✅ Use DatabaseModelBuilder When:
- Writing unit tests for MigrationPlanner, ModelExporter, or other components
- Creating test data for integration tests
- Building complex database models for testing scenarios
- Need readable, maintainable test setup code

### ❌ Don't Use DatabaseModelBuilder When:
- Building production database models (use Parser or SqlServerLoader instead)
- Need complex database features not supported by the builder
- Working with existing database schemas (use SqlServerLoader instead)

## Performance Considerations

- **Memory Efficient**: Builders create objects in-memory only
- **No Database Impact**: Builders don't interact with actual databases
- **Fast Construction**: Fluent API is optimized for test scenarios
- **Garbage Collection**: Models are short-lived in test scenarios

The `DatabaseModelBuilder` is specifically designed for test scenarios and provides an excellent balance of flexibility, readability, and performance for unit and integration testing.
