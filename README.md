# Shift - Database Model Parser and Migration Tool

A comprehensive tool for parsing domain model files, generating database migration plans, and creating Entity Framework code generators. Shift provides a complete solution for database schema management and code generation.

## Installation

```bash
dotnet tool install -g Shift.Cli
```

## Quick Start

### Database Migration
```bash
# Apply migrations to database
shift apply "Server=localhost;Database=example;Trusted_Connection=true;TrustServerCertificate=true;" ./models/

# Export current database schema
shift export "Server=localhost;Database=example;Trusted_Connection=true;" ./exported/
```

### Entity Framework Code Generation
```bash
# Generate EF code from SQL Server
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated

# Generate EF code from model files
shift ef files ./Models/User.yaml ./Models/Order.yaml ./Generated

# Generate with custom options
shift ef sql-custom "Server=localhost;Database=MyDb;" ./Generated \
  --namespace MyApp.Data \
  --context MyAppDbContext \
  --interface IMyAppDbContext
```

### Assembly Loading
```bash
# Load models from embedded resources in assemblies
shift apply-assemblies "Server=localhost;Database=MyDb;" ./MyModels.dll ./OtherModels.dll
```

## Features

### Core Functionality
- **Database Migration**: Generate and apply SQL migration scripts
- **Model Parsing**: Parse DMD/DMDX domain model files
- **Schema Export**: Export existing database schemas to model files
- **Assembly Loading**: Load models from embedded resources in .NET assemblies

### Entity Framework Integration
- **Code Generation**: Generate Entity Framework entities, maps, and DbContext
- **Type Mapping**: Comprehensive SQL Server to C# type mapping
- **Customization**: Custom namespaces, class names, and base classes
- **Interface Generation**: Generate DbContext interfaces for better testability

### Advanced Features
- **Mixin Support**: Reusable field sets for common patterns
- **Model Extensions**: Extend existing models with additional fields
- **Smart Index Resolution**: Automatic resolution of model names to foreign key columns
- **Relationship Mapping**: Support for one-to-one and one-to-many relationships

## Commands

### Database Management
- `shift apply <connection_string> <model_path>` - Apply migrations to database
- `shift export <connection_string> <output_path>` - Export database schema to model files

### Entity Framework Code Generation
- `shift ef sql <connection_string> <output_path>` - Generate EF code from SQL Server database
- `shift ef files <model_files> <output_path>` - Generate EF code from model files
- `shift ef sql-custom <connection_string> <output_path> [options]` - Generate EF code with custom options

### Assembly Loading
- `shift apply-assemblies <connection_string> <dll_files>` - Load models from assembly resources

### Help
- `shift --help` - Show all available commands
- `shift ef --help` - Show EF-specific commands

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
- **Primitive types**: `ustring(n)`, `astring(n)`, `uchar(n)`, `achar(n)`, `int`, `long`, `bool`, `guid`, `datetime`, `decimal(p,s)`, `float`
- **Nullable types**: Append `?` to make a field nullable (e.g., `ustring(100)?`)
- **String types**:
  - `ustring(n)` - nvarchar(n) in SQL Server (Unicode)
  - `astring(n)` - varchar(n) in SQL Server (ASCII)
  - `uchar(n)` - nchar(n) in SQL Server (Unicode, fixed-length)
  - `achar(n)` - char(n) in SQL Server (ASCII, fixed-length)
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
  ustring(20)? BusinessPhone
  ustring(512) Fullname
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
  ustring(50) CreatedBy
  datetime CreatedDateTime
  ustring(50) LastModifiedBy
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
  ustring(255) Title
  datetime? DueDate
}

// Extension (fisheries/models/Task.dmd)
extends Task {
  model Application?
}
```


### Type Mapping

| DSL Type | SQL Server Type | Notes |
|----------|-----------------|-------|
| `ustring(n)` | `nvarchar(n)` | Unicode string with length |
| `astring(n)` | `varchar(n)` | ASCII string with length |
| `uchar(n)` | `nchar(n)` | Fixed-length Unicode string |
| `achar(n)` | `char(n)` | Fixed-length ASCII string |
| `int` | `int` | 32-bit integer |
| `long` | `bigint` | 64-bit integer |
| `bool` | `bit` | Boolean value |
| `guid` | `uniqueidentifier` | Globally unique identifier |
| `datetime` | `datetime` | Date and time |
| `decimal(p,s)` | `decimal(p,s)` | Decimal with precision and scale |
| `float` | `float` | Floating point number |
| `ustring(max)` | `nvarchar(max)` | Unicode string with max length |
| `astring(max)` | `varchar(max)` | ASCII string with max length |

## License

MIT License - see [LICENCE.md](LICENCE.md) for details.

## Support

For questions, issues, or contributions, please visit the project repository.