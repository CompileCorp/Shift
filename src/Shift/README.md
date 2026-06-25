# Compile.Shift

A .NET library for database migration planning and execution.

## Installation

```bash
dotnet add package Compile.Shift
```

## Documentation

For complete documentation, usage examples, and API reference, see the [Shift documentation](https://github.com/CompileCorp/shift/blob/main/docs/README.md).

## Quick Start

```csharp
using Compile.Shift;

var shift = new Shift { Logger = logger };
var model = await shift.LoadFromSqlAsync(connectionString);
```

## License

This project is licensed under the MIT License.