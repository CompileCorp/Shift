# Shift - Database Model Parser and Migration Tool

A tool for parsing domain model files and generating database migration plans.

## Usage

```bash
dotnet run -- apply "Server=localhost;Database=example;Trusted_Connection=true;TrustServerCertificate=true;" ..\..\data\
```

## File Structure Specifications

### Model Files (.dmd)

Model files define database tables and their relationships. They use a domain-specific language (DSL) with the following structure:

#### Basic Model Definition
```
model ModelName with MixinName {
  // fields and relationships
}
```

#### Field Types
- **Primitive types**: `string(n)`, `astring(n)`, `int`, `bool`, `datetime`, `decimal(p,s)`, `long`
- **Nullable types**: Append `?` to make a field nullable (e.g., `string(100)?`)
- **String types**:
  - `string(n)` - nvarchar(n) in SQL Server
  - `astring(n)` - varchar(n) in SQL Server
- **Decimal**: `decimal(precision,scale)` (e.g., `decimal(12,2)`)

#### Relationships
- **One-to-One**: `model RelatedModel? as AliasName`
- **One-to-Many**: `models RelatedModel? as AliasName`
- **Optional relationships**: Prefix with `!` (e.g., `!model RelatedModel?`)
- **Foreign key fields**: Automatically generated as `{AliasName}{ModelName}ID`

#### Indexes and Keys
- **Primary key**: Automatically generated as `{ModelName}ID` (int, identity)
- **Unique key**: `key (FieldName)`
- **Indexes**: `index (Field1, Field2, Field3)`
- **Unique indexes**: `index (Field1, Field2) @unique`

#### Example Model
```
model Client with Auditable {
  model Address? as Postal
  model Address? as Residential
  model ClientStatus
  model ClientType
  string(20)? BusinessPhone
  string(512) Fullname
  bool IsAssignedPassword
  index (Email, ClientStatus)
}
```

### Mixin Files (.dmdx)

Mixin files define reusable field sets that can be applied to multiple models. They use the `.dmdx` extension.

#### Mixin Structure
```
mixin MixinName {
  // fields and relationships (same syntax as models)
}
```

#### Mixin Usage
- Apply to a model: `model ModelName with MixinName`
- Mixins are automatically expanded into the target model
- All fields and relationships from the mixin are added to the model

#### Example Mixin
```
mixin Auditable {
  !model User? as CreatedBy
  !model User? as LastModifiedBy
  string(50) CreatedBy
  datetime CreatedDateTime
  string(50) LastModifiedBy
  datetime LastModifiedDateTime
  !model Transaction?
  int LockNumber
}
```

### Model Extension (.dmd)

Models can extend existing models to add additional fields and relationships.

#### Extension Structure
```
extends ExistingModelName {
  // additional fields and relationships
}
```

#### Extension Rules
- The base model must exist and be parsed first
- Extensions add fields to the existing model
- Cannot override existing fields
- Useful for domain-specific extensions of core models

#### Example Extension
```
// Base model (core/models/Task.dmd)
model Task with Auditable {
  model TaskStatus
  model TaskType
  string(255) Title
  datetime? DueDate
}

// Extension (fisheries/models/Task.dmd)
extends Task {
  model Application?
}
```

### Processing Order

1. **Mixins** are parsed first from all `mixins/` directories
2. **Core models** are parsed from the core domain
3. **Domain-specific models** are parsed from their respective directories
4. **Extensions** are applied to existing models

### Field Generation Rules

- **Primary keys**: Automatically generated as `{ModelName}ID` (int, identity, not null)
- **Foreign keys**: Generated as `{AliasName}{RelatedModelName}ID` (int, nullable based on relationship)
- **Mixin fields**: Expanded directly into the target model
- **Extension fields**: Added to the existing model structure

### Type Mapping

| DSL Type | SQL Server Type | Notes |
|----------|-----------------|-------|
| `string(n)` | `nvarchar(n)` | Unicode string with length |
| `astring(n)` | `varchar(n)` | ASCII string with length |
| `char(n)` | `char(n)` | Fixed-length ASCII string |
| `achar(n)` | `nchar(n)` | Fixed-length Unicode string |
| `int` | `int` | 32-bit integer |
| `bool` | `bit` | Boolean value |
| `datetime` | `datetime` | Date and time |
| `decimal(p,s)` | `decimal(p,s)` | Decimal with precision and scale |
| `money` | `money` | Currency (19,4 precision) |
| `smallmoney` | `smallmoney` | Small currency (10,4 precision) |
| `long` | `bigint` | 64-bit integer |
| `float` | `float` | Floating point number |
| `string(max)` | `nvarchar(max)` | Unicode string with max length |
| `ntext` | `ntext` | Unicode text (deprecated) |
| `text` | `text` | ASCII text (deprecated) |

### Reducing Column Sizes

You can intentionally shrink the width/length of columns by updating the field's length in the target model and annotating the field with `@reducesize`. By default, Shift blocks migrations that would truncate existing data; add `@allowdataloss` to permit truncation.

- **Opt-in shrink**: Shrinks are only planned when the target field is marked with `@reducesize`.
- **Default no-data-loss**: Without `@allowdataloss`, the migration will fail if any existing values exceed the new size.
- **Allow data loss**: With `@allowdataloss`, Shift will truncate existing values to the new size before altering the column.
- **Supported types**:
  - Strings: `string(n)` → `nvarchar(n)`, `astring(n)` → `varchar(n)`, `char(n)`, `achar(n)` → `nchar(n)`
  - Binary: `binary(n)`, `varbinary(n)` (including shrinking from `max` to a fixed length)

#### Syntax

```dmd
model Product {
  // Shrink an nvarchar from 50 to 20, block if truncation would occur
  string(20) Name @reducesize

  // Shrink an nvarchar from 50 to 20, truncate values > 20 then alter
  string(20) ShortName @reducesize @allowdataloss

  // Shrink varbinary(max) to varbinary(20), truncate to 20 bytes then alter
  varbinary(20) BinaryVar @reducesize @allowdataloss
}
```

#### Behavior

- When `@reducesize` is present and `@allowdataloss` is not:
  - Shift inserts a guard that throws if any row has a value longer than the new size (strings use `LEN`, binaries use `DATALENGTH`).
  - No changes are applied if the guard fails.
- When both `@reducesize` and `@allowdataloss` are present:
  - Shift issues an `UPDATE` to truncate oversized data (strings via `LEFT(value, newLen)`, binaries via `SUBSTRING(value, 1, newLen)`).
  - Then Shift alters the column to the new size.
