# Shift.Ef

Shift.Ef is a code generator that creates Entity Framework components from Shift DatabaseModel instances. It generates Entity classes, Entity Framework configuration maps, and DbContext classes with appropriate `.g.cs` file naming to indicate they are code-generated files.

## Features

- **Entity Generation**: Creates strongly-typed entity classes with appropriate data annotations
- **Entity Map Generation**: Creates Entity Framework configuration classes using the Fluent API
- **DbContext Generation**: Creates a complete DbContext with DbSet properties and entity configurations
- **Type Mapping**: Automatic mapping from database types to appropriate C# types
- **Code Generation Headers**: All generated files include headers indicating they are auto-generated
- **Integration**: Seamless integration with existing Shift data loading methods

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

### Basic Usage

```csharp
using Compile.Shift;
using Compile.Shift.Ef;
using Microsoft.Extensions.Logging;

// Create Shift instance with logger
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Shift>();
var shift = new Shift { Logger = logger };

// Load model from SQL Server and generate EF code
await shift.GenerateEfCodeFromSqlAsync(
    connectionString: "Server=localhost;Database=MyDb;Trusted_Connection=true;",
    outputPath: "./Generated",
    namespaceName: "MyApp.Data.Generated"
);
```

### Advanced Usage

```csharp
// Load from model files
await shift.GenerateEfCodeFromPathAsync(
    paths: new[] { "./Models" },
    outputPath: "./Generated",
    namespaceName: "MyApp.Data.Generated"
);

// Or use an existing DatabaseModel
var model = await shift.LoadFromSqlAsync(connectionString);
await shift.GenerateEfCodeAsync(model, "./Generated", "MyApp.Data.Generated");
```

### Using Generated Code

After generation, you can use the generated classes in your application:

```csharp
using Microsoft.EntityFrameworkCore;
using MyApp.Data.Generated;

// Configure in Startup.cs or Program.cs
services.AddDbContext<GeneratedDbContext>(options =>
    options.UseSqlServer(connectionString));

// Use in your application
public class MyService
{
    private readonly GeneratedDbContext _context;
    
    public MyService(GeneratedDbContext context)
    {
        _context = context;
    }
    
    public async Task<ClientEntity> GetClientAsync(int id)
    {
        return await _context.Client.FindAsync(id);
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