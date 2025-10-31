using Compile.Shift.Model;
using Compile.Shift.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Integration;

[Collection("SqlServer")]
public class SqlServerLoaderTests
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ILogger<SqlServerLoader> _logger;

    public SqlServerLoaderTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SqlServerLoader>();
    }

    #region Schema Loading Tests

    /// <summary>
    /// Tests that SqlServerLoader can load tables from a database with test data.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadTablesFromDatabase()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            result.Should().NotBeNull();
            result.Tables.Should().NotBeEmpty();
            result.Tables.Keys.Should().Contain("TestUser");
            result.Tables.Keys.Should().Contain("TestProduct");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    /// <summary>
    /// Tests that SqlServerLoader can load tables from a specific schema.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_WithSchema_ShouldLoadTablesFromSpecificSchema()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync("dbo");

            // Assert
            result.Should().NotBeNull();
            result.Tables.Should().NotBeEmpty();
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    /// <summary>
    /// Tests that SqlServerLoader returns an empty model when loading from an empty database.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_EmptyDatabase_ShouldReturnEmptyModel()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Don't create any tables - keep database empty
            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            result.Should().NotBeNull();
            result.Tables.Should().BeEmpty();
            result.Mixins.Should().BeEmpty();
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    #endregion

    #region Column Loading Tests

    /// <summary>
    /// Tests that SqlServerLoader correctly loads columns with their SQL Server types.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadColumnsWithCorrectTypes()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            var userTable = result.Tables["TestUser"];
            userTable.Should().NotBeNull();

            // Check for specific columns
            userTable.Fields.Should().Contain(f => f.Name == "UserID" && f.Type == "int");
            userTable.Fields.Should().Contain(f => f.Name == "Username" && f.Type == "nvarchar");
            userTable.Fields.Should().Contain(f => f.Name == "Email" && f.Type == "nvarchar");
            userTable.Fields.Should().Contain(f => f.Name == "IsActive" && f.Type == "bit");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    /// <summary>
    /// Tests that SqlServerLoader correctly identifies nullable columns.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadNullableColumns()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            var productTable = result.Tables["TestProduct"];
            productTable.Should().NotBeNull();

            // Check nullable columns
            var descriptionField = productTable.Fields.FirstOrDefault(f => f.Name == "Description");
            descriptionField.Should().NotBeNull();
            descriptionField!.IsNullable.Should().BeTrue();

            var priceField = productTable.Fields.FirstOrDefault(f => f.Name == "Price");
            priceField.Should().NotBeNull();
            priceField!.IsNullable.Should().BeTrue();
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    /// <summary>
    /// Tests that SqlServerLoader correctly loads precision and scale for decimal and string types.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadPrecisionAndScale()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            var productTable = result.Tables["TestProduct"];
            productTable.Should().NotBeNull();

            // Check precision and scale for decimal fields
            var priceField = productTable.Fields.FirstOrDefault(f => f.Name == "Price");
            priceField.Should().NotBeNull();
            priceField!.Type.Should().Be("decimal");
            priceField.Precision.Should().Be(18);
            priceField.Scale.Should().Be(2);

            // Check precision for string fields
            var nameField = productTable.Fields.FirstOrDefault(f => f.Name == "Name");
            nameField.Should().NotBeNull();
            nameField!.Type.Should().Be("nvarchar");
            nameField.Precision.Should().Be(200);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    #endregion

    #region Foreign Key Loading Tests

    /// <summary>
    /// Tests that SqlServerLoader correctly loads foreign key relationships.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadForeignKeys()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            var orderTable = result.Tables["TestOrder"];
            orderTable.Should().NotBeNull();
            orderTable.ForeignKeys.Should().NotBeEmpty();

            // Check for foreign key to User table
            orderTable.ForeignKeys.Should().Contain(fk =>
                fk.TargetTable == "TestUser" &&
                fk.ColumnName == "UserID");
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    /// <summary>
    /// Tests that SqlServerLoader correctly loads foreign key properties and relationships.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadForeignKeyProperties()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            var orderTable = result.Tables["TestOrder"];
            var userForeignKey = orderTable.ForeignKeys.FirstOrDefault(fk => fk.TargetTable == "TestUser");

            userForeignKey.Should().NotBeNull();
            userForeignKey!.ColumnName.Should().Be("UserID");
            userForeignKey.TargetTable.Should().Be("TestUser");
            // The target column name should match the primary key of the target table
            userForeignKey.TargetColumnName.Should().NotBeNullOrEmpty();
            // SqlServerLoader determines relationship type based on foreign key constraints
            userForeignKey.RelationshipType.Should().Be(RelationshipType.OneToOne);

            await Verify(result);
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    #endregion

    #region Index Loading Tests

    /// <summary>
    /// Tests that SqlServerLoader correctly loads indexes from the database.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadIndexes()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            var userTable = result.Tables["TestUser"];
            userTable.Should().NotBeNull();

            // Should have indexes (excluding primary key)
            userTable.Indexes.Should().NotBeEmpty();
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    /// <summary>
    /// Tests that SqlServerLoader correctly loads unique indexes from the database.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadUniqueIndexes()
    {
        // Arrange
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(_fixture.ConnectionStringMaster, dbName);

        try
        {
            // Create test tables
            await CreateTestTablesAsync(connectionString);

            var loader = new SqlServerLoader(connectionString) { Logger = _logger };

            // Act
            var result = await loader.LoadDatabaseAsync();

            // Assert
            var userTable = result.Tables["TestUser"];
            userTable.Should().NotBeNull();

            // Check for unique index on Email
            var emailIndex = userTable.Indexes.FirstOrDefault(i => i.Fields.Contains("Email"));
            emailIndex.Should().NotBeNull();
            emailIndex!.IsUnique.Should().BeTrue();
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Tests that SqlServerLoader throws an exception when given an invalid connection string.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_InvalidConnectionString_ShouldThrowException()
    {
        // Arrange
        var invalidConnectionString = "Server=InvalidServer;Database=NonExistentDb;Trusted_Connection=true;";
        var loader = new SqlServerLoader(invalidConnectionString) { Logger = _logger };

        // Act & Assert
        await Assert.ThrowsAsync<SqlException>(() => loader.LoadDatabaseAsync());
    }

    /// <summary>
    /// Tests that SqlServerLoader throws an exception when connecting to a non-existent database.
    /// </summary>
    [Fact]
    public async Task LoadDatabaseAsync_NonExistentDatabase_ShouldThrowException()
    {
        // Arrange
        var invalidConnectionString = "Server=InvalidServer;Database=NonExistentDatabase;Trusted_Connection=true;TrustServerCertificate=true;";
        var loader = new SqlServerLoader(invalidConnectionString) { Logger = _logger };

        // Act & Assert
        await Assert.ThrowsAsync<SqlException>(() => loader.LoadDatabaseAsync());
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates test tables with various data types and constraints for testing SqlServerLoader.
    /// </summary>
    private async Task CreateTestTablesAsync(string connectionString)
    {
        var createTablesScript = @"
            CREATE TABLE TestUser (
                UserID int IDENTITY(1,1) PRIMARY KEY,
                Username nvarchar(100) NOT NULL,
                Email nvarchar(256) NOT NULL,
                IsActive bit NOT NULL DEFAULT 1,
                CreatedDate datetime2 NULL,
                LastLogin datetime2 NULL
            );

            CREATE TABLE TestProduct (
                ProductID int IDENTITY(1,1) PRIMARY KEY,
                Name nvarchar(200) NOT NULL,
                Description nvarchar(1000) NULL,
                Price decimal(18,2) NULL,
                CategoryID int NULL,
                IsActive bit NOT NULL DEFAULT 1
            );

            CREATE TABLE TestOrder (
                OrderID int IDENTITY(1,1) PRIMARY KEY,
                UserID int NOT NULL,
                ProductID int NOT NULL,
                OrderDate datetime2 NOT NULL,
                Quantity int NOT NULL DEFAULT 1,
                TotalAmount decimal(18,2) NULL,
                FOREIGN KEY (UserID) REFERENCES TestUser(UserID),
                FOREIGN KEY (ProductID) REFERENCES TestProduct(ProductID)
            );

            -- Create indexes
            CREATE UNIQUE INDEX IX_TestUser_Email ON TestUser(Email);
            CREATE INDEX IX_TestUser_Username ON TestUser(Username);
            CREATE INDEX IX_TestProduct_CategoryID ON TestProduct(CategoryID);
            CREATE INDEX IX_TestOrder_UserID ON TestOrder(UserID);
            CREATE INDEX IX_TestOrder_OrderDate ON TestOrder(OrderDate);
        ";

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(createTablesScript, connection);
        await command.ExecuteNonQueryAsync();
    }

    #endregion
}