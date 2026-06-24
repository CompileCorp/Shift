# MigrationPlanBuilder

## Overview

The `MigrationPlanBuilder` is a fluent builder pattern designed to simplify the creation of `MigrationPlan` instances in unit and integration tests. It provides a clean, readable API for constructing migration plans without the verbosity of direct object instantiation.

## Purpose

- **Test Setup**: Streamline creation of `MigrationPlan` objects for testing SqlMigrationPlanRunner
- **Readability**: Make test code more expressive and maintainable
- **Flexibility**: Support complex migration scenarios with minimal code
- **Consistency**: Standardize migration plan creation patterns across test suites

## Architecture

`MigrationPlanBuilder` is a single flat builder class. Each `With*` method appends a `MigrationStep` to the plan and returns the builder for chaining. Table/field configuration is delegated to the shared `TableModelBuilder` and `FieldModelBuilder` from `DatabaseModelBuilder` (see [DatabaseModelBuilder](database-model-builder.md)).

## API Reference

### MigrationPlanBuilder

The main entry point for creating migration plans.

```csharp
// Create a new migration plan
var plan = MigrationPlanBuilder.Create()
    .WithCreateTable("TestUser", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
    .WithAddColumn("TestUser", "Username", "nvarchar", f => f.Precision(100).Nullable(false))
    .WithAddIndex("TestUser", "IX_TestUser_Email", "Email", isUnique: true)
    .Build();
```

#### Methods

- **`Create()`**: Creates a new `MigrationPlanBuilder` instance
- **`WithCreateTable(string tableName, Action<TableModelBuilder> configure)`**: Adds a `CreateTable` step. The supplied `TableModelBuilder` is built and its `Fields` are attached to the step.
- **`WithAddColumn(string tableName, string fieldName, string fieldType, Action<FieldModelBuilder>? configure = null)`**: Adds an `AddColumn` step for a single field.
- **`WithAlterColumn(string tableName, string fieldName, string fieldType, Action<FieldModelBuilder>? configure = null)`**: Adds an `AlterColumn` step for a single field.
- **`WithAddForeignKey(string tableName, string columnName, string targetTable, string targetColumn, RelationshipType relationshipType = RelationshipType.OneToMany)`**: Adds an `AddForeignKey` step.
- **`WithAddIndex(string tableName, string indexName, string columnName, bool isUnique = false, IndexKind kind = IndexKind.NonClustered)`**: Adds an `AddIndex` step for a single-column index.
- **`WithAddIndex(string tableName, string indexName, IEnumerable<string> columnNames, bool isUnique = false, IndexKind kind = IndexKind.NonClustered)`**: Adds an `AddIndex` step for a multi-column index.
- **`Build()`**: Returns the constructed `MigrationPlan`

> Note: `indexName` is accepted for readability but is not stored on the resulting `IndexModel` (the runner derives index names from the table and fields). It documents intent at the call site.

### CreateTable steps

`WithCreateTable` uses the shared `TableModelBuilder`, so fields are configured exactly as with `DatabaseModelBuilder`.

```csharp
.WithCreateTable("TestUser", table => table
    .WithField("UserID", "int", f => f.PrimaryKey().Identity())
    .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
    .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(true))
    .WithField("IsActive", "bit", f => f.Nullable(false))
    .WithField("CreatedDate", "datetime2", f => f.Nullable(false)))
```

### AddColumn / AlterColumn steps

Both take the table name, field name, field type, and an optional `FieldModelBuilder` configuration delegate.

```csharp
// Add a new column
.WithAddColumn("TestUser", "Username", "nvarchar", f => f.Precision(100).Nullable(false))

// Alter an existing column (e.g. safe widening)
.WithAlterColumn("TestUser", "Username", "nvarchar", f => f.Precision(200).Nullable(false))

// Alter a decimal column's precision/scale
.WithAlterColumn("TestProduct", "Price", "decimal", f => f.Precision(18, 4).Nullable(false))
```

### AddForeignKey steps

```csharp
.WithAddForeignKey("Order", "UserID", "User", "UserID", RelationshipType.OneToMany)
```

`relationshipType` defaults to `RelationshipType.OneToMany` and may be omitted.

### AddIndex steps

Single-column and multi-column overloads are available. `isUnique` and `kind` are optional.

```csharp
// Single-column index
.WithAddIndex("User", "EmailUsername", "Email", isUnique: true)

// Multi-column index
.WithAddIndex("User", "EmailUsername", new[] { "Email", "Username" }, isUnique: false)

// Specify the index kind explicitly
.WithAddIndex("User", "EmailUsername", "Email", isUnique: false, kind: IndexKind.NonClustered)
```

## Complete Examples

### Simple Single-Step Migration

```csharp
var plan = MigrationPlanBuilder.Create()
    .WithCreateTable("TestUser", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
    .Build();
```

### Complex Multi-Step Migration

```csharp
var plan = MigrationPlanBuilder.Create()
    // Create User table
    .WithCreateTable("User", user => user
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false)))

    // Create Order table
    .WithCreateTable("Order", order => order
        .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
        .WithField("UserID", "int", f => f.Nullable(false)))

    // Add foreign key
    .WithAddForeignKey("Order", "UserID", "User", "UserID", RelationshipType.OneToMany)

    // Add a column to an existing table
    .WithAddColumn("User", "Email", "nvarchar", f => f.Precision(256).Nullable(false))

    // Add indexes
    .WithAddIndex("User", "IX_User_Email", "Email", isUnique: true)
    .Build();
```

### Index Variations

```csharp
var plan = MigrationPlanBuilder.Create()
    // Single-column unique index
    .WithAddIndex("User", "IX_User_Email", "Email", isUnique: true)
    // Multi-column non-unique index
    .WithAddIndex("User", "IX_User_Dept_Username", new[] { "Department", "Username" }, isUnique: false)
    .Build();
```

### Column Operations

```csharp
var plan = MigrationPlanBuilder.Create()
    // Add a new column
    .WithAddColumn("User", "Phone", "nvarchar", f => f.Precision(20).Nullable(true))
    // Alter an existing column (safe widening)
    .WithAlterColumn("User", "Email", "nvarchar", f => f.Precision(512).Nullable(false))
    .Build();
```

## Integration with Tests

### SqlMigrationPlanRunner Tests

The real integration tests in `src/test/Shift.Tests/Integration/SqlMigrationPlanRunnerTests.cs` follow this pattern:

```csharp
[Fact]
public async Task Run_WithCreateTable_ShouldCreateTableSuccessfully()
{
    // Arrange
    var plan = MigrationPlanBuilder.Create()
        .WithCreateTable("TestUser", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
            .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(true)))
        .Build();

    var databaseName = SqlServerTestHelper.GenerateDatabaseName();
    var connectionString = SqlServerTestHelper.BuildDbConnectionString(
        _containerFixture.ConnectionStringMaster, databaseName);

    await SqlServerTestHelper.CreateDatabaseAsync(
        _containerFixture.ConnectionStringMaster, databaseName);

    try
    {
        var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = _logger };
        var result = runner.Run();

        // Assert
        result.Should().BeEmpty("Table creation should complete without failures");
    }
    finally
    {
        await SqlServerTestHelper.DropDatabaseAsync(
            _containerFixture.ConnectionStringMaster, databaseName);
    }
}
```

### Add Column / Add Foreign Key

```csharp
// Add a column to an existing table
var plan = MigrationPlanBuilder.Create()
    .WithAddColumn("TestUser", "Username", "nvarchar", f => f.Precision(100).Nullable(false))
    .Build();

// Add a foreign key constraint
var plan = MigrationPlanBuilder.Create()
    .WithAddForeignKey("Order", "UserID", "User", "UserID", RelationshipType.OneToMany)
    .Build();
```

## Best Practices

### 1. Use Descriptive Names
```csharp
// Good - clear purpose
.WithCreateTable("User", user => user
    .WithField("UserID", "int", f => f.PrimaryKey().Identity()))

// Avoid - unclear purpose
.WithCreateTable("T1", t => t
    .WithField("ID", "int", f => f.PrimaryKey().Identity()))
```

### 2. Group Related Operations
```csharp
var plan = MigrationPlanBuilder.Create()
    .WithCreateTable("User", user => user
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256)))
    .WithAddIndex("User", "IX_User_Email", "Email", isUnique: true)
    .Build();
```

### 3. Use Consistent Field Configuration
```csharp
.WithField("Email", "nvarchar", f => f
    .Precision(256)
    .Nullable(false))
```

### 4. Test-Specific Plans
```csharp
private static MigrationPlan CreatePlanWithIndexes()
{
    return MigrationPlanBuilder.Create()
        .WithCreateTable("User", table => table
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Email", "nvarchar", f => f.Precision(256)))
        .WithAddIndex("User", "IX_User_Email", "Email", isUnique: true)
        .Build();
}
```

## Comparison with DatabaseModelBuilder

### Similarities
- **Fluent API**: Both use method chaining for readability
- **Field Configuration**: Both reuse `TableModelBuilder` / `FieldModelBuilder` for table and field setup
- **Test Focus**: Both are designed for test scenarios

### Differences
- **Purpose**: `DatabaseModelBuilder` creates `DatabaseModel` instances; `MigrationPlanBuilder` creates `MigrationPlan` instances composed of `MigrationStep`s
- **Shape**: `DatabaseModelBuilder` exposes nested `TableModelBuilder`/`MixinModelBuilder`; `MigrationPlanBuilder` is a single flat builder whose methods append individual steps
- **Usage**: `DatabaseModelBuilder` for model comparison/planning tests, `MigrationPlanBuilder` for migration execution tests

## When to Use

### Use MigrationPlanBuilder When:
- Writing tests for `SqlMigrationPlanRunner`
- Testing migration execution scenarios
- Creating complex migration plans for integration tests
- You need readable, maintainable migration test setup code

### Don't Use MigrationPlanBuilder When:
- Building production migration plans (use `MigrationPlanner` instead)
- Testing migration plan *generation* (use `MigrationPlanner` with `DatabaseModelBuilder`)

## Limitations

1. **One Field per AddColumn/AlterColumn Step**: `WithAddColumn`/`WithAlterColumn` add a single field per call; chain multiple calls for multiple columns.
2. **No Schema Support**: All objects are created in the default schema.
3. **No Data Migration**: No support for data transformation or migration.
4. **Index Names Not Persisted**: The `indexName` argument is for call-site readability only.

## Integration Points

### With SqlMigrationPlanRunner
```csharp
var plan = MigrationPlanBuilder.Create()
    .WithCreateTable("User", table => table
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

    public SqlMigrationPlanRunnerTests(SqlServerContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
    }

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
