# Shift CLI Reference

`shift` is a .NET 9 console tool for applying DMD schema definitions to SQL Server,
exporting an existing database back to DMD, and generating Entity Framework Core code from
either source. It is the primary command-line entry point to the Shift framework.

## Installation

The CLI is **not currently published as a .NET global tool** — run it from source, or build
and invoke the produced executable:

```bash
# Run from source (note the -- separating dotnet args from CLI args)
dotnet run --project src/Shift.Cli -- apply "Server=.;Database=MyDb;" ./Models

# Or build/publish once and run the executable directly
dotnet publish src/Shift.Cli -c Release -o ./shift-cli
./shift-cli/Shift.Cli apply "Server=.;Database=MyDb;" ./Models
```

In the examples below, `shift` stands for however you invoke the built CLI
(`dotnet run --project src/Shift.Cli --` or the published executable).

## Commands

| Command | Purpose |
|---------|---------|
| `apply` | Apply DMD/DMDX files to a database |
| `apply-assemblies` | Apply DMD resources embedded in .NET assemblies |
| `export` | Reverse-engineer a database schema into DMD files |
| `ef sql` | Generate EF Core code from a database |
| `ef files` | Generate EF Core code from DMD files |
| `ef sql-custom` | Generate EF Core code from a database with custom naming |
| `dbml` | Export DMD/DMDX files to a DBML diagram for dbdiagram.io |
| `attributes` | List the plugin attributes each plugin understands |

Running `shift` with no arguments, or with an unrecognised command, prints help. The `ef`
command also responds to the aliases `ef-generate` and `generate-ef`.

`ef`, `dbml` and `attributes` are the plugin-facing commands. Every plugin implements the same
contract (`IShiftPlugin`), which is what lets `attributes` enumerate them.

### apply

```bash
shift apply <connection-string> <path> [path...] [--schema <name>]
```

- **`<path>`** — a DMD file or a directory of DMD files; repeatable.
- **`--schema <name>`** — target schema (default `dbo`); may appear anywhere in the arguments.

```bash
shift apply "Server=.;Database=MyDb;" ./Models
shift apply "Server=.;Database=MyDb;" ./Models/Core ./Models/Auth
shift apply "Server=.;Database=MyDb;" ./Models --schema Sales
```

The tool loads the DMD files, loads the live schema, diffs them, and applies the resulting
migration plan. Unsafe operations (e.g. changes that would truncate data) are skipped — see
[Migration Planner](../architecture/migration-planner.md).

### apply-assemblies

```bash
shift apply-assemblies <connection-string> <dll> [dll|filter...] [--schema <name>]
```

Loads DMD/DMDX files embedded as assembly resources. Arguments are classified by extension:
anything ending in `.dll` is an assembly path, anything else is a namespace filter. DLLs and
filters may be interleaved in any order, and every filter applies to every assembly. At least
one `.dll` is required.

A resource matches a filter when its manifest resource name starts with the filter followed
by a dot, or equals the filter exactly.

```bash
# All embedded resources
shift apply-assemblies "Server=.;Database=MyDb;" ./MyApp.Models.dll

# Only the MyApp.Models and MyApp.Mixins namespaces, across two assemblies
shift apply-assemblies "Server=.;Database=MyDb;" ./Core.dll ./Auth.dll MyApp.Models MyApp.Mixins
```

### export

```bash
shift export <connection-string> <schema> <output-path>
```

All three arguments are required (help is printed if any are missing). Reads tables, columns,
foreign keys, and indexes for the given schema and writes one DMD file per table to the output
directory.

```bash
shift export "Server=.;Database=MyDb;" dbo ./ExportedModels
```

### ef sql / ef files / ef sql-custom

```bash
shift ef sql <connection-string> <output-path> [schema]      # schema defaults to dbo
shift ef files <path> [path...] <output-path>                # last argument is the output path
shift ef sql-custom <connection-string> <output-path> [options]
```

`ef sql-custom` options (supplied as `--key value` pairs; unknown options are warned and ignored):

| Option | Effect | Default |
|--------|--------|---------|
| `--namespace <name>` | Namespace for generated classes | `Generated` |
| `--context <name>` | DbContext class name | `GeneratedDbContext` |
| `--interface <name>` | DbContext interface name | `IGeneratedDbContext` |
| `--base-class <name>` | Base class for the DbContext | (none) |
| `--schema <name>` | Schema to read | `dbo` |

```bash
shift ef sql "Server=.;Database=MyDb;" ./Generated
shift ef files ./Models/User.dmd ./Models/Order.dmd ./Generated
shift ef sql-custom "Server=.;Database=MyDb;" ./Generated \
  --namespace MyApp.Data --context MyAppDbContext --interface IMyAppDbContext
```

**Generated output.** All files are written flat into the output directory (created if
missing), each with a `.g.cs` suffix:

- `<Table>Entity.g.cs` — the entity class (e.g. `UserEntity`).
- `<Table>EntityMap.g.cs` — its `IEntityTypeConfiguration<TEntity>` fluent configuration.
- `<Context>.g.cs` and `<Interface>.g.cs` — the DbContext and its interface (defaults
  `GeneratedDbContext.g.cs` / `IGeneratedDbContext.g.cs`).

### dbml

```bash
shift dbml <path> [path...] <output-path>
```

- **`<path>`** — a directory of DMD/DMDX files; repeatable. At least one is required.
- **`<output-path>`** — the last argument. Ending in `.dbml` (case-insensitive) names the file;
  anything else is treated as a directory and receives `model.dbml`.

```bash
shift dbml ./Models ./Diagrams                 # writes ./Diagrams/model.dbml
shift dbml ./Models ./Mixins ./schema.dbml     # writes ./schema.dbml
```

Paste the result into [dbdiagram.io](https://dbdiagram.io). The diagram is shaped by the `erd:*`
plugin attributes — see [architecture/shift-dbml-exporter.md](../architecture/shift-dbml-exporter.md).

### attributes

```bash
shift attributes [plugin]
```

Lists every plugin attribute the installed plugins understand, so you can discover attribute names
without reading plugin source. Takes no required arguments; pass a plugin name to list just that one.

```bash
shift attributes            # every plugin
shift attributes dbml       # just the DBML exporter
```

Attributes are grouped by namespace. Each line shows the full attribute name as you would write it,
its scope (`model`, `field` or `both`), whether it is a flag or takes a value, and what the plugin
does with it:

```text
dbml - Exports the model as a DBML diagram for dbdiagram.io
  namespace: erd
    @erd:color scope=model kind=valued - Sets the table header colour, as rgb or rrggbb hex digits
    @erd:group scope=model kind=valued - Puts the table in the named TableGroup; ignored on a field because DBML has no column groups
    @erd:hide scope=both kind=flag - Omits the table (with its relationships and group membership) or the column from the diagram
    @erd:note scope=both kind=valued - Adds the text as a DBML note on the table or column
ef - Generates Entity Framework entities, maps and a DbContext
  (no plugin attributes)
```

A plugin claims one namespace and is handed only that namespace's attributes. The `ef` generator
consumes none, so it claims no namespace and lists nothing.

See the [plugin attributes section of the DMD reference](../dsl/dmd-file-format.md#plugin-attributes)
for the syntax and validation rules.

## Connection strings

```text
# Windows authentication
Server=.;Database=MyDb;Integrated Security=true;
Server=.\SQLEXPRESS;Database=MyDb;Integrated Security=true;

# SQL authentication
Server=.;Database=MyDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True;

# LocalDB
Server=(localdb)\mssqllocaldb;Database=MyDb;

# Azure SQL Database
Server=tcp:server.database.windows.net,1433;Database=MyDb;User Id=user@server;Password=pass;Encrypt=True;
```

`TrustServerCertificate=True` is convenient for local development but should not be used
against production servers.

## Common workflows

```bash
# Database-first: apply DMD, then generate EF code from the resulting database
shift apply "Server=.;Database=MyDb;" ./Models
shift ef sql "Server=.;Database=MyDb;" ./Generated

# Code-first: generate EF code straight from DMD, then apply to the database
shift ef files ./Models ./Generated
shift apply "Server=.;Database=MyDb;" ./Models

# Embedded: ship models as assembly resources and apply them on deploy
shift apply-assemblies "Server=.;Database=MyDb;" ./MyApp.Models.dll
```

In CI, run the CLI from the checked-out source, passing the connection string from a secret:

```yaml
- run: dotnet run --project src/Shift.Cli -- apply "${{ secrets.CONNECTION_STRING }}" ./Models
```

## Output and exit codes

- The tool prints `Domain Migration Definition (DMD) System` on startup, followed by
  per-action progress. Logs are single-line and prefixed with an `HH:mm:ss` timestamp at
  `Information` level and above (e.g. `14:23:15 CreateTable User`).
- **Exit `0`** — success.
- **Exit `1`** — an unhandled error (connection failure, missing file, assembly load error,
  etc.); the message is written to the console as `Error: <message>`.
- **Invalid arguments do not fail the process** — the CLI prints a usage/help message and
  exits `0`.
