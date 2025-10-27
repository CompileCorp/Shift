using Compile.VnumEnumeration;

namespace Compile.Shift.Model.Vnums;

public enum DmdFieldTypeId
{
    BOOL = 1,   // Maps to SQL bit
    GUID,       // Maps to SQL uniqueidentifier

    // ASCII string types
    ACHAR,      // Maps to SQL char(n)
    ASTRING,    // Maps to SQL varchar(n)

    // Unicode string types
    UCHAR,      // Maps to SQL nchar(n)
    USTRING,    // Maps to SQL nvarchar(n)

    // Backward compatibility
    [Obsolete("Replaced with UCHAR")] CHAR,     // Maps to SQL nchar(n)
    [Obsolete("Replaced with USTRING")] STRING, // Maps to SQL nvarchar(n)

    INT,        // Maps to SQL int
    LONG,       // Maps to SQL bigint
    DECIMAL,    // Maps to SQL decimal(p,s)
    FLOAT,      // Maps to SQL float

    DATETIME,   // Maps to SQL datetime
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
        string dmdCode,
        SqlFieldTypeId mapToSql,
        PrecisionType precisionType = PrecisionType.None,
        int? defaultPrecision = null,
        int? defaultScale = null
    ) : base(id, dmdCode)
    {
        SqlFieldTypeId = mapToSql;
        PrecisionType = precisionType;
        DefaultPrecision = defaultPrecision;
        DefaultScale = defaultScale;
    }

    public static readonly DmdFieldType BOOL =
        new(id: DmdFieldTypeId.BOOL,
            dmdCode: "bool",
            mapToSql: SqlFieldTypeId.BIT);

    public static readonly DmdFieldType GUID =
        new(id: DmdFieldTypeId.GUID,
            dmdCode: "guid",
            mapToSql: SqlFieldTypeId.UNIQUEIDENTIFIER);


    // ASCII string types
    public static readonly DmdFieldType ACHAR =
        new(id: DmdFieldTypeId.ACHAR,
            dmdCode: "achar",
            mapToSql: SqlFieldTypeId.CHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    public static readonly DmdFieldType ASTRING =
        new(id: DmdFieldTypeId.ASTRING,
            dmdCode: "astring",
            mapToSql: SqlFieldTypeId.VARCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255);


    // Unicode string types
    public static readonly DmdFieldType UCHAR =
        new(id: DmdFieldTypeId.UCHAR,
            dmdCode: "uchar",
            mapToSql: SqlFieldTypeId.NCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    public static readonly DmdFieldType USTRING =
        new(id: DmdFieldTypeId.USTRING,
            dmdCode: "ustring",
            mapToSql: SqlFieldTypeId.NVARCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255);

    [Obsolete("Replaced with UCHAR")]
    public static readonly DmdFieldType CHAR =
        new(id: DmdFieldTypeId.CHAR,
            dmdCode: "char",
            mapToSql: SqlFieldTypeId.NCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    [Obsolete("Replaced with USTRING")]
    public static readonly DmdFieldType STRING =
        new(id: DmdFieldTypeId.STRING,
            dmdCode: "string",
            mapToSql: SqlFieldTypeId.NVARCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255);


    // Numeric types
    public static readonly DmdFieldType INT =
        new(id: DmdFieldTypeId.INT,
            dmdCode: "int",
            mapToSql: SqlFieldTypeId.INT);

    public static readonly DmdFieldType LONG =
        new(id: DmdFieldTypeId.LONG,
            dmdCode: "long",
            mapToSql: SqlFieldTypeId.BIGINT);

    public static readonly DmdFieldType DECIMAL =
        new(id: DmdFieldTypeId.DECIMAL,
            dmdCode: "decimal",
            mapToSql: SqlFieldTypeId.DECIMAL,
            precisionType: PrecisionType.PrecisionWithScaleRequired,
            defaultPrecision: 18,
            defaultScale: 0);

    public static readonly DmdFieldType FLOAT =
        new(id: DmdFieldTypeId.FLOAT,
            dmdCode: "float",
            mapToSql: SqlFieldTypeId.FLOAT);


    // Date/time types
    public static readonly DmdFieldType DATETIME =
        new(id: DmdFieldTypeId.DATETIME,
            dmdCode: "datetime",
            mapToSql: SqlFieldTypeId.DATETIME);

}
