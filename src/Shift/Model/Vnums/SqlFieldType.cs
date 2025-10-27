using Compile.VnumEnumeration;

namespace Compile.Shift.Model.Vnums;

public enum SqlFieldTypeId
{
    BIT = 1,    // Maps to DMD bool
    UNIQUEIDENTIFIER, // Maps to DMD guid

    // ASCII string types
    CHAR,       // Maps to DMD achar(n)
    VARCHAR,    // Maps to DMD astring(n)
    TEXT,       // Maps to DMD astring(max). TEXT was deprecated in SQL Server 2005 because VARCHAR(MAX) is superior

    // Unicode string types
    NCHAR,      // Maps to DMD uchar(n)
    NVARCHAR,   // Maps to DMD ustring(n)
    NTEXT,      // Maps to DMD ustring(max). NTEXT was deprecated in SQL Server 2005 because NVARCHAR(MAX) is superior 

    INT,        // Maps to DMD int
    BIGINT,     // Maps to DMD long
    DECIMAL,    // Maps to DMD decimal(p,s)
    NUMERIC,    // Maps to DMD decimal(p,s)
    FLOAT,      // Maps to DMD float
    MONEY,      // Maps to DMD decimal(19,4)
    SMALLMONEY, // Maps to DMD decimal(10,4)

    DATETIME,   // Maps to DMD datetime

    //TODO: DATETIME2,
    //TODO: DATE,
    //TODO: TIME,
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
        string sqlCode,
        DmdFieldTypeId mapToDmd,
        PrecisionType precisionType = PrecisionType.None,
        int? defaultPrecision = null,
        int? defaultScale = null,
        bool supportsMaxLength = false,
        int? maxLengthMarker = null
    ) : base(id, sqlCode)
    {
        DmdTypeId = mapToDmd;
        PrecisionType = precisionType;
        DefaultPrecision = defaultPrecision;
        DefaultScale = defaultScale;
        SupportsMaxLength = supportsMaxLength;
        MaxLengthMarker = maxLengthMarker;
    }


    public static readonly SqlFieldType BIT =
        new(id: SqlFieldTypeId.BIT,
            sqlCode: "bit",
            mapToDmd: DmdFieldTypeId.BOOL);

    public static readonly SqlFieldType UNIQUEIDENTIFIER =
        new(id: SqlFieldTypeId.UNIQUEIDENTIFIER,
            sqlCode: "uniqueidentifier",
            mapToDmd: DmdFieldTypeId.GUID);


    // ASCII string types
    public static readonly SqlFieldType CHAR =
        new(id: SqlFieldTypeId.CHAR,
            sqlCode: "char",
            mapToDmd: DmdFieldTypeId.ACHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    public static readonly SqlFieldType VARCHAR =
        new(id: SqlFieldTypeId.VARCHAR,
            sqlCode: "varchar",
            mapToDmd: DmdFieldTypeId.ASTRING,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255,
            supportsMaxLength: true,
            maxLengthMarker: -1); 

    public static readonly SqlFieldType TEXT =
        new(id: SqlFieldTypeId.TEXT,
            sqlCode: "text",
            mapToDmd: DmdFieldTypeId.ASTRING); // Convert to DMD astring(max) data type


    // Unicode string types
    public static readonly SqlFieldType NCHAR =
        new(id: SqlFieldTypeId.NCHAR,
            sqlCode: "nchar",
            mapToDmd: DmdFieldTypeId.UCHAR,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 1);

    public static readonly SqlFieldType NVARCHAR =
        new(id: SqlFieldTypeId.NVARCHAR,
            sqlCode: "nvarchar",
            mapToDmd: DmdFieldTypeId.USTRING,
            precisionType: PrecisionType.PrecisionOnlyRequired,
            defaultPrecision: 255,
            supportsMaxLength: true,
            maxLengthMarker: -1); 

    public static readonly SqlFieldType NTEXT =
        new(id: SqlFieldTypeId.NTEXT,
            sqlCode: "ntext",
            mapToDmd: DmdFieldTypeId.USTRING); // Convert to DMD ustring(max) data type


    // Numeric types
    public static readonly SqlFieldType INT =
        new(id: SqlFieldTypeId.INT,
            sqlCode: "int",
            mapToDmd: DmdFieldTypeId.INT);

    public static readonly SqlFieldType BIGINT =
        new(id: SqlFieldTypeId.BIGINT,
            sqlCode: "bigint",
            mapToDmd: DmdFieldTypeId.LONG);

    public static readonly SqlFieldType DECIMAL =
        new(id: SqlFieldTypeId.DECIMAL,
            sqlCode: "decimal",
            mapToDmd: DmdFieldTypeId.DECIMAL,
            precisionType: PrecisionType.PrecisionWithScaleRequired,
            defaultPrecision: 18,
            defaultScale: 0);

    public static readonly SqlFieldType NUMERIC =
        new(id: SqlFieldTypeId.NUMERIC,
            sqlCode: "numeric",
            mapToDmd: DmdFieldTypeId.DECIMAL, // Convert to DMD decimal(p,s) data type
            precisionType: PrecisionType.PrecisionWithScaleRequired,
            defaultPrecision: 18,
            defaultScale: 0); 

    public static readonly SqlFieldType FLOAT =
        new(id: SqlFieldTypeId.FLOAT,
            sqlCode: "float",
            mapToDmd: DmdFieldTypeId.FLOAT);

    public static readonly SqlFieldType MONEY =
        new(id: SqlFieldTypeId.MONEY,
            sqlCode: "money",
            mapToDmd: DmdFieldTypeId.DECIMAL, // Convert to DMD decimal(19,4) data type
            precisionType: PrecisionType.None,
            defaultPrecision: 19,
            defaultScale: 4);

    public static readonly SqlFieldType SMALLMONEY =
        new(id: SqlFieldTypeId.SMALLMONEY,
            sqlCode: "smallmoney",
            mapToDmd: DmdFieldTypeId.DECIMAL, // Convert to DMD decimal(10,4) data type
            precisionType: PrecisionType.None,
            defaultPrecision: 10,
            defaultScale: 4);

    public static readonly SqlFieldType DATETIME =
        new(id: SqlFieldTypeId.DATETIME,
            sqlCode: "datetime",
            mapToDmd: DmdFieldTypeId.DATETIME);

}
