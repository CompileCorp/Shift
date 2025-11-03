# Assembly Loading Implementation

## Overview

Shift supports loading DMD (Domain Model Definition) and DMDX (Domain Model Definition Extension/Mixin) files from embedded resources in .NET assemblies. This allows for modular and distributable domain definitions, enabling teams to package their domain models as reusable libraries.

## Key Features

### Assembly Loading Support

- **Multiple Assembly Support**: Load from multiple assemblies in priority order
- **Embedded Resources**: Access DMD/DMDX files embedded as assembly resources
- **Priority-based Loading**: First assembly wins when duplicate mixins/tables are found
- **Proper Mixin Application**: Mixins are loaded first and available when parsing tables
- **Namespace Filtering**: Optionally filter resources by namespace to load only specific subsets of models

### CLI Integration

- **apply-assemblies Command**: Load models from assembly resources
- **Multiple DLL Support**: Process multiple assemblies in sequence
- **Error Handling**: Comprehensive error reporting for missing or invalid assemblies
- **Dynamic Loading**: Uses `Assembly.LoadFrom()` for runtime assembly loading

## Implementation Details

### Core Methods

#### LoadFromAssembly

```csharp
public async Task<DatabaseModel> LoadFromAssembly(
    Assembly assembly, 
    IEnumerable<string>? namespaces = null)
```

Loads models from a single assembly. The optional `namespaces` parameter filters which resources are loaded based on their namespace.

**Parameters:**
- `assembly`: The assembly to load models from
- `namespaces`: Optional list of namespaces to filter resources. If provided, only resources whose manifest resource name starts with one of the specified namespaces (followed by a dot) or matches exactly will be loaded. If `null` or empty, all matching resources are loaded.

**Example:**
```csharp
// Load all models from assembly
var model = await shift.LoadFromAssembly(assembly);

// Load only models from specific namespaces
var model = await shift.LoadFromAssembly(
    assembly, 
    new[] { "MyNamespace.Models", "MyNamespace.Mixins" });
```

#### LoadFromAssembliesAsync

```csharp
public async Task<DatabaseModel> LoadFromAssembliesAsync(
    IEnumerable<Assembly> assemblies, 
    IEnumerable<string>? namespaces = null)
```

Loads models from multiple assemblies with priority-based merging. The optional `namespaces` parameter filters resources across all assemblies.

**Parameters:**
- `assemblies`: The assemblies to load models from, processed in order
- `namespaces`: Optional list of namespaces to filter resources (same behavior as `LoadFromAssembly`)

**Example:**
```csharp
// Load all models from multiple assemblies
var model = await shift.LoadFromAssembliesAsync(assemblies);

// Load only models from specific namespaces across all assemblies
var model = await shift.LoadFromAssembliesAsync(
    assemblies,
    new[] { "Core.Models", "Domain.Models" });
```

### CLI Command Implementation

#### apply-assemblies Command

The CLI command supports loading from multiple assemblies with optional namespace filtering.

**Syntax:**
```bash
shift apply-assemblies <connection_string> <dll1> [dll2] ... [filter1] [filter2] ...
```

**Parsing Logic:**
- Arguments ending with `.dll` (case-insensitive) are treated as assembly paths
- All other arguments are treated as namespace filters
- DLLs and filters can be specified in any order
- All filters apply to all assemblies
- At least one DLL must be provided

**Example Implementation Flow:**
1. Parse connection string and remaining arguments
2. Separate arguments into DLLs (ending with `.dll`) and filters (everything else)
3. Load assemblies from DLL paths
4. Load models with namespace filtering if filters are provided
5. Apply migration plan to database

```csharp
// Simplified parsing logic
var dllPaths = args.Where(arg => arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
var namespaces = args.Where(arg => !arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();

var assemblies = dllPaths.Select(Assembly.LoadFrom).ToList();
var model = await shift.LoadFromAssembliesAsync(assemblies, namespaces.Count > 0 ? namespaces : null);
await shift.ApplyToSqlAsync(model, connectionString);
```

## Resource Embedding

### Project Configuration

To embed DMD/DMDX files as resources in your assembly:

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

### Resource Naming

- **DMD Files**: `Models\*.dmd` → Embedded as `Namespace.Models.Filename.dmd`
- **DMDX Files**: `Mixins\*.dmdx` → Embedded as `Namespace.Mixins.Filename.dmdx`

### Example Structure

```
MyDomainModels/
├── MyDomainModels.csproj
├── Models/
│   ├── User.dmd
│   ├── Order.dmd
│   └── Product.dmd
└── Mixins/
    ├── Auditable.dmdx
    └── SoftDelete.dmdx
```

## Usage Examples

### Basic Assembly Loading

```csharp
var assembly = Assembly.GetExecutingAssembly();
var shift = new Shift { Logger = logger };
var model = await shift.LoadFromAssembly(assembly);
```

### Namespace Filtering

Filter which models are loaded based on their namespace. This is useful when an assembly contains models from multiple namespaces and you only want to load a subset.

```csharp
var assembly = Assembly.GetExecutingAssembly();
var shift = new Shift { Logger = logger };

// Load only models from specific namespaces
// Resources like "MyApp.Models.User.dmd" and "MyApp.Mixins.Auditable.dmdx" will be included
// Resources like "MyApp.Legacy.OldModel.dmd" will be excluded
var model = await shift.LoadFromAssembly(
    assembly,
    new[] { "MyApp.Models", "MyApp.Mixins" });
```

**Namespace Matching Rules:**
- A resource matches if its manifest resource name starts with the namespace followed by a dot (e.g., `MyNamespace.File.dmd`)
- A resource also matches if it exactly equals the namespace (e.g., `MyNamespace.dmd`)
- Matching is case-sensitive and uses ordinal comparison

### Multiple Assembly Loading

```csharp
var assemblies = new[]
{
    Assembly.LoadFrom("./CoreModels.dll"),
    Assembly.LoadFrom("./DomainModels.dll"),
    Assembly.LoadFrom("./Extensions.dll")
};

var shift = new Shift { Logger = logger };

// Load all models
var model = await shift.LoadFromAssembliesAsync(assemblies);

// Or filter by namespace across all assemblies
var filteredModel = await shift.LoadFromAssembliesAsync(
    assemblies,
    new[] { "Core.Models", "Domain.Entities" });
```

### CLI Usage

```bash
# Load from single assembly
shift apply-assemblies "Server=localhost;Database=MyDb;" ./MyModels.dll

# Load from multiple assemblies
shift apply-assemblies "Server=localhost;Database=MyDb;" ./CoreModels.dll ./DomainModels.dll ./Extensions.dll

# Load with namespace filtering
shift apply-assemblies "Server=localhost;Database=MyDb;" ./MyModels.dll MyApp.Models MyApp.Mixins

# Multiple assemblies with filters (all filters apply to all assemblies)
shift apply-assemblies "Server=localhost;Database=MyDb;" ./CoreModels.dll ./DomainModels.dll Core.Models Domain.Models

# Mixed order - DLLs and filters can be interleaved
shift apply-assemblies "Server=localhost;Database=MyDb;" ./Lib1.dll Namespace1 ./Lib2.dll Namespace2
```

## Priority and Conflict Resolution

### Loading Order

1. **Mixins First**: All mixins are loaded before any tables
2. **Assembly Order**: Assemblies are processed in the order provided
3. **First Wins**: First assembly to define a mixin or table wins

### Conflict Resolution

```csharp
// Assembly 1 defines: User mixin
// Assembly 2 defines: User mixin (different content)
// Result: Assembly 1's User mixin is used

// Assembly 1 defines: Order table
// Assembly 2 defines: Order table (different content)  
// Result: Assembly 1's Order table is used
```

### Best Practices

1. **Clear Ownership**: Each assembly should own specific domain concepts
2. **Avoid Conflicts**: Don't define the same mixins/tables in multiple assemblies
3. **Logical Ordering**: Load core assemblies before domain-specific ones
4. **Documentation**: Document which assemblies contain which models

## Error Handling

### Common Issues

1. **Missing Assemblies**: File not found or invalid DLL
2. **Invalid Resources**: Corrupted or malformed DMD/DMDX files
3. **Assembly Loading**: .NET assembly loading failures
4. **Resource Access**: Permission issues accessing embedded resources

### Error Recovery

```csharp
try
{
    var assembly = Assembly.LoadFrom(dllPath);
    var model = await shift.LoadFromAssembly(assembly);
}
catch (FileNotFoundException)
{
    logger.LogError("Assembly file not found: {DllPath}", dllPath);
}
catch (BadImageFormatException)
{
    logger.LogError("Invalid assembly format: {DllPath}", dllPath);
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to load assembly: {DllPath}", dllPath);
}
```

## Testing

### Unit Tests

```csharp
[Fact]
public async Task LoadFromAssembly_ShouldLoadEmbeddedResources()
{
    // Arrange
    var assembly = Assembly.GetExecutingAssembly();
    var shift = new Shift { Logger = _logger };

    // Act
    var model = await shift.LoadFromAssembly(assembly);

    // Assert
    Assert.NotNull(model);
    Assert.True(model.Mixins.ContainsKey("Auditable"));
    Assert.True(model.Tables.ContainsKey("User"));
    Assert.True(model.Tables.ContainsKey("Task"));
}
```

### Integration Tests

```csharp
[Fact]
public async Task LoadFromAssembliesAsync_ShouldRespectOrderAndPriority()
{
    // Arrange
    var assembly1 = Assembly.GetExecutingAssembly();
    var assembly2 = Assembly.GetExecutingAssembly();
    var assemblies = new[] { assembly1, assembly2 };
    var shift = new Shift { Logger = _logger };

    // Act
    var model = await shift.LoadFromAssembliesAsync(assemblies);

    // Assert
    Assert.NotNull(model);
    // Verify priority handling
}
```

## Performance Considerations

### Resource Loading

- **Stream Management**: Resources are loaded using `using` statements for proper disposal
- **Memory Usage**: Large models may consume significant memory
- **Assembly Caching**: Consider caching loaded assemblies for repeated use

### Optimization Tips

1. **Selective Loading**: Only load necessary assemblies
2. **Resource Filtering**: Filter resources by extension before processing
3. **Namespace Filtering**: Use namespace filtering to load only the models you need, reducing memory usage and processing time
4. **Async Operations**: Use async/await for I/O operations
5. **Error Boundaries**: Handle errors gracefully without stopping entire process

## Future Enhancements

For planned features and advanced scenarios, see the [Feature Development Backlog](../development/backlog-features.md).
