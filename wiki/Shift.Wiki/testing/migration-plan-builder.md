# MigrationPlanBuilder

## Overview

The `MigrationPlanBuilder` is a fluent builder pattern designed to simplify the creation of `MigrationPlan` instances in unit and integration tests. It provides a clean, readable API for constructing complex migration plans without the verbosity of direct object instantiation.

## Purpose

- **Test Setup**: Streamline creation of `MigrationPlan` objects for testing SqlMigrationPlanRunner
- **Readability**: Make test code more expressive and maintainable
- **Flexibility**: Support complex migration scenarios with minimal code
- **Consistency**: Standardize migration plan creation patterns across test suites

## Architecture

The builder pattern consists of five main classes:

- **`MigrationPlanBuilder`**: Root builder for the entire migration plan
- **`CreateTableStepBuilder`**: Builder for CreateTable migration steps
- **`AddColumnStepBuilder`**: Builder for AddColumn migration steps
- **`AlterColumnStepBuilder`**: Builder for AlterColumn migration steps
- **`AddIndexStepBuilder`**: Builder for AddIndex migration steps

## API Reference

### MigrationPlanBuilder

The main entry point for creating migration plans.

```csharp
// Create a new migration plan
var plan = MigrationPlanBuilder.Create()
    .WithCreateTableStep("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
    .WithAddColumnStep("User", column => column
        .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false)))
    .WithAddIndexStep("User", index => index
        .WithIndex("Email", isUnique: true))
    .Build();
```

#### Methods

- **`Create()`**: Creates a new `MigrationPlanBuilder` instance
- **`WithCreateTableStep(string tableName, Action<CreateTableStepBuilder> configure)`**: Adds a CreateTable step
- **`WithAddColumnStep(string tableName, Action<AddColumnStepBuilder> configure)`**: Adds an AddColumn step
- **`WithAlterColumnStep(string tableName, Action<AlterColumnStepBuilder> configure)`**: Adds an AlterColumn step
- **`WithAddForeignKeyStep(string tableName, string columnName, string targetTable, string targetColumnName, RelationshipType relationshipType)`**: Adds an AddForeignKey step
- **`WithAddIndexStep(string tableName, Action<AddIndexStepBuilder> configure)`**: Adds an AddIndex step
- **`Build()`**: Returns the constructed `MigrationPlan`

### CreateTableStepBuilder

Builds CreateTable migration steps with fields and constraints.

```csharp
.WithCreateTableStep("User", table => table
    .WithField("UserID", "int", f => f.PrimaryKey().Identity())
    .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false))
    .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false)))
```

#### Methods

- **`WithField(string name, string type, Action<FieldModelBuilder>? configure = null)`**: Adds a field to the table
- **`Build()`**: Returns the constructed `MigrationStep`

### AddColumnStepBuilder

Builds AddColumn migration steps for adding new columns to existing tables.

```csharp
.WithAddColumnStep("User", column => column
    .WithField("Phone", "nvarchar", f => f.Precision(20).Nullable(true))
    .WithField("Address", "nvarchar", f => f.Precision(500).Nullable(true)))
```

#### Methods

- **`WithField(string name, string type, Action<FieldModelBuilder>? configure = null)`**: Adds a field to the column addition
- **`Build()`**: Returns the constructed `MigrationStep`

### AlterColumnStepBuilder

Builds AlterColumn migration steps for modifying existing column definitions.

```csharp
.WithAlterColumnStep("User", column => column
    .WithField("Email", "nvarchar", f => f.Precision(512).Nullable(false)))
```

#### Methods

- **`WithField(string name, string type, Action<FieldModelBuilder>? configure = null)`**: Adds a field to the column alteration
- **`Build()`**: Returns the constructed `MigrationStep`

### AddIndexStepBuilder

Builds AddIndex migration steps for creating indexes on tables.

```csharp
.WithAddIndexStep("User", index => index
    .WithIndex("Email", isUnique: true)
    .WithIndex(new[] { "Email", "Username" }, isUnique: false))
```

#### Methods

- **`WithIndex(string columnName, bool isUnique = false)`**: Adds a single-column index
- **`WithIndex(IEnumerable<string> columnNames, bool isUnique = false)`**: Adds a multi-column index
- **`Build()`**: Returns the constructed `MigrationStep`

## Complete Examples

### Simple Single-Step Migration

```csharp
var plan = MigrationPlanBuilder.Create()
    .WithCreateTableStep("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
    .Build();
```

### Complex Multi-Step Migration

```csharp
var plan = MigrationPlanBuilder.Create()
    // Create User table
    .WithCreateTableStep("User", user => user
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false))
        .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false)))
    
    // Create Order table
    .WithCreateTableStep("Order", order => order
        .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
        .WithField("UserID", "int", f => f.Nullable(false))
        .WithField("OrderDate", "datetime", f => f.Nullable(false)))
    
    // Add foreign key
    .WithAddForeignKeyStep("Order", "UserID", "User", "UserID", RelationshipType.ManyToOne)
    
    // Add indexes
    .WithAddIndexStep("User", index => index
        .WithIndex("Email", isUnique: true)
        .WithIndex("Username", isUnique: false))
    
    .WithAddIndexStep("Order", index => index
        .WithIndex("UserID", isUnique: false)
        .WithIndex("OrderDate", isUnique: false))
    
    .Build();
```

### Index Variations

```csharp
var plan = MigrationPlanBuilder.Create()
    .WithCreateTableStep("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256))
        .WithField("Username", "nvarchar", f => f.Precision(100))
        .WithField("Department", "nvarchar", f => f.Precision(50)))
    
    // Single column unique index
    .WithAddIndexStep("User", index => index
        .WithIndex("Email", isUnique: true))
    
    // Multi-column non-unique index
    .WithAddIndexStep("User", index => index
        .WithIndex(new[] { "Department", "Username" }, isUnique: false))
    
    .Build();
```

### Column Operations

```csharp
var plan = MigrationPlanBuilder.Create()
    // Add new columns
    .WithAddColumnStep("User", column => column
        .WithField("Phone", "nvarchar", f => f.Precision(20).Nullable(true))
        .WithField("Address", "nvarchar", f => f.Precision(500).Nullable(true)))
    
    // Alter existing columns (safe widening)
    .WithAlterColumnStep("User", column => column
        .WithField("Email", "nvarchar", f => f.Precision(512).Nullable(false)))
    
    .Build();
```

## Integration with Tests

### SqlMigrationPlanRunner Tests

```csharp
[Fact]
public async Task Run_WithCreateTableStep_ShouldCreateTableSuccessfully()
{
    // Arrange
    var plan = MigrationPlanBuilder.Create()
        .WithCreateTableStep("TestUser", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
            .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(true)))
        .Build();

    var databaseName = SqlServerTestHelper.GenerateDatabaseName();
    var connectionString = SqlServerTestHelper.BuildDbConnectionString(containerFixture.ConnectionStringMaster, databaseName);
    
    await SqlServerTestHelper.CreateDatabaseAsync(containerFixture.ConnectionStringMaster, databaseName);
    
    try
    {
        var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = logger };
        var result = runner.Run();
        
        // Assert
        result.Should().BeEmpty("Table creation should complete without failures");
    }
    finally
    {
        await SqlServerTestHelper.DropDatabaseAsync(containerFixture.ConnectionStringMaster, databaseName);
    }
}
```

### Complex Migration Test

```csharp
[Fact]
public async Task Run_WithMixedMigrationSteps_ShouldExecuteInCorrectOrder()
{
    // Arrange
    var plan = MigrationPlanBuilder.Create()
        .WithCreateTableStep("User", user => user
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
        .WithAddColumnStep("User", column => column
            .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false)))
        .WithAddIndexStep("User", index => index
            .WithIndex("Email", isUnique: true))
        .Build();

    // Act & Assert
    var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = logger };
    var result = runner.Run();
    result.Should().BeEmpty("Migration should complete without errors");
}
```

## Best Practices

### 1. Use Descriptive Step Names
```csharp
// Good - clear step purpose
.WithCreateTableStep("User", user => user
    .WithField("UserID", "int", f => f.PrimaryKey().Identity()))

// Avoid - unclear purpose
.WithCreateTableStep("T1", t => t
    .WithField("ID", "int", f => f.PrimaryKey().Identity()))
```

### 2. Group Related Operations
```csharp
// Good - logical grouping
var plan = MigrationPlanBuilder.Create()
    .WithCreateTableStep("User", user => user
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256)))
    .WithAddIndexStep("User", index => index
        .WithIndex("Email", isUnique: true))
    .Build();
```

### 3. Use Consistent Field Configuration
```csharp
// Good - consistent pattern
.WithField("Email", "nvarchar", f => f
    .Precision(256)
    .Nullable(false)
    .PrimaryKey(false))

// Avoid - scattered configuration
.WithField("Email", "nvarchar", f => f.Precision(256))
// ... other code ...
.WithField("Email", "nvarchar", f => f.Nullable(false))
```

### 4. Test-Specific Plans
```csharp
// Create focused plans for specific test scenarios
private static MigrationPlan CreatePlanWithIndexes()
{
    return MigrationPlanBuilder.Create()
        .WithCreateTableStep("User", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Email", "nvarchar", f => f.Precision(256)))
        .WithAddIndexStep("User", index => index
            .WithIndex("Email", isUnique: true))
        .Build();
}
```

## Comparison with DatabaseModelBuilder

### Similarities
- **Fluent API**: Both use method chaining for readability
- **Field Configuration**: Both use `FieldModelBuilder` for field setup
- **Test Focus**: Both are designed for test scenarios
- **Consistency**: Both follow similar naming and structure patterns

### Differences
- **Purpose**: `DatabaseModelBuilder` creates database models, `MigrationPlanBuilder` creates migration plans
- **Scope**: `DatabaseModelBuilder` handles tables, fields, indexes, foreign keys, and mixins; `MigrationPlanBuilder` handles migration steps
- **Usage**: `DatabaseModelBuilder` for model comparison tests, `MigrationPlanBuilder` for migration execution tests
- **Complexity**: `MigrationPlanBuilder` is simpler as it focuses on step-by-step operations

## When to Use

### ✅ Use MigrationPlanBuilder When:
- Writing tests for `SqlMigrationPlanRunner`
- Testing migration execution scenarios
- Creating complex migration plans for integration tests
- Need readable, maintainable migration test setup code

### ❌ Don't Use MigrationPlanBuilder When:
- Building production migration plans (use `MigrationPlanner` instead)
- Need to test migration plan generation (use `MigrationPlanner` with `DatabaseModelBuilder`)
- Working with existing migration plans (use direct `MigrationPlan` construction)

## Performance Considerations

- **Memory Efficient**: Builders create objects in-memory only
- **No Database Impact**: Builders don't interact with actual databases
- **Fast Construction**: Fluent API is optimized for test scenarios
- **Short-Lived**: Plans are typically used once per test

## Limitations

1. **No Support for Complex Constraints**: The builder doesn't support check constraints, computed columns, or triggers
2. **No Schema Support**: All objects are created in the default schema
3. **No Data Migration**: No support for data transformation or migration
4. **Step-Only Focus**: Only handles migration steps, not complete database models

## Integration Points

### With SqlMigrationPlanRunner
```csharp
var plan = MigrationPlanBuilder.Create()
    .WithCreateTableStep("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity()))
    .Build();

var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = logger };
var failures = runner.Run();
```

### With Test Infrastructure
```csharp
[Collection("SqlServer")]
public class SqlMigrationPlanRunnerTests
{
    private readonly SqlServerContainerFixture _containerFixture;
    
    [Fact]
    public async Task Run_WithComplexPlan_ShouldExecuteSuccessfully()
    {
        var plan = MigrationPlanBuilder.Create()
            // ... complex plan setup
            .Build();
            
        // Test execution with Docker container
    }
}
```

The `MigrationPlanBuilder` provides an excellent balance of flexibility, readability, and performance for testing migration execution scenarios in the Shift framework.
