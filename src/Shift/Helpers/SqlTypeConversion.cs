using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
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
    /// hold every possible value. <paramref name="maxRenderedWidth"/> is only meaningful when this
    /// returns true; it is zero otherwise.
    /// </summary>
    public static bool IsSupportedInPlaceConversion(string fromType, string toType, out int maxRenderedWidth)
    {
        maxRenderedWidth = 0;

        if (!IntegerMaxRenderedWidths.TryGetValue(fromType, out var width))
            return false;

        if (!VariableWidthStringTypes.Contains(toType))
            return false;

        maxRenderedWidth = width;
        return true;
    }

    /// <summary>
    /// True when SQL Server permits an IDENTITY column to have this type. Identity columns must be
    /// an integer type, or <c>decimal</c>/<c>numeric</c> with a scale of 0. Converting an identity
    /// column to one of those is allowed — a <c>numeric(18,0)</c> identity can become
    /// <c>decimal(19,0)</c> — while any other target is rejected outright with error 2749.
    ///
    /// The integer set is the allow-list's own key set: both are simply SQL Server's integer
    /// types, and keeping one list avoids the two drifting apart.
    /// </summary>
    public static bool CanBeIdentity(FieldModel field) =>
        IntegerMaxRenderedWidths.ContainsKey(field.Type)
        || ((string.Equals(field.Type, "decimal", StringComparison.OrdinalIgnoreCase)
             || string.Equals(field.Type, "numeric", StringComparison.OrdinalIgnoreCase))
            && (field.Scale ?? 0) == 0);

    /// <summary>
    /// True when <paramref name="targetField"/> is precisely what Shift's own dmd round-trip
    /// produces for <paramref name="actualField"/>, and so is not model drift: a <c>text</c>
    /// column exports as dmd <c>astring(max)</c> and comes back as <c>varchar(max)</c>, and
    /// <c>money</c> comes back as <c>decimal(19,4)</c>. Warning about those pairs would fire on
    /// every plan for any schema holding a legacy <c>text</c> or <c>money</c> column.
    ///
    /// The comparison is on the fully rendered type, precision and scale included, so only the
    /// exact round-trip is exempt. A <c>text</c> column targeting <c>varchar(50)</c>, or a
    /// <c>money</c> column targeting <c>decimal(18,4)</c>, is a real change of intent and is still
    /// reported.
    /// </summary>
    public static bool IsRoundTripEquivalent(FieldModel actualField, FieldModel targetField) =>
        string.Equals(RenderSqlType(actualField), RenderSqlType(targetField), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Renders a field the way Shift would emit it in DDL, so two fields can be compared on the
    /// type they actually resolve to rather than on the spelling they were declared with.
    /// </summary>
    private static string RenderSqlType(FieldModel field) =>
        Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType)
            ? SqlTypeHelper.GetSqlTypeString(field, sqlFieldType)
            : SqlTypeHelper.GetUnknownSqlTypeString(field);
}