# Docker Testing Setup Guide

## Overview

This document provides comprehensive guidance on using Docker containers for testing in the Shift project. Our Docker-based testing infrastructure ensures realistic, isolated, and reliable database testing.

## 🐳 **Docker Testing Infrastructure**

### **Key Components**

| Component | Purpose | Location |
|-----------|---------|----------|
| **SqlServerContainerFixture** | Manages SQL Server 2022 containers | `src/test/Shift.Tests/Infrastructure/SqlServerContainerFixture.cs` |
| **SqlServerTestHelper** | Database creation/cleanup utilities | `src/test/Shift.Tests/Infrastructure/SqlServerTestHelper.cs` |
| **Collection Pattern** | Test isolation with `[Collection("SqlServer")]` | Used in test classes |

### **Benefits of Docker Approach**

- ✅ **Perfect Isolation**: Each test gets unique database
- ✅ **Automatic Cleanup**: Databases dropped after each test
- ✅ **Realistic Testing**: Uses actual SQL Server behavior
- ✅ **Consistent Patterns**: Aligns with existing integration tests
- ✅ **No Test Interference**: Tests can't affect each other
- ✅ **Cross-Platform**: Works on Windows, Linux, and macOS (verified on Windows)

## 🚀 **Getting Started**

### **Prerequisites**

1. **Docker Desktop** installed and running
2. **Testcontainers** NuGet package (already included)
3. **SQL Server 2022** container image (automatically pulled)

### **Basic Test Structure**

```csharp
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;

[Collection("SqlServer")]
public class YourTestClass
{
    private readonly SqlServerContainerFixture _containerFixture;
    private readonly ILogger<YourClass> _logger;

    public YourTestClass(SqlServerContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<YourClass>();
    }

    [Fact]
    public async Task YourTest_ShouldWorkWithDatabase()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Act & Assert
            // Your test logic here
        }
        finally
        {
            // Clean up
            await SqlServerTestHelper.DropDatabaseAsync(
                _containerFixture.ConnectionStringMaster, databaseName);
        }
    }
}
```

## 🔧 **Infrastructure Components**

### **SqlServerContainerFixture**

**Purpose**: Manages the lifecycle of SQL Server Docker containers.

**Key Features**:
- **SQL Server 2022**: Latest version with all features
- **Automatic Startup**: Container starts and waits for SQL Server readiness
- **Connection String**: Provides master connection string for database operations
- **Cleanup**: Automatically disposes container after tests

**Configuration**:
```csharp
// Container configuration
.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
.WithEnvironment("ACCEPT_EULA", "Y")
.WithEnvironment("MSSQL_SA_PASSWORD", password)
.WithEnvironment("MSSQL_PID", "Express")
.WithPortBinding(0, 1433)
```

### **SqlServerTestHelper**

**Purpose**: Provides utilities for database management in tests.

**Key Methods**:

| Method | Purpose | Example |
|--------|---------|---------|
| `GenerateDatabaseName()` | Creates unique database names | `ShiftTests_a1b2c3d4e5f6` |
| `BuildDbConnectionString()` | Builds connection strings | `Server=host,port;Database=name;...` |
| `CreateDatabaseAsync()` | Creates test databases | `CREATE DATABASE [TestDb]` |
| `DropDatabaseAsync()` | Drops test databases | `DROP DATABASE [TestDb]` |

**Usage Pattern**:
```csharp
// Generate unique database name
var databaseName = SqlServerTestHelper.GenerateDatabaseName();

// Build connection string
var connectionString = SqlServerTestHelper.BuildDbConnectionString(
    masterConnectionString, databaseName);

// Create database
await SqlServerTestHelper.CreateDatabaseAsync(masterConnectionString, databaseName);

// Use database in test...

// Clean up
await SqlServerTestHelper.DropDatabaseAsync(masterConnectionString, databaseName);
```

## 📋 **Testing Patterns**

### **1. Database-Dependent Tests**

**Use Case**: Tests that require database connections (SqlServerLoader, SqlMigrationPlanRunner)

```csharp
[Collection("SqlServer")]
public class DatabaseTests
{
    private readonly SqlServerContainerFixture _containerFixture;

    public DatabaseTests(SqlServerContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
    }

    [Fact]
    public async Task Test_WithDatabase_ShouldWork()
    {
        // Create unique database
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Test with real database
            var service = new YourService(connectionString);
            var result = await service.DoSomething();
            
            // Assert results
            result.Should().NotBeNull();
        }
        finally
        {
            // Always clean up
            await SqlServerTestHelper.DropDatabaseAsync(
                _containerFixture.ConnectionStringMaster, databaseName);
        }
    }
}
```

### **2. Integration Tests**

**Use Case**: End-to-end testing with multiple components

```csharp
[Collection("SqlServer")]
public class IntegrationTests
{
    [Fact]
    public async Task FullWorkflow_ShouldWork()
    {
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(
            _containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // 1. Load from assembly
            var shift = new Shift { Logger = _logger };
            var targetModel = await shift.LoadFromAssembly(assembly);
            
            // 2. Apply to database
            await shift.ApplyToSqlAsync(targetModel, connectionString);
            
            // 3. Verify results
            var actualModel = await shift.LoadFromSqlAsync(connectionString);
            actualModel.Tables.Should().HaveCount(targetModel.Tables.Count);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(
                _containerFixture.ConnectionStringMaster, databaseName);
        }
    }
}
```

### **3. Error Handling Tests**

**Use Case**: Testing failure scenarios and error recovery

```csharp
[Fact]
public async Task Test_WithInvalidData_ShouldHandleGracefully()
{
    var databaseName = SqlServerTestHelper.GenerateDatabaseName();
    var connectionString = SqlServerTestHelper.BuildDbConnectionString(
        _containerFixture.ConnectionStringMaster, databaseName);
    
    await SqlServerTestHelper.CreateDatabaseAsync(
        _containerFixture.ConnectionStringMaster, databaseName);
    
    try
    {
        // Test error scenarios
        var service = new YourService(connectionString);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SqlException>(
            () => service.DoSomethingInvalid());
        
        exception.Should().NotBeNull();
    }
    finally
    {
        await SqlServerTestHelper.DropDatabaseAsync(
            _containerFixture.ConnectionStringMaster, databaseName);
    }
}
```

## 🎯 **Best Practices**

### **1. Database Naming**
- ✅ Use `SqlServerTestHelper.GenerateDatabaseName()` for unique names
- ✅ Avoid hardcoded database names
- ✅ Include test context in naming when needed

### **2. Connection Management**
- ✅ Always use `try/finally` for cleanup
- ✅ Use the master connection string for database operations
- ✅ Use the test database connection string for application logic

### **3. Test Isolation**
- ✅ Each test gets its own database
- ✅ Tests can run in parallel safely
- ✅ No shared state between tests

### **4. Error Handling**
- ✅ Always clean up databases, even on test failure
- ✅ Use `finally` blocks for cleanup
- ✅ Handle connection failures gracefully

### **5. Performance**
- ✅ Use `[Collection("SqlServer")]` for shared container
- ✅ Create/drop databases per test (not per test class)
- ✅ Keep test data minimal

## 🔍 **Troubleshooting**

### **Common Issues**

| Issue | Cause | Solution |
|-------|-------|----------|
| **Container startup timeout** | Docker not running | Start Docker Desktop |
| **Connection failures** | SQL Server not ready | Wait for readiness check |
| **Database already exists** | Previous test didn't cleanup | Check cleanup in `finally` |
| **Port conflicts** | Multiple containers | Use dynamic port binding |

### **Debug Tips**

1. **Check Docker Status**: Ensure Docker Desktop is running
2. **Verify Container Logs**: Check container startup logs
3. **Test Connection**: Verify connection string format
4. **Check Cleanup**: Ensure databases are dropped

### **Container Configuration**

```csharp
// Default configuration (in SqlServerContainerFixture)
.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
.WithEnvironment("ACCEPT_EULA", "Y")
.WithEnvironment("MSSQL_SA_PASSWORD", "Your_strong_password123!")
.WithEnvironment("MSSQL_PID", "Express")
.WithPortBinding(0, 1433)  // Dynamic port
.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
```

## 📊 **Current Usage**

### **Test Classes Using Docker**

| Test Class | Purpose | Test Count | Database Usage |
|------------|---------|------------|----------------|
| **SqlServerLoaderTests** | Schema loading tests | 12 tests | ✅ Uses Docker |
| **SqlMigrationPlanRunnerTests** | SQL execution tests | 1 test | ✅ Uses Docker |
| **SqlMigrationRunner_TypesAndConstraints_Tests** | Integration tests | 2 tests | ✅ Uses Docker |
| **SqlMigrationRunner_Mixins_Tests** | Integration tests | 1 test | ✅ Uses Docker |

### **Test Coverage**

- **Total Tests**: 62 tests (verified by running all tests)
- **Docker Tests**: 16 tests using containers (SqlServerLoaderTests: 12, SqlMigrationPlanRunnerTests: 1, Integration tests: 3)
- **Isolation**: Each test gets unique database
- **Reliability**: All tests passing

## 🚀 **Advanced Usage**

### **Custom Container Configuration**

```csharp
// For specialized testing needs
var customContainer = new ContainerBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("MSSQL_SA_PASSWORD", "CustomPassword123!")
    .WithEnvironment("MSSQL_PID", "Developer")  // Full features
    .WithPortBinding(0, 1433)
    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
    .Build();
```

### **Performance Optimization**

```csharp
// For high-performance test suites
[Collection("SqlServer")]
public class PerformanceTests
{
    // Use shared database for multiple tests
    private static readonly string SharedDatabaseName = "PerformanceTestDb";
    
    // Setup once, use many times
    // Cleanup at the end
}
```
