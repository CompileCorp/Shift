using Compile.Shift.Ef;
using Compile.Shift.Model;
using FluentAssertions;

namespace Compile.Shift.Ef.Tests;

/// <summary>
/// Tests for <see cref="TypeMapper"/>.
///
/// Intent (derived from the type dictionary and MapToCSharpType):
///  - Each SQL Server type maps to a specific C# type.
///  - Mapping is case-insensitive and ignores size specifiers e.g. varchar(50).
///  - Nullable/optional VALUE types get a trailing '?'; reference types (string, byte[]) do not.
///  - Unknown types fall back to string.
/// </summary>
public class TypeMapperTests
{
    private readonly TypeMapper _sut = new();

    private static FieldModel Field(string type, bool nullable = false, bool optional = false) =>
        new() { Name = "Col", Type = type, IsNullable = nullable, IsOptional = optional };

    [Theory]
    // integers
    [InlineData("bit", "bool")]
    [InlineData("tinyint", "byte")]
    [InlineData("smallint", "short")]
    [InlineData("int", "int")]
    [InlineData("bigint", "long")]
    // decimals / floating point
    [InlineData("decimal", "decimal")]
    [InlineData("numeric", "decimal")]
    [InlineData("money", "decimal")]
    [InlineData("smallmoney", "decimal")]
    [InlineData("float", "double")]
    [InlineData("real", "float")]
    // strings
    [InlineData("char", "string")]
    [InlineData("varchar", "string")]
    [InlineData("text", "string")]
    [InlineData("nchar", "string")]
    [InlineData("nvarchar", "string")]
    [InlineData("ntext", "string")]
    [InlineData("xml", "string")]
    // date/time
    [InlineData("datetime", "DateTime")]
    [InlineData("datetime2", "DateTime")]
    [InlineData("smalldatetime", "DateTime")]
    [InlineData("date", "DateTime")]
    [InlineData("time", "TimeSpan")]
    [InlineData("datetimeoffset", "DateTimeOffset")]
    // binary
    [InlineData("binary", "byte[]")]
    [InlineData("varbinary", "byte[]")]
    [InlineData("image", "byte[]")]
    [InlineData("timestamp", "byte[]")]
    [InlineData("rowversion", "byte[]")]
    // other
    [InlineData("uniqueidentifier", "Guid")]
    [InlineData("sql_variant", "object")]
    public void MapToCSharpType_NonNullable_MapsToExpectedType(string sqlType, string expected)
    {
        _sut.MapToCSharpType(Field(sqlType)).Should().Be(expected);
    }

    [Fact]
    public void MapToCSharpType_IsCaseInsensitive()
    {
        _sut.MapToCSharpType(Field("INT")).Should().Be("int");
        _sut.MapToCSharpType(Field("NVarChar")).Should().Be("string");
    }

    [Fact]
    public void MapToCSharpType_StripsSizeSpecifier()
    {
        _sut.MapToCSharpType(Field("varchar(50)")).Should().Be("string");
        _sut.MapToCSharpType(Field("decimal(18,2)")).Should().Be("decimal");
    }

    [Fact]
    public void MapToCSharpType_UnknownType_FallsBackToString()
    {
        _sut.MapToCSharpType(Field("geography")).Should().Be("string");
    }

    [Theory]
    [InlineData("int", "int?")]
    [InlineData("bigint", "long?")]
    [InlineData("bit", "bool?")]
    [InlineData("decimal", "decimal?")]
    [InlineData("datetime", "DateTime?")]
    [InlineData("uniqueidentifier", "Guid?")]
    [InlineData("time", "TimeSpan?")]
    [InlineData("datetimeoffset", "DateTimeOffset?")]
    public void MapToCSharpType_NullableValueType_AppendsQuestionMark(string sqlType, string expected)
    {
        _sut.MapToCSharpType(Field(sqlType, nullable: true)).Should().Be(expected);
    }

    [Fact]
    public void MapToCSharpType_OptionalValueType_AppendsQuestionMark()
    {
        // IsOptional is treated the same as IsNullable for nullability purposes.
        _sut.MapToCSharpType(Field("int", optional: true)).Should().Be("int?");
    }

    [Theory]
    [InlineData("nvarchar")]
    [InlineData("varchar")]
    [InlineData("varbinary")]
    public void MapToCSharpType_NullableReferenceType_DoesNotAppendQuestionMark(string sqlType)
    {
        // Documents current behaviour: reference types are not annotated nullable even when the
        // column is nullable. EF treats a non-annotated reference type as optional by default,
        // so this is acceptable, but worth pinning so a future NRT change is a conscious decision.
        var result = _sut.MapToCSharpType(Field(sqlType, nullable: true));
        result.Should().NotEndWith("?");
    }
}