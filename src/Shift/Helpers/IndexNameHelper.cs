using System;

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
    /// Computes a short discriminator for the input string. Returns 8 hexadecimal characters.
    ///
    /// The suffix exists only to tell two names apart once they have been trimmed to the same
    /// prefix — it is not a security boundary and is never verified against anything, so there is
    /// nothing for a cryptographic digest to buy here. The runtime's ordinal string hash gives the
    /// same 32 bits over the same alphabet without standing up (and disposing) a SHA256 instance,
    /// and without the UTF-8 encode, for every long index name in the plan.
    /// </summary>
    private static string ComputeHash(string input) =>
        ((uint)input.GetHashCode(StringComparison.Ordinal)).ToString("x8");
}