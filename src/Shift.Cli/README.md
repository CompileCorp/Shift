# Compile.Shift.Cli

The command-line tool for [Compile.Shift](https://www.nuget.org/packages/Compile.Shift): apply DMD
schema definitions to SQL Server, export an existing database back to DMD, and generate Entity
Framework Core and DBML output from either source.

## Installation

```bash
dotnet tool install --global Compile.Shift.Cli
```

The tool is invoked as `shift`.

## Documentation

For every command and its arguments, see
[the Shift CLI reference](https://github.com/CompileCorp/shift/blob/main/docs/cli/shift-cli-reference.md).

## Quick Start

```bash
# Apply a directory of DMD files to a database
shift apply "Server=.;Database=MyDb;" ./Models

# Reverse-engineer a database into DMD files
shift export "Server=.;Database=MyDb;" ./Models

# Render DMD files as a DBML diagram for dbdiagram.io
shift dbml ./Models ./out/model.dbml

# List the plugin attributes the installed plugins understand
shift attributes
```

## License

This project is licensed under the MIT License.
