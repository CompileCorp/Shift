# Assembly Loading Implementation for DMX/DMDX Files

This document describes the implementation of assembly loading support for DMX (Domain Model Definition) and DMDX (Domain Model Definition Extension/Mixin) files in the Shift system.

## Overview

The implementation adds the ability to load DMD and DMDX files from embedded resources in .NET assemblies, allowing for modular and distributable domain definitions.

## Key Features

### 1. Assembly Loading Support in Shift.cs

- **New Method**: `LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies)`
- **Enhanced Method**: `LoadFromAssembly(Assembly assembly)` - now properly implemented
- **Priority-based Loading**: First assembly wins when duplicate mixins/tables are found
- **Proper Mixin Application**: Mixins are loaded first and available when parsing tables

### 2. CLI Extension (apply-assemblies command)

- **New Command**: `apply-assemblies <connection_string> <dll1> [dll2] ...`
- **Multiple DLLs**: Supports loading from multiple assemblies in order
- **Error Handling**: Proper error reporting for missing or invalid assemblies
- **Assembly Loading**: Uses `Assembly.LoadFrom()` for dynamic loading

### 3. Comprehensive Unit Tests

- **Embedded Resources**: Test assembly includes embedded DMD/DMDX files
- **Mixin Application**: Verifies mixins are properly applied to tables
- **Order Priority**: Tests that first assembly wins for duplicates
- **Empty Assembly Handling**: Tests behavior with assemblies containing no resources
- **Resource Enumeration**: Verifies embedded resources are detected correctly

## Technical Implementation

### File Structure
```
src/
├── Shift/
│   └── Shift.cs                    # Core assembly loading logic
├── Shift.Cli/
│   └── Program.cs                  # Extended CLI with apply-assemblies command
├── test/Shift.Tests/
│   ├── AssemblyLoadingTests.cs     # Comprehensive test suite
│   └── TestResources/              # Embedded test resources
│       ├── Auditable.dmdx          # Test mixin
│       ├── User.dmd                # Test model
│       └── Task.dmd                # Test model with mixin usage
└── TestLibrary/                    # Example library for testing
    ├── TestLibrary.csproj          # Project with embedded resources
    ├── Class1.cs                   # Dummy class
    └── Resources/                  # Embedded DMD/DMDX files
        ├── BaseEntity.dmdx         # Example mixin
        ├── Customer.dmd            # Example model
        └── Order.dmd               # Example model with references
```

### Key Methods

#### Shift.cs
```csharp
public async Task<DatabaseModel> LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies)
```
- Loads DMD/DMDX files from multiple assemblies in order
- Handles duplicate prevention (first assembly wins)
- Ensures mixins are loaded before models for proper application
- Provides detailed logging for debugging

#### Program.cs (CLI)
```csharp
private static async Task CommandApplyAssembliesAsync(string[] args, ILoggerFactory loggerFactory)
```
- Validates command arguments
- Loads assemblies using full path resolution
- Handles assembly loading errors gracefully
- Integrates with existing Shift workflow

### Assembly Resource Loading Process

1. **Assembly Enumeration**: Get all manifest resource names from each assembly
2. **Mixin Loading**: Load all `.dmdx` files first to ensure availability
3. **Model Loading**: Load all `.dmd` files, applying mixins as needed
4. **Duplicate Handling**: Skip resources already loaded from previous assemblies
5. **Error Handling**: Log errors without stopping the entire process

### Test Coverage

The implementation includes comprehensive tests covering:

- **Basic Loading**: Verify assemblies can be loaded and parsed
- **Mixin Application**: Ensure mixins are properly applied to models
- **Priority Handling**: Confirm first assembly wins for duplicates  
- **Empty Assemblies**: Handle assemblies without DMD/DMDX resources
- **Resource Detection**: Verify embedded resources are found correctly
- **Multiple Assemblies**: Test loading from multiple assemblies in sequence

## Usage Examples

### CLI Usage
```bash
# Load from single assembly
dotnet run --project src/Shift.Cli -- apply-assemblies "Server=localhost;Database=test" MyLibrary.dll

# Load from multiple assemblies (priority order)
dotnet run --project src/Shift.Cli -- apply-assemblies "Server=localhost;Database=test" CoreModels.dll ExtensionModels.dll UserModels.dll
```

### Programmatic Usage
```csharp
var shift = new Shift { Logger = logger };
var assembly = Assembly.LoadFrom("MyLibrary.dll");
var model = await shift.LoadFromAssembly(assembly);

// Or multiple assemblies
var assemblies = new[] { assembly1, assembly2, assembly3 };
var model = await shift.LoadFromAssembliesAsync(assemblies);
```

### Embedding Resources in Projects
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <EmbeddedResource Include="Models\*.dmd" />
    <EmbeddedResource Include="Mixins\*.dmdx" />
  </ItemGroup>
</Project>
```

## Benefits

1. **Modularity**: Domain definitions can be packaged in separate assemblies
2. **Distribution**: DMD/DMDX files travel with business logic assemblies
3. **Version Control**: Models are versioned with the assemblies that use them
4. **Reusability**: Common mixins can be shared across multiple assemblies
5. **Deployment**: Simplified deployment with self-contained assemblies

## Testing

All functionality is thoroughly tested with:
- 7 comprehensive unit tests
- Embedded test resources in the test assembly
- Example test library demonstrating usage
- CLI integration testing

Run tests with:
```bash
dotnet test src/test/Shift.Tests/Shift.Tests.csproj
```

## Future Enhancements

Potential improvements could include:
- Support for assembly version conflict resolution
- Caching of loaded assemblies for performance
- Support for plugin-style hot-loading
- Integration with dependency injection containers
- Support for assembly signing and security verification