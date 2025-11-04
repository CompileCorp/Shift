using Compile.Shift.Helpers;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Unit tests for IndexNameHelper utility class.
/// Tests the logic for generating SQL Server index names that comply with the 128-character limit.
/// </summary>
public class IndexNameHelperTests
{
    /// <summary>
    /// Tests that index names within the 128-character limit are returned unchanged.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithShortName_ReturnsUnchanged()
    {
        // Arrange
        var tableName = "User";
        var fields = new[] { "Email" };

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Should().Be("IX_User_Email");
        result.Length.Should().BeLessThanOrEqualTo(128);
    }

    /// <summary>
    /// Tests that index names exactly at the 128-character limit are returned unchanged.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithExact128Characters_ReturnsUnchanged()
    {
        // Arrange
        var tableName = "User";
        // Create a field name that makes the total exactly 128 characters
        // "IX_User_" = 8 characters, so we need 120 characters for the field
        var longField = new string('A', 120);
        var fields = new[] { longField };
        var expected = $"IX_User_{longField}";

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Length.Should().Be(128);
        result.Should().Be(expected);
        // Verify it doesn't have a hash (should be exactly the base name)
        result.Should().NotMatchRegex(@".+_[0-9a-f]{8}$");
    }

    /// <summary>
    /// Tests that index names exceeding 128 characters are trimmed and a hash is appended.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithLongName_TrimsAndAppendsHash()
    {
        // Arrange
        var tableName = "User";
        // Create a field name that makes the total exceed 128 characters
        var longField = new string('A', 150);
        var fields = new[] { longField };

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Length.Should().Be(128);
        result.Should().StartWith("IX_User_");
        // Should end with underscore followed by 8 hex characters (hash)
        result.Should().MatchRegex(@"^IX_User_.+_[0-9a-f]{8}$");
        var hash = result.Substring(result.Length - 8);
        hash.Should().MatchRegex(@"^[0-9a-f]{8}$");
    }

    /// <summary>
    /// Tests that two very similar long names produce different hashes.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithSimilarLongNames_ProducesDifferentHashes()
    {
        // Arrange
        var tableName = "User";
        var longField1 = new string('A', 150) + "X";
        var longField2 = new string('A', 150) + "Y";
        var fields1 = new[] { longField1 };
        var fields2 = new[] { longField2 };

        // Act
        var result1 = IndexNameHelper.GenerateIndexName(false, tableName, fields1);
        var result2 = IndexNameHelper.GenerateIndexName(false, tableName, fields2);

        // Assert
        result1.Length.Should().Be(128);
        result2.Length.Should().Be(128);

        // Extract the hashes (last 8 characters after the underscore)
        var hash1 = result1.Substring(result1.Length - 8);
        var hash2 = result2.Substring(result2.Length - 8);

        hash1.Should().NotBe(hash2, "Different input names should produce different hashes");
        hash1.Should().MatchRegex(@"^[0-9a-f]{8}$");
        hash2.Should().MatchRegex(@"^[0-9a-f]{8}$");
    }

    /// <summary>
    /// Tests that two identical long names produce the same hash (deterministic).
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithSameLongName_ProducesSameHash()
    {
        // Arrange
        var tableName = "User";
        var longField = new string('A', 150);
        var fields = new[] { longField };

        // Act
        var result1 = IndexNameHelper.GenerateIndexName(false, tableName, fields);
        var result2 = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result1.Should().Be(result2, "Same input should produce the same hash (deterministic)");
    }

    /// <summary>
    /// Tests that index names with multiple fields are handled correctly when they exceed the limit.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithMultipleLongFields_TrimsAndAppendsHash()
    {
        // Arrange
        var tableName = "Order";
        var fields = new[] { "CustomerID", "OrderDate", "Status", new string('A', 100), new string('B', 50) };

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Length.Should().Be(128);
        result.Should().StartWith("IX_Order_");
        // Should end with underscore followed by 8 hex characters (hash)
        result.Should().MatchRegex(@"^IX_Order_.+_[0-9a-f]{8}$");
        var hash = result.Substring(result.Length - 8);
        hash.Should().MatchRegex(@"^[0-9a-f]{8}$");
    }

    /// <summary>
    /// Tests that index names with very long table names are handled correctly.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithLongTableName_TrimsAndAppendsHash()
    {
        // Arrange
        var tableName = new string('T', 150);
        var fields = new[] { "Email", "Username" };

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Length.Should().Be(128);
        result.Should().StartWith("IX_");
        // Should end with underscore followed by 8 hex characters (hash)
        result.Should().MatchRegex(@"^IX_.+_[0-9a-f]{8}$");
        var hash = result.Substring(result.Length - 8);
        hash.Should().MatchRegex(@"^[0-9a-f]{8}$");
    }

    /// <summary>
    /// Tests that index names with many fields are handled correctly.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithManyFields_HandlesCorrectly()
    {
        // Arrange
        var tableName = "Product";
        var fields = new[] { "Name", "Category", "SKU", "Brand", "Price", "Stock", "IsActive", "CreatedDate", "UpdatedDate", "SomeOtherField", "AndSomeMoreToGoOver128InLengthAndMakeSureItsHashed" };

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(128);
        result.Should().StartWith("IX_Product_");
    }

    /// <summary>
    /// Tests that the hash is consistently 8 characters long.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithLongName_HashIsAlways8Characters()
    {
        // Arrange
        var tableName = "User";
        var longField = new string('A', 200);
        var fields = new[] { longField };

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Length.Should().Be(128);
        // Extract the hash (last 8 characters after the underscore)
        var hash = result.Substring(result.Length - 8);
        hash.Length.Should().Be(8);
        hash.Should().MatchRegex(@"^[0-9a-f]{8}$");
    }

    /// <summary>
    /// Tests that two different names that would have the same prefix after trimming produce different hashes.
    /// This is a critical test to ensure uniqueness.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithDifferentNamesSamePrefix_ProducesDifferentHashes()
    {
        // Arrange
        var tableName = "User";
        // Create two names that would be trimmed to the same prefix
        var prefix = new string('A', 120);
        var fields1 = new[] { prefix + "X123456789" };
        var fields2 = new[] { prefix + "Y987654321" };

        // Act
        var result1 = IndexNameHelper.GenerateIndexName(false, tableName, fields1);
        var result2 = IndexNameHelper.GenerateIndexName(false, tableName, fields2);

        // Assert
        result1.Length.Should().Be(128);
        result2.Length.Should().Be(128);

        // The prefixes should be the same (both trimmed)
        var prefix1 = result1.Substring(0, result1.Length - 9);
        var prefix2 = result2.Substring(0, result2.Length - 9);
        prefix1.Should().Be(prefix2, "Both should be trimmed to the same length");

        // But the hashes should be different
        var hash1 = result1.Substring(result1.Length - 8);
        var hash2 = result2.Substring(result2.Length - 8);
        hash1.Should().NotBe(hash2, "Different full names should produce different hashes even with same prefix");
    }

    /// <summary>
    /// Tests that empty fields list is handled correctly.
    /// </summary>
    [Fact]
    public void GenerateIndexName_WithEmptyFields_ReturnsValidName()
    {
        // Arrange
        var tableName = "User";
        var fields = Array.Empty<string>();

        // Act
        var result = IndexNameHelper.GenerateIndexName(false, tableName, fields);

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(128);
        result.Should().Be("IX_User_");
    }
}