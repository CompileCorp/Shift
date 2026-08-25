using Compile.Shift.Helpers;
using Compile.Shift.Model;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Tests the allow-list that both the planner and the runner consult. It is the single source of
/// truth for which base-type changes Shift will apply, so its edges are worth pinning down
/// directly rather than only through the two callers.
/// </summary>
public class SqlTypeConversionTests
{
    /// <summary>
    /// Tests that each integer type reports the width of its widest rendering, sign included.
    /// </summary>
    [Theory]
    [InlineData("tinyint", 3)]
    [InlineData("smallint", 6)]
    [InlineData("int", 11)]
    [InlineData("bigint", 20)]
    public void IsSupportedInPlaceConversion_WithIntegerSource_ShouldReportRenderedWidth(string fromType, int expectedWidth)
    {
        // Act
        var supported = SqlTypeConversion.IsSupportedInPlaceConversion(fromType, "varchar", out var width);

        // Assert
        supported.Should().BeTrue();
        width.Should().Be(expectedWidth);
    }

    /// <summary>
    /// Tests that type names are matched case-insensitively, since they arrive from both dmd files
    /// and SQL Server metadata.
    /// </summary>
    [Theory]
    [InlineData("INT", "VARCHAR")]
    [InlineData("Int", "NVarChar")]
    [InlineData("BigInt", "varchar")]
    public void IsSupportedInPlaceConversion_WithMixedCase_ShouldBeSupported(string fromType, string toType)
    {
        SqlTypeConversion.IsSupportedInPlaceConversion(fromType, toType).Should().BeTrue();
    }

    /// <summary>
    /// Tests the shape of the allow-list: only variable-width string targets qualify, and only
    /// integer sources. Fixed-width targets are excluded because SQL Server right-pads them.
    /// </summary>
    [Theory]
    [InlineData("int", "char")]
    [InlineData("int", "nchar")]
    [InlineData("int", "binary")]
    [InlineData("int", "varbinary")]
    [InlineData("int", "decimal")]
    [InlineData("int", "bigint")]
    [InlineData("varchar", "int")]
    [InlineData("nvarchar", "int")]
    [InlineData("decimal", "varchar")]
    [InlineData("datetime", "varchar")]
    [InlineData("bit", "varchar")]
    [InlineData("float", "varchar")]
    [InlineData("uniqueidentifier", "varchar")]
    public void IsSupportedInPlaceConversion_WithUnsupportedPair_ShouldBeFalse(string fromType, string toType)
    {
        SqlTypeConversion.IsSupportedInPlaceConversion(fromType, toType).Should().BeFalse();
    }

    /// <summary>
    /// Tests that the reported width is zero whenever the conversion is rejected, including when
    /// the source is an integer but the target is not a variable-width string. A caller reading
    /// the width on a false return would otherwise get a plausible-looking number for a conversion
    /// that is never going to happen.
    /// </summary>
    [Theory]
    [InlineData("int", "char")]
    [InlineData("bigint", "nchar")]
    [InlineData("varchar", "int")]
    [InlineData("datetime", "varchar")]
    public void IsSupportedInPlaceConversion_WhenRejected_ShouldReportZeroWidth(string fromType, string toType)
    {
        // Act
        var supported = SqlTypeConversion.IsSupportedInPlaceConversion(fromType, toType, out var width);

        // Assert
        supported.Should().BeFalse();
        width.Should().Be(0);
    }

    /// <summary>
    /// Tests that an unknown type name is simply unsupported rather than throwing.
    /// </summary>
    [Theory]
    [InlineData("geography", "varchar")]
    [InlineData("int", "sql_variant")]
    [InlineData("", "varchar")]
    public void IsSupportedInPlaceConversion_WithUnknownType_ShouldBeFalse(string fromType, string toType)
    {
        SqlTypeConversion.IsSupportedInPlaceConversion(fromType, toType).Should().BeFalse();
    }

    /// <summary>
    /// Tests that a target which is exactly Shift's own round-trip of the actual type is treated as
    /// equivalent. The actual precisions are the ones SQL Server reports for these types.
    /// </summary>
    [Theory]
    [InlineData("text", 2147483647, null, "varchar", -1, null)]
    [InlineData("ntext", 1073741823, null, "nvarchar", -1, null)]
    [InlineData("money", 19, 4, "decimal", 19, 4)]
    [InlineData("smallmoney", 10, 4, "decimal", 10, 4)]
    public void IsRoundTripEquivalent_WithExactRoundTrip_ShouldBeTrue(
        string actualType, int? actualPrecision, int? actualScale,
        string targetType, int? targetPrecision, int? targetScale)
    {
        // Arrange
        var actual = Field(actualType, actualPrecision, actualScale);
        var target = Field(targetType, targetPrecision, targetScale);

        // Act & Assert
        SqlTypeConversion.IsRoundTripEquivalent(actual, target).Should().BeTrue();
    }

    /// <summary>
    /// Tests that the same dmd type at a different width or scale is not equivalent, so genuine
    /// drift is not swallowed along with the round-trip noise.
    /// </summary>
    [Theory]
    [InlineData("text", 2147483647, null, "varchar", 50, null)]
    [InlineData("ntext", 1073741823, null, "nvarchar", 50, null)]
    [InlineData("money", 19, 4, "decimal", 18, 4)]
    [InlineData("money", 19, 4, "decimal", 19, 2)]
    [InlineData("smallmoney", 10, 4, "decimal", 19, 4)]
    public void IsRoundTripEquivalent_WithSameDmdTypeButDifferentPrecision_ShouldBeFalse(
        string actualType, int? actualPrecision, int? actualScale,
        string targetType, int? targetPrecision, int? targetScale)
    {
        // Arrange
        var actual = Field(actualType, actualPrecision, actualScale);
        var target = Field(targetType, targetPrecision, targetScale);

        // Act & Assert
        SqlTypeConversion.IsRoundTripEquivalent(actual, target).Should().BeFalse();
    }

    /// <summary>
    /// Tests that unrelated types are never equivalent.
    /// </summary>
    [Theory]
    [InlineData("int", "varchar")]
    [InlineData("datetime", "nvarchar")]
    [InlineData("bit", "int")]
    public void IsRoundTripEquivalent_WithUnrelatedTypes_ShouldBeFalse(string actualType, string targetType)
    {
        SqlTypeConversion.IsRoundTripEquivalent(Field(actualType), Field(targetType, 50)).Should().BeFalse();
    }

    private static FieldModel Field(string type, int? precision = null, int? scale = null) =>
        new() { Name = "Code", Type = type, Precision = precision, Scale = scale };
}