using Compile.Shift.Model;
using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;

namespace Compile.Shift.Dbml;

/// <summary>
/// Renders a <see cref="FieldModel"/>'s type as a DBML column type.
///
/// DBML passes column types through to the diagram verbatim, so the goal is the SQL Server spelling
/// the field will actually have. <see cref="FieldModel.Type"/> already holds the normalised SQL type
/// code with precision and scale kept separately, so this mirrors the decision table in
/// <c>DmdTypeHelper</c> — which precision suffix a type takes, and when <c>(max)</c> applies — while
/// keeping the SQL type name instead of translating it to a DMD type.
/// </summary>
public class DbmlTypeMapper
{
    /// <summary>
    /// Maps a field to its DBML column type.
    /// </summary>
    /// <param name="field">The field to map.</param>
    /// <param name="dbmlType">
    /// The rendered type. For a type Shift does not model, this is the raw type name, double-quoted
    /// if it contains whitespace (DBML requires quoting for multi-word types).
    /// </param>
    /// <returns>True when the type is one Shift models; false when it was passed through verbatim.</returns>
    public bool TryMapToDbmlType(FieldModel field, out string dbmlType)
    {
        if (!Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType))
        {
            dbmlType = QuoteIfNeeded(field.Type);
            return false;
        }

        var sqlCode = sqlFieldType.Code;

        // (max) marker, e.g. varchar(max) / nvarchar(max).
        if (sqlFieldType.SupportsMaxLength &&
            field.Precision.HasValue &&
            field.Precision == sqlFieldType.MaxLengthMarker)
        {
            dbmlType = $"{sqlCode}(max)";
            return true;
        }

        dbmlType = sqlFieldType.PrecisionType switch
        {
            PrecisionType.PrecisionOnlyRequired =>
                $"{sqlCode}({field.Precision ?? sqlFieldType.DefaultPrecision ?? sqlFieldType.DmdType.DefaultPrecision})",

            PrecisionType.PrecisionWithScaleRequired =>
                $"{sqlCode}({field.Precision ?? sqlFieldType.DefaultPrecision ?? sqlFieldType.DmdType.DefaultPrecision}," +
                $"{field.Scale ?? sqlFieldType.DefaultScale ?? sqlFieldType.DmdType.DefaultScale})",

            _ => sqlCode
        };

        return true;
    }

    private static string QuoteIfNeeded(string typeName)
    {
        return typeName.Any(char.IsWhiteSpace) ? $"\"{typeName}\"" : typeName;
    }
}