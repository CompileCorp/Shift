using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;
using FluentAssertions;
using static Compile.Shift.Dbml.Tests.TestModels;

namespace Compile.Shift.Dbml.Tests;

/// <summary>
/// Tests for <see cref="DbmlTypeMapper"/>.
///
/// Intent: DBML passes column types straight through to the diagram, so the rendered type is the
/// SQL Server spelling the column will actually have — the SQL type code with whichever precision
/// suffix that type takes, and (max) where the model carries the max-length marker.
/// </summary>
public class DbmlTypeMapperTests
{
    private readonly DbmlTypeMapper _sut = new();

    [Theory]
    // No precision suffix.
    [InlineData("bit", null, null, "bit")]
    [InlineData("uniqueidentifier", null, null, "uniqueidentifier")]
    [InlineData("int", null, null, "int")]
    [InlineData("bigint", null, null, "bigint")]
    [InlineData("float", null, null, "float")]
    [InlineData("datetime", null, null, "datetime")]
    // money/smallmoney carry a fixed precision in SQL, so the type name says it all.
    [InlineData("money", null, null, "money")]
    [InlineData("smallmoney", null, null, "smallmoney")]
    // text/ntext have no length in SQL.
    [InlineData("text", null, null, "text")]
    [InlineData("ntext", null, null, "ntext")]
    // Precision only, falling back to the type's default when the model has none.
    [InlineData("char", 10, null, "char(10)")]
    [InlineData("char", null, null, "char(1)")]
    [InlineData("varchar", 100, null, "varchar(100)")]
    [InlineData("varchar", null, null, "varchar(255)")]
    [InlineData("nchar", 5, null, "nchar(5)")]
    [InlineData("nvarchar", 256, null, "nvarchar(256)")]
    // The -1 marker means MAX.
    [InlineData("varchar", -1, null, "varchar(max)")]
    [InlineData("nvarchar", -1, null, "nvarchar(max)")]
    // Precision with scale.
    [InlineData("decimal", 10, 2, "decimal(10,2)")]
    [InlineData("decimal", null, null, "decimal(18,0)")]
    [InlineData("numeric", 12, 4, "numeric(12,4)")]
    public void TryMapToDbmlType_KnownTypes_RenderTheSqlSpelling(string type, int? precision, int? scale, string expected)
    {
        var recognised = _sut.TryMapToDbmlType(Field("Column", type, precision: precision, scale: scale), out var dbmlType);

        recognised.Should().BeTrue();
        dbmlType.Should().Be(expected);
    }

    /// <summary>
    /// Guards the theory above: every SqlFieldType Shift models must be recognised by the mapper, so
    /// adding a type forces a rendering decision here rather than silently falling through to the
    /// verbatim path.
    /// </summary>
    [Fact]
    public void TryMapToDbmlType_EverySqlFieldType_IsRecognised()
    {
        foreach (var sqlFieldType in Vnum.GetAll<SqlFieldType>())
        {
            var recognised = _sut.TryMapToDbmlType(Field("Column", sqlFieldType.Code), out var dbmlType);

            recognised.Should().BeTrue($"{sqlFieldType.Code} should be recognised");
            dbmlType.Should().StartWith(sqlFieldType.Code);
        }
    }

    [Theory]
    [InlineData("geography", "geography")]
    [InlineData("hierarchyid", "hierarchyid")]
    // DBML requires a type containing whitespace to be double-quoted.
    [InlineData("user defined", "\"user defined\"")]
    public void TryMapToDbmlType_UnknownType_IsPassedThroughAndReported(string type, string expected)
    {
        var recognised = _sut.TryMapToDbmlType(Field("Column", type), out var dbmlType);

        recognised.Should().BeFalse();
        dbmlType.Should().Be(expected);
    }
}