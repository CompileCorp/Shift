# Shift.Ef CLI Commands

## Overview

Shift.Cli includes comprehensive Entity Framework code generation commands that integrate seamlessly with the existing Shift command structure. These commands leverage the Shift.Ef library to generate production-ready Entity Framework code from various sources.

## Command Structure

### Main Command

```bash
shift ef <subcommand> [options]
```

### Available Subcommands

- `sql` - Generate from SQL Server database
- `files` - Generate from model files (YAML/JSON)
- `sql-custom` - Generate from SQL Server with custom options

## Commands Reference

### `shift ef sql`

Generates Entity Framework code directly from a SQL Server database.

#### Syntax

```bash
shift ef sql <connection_string> <output_path> [options]
```

#### Parameters

- `connection_string` - SQL Server connection string
- `output_path` - Directory where generated files will be created

#### Options

- `--namespace <name>` - Custom namespace for generated classes
- `--context <name>` - Custom DbContext class name
- `--interface <name>` - Custom DbContext interface name
- `--base-class <name>` - Custom base class to inherit from

#### Examples

```bash
# Basic generation
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated

# With custom namespace
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated --namespace MyApp.Data

# With custom context and interface
shift ef sql "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated \
  --namespace MyApp.Data \
  --context MyAppDbContext \
  --interface IMyAppDbContext
```

### `shift ef files`

Generates Entity Framework code from model files (YAML/JSON format).

#### Syntax

```bash
shift ef files <file1> [file2] ... <output_path> [options]
```

#### Parameters

- `file1, file2, ...` - Paths to model files (YAML/JSON)
- `output_path` - Directory where generated files will be created

#### Options

- `--namespace <name>` - Custom namespace for generated classes
- `--context <name>` - Custom DbContext class name
- `--interface <name>` - Custom DbContext interface name
- `--base-class <name>` - Custom base class to inherit from

#### Examples

```bash
# Generate from multiple model files
shift ef files ./Models/User.yaml ./Models/Order.yaml ./Generated

# With custom options
shift ef files ./Models/*.yaml ./Generated \
  --namespace MyApp.Data \
  --context MyAppDbContext
```

### `shift ef sql-custom`

Advanced SQL Server generation with full customization options.

#### Syntax

```bash
shift ef sql-custom <connection_string> <output_path> [options]
```

#### Parameters

- `connection_string` - SQL Server connection string
- `output_path` - Directory where generated files will be created

#### Options

- `--namespace <name>` - Custom namespace for generated classes
- `--context <name>` - Custom DbContext class name
- `--interface <name>` - Custom DbContext interface name
- `--base-class <name>` - Custom base class to inherit from

#### Examples

```bash
# Full customization
shift ef sql-custom "Server=localhost;Database=MyDb;Integrated Security=true;" ./Generated \
  --namespace MyApp.Data \
  --context MyAppDbContext \
  --interface IMyAppDbContext \
  --base-class MyCustomBaseDbContext
```

## Configuration Options

### Namespace Customization

```bash
--namespace MyApp.Data
```

Sets the namespace for all generated classes. Defaults to `Compile.Shift.Generated`.

### Context Class Name

```bash
--context MyAppDbContext
```

Sets the name of the generated DbContext class. Defaults to `GeneratedDbContext`.

### Interface Name

```bash
--interface IMyAppDbContext
```

Sets the name of the generated DbContext interface. Defaults to `IGeneratedDbContext`.

### Base Class

```bash
--base-class MyCustomBaseDbContext
```

Sets the base class that the generated DbContext will inherit from. Defaults to `DbContext`.

## Generated File Structure

### Default Structure

```
Generated/
├── Entities/
│   ├── ClientEntity.g.cs
│   ├── OrderEntity.g.cs
│   └── ...
├── Maps/
│   ├── ClientEntityMap.g.cs
│   ├── OrderEntityMap.g.cs
│   └── ...
├── Contexts/
│   ├── GeneratedDbContext.g.cs
│   └── IGeneratedDbContext.g.cs
└── Extensions/
    └── (for custom extensions)
```

### Custom Structure (with options)

```
Generated/
├── Entities/
│   ├── ClientEntity.g.cs
│   └── OrderEntity.g.cs
├── Maps/
│   ├── ClientEntityMap.g.cs
│   └── OrderEntityMap.g.cs
├── Contexts/
│   ├── MyAppDbContext.g.cs      # Custom context name
│   └── IMyAppDbContext.g.cs     # Custom interface name
└── Extensions/
    └── (for custom extensions)
```

## Integration with Existing Commands

### Help System

```bash
# Show all commands including EF commands
shift

# Show EF-specific help
shift ef --help

# Show specific EF command help
shift ef sql --help
```

### Error Handling

- Connection string validation
- Output path verification
- Option validation
- Clear error messages with suggestions

### Logging

- Respects Shift's logging configuration
- Detailed generation progress
- Error reporting
- Success confirmation

## Usage Patterns

### Development Workflow

1. **Initial Generation**
   ```bash
   shift ef sql "Server=localhost;Database=MyDb;" ./Generated
   ```

2. **Custom Configuration**
   ```bash
   shift ef sql-custom "Server=localhost;Database=MyDb;" ./Generated \
     --namespace MyApp.Data \
     --context MyAppDbContext
   ```

3. **Incremental Updates**
   ```bash
   # Re-run with same options to update generated code
   shift ef sql-custom "Server=localhost;Database=MyDb;" ./Generated \
     --namespace MyApp.Data \
     --context MyAppDbContext
   ```

### CI/CD Integration

```bash
# In build scripts
shift ef sql "$CONNECTION_STRING" ./Generated \
  --namespace $PROJECT_NAMESPACE \
  --context ${PROJECT_NAME}DbContext
```

### Team Development

```bash
# Standardized generation for team
shift ef sql-custom "Server=localhost;Database=MyDb;" ./Generated \
  --namespace Company.Project.Data \
  --context ProjectDbContext \
  --interface IProjectDbContext \
  --base-class CompanyDbContext
```

## Troubleshooting

### Common Issues

1. **Connection String Issues**
   - Verify SQL Server is accessible
   - Check authentication credentials
   - Ensure database exists

2. **Output Path Issues**
   - Ensure output directory exists
   - Check write permissions
   - Verify path is valid

3. **Option Parsing Issues**
   - Use quotes around values with spaces
   - Ensure proper option syntax
   - Check for typos in option names

### Debug Mode

```bash
# Enable verbose logging
shift ef sql "connection_string" ./Generated --verbose
```

### Validation

```bash
# Test connection without generation
shift ef sql "connection_string" ./Generated --dry-run
```

## Best Practices

### Command Organization

1. **Use consistent naming** for context and interface names
2. **Organize by namespace** to avoid conflicts
3. **Use meaningful base classes** for common functionality
4. **Version control generated files** appropriately

### Integration

1. **Include in build scripts** for automated generation
2. **Document generation process** for team members
3. **Use consistent options** across environments
4. **Test generated code** before committing

### Maintenance

1. **Regular regeneration** when database schema changes
2. **Review generated code** for accuracy
3. **Update options** as requirements change
4. **Monitor for breaking changes** in dependencies
