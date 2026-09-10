using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;

namespace Compile.Shift.Helpers;

/// <summary>
/// Describes which SQL Server base-type changes Shift is willing to apply with a single
/// in-place <c>ALTER TABLE ... ALTER COLUMN</c>.
///
/// The allow-list is deliberately narrow: an integer becoming a variable-width string, and
/// <c>varchar</c> becoming <c>nvarchar</c>. Fixed-width targets (<c>char</c>/<c>nchar</c>) are
/// excluded on purpose because SQL Server right-pads the converted value with spaces, which
/// changes the stored data rather than just its type. The reverse directions are excluded because
/// they lose data: arbitrary text does not convert to a number, and narrowing <c>nvarchar</c> to
/// <c>varchar</c> silently replaces every character outside the target collation's code page
/// with <c>?</c>.
/// </summary>
internal static class SqlTypeConversion
{
    /// <summary>
    /// A width that only a MAX target can satisfy, and equally the precision SQL Server reports for
    /// a MAX column. The two meanings coincide: an unbounded source needs an unbounded target.
    /// </summary>
    public const int MaxWidth = -1;

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
    public static bool IsSupportedInPlaceConversion(string fromType, int? fromPrecision, string toType) =>
        IsSupportedInPlaceConversion(fromType, fromPrecision, toType, out _);

    /// <summary>
    /// True when a column of <paramref name="fromType"/> may be converted to
    /// <paramref name="toType"/> by an in-place ALTER COLUMN, also reporting how many characters
    /// the target has to hold for every value the source could carry to survive.
    /// <paramref name="requiredWidth"/> is <see cref="MaxWidth"/> when the source is unbounded and
    /// only a MAX target will do. It is meaningful only when this returns true; it is zero
    /// otherwise, so a caller reading it on a false return cannot mistake it for a real width.
    /// </summary>
    public static bool IsSupportedInPlaceConversion(string fromType, int? fromPrecision, string toType, out int requiredWidth)
    {
        requiredWidth = 0;

        if (!VariableWidthStringTypes.Contains(toType))
            return false;

        // Integer to string. The width required is the widest rendering of the source *type*, never
        // the widest value stored today - see FailsOpenOnNarrowTarget for why that has to be so.
        if (IntegerMaxRenderedWidths.TryGetValue(fromType, out var integerWidth))
        {
            requiredWidth = integerWidth;
            return true;
        }

        // varchar to nvarchar. Every ASCII string is a valid unicode string, so nothing but width
        // is in question, and this is the direction a dmd field flipping from astring to ustring
        // asks for. The reverse is deliberately absent: it is lossy and fails open.
        if (string.Equals(fromType, "varchar", StringComparison.OrdinalIgnoreCase)
            && string.Equals(toType, "nvarchar", StringComparison.OrdinalIgnoreCase))
        {
            // A source of unknown width is treated as unbounded, so only a MAX target satisfies it.
            requiredWidth = fromPrecision ?? MaxWidth;
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when SQL Server destroys data rather than raising if the target turns out to be too
    /// narrow, which is what forces the width to be guaranteed from the source type at plan time
    /// instead of probed from the rows.
    ///
    /// An integer conversion fails <b>open</b>: a value that will not fit is stored as <c>*</c> and
    /// the ALTER still reports success, so nothing downstream can catch it. A string widening fails
    /// <b>closed</b>: truncation raises, so a too-narrow target is safe to leave to the runner's
    /// live-data probe, exactly as a plain resize already is.
    /// </summary>
    public static bool FailsOpenOnNarrowTarget(string fromType) =>
        IntegerMaxRenderedWidths.ContainsKey(fromType);

    /// <summary>
    /// True when <paramref name="targetField"/> can hold every value a source needing
    /// <paramref name="requiredWidth"/> characters can produce, reporting through
    /// <paramref name="effectiveWidth"/> the width the field will actually be created at.
    ///
    /// The width is resolved rather than read straight off the field, because a field that leaves
    /// its precision unset is still created at the type's default width. Treating an absent
    /// precision as "nothing to check" would skip the guard altogether for the one conversion that
    /// cannot afford to have it skipped.
    /// </summary>
    public static bool IsTargetWideEnough(FieldModel targetField, int requiredWidth, out int effectiveWidth)
    {
        effectiveWidth = ResolveWidth(targetField);

        if (effectiveWidth == MaxWidth)
            return true; // MAX holds anything

        if (requiredWidth == MaxWidth)
            return false; // an unbounded source needs an unbounded target, however wide the offer

        return effectiveWidth >= requiredWidth;
    }

    /// <summary>
    /// The width a field will be created at: its own precision when it has one, otherwise the
    /// default Shift renders for the type. Falls back to zero when the type carries neither, so an
    /// unrecognised target is refused rather than waved through.
    /// </summary>
    private static int ResolveWidth(FieldModel field) =>
        field.Precision
        ?? (Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType)
            ? sqlFieldType.DefaultPrecision
            : null)
        ?? 0;

    /// <summary>
    /// True when SQL Server permits an IDENTITY column to take this field's shape. Identity
    /// columns must be an integer type, or <c>decimal</c>/<c>numeric</c> with a scale of 0, and
    /// must be non-nullable. Converting an identity column to a shape that satisfies all of that
    /// is allowed — a <c>numeric(18,0)</c> identity can become <c>decimal(19,0)</c> — while any
    /// other target is rejected: error 2749 for the type, error 8147 for nullability.
    ///
    /// The integer set is the allow-list's own key set: both are simply SQL Server's integer
    /// types, and keeping one list avoids the two drifting apart.
    /// </summary>
    public static bool CanBeIdentity(FieldModel field) =>
        !field.IsNullable
        && (IntegerMaxRenderedWidths.ContainsKey(field.Type)
            || ((string.Equals(field.Type, "decimal", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(field.Type, "numeric", StringComparison.OrdinalIgnoreCase))
                && (field.Scale ?? 0) == 0));

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