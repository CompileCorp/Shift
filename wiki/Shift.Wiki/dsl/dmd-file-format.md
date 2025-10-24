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

### Simple Types

| DMD Type   | SQL Server Type    | Description |
|------------|--------------------|-------------|
| `bool`     | `bit`              | Boolean value (true/false) |
| `int`      | `int`              | 32-bit integer |
| `long`     | `bigint`           | 64-bit integer |
| `decimal`  | `decimal`          | Fixed-point decimal number |
| `guid`     | `uniqueidentifier` | Globally unique identifier |
| `datetime` | `datetime2`        | Date and time |
| `date`     | `date`             | Date only |
| `time`     | `time`             | Time only |

### String Types

| DMD Type          | SQL Server Type    | Description |
|-------------------|--------------------|-------------|
| `string`          | `nvarchar(255)`    | Unicode string (default length) |
| `string(length)`  | `nvarchar(length)` | Unicode string with specified length |
| `string(max)`     | `nvarchar(max)`    | Unicode string with maximum length |
| `astring`         | `varchar(255)`     | ASCII string (default length) |
| `astring(length)` | `varchar(length)`  | ASCII string with specified length |
| `astring(max)`    | `varchar(max)`     | ASCII string with maximum length |

### Type Modifiers

#### Precision and Scale
```dmd
decimal(10,2) Price       // 10 digits total, 2 after decimal
string(100) Username      // 100 character string
string(max) Description   // Maximum length string
```

#### Nullable Fields
```dmd
string Username          // NOT NULL
string? Email            // NULL allowed
datetime? LastLoginDate  // NULL allowed
```

## Primary Keys

### Automatic Primary Key Generation

Every model automatically gets a primary key field:

```dmd
model User {
  string Username
  string Email
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
  string Username
  string Email
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
  string Username
  string Email
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
  string Username
  string Email
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

```dmd
model Order {
  model Customer
  decimal(10,2) Amount
  datetime OrderDate
}
```

**Generated SQL:**
```sql
CREATE TABLE [Order] (
  [OrderID] int IDENTITY(1,1) NOT NULL,
  [CustomerID] int NOT NULL,
  [Amount] decimal(10,2) NOT NULL,
  [OrderDate] datetime2 NOT NULL,
  CONSTRAINT [PK_Order] PRIMARY KEY ([OrderID]),
  CONSTRAINT [FK_Order_CustomerID] FOREIGN KEY ([CustomerID]) REFERENCES [Customer]([CustomerID])
)
```

### Nullable Relationships

```dmd
model Task {
  model User? AssignedUser
  string Title
  bool IsCompleted
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
  [CreatedAt] datetime2 NOT NULL,
  [UpdatedAt] datetime2 NOT NULL,
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
3. **Foreign key indexes** - Consider indexes on foreign key columns for join performance

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
  bool IsDeleted
  datetime? DeletedAt
}

mixin FullAudit with Timestamps {
  string(50) CreatedBy
  string(50) UpdatedBy
  bool IsDeleted
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
3. **Index creation** - Separate index creation statements
4. **Column ordering** - Primary key first, then foreign keys, then other fields

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
var model = await shift.LoadFromAssemblyAsync(typeof(User).Assembly);

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
