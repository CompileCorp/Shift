using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Compile.Shift.Integration;

/// <summary>
/// Unit tests for Shift class.
/// Tests the main orchestrator functionality including assembly loading, file path loading, SQL operations, and migration workflows.
/// Uses Docker containers for realistic database testing.
/// </summary>
[Collection("SqlServer")]
public class ShiftTests
{
    private readonly ILogger<Shift> _logger;
    private readonly SqlServerContainerFixture _containerFixture;

    public ShiftTests(SqlServerContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
    }

    #region Assembly Loading Tests

    /// <summary>
    /// Tests that Shift can load models from a single assembly with embedded resources.
    /// </summary>
    [Fact]
    public async Task LoadFromAssembly_WithValidAssembly_ShouldLoadModelsSuccessfully()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();
        var shift = new Shift { Logger = _logger };

        // Act
        var result = await shift.LoadFromAssembly(assembly);

        // Assert
        result.Should().NotBeNull();
        result.Tables.Should().NotBeNull("Should return valid model");
        result.Mixins.Should().NotBeNull("Should return valid model");
    }

    /// <summary>
    /// Tests that Shift can load models from multiple assemblies with proper priority handling.
    /// </summary>
    [Fact]
    public async Task LoadFromAssembliesAsync_WithMultipleAssemblies_ShouldLoadModelsWithPriority()
    {
        // Arrange
        var assemblies = new[] 
        { 
            Assembly.GetExecutingAssembly(),
            typeof(Shift).Assembly // Main Shift assembly
        };
        var shift = new Shift { Logger = _logger };

        // Act
        var result = await shift.LoadFromAssembliesAsync(assemblies);

        // Assert
        result.Should().NotBeNull();
        result.Tables.Should().NotBeNull("Should return valid model");
        result.Mixins.Should().NotBeNull("Should return valid model");
    }

    #endregion

    #region File Path Loading Tests

    /// <summary>
    /// Tests that Shift handles missing directories gracefully.
    /// </summary>
    [Fact]
    public async Task LoadFromPathAsync_WithMissingDirectories_ShouldHandleErrorsGracefully()
    {
        // Arrange
        var invalidPaths = new[] { "NonExistentDirectory", "AnotherMissingDir" };
        var shift = new Shift { Logger = _logger };

        // Act
        var result = await shift.LoadFromPathAsync(invalidPaths);

        // Assert
        result.Should().NotBeNull();
        result.Tables.Should().BeEmpty("Should return empty model for missing directories");
        result.Mixins.Should().BeEmpty("Should return empty model for missing directories");
    }

    #endregion

    #region SQL Server Integration Tests

    /// <summary>
    /// Tests that Shift can load models from SQL Server database.
    /// </summary>
    [Fact]
    public async Task LoadFromSqlAsync_WithValidConnection_ShouldLoadDatabaseModel()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create test tables
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            await using var createUserTableCmd = new SqlCommand(@"
                CREATE TABLE [User] (
                    UserID int IDENTITY(1,1) PRIMARY KEY,
                    Username nvarchar(100) NOT NULL,
                    Email nvarchar(256) NULL,
                    IsActive bit NOT NULL DEFAULT 1
                )", connection);
            await createUserTableCmd.ExecuteNonQueryAsync();
            
            await using var createProductTableCmd = new SqlCommand(@"
                CREATE TABLE Product (
                    ProductID int IDENTITY(1,1) PRIMARY KEY,
                    Name nvarchar(200) NOT NULL,
                    Price decimal(10,2) NOT NULL
                )", connection);
            await createProductTableCmd.ExecuteNonQueryAsync();

            var shift = new Shift { Logger = _logger };

            // Act
            var result = await shift.LoadFromSqlAsync(connectionString);

            // Assert
            result.Should().NotBeNull();
            result.Tables.Should().HaveCount(2, "Should load both User and Product tables");
            result.Tables.Keys.Should().Contain("User");
            result.Tables.Keys.Should().Contain("Product");
            
            // Verify table structure
            var userTable = result.Tables["User"];
            userTable.Fields.Should().HaveCount(4, "User table should have 4 fields");
            userTable.Fields.Should().Contain(f => f.Name == "UserID");
            userTable.Fields.Should().Contain(f => f.Name == "Username" && f.Type == "nvarchar");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that Shift can load models from SQL Server with custom schema.
    /// </summary>
    [Fact]
    public async Task LoadFromSqlAsync_WithCustomSchema_ShouldLoadFromSpecifiedSchema()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create custom schema and table
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            await using var createSchemaCmd = new SqlCommand("CREATE SCHEMA [CustomSchema]", connection);
            await createSchemaCmd.ExecuteNonQueryAsync();
            
            await using var createTableCmd = new SqlCommand(@"
                CREATE TABLE [CustomSchema].[TestTable] (
                    ID int IDENTITY(1,1) PRIMARY KEY,
                    Name nvarchar(100) NOT NULL
                )", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            var shift = new Shift { Logger = _logger };

            // Act
            var result = await shift.LoadFromSqlAsync(connectionString, "CustomSchema");

            // Assert
            result.Should().NotBeNull();
            result.Tables.Should().HaveCount(1, "Should load table from custom schema");
            result.Tables.Keys.Should().Contain("TestTable");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    #endregion

    #region Migration Workflow Tests

    /// <summary>
    /// Tests the complete migration workflow: load target model, apply to database, verify changes.
    /// </summary>
    [Fact]
    public async Task ApplyToSqlAsync_WithTargetModel_ShouldApplyMigrationSuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create target model
            var targetModel = new DatabaseModel();
            var userTable = new TableModel
            {
                Name = "User",
                Fields = new List<FieldModel>
                {
                    new() { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                    new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false },
                    new() { Name = "Email", Type = "nvarchar", Precision = 256, IsNullable = true }
                }
            };
            targetModel.Tables.Add("User", userTable);

            var shift = new Shift { Logger = _logger };

            // Act
            await shift.ApplyToSqlAsync(targetModel, connectionString);

            // Assert - Verify table was created
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var checkTableQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'User'";
            await using var command = new SqlCommand(checkTableQuery, connection);
            var tableCount = (int)await command.ExecuteScalarAsync()!;
            
            tableCount.Should().Be(1, "User table should be created");
            
            // Verify columns exist
            var checkColumnsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'User'";
            await using var columnsCmd = new SqlCommand(checkColumnsQuery, connection);
            var columnCount = (int)await columnsCmd.ExecuteScalarAsync()!;
            
            columnCount.Should().Be(3, "User table should have 3 columns");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that Shift handles already up-to-date databases gracefully.
    /// </summary>
    [Fact]
    public async Task ApplyToSqlAsync_WithUpToDateDatabase_ShouldReportNoChanges()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create target model
            var targetModel = new DatabaseModel();
            var userTable = new TableModel
            {
                Name = "User",
                Fields = new List<FieldModel>
                {
                    new() { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                    new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false }
                }
            };
            targetModel.Tables.Add("User", userTable);

            var shift = new Shift { Logger = _logger };

            // First apply to create the table
            await shift.ApplyToSqlAsync(targetModel, connectionString);
            
            // Second apply should be up-to-date
            await shift.ApplyToSqlAsync(targetModel, connectionString);

            // Assert - Should complete without errors
            // The logging will show "Already up-to date" message
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that Shift can apply migrations with foreign key relationships.
    /// </summary>
    [Fact]
    public async Task ApplyToSqlAsync_WithForeignKeyRelationships_ShouldCreateTablesAndConstraints()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create target model with foreign key relationship
            var targetModel = new DatabaseModel();
            
            var userTable = new TableModel
            {
                Name = "User",
                Fields = new List<FieldModel>
                {
                    new() { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                    new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false }
                }
            };
            targetModel.Tables.Add("User", userTable);
            
            var orderTable = new TableModel
            {
                Name = "Order",
                Fields = new List<FieldModel>
                {
                    new() { Name = "OrderID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                    new() { Name = "UserID", Type = "int", IsNullable = false }
                },
                ForeignKeys = new List<ForeignKeyModel>
                {
                    new()
                    {
                        ColumnName = "UserID",
                        TargetTable = "User",
                        TargetColumnName = "UserID",
                        RelationshipType = RelationshipType.OneToMany
                    }
                }
            };
            targetModel.Tables.Add("Order", orderTable);

            var shift = new Shift { Logger = _logger };

            // Act
            await shift.ApplyToSqlAsync(targetModel, connectionString);

            // Assert - Verify both tables and foreign key were created
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            // Check tables exist
            var checkTablesQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('User', 'Order')";
            await using var tablesCmd = new SqlCommand(checkTablesQuery, connection);
            var tableCount = (int)await tablesCmd.ExecuteScalarAsync()!;
            tableCount.Should().Be(2, "Both User and Order tables should exist");
            
            // Check foreign key exists
            var checkFkQuery = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                WHERE kcu.TABLE_NAME = 'Order' AND kcu.COLUMN_NAME = 'UserID'";
            
            await using var fkCmd = new SqlCommand(checkFkQuery, connection);
            var fkCount = (int)await fkCmd.ExecuteScalarAsync()!;
            fkCount.Should().Be(1, "Foreign key constraint should exist");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Tests that Shift handles invalid connection strings gracefully.
    /// </summary>
    [Fact]
    public async Task LoadFromSqlAsync_WithInvalidConnectionString_ShouldThrowException()
    {
        // Arrange
        var invalidConnectionString = "Server=InvalidServer;Database=NonExistent;Trusted_Connection=true;";
        var shift = new Shift { Logger = _logger };

        // Act & Assert
        await Assert.ThrowsAsync<SqlException>(() => shift.LoadFromSqlAsync(invalidConnectionString));
    }

    /// <summary>
    /// Tests that Shift handles null or empty parameters gracefully.
    /// </summary>
    [Fact]
    public async Task LoadFromAssembliesAsync_WithNullAssemblies_ShouldThrowException()
    {
        // Arrange
        var shift = new Shift { Logger = _logger };

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => shift.LoadFromAssembliesAsync(null!));
    }

    /// <summary>
    /// Tests that Shift handles empty assembly collections gracefully.
    /// </summary>
    [Fact]
    public async Task LoadFromAssembliesAsync_WithEmptyCollection_ShouldReturnEmptyModel()
    {
        // Arrange
        var emptyAssemblies = new Assembly[0];
        var shift = new Shift { Logger = _logger };

        // Act
        var result = await shift.LoadFromAssembliesAsync(emptyAssemblies);

        // Assert
        result.Should().NotBeNull();
        result.Tables.Should().BeEmpty("Should return empty model for empty assembly collection");
        result.Mixins.Should().BeEmpty("Should return empty model for empty assembly collection");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests the complete workflow: create target model, apply to database, verify with SQL loading.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_ModelToDatabase_ShouldWorkEndToEnd()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create target model
            var targetModel = new DatabaseModel();
            var userTable = new TableModel
            {
                Name = "User",
                Fields = new List<FieldModel>
                {
                    new() { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                    new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false }
                }
            };
            targetModel.Tables.Add("User", userTable);

            var shift = new Shift { Logger = _logger };

            // Step 1: Apply to database
            await shift.ApplyToSqlAsync(targetModel, connectionString);

            // Step 2: Load from database and verify
            var actualModel = await shift.LoadFromSqlAsync(connectionString);
            actualModel.Should().NotBeNull();
            actualModel.Tables.Should().NotBeEmpty();
            
            // Verify that the table was created
            actualModel.Tables.Keys.Should().Contain("User");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    #endregion
}
