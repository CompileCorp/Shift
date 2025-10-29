using Compile.Shift.Helpers;
using Compile.Shift.Model;
using FluentAssertions;

namespace Compile.Shift.Tests.UnitTests;

/// <summary>
/// Unit tests for IndexFieldResolver utility class.
/// Tests the logic for resolving index field names from model names to actual column names.
/// </summary>
public class IndexFieldResolverTests
{
    /// <summary>
    /// Tests that when a null table is provided, the original field names are returned unchanged.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithNullTable_ReturnsOriginalFields()
    {
        // Arrange
        var fields = new List<string> { "Email", "ClientStatus" };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, null);

        // Assert
        result.Should().BeEquivalentTo(fields);
    }

    /// <summary>
    /// Tests that when a table with no foreign keys is provided, the original field names are returned unchanged.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithEmptyTable_ReturnsOriginalFields()
    {
        // Arrange
        var fields = new List<string> { "Email", "ClientStatus" };
        var table = new TableModel { Name = "Client" };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEquivalentTo(fields);
    }

    /// <summary>
    /// Tests that when a table with an empty foreign keys collection is provided, the original field names are returned unchanged.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithNoForeignKeys_ReturnsOriginalFields()
    {
        // Arrange
        var fields = new List<string> { "Email", "ClientStatus" };
        var table = new TableModel 
        { 
            Name = "Client",
            ForeignKeys = new List<ForeignKeyModel>()
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEquivalentTo(fields);
    }

    /// <summary>
    /// Tests that when field names match foreign key target table names, they are correctly resolved to their corresponding column names.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithMatchingForeignKeys_ResolvesModelNamesToColumnNames()
    {
        // Arrange
        var fields = new List<string> { "Email", "ClientStatus", "ClientType" };
        var table = new TableModel
        {
            Name = "Client",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "ClientStatusID", TargetTable = "ClientStatus", TargetColumnName = "ClientStatusID" },
                new() { ColumnName = "ClientTypeID", TargetTable = "ClientType", TargetColumnName = "ClientTypeID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEquivalentTo(new List<string> { "Email", "ClientStatusID", "ClientTypeID" });
    }

    /// <summary>
    /// Tests that when only some field names match foreign key target table names, only the matching ones are resolved while others remain unchanged.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithPartialMatchingForeignKeys_ResolvesOnlyMatchingFields()
    {
        // Arrange
        var fields = new List<string> { "Email", "ClientStatus", "NonExistentModel" };
        var table = new TableModel
        {
            Name = "Client",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "ClientStatusID", TargetTable = "ClientStatus", TargetColumnName = "ClientStatusID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEquivalentTo(new List<string> { "Email", "ClientStatusID", "NonExistentModel" });
    }

    /// <summary>
    /// Tests that field name matching is case-insensitive, correctly resolving model names regardless of case differences.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithCaseInsensitiveMatching_ResolvesCorrectly()
    {
        // Arrange
        var fields = new List<string> { "clientstatus", "CLIENTTYPE" };
        var table = new TableModel
        {
            Name = "Client",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "ClientStatusID", TargetTable = "ClientStatus", TargetColumnName = "ClientStatusID" },
                new() { ColumnName = "ClientTypeID", TargetTable = "ClientType", TargetColumnName = "ClientTypeID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEquivalentTo(new List<string> { "ClientStatusID", "ClientTypeID" });
    }

    /// <summary>
    /// Tests that when an empty field list is provided, an empty list is returned.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithEmptyFields_ReturnsEmptyList()
    {
        // Arrange
        var fields = new List<string>();
        var table = new TableModel
        {
            Name = "Client",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "ClientStatusID", TargetTable = "ClientStatus", TargetColumnName = "ClientStatusID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that field resolution works correctly with complex foreign key column names that don't follow simple naming conventions.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithComplexForeignKeyNames_ResolvesCorrectly()
    {
        // Arrange
        var fields = new List<string> { "User", "Order", "Product" };
        var table = new TableModel
        {
            Name = "OrderItem",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "CreatedByUserID", TargetTable = "User", TargetColumnName = "UserID" },
                new() { ColumnName = "OrderID", TargetTable = "Order", TargetColumnName = "OrderID" },
                new() { ColumnName = "ProductID", TargetTable = "Product", TargetColumnName = "ProductID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEquivalentTo(new List<string> { "CreatedByUserID", "OrderID", "ProductID" });
    }

    /// <summary>
    /// Tests that when multiple foreign keys have the same target table name, the last one in the collection is used for resolution.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithDuplicateForeignKeys_UsesLastMatch()
    {
        // Arrange
        var fields = new List<string> { "User" };
        var table = new TableModel
        {
            Name = "Task",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "AssignedUserID", TargetTable = "User", TargetColumnName = "UserID" },
                new() { ColumnName = "CreatedByUserID", TargetTable = "User", TargetColumnName = "UserID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        // When there are duplicate foreign keys with the same TargetTable, 
        // the dictionary will use the last one added (CreatedByUserID)
        result.Should().BeEquivalentTo(new List<string> { "CreatedByUserID" });
    }

    /// <summary>
    /// Tests that field resolution works correctly with non-standard foreign key column names that don't follow the simple "ModelNameID" pattern.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_WithNonStandardForeignKeyNames_HandlesCorrectly()
    {
        // Arrange
        var fields = new List<string> { "Address", "Contact" };
        var table = new TableModel
        {
            Name = "Client",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "PostalAddressID", TargetTable = "Address", TargetColumnName = "AddressID" },
                new() { ColumnName = "PrimaryContactID", TargetTable = "Contact", TargetColumnName = "ContactID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().BeEquivalentTo(new List<string> { "PostalAddressID", "PrimaryContactID" });
    }

    /// <summary>
    /// Tests that the original order of field names is preserved in the resolved result, with only matching fields being replaced.
    /// </summary>
    [Fact]
    public void ResolveIndexFieldNames_PreservesOriginalOrder()
    {
        // Arrange
        var fields = new List<string> { "ClientType", "Email", "ClientStatus", "CreatedBy" };
        var table = new TableModel
        {
            Name = "Client",
            ForeignKeys = new List<ForeignKeyModel>
            {
                new() { ColumnName = "ClientStatusID", TargetTable = "ClientStatus", TargetColumnName = "ClientStatusID" },
                new() { ColumnName = "ClientTypeID", TargetTable = "ClientType", TargetColumnName = "ClientTypeID" },
                new() { ColumnName = "CreatedByUserID", TargetTable = "CreatedBy", TargetColumnName = "UserID" }
            }
        };

        // Act
        var result = IndexFieldResolver.ResolveIndexFieldNames(fields, table);

        // Assert
        result.Should().Equal("ClientTypeID", "Email", "ClientStatusID", "CreatedByUserID");
    }
}
