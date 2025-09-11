# Compile.Shift

A .NET library for database migration planning and execution.

## Features

- Database model parsing and analysis
- Migration planning and execution
- Support for SQL Server
- Command-line interface for easy integration
- Customizable primary key generation

## Installation

```bash
dotnet add package Compile.Shift
```

## Usage

```csharp
using Compile.Shift;

// Your code here
```

## Primary Key Attributes

### @NoIdentity

The `@NoIdentity` attribute allows you to create primary keys without the IDENTITY property. This is useful when you want to manually control primary key values.

**Example:**
```
model User {
  string name
  string email
  @NoIdentity
}
```

**Generated SQL:**
```sql
CREATE TABLE [User] (
  [UserID] int NOT NULL,
  [name] nvarchar(255) NOT NULL,
  [email] nvarchar(255) NOT NULL,
  CONSTRAINT [PK_User] PRIMARY KEY ([UserID])
)
```

**Without @NoIdentity (default behavior):**
```sql
CREATE TABLE [User] (
  [UserID] int IDENTITY(1,1) NOT NULL,
  [name] nvarchar(255) NOT NULL,
  [email] nvarchar(255) NOT NULL,
  CONSTRAINT [PK_User] PRIMARY KEY ([UserID])
)
```

## License

This project is licensed under the MIT License.