# Shift.Ef Code Generator

## Overview

Shift.Ef is a comprehensive Entity Framework code generator that leverages Shift's DatabaseModel infrastructure to generate production-ready Entity Framework components. It integrates seamlessly with Shift's existing data loading methods and produces high-quality, maintainable code.

## Architecture

### Project Structure

```
src/Shift.Ef/
├── Shift.Ef.csproj                    # Project file with EF dependencies
├── EfCodeGenerator.cs                 # Main orchestrator class
├── EntityGenerator.cs                 # Generates entity classes
├── EntityMapGenerator.cs              # Generates EF configuration classes
├── DbContextGenerator.cs              # Generates DbContext class
├── DbContextInterfaceGenerator.cs     # Generates DbContext interfaces
├── TypeMapper.cs                      # Maps SQL types to C# types
├── EfCodeGenerationOptions.cs         # Configuration options
├── ShiftEfExtensions.cs               # Extension methods for Shift integration
└── Examples/
    └── EfGeneratorExample.cs          # Usage examples
```

### Dependencies

- **Microsoft.EntityFrameworkCore**
- **Microsoft.EntityFrameworkCore.Design**
- **Microsoft.EntityFrameworkCore.SqlServer**
- **Microsoft.Extensions.Logging.Console**

## Key Features

### Core Code Generation

- **Entity Classes**: Generates strongly-typed entity classes with proper data annotations
- **Entity Maps**: Creates Entity Framework Fluent API configuration classes
- **DbContext**: Generates complete DbContext with DbSet properties and configurations
- **Type Mapping**: Comprehensive SQL Server to C# type mapping
- **Foreign Key Support**: Generates navigation properties for relationships
- **Index Support**: Maps database indexes to EF index configurations

### Code Generation Standards

- **Proper Naming**: All generated files use `.g.cs` extension
- **Auto-Generated Headers**: Clear indication that files are code-generated
- **Partial Classes**: Allows for user extensions
- **Nullable Types**: Proper handling of nullable/optional fields
- **Precision/Scale**: Decimal types with proper precision and scale mapping

### Integration with Shift

- **Extension Methods**: Seamless integration via extension methods
- **Existing Data Loading**: Uses Shift's `LoadFromSqlAsync()` and `LoadFromPathAsync()` methods
- **Logger Integration**: Respects Shift's logging infrastructure
- **DatabaseModel Support**: Works with existing TableModel, FieldModel, etc.

## Generated Code Examples

### Entity Class

```csharp
[Table("Client")]
public partial class ClientEntity
{
    [Column("ClientId")]
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ClientId { get; set; }

    [Column("Name")]
    public string Name { get; set; }
    
    [Column("Email")]
    public string? Email { get; set; }
    
    // Navigation properties
    public virtual ClientStatusEntity? ClientStatus { get; set; }
}
```

### Entity Map

```csharp
public partial class ClientEntityMap : IEntityTypeConfiguration<ClientEntity>
{
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.ToTable("Client");
        
        builder.Property(e => e.ClientId)
            .HasColumnName("ClientId")
            .HasColumnType("int")
            .IsRequired();
            
        builder.Property(e => e.Name)
            .HasColumnName("Name")
            .HasColumnType("nvarchar(100)")
            .IsRequired();
            
        builder.HasKey(e => e.ClientId);
        
        // Foreign key relationships
        builder.HasOne(e => e.ClientStatus)
            .WithMany()
            .HasForeignKey("ClientStatusId")
            .HasConstraintName("FK_Client_ClientStatus");
    }
}
```

### DbContext

```csharp
public partial class GeneratedDbContext : DbContext
{
    public virtual DbSet<ClientEntity> Client { get; set; }
    public virtual DbSet<OrderEntity> Order { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ClientEntityMap());
        modelBuilder.ApplyConfiguration(new OrderEntityMap());
    }
}
```

### DbContext Interface

```csharp
public partial interface IGeneratedDbContext
{
    DbSet<ClientEntity> Client { get; set; }
    DbSet<OrderEntity> Order { get; set; }
}
```

## Configuration Options

### EfCodeGenerationOptions

```csharp
public class EfCodeGenerationOptions
{
    public string? NamespaceName { get; set; }
    public string? ContextClassName { get; set; }
    public string? InterfaceName { get; set; }
    public string? BaseClassName { get; set; }
}
```

### Usage with Custom Options

```csharp
var options = new EfCodeGenerationOptions
{
    NamespaceName = "MyApp.Data",
    ContextClassName = "MyAppDbContext",
    InterfaceName = "IMyAppDbContext",
    BaseClassName = "MyCustomBaseDbContext"
};

await shift.GenerateEfCodeFromSqlAsync(
    connectionString: "Server=localhost;Database=MyDb;Trusted_Connection=true;",
    outputPath: "./Generated",
    logger: logger,
    options: options
);
```

## Type Mapping

### SQL Server to C# Type Mapping

| SQL Server Type | C# Type | Notes |
|----------------|---------|-------|
| `int` | `int` | 32-bit integer |
| `bigint` | `long` | 64-bit integer |
| `bit` | `bool` | Boolean value |
| `nvarchar(n)` | `string` | Unicode string |
| `varchar(n)` | `string` | ASCII string |
| `datetime` | `DateTime` | Date and time |
| `datetime2` | `DateTime` | High precision date and time |
| `decimal(p,s)` | `decimal` | Decimal with precision and scale |
| `money` | `decimal` | Currency (19,4 precision) |
| `float` | `double` | Floating point number |
| `uniqueidentifier` | `Guid` | Globally unique identifier |

### Nullable Type Handling

- Fields marked as nullable in the database model generate nullable C# properties
- Non-nullable fields generate non-nullable C# properties
- Navigation properties follow relationship cardinality rules

## Integration Patterns

### Extension Methods

```csharp
// Generate from SQL Server
await shift.GenerateEfCodeFromSqlAsync(connectionString, outputPath, logger, options);

// Generate from model files
await shift.GenerateEfCodeFromPathAsync(paths, outputPath, logger, options);

// Generate from DatabaseModel
await shift.GenerateEfCodeAsync(model, outputPath, logger, options);
```

### Dependency Injection

```csharp
// Register in DI container
services.AddScoped<IMyAppDbContext, MyAppDbContext>();

// Use in services
public class MyService
{
    private readonly IMyAppDbContext _context;
    
    public MyService(IMyAppDbContext context)
    {
        _context = context;
    }
}
```

## Best Practices

### Generated Code Usage

1. **Never edit generated files** - Use partial classes for extensions
2. **Use interfaces** - Inject interfaces rather than concrete DbContext
3. **Custom base classes** - Inherit from custom base classes for common functionality
4. **Namespace organization** - Use meaningful namespaces for generated code

### File Organization

```
Generated/
├── Entities/
│   ├── ClientEntity.g.cs
│   └── OrderEntity.g.cs
├── Maps/
│   ├── ClientEntityMap.g.cs
│   └── OrderEntityMap.g.cs
├── Contexts/
│   ├── MyAppDbContext.g.cs
│   └── IMyAppDbContext.g.cs
└── Extensions/
    ├── ClientEntity.cs          # Custom extensions
    └── MyAppDbContext.cs        # Custom extensions
```

## Future Enhancements

For planned enhancements and new features, see the [Feature Development Backlog](../development/backlog-features.md).

## Troubleshooting

### Common Issues

1. **Compilation Errors**: Ensure all generated files are included in the project
2. **Missing Dependencies**: Verify Entity Framework packages are installed
3. **Type Mapping Issues**: Check TypeMapper.cs for custom type mappings
4. **Relationship Errors**: Verify foreign key relationships are properly defined

### Debugging

- Enable detailed logging to see generation process
- Check generated files for syntax errors
- Verify database model is correctly loaded
- Test with simple models first
