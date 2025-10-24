using Compile.VnumEnumeration;

namespace Compile.Shift.Model.Vnums;

public enum SqlFieldTypeId
{
    BIT = 1,

    CHAR,
    VARCHAR,
    TEXT,

    NCHAR,
    NVARCHAR,
    NTEXT,

    INT,
    BIGINT,
    DECIMAL,
    NUMERIC,
    FLOAT,
    MONEY,
    SMALLMONEY,

    DATETIME,

    //TODO: UNIQUEIDENTIFIER,
    //TODO: DATETIME2,
    //TODO: DATE,
    //TODO: TIME,
}

public enum PrecisionType
{
    None = 1,
    PrecisionOnlyAlwaysRequired,
    PrecisionOnlyOptional,
    PrecisionWithScaleAlwaysRequired,
}

/// <summary>
/// Supported DMD field types (extend as needed)
/// </summary>
public class SqlFieldType : Vnum<SqlFieldTypeId>
{
    private DmdFieldTypeId DmdTypeId { get; }
    public DmdFieldType DmdType => FromEnum<DmdFieldType, DmdFieldTypeId>(DmdTypeId); // To avoid circular reference
    public PrecisionType PrecisionType { get; }
    public int? DefaultPrecision { get; }
    public int? DefaultScale { get; }
    public bool SupportsMaxLength { get; }
    public int? MaxLengthMarker { get; }

    private SqlFieldType(
        SqlFieldTypeId id,
        string code,
        DmdFieldTypeId mapTo,
        PrecisionType precisionType = PrecisionType.None,
        int? defaultPrecision = null,
        int? defaultScale = null,
        bool supportsMaxLength = false,
        int? maxLengthMarker = null
    ) : base(id, code)
    {
        DmdTypeId = mapTo;
        PrecisionType = precisionType;
        DefaultPrecision = defaultPrecision;
        DefaultScale = defaultScale;
        SupportsMaxLength = supportsMaxLength;
        MaxLengthMarker = maxLengthMarker;
    }


    public static readonly SqlFieldType BIT =
        new(id: SqlFieldTypeId.BIT,
            code: "bit",
            mapTo: DmdFieldTypeId.BOOL);


    public static readonly SqlFieldType CHAR =
        new(id: SqlFieldTypeId.CHAR,
            code: "char",
            mapTo: DmdFieldTypeId.ASTRING, // Conver to ASTRING
            precisionType: PrecisionType.PrecisionOnlyAlwaysRequired,
            defaultPrecision: 1);

    public static readonly SqlFieldType VARCHAR =
        new(id: SqlFieldTypeId.VARCHAR,
            code: "varchar",
            mapTo: DmdFieldTypeId.ASTRING, // Conver to ASTRING
            precisionType: PrecisionType.PrecisionOnlyAlwaysRequired,
            defaultPrecision: 255,
            supportsMaxLength: true,
            maxLengthMarker: -1); 

    public static readonly SqlFieldType TEXT =
        new(id: SqlFieldTypeId.TEXT,
            code: "text",
            mapTo: DmdFieldTypeId.ASTRING); // Conver to ASTRING


    public static readonly SqlFieldType NCHAR =
        new(id: SqlFieldTypeId.NCHAR,
            code: "nchar",
            mapTo: DmdFieldTypeId.STRING, // Conver to STRING
            precisionType: PrecisionType.PrecisionOnlyAlwaysRequired,
            defaultPrecision: 1);

    public static readonly SqlFieldType NVARCHAR =
        new(id: SqlFieldTypeId.NVARCHAR,
            code: "nvarchar",
            mapTo: DmdFieldTypeId.STRING, // Conver to STRING
            precisionType: PrecisionType.PrecisionOnlyAlwaysRequired,
            defaultPrecision: 255,
            supportsMaxLength: true,
            maxLengthMarker: -1); 

    public static readonly SqlFieldType NTEXT =
        new(id: SqlFieldTypeId.NTEXT,
            code: "ntext",
            mapTo: DmdFieldTypeId.STRING); // Conver to STRING


    public static readonly SqlFieldType INT =
        new(id: SqlFieldTypeId.INT,
            code: "int",
            mapTo: DmdFieldTypeId.INT);

    public static readonly SqlFieldType BIGINT =
        new(id: SqlFieldTypeId.BIGINT,
            code: "bigint",
            mapTo: DmdFieldTypeId.LONG);

    public static readonly SqlFieldType DECIMAL =
        new(id: SqlFieldTypeId.DECIMAL,
            code: "decimal",
            mapTo: DmdFieldTypeId.DECIMAL,
            precisionType: PrecisionType.PrecisionWithScaleAlwaysRequired,
            defaultPrecision: 18,
            defaultScale: 0);

    public static readonly SqlFieldType NUMERIC =
        new(id: SqlFieldTypeId.NUMERIC,
            code: "numeric",
            mapTo: DmdFieldTypeId.DECIMAL, // Convert to DECIMAL
            precisionType: PrecisionType.PrecisionWithScaleAlwaysRequired,
            defaultPrecision: 18,
            defaultScale: 0); 

    public static readonly SqlFieldType FLOAT =
        new(id: SqlFieldTypeId.FLOAT,
            code: "float",
            mapTo: DmdFieldTypeId.FLOAT,
            precisionType: PrecisionType.PrecisionOnlyOptional);

    public static readonly SqlFieldType MONEY =
        new(id: SqlFieldTypeId.MONEY,
            code: "money",
            mapTo: DmdFieldTypeId.DECIMAL, // Convert to DECIMAL
            precisionType: PrecisionType.PrecisionWithScaleAlwaysRequired,
            defaultPrecision: 19,
            defaultScale: 4);

    public static readonly SqlFieldType SMALLMONEY =
        new(id: SqlFieldTypeId.SMALLMONEY,
            code: "smallmoney",
            mapTo: DmdFieldTypeId.DECIMAL, // Convert to DECIMAL
            precisionType: PrecisionType.PrecisionWithScaleAlwaysRequired,
            defaultPrecision: 10,
            defaultScale: 4);

    public static readonly SqlFieldType DATETIME =
        new(id: SqlFieldTypeId.DATETIME,
            code: "datetime",
            mapTo: DmdFieldTypeId.DATETIME);

}
