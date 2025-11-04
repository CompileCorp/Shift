using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Compile.Shift.Integration;

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
    public async Task Run_WithCreateTable_ShouldCreateTableSuccessfully()
    {
        // Arrange
        var plan = MigrationPlanBuilder.Create()
            .WithCreateTable("TestUser", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false))
                .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(true))
                .WithField("IsActive", "bit", f => f.Nullable(false))
                .WithField("CreatedDate", "datetime2", f => f.Nullable(false)))
            .Build();

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
            var plan = MigrationPlanBuilder.Create()
                .WithAddColumn("TestUser", "Username", "nvarchar", f => f.Precision(100).Nullable(false))
                .Build();

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
    public async Task Run_WithAddForeignKey_ShouldAddForeignKeySuccessfully()
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
            var plan = MigrationPlanBuilder.Create()
                .WithAddForeignKey("Order", "UserID", "User", "UserID", RelationshipType.OneToMany)
                .Build();

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

            // Verify index was created for the FK column
            var checkIndexQuery = @"
                SELECT COUNT(*) 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'Order' AND i.name = 'IX_Order_UserID' AND i.is_unique = 0";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexCount = (int)await indexCmd.ExecuteScalarAsync();

            indexCount.Should().Be(1, "Non-clustered index should exist for FK column UserID");
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
            var plan = MigrationPlanBuilder.Create()
                // Step 1: Create User table
                .WithCreateTable("User", user => user
                    .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                    .WithField("Username", "nvarchar", f => f.Precision(100).Nullable(false)))
                // Step 2: Create Order table
                .WithCreateTable("Order", order => order
                    .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                    .WithField("UserID", "int", f => f.Nullable(false)))
                // Step 3: Add foreign key
                .WithAddForeignKey("Order", "UserID", "User", "UserID", RelationshipType.OneToMany)
                .Build();

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

            // Verify index was created for the FK column
            var checkIndexQuery = @"
                SELECT COUNT(*) 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'Order' AND i.name = 'IX_Order_UserID' AND i.is_unique = 0";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexCount = (int)await indexCmd.ExecuteScalarAsync();

            indexCount.Should().Be(1, "Non-clustered index should exist for FK column UserID");
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
            var plan = MigrationPlanBuilder.Create()
                .WithAlterColumn("TestUser", "Username", "nvarchar", f => f.Precision(200).Nullable(false))
                .Build();

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

            //Create migration plan to alter decimal column(increase precision -safe)
            var plan = MigrationPlanBuilder.Create()
                .WithAlterColumn("TestProduct", "Price", "decimal", f => f.Precision(18, 4).Nullable(false))
                .Build();

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
            var plan = MigrationPlanBuilder.Create()
                .WithAlterColumn("TestUser", "Username", "nvarchar", f => f.Precision(50).Nullable(false))
                .Build();

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
            var plan = MigrationPlanBuilder.Create()
                .WithAlterColumn("TestProduct", "Price", "decimal", f => f.Precision(10, 2).Nullable(false))
                .Build();

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
            var plan = MigrationPlanBuilder.Create()
                .WithAlterColumn("TestData", "BinaryData", "varbinary", f => f.Precision(200).Nullable(false))
                .Build();

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
            var plan = MigrationPlanBuilder.Create()
                .WithAlterColumn("TestCode", "StatusCode", "char", f => f.Precision(10).Nullable(false))
                .Build();

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

    #region AddIndex Tests

    /// <summary>
    /// Tests that SqlMigrationPlanRunner creates non-unique indexes correctly.
    /// Verifies the SQL generation and execution for single-column indexes.
    /// </summary>
    [Fact]
    public async Task Run_WithAddIndex_ShouldCreateIndex()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE [dbo].[User] ([UserID] int IDENTITY(1,1) PRIMARY KEY, [Email] nvarchar(256) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan with AddIndex step
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("User", "Email", "Email", isUnique: false)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify index exists
            var checkIndexQuery = @"
                SELECT COUNT(*) 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name LIKE 'IX_User_%' AND i.is_unique = 0";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexCount = (int)await indexCmd.ExecuteScalarAsync();
            indexCount.Should().Be(1, "Non-unique index should exist");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner creates unique indexes correctly.
    /// Verifies the SQL generation and execution for unique indexes.
    /// </summary>
    [Fact]
    public async Task Run_WithAddUniqueIndexStep_ShouldCreateUniqueIndex()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE [dbo].[User] ([UserID] int IDENTITY(1,1) PRIMARY KEY, [Email] nvarchar(256) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan with AddIndex step
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("User", "Email", "Email", isUnique: true)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify unique index exists
            var checkIndexQuery = @"
                SELECT COUNT(*) 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name LIKE 'IX_User_%' AND i.is_unique = 1";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexCount = (int)await indexCmd.ExecuteScalarAsync();
            indexCount.Should().Be(1, "Unique index should exist");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner creates multi-column indexes correctly.
    /// Verifies the SQL generation and execution for indexes with multiple columns.
    /// </summary>
    [Fact]
    public async Task Run_WithMultiColumnIndexStep_ShouldCreateMultiColumnIndex()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE [dbo].[User] ([UserID] int IDENTITY(1,1) PRIMARY KEY, [Email] nvarchar(256) NOT NULL, [Username] nvarchar(100) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan with AddIndex step
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("User", "EmailUsername", new[] { "Email", "Username" }, isUnique: false)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify multi-column index exists
            var checkIndexQuery = @"
                SELECT COUNT(*) 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name LIKE 'IX_User_%'";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexCount = (int)await indexCmd.ExecuteScalarAsync();
            indexCount.Should().Be(1, "Multi-column index should exist");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner executes mixed migration steps in correct order.
    /// Verifies that AddIndex steps work correctly alongside other migration actions.
    /// </summary>
    [Fact]
    public async Task Run_WithMixedMigrationSteps_ShouldExecuteAddIndexInCorrectOrder()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create migration plan with multiple step types
            var plan = MigrationPlanBuilder.Create()
                // Create table step
                .WithCreateTable("User", user => user
                    .WithField("UserID", "int", f => f.PrimaryKey().Identity().Nullable(false))
                    .WithField("Email", "nvarchar", f => f.Precision(256).Nullable(false)))
                // Add column step
                .WithAddColumn("User", "Username", "nvarchar", f => f.Precision(100).Nullable(false))
                // Add index step
                .WithAddIndex("User", "Email", "Email", isUnique: true)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify table exists
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var checkTableQuery = "SELECT COUNT(*) FROM sys.tables WHERE name = 'User'";
            await using var tableCmd = new SqlCommand(checkTableQuery, connection);
            var tableCount = (int)await tableCmd.ExecuteScalarAsync();
            tableCount.Should().Be(1, "Table should exist");

            // Verify index exists
            var checkIndexQuery = @"
                SELECT COUNT(*) 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name LIKE 'IX_User_%' AND i.is_unique = 1";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexCount = (int)await indexCmd.ExecuteScalarAsync();
            indexCount.Should().Be(1, "Unique index should exist");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Verifies that model names in index definitions are normalized to FK column names:
    /// 1) Planner detects the existing index after normalization (no AddIndex step).
    /// 2) Runner SQL generation resolves model names to actual column names.
    /// </summary>
    [Fact]
    public async Task Run_WithIndexUsingModelNames_ShouldNormalizeAndResolveColumns()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        // Create test database
        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create target model with index using model names (like in DMD files)
            var targetModel = DatabaseModelBuilder.Create()
                .WithTable("Client", table => table
                    .WithField("ClientID", "int", f => f.PrimaryKey().Identity())
                    .WithField("Email", "varchar", f => f.Precision(100).Nullable(true))
                    .WithField("ClientStatusID", "int", f => f.Nullable(false))
                    .WithForeignKey("ClientStatusID", "ClientStatus", "ClientStatusID", RelationshipType.OneToMany)
                    .WithIndex("IX_Client_Email_ClientStatus", new[] { "Email", "ClientStatus" }, isUnique: false)) // Note: "ClientStatus" is model name, not column name
                .Build();

            // Create actual model with the index already existing (using actual column names)
            var actualModel = DatabaseModelBuilder.Create()
                .WithTable("Client", table => table
                    .WithField("ClientID", "int", f => f.PrimaryKey().Identity())
                    .WithField("Email", "varchar", f => f.Precision(100).Nullable(true))
                    .WithField("ClientStatusID", "int", f => f.Nullable(false))
                    .WithForeignKey("ClientStatusID", "ClientStatus", "ClientStatusID", RelationshipType.OneToMany)
                    .WithIndex("IX_Client_Email_ClientStatusID", new[] { "Email", "ClientStatusID" }, isUnique: false)) // Note: "ClientStatusID" is actual column name
                .Build();

            // Generate migration plan
            var migrationPlanner = new MigrationPlanner();
            var plan = migrationPlanner.GeneratePlan(targetModel, actualModel);

            // With normalization in MigrationPlanner, no AddIndex step should be emitted
            plan.Steps.Should().NotContain(step => step.Action == MigrationAction.AddIndex,
                "Planner should normalize model names to column names and detect existing index");

            // Test the SQL generation directly to verify column name resolution
            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Create a test step with model names to verify SQL generation
            var testStep = new MigrationStep
            {
                Action = MigrationAction.AddIndex,
                TableName = "Client",
                Index = new IndexModel
                {
                    Fields = new List<string> { "Email", "ClientStatus" }, // Model names
                    IsUnique = false,
                    Kind = IndexKind.NonClustered
                },
                Table = targetModel.Tables["Client"] // Include table model for resolution
            };

            // Use reflection to test the private GenerateIndexSql method
            var method = typeof(SqlMigrationPlanRunner).GetMethod("GenerateIndexSql", BindingFlags.NonPublic | BindingFlags.Instance);

            var sqls = (IEnumerable<string>)method!.Invoke(runner, [testStep.TableName, testStep.Index, testStep.Table])!;

            var sqlList = sqls.ToList();
            sqlList.Should().HaveCount(1, "Should generate one SQL statement");

            var sql = sqlList[0];

            sql.Should().Contain("[Email]", "Should include Email column");
            sql.Should().Contain("[ClientStatusID]", "Should resolve ClientStatus model name to ClientStatusID column name");
            sql.Should().NotContain("[ClientStatus]", "Should not use the model name ClientStatus");
            sql.Should().Contain("IF NOT EXISTS", "Should use defensive IF NOT EXISTS check");
            sql.Should().Contain("CREATE NONCLUSTERED INDEX [IX_Client_Email_ClientStatusID] ON [dbo].[Client]([Email], [ClientStatusID])", "Should contain the CREATE INDEX statement");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Verifies that alternate keys (key() in DMD files) generate indexes with the "AK_" prefix
    /// instead of the standard "IX_" prefix used for regular indexes.
    /// </summary>
    [Fact]
    public void GenerateIndexSql_WithAlternateKey_ShouldUseAKPrefix()
    {
        // Arrange
        var tableModel = DatabaseModelBuilder.Create()
            .WithTable("User", table => table
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256)))
            .Build()
            .Tables["User"];

        var alternateKeyIndex = new IndexModel
        {
            Fields = new List<string> { "Email" },
            IsUnique = true,
            IsAlternateKey = true,
            Kind = IndexKind.NonClustered
        };

        var runner = new SqlMigrationPlanRunner("Server=.;Database=Test;", new MigrationPlan())
        {
            Logger = _logger
        };

        // Use reflection to test the private GenerateIndexSql method
        var method = typeof(SqlMigrationPlanRunner).GetMethod("GenerateIndexSql", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var sqls = (IEnumerable<string>)method!.Invoke(runner, ["User", alternateKeyIndex, tableModel])!;
        var sqlList = sqls.ToList();

        // Assert
        sqlList.Should().HaveCount(1, "Should generate one SQL statement");
        var sql = sqlList[0];

        sql.Should().Contain("[AK_User_Email]", "Alternate key should use AK_ prefix");
        sql.Should().NotContain("[IX_User_Email]", "Alternate key should not use IX_ prefix");
        sql.Should().Contain("CREATE UNIQUE", "Alternate key should be unique");
        sql.Should().Contain("CREATE UNIQUE NONCLUSTERED INDEX [AK_User_Email] ON [dbo].[User]([Email])", "Should contain the correct CREATE INDEX statement with AK prefix");
    }

    /// <summary>
    /// Tests that SqlMigrationPlanRunner handles duplicate index creation gracefully.
    /// Verifies that attempting to create an index that already exists does not cause an error.
    /// </summary>
    [Fact]
    public async Task Run_WithDuplicateIndex_ShouldNotFail()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE [dbo].[User] ([UserID] int IDENTITY(1,1) PRIMARY KEY, [Email] nvarchar(256) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create the index manually first
            await using var createIndexCmd = new SqlCommand("CREATE INDEX [IX_User_Email] ON [dbo].[User]([Email])", connection);
            await createIndexCmd.ExecuteNonQueryAsync();

            // Create migration plan that tries to create the same index
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("User", "Email", "Email", isUnique: false)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act - This should not fail even though the index already exists
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors even with duplicate index");

            // Verify index still exists (should not have been affected)
            var checkIndexQuery = @"
                SELECT COUNT(*) 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name = 'IX_User_Email'";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexCount = (int)await indexCmd.ExecuteScalarAsync();
            indexCount.Should().Be(1, "Index should still exist");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    #endregion

    #region Index Name Length Tests

    /// <summary>
    /// Tests that index names with exactly 128 characters are created successfully in SQL Server.
    /// This verifies that the 128-character limit is handled correctly and SQL Server accepts names at the limit.
    /// </summary>
    [Fact]
    public async Task Run_WithIndexNameExactly128Characters_ShouldCreateIndexSuccessfully()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first with a column that will result in exactly 128 characters for the index name
            // "IX_User_" = 8 characters, so we need 120 characters for the column name
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var longColumnName = new string('A', 120);
            var createTableSql = $"CREATE TABLE [dbo].[User] ([UserID] int IDENTITY(1,1) PRIMARY KEY, [{longColumnName}] nvarchar(256) NOT NULL)";
            await using var createTableCmd = new SqlCommand(createTableSql, connection);
            await createTableCmd.ExecuteNonQueryAsync();

            var expectedIndexName = $"IX_User_{longColumnName}";
            expectedIndexName.Length.Should().Be(128, "Test setup: index name should be exactly 128 characters");

            // Create migration plan with AddIndex step (indexName parameter is ignored, but we pass it for clarity)
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("User", "TestIndex", longColumnName, isUnique: false)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify index exists with the exact name (should be unchanged since it's exactly 128 characters)
            var checkIndexQuery = @"
                SELECT i.name 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name = @indexName";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            indexCmd.Parameters.AddWithValue("@indexName", expectedIndexName);
            var actualIndexName = await indexCmd.ExecuteScalarAsync() as string;

            actualIndexName.Should().Be(expectedIndexName, "Index name should be exactly 128 characters and unchanged");
            actualIndexName!.Length.Should().Be(128, "Index name length should be exactly 128 characters");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that index names exceeding 128 characters are trimmed and hashed when created in SQL Server.
    /// This verifies that the hashing logic works correctly in a real database environment.
    /// </summary>
    [Fact]
    public async Task Run_WithIndexNameExceeding128Characters_ShouldTrimAndHashIndexName()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first with multiple columns that, when combined in an index name, exceed 128 characters
            // Use multiple shorter columns to work around SQL Server's 128-character column name limit
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Create columns with names that when joined (IX_User_Col1_Col2_Col3...) exceed 128
            // Each column name is 30 chars, so 5 columns = 150 chars in field names + 8 for "IX_User_" + 4 underscores = 162 total
            var columns = new[] { "Col1_AAAAAAAAAAAAAAAAAAAA", "Col2_BBBBBBBBBBBBBBBBBBBB", "Col3_CCCCCCCCCCCCCCCCCCCC", "Col4_DDDDDDDDDDDDDDDDDDDD", "Col5_EEEEEEEEEEEEEEEEEEEE" };
            var createTableSql = $"CREATE TABLE [dbo].[User] ([UserID] int IDENTITY(1,1) PRIMARY KEY, [{columns[0]}] nvarchar(256) NOT NULL, [{columns[1]}] nvarchar(256) NOT NULL, [{columns[2]}] nvarchar(256) NOT NULL, [{columns[3]}] nvarchar(256) NOT NULL, [{columns[4]}] nvarchar(256) NOT NULL)";
            await using var createTableCmd = new SqlCommand(createTableSql, connection);
            await createTableCmd.ExecuteNonQueryAsync();

            var originalIndexName = $"IX_User_{string.Join("_", columns)}";
            originalIndexName.Length.Should().BeGreaterThan(128, "Test setup: index name should exceed 128 characters");

            // Create migration plan with AddIndex step using all columns (indexName parameter is ignored, but we pass it for clarity)
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("User", "TestIndex", columns, isUnique: false)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify index exists with a trimmed and hashed name
            var checkIndexQuery = @"
                SELECT i.name 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name LIKE 'IX_User_%'";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var actualIndexName = await indexCmd.ExecuteScalarAsync() as string;

            actualIndexName.Should().NotBeNull("Index should exist");
            actualIndexName!.Length.Should().Be(128, "Index name should be exactly 128 characters after trimming and hashing");
            actualIndexName.Should().StartWith("IX_User_", "Index name should start with the expected prefix");
            actualIndexName.Should().MatchRegex(@"^IX_User_.+_[0-9a-f]{8}$", "Index name should end with underscore followed by 8-character hex hash");
            actualIndexName.Should().NotBe(originalIndexName, "Index name should be trimmed and hashed, not the original long name");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that two similar long index names produce different hashed names in SQL Server.
    /// This verifies that the hashing ensures uniqueness even when the trimmed prefixes are the same.
    /// </summary>
    [Fact]
    public async Task Run_WithTwoSimilarLongIndexNames_ShouldCreateDifferentHashedNames()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first with columns that will result in long index names
            // Use multiple columns to create index names that exceed 128 characters
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Create two sets of columns that, when combined in index names, exceed 128 and have similar prefixes
            // Each set uses 5 columns of 30 chars each = 150 chars in field names + 8 for "IX_User_" + 4 underscores = 162 total
            var columns1 = new[] { "Col1_AAAAAAAAAAAAAAAAAAAA", "Col2_BBBBBBBBBBBBBBBBBBBB", "Col3_CCCCCCCCCCCCCCCCCCCC", "Col4_DDDDDDDDDDDDDDDDDDDD", "Col5_EEEEEEEEEEEEEEEEEEEE" };
            var columns2 = new[] { "Col1_AAAAAAAAAAAAAAAAAAAA", "Col2_BBBBBBBBBBBBBBBBBBBB", "Col3_CCCCCCCCCCCCCCCCCCCC", "Col4_DDDDDDDDDDDDDDDDDDDD", "Col6_FFFFFFFFFFFFFFFFFFFF" }; // Last column differs

            // Create table with all columns
            var allColumns = columns1.Union(columns2).Distinct().ToList();
            var createTableSql = $"CREATE TABLE [dbo].[User] ([UserID] int IDENTITY(1,1) PRIMARY KEY, {string.Join(", ", allColumns.Select(c => $"[{c}] nvarchar(256) NOT NULL"))})";
            await using var createTableCmd = new SqlCommand(createTableSql, connection);
            await createTableCmd.ExecuteNonQueryAsync();

            var originalIndexName1 = $"IX_User_{string.Join("_", columns1)}";
            var originalIndexName2 = $"IX_User_{string.Join("_", columns2)}";

            originalIndexName1.Length.Should().BeGreaterThan(128, "Test setup: first index name should exceed 128 characters");
            originalIndexName2.Length.Should().BeGreaterThan(128, "Test setup: second index name should exceed 128 characters");

            // Create migration plan with two AddIndex steps using multiple columns (indexName parameters are ignored)
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("User", "TestIndex1", columns1, isUnique: false)
                .WithAddIndex("User", "TestIndex2", columns2, isUnique: false)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify both indexes exist with different hashed names
            var checkIndexQuery = @"
                SELECT i.name 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'User' AND i.name LIKE 'IX_User_%' AND i.name != 'PK_User'
                ORDER BY i.name";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var indexNames = new List<string>();
            await using var reader = await indexCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexNames.Add(reader.GetString(0));
            }

            indexNames.Should().HaveCount(2, "Both indexes should exist");

            var indexName1 = indexNames[0];
            var indexName2 = indexNames[1];

            // Both should be exactly 128 characters
            indexName1.Length.Should().Be(128, "First index name should be exactly 128 characters");
            indexName2.Length.Should().Be(128, "Second index name should be exactly 128 characters");

            // Both should start with the same prefix (after trimming)
            indexName1.Should().StartWith("IX_User_", "First index should start with prefix");
            indexName2.Should().StartWith("IX_User_", "Second index should start with prefix");

            // Extract the hashes (last 8 characters after underscore)
            var hash1 = indexName1.Substring(indexName1.Length - 8);
            var hash2 = indexName2.Substring(indexName2.Length - 8);

            hash1.Should().MatchRegex(@"^[0-9a-f]{8}$", "First index should have valid hex hash");
            hash2.Should().MatchRegex(@"^[0-9a-f]{8}$", "Second index should have valid hex hash");
            hash1.Should().NotBe(hash2, "Different input names should produce different hashes even with same prefix");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that index names with multiple long fields are handled correctly when they exceed 128 characters.
    /// This verifies the behavior with composite indexes that have long names.
    /// </summary>
    [Fact]
    public async Task Run_WithCompositeIndexExceeding128Characters_ShouldTrimAndHashIndexName()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table first with columns that, when combined in an index name, exceed 128 characters
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Create field names that, when combined, exceed 128 characters in the index name
            var longField1 = new string('A', 50);
            var longField2 = new string('B', 50);

            var createTableSql = $@"
                CREATE TABLE [dbo].[Order] (
                    [OrderID] int IDENTITY(1,1) PRIMARY KEY, 
                    [CustomerID] int NOT NULL,
                    [OrderDate] datetime2 NOT NULL,
                    [Status] nvarchar(50) NOT NULL,
                    [{longField1}] int NOT NULL,
                    [{longField2}] int NOT NULL
                )";
            await using var createTableCmd = new SqlCommand(createTableSql, connection);
            await createTableCmd.ExecuteNonQueryAsync();

            var fields = new[] { "CustomerID", "OrderDate", "Status", longField1, longField2 };

            // Create migration plan with AddIndex step using multiple fields
            var plan = MigrationPlanBuilder.Create()
                .WithAddIndex("Order", "CompositeIndex", fields, isUnique: false)
                .Build();

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without errors");

            // Verify index exists with a trimmed and hashed name
            var checkIndexQuery = @"
                SELECT i.name 
                FROM sys.indexes i 
                INNER JOIN sys.tables t ON i.object_id = t.object_id 
                WHERE t.name = 'Order' AND i.name LIKE 'IX_Order_%'";

            await using var indexCmd = new SqlCommand(checkIndexQuery, connection);
            var actualIndexName = await indexCmd.ExecuteScalarAsync() as string;

            actualIndexName.Should().NotBeNull("Index should exist");
            actualIndexName!.Length.Should().Be(128, "Index name should be exactly 128 characters after trimming and hashing");
            actualIndexName.Should().StartWith("IX_Order_", "Index name should start with the expected prefix");
            actualIndexName.Should().MatchRegex(@"^IX_Order_.+_[0-9a-f]{8}$", "Index name should end with underscore followed by 8-character hex hash");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    #endregion
}