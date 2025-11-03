# Shift CLI Reference

The Shift CLI is a command-line tool that provides a user-friendly interface for database migrations, schema exports, and Entity Framework code generation. It serves as the primary entry point for interacting with the Shift framework from the command line.

## Overview

### What is Shift CLI?

Shift CLI is a .NET 9.0 console application that enables developers to:

- **Apply database migrations** from DMD files to SQL Server databases
- **Generate Entity Framework code** from databases or DMD files
- **Deploy embedded models** from assembly resources
- **Automate database schema management** in CI/CD pipelines
- **Support multiple workflows** for database-first and code-first development

### Key Features

- 🧠 **Domain Migration Definition (DMD) System** - Human-readable database schema definitions
- 🏗️ **Entity Framework Code Generation** - Automatic EF Core code generation
- 📦 **Assembly Resource Support** - Embedded DMD files in assemblies
- 🔄 **Migration Automation** - Compare and apply schema changes
- ⚙️ **Customizable Output** - Configurable namespaces, class names, and interfaces

## Installation

### Prerequisites

- **.NET 9.0 SDK** or later
- **SQL Server** (LocalDB, Express, or full edition)
- **Windows, macOS, or Linux** (cross-platform support)

### Installation Methods

#### Global Tool Installation (Recommended)

```bash
# Install as a global .NET tool
dotnet tool install --global Shift.Cli

# Verify installation
shift --version
```

#### Building from Source

```bash
# Clone the repository
git clone https://github.com/your-org/Shift.git
cd Shift

# Build the CLI
dotnet build src/Shift.Cli

# Run directly
dotnet run --project src/Shift.Cli
```

#### Local Development

```bash
# Navigate to CLI project
cd src/Shift.Cli

# Run with arguments
dotnet run apply "Server=.;Database=MyDb;" ./Models
```

## Command Reference

### Apply Command

Apply DMD/DMDX files to a SQL Server database.

#### Syntax
```bash
shift apply <connection_string> <path1> [path2] ...
```

#### Parameters
- **`connection_string`** - SQL Server connection string
- **`path1`** - Path to DMD files (file or directory)
- **`path2`** - Additional paths (optional)

#### Examples

**Single directory:**
```bash
shift apply "Server=.;Database=MyDb;" ./Models
```

**Multiple paths:**
```bash
shift apply "Server=.;Database=MyDb;" ./Models/Core ./Models/Auth ./Models/Features
```

**Specific files:**
```bash
shift apply "Server=.;Database=MyDb;" ./Models/User.dmd ./Models/Order.dmd
```

#### Workflow
1. **Load DMD files** from specified paths
2. **Connect to database** using connection string
3. **Compare schemas** (target vs actual)
4. **Generate migration plan** with required changes
5. **Apply migrations** to update database schema
6. **Report results** with success/failure status

### Apply-Assemblies Command

Apply embedded DMD resources from .NET assemblies to a database.

#### Syntax
```bash
shift apply-assemblies <connection_string> <dll1> [dll2] ... [filter1] [filter2] ...
```

#### Parameters
- **`connection_string`** - SQL Server connection string
- **`dll1`, `dll2`, ...** - Paths to assemblies containing embedded DMD resources (files ending with `.dll`)
- **`filter1`, `filter2`, ...** - Optional namespace filters to limit which resources are loaded (any argument not ending with `.dll`)

**Note:** DLLs and filters can be specified in any order. Arguments ending with `.dll` are treated as assembly paths, all other arguments are treated as namespace filters. All filters apply to all assemblies.

#### Examples

**Single assembly:**
```bash
shift apply-assemblies "Server=.;Database=MyDb;" ./MyApp.Models.dll
```

**Multiple assemblies:**
```bash
shift apply-assemblies "Server=.;Database=MyDb;" ./Core.Models.dll ./Auth.Models.dll
```

**With namespace filtering:**
```bash
# Load only models from specific namespaces
shift apply-assemblies "Server=.;Database=MyDb;" ./MyApp.Models.dll MyApp.Models MyApp.Mixins
```

**Multiple assemblies with filters:**
```bash
# All filters apply to all assemblies
shift apply-assemblies "Server=.;Database=MyDb;" ./Core.Models.dll ./Auth.Models.dll Core.Models Domain.Models
```

**Mixed order (DLLs and filters can be interleaved):**
```bash
shift apply-assemblies "Server=.;Database=MyDb;" ./Lib1.dll Namespace1 ./Lib2.dll Namespace2 Namespace3
```

#### Namespace Filtering

Namespace filters allow you to load only specific subsets of models from assemblies. A resource matches a filter if its manifest resource name:
- Starts with the namespace followed by a dot (e.g., `MyNamespace.File.dmd` matches `MyNamespace`)
- Or exactly equals the namespace (e.g., `MyNamespace.dmd` matches `MyNamespace`)

**Example:**
```bash
# Assembly contains: MyApp.Models.User.dmd, MyApp.Legacy.OldModel.dmd, MyApp.Mixins.Auditable.dmdx
# Filter loads only: MyApp.Models.User.dmd and MyApp.Mixins.Auditable.dmdx
shift apply-assemblies "Server=.;Database=MyDb;" ./MyApp.dll MyApp.Models MyApp.Mixins
```

#### Use Cases
- **Distributed deployments** - Deploy models with application assemblies
- **Embedded resources** - Include DMD files as assembly resources
- **Version control** - Model versions tied to application versions
- **Microservices** - Each service includes its own models
- **Selective loading** - Load only specific namespaces from large assemblies

### Export Command

Export SQL Server schema to DMD files (reverse engineering).

#### Syntax
```bash
shift export <connection_string> <schema> <path>
```

#### Parameters
- **`connection_string`** - SQL Server connection string
- **`schema`** - Database schema to export (e.g., "dbo")
- **`path`** - Output directory for DMD files

#### Examples

**Export dbo schema:**
```bash
shift export "Server=.;Database=MyDb;" "dbo" ./ExportedModels
```

**Export specific schema:**
```bash
shift export "Server=.;Database=MyDb;" "MySchema" ./ExportedModels
```

#### Status
⚠️ **Partially implemented** - Core infrastructure exists but CLI command needs completion

#### Available Commands
- **Database loading** - `SqlServerLoader` can read database schemas
- **DMD generation** - `ModelExporter` can create DMD files
- **Schema analysis** - Full table, column, foreign key, and index support

#### Implementation Status
- ✅ **Core infrastructure** - `SqlServerLoader` and `ModelExporter` are complete
- ✅ **Database model loading** - Can load complete database schemas
- ✅ **DMD file generation** - Can export tables to DMD format
- ⚠️ **CLI command** - `CommandExportAsync` throws `NotImplementedException`
- ⚠️ **Mixin support** - Mixin detection and generation needs implementation
- ⚠️ **Testing** - Export functionality needs comprehensive testing

## Entity Framework Code Generation

### EF SQL Command

Generate Entity Framework code from an existing SQL Server database.

#### Syntax
```bash
shift ef sql <connection-string> <output-path> [schema]
```

#### Parameters
- **`connection_string`** - SQL Server connection string
- **`output_path`** - Directory for generated EF code
- **`schema`** - Database schema (optional, defaults to "dbo")

#### Examples

**Basic generation:**
```bash
shift ef sql "Server=.;Database=MyDb;" ./Generated
```

**Specific schema:**
```bash
shift ef sql "Server=.;Database=MyDb;" ./Generated "MySchema"
```

**Generated Files:**
- **Entities** - C# entity classes for each table
- **DbContext** - Main database context class
- **Entity Maps** - Fluent API configuration classes
- **Interfaces** - Repository and context interfaces

### EF Files Command

Generate Entity Framework code from DMD files.

#### Syntax
```bash
shift ef files <path1> [path2] ... <output-path>
```

#### Parameters
- **`path1`** - Path to DMD files (file or directory)
- **`path2`** - Additional DMD paths (optional)
- **`output_path`** - Directory for generated EF code (last argument)

#### Examples

**From directories:**
```bash
shift ef files ./Models ./Generated
```

**From specific files:**
```bash
shift ef files ./Models/User.dmd ./Models/Order.dmd ./Generated
```

**Multiple paths:**
```bash
shift ef files ./Models/Core ./Models/Auth ./Models/Features ./Generated
```

#### Use Cases
- **DMD-first development** - Generate EF code before database creation
- **New projects** - Start with DMD files and generate EF code
- **Schema validation** - Verify DMD files generate expected EF code

### EF SQL-Custom Command

Generate Entity Framework code with custom naming and structure options.

#### Syntax
```bash
shift ef sql-custom <connection-string> <output-path> [options]
```

#### Options
- **`--namespace <name>`** - Custom namespace for generated classes
- **`--context <name>`** - Custom DbContext class name
- **`--interface <name>`** - Custom interface name for DbContext
- **`--base-class <name>`** - Custom base class for DbContext

#### Examples

**Custom namespace and context:**
```bash
shift ef sql-custom "Server=.;Database=MyDb;" ./Generated \\
  --namespace MyApp.Data \\
  --context MyAppDbContext
```

**Full customization:**
```bash
shift ef sql-custom "Server=.;Database=MyDb;" ./Generated \\
  --namespace ECommerce.Data \\
  --context ECommerceDbContext \\
  --interface IECommerceDbContext \\
  --base-class BaseDbContext
```

#### Use Cases
- **Team standards** - Enforce consistent naming conventions
- **Legacy integration** - Match existing code patterns
- **Multi-tenant applications** - Separate contexts per tenant
- **Testing** - Generate testable interfaces and base classes

## Connection Strings

### SQL Server Connection Strings

#### Windows Authentication
```bash
# Local SQL Server
"Server=.;Database=MyDb;Integrated Security=true;"

# Named instance
"Server=.\SQLEXPRESS;Database=MyDb;Integrated Security=true;"

# Remote server
"Server=server-name;Database=MyDb;Integrated Security=true;"
```

#### SQL Authentication
```bash
# Basic authentication
"Server=.;Database=MyDb;User Id=sa;Password=YourPassword;"

# With trusted connection
"Server=.;Database=MyDb;User Id=user;Password=pass;TrustServerCertificate=True;"
```

#### LocalDB
```bash
# LocalDB instance
"Server=(localdb)\\mssqllocaldb;Database=MyDb;"

# Named LocalDB instance
"Server=(localdb)\\MyInstance;Database=MyDb;"
```

#### Azure SQL Database
```bash
# Azure SQL with authentication
"Server=tcp:server.database.windows.net,1433;Database=MyDb;User Id=user@server;Password=pass;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

#### Development Settings
```bash
# Trust server certificate (development only)
"Server=.;Database=MyDb;Integrated Security=true;TrustServerCertificate=True;"

# Connection timeout
"Server=.;Database=MyDb;Integrated Security=true;Connection Timeout=60;"
```

## Workflow Examples

### Database-First Workflow

Start with an existing database and generate DMD files and EF code.

```bash
# 1. Create DMD files (manual or export when available)
# 2. Apply to database
shift apply "Server=.;Database=MyDb;" ./Models

# 3. Generate EF code from database
shift ef sql "Server=.;Database=MyDb;" ./Generated
```

### Code-First Workflow

Start with DMD files and generate EF code before database creation.

```bash
# 1. Create DMD files
# 2. Generate EF code directly from DMD files
shift ef files ./Models/User.dmd ./Models/Order.dmd ./Generated

# 3. Apply to database
shift apply "Server=.;Database=MyDb;" ./Models
```

### Embedded Resources Workflow

Deploy models as embedded resources in assemblies.

```bash
# 1. Build assembly with embedded DMD resources
dotnet build MyApp.Models

# 2. Deploy DLL to target environment
# 3. Apply from assembly (loads all resources)
shift apply-assemblies "Server=.;Database=MyDb;" ./MyApp.Models.dll

# 4. Or apply with namespace filtering (loads only specific namespaces)
shift apply-assemblies "Server=.;Database=MyDb;" ./MyApp.Models.dll MyApp.Models MyApp.Mixins
```

### Multi-Environment Workflow

Deploy to multiple environments with different configurations.

```bash
# Development
shift apply "Server=dev-server;Database=MyDb;" ./Models

# Staging
shift apply "Server=staging-server;Database=MyDb;" ./Models

# Production
shift apply "Server=prod-server;Database=MyDb;" ./Models
```

## Complete Examples

### E-Commerce Application

```bash
# Apply schema to database
shift apply "Server=.;Database=ECommerce;" ./Models

# Generate EF code with custom options
shift ef sql-custom "Server=.;Database=ECommerce;" ./ECommerce.Data \\
  --namespace ECommerce.Data \\
  --context ECommerceDbContext \\
  --interface IECommerceDbContext
```

**Generated Structure:**
```
ECommerce.Data/
├── Entities/
│   ├── User.cs
│   ├── Product.cs
│   ├── Order.cs
│   └── OrderItem.cs
├── ECommerceDbContext.cs
├── IECommerceDbContext.cs
└── Configurations/
    ├── UserConfiguration.cs
    ├── ProductConfiguration.cs
    └── OrderConfiguration.cs
```

### Task Management System

```bash
# Apply from multiple model directories
shift apply "Server=.;Database=TaskManager;" \\
  ./Models/Core \\
  ./Models/Auth \\
  ./Models/Tasks

# Generate EF code
shift ef sql "Server=.;Database=TaskManager;" ./Generated
```

### Microservices Architecture

```bash
# User Service
shift apply-assemblies "Server=.;Database=UserService;" ./UserService.Models.dll

# Order Service  
shift apply-assemblies "Server=.;Database=OrderService;" ./OrderService.Models.dll

# Product Service
shift apply-assemblies "Server=.;Database=ProductService;" ./ProductService.Models.dll

# Shared library with selective loading (multiple services share same DLL)
shift apply-assemblies "Server=.;Database=UserService;" ./Shared.Models.dll UserService.Models
shift apply-assemblies "Server=.;Database=OrderService;" ./Shared.Models.dll OrderService.Models
```

## Logging and Output

### Console Output

The CLI provides rich console output with visual indicators:

```
🧠 Domain Migration Definition (DMD) System
🏗️  Generating Entity Framework code from SQL Server...
   Connection: Server=.;Database=MyDb;
   Schema: dbo
   Output: ./Generated
✅ Entity Framework code generation completed!
```

### Log Levels

- **Information** - Migration actions, progress updates
- **Warning** - Non-critical issues, deprecated features
- **Error** - Failures, exceptions, invalid configurations
- **Debug** - Detailed execution flow (when enabled)

### Timestamp Format

All log messages include timestamps in `HH:mm:ss` format for easy tracking:

```
14:23:15 CreateTable User
14:23:15 CreateTable Product
14:23:16 AddForeignKey Order CustomerID
14:23:16 ✅ Migration completed successfully
```

## Error Handling

### Common Errors

#### Connection Issues
```bash
# Error: Invalid connection string
❌ Error: Failed to connect to database

# Solution: Verify connection string format
shift apply "Server=.;Database=MyDb;Integrated Security=true;" ./Models
```

#### File Path Issues
```bash
# Error: DMD files not found
❌ Error: No DMD files found in specified path

# Solution: Check file paths and extensions
ls ./Models/*.dmd
```

#### Database Permission Issues
```bash
# Error: Insufficient permissions
❌ Error: Cannot create table - insufficient permissions

# Solution: Grant appropriate database permissions
```

#### Assembly Loading Issues
```bash
# Error: Assembly not found
❌ Error: Assembly file does not exist: ./MyApp.dll

# Solution: Verify assembly path and .NET version compatibility
```

### Exit Codes

- **`0`** - Success
- **`1`** - General error
- **`2`** - Invalid arguments
- **`3`** - Database connection failed
- **`4`** - File system error
- **`5`** - Assembly loading error

## Best Practices

### File Organization

#### Recommended Structure
```
MyProject/
├── Models/
│   ├── Core/
│   │   ├── User.dmd
│   │   └── Product.dmd
│   ├── Auth/
│   │   ├── Role.dmd
│   │   └── Permission.dmd
│   └── Features/
│       ├── Order.dmd
│       └── OrderItem.dmd
├── Mixins/
│   ├── BaseEntity.dmdx
│   └── Auditable.dmdx
└── Generated/
    ├── Entities/
    ├── DbContext.cs
    └── Configurations/
```

#### Naming Conventions
- **DMD files** - Match model names exactly (`User.dmd`, `OrderItem.dmd`)
- **Directories** - Use PascalCase (`Models/Core/`)
- **Mixins** - Descriptive names (`BaseEntity.dmdx`, `Auditable.dmdx`)

### Migration Strategy

#### Development Workflow
1. **Create DMD files** for new features
2. **Test locally** with development database
3. **Review migration plan** before applying
4. **Commit DMD files** to version control
5. **Deploy to staging** for integration testing

#### Production Deployment
1. **Backup production database** before major changes
2. **Test migrations** on staging environment
3. **Deploy during maintenance windows** for large changes
4. **Monitor migration progress** and rollback if needed

### EF Code Generation

#### Namespace Conventions
```bash
# Consistent namespace structure
shift ef sql-custom "Server=.;Database=MyDb;" ./Generated \\
  --namespace MyApp.Data.Entities \\
  --context MyAppDbContext \\
  --interface IMyAppDbContext
```

#### Testing Integration
```bash
# Generate with testable interfaces
shift ef sql-custom "Server=.;Database=MyDb;" ./Generated \\
  --interface IMyAppDbContext \\
  --base-class TestableDbContext
```

## Integration Scenarios

### CI/CD Pipeline

#### Azure DevOps
```yaml
# azure-pipelines.yml
- task: DotNetCoreCLI@2
  displayName: 'Apply Database Migrations'
  inputs:
    command: 'custom'
    custom: 'shift'
    arguments: 'apply "$(CONNECTION_STRING)" ./Models'

- task: DotNetCoreCLI@2
  displayName: 'Generate EF Code'
  inputs:
    command: 'custom'
    custom: 'shift'
    arguments: 'ef sql "$(CONNECTION_STRING)" ./Generated'
```

#### GitHub Actions
```yaml
# .github/workflows/deploy.yml
- name: Apply Database Migrations
  run: shift apply "${{ secrets.CONNECTION_STRING }}" ./Models

- name: Generate Entity Framework Code
  run: shift ef sql "${{ secrets.CONNECTION_STRING }}" ./Generated
```

### Docker Deployments

#### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Copy DMD files
COPY Models/ /app/Models/

# Install Shift CLI
RUN dotnet tool install --global Shift.Cli

# Apply migrations
RUN shift apply "$CONNECTION_STRING" /app/Models

# Generate EF code
RUN shift ef sql "$CONNECTION_STRING" /app/Generated

# Build application
COPY . /app/
WORKDIR /app
RUN dotnet build
```

#### Docker Compose
```yaml
# docker-compose.yml
version: '3.8'
services:
  app:
    build: .
    environment:
      - CONNECTION_STRING=Server=db;Database=MyDb;User Id=sa;Password=YourPassword;
    depends_on:
      - db
  
  db:
    image: mcr.microsoft.com/mssql/server:latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourPassword
```

### Multi-Environment Scripts

#### PowerShell Script
```powershell
# deploy.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$Environment
)

$databases = @{
    "dev" = "Server=dev-server;Database=MyDb;"
    "staging" = "Server=staging-server;Database=MyDb;"
    "prod" = "Server=prod-server;Database=MyDb;"
}

$connectionString = $databases[$Environment]
if (-not $connectionString) {
    Write-Error "Invalid environment: $Environment"
    exit 1
}

Write-Host "Deploying to $Environment environment..."
shift apply $connectionString ./Models
shift ef sql $connectionString ./Generated
```

#### Bash Script
```bash
#!/bin/bash
# deploy.sh

ENV=$1
case $ENV in
    "dev")
        CONNECTION_STRING="Server=dev-server;Database=MyDb;"
        ;;
    "staging")
        CONNECTION_STRING="Server=staging-server;Database=MyDb;"
        ;;
    "prod")
        CONNECTION_STRING="Server=prod-server;Database=MyDb;"
        ;;
    *)
        echo "Invalid environment: $ENV"
        exit 1
        ;;
esac

echo "Deploying to $ENV environment..."
shift apply "$CONNECTION_STRING" ./Models
shift ef sql "$CONNECTION_STRING" ./Generated
```

## Troubleshooting

### Connection Issues

#### SQL Server Not Running
```bash
# Error: Cannot connect to SQL Server
❌ Error: A network-related or instance-specific error occurred

# Solutions:
# 1. Verify SQL Server is running
net start MSSQLSERVER

# 2. Check firewall rules
netsh advfirewall firewall add rule name="SQL Server" dir=in action=allow protocol=TCP localport=1433

# 3. Test connection with SQL Server Management Studio
```

#### Authentication Issues
```bash
# Error: Login failed
❌ Error: Login failed for user 'sa'

# Solutions:
# 1. Enable SQL Server Authentication
# 2. Reset SA password
# 3. Use Windows Authentication
"Server=.;Database=MyDb;Integrated Security=true;"
```

### File Path Issues

#### Path Not Found
```bash
# Error: Path does not exist
❌ Error: The specified path does not exist

# Solutions:
# 1. Use absolute paths
shift apply "Server=.;Database=MyDb;" "C:\MyProject\Models"

# 2. Check current directory
pwd
ls -la ./Models

# 3. Verify file extensions
ls ./Models/*.dmd
```

#### Permission Issues
```bash
# Error: Access denied
❌ Error: Access to the path is denied

# Solutions:
# 1. Run as administrator
# 2. Check file permissions
# 3. Use different output directory
```

### Assembly Loading Issues

#### .NET Version Mismatch
```bash
# Error: Assembly loading failed
❌ Error: Could not load file or assembly

# Solutions:
# 1. Check .NET version compatibility
dotnet --version

# 2. Rebuild assembly with correct target framework
dotnet build --framework net9.0

# 3. Verify assembly dependencies
```

#### Missing Dependencies
```bash
# Error: Missing assembly references
❌ Error: Could not load file or assembly 'System.Data.SqlClient'

# Solutions:
# 1. Include all dependencies
# 2. Use self-contained deployment
# 3. Install required packages
```

## Advanced Usage

### Scripting and Automation

#### PowerShell Automation
```powershell
# deploy-all-environments.ps1
$environments = @("dev", "staging", "prod")
$modelsPath = "./Models"

foreach ($env in $environments) {
    Write-Host "Deploying to $env environment..."
    
    $connectionString = "Server=$env-server;Database=MyDb;"
    
    # Apply migrations
    shift apply $connectionString $modelsPath
    
    # Generate EF code
    shift ef sql $connectionString "./Generated/$env"
    
    Write-Host "✅ Deployment to $env completed"
}
```

#### Batch Processing
```bash
#!/bin/bash
# process-multiple-databases.sh

DATABASES=("Database1" "Database2" "Database3")
SERVER="Server=.;"

for db in "${DATABASES[@]}"; do
    echo "Processing $db..."
    shift apply "${SERVER}Database=${db};" ./Models
    shift ef sql "${SERVER}Database=${db};" "./Generated/${db}"
done
```

### Custom Logging Configuration

#### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Compile.Shift": "Debug"
    }
  }
}
```

#### Environment Variables
```bash
# Set log level
export DOTNET_LOGGING__LOGLEVEL__DEFAULT=Debug

# Run with custom logging
shift apply "Server=.;Database=MyDb;" ./Models
```

### Performance Optimization

#### Large Database Migrations
```bash
# Use connection timeout for large databases
shift apply "Server=.;Database=MyDb;Connection Timeout=300;" ./Models

# Process in batches for very large schemas
shift apply "Server=.;Database=MyDb;" ./Models/Core
shift apply "Server=.;Database=MyDb;" ./Models/Features
```

#### Memory Optimization
```bash
# Limit concurrent operations
export DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=1

# Use streaming for large result sets
shift ef sql "Server=.;Database=MyDb;" ./Generated
```
