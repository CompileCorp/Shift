using Compile.VnumEnumeration;

namespace Compile.Shift.Model.Vnums;

public enum SqlFieldTypeId
{
    BIT = 1,    // Maps to DMD bool
    UNIQUEIDENTIFIER, // Maps to DMD guid

    CHAR,       // Maps to DMD achar
    VARCHAR,    // Maps to DMD astring
    TEXT,       // Maps to DMD astring(max). TEXT was deprecated in SQL Server 2005 because VARCHAR(MAX) is superior

    NCHAR,      // Maps to DMD char
    NVARCHAR,   // Maps to DMD string
    NTEXT,      // Maps to DMD string(max). NTEXT was deprecated in SQL Server 2005 because NVARCHAR(MAX) is superior 

    INT,        // Maps to DMD int
    BIGINT,     // Maps to DMD long
    DECIMAL,    // Maps to DMD decimal
    NUMERIC,    // Maps to DMD decimal
    FLOAT,      // Maps to DMD float
    MONEY,      // Maps to DMD decimal(19,4)
    SMALLMONEY, // Maps to DMD decimal(10,4)

    DATETIME,   // Maps to DMD datetime

    //TODO: DATETIME2,
    //TODO: DATE,
    //TODO: TIME,
    //TODO: NUMERIC,
    //TODO: BINARY,
    //TODO: VARBINARY,
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

    public static readonly SqlFieldType UNIQUEIDENTIFIER =
        new(id: SqlFieldTypeId.UNIQUEIDENTIFIER,
            code: "uniqueidentifier",
            mapTo: DmdFieldTypeId.GUID);

    public static readonly SqlFieldType CHAR =
        new(id: SqlFieldTypeId.CHAR,
            code: "char",
            mapTo: DmdFieldTypeId.ACHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    public static readonly SqlFieldType VARCHAR =
        new(id: SqlFieldTypeId.VARCHAR,
            code: "varchar",
            mapTo: DmdFieldTypeId.ASTRING,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255,
            supportsMaxLength: true,
            maxLengthMarker: -1); 

    public static readonly SqlFieldType TEXT =
        new(id: SqlFieldTypeId.TEXT,
            code: "text",
            mapTo: DmdFieldTypeId.ASTRING); // Convert to ASTRING DMD data type


    public static readonly SqlFieldType NCHAR =
        new(id: SqlFieldTypeId.NCHAR,
            code: "nchar",
            mapTo: DmdFieldTypeId.CHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    public static readonly SqlFieldType NVARCHAR =
        new(id: SqlFieldTypeId.NVARCHAR,
            code: "nvarchar",
            mapTo: DmdFieldTypeId.STRING,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255,
            supportsMaxLength: true,
            maxLengthMarker: -1); 

    public static readonly SqlFieldType NTEXT =
        new(id: SqlFieldTypeId.NTEXT,
            code: "ntext",
            mapTo: DmdFieldTypeId.STRING); // Convert to STRING DMD data type


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
            precisionType: PrecisionType.PrecisionWithScaleRequired,
            defaultPrecision: 18,
            defaultScale: 0);

    public static readonly SqlFieldType NUMERIC =
        new(id: SqlFieldTypeId.NUMERIC,
            code: "numeric",
            mapTo: DmdFieldTypeId.DECIMAL, // Convert to DECIMAL DMD data type
            precisionType: PrecisionType.PrecisionWithScaleRequired,
            defaultPrecision: 18,
            defaultScale: 0); 

    public static readonly SqlFieldType FLOAT =
        new(id: SqlFieldTypeId.FLOAT,
            code: "float",
            mapTo: DmdFieldTypeId.FLOAT);

    public static readonly SqlFieldType MONEY =
        new(id: SqlFieldTypeId.MONEY,
            code: "money",
            mapTo: DmdFieldTypeId.DECIMAL, // Convert to DECIMAL DMD data type
            precisionType: PrecisionType.None,
            defaultPrecision: 19,
            defaultScale: 4);

    public static readonly SqlFieldType SMALLMONEY =
        new(id: SqlFieldTypeId.SMALLMONEY,
            code: "smallmoney",
            mapTo: DmdFieldTypeId.DECIMAL, // Convert to DECIMAL DMD data type
            precisionType: PrecisionType.None,
            defaultPrecision: 10,
            defaultScale: 4);

    public static readonly SqlFieldType DATETIME =
        new(id: SqlFieldTypeId.DATETIME,
            code: "datetime",
            mapTo: DmdFieldTypeId.DATETIME);

}
