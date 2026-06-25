# Shift.Ef CLI Commands

## Overview

Shift.Cli includes comprehensive Entity Framework code generation commands that integrate seamlessly with the existing Shift command structure. These commands leverage the Shift.Ef library to generate production-ready Entity Framework code from various sources.

## Command Structure

### Main Command

```bash
shift ef <subcommand> [options]
```

The `ef` command can also be invoked using the aliases `ef-generate` or `generate-ef`.

### Available Subcommands

- `sql` - Generate from SQL Server database
- `files` - Generate from DMD/DMDX model files
- `sql-custom` - Generate from SQL Server with custom options

## Commands Reference

### `shift ef sql`

Generates Entity Framework code directly from a SQL Server database.

#### Syntax

```bash
shift ef sql <connection_string> <output_path> [schema]
```

#### Parameters

- `connection_string` - SQL Server connection string
- `output_path` - Directory where generated files will be created
- `schema` - Database schema to read (optional, positional, defaults to `dbo`)

> **Note:** `ef sql` uses default code-generation settings. To customise the namespace, context, interface, or base class, use [`ef sql-custom`](#shift-ef-sql-custom).

#### Examples

```bash
# Basic generation (uses the dbo schema)
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated

# Generate from a specific schema
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated MySchema
```

### `shift ef files`

Generates Entity Framework code from DMD/DMDX model files.

#### Syntax

```bash
shift ef files <path1> [path2] ... <output_path>
```

#### Parameters

- `path1, path2, ...` - Paths to DMD/DMDX files (files or directories)
- `output_path` - Directory where generated files will be created (the last argument is always treated as the output path)

> **Note:** `ef files` uses default code-generation settings and does not accept namespace/context/interface/base-class options. For custom output settings, generate from SQL Server with [`ef sql-custom`](#shift-ef-sql-custom).

#### Examples

```bash
# Generate from multiple model files
shift ef files ./Models/User.dmd ./Models/Order.dmd ./Generated

# Generate from directories of DMD files
shift ef files ./Models/Core ./Models/Auth ./Generated
```

### `shift ef sql-custom`

Advanced SQL Server generation with full customization options.

#### Syntax

```bash
shift ef sql-custom <connection_string> <output_path> [options]
```

#### Parameters

- `connection_string` - SQL Server connection string
- `output_path` - Directory where generated files will be created

#### Options

- `--namespace <name>` - Custom namespace for generated classes
- `--context <name>` - Custom DbContext class name
- `--interface <name>` - Custom DbContext interface name
- `--base-class <name>` - Custom base class to inherit from
- `--schema <name>` - Database schema to read (defaults to `dbo`)

#### Examples

```bash
# Full customization
shift ef sql-custom "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated \
  --namespace MyApp.Data \
  --context MyAppDbContext \
  --interface IMyAppDbContext \
  --base-class MyCustomBaseDbContext

# Generate from a specific schema
shift ef sql-custom "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated \
  --namespace MyApp.Data \
  --schema MySchema
```

## Configuration Options

### Namespace Customization

```bash
--namespace MyApp.Data
```

Sets the namespace for all generated classes. Defaults to `Generated`.

### Context Class Name

```bash
--context MyAppDbContext
```

Sets the name of the generated DbContext class. Defaults to `GeneratedDbContext`.

### Interface Name

```bash
--interface IMyAppDbContext
```

Sets the name of the generated DbContext interface. Defaults to `IGeneratedDbContext`.

### Base Class

```bash
--base-class MyCustomBaseDbContext
```

Sets the base class that the generated DbContext will inherit from. When not specified it is left unset, and the generator uses its standard `DbContext` base class.

## Generated File Structure

All files are written **flat** into the output directory (created if missing), each with a
`.g.cs` suffix — there are no subfolders. The context and interface file names follow the
`--context` / `--interface` options (defaults `GeneratedDbContext` / `IGeneratedDbContext`):

```
Generated/                        # the <output_path> argument
├── ClientEntity.g.cs             # {TableName}Entity.g.cs        (one per table)
├── OrderEntity.g.cs
├── ClientEntityMap.g.cs          # {TableName}EntityMap.g.cs     (one per table)
├── OrderEntityMap.g.cs
├── GeneratedDbContext.g.cs       # {ContextClassName}.g.cs       (--context)
└── IGeneratedDbContext.g.cs      # {InterfaceName}.g.cs          (--interface)
```

## Behavior notes

- **Help.** Running `shift` with no arguments, `shift ef` with no sub-command, or an
  unrecognised (sub-)command prints help. There is no dedicated `--help` flag.
- **Unknown options.** `ef sql-custom` warns about and ignores any option it doesn't
  recognise; quote option values that contain spaces.
- **Output.** The output directory is created if it does not exist. Per-table progress is
  logged as generation proceeds — single-line, prefixed with an `HH:mm:ss` timestamp, at
  `Information` level.
- **Regeneration.** Re-running a command overwrites the `.g.cs` files in place. Keep
  hand-written code in separate, non-generated files — the generated classes are `partial`.
