using System;
using System.Security.Cryptography;
using System.Text;

namespace Compile.Shift.Helpers;

/// <summary>
/// Utility class for generating SQL Server index names that comply with the 128-character limit.
/// When an index name would exceed 128 characters, it is trimmed and a hash suffix is appended
/// to ensure uniqueness while maintaining readability.
/// </summary>
public static class IndexNameHelper
{
    private const int MaxIndexNameLength = 128;
    private const int HashLength = 8; // 8 hex characters = 4 bytes = 32 bits of entropy

    /// <summary>
    /// Generates a valid SQL Server index name that complies with the 128-character limit.
    /// If the name exceeds the limit, it is trimmed and a hash suffix is appended to ensure uniqueness.
    /// </summary>
    /// <param name="isAlternateKey">true if the index is an alternate key</param>
    /// <param name="tableName">The name of the table</param>
    /// <param name="resolvedFields">The resolved field names for the index</param>
    /// <returns>A valid index name that is at most 128 characters long</returns>
    public static string GenerateIndexName(bool isAlternateKey, string tableName, IEnumerable<string> resolvedFields)
    {
        var prefix = isAlternateKey ? "AK" : "IX";

        var fieldsList = resolvedFields.ToList();
        var baseName = $"{prefix}_{tableName}_{string.Join("_", fieldsList)}";

        var underLimit = baseName.Length <= MaxIndexNameLength;
        if (underLimit)
            return baseName;

        // Calculate how much space we need for the hash suffix
        // Format: "IX_TableName_Field1_Field2..._Hash"
        var suffixLength = 1 + HashLength; // "_" + hash
        var maxBaseLength = MaxIndexNameLength - suffixLength;

        // Trim the base name to fit
        var trimmedBase = baseName.Substring(0, maxBaseLength);

        // Generate hash from the original full name to ensure uniqueness
        var hash = ComputeHash(baseName);

        // Return trimmed name with hash suffix
        return $"{trimmedBase}_{hash}";
    }

    /// <summary>
    /// Computes a short hash of the input string using SHA256.
    /// Returns the first 8 hexadecimal characters of the hash.
    /// </summary>
    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashString.Substring(0, HashLength);
    }
}