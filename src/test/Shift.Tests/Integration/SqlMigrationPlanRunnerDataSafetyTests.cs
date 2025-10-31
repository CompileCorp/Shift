using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Integration;

/// <summary>
/// Unit tests for SqlMigrationPlanRunner data safety validation.
/// Tests the IsAlterColumnPotentiallyUnsafe method to ensure data loss prevention works correctly.
/// Uses Docker containers for realistic database testing.
/// </summary>
[Collection("SqlServer")]
public class SqlMigrationPlanRunnerDataSafetyTests
{
    private readonly ILogger<SqlMigrationPlanRunner> _logger;
    private readonly SqlServerContainerFixture _containerFixture;

    public SqlMigrationPlanRunnerDataSafetyTests(SqlServerContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SqlMigrationPlanRunner>();
    }

    /// <summary>
    /// Tests that data safety check correctly identifies unsafe string truncation scenarios.
    /// </summary>
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithStringTruncation_ShouldReturnTrue()
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
            var unsafeStep = new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestUser",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Username", Type = "nvarchar", Precision = 50, IsNullable = false }
                }
            };
            plan.Steps.Add(unsafeStep);

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without failures (unsafe step skipped)");

            // Verify the column was NOT altered (still has original precision)
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestUser' AND COLUMN_NAME = 'Username'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();

            precision.Should().Be(200, "Username column should retain original precision 200 since unsafe alteration was skipped");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that data safety check correctly identifies unsafe decimal precision reduction scenarios.
    /// </summary>
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithDecimalTruncation_ShouldReturnTrue()
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
            var unsafeStep = new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestProduct",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Price", Type = "decimal", Precision = 10, Scale = 2, IsNullable = false }
                }
            };
            plan.Steps.Add(unsafeStep);

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without failures (unsafe step skipped)");

            // Verify the decimal column was NOT altered (still has original precision/scale)
            var checkColumnQuery = @"
                SELECT NUMERIC_PRECISION, NUMERIC_SCALE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestProduct' AND COLUMN_NAME = 'Price'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            var precision = Convert.ToInt32(reader.GetValue(0));
            var scale = Convert.ToInt32(reader.GetValue(1));

            precision.Should().Be(18, "Price column should retain original precision 18 since unsafe alteration was skipped");
            scale.Should().Be(4, "Price column should retain original scale 4 since unsafe alteration was skipped");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that data safety check allows safe string precision increases.
    /// </summary>
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithSafeStringIncrease_ShouldReturnFalse()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table with existing data
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestUser (UserID int IDENTITY(1,1) PRIMARY KEY, Username nvarchar(50) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Insert data that fits within the new precision
            await using var insertCmd = new SqlCommand("INSERT INTO TestUser (Username) VALUES ('ShortName')", connection);
            await insertCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter column (increase precision - safe)
            var plan = new MigrationPlan();
            var safeStep = new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestUser",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Username", Type = "nvarchar", Precision = 200, IsNullable = false }
                }
            };
            plan.Steps.Add(safeStep);

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Safe migration should complete without failures");

            // Verify the column WAS altered (has new precision)
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestUser' AND COLUMN_NAME = 'Username'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();

            precision.Should().Be(200, "Username column should have new precision 200 after safe alteration");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that data safety check allows safe decimal precision increases.
    /// </summary>
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithSafeDecimalIncrease_ShouldReturnFalse()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table with existing data
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestProduct (ProductID int IDENTITY(1,1) PRIMARY KEY, Price decimal(10,2) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Insert data that fits within the new precision
            await using var insertCmd = new SqlCommand("INSERT INTO TestProduct (Price) VALUES (123.45)", connection);
            await insertCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter decimal column (increase precision - safe)
            var plan = new MigrationPlan();
            var safeStep = new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestProduct",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Price", Type = "decimal", Precision = 18, Scale = 4, IsNullable = false }
                }
            };
            plan.Steps.Add(safeStep);

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Safe decimal migration should complete without failures");

            // Verify the decimal column WAS altered (has new precision/scale)
            var checkColumnQuery = @"
                SELECT NUMERIC_PRECISION, NUMERIC_SCALE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestProduct' AND COLUMN_NAME = 'Price'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            var precision = Convert.ToInt32(reader.GetValue(0));
            var scale = Convert.ToInt32(reader.GetValue(1));

            precision.Should().Be(18, "Price column should have new precision 18 after safe alteration");
            scale.Should().Be(4, "Price column should have new scale 4 after safe alteration");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that data safety check correctly handles binary data truncation scenarios.
    /// </summary>
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithBinaryTruncation_ShouldReturnTrue()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table and insert binary data that would be truncated
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestData (DataID int IDENTITY(1,1) PRIMARY KEY, BinaryData varbinary(200) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Insert binary data that exceeds the target precision
            var longBinaryData = new byte[150]; // 150 bytes, exceeds target of 50
            for (int i = 0; i < longBinaryData.Length; i++)
            {
                longBinaryData[i] = (byte)(i % 256);
            }

            await using var insertCmd = new SqlCommand("INSERT INTO TestData (BinaryData) VALUES (@data)", connection);
            insertCmd.Parameters.AddWithValue("@data", longBinaryData);
            await insertCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter binary column (decrease precision - unsafe)
            var plan = new MigrationPlan();
            var unsafeStep = new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestData",
                Fields = new List<FieldModel>
                {
                    new() { Name = "BinaryData", Type = "varbinary", Precision = 50, IsNullable = false }
                }
            };
            plan.Steps.Add(unsafeStep);

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without failures (unsafe step skipped)");

            // Verify the binary column was NOT altered (still has original precision)
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestData' AND COLUMN_NAME = 'BinaryData'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();

            precision.Should().Be(200, "BinaryData column should retain original precision 200 since unsafe alteration was skipped");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that data safety check correctly handles char/nchar data truncation scenarios.
    /// </summary>
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithCharTruncation_ShouldReturnTrue()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create table and insert char data that would be truncated
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestCode (CodeID int IDENTITY(1,1) PRIMARY KEY, StatusCode char(20) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Insert char data that exceeds the target precision
            await using var insertCmd = new SqlCommand("INSERT INTO TestCode (StatusCode) VALUES ('LONG_STATUS_CODE')", connection);
            await insertCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter char column (decrease precision - unsafe)
            var plan = new MigrationPlan();
            var unsafeStep = new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestCode",
                Fields = new List<FieldModel>
                {
                    new() { Name = "StatusCode", Type = "char", Precision = 5, IsNullable = false }
                }
            };
            plan.Steps.Add(unsafeStep);

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without failures (unsafe step skipped)");

            // Verify the char column was NOT altered (still has original precision)
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestCode' AND COLUMN_NAME = 'StatusCode'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();

            precision.Should().Be(20, "StatusCode column should retain original precision 20 since unsafe alteration was skipped");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }

    /// <summary>
    /// Tests that data safety check allows safe operations when no data exists.
    /// </summary>
    [Fact]
    public async Task IsAlterColumnPotentiallyUnsafe_WithEmptyTable_ShouldReturnFalse()
    {
        // Arrange
        var databaseName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_containerFixture.ConnectionStringMaster, databaseName);

        await SqlServerTestHelper.CreateDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);

        try
        {
            // Create empty table
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var createTableCmd = new SqlCommand("CREATE TABLE TestUser (UserID int IDENTITY(1,1) PRIMARY KEY, Username nvarchar(200) NOT NULL)", connection);
            await createTableCmd.ExecuteNonQueryAsync();

            // Create migration plan to alter column (decrease precision - should be safe on empty table)
            var plan = new MigrationPlan();
            var step = new MigrationStep
            {
                Action = MigrationAction.AlterColumn,
                TableName = "TestUser",
                Fields = new List<FieldModel>
                {
                    new() { Name = "Username", Type = "nvarchar", Precision = 50, IsNullable = false }
                }
            };
            plan.Steps.Add(step);

            var runner = new SqlMigrationPlanRunner(connectionString, plan)
            {
                Logger = _logger
            };

            // Act
            var result = runner.Run();

            // Assert
            result.Should().BeEmpty("Migration should complete without failures on empty table");

            // Verify the column WAS altered (has new precision)
            var checkColumnQuery = @"
                SELECT CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = 'TestUser' AND COLUMN_NAME = 'Username'";
            await using var command = new SqlCommand(checkColumnQuery, connection);
            var precision = (int)await command.ExecuteScalarAsync();

            precision.Should().Be(50, "Username column should have new precision 50 after alteration on empty table");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_containerFixture.ConnectionStringMaster, databaseName);
        }
    }
}