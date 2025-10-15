# Shift Testing Strategy

## Overview

Shift employs a comprehensive multi-level testing strategy designed to ensure reliability, data safety, and regression prevention. Our testing approach follows the testing pyramid principle, with a strong foundation of unit tests, complemented by integration tests, specialized data safety tests, and end-to-end verification.

### Testing Philosophy

- **Data Safety First**: Protecting production data is our highest priority
- **Realistic Testing**: Using actual SQL Server via Docker containers, not in-memory alternatives
- **Comprehensive Coverage**: Multiple testing levels provide confidence in all scenarios
- **Maintainable Patterns**: Clear, consistent testing patterns across the codebase

## Testing Pyramid

```
        /\
       /  \     E2E Tests (8 tests)
      /____\    - Complete workflows
     /      \   
    /        \  Integration Tests (10 tests)
   /__________\ - Real database operations
  /            \
 /              \  Unit Tests (70+ tests)
/________________\ - Pure logic testing
```

## Level 1: Unit Tests

**Purpose**: Test pure logic without external dependencies

**Framework**: `UnitTestContext<T>` with AutoMocker for dependency injection

**Examples**:
- `MigrationPlannerTests.cs` - Plan generation logic
- `ParserTests.cs` - DMD parsing logic  
- `ModelExporterTests.cs` - Model export logic
- `AssemblyLoadingTests.cs` - Assembly loading logic

**Benefits**:
- ⚡ **Fast execution** (milliseconds)
- 🔒 **Isolated** - no external dependencies
- 🎯 **Deterministic** - same result every time
- 🧪 **Easy to debug** - clear failure points

**Example Pattern**:
```csharp
public class MigrationPlannerTests : UnitTestContext<MigrationPlanner>
{
    [Fact]
    public void GeneratePlan_WithNewTables_ShouldCreateTableSteps()
    {
        // Arrange
        var targetModel = CreateTargetModelWithTables();
        var actualModel = new DatabaseModel();

        // Act
        var plan = Sut.GeneratePlan(targetModel, actualModel);

        // Assert
        plan.Steps.Should().Contain(step => 
            step.Action == MigrationAction.CreateTable);
    }
}
```

## Level 2: Integration Tests with Real Database

**Purpose**: Test components with actual SQL Server database

**Framework**: Docker containers via `SqlServerContainerFixture`

**Examples**:
- `SqlServerLoaderTests.cs` - Schema loading from database
- `SqlMigrationPlanRunnerTests.cs` - SQL execution
- `Integration/SqlMigrationRunner_TypesAndConstraints_Tests.cs`

**Benefits**:
- 🗄️ **Realistic behavior** - actual SQL Server features
- 🔧 **SQL Server specific** - tests real database constraints
- 🚀 **Production-like** - mirrors real deployment scenarios
- 🐳 **Isolated** - each test gets unique database

**Example Pattern**:
```csharp
[Collection("SqlServer")]
public class SqlServerLoaderTests
{
    private readonly SqlServerContainerFixture _fixture;

    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadTablesFromDatabase()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(
            _fixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(
            _fixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Act & Assert
            var loader = new SqlServerLoader(connectionString);
            var result = await loader.LoadDatabaseAsync();
            
            result.Tables.Should().NotBeEmpty();
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(
                _fixture.ConnectionStringMaster, databaseName);
        }
    }
}
```

## Level 3: Data Safety Tests

**Purpose**: Specialized tests for data loss prevention during migrations

**Framework**: `SqlMigrationPlanRunnerDataSafetyTests.cs`

**Examples**:
- String truncation detection
- Decimal precision reduction detection  
- Binary data truncation
- Char/nchar data truncation

**Benefits**:
- 🛡️ **Data Protection** - prevents destructive migrations
- ⚠️ **Early Warning** - catches unsafe operations before execution
- 🔍 **Comprehensive** - covers all data types and scenarios
- 📊 **Real Data** - tests with actual data that would be affected

**Example Pattern**:
```csharp
[Collection("SqlServer")]
public class SqlMigrationPlanRunnerDataSafetyTests
{
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithStringTruncation_ShouldReturnTrue()
    {
        // Arrange - Create table with data that would be truncated
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var createTableCmd = new SqlCommand(
            "CREATE TABLE TestUser (Username nvarchar(200) NOT NULL)", connection);
        await createTableCmd.ExecuteNonQueryAsync();
        
        await using var insertCmd = new SqlCommand(
            "INSERT INTO TestUser (Username) VALUES ('Very long username that exceeds target precision')", 
            connection);
        await insertCmd.ExecuteNonQueryAsync();

        // Act - Try to reduce precision (unsafe)
        var plan = new MigrationPlan();
        plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.AlterColumn,
            TableName = "TestUser",
            Fields = new List<FieldModel>
            {
                new() { Name = "Username", Type = "nvarchar", Precision = 50 }
            }
        });

        var runner = new SqlMigrationPlanRunner(connectionString, plan);
        var result = runner.Run();

        // Assert - Operation should be skipped
        result.Should().BeEmpty("Unsafe operation should be skipped");
        
        // Verify column was NOT altered
        var checkQuery = "SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TestUser'";
        var precision = (int)await new SqlCommand(checkQuery, connection).ExecuteScalarAsync();
        precision.Should().Be(200, "Column should retain original precision");
    }
}
```

## Level 4: End-to-End Tests

**Purpose**: Test complete workflows from assembly loading to database execution

**Framework**: `ShiftTests.cs` and integration test classes

**Examples**:
- Complete migration workflows
- Assembly loading → planning → execution → verification
- Mixin application workflows
- Safe shrink operations

**Benefits**:
- 🔄 **Complete Workflows** - tests entire user journeys
- 🎯 **User-Focused** - validates real usage patterns
- 🔗 **Component Integration** - tests how components work together
- 📋 **Business Logic** - validates complete business processes

**Example Pattern**:
```csharp
[Collection("SqlServer")]
public class ShiftTests
{
    [Fact]
    public async Task CompleteWorkflow_ModelToDatabase_ShouldWorkEndToEnd()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Act - Complete workflow
            var shift = new Shift { Logger = _logger };
            
            // 1. Load from assembly
            var targetModel = await shift.LoadFromAssemblyAsync(assembly);
            
            // 2. Apply to database
            await shift.ApplyToSqlAsync(targetModel, connectionString);
            
            // 3. Verify by loading back
            var actualModel = await shift.LoadFromSqlAsync(connectionString);
            
            // Assert
            actualModel.Tables.Should().HaveCount(targetModel.Tables.Count);
            actualModel.Tables.Keys.Should().BeEquivalentTo(targetModel.Tables.Keys);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(
                _containerFixture.ConnectionStringMaster, databaseName);
        }
    }
}
```

## Level 5: Snapshot/Verification Tests

**Purpose**: Prevent regression in output format and content

**Framework**: Verify library with `.verified.txt` files

**Examples**:
- `ModelExporterTests` with verified output files
- `ParserTests.ParseTable_SimpleModel_ShouldCreateTableWithPrimaryKey.verified.txt`
- DMD content generation verification

**Benefits**:
- 📸 **Regression Prevention** - catches unintended output changes
- 📝 **Documentation** - verified files serve as examples
- 🔍 **Format Validation** - ensures consistent output formatting
- ⚡ **Fast Feedback** - quickly identifies output changes

**Example Pattern**:
```csharp
public class ModelExporterTests
{
    [Fact]
    public void GenerateDmdContent_WithSimpleTable_ShouldGenerateCorrectContent()
    {
        // Arrange
        var model = CreateDatabaseModelWithSimpleTable();
        var exporter = new ModelExporter();

        // Act
        var result = exporter.GenerateDmdContent(model);

        // Assert - Verify against snapshot
        Verify(result);
    }
}
```

## Testing Infrastructure

### Docker Containers
- **Testcontainers** for SQL Server 2022 containers
- **Automatic lifecycle** management (start/stop/cleanup)
- **Port binding** for dynamic port allocation
- **Readiness checks** to ensure SQL Server is ready

### Test Helpers
- **`SqlServerTestHelper`** - Database creation/cleanup utilities
- **`DatabaseModelBuilder`** - Fluent API for test data creation
- **`TestModels`** - Pre-built test scenarios
- **`UnitTestContext<T>`** - Base class for unit tests with AutoMocker

### Shared Fixtures
- **`SqlServerContainerFixture`** - Shared SQL Server container
- **`[Collection("SqlServer")]`** - xUnit collection for test isolation
- **Performance optimization** - Reuse containers across tests

## Best Practices

### When to Use Each Testing Level

| Level | Use For | Don't Use For |
|-------|---------|---------------|
| **Unit Tests** | Pure logic, algorithms, business rules | Database operations, external APIs |
| **Integration Tests** | Database operations, SQL execution | Pure logic, fast feedback |
| **Data Safety Tests** | Migration safety, data loss prevention | General functionality |
| **E2E Tests** | Complete workflows, user scenarios | Individual component testing |
| **Snapshot Tests** | Output format, content generation | Logic testing, database operations |

### Test Naming Conventions

```csharp
// Pattern: MethodName_Scenario_ExpectedResult
[Fact]
public void GeneratePlan_WithNewTables_ShouldCreateTableSteps()

[Fact] 
public void LoadDatabaseAsync_WithInvalidConnectionString_ShouldThrowException()

[Fact]
public void IsAlterColumnPotentiallyUnsafe_WithStringTruncation_ShouldReturnTrue()
```

### Database Cleanup Patterns

```csharp
[Fact]
public async Task Test_WithDatabase_ShouldWork()
{
    var databaseName = SqlServerTestHelper.GenerateDatabaseName();
    var connectionString = SqlServerTestHelper.BuildDbConnectionString(
        _fixture.ConnectionStringMaster, databaseName);
    
    await SqlServerTestHelper.CreateDatabaseAsync(
        _fixture.ConnectionStringMaster, databaseName);
    
    try
    {
        // Test logic here
    }
    finally
    {
        // Always clean up, even on failure
        await SqlServerTestHelper.DropDatabaseAsync(
            _fixture.ConnectionStringMaster, databaseName);
    }
}
```

### Parallel Test Execution

- ✅ **Unit tests** - Can run in parallel safely
- ✅ **Integration tests** - Each gets unique database
- ✅ **Data safety tests** - Isolated by database name
- ✅ **E2E tests** - Independent workflows

## Current Test Coverage

### Test Counts by Level

| Level | Test Count | Examples |
|-------|------------|----------|
| **Unit Tests** | ~70 tests | MigrationPlanner (13), Parser (15), ModelExporter (15), AssemblyLoading (6), Others (21) |
| **Integration Tests** | ~10 tests | SqlServerLoader (12), SqlMigrationPlanRunner (11), Integration (3) |
| **Data Safety Tests** | 7 tests | SqlMigrationPlanRunnerDataSafetyTests |
| **E2E Tests** | ~8 tests | ShiftTests, Integration workflows |
| **Snapshot Tests** | ~15 tests | ModelExporter, Parser output verification |
| **Total** | **99 tests** | Comprehensive coverage across all levels |

### Coverage by Component

| Component | Unit Tests | Integration Tests | Data Safety | E2E | Total |
|-----------|------------|-------------------|-------------|-----|-------|
| **MigrationPlanner** | 13 | - | - | - | 13 |
| **SqlMigrationPlanRunner** | 11 | - | 7 | - | 18 |
| **Parser** | 15 | - | - | - | 15 |
| **ModelExporter** | 15 | - | - | - | 15 |
| **SqlServerLoader** | - | 12 | - | - | 12 |
| **Shift (E2E)** | - | - | - | 8 | 8 |
| **Integration** | - | 3 | - | - | 3 |
| **Others** | 15 | - | - | - | 15 |

## Running Tests

### Prerequisites
- **Docker Desktop** must be running
- **.NET 9.0** SDK installed
- **Testcontainers** NuGet package (included)

### Commands

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "MigrationPlannerTests"

# Run integration tests only
dotnet test --filter "SqlServerLoaderTests"

# Run with verbose output
dotnet test --logger "console;verbosity=normal"

# Run tests in specific project
dotnet test test/Shift.Tests/
```

### Test Execution Time
- **Unit Tests**: ~1-2 seconds total
- **Integration Tests**: ~3-5 minutes total (includes Docker startup)
- **All Tests**: ~4-6 minutes total

## Test Maintenance

### Updating Snapshot Tests
When output format changes intentionally:
1. Delete the `.verified.txt` file
2. Run the test - it will fail
3. Review the new output in the `.received.txt` file
4. Rename `.received.txt` to `.verified.txt`
5. Commit the updated verification file

### Managing Test Data
- Use `DatabaseModelBuilder` for consistent test data
- Leverage `TestModels` for common scenarios
- Keep test data minimal and focused
- Use unique database names to avoid conflicts
