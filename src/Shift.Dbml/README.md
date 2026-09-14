# Compile.Shift.Dbml

A DBML exporter for [Compile.Shift](https://www.nuget.org/packages/Compile.Shift): renders a DMD
model as a DBML document to paste into [dbdiagram.io](https://dbdiagram.io).

## Installation

```bash
dotnet add package Compile.Shift.Dbml
```

To export from the command line instead, install the CLI
([Compile.Shift.Cli](https://www.nuget.org/packages/Compile.Shift.Cli)) and run `shift dbml`.

## Documentation

For the exporter's behaviour and the `erd` attributes it understands, see
[the Shift.Dbml documentation](https://github.com/CompileCorp/shift/blob/main/docs/architecture/shift-dbml-exporter.md).

## Quick Start

```csharp
using Compile.Shift.Dbml;

var exporter = new DbmlExporter(logger);
var dbml = exporter.Export(model);
```

Tables, columns with their primary keys and unique indexes, `indexes { ... }` blocks and a `Ref:`
for each foreign key are emitted. The exporter claims the `erd` attribute namespace and understands
`@erd:hide`, `@erd:group`, `@erd:note` and `@erd:color`.

## License

This project is licensed under the MIT License.
