# Shift.Ef

Shift.Ef is a code generator that creates Entity Framework components from Shift DatabaseModel instances. It generates Entity classes, Entity Framework configuration maps, and DbContext classes with appropriate `.g.cs` file naming to indicate they are code-generated files.

**Note**: Shift.Ef is now integrated into the Shift CLI. You can use it directly through the `shift ef` commands.

## Features

- **Entity Generation**: Creates strongly-typed entity classes with appropriate data annotations
- **Entity Map Generation**: Creates Entity Framework configuration classes using the Fluent API
- **DbContext Generation**: Creates a complete DbContext with DbSet properties and entity configurations
- **Interface Generation**: Creates interfaces for DbContext classes for better testability and dependency injection
- **Type Mapping**: Automatic mapping from database types to appropriate C# types
- **Customizable Options**: Support for custom context class names, interface names, and base classes
- **Code Generation Headers**: All generated files include headers indicating they are auto-generated
- **Integration**: Seamless integration with existing Shift data loading methods (SQL Server and file-based)

## Generated Files

The generator creates the following types of files:

1. **Entity Classes**: `{TableName}Entity.g.cs`
   - Properties for all table fields with appropriate data annotations
   - Navigation properties for foreign key relationships
   - Nullable type handling

2. **Entity Maps**: `{TableName}EntityMap.g.cs`
   - Entity Framework configuration using Fluent API
   - Column mappings, constraints, and relationships
   - Index configurations

3. **DbContext**: `GeneratedDbContext.g.cs`
   - DbSet properties for all entities
   - Entity configuration application
   - Optional connection configuration

## Usage

### CLI Usage (Recommended)

The easiest way to use Shift.Ef is through the Shift CLI:

```bash
# Generate EF code from SQL Server
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated

# Generate EF code from model files
shift ef files ./Models/User.yaml ./Models/Order.yaml ./Generated

# Generate with custom options
shift ef sql-custom "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated \
  --namespace MyApp.Data --context MyDbContext --interface IMyDbContext --base-class MyBaseDbContext
```

### Programmatic Usage

You can also use Shift.Ef programmatically:

```csharp
using Compile.Shift;
using Compile.Shift.Ef;
using Microsoft.Extensions.Logging;

// Create Shift instance with logger
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Shift>();
var shift = new Shift { Logger = logger };

// Load model from SQL Server and generate EF code with default settings
await shift.GenerateEfCodeFromSqlAsync(
    connectionString: "Server=localhost;Database=MyDb;Trusted_Connection=true;",
    outputPath: "./Generated",
    logger: logger,
    namespaceName: "MyApp.Data.Generated"
);
```

### Advanced Usage with Custom Options

```csharp
// Configure custom options for more control
var options = new EfCodeGenerationOptions
{
    NamespaceName = "MyApp.Data",
    ContextClassName = "MyAppDbContext",        // Custom context class name
    InterfaceName = "IMyAppDbContext",          // Custom interface name
    BaseClassName = "MyCustomBaseDbContext"     // Optional: inherit from custom base class
};

// Generate from SQL Server with custom options
await shift.GenerateEfCodeFromSqlAsync(
    connectionString: "Server=localhost;Database=MyDb;Trusted_Connection=true;",
    outputPath: "./Generated",
    logger: logger,
    options: options
);

// Generate from file-based models (YAML/JSON)
await shift.GenerateEfCodeFromPathAsync(
    paths: new[] { "./Models/User.yaml", "./Models/Order.yaml" },
    outputPath: "./Generated",
    logger: logger,
    options: options
);

// Or use an existing DatabaseModel
var model = await shift.LoadFromSqlAsync(connectionString);
await shift.GenerateEfCodeAsync(model, "./Generated", logger, options);
```

### Using Generated Code

After generation, you can use the generated classes in your application:

```csharp
using Microsoft.EntityFrameworkCore;
using MyApp.Data;

// Configure in Startup.cs or Program.cs with interface for better testability
services.AddDbContext<MyAppDbContext>(options =>
    options.UseSqlServer(connectionString));
services.AddScoped<IMyAppDbContext>(provider => provider.GetRequiredService<MyAppDbContext>());

// Use in your application with dependency injection
public class MyService
{
    private readonly IMyAppDbContext _context;
    
    public MyService(IMyAppDbContext context)
    {
        _context = context;
    }
    
    public async Task<ClientEntity> GetClientAsync(int id)
    {
        return await _context.Client.FindAsync(id);
    }
    
    public async Task<List<OrderEntity>> GetOrdersForClientAsync(int clientId)
    {
        return await _context.Order
            .Where(o => o.ClientId == clientId)
            .Include(o => o.Client)
            .ToListAsync();
    }
}
```

## Type Mappings

The generator includes comprehensive type mapping from SQL Server types to C# types:

| SQL Server Type | C# Type |
|----------------|---------|
| bit | bool |
| tinyint | byte |
| smallint | short |
| int | int |
| bigint | long |
| decimal/numeric | decimal |
| float | double |
| real | float |
| varchar/nvarchar | string |
| datetime/datetime2 | DateTime |
| uniqueidentifier | Guid |
| varbinary | byte[] |

Nullable columns are automatically mapped to nullable C# types where appropriate.

## Integration with Shift

Shift.Ef extends the existing Shift library functionality by adding Entity Framework code generation capabilities to the existing data loading methods:

- `LoadFromSqlAsync()` - Load from SQL Server database
- `LoadFromPathAsync()` - Load from model definition files
- `LoadFromAssembly()` - Load from embedded resources (when implemented)

## Code Generation Features

- **Automatic Headers**: All generated files include headers indicating they are auto-generated
- **Partial Classes**: Generated classes are marked as partial to allow for extensions
- **Proper Naming**: Files use `.g.cs` extension following .NET conventions
- **Type Safety**: Strong typing with appropriate nullability handling
- **Relationships**: Foreign key relationships are properly configured
- **Indexes**: Database indexes are mapped to Entity Framework index configurations

## Dependencies

- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.Extensions.Logging.Abstractions
- Shift (project reference)