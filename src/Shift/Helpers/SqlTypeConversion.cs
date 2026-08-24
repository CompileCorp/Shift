using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;

namespace Compile.Shift.Helpers;

/// <summary>
/// Describes which SQL Server base-type changes Shift is willing to apply with a single
/// in-place <c>ALTER TABLE ... ALTER COLUMN</c>.
///
/// The allow-list is deliberately narrow: only integer to variable-width string. Fixed-width
/// targets (<c>char</c>/<c>nchar</c>) are excluded on purpose because SQL Server right-pads the
/// converted value with spaces, which changes the stored data rather than just its type.
/// </summary>
internal static class SqlTypeConversion
{
    /// <summary>
    /// Integer types mapped to the widest string SQL Server renders them as, counting the sign
    /// (for example <c>int</c> spans <c>-2147483648</c>, which is 11 characters).
    /// </summary>
    private static readonly Dictionary<string, int> IntegerMaxRenderedWidths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tinyint"] = 3,   // 0 .. 255
        ["smallint"] = 6,  // -32768 .. 32767
        ["int"] = 11,      // -2147483648 .. 2147483647
        ["bigint"] = 20    // -9223372036854775808 .. 9223372036854775807
    };

    private static readonly HashSet<string> VariableWidthStringTypes =
        new(StringComparer.OrdinalIgnoreCase) { "varchar", "nvarchar" };

    /// <summary>
    /// True when a column of <paramref name="fromType"/> may be converted to
    /// <paramref name="toType"/> by an in-place ALTER COLUMN.
    /// </summary>
    public static bool IsSupportedInPlaceConversion(string fromType, string toType) =>
        IsSupportedInPlaceConversion(fromType, toType, out _);

    /// <summary>
    /// True when a column of <paramref name="fromType"/> may be converted to
    /// <paramref name="toType"/> by an in-place ALTER COLUMN, also reporting the widest rendering
    /// of <paramref name="fromType"/> in characters so callers can spot a target too narrow to
    /// hold every possible value.
    /// </summary>
    public static bool IsSupportedInPlaceConversion(string fromType, string toType, out int maxRenderedWidth) =>
        IntegerMaxRenderedWidths.TryGetValue(fromType, out maxRenderedWidth)
        && VariableWidthStringTypes.Contains(toType);

    /// <summary>
    /// True when two SQL types are different spellings of the same dmd type, for example
    /// <c>text</c> and <c>varchar</c> (both dmd <c>astring</c>) or <c>money</c> and
    /// <c>decimal</c>. Shift's own round-trip produces these pairs on purpose, so they are not
    /// model drift and must not be reported as unmigrated type changes.
    /// </summary>
    public static bool AreSameDmdType(string firstSqlType, string secondSqlType) =>
        Vnum.TryFromCode<SqlFieldType>(firstSqlType, ignoreCase: true, out var first)
        && Vnum.TryFromCode<SqlFieldType>(secondSqlType, ignoreCase: true, out var second)
        && first.DmdType.Code == second.DmdType.Code;
}