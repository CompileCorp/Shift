using Compile.VnumEnumeration;

namespace Compile.Shift.Model.Vnums;

public enum DmdFieldTypeId
{
    BOOL = 1, // Maps to SQL bit
    GUID,     // Maps to SQL uniqueidentifier

    CHAR,     // Maps to SQL nchar
    STRING,   // Maps to SQL nvarchar

    ACHAR,    // Maps to SQL char
    ASTRING,  // Maps to SQL varchar

    INT,      // Maps to SQL int
    LONG,     // Maps to SQL bigint
    DECIMAL,  // Maps to SQL decimal
    FLOAT,    // Maps to SQL float

    DATETIME, // Maps to SQL datetime
}

/// <summary>
/// Supported DMD field types (extend as needed)
/// </summary>
public class DmdFieldType : Vnum<DmdFieldTypeId>
{
    //public string Type => Code; // Alias for Code
    private SqlFieldTypeId SqlFieldTypeId { get; }
    public SqlFieldType SqlFieldType => FromEnum<SqlFieldType, SqlFieldTypeId>(SqlFieldTypeId); // To avoid circular reference
    public PrecisionType PrecisionType { get; }
    public int? DefaultPrecision { get; }
    public int? DefaultScale { get; }

    private DmdFieldType(
        DmdFieldTypeId id,
        string code,
        SqlFieldTypeId mapTo,
        PrecisionType precisionType = PrecisionType.None,
        int? defaultPrecision = null,
        int? defaultScale = null
    ) : base(id, code)
    {
        SqlFieldTypeId = mapTo;
        PrecisionType = precisionType;
        DefaultPrecision = defaultPrecision;
        DefaultScale = defaultScale;
    }


    public static readonly DmdFieldType BOOL =
        new(id: DmdFieldTypeId.BOOL,
            code: "bool",
            mapTo: SqlFieldTypeId.BIT);

    public static readonly DmdFieldType GUID =
        new(id: DmdFieldTypeId.GUID,
            code: "guid",
            mapTo: SqlFieldTypeId.UNIQUEIDENTIFIER);


    // String types
    public static readonly DmdFieldType STRING =
        new(id: DmdFieldTypeId.STRING,
            code: "string",
            mapTo: SqlFieldTypeId.NVARCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255);

    public static readonly DmdFieldType CHAR =
        new(id: DmdFieldTypeId.CHAR,
            code: "char",
            mapTo: SqlFieldTypeId.NCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    public static readonly DmdFieldType ASTRING =
        new(id: DmdFieldTypeId.ASTRING,
            code: "astring",
            mapTo: SqlFieldTypeId.VARCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255);

    public static readonly DmdFieldType ACHAR =
        new(id: DmdFieldTypeId.ACHAR,
            code: "achar",
            mapTo: SqlFieldTypeId.CHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);


    // Numeric types
    public static readonly DmdFieldType INT =
        new(id: DmdFieldTypeId.INT,
            code: "int",
            mapTo: SqlFieldTypeId.INT);

    public static readonly DmdFieldType LONG =
        new(id: DmdFieldTypeId.LONG,
            code: "long",
            mapTo: SqlFieldTypeId.BIGINT);

    public static readonly DmdFieldType DECIMAL =
        new(id: DmdFieldTypeId.DECIMAL,
            code: "decimal",
            mapTo: SqlFieldTypeId.DECIMAL,
            precisionType: PrecisionType.PrecisionWithScaleRequired,
            defaultPrecision: 18,
            defaultScale: 0);

    public static readonly DmdFieldType FLOAT =
        new(id: DmdFieldTypeId.FLOAT,
            code: "float",
            mapTo: SqlFieldTypeId.FLOAT);


    // Date/time types
    public static readonly DmdFieldType DATETIME =
        new(id: DmdFieldTypeId.DATETIME,
            code: "datetime",
            mapTo: SqlFieldTypeId.DATETIME);

}
