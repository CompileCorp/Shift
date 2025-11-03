# DMD File Format Reference

The DMD (Database Model Definition) file format is Shift's domain-specific language for defining database schemas. DMD files provide a clean, readable syntax for describing database models that can be automatically converted to SQL Server schemas and Entity Framework code.

## Overview

### What are DMD Files?

DMD files are text files that define database models using a simple, declarative syntax. They serve as the source of truth for your database schema and enable:

- **Database-first development** with human-readable schema definitions
- **Automatic migration generation** by comparing DMD files with existing databases
- **Entity Framework code generation** from schema definitions
- **Version control friendly** schema management
- **Team collaboration** with clear, readable database models

### ⚠️ Important: SQL-DMD-SQL Roundtrip Conversion

When converting from SQL Server to DMD format and back, certain SQL types will be converted to their modern equivalents:

| Original SQL Type | DMD Type        | Converted Back To |
|-------------------|-----------------|-------------------|
| `text`            | `astring(max)`  | `varchar(max)`    |
| `ntext`           | `ustring(max)`  | `nvarchar(max)`   |
| `money`           | `decimal(19,4)` | `decimal(19,4)`   |
| `smallmoney`      | `decimal(10,4)` | `decimal(10,4)`   |
| `numeric(p,s)`    | `decimal(p,s)`  | `decimal(p,s)`    |

This conversion is **intentional** and represents best practices for modern SQL Server development. The deprecated `text` and `ntext` types are converted to their `varchar(max)` and `nvarchar(max)` equivalents, while `money` and `smallmoney` are normalized to `decimal` with appropriate precision and scale.

### File Extensions

- **`.dmd`** - Database model files (tables, relationships, indexes)
- **`.dmdx`** - Mixin files (reusable field sets)

### Integration with Shift

DMD files are processed by Shift's `Parser` class and converted into `DatabaseModel` objects that can be:
- Compared with existing databases to generate migrations
- Exported back to DMD format for round-trip validation
- Used to generate Entity Framework code
- Applied to SQL Server databases

## Basic Syntax

### Model Declaration

```dmd
model TableName {
  // field definitions
}
```

### Field Declaration

```dmd
type fieldName
type? nullableFieldName
type(precision) fieldName
type(precision, scale) fieldName
```

### Comments and Whitespace

- **Comments**: Not currently supported (planned for future versions)
- **Whitespace**: Significant for field indentation
- **Case sensitivity**: Field names and types are case-sensitive
- **Line endings**: Unix-style (`\n`) and Windows-style (`\r\n`) supported

## Data Types

### Current Types (Recommended)

#### Simple Types
| DMD Type       | SQL Server Type    | Default Precision | Description |
|----------------|--------------------|-------------------|-------------|
| `bool`         | `bit`              | -                 | Boolean value (true/false) |
| `int`          | `int`              | -                 | 32-bit integer |
| `long`         | `bigint`           | -                 | 64-bit integer |
| `decimal(p,s)` | `decimal(p,s)`     | 18,0              | Fixed-point decimal number |
| `float`        | `float`            | -                 | Floating-point number |
| `guid`         | `uniqueidentifier` | -                 | Globally unique identifier |
| `datetime`     | `datetime`         | -                 | Date and time |

#### String Types

##### Unicode String Types
| DMD Type          | SQL Server Type    | Default Length | Description |
|-------------------|--------------------|----------------|-------------|
| `ustring`         | `nvarchar(255)`    | 255            | Unicode string (default length) |
| `ustring(length)` | `nvarchar(length)` | -              | Unicode string with specified length |
| `ustring(max)`    | `nvarchar(max)`    | -              | Unicode string with maximum length |
| `uchar`           | `nchar(1)`         | 1              | Unicode character (default length) |
| `uchar(length)`   | `nchar(length)`    | -              | Unicode character with specified length |

##### ASCII String Types
| DMD Type          | SQL Server Type    | Default Length | Description |
|-------------------|--------------------|----------------|-------------|
| `astring`         | `varchar(255)`     | 255            | ASCII string (default length) |
| `astring(length)` | `varchar(length)`  | -              | ASCII string with specified length |
| `astring(max)`    | `varchar(max)`     | -              | ASCII string with maximum length |
| `achar`           | `char(1)`          | 1              | ASCII character (default length) |
| `achar(length)`   | `char(length)`     | -              | ASCII character with specified length |

### Deprecated Types (Backward Compatibility)

> **⚠️ Deprecated**: These types are still supported but should be migrated to the new type names.

#### Deprecated String Types
| DMD Type          | SQL Server Type    | Migration To      | Status |
|-------------------|--------------------|-------------------|--------|
| `string`          | `nvarchar(255)`    | `ustring`         | ⚠️ Deprecated |
| `string(length)`  | `nvarchar(length)` | `ustring(length)` | ⚠️ Deprecated |
| `string(max)`     | `nvarchar(max)`    | `ustring(max)`    | ⚠️ Deprecated |
| `char`            | `nchar(1)`         | `uchar`           | ⚠️ Deprecated |
| `char(length)`    | `nchar(length)`    | `uchar(length)`   | ⚠️ Deprecated |

### Type Modifiers

#### Precision and Scale
```dmd
decimal(10,2) Price      // 10 digits total, 2 after decimal
ustring(100) Username    // 100 character Unicode string
ustring(max) Description // Maximum length Unicode string
uchar(1) Status          // Single Unicode character
achar(10) Code           // 10 ASCII characters
```

**Deprecated syntax (still supported)**:
```dmd
string(100) Username     // Use nvarchar(100) instead
string(max) Description  // Use nvarchar(max) instead
achar(10) Code           // Use char(10) instead
```

#### Nullable Fields
```dmd
ustring Username         // NOT NULL
ustring? Email           // NULL allowed
datetime? LastLoginDate  // NULL allowed
```

**Deprecated syntax (still supported)**:
```dmd
string Username          // Use nvarchar instead
string? Email            // Use nvarchar? instead
```

## Primary Keys

### Automatic Primary Key Generation

Every model automatically gets a primary key field:

```dmd
model User {
  ustring Username
  ustring Email
}
```

**Generated SQL:**
```sql
CREATE TABLE [User] (
  [UserID] int IDENTITY(1,1) NOT NULL,
  [Username] nvarchar(255) NOT NULL,
  [Email] nvarchar(255) NOT NULL,
  CONSTRAINT [PK_User] PRIMARY KEY ([UserID])
)
```

### Custom Primary Key Types

#### GUID Primary Key
```dmd
model User guid {
  ustring Username
  ustring Email
}
```

**Generated SQL:**
```sql
CREATE TABLE [User] (
  [UserID] uniqueidentifier NOT NULL,
  [Username] nvarchar(255) NOT NULL,
  [Email] nvarchar(255) NOT NULL,
  CONSTRAINT [PK_User] PRIMARY KEY ([UserID])
)
```

#### Explicit Integer Primary Key
```dmd
model User int {
  ustring Username
  ustring Email
}
```

**Generated SQL:**
```sql
CREATE TABLE [User] (
  [UserID] int IDENTITY(1,1) NOT NULL,
  [Username] nvarchar(255) NOT NULL,
  [Email] nvarchar(255) NOT NULL,
  CONSTRAINT [PK_User] PRIMARY KEY ([UserID])
)
```

### @NoIdentity Attribute

Disable the IDENTITY property on primary keys:

```dmd
model User {
  ustring Username
  ustring Email
  @NoIdentity
}
```

**Generated SQL:**
```sql
CREATE TABLE [User] (
  [UserID] int NOT NULL,
  [Username] nvarchar(255) NOT NULL,
  [Email] nvarchar(255) NOT NULL,
  CONSTRAINT [PK_User] PRIMARY KEY ([UserID])
)
```

## Foreign Keys and Relationships

### Basic Relationships

#### One-to-One Relationships
```dmd
model Order {
  model Customer
  decimal(10,2) Amount
  datetime OrderDate
}
```

#### One-to-Many Relationships
```dmd
model Customer {
  string(100) Name
  string(255) Email
  models Order        // One-to-many relationship
}
```

**Generated SQL for Order table:**
```sql
CREATE TABLE [Order] (
  [OrderID] int IDENTITY(1,1) NOT NULL,
  [CustomerID] int NOT NULL,
  [Amount] decimal(10,2) NOT NULL,
  [OrderDate] datetime NOT NULL,
  CONSTRAINT [PK_Order] PRIMARY KEY ([OrderID]),
  CONSTRAINT [FK_Order_CustomerID] FOREIGN KEY ([CustomerID]) REFERENCES [Customer]([CustomerID])
)
```

**Note:** The `models` syntax in the Customer table is for documentation purposes and doesn't generate additional SQL - the foreign key is created in the Order table.

### Nullable Relationships

```dmd
model Task {
  model User? AssignedUser
  nvarchar Title
  bit IsCompleted
}
```

**Generated SQL:**
```sql
CREATE TABLE [Task] (
  [TaskID] int IDENTITY(1,1) NOT NULL,
  [AssignedUserID] int NULL,
  [Title] nvarchar(255) NOT NULL,
  [IsCompleted] bit NOT NULL,
  CONSTRAINT [PK_Task] PRIMARY KEY ([TaskID]),
  CONSTRAINT [FK_Task_AssignedUserID] FOREIGN KEY ([AssignedUserID]) REFERENCES [User]([UserID])
)
```

### Optional Relationships

```dmd
model Order {
  !model Customer? OptionalCustomer
  decimal(10,2) Amount
}
```

**Generated SQL:**
```sql
CREATE TABLE [Order] (
  [OrderID] int IDENTITY(1,1) NOT NULL,
  [OptionalCustomerID] int NULL,
  [Amount] decimal(10,2) NOT NULL,
  CONSTRAINT [PK_Order] PRIMARY KEY ([OrderID]),
  CONSTRAINT [FK_Order_OptionalCustomerID] FOREIGN KEY ([OptionalCustomerID]) REFERENCES [Customer]([CustomerID])
)
```

### Relationship Aliases

```dmd
model Task {
  model User as AssignedUser
  model User as CreatedBy
  string Title
}
```

**Generated SQL:**
```sql
CREATE TABLE [Task] (
  [TaskID] int IDENTITY(1,1) NOT NULL,
  [AssignedUserID] int NOT NULL,
  [CreatedByID] int NOT NULL,
  [Title] nvarchar(255) NOT NULL,
  CONSTRAINT [PK_Task] PRIMARY KEY ([TaskID]),
  CONSTRAINT [FK_Task_AssignedUserID] FOREIGN KEY ([AssignedUserID]) REFERENCES [User]([UserID]),
  CONSTRAINT [FK_Task_CreatedByID] FOREIGN KEY ([CreatedByID]) REFERENCES [User]([UserID])
)
```

## Mixins (Reusable Field Sets)

### Mixin Definition (.dmdx files)

Create reusable field sets in `.dmdx` files:

```dmdx
mixin BaseEntity {
  datetime CreatedAt
  datetime UpdatedAt
  string(50) CreatedBy
  string(50) UpdatedBy
  bool IsDeleted
}
```

### Applying Mixins

```dmd
model User with BaseEntity {
  string(100) Username
  string(256) Email
  bool IsActive
}
```

**Generated SQL:**
```sql
CREATE TABLE [User] (
  [UserID] int IDENTITY(1,1) NOT NULL,
  [Username] nvarchar(100) NOT NULL,
  [Email] nvarchar(256) NOT NULL,
  [IsActive] bit NOT NULL,
  [CreatedAt] datetime NOT NULL,
  [UpdatedAt] datetime NOT NULL,
  [CreatedBy] nvarchar(50) NOT NULL,
  [UpdatedBy] nvarchar(50) NOT NULL,
  [IsDeleted] bit NOT NULL,
  CONSTRAINT [PK_User] PRIMARY KEY ([UserID])
)
```

### Mixins with Foreign Keys

```dmdx
mixin Auditable {
  !model User? as CreatedBy
  !model User? as LastModifiedBy
  datetime CreatedDateTime
  datetime LastModifiedDateTime
  int LockNumber
}
```

```dmd
model Document with Auditable {
  string(200) Title
  string(max) Content
}
```

**Note:** The `!` prefix indicates optional relationships that may not be required in all contexts.

## Indexes

### Simple Index

```dmd
model User {
  string(100) Username
  string(256) Email
  index (Email)
}
```

**Generated SQL:**
```sql
CREATE INDEX [IX_User_Email] ON [dbo].[User]([Email])
```

### Multi-Column Index

```dmd
model Product {
  string(100) Name
  string(50) Category
  bool IsActive
  index (Name, Category)
}
```

**Generated SQL:**
```sql
CREATE INDEX [IX_Product_Name_Category] ON [dbo].[Product]([Name], [Category])
```

### Unique Index

```dmd
model User {
  string(100) Username
  string(256) Email
  index (Email) @unique
}
```

**Generated SQL:**
```sql
CREATE UNIQUE INDEX [IX_User_Email] ON [dbo].[User]([Email])
```

### Multiple Indexes

```dmd
model Product {
  string(100) Name
  string(50) SKU
  string(50) Category
  bool IsActive
  index (SKU) @unique
  index (Name, IsActive)
  index (Category)
}
```

### Index Field Resolution

When defining indexes in DMD files, you can use **model names** (the names of related tables) in index definitions, and Shift will automatically resolve them to the actual foreign key column names when generating SQL.

#### Using Model Names in Indexes

```dmd
model Client {
  int ClientID
  string(100) Email
  int ClientStatusID
  int ClientTypeID
  
  // Foreign key relationships
  model ClientStatus
  model ClientType
  
  // Index using model names (automatically resolved to column names)
  index (Email, ClientStatus)
  index (ClientType, Email) @unique
}
```

**Generated SQL:**
```sql
CREATE INDEX [IX_Client_Email_ClientStatusID] ON [dbo].[Client]([Email], [ClientStatusID])
CREATE UNIQUE INDEX [IX_Client_ClientTypeID_Email] ON [dbo].[Client]([ClientTypeID], [Email])
```

#### How Field Resolution Works

1. **Model Name Detection**: When parsing index definitions, Shift identifies field names that match the `TargetTable` names of foreign key relationships
2. **Column Name Resolution**: These model names are automatically resolved to their corresponding foreign key column names
3. **Planning and SQL Generation**: Resolution is applied during migration planning (to avoid emitting redundant AddIndex steps) and again during SQL generation

#### Examples of Field Resolution

| DMD Index Definition          | Resolved SQL Column         | Foreign Key Relationship |
|-------------------------------|-----------------------------|-------------------------|
| `index (Email, ClientStatus)` | `[Email], [ClientStatusID]` | `ClientStatusID` → `ClientStatus` |
| `index (User, OrderDate)`     | `[UserID], [OrderDate]`     | `UserID` → `User` |
| `index (Product, Category)`   | `[ProductID], [Category]`   | `ProductID` → `Product` |

#### Benefits of Model Name Resolution

- **Readable DMD Files**: Index definitions use meaningful model names instead of technical column names
- **Maintainable**: Changes to foreign key column names don't require updating index definitions
- **Consistent**: Follows the same naming patterns used throughout DMD files
- **Automatic**: No manual mapping required - Shift handles the resolution automatically

#### Complex Foreign Key Scenarios

```dmd
model OrderItem {
  int OrderItemID
  int OrderID
  int ProductID
  int CreatedByUserID
  int AssignedUserID
  
  // Multiple foreign keys to the same table
  model Order
  model Product  
  model User  // Maps to CreatedByUserID (first occurrence)
  
  // Indexes using model names
  index (Order, Product)
  index (User, OrderItemID)  // Resolves to CreatedByUserID
}
```

**Generated SQL:**
```sql
CREATE INDEX [IX_OrderItem_OrderID_ProductID] ON [dbo].[OrderItem]([OrderID], [ProductID])
CREATE INDEX [IX_OrderItem_CreatedByUserID_OrderItemID] ON [dbo].[OrderItem]([CreatedByUserID], [OrderItemID])
```

> **Note**: When multiple foreign keys reference the same target table, the last foreign key in the collection is used for resolution. In the example above, `User` resolves to `CreatedByUserID` because it appears last in the foreign keys list.

#### Fallback Behavior

If a field name in an index definition doesn't match any foreign key target table names, it's used as-is in the generated SQL:

```dmd
model User {
  string(100) Username
  string(256) Email
  int DepartmentID
  
  model Department
  
  // Mixed: model name + regular field name
  index (Username, Department, Email)
}
```

**Generated SQL:**
```sql
CREATE INDEX [IX_User_Username_DepartmentID_Email] ON [dbo].[User]([Username], [DepartmentID], [Email])
```

This feature makes DMD files more intuitive and maintainable while ensuring that the generated SQL uses the correct column names for optimal database performance.

## Attributes

### @NoIdentity

Disable IDENTITY property on primary key:

```dmd
model User {
  string Username
  string Email
  @NoIdentity
}
```

### @unique

Mark index as unique:

```dmd
model User {
  string Email
  index (Email) @unique
}
```

## Complete Examples

### E-Commerce System

**Customer.dmd:**
```dmd
model Customer {
  string(100) Name
  string(255) Email
  string(20)? Phone
  string(500)? Address
  bool IsActive
  index (Email) @unique
}
```

**Order.dmd:**
```dmd
model Order {
  model Customer
  decimal(10,2) Amount
  datetime OrderDate
  string(50) Status
  string(1000)? Notes
  index (OrderDate)
  index (Customer, Status)
}
```

**OrderItem.dmd:**
```dmd
model OrderItem {
  model Order
  model Product
  int Quantity
  decimal(10,2) UnitPrice
  decimal(10,2) TotalPrice
  index (Order)
}
```

**Product.dmd:**
```dmd
model Product {
  string(100) Name
  string(50) SKU
  string(500)? Description
  decimal(10,2) Price
  bool IsActive
  index (SKU) @unique
  index (Name, IsActive)
}
```

### Task Management System

**User.dmd:**
```dmd
model User guid {
  string(100) Username
  string(256) Email
  string(255) PasswordHash
  bool IsActive
  datetime? LastLoginDate
  index (Email) @unique
  index (Username)
}
```

**Task.dmd:**
```dmd
model Task with Auditable {
  string(200) Title
  string(1000)? Description
  bool IsCompleted
  datetime? DueDate
  model User as AssignedUser
  model User as CreatedBy
  index (AssignedUser, IsCompleted)
  index (DueDate)
}
```

**Note:** The `with Auditable` syntax applies the Auditable mixin to the Task model.

**Auditable.dmdx:**
```dmdx
mixin Auditable {
  !model User? as CreatedBy
  !model User? as LastModifiedBy
  datetime CreatedDateTime
  datetime LastModifiedDateTime
  int LockNumber
}
```

## Best Practices

### File Organization

1. **One model per file** - Keep each model in its own `.dmd` file
2. **Descriptive file names** - Use the model name as the filename (e.g., `User.dmd`)
3. **Organize mixins separately** - Keep mixins in `.dmdx` files
4. **Group related models** - Use folders to organize related models

### Naming Conventions

1. **Model names** - Use PascalCase (e.g., `User`, `OrderItem`)
2. **Field names** - Use PascalCase (e.g., `FirstName`, `EmailAddress`)
3. **Mixin names** - Use PascalCase (e.g., `BaseEntity`, `Auditable`)
4. **File names** - Match model names exactly

### Type Selection

1. **Primary keys** - Use `guid` for distributed systems, `int` for single-server applications
2. **Strings** - Use `string` for Unicode text, `astring` for ASCII-only data
3. **Decimals** - Always specify precision and scale for monetary values
4. **Nullable fields** - Be explicit about nullability with `?` modifier

### Relationship Design

1. **Foreign key naming** - Use descriptive aliases for multiple relationships to the same table
2. **Nullable relationships** - Use `?` for optional relationships
3. **Optional relationships** - Use `!` prefix for relationships that might not be required

### Index Strategy

1. **Unique constraints** - Use `@unique` for business keys and natural identifiers
2. **Query optimization** - Create indexes for frequently queried column combinations
3. **Foreign key indexes** - Indexes are automatically created on foreign key columns for join performance (no manual index definition needed)

## Advanced Features

### Complex Foreign Key Patterns

```dmd
model Order {
  model Customer as BillingCustomer
  model Customer as ShippingCustomer
  model User as SalesRep
  decimal(10,2) Amount
}
```

### Mixin Composition

```dmdx
mixin Timestamps {
  datetime CreatedAt
  datetime UpdatedAt
}

mixin SoftDelete {
  bit IsDeleted
  datetime? DeletedAt
}

mixin FullAudit with Timestamps {
  nvarchar(50) CreatedBy
  nvarchar(50) UpdatedBy
  bit IsDeleted
}
```

### Composite Indexes

```dmd
model Product {
  string(100) Name
  string(50) Category
  string(20) Brand
  bool IsActive
  index (Category, Brand, IsActive)
  index (Name, Category)
}
```

## SQL Generation

### Table Creation Logic

1. **Primary key generation** - Automatic `{TableName}ID` field with specified type
2. **Foreign key constraints** - Automatic FK constraint creation
3. **Automatic FK indexes** - Non-clustered indexes are automatically created on all foreign key columns
4. **Index creation** - Separate index creation statements for explicitly defined indexes
5. **Column ordering** - Primary key first, then foreign keys, then other fields

### Constraint Naming

- **Primary keys**: `PK_{TableName}`
- **Foreign keys**: `FK_{TableName}_{ColumnName}`
- **Indexes**: `IX_{TableName}_{Field1}_{Field2}`

### Default Values

- **Non-nullable strings**: No default values (application must provide)
- **Nullable fields**: `NULL` allowed
- **Identity fields**: `IDENTITY(1,1)` for integer primary keys

## Integration with Shift

### Loading Models

```csharp
// Load from file paths
var model = await shift.LoadFromPathAsync("Models/");

// Load from assemblies
var model = await shift.LoadFromAssembly(typeof(User).Assembly);

// Load from assemblies with namespace filtering
var model = await shift.LoadFromAssembly(
    typeof(User).Assembly,
    new[] { "MyNamespace.Models", "MyNamespace.Mixins" });

// Load from SQL Server
var model = await shift.LoadFromSqlAsync(connectionString);
```

### Migration Generation

```csharp
// Compare DMD model with existing database
var plan = migrationPlanner.GeneratePlan(targetModel, actualModel);

// Apply migrations
var runner = new SqlMigrationPlanRunner(connectionString, plan);
var failures = runner.Run();
```

### Entity Framework Code Generation

```csharp
// Generate EF code from DMD models
var generator = new EfCodeGenerator();
await generator.GenerateAsync(model, outputPath);
```

## Type Conversion Matrix

### Complete SQL ↔ DMD Type Mapping

#### Current Types (Recommended)
| SQL Server Type    | DMD Type        | Notes |
|--------------------|-----------------|-------|
| `bit`              | `bool`          | Direct mapping |
| `uniqueidentifier` | `guid`          | Direct mapping |
| `int`              | `int`           | Direct mapping |
| `bigint`           | `long`          | Direct mapping |
| `decimal(p,s)`     | `decimal(p,s)`  | Direct mapping with precision/scale |
| `numeric(p,s)`     | `decimal(p,s)`  | **Converted** - numeric becomes decimal |
| `float`            | `float`         | Direct mapping |
| `money`            | `decimal(19,4)` | **Converted** - money becomes decimal with fixed precision |
| `smallmoney`       | `decimal(10,4)` | **Converted** - smallmoney becomes decimal with fixed precision |
| `datetime`         | `datetime`      | Direct mapping |
| `char(n)`          | `achar(n)`      | ASCII character type |
| `varchar(n)`       | `astring(n)`    | ASCII string type |
| `varchar(max)`     | `astring(max)`  | ASCII string with max length |
| `text`             | `astring(max)`  | **Converted** - deprecated text becomes astring(max) |
| `nchar(n)`         | `uchar(n)`      | Unicode character type |
| `nvarchar(n)`      | `ustring(n)`    | Unicode string type |
| `nvarchar(max)`    | `ustring(max)`  | Unicode string with max length |
| `ntext`            | `ustring(max)`  | **Converted** - deprecated ntext becomes ustring(max) |

#### Deprecated Types (Backward Compatibility)
| SQL Server Type    | DMD Type        | Migration To | Status |
|--------------------|-----------------|--------------|--------|
| `nvarchar(n)`      | `string(n)`     | `ustring(n)` | ⚠️ Deprecated |
| `nvarchar(max)`    | `string(max)`   | `ustring(max)` | ⚠️ Deprecated |

### Roundtrip Conversion Behavior

When converting SQL → DMD → SQL, the following types will change:

1. **Deprecated SQL Types**: `text` and `ntext` are converted to their modern equivalents (`astring(max)` and `ustring(max)`)
2. **Money Types**: `money` and `smallmoney` are normalized to `decimal` with appropriate precision
3. **Numeric Type**: `numeric` is converted to `decimal` (they are functionally equivalent)
4. **Type Name Alignment**: DMD types use descriptive names that clearly indicate their SQL mapping

This behavior is **intentional** and follows SQL Server best practices for modern development.

### Migration from Deprecated Types

If you have existing DMD files using deprecated type names, you can migrate them gradually:

1. **Immediate**: Deprecated types continue to work without changes
2. **Recommended**: Update to new type names for better clarity and future compatibility
3. **Automatic**: The system will generate appropriate SQL regardless of which type names you use

**Example Migration**:
```dmd
// Old (deprecated but still works)
model User {
  string Username
  astring Email
  bool IsActive
}

// New (recommended)
model User {
  ustring Username
  astring Email
  bool IsActive
}
```

## Future Enhancements

For planned data types and language features, see the [Feature Development Backlog](../development/backlog-features.md).

## Troubleshooting

### Common Syntax Errors

1. **Missing braces** - Ensure all models have opening and closing braces
2. **Invalid type names** - Use only supported DMD types
3. **Missing field names** - Every field must have a name
4. **Invalid precision** - Use positive integers for precision values

### Type Mismatch Issues

1. **Unsupported types** - Check that all types are supported by the parser
2. **Precision conflicts** - Ensure precision values are reasonable for the type
3. **Scale validation** - Scale must be less than or equal to precision

### Foreign Key Validation

1. **Circular dependencies** - Avoid circular references between models
2. **Missing target tables** - Ensure referenced models exist
3. **Type compatibility** - Foreign key types must match target primary key types

### Performance Considerations

1. **Index strategy** - Create indexes for query patterns
2. **String lengths** - Use appropriate lengths to avoid wasted space
3. **Nullable fields** - Minimize nullable fields where possible
4. **Foreign key indexes** - Consider indexes on foreign key columns
