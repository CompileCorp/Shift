# MigrationPlanner Architecture

## Overview

The `MigrationPlanner` is a core component of the Shift framework responsible for analyzing the differences between a target database model and an actual database model, then generating a comprehensive migration plan to transform the actual model into the target model.

## Purpose and Responsibilities

- **Model Comparison**: Compare target and actual database models to identify differences
- **Migration Planning**: Generate a step-by-step migration plan to achieve the target state
- **Extra Reporting**: Identify and report database objects that exist in the actual model but not in the target model
- **Case-Insensitive Matching**: Handle database object names in a case-insensitive manner for cross-platform compatibility

## The 4-Step Migration Planning Algorithm

The MigrationPlanner follows a systematic 4-step approach to generate migration plans:

### Step 1: Create Missing Tables
**Purpose**: Identify tables that exist in the target model but not in the actual model.

**Detection Logic**:
- Compares table names using case-insensitive matching
- Creates `CreateTable` migration steps for missing tables
- Automatically includes foreign key creation for tables that reference existing target tables

**Example**:
```csharp
// Target model has "User" table, actual model is empty
// Result: Creates MigrationStep with Action = CreateTable
```

### Step 2: Add Missing Columns to Existing Tables
**Purpose**: Identify columns that exist in target tables but not in actual tables.

**Detection Logic**:
- For each target table, finds the corresponding actual table (case-insensitive)
- Compares field names using case-insensitive matching
- Creates `AddColumn` migration steps for missing fields
- Detects safe widening operations for string/binary types (e.g., nvarchar(50) → nvarchar(100))

**Safe Widening Rules**:
- `varchar(n)` → `varchar(m)` where m > n
- `nvarchar(n)` → `nvarchar(m)` where m > n
- `varchar(n)` → `varchar(MAX)`
- `binary(n)` → `binary(m)` where m > n
- `varbinary(n)` → `varbinary(m)` where m > n

**Example**:
```csharp
// Target: User table with Email field
// Actual: User table without Email field
// Result: Creates MigrationStep with Action = AddColumn
```

### Step 3: Add Missing Foreign Keys
**Purpose**: Identify foreign key relationships that exist in the target model but not in the actual model.

**Detection Logic**:
- Compares foreign key definitions between target and actual tables
- Uses case-insensitive matching for table and column names
- Only creates foreign keys for tables that exist in the target model
- Creates `AddForeignKey` migration steps

**Example**:
```csharp
// Target: Order table with FK to User table
// Actual: Order table without FK
// Result: Creates MigrationStep with Action = AddForeignKey
```

### Step 4: Add Missing Indexes for Existing Tables
**Purpose**: Identify indexes that exist in the target model but not in the actual model.

**Detection Logic**:
- Compares index definitions between target and actual tables
- Uses case-insensitive matching for field names
- Distinguishes between unique and non-unique indexes
- Handles multi-column indexes with proper field order matching
- Creates `AddIndex` migration steps for missing indexes
- Reports extra indexes (indexes in actual but not in target) via `ExtraIndexReport`

**Index Matching Rules**:
- Field names are compared case-insensitively
- Field order matters for multi-column indexes
- Unique vs non-unique indexes are treated as different
- Extra indexes are reported but not included in migration steps

**Example**:
```csharp
// Target: User table with unique index on Email
// Actual: User table without index on Email
// Result: Creates MigrationStep with Action = AddIndex
```

## Extra Reporting

The MigrationPlanner identifies database objects that exist in the actual model but not in the target model:

### ExtraIndexReport
Reports indexes that exist in the actual database but are not defined in the target model:

```csharp
public class ExtraIndexReport
{
    public required string TableName { get; init; }
    public required bool IsUnique { get; init; }
    public required IEnumerable<string> Fields { get; init; }
}
```

**Key Features**:
- **Non-destructive reporting**: Extra indexes are reported but not removed
- **Case-insensitive matching**: Field names are compared case-insensitively
- **Order preservation**: Field order in multi-column indexes is preserved
- **Unique vs non-unique**: Distinguishes between unique and non-unique indexes

**Usage**:
```csharp
// Access extra indexes from migration plan
foreach (var extraIndex in plan.ExtrasInSqlServer.ExtraIndexes)
{
    Console.WriteLine($"Extra index on {extraIndex.TableName}: " +
                     $"{(extraIndex.IsUnique ? "UNIQUE" : "NON-UNIQUE")} " +
                     $"({string.Join(", ", extraIndex.Fields)})");
}
```

These extra indexes are reported in `plan.ExtrasInSqlServer.ExtraIndexes` but are not included in the migration plan.

## Code Examples

### Basic Usage

```csharp
var migrationPlanner = new MigrationPlanner();
var plan = migrationPlanner.GeneratePlan(targetModel, actualModel);

// Process migration steps
foreach (var step in plan.Steps)
{
    switch (step.Action)
    {
        case MigrationAction.CreateTable:
            // Create table logic
            break;
        case MigrationAction.AddColumn:
            // Add column logic
            break;
        case MigrationAction.AddForeignKey:
            // Add foreign key logic
            break;
        case MigrationAction.AlterColumn:
            // Alter column logic
            break;
        case MigrationAction.AddIndex:
            // Add index logic
            // Example: CREATE UNIQUE INDEX IX_User_Email ON [dbo].[User]([Email])
            break;
    }
}

// Handle extra indexes
foreach (var extraIndex in plan.ExtrasInSqlServer.ExtraIndexes)
{
    Console.WriteLine($"Extra index: {extraIndex.TableName} ({string.Join(", ", extraIndex.Fields)})");
}
```

### Using with DatabaseModelBuilder

```csharp
// Create target model with indexes
var targetModel = DatabaseModelBuilder.Create()
    .WithTable("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256))
        .WithField("Username", "nvarchar", f => f.Precision(100))
        .WithIndex("IX_User_Email", "Email", isUnique: true)
        .WithIndex("IX_User_Username", "Username", isUnique: false))
    .Build();

// Create actual model without indexes
var actualModel = DatabaseModelBuilder.Create()
    .WithTable("User", table => table
        .WithField("UserID", "int", f => f.PrimaryKey().Identity())
        .WithField("Email", "nvarchar", f => f.Precision(256))
        .WithField("Username", "nvarchar", f => f.Precision(100)))
    .Build();

var plan = migrationPlanner.GeneratePlan(targetModel, actualModel);
// Result: Plan will contain AddIndex steps for the missing indexes
```

## Integration with SqlMigrationPlanRunner

The `SqlMigrationPlanRunner` executes the migration plan generated by `MigrationPlanner`:

```csharp
var runner = new SqlMigrationPlanRunner(connectionString, plan)
{
    Logger = logger
};

var failures = runner.Run();
```

### Supported Migration Actions

The `SqlMigrationPlanRunner` supports the following actions:
- ✅ `CreateTable` - Creates tables with fields and constraints
- ✅ `AddColumn` - Adds columns to existing tables
- ✅ `AddForeignKey` - Creates foreign key constraints
- ✅ `AlterColumn` - Modifies column definitions (safe widening)
- ✅ `AddIndex` - Creates single and multi-column indexes (unique and non-unique)

## Case-Insensitive Matching

All database object comparisons use case-insensitive matching to ensure cross-platform compatibility:

- **Table Names**: "User" matches "USER" or "user"
- **Column Names**: "Email" matches "EMAIL" or "email"
- **Index Fields**: ["Email", "Username"] matches ["EMAIL", "USERNAME"]
- **Foreign Key References**: Case-insensitive table and column matching

## Error Handling

The MigrationPlanner is designed to be robust and handle various edge cases:

- **Missing Target Tables**: Foreign keys to non-existent tables are ignored
- **Case Variations**: All comparisons are case-insensitive
- **Extra Objects**: Objects in actual but not target are reported, not migrated
- **Safe Operations**: Only safe widening operations are included in migration plans

## Performance Considerations

- **Linear Complexity**: O(n) where n is the number of database objects
- **Memory Efficient**: Processes objects in streams rather than loading all into memory
- **Case-Insensitive Optimized**: Uses efficient string comparison methods
