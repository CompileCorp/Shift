# Shift.Ef

Entity Framework code generator for Shift DatabaseModel instances.

## Installation

```bash
dotnet add package Compile.Shift.Ef
```

## Documentation

For complete documentation, usage examples, and API reference, please visit the [Shift Wiki](https://github.com/CompileCorp/shift/wiki).

## Quick Start

### CLI Usage (Recommended)

```bash
# Generate EF code from SQL Server
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated

# Generate EF code from model files
shift ef files ./Models/User.yaml ./Models/Order.yaml ./Generated
```

### Programmatic Usage

```csharp
using Compile.Shift;
using Compile.Shift.Ef;

var shift = new Shift { Logger = logger };
await shift.GenerateEfCodeFromSqlAsync(connectionString, outputPath, logger, options);
```

## License

This project is licensed under the MIT License.