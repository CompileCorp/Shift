using Compile.Shift.Model;

namespace Compile.Shift.Ef;

public class TypeMapper
{
    private readonly Dictionary<string, string> _typeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Integer types
        { "bit", "bool" },
        { "tinyint", "byte" },
        { "smallint", "short" },
        { "int", "int" },
        { "bigint", "long" },

        // Decimal types
        { "decimal", "decimal" },
        { "numeric", "decimal" },
        { "money", "decimal" },
        { "smallmoney", "decimal" },
        { "float", "double" },
        { "real", "float" },

        // String types
        { "char", "string" },
        { "varchar", "string" },
        { "text", "string" },
        { "nchar", "string" },
        { "nvarchar", "string" },
        { "ntext", "string" },

        // Date/Time types
        { "datetime", "DateTime" },
        { "datetime2", "DateTime" },
        { "smalldatetime", "DateTime" },
        { "date", "DateTime" },
        { "time", "TimeSpan" },
        { "datetimeoffset", "DateTimeOffset" },

        // Binary types
        { "binary", "byte[]" },
        { "varbinary", "byte[]" },
        { "image", "byte[]" },

        // Other types
        { "uniqueidentifier", "Guid" },
        { "timestamp", "byte[]" },
        { "rowversion", "byte[]" },
        { "xml", "string" },
        { "sql_variant", "object" }
    };

    public string MapToCSharpType(FieldModel field)
    {
        var baseType = GetBaseType(field.Type);
        
        // Handle nullable types
        if (field.IsNullable || field.IsOptional)
        {
            if (IsValueType(baseType))
            {
                return $"{baseType}?";
            }
        }

        return baseType;
    }

    private string GetBaseType(string sqlType)
    {
        // Remove size specifications (e.g., varchar(50) -> varchar)
        var cleanType = sqlType.Split('(')[0].Trim();

        if (_typeMap.TryGetValue(cleanType, out var csharpType))
        {
            return csharpType;
        }

        // Default to string for unknown types
        return "string";
    }

    private bool IsValueType(string csharpType)
    {
        return csharpType switch
        {
            "bool" or "byte" or "short" or "int" or "long" or 
            "decimal" or "double" or "float" or "DateTime" or 
            "TimeSpan" or "DateTimeOffset" or "Guid" => true,
            _ => false
        };
    }
}