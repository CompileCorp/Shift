# Assembly Loading Implementation

## Overview

Shift supports loading DMD (Domain Model Definition) and DMDX (Domain Model Definition Extension/Mixin) files from embedded resources in .NET assemblies. This allows for modular and distributable domain definitions, enabling teams to package their domain models as reusable libraries.

## Key Features

### Assembly Loading Support

- **Multiple Assembly Support**: Load from multiple assemblies in priority order
- **Embedded Resources**: Access DMD/DMDX files embedded as assembly resources
- **Priority-based Loading**: First assembly wins when duplicate mixins/tables are found
- **Proper Mixin Application**: Mixins are loaded first and available when parsing tables

### CLI Integration

- **apply-assemblies Command**: Load models from assembly resources
- **Multiple DLL Support**: Process multiple assemblies in sequence
- **Error Handling**: Comprehensive error reporting for missing or invalid assemblies
- **Dynamic Loading**: Uses `Assembly.LoadFrom()` for runtime assembly loading

## Implementation Details

### Core Methods

#### LoadFromAssembly

```csharp
public async Task<DatabaseModel> LoadFromAssembly(Assembly assembly)
{
    var resourceNames = assembly.GetManifestResourceNames()
        .Where(name => name.EndsWith(".dmd") || name.EndsWith(".dmdx"))
        .ToList();

    var mixins = new Dictionary<string, MixinModel>();
    var tables = new Dictionary<string, TableModel>();

    // Load mixins first
    foreach (var resourceName in resourceNames.Where(name => name.EndsWith(".dmdx")))
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        var mixin = _parser.ParseMixin(content);
        mixins[mixin.Name] = mixin;
    }

    // Load tables with mixin application
    foreach (var resourceName in resourceNames.Where(name => name.EndsWith(".dmd")))
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        var table = _parser.ParseTable(content, mixins);
        tables[table.Name] = table;
    }

    return new DatabaseModel { Mixins = mixins, Tables = tables };
}
```

#### LoadFromAssembliesAsync

```csharp
public async Task<DatabaseModel> LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies)
{
    var combinedModel = new DatabaseModel();
    
    foreach (var assembly in assemblies)
    {
        var assemblyModel = await LoadFromAssembly(assembly);
        
        // Merge with priority: first assembly wins
        foreach (var mixin in assemblyModel.Mixins)
        {
            if (!combinedModel.Mixins.ContainsKey(mixin.Key))
            {
                combinedModel.Mixins[mixin.Key] = mixin.Value;
            }
        }
        
        foreach (var table in assemblyModel.Tables)
        {
            if (!combinedModel.Tables.ContainsKey(table.Key))
            {
                combinedModel.Tables[table.Key] = table.Value;
            }
        }
    }
    
    return combinedModel;
}
```

### CLI Command Implementation

#### apply-assemblies Command

```csharp
private static async Task CommandApplyAssembliesAsync(string[] args, ILoggerFactory loggerFactory)
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: shift apply-assemblies <connection_string> <dll1> [dll2] ...");
        return;
    }

    var connectionString = args[0];
    var dllPaths = args[1..];
    
    var logger = loggerFactory.CreateLogger("ApplyAssemblies");
    var shift = new Shift { Logger = logger };
    
    try
    {
        // Load assemblies
        var assemblies = new List<Assembly>();
        foreach (var dllPath in dllPaths)
        {
            var assembly = Assembly.LoadFrom(dllPath);
            assemblies.Add(assembly);
        }
        
        // Load models from assemblies
        var model = await shift.LoadFromAssembliesAsync(assemblies);
        
        // Apply to database
        var runner = new SqlMigrationPlanRunner(connectionString, model);
        await runner.RunAsync();
        
        logger.LogInformation("Successfully applied models from {AssemblyCount} assemblies", assemblies.Count);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply models from assemblies");
        throw;
    }
}
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

### Multiple Assembly Loading

```csharp
var assemblies = new[]
{
    Assembly.LoadFrom("./CoreModels.dll"),
    Assembly.LoadFrom("./DomainModels.dll"),
    Assembly.LoadFrom("./Extensions.dll")
};

var shift = new Shift { Logger = logger };
var model = await shift.LoadFromAssembliesAsync(assemblies);
```

### CLI Usage

```bash
# Load from single assembly
shift apply-assemblies "Server=localhost;Database=MyDb;" ./MyModels.dll

# Load from multiple assemblies
shift apply-assemblies "Server=localhost;Database=MyDb;" ./CoreModels.dll ./DomainModels.dll ./Extensions.dll
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
3. **Async Operations**: Use async/await for I/O operations
4. **Error Boundaries**: Handle errors gracefully without stopping entire process

## Future Enhancements

For planned features and advanced scenarios, see the [Feature Development Backlog](../development/backlog-features.md).
