using Compile.Shift.Model.Vnums;

namespace Compile.Shift.Model.Helpers;

internal static class SqlTypeHelper
{
    /// <summary>
    /// Generates the SQL type string representation for the specified field model and SQL field type.
    /// </summary>
    /// <remarks>
    /// The method handles special cases for certain SQL field types, such as converting `TEXT` and
    /// `NTEXT`  to their modern equivalents (`nvarchar(max)` or `varchar(max)`), and formatting `MONEY` and 
    /// `SMALLMONEY` types with their default precision and scale. If the field model specifies a precision  equal to
    /// the maximum length marker, the type is formatted with "(max)".
    /// </remarks>
    /// <returns>
    /// A string representing the SQL type, including any applicable precision, scale, or length modifiers.
    /// For example, "nvarchar(max)", "decimal(19,4)", or "int".
    /// </returns>
    public static string GetSqlTypeString(FieldModel fieldModel, SqlFieldType sqlFieldType)
    {
        if (sqlFieldType is null)
        {
            return GetUnknownSqlTypeString(fieldModel);
        }

        string sqlType = sqlFieldType.Code;

        // TEXT/NTEXT converted nvarchar(max)/varchar(max)
        if (sqlFieldType == SqlFieldType.TEXT || sqlFieldType == SqlFieldType.NTEXT)
            return $"{sqlFieldType.DmdType.SqlFieldType}(max)";

        // MONEY/SMALLMONEY converted to decimal(19,4)/decimal(10,4)
        if (sqlFieldType == SqlFieldType.MONEY || sqlFieldType == SqlFieldType.SMALLMONEY)
            return $"{sqlFieldType.DmdType.SqlFieldType}({sqlFieldType.DefaultPrecision},{sqlFieldType.DefaultScale})";

        // Check for MAX length marker
        if (sqlFieldType.SupportsMaxLength &&
            fieldModel.Precision.HasValue &&
            fieldModel.Precision == sqlFieldType.MaxLengthMarker)
        {
            return $"{sqlType}(max)";
        }

        // Handle precision/scale based on type
        return sqlFieldType.PrecisionType switch
        {
            PrecisionType.PrecisionOnlyRequired =>
                $"{sqlType}({fieldModel.Precision ?? sqlFieldType.DefaultPrecision})",

            PrecisionType.PrecisionWithScaleRequired =>
                FormatPrecisionAndScale(sqlType, fieldModel, sqlFieldType),

            _ => sqlType
        };
    }

    private static string FormatPrecisionAndScale(string sqlTypeCode, FieldModel fieldModel, SqlFieldType sqlFieldType)
    {
        var precision = fieldModel.Precision ?? sqlFieldType.DefaultPrecision;

        var scale = fieldModel.Scale ?? sqlFieldType.DefaultScale;

        return $"{sqlTypeCode}({precision},{scale})";
    }

    public static string GetUnknownSqlTypeString(FieldModel fieldModel)
    {
        var sqlType = fieldModel.Type;

        if (!fieldModel.Precision.HasValue)
            return sqlType;

        if (fieldModel.Precision == -1)
            return $"{sqlType}(max)";

        if (fieldModel.Scale.HasValue)
            return $"{sqlType}({fieldModel.Precision.Value},{fieldModel.Scale.Value})";

        return $"{sqlType}({fieldModel.Precision.Value})";
    }
}