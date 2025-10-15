using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Tests;

/// <summary>
/// Unit tests for SqlMigrationPlanRunner class.
/// Tests SQL generation and execution logic for database migrations.
/// Uses Docker containers for realistic database testing.
/// </summary>
[Collection("SqlServer")]
public class SqlMigrationPlanRunnerTests
{
    private readonly ILogger<SqlMigrationPlanRunner> _logger;
    private readonly SqlServerContainerFixture _containerFixture;

    public SqlMigrationPlanRunnerTests(SqlServerContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SqlMigrationPlanRunner>();
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner executes empty migration plans successfully with Docker database.
    /// This establishes the pattern for testing SqlMigrationPlanRunner with real database connections.
    /// </summary>
    [Fact]
    public async Task Run_WithEmptyMigrationPlan_ShouldCompleteWithoutErrors()
    {
        // Arrange
        var plan = new MigrationPlan(); // Empty plan
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        // Create test database
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Empty migration plan should complete without failures");
            
            // This establishes the pattern for testing SqlMigrationPlanRunner with Docker:
            // 1. Use SqlServerContainerFixture for database connection
            // 2. Create unique test databases per test
            // 3. Test actual SQL execution with real database
            // 4. Clean up databases after each test
        }
        finally
        {
            // Clean up test database
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner can create tables with various field types.
    /// </summary>
    [Fact]
    public async Task Run_WithCreateTableStep_ShouldCreateTableSuccessfully()
    {
        // Arrange
        var plan = new MigrationPlan();
        var fields = new List<FieldModel>
        {
            new() { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
            new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false },
            new() { Name = "Email", Type = "nvarchar", Precision = 256, IsNullable = true },
            new() { Name = "IsActive", Type = "bit", IsNullable = false },
            new() { Name = "CreatedDate", Type = "datetime2", IsNullable = false }
        };

        plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.CreateTable,
            TableName = "TestUser",
            Fields = fields
        });

        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Table creation should complete without failures");
            
            // Verify table was created by checking if it exists
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var checkTableQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TestUser'";
            await using var command = new SqlCommand(checkTableQuery, connection);
            var tableCount = (int)await command.ExecuteScalarAsync();
            
            tableCount.Should().Be(1, "TestUser table should exist after creation");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner can add columns to existing tables.
    /// </summary>
    [Fact]
    public async Task Run_WithAddColumnStep_ShouldAddColumnSuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create initial table
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestUser (UserID int IDENTITY(1,1) PRIMARY KEY)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan to add column
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AddColumn,
                TableName = "TestUser",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false }
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Column addition should complete without failures");
            
            // Verify column was added
            var checkColumnQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TestUser' AND COLUMN_NAME = 'Username'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var columnCount = (int)await command.ExecuteScalarAsync();
            
            columnCount.Should().Be(1, "Username column should exist after addition");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner can add foreign key constraints.
    /// </summary>
    [Fact]
    public async Task Run_WithAddForeignKeyStep_ShouldAddForeignKeySuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create tables
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            await using var createUserTableCmd = new SqlCommand("CREATE TABLE [User] (UserID int IDENTITY(1,1) PRIMARY KEY, Username nvarchar(100) NOT NULL)", connection);
            await createUserTableCmd.ExecuteNonQueryAsync();
            
            await using var createOrderTableCmd = new SqlCommand("CREATE TABLE [Order] (OrderID int IDENTITY(1,1) PRIMARY KEY, UserID int NOT NULL)", connection);
            await createOrderTableCmd.ExecuteNonQueryAsync();

            // Create migration plan to add foreign key
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AddForeignKey,
                TableName = "Order",
                ForeignKey = new ForeignKeyModel
                {
                    ColumnName = "UserID",
                    TargetTable = "User",
                    TargetColumnName = "UserID",
                    RelationshipType = RelationshipType.OneToMany
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Foreign key addition should complete without failures");
            
            // Verify foreign key was added
            var checkFkQuery = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                WHERE kcu.TABLE_NAME = 'Order' AND kcu.COLUMN_NAME = 'UserID'";
            
            await using var command = new SqlCommand(checkFkQuery, connection);
            var fkCount = (int)await command.ExecuteScalarAsync();
            
            fkCount.Should().Be(1, "Foreign key constraint should exist after addition");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner handles multiple steps in sequence.
    /// </summary>
    [Fact]
    public async Task Run_WithMultipleSteps_ShouldExecuteAllStepsSuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            var plan = new MigrationPlan();
            
            // Step 1: Create User table
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.CreateTable,
                TableName = "User",
                Fields = new List<FieldModel>
                {
                    new() { Name = "UserID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                    new() { Name = "Username", Type = "nvarchar", Precision = 100, IsNullable = false }
                }
            });

            // Step 2: Create Order table
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.CreateTable,
                TableName = "Order",
                Fields = new List<FieldModel>
                {
                    new() { Name = "OrderID", Type = "int", IsPrimaryKey = true, IsIdentity = true },
                    new() { Name = "UserID", Type = "int", IsNullable = false }
                }
            });

            // Step 3: Add foreign key
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AddForeignKey,
                TableName = "Order",
                ForeignKey = new ForeignKeyModel
                {
                    ColumnName = "UserID",
                    TargetTable = "User",
                    TargetColumnName = "UserID",
                    RelationshipType = RelationshipType.OneToMany
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("All migration steps should complete without failures");
            
            // Verify all tables and constraints were created
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var checkTablesQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('User', 'Order')";
            await using var tablesCmd = new SqlCommand(checkTablesQuery, connection);
            var tableCount = (int)await tablesCmd.ExecuteScalarAsync();
            tableCount.Should().Be(2, "Both User and Order tables should exist");
            
            var checkFkQuery = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                WHERE kcu.TABLE_NAME = 'Order' AND kcu.COLUMN_NAME = 'UserID'";
            
            await using var fkCmd = new SqlCommand(checkFkQuery, connection);
            var fkCount = (int)await fkCmd.ExecuteScalarAsync();
            fkCount.Should().Be(1, "Foreign key constraint should exist");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner can alter column types and precision safely.
    /// </summary>
    [Fact]
    public async Task Run_WithAlterColumnStep_ShouldAlterColumnSuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create initial table with smaller precision
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestUser (UserID int IDENTITY(1,1) PRIMARY KEY, Username nvarchar(50) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter column (increase precision - safe)
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestUser",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Username", Type = "nvarchar", Precision = 200, IsNullable = false }
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Column alteration should complete without failures");
            
            // Verify column was altered by checking the new precision
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestUser' AND COLUMN_NAME = 'Username'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();
            
            precision.Should().Be(200, "Username column should have precision 200 after alteration");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner can alter decimal precision and scale safely.
    /// </summary>
    [Fact]
    public async Task Run_WithAlterDecimalColumn_ShouldAlterDecimalSuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create initial table with smaller decimal precision
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestProduct (ProductID int IDENTITY(1,1) PRIMARY KEY, Price decimal(10,2) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter decimal column (increase precision - safe)
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestProduct",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Price", Type = "decimal", Precision = 18, Scale = 4, IsNullable = false }
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Decimal column alteration should complete without failures");
            
            // Verify decimal column was altered by checking precision and scale
            var checkColumnQuery = @"
                SELECT NUMERIC_PRECISION, NUMERIC_SCALE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestProduct' AND COLUMN_NAME = 'Price'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            
            var precision = Convert.ToInt32(reader.GetValue(0));
            var scale = Convert.ToInt32(reader.GetValue(1));
            
            precision.Should().Be(18, "Price column should have precision 18 after alteration");
            scale.Should().Be(4, "Price column should have scale 4 after alteration");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner skips unsafe column alterations that would cause data loss.
    /// </summary>
    [Fact]
    public async Task Run_WithUnsafeAlterColumn_ShouldSkipAndLogWarning()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create table and insert data that would be truncated
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestUser (UserID int IDENTITY(1,1) PRIMARY KEY, Username nvarchar(200) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();
            
            // Insert data that exceeds the target precision
            await using var insertCmd = new SqlCommand("INSERT INTO TestUser (Username) VALUES ('This is a very long username that exceeds the target precision of 50 characters and would cause data loss')", connection);
            await insertCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter column (decrease precision - unsafe)
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestUser",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Username", Type = "nvarchar", Precision = 50, IsNullable = false }
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Unsafe alteration should be skipped without failures");
            
            // Verify column was NOT altered (still has original precision)
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestUser' AND COLUMN_NAME = 'Username'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();
            
            precision.Should().Be(200, "Username column should retain original precision 200 since alteration was skipped");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner skips unsafe decimal alterations that would cause data loss.
    /// </summary>
    [Fact]
    public async Task Run_WithUnsafeDecimalAlter_ShouldSkipAndLogWarning()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create table and insert data that would be truncated
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestProduct (ProductID int IDENTITY(1,1) PRIMARY KEY, Price decimal(18,4) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();
            
            // Insert data that exceeds the target precision
            await using var insertCmd = new SqlCommand("INSERT INTO TestProduct (Price) VALUES (1234567890.1234)", connection);
            await insertCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter decimal column (decrease precision - unsafe)
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestProduct",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Price", Type = "decimal", Precision = 10, Scale = 2, IsNullable = false }
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Unsafe decimal alteration should be skipped without failures");
            
            // Verify decimal column was NOT altered (still has original precision/scale)
            var checkColumnQuery = @"
                SELECT NUMERIC_PRECISION, NUMERIC_SCALE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestProduct' AND COLUMN_NAME = 'Price'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            
            var precision = Convert.ToInt32(reader.GetValue(0));
            var scale = Convert.ToInt32(reader.GetValue(1));
            
            precision.Should().Be(18, "Price column should retain original precision 18 since alteration was skipped");
            scale.Should().Be(4, "Price column should retain original scale 4 since alteration was skipped");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner handles binary/varbinary precision changes safely.
    /// </summary>
    [Fact]
    public async Task Run_WithAlterBinaryColumn_ShouldAlterBinarySuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create initial table with smaller binary precision
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestData (DataID int IDENTITY(1,1) PRIMARY KEY, BinaryData varbinary(50) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter binary column (increase precision - safe)
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestData",
                Fields = new List<FieldModel>
                {
                    new() { Name = "BinaryData", Type = "varbinary", Precision = 200, IsNullable = false }
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Binary column alteration should complete without failures");
            
            // Verify binary column was altered by checking the new precision
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestData' AND COLUMN_NAME = 'BinaryData'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();
            
            precision.Should().Be(200, "BinaryData column should have precision 200 after alteration");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner handles char/nchar precision changes safely.
    /// </summary>
    [Fact]
    public async Task Run_WithAlterCharColumn_ShouldAlterCharSuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);
        
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        
        try
        {
            // Create initial table with smaller char precision
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestCode (CodeID int IDENTITY(1,1) PRIMARY KEY, StatusCode char(5) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter char column (increase precision - safe)
            var plan = new MigrationPlan();
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestCode",
                Fields = new List<FieldModel>
                {
                    new() { Name = "StatusCode", Type = "char", Precision = 10, IsNullable = false }
                }
            });

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Char column alteration should complete without failures");
            
            // Verify char column was altered by checking the new precision
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestCode' AND COLUMN_NAME = 'StatusCode'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();
            
            precision.Should().Be(10, "StatusCode column should have precision 10 after alteration");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }
}
