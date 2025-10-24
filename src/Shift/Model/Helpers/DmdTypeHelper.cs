using Compile.Shift.Model.Vnums;

namespace Compile.Shift.Model.Helpers;

internal static class DmdTypeHelper
{
    public static string GetDmdTypeString(FieldModel fieldModel, SqlFieldType sqlFieldType)
    {
        string dmdTypeCode = sqlFieldType.DmdType.Code;

        // TEXT/NTEXT converted string(max)/astring(max)
        if (sqlFieldType == SqlFieldType.TEXT || sqlFieldType == SqlFieldType.NTEXT)
            return $"{dmdTypeCode}(max)";

        // MONEY/SMALLMONEY converted to decimal(19,4)/decimal(10,4)
        if (sqlFieldType == SqlFieldType.MONEY || sqlFieldType == SqlFieldType.SMALLMONEY)
            return $"{dmdTypeCode}({sqlFieldType.DefaultPrecision},{sqlFieldType.DefaultScale})";

        // Check for MAX length marker
        if (sqlFieldType.SupportsMaxLength &&
            fieldModel.Precision.HasValue &&
            fieldModel.Precision == sqlFieldType.MaxLengthMarker)
        {
            return $"{dmdTypeCode}(max)";
        }

        // Handle precision/scale based on type
        return sqlFieldType.PrecisionType switch
        {
            PrecisionType.PrecisionOnlyRequired =>
                $"{dmdTypeCode}({fieldModel.Precision ?? sqlFieldType.DefaultPrecision ?? sqlFieldType.DmdType.DefaultPrecision})",

            PrecisionType.PrecisionWithScaleRequired =>
                FormatPrecisionAndScale(dmdTypeCode, fieldModel, sqlFieldType),

            _ => dmdTypeCode
        };
    }

    private static string FormatPrecisionAndScale(string dmdTypeCode, FieldModel fieldModel, SqlFieldType sqlFieldType)
    {
        var precision = fieldModel.Precision ?? sqlFieldType.DefaultPrecision ?? sqlFieldType.DmdType.DefaultPrecision;
        var scale = fieldModel.Scale ?? sqlFieldType.DefaultScale ?? sqlFieldType.DmdType.DefaultScale;

        return $"{dmdTypeCode}({precision},{scale})";
    }
}
