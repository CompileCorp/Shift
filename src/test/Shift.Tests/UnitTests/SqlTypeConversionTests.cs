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
        var supported = SqlTypeConversion.IsSupportedInPlaceConversion(fromType, null, "varchar", out var width);

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
    [InlineData("VarChar", "NVARCHAR")]
    public void IsSupportedInPlaceConversion_WithMixedCase_ShouldBeSupported(string fromType, string toType)
    {
        SqlTypeConversion.IsSupportedInPlaceConversion(fromType, 50, toType).Should().BeTrue();
    }

    /// <summary>
    /// Tests that varchar to nvarchar is on the allow-list, reporting the source's own width as the
    /// width the target has to carry. Every ASCII string is a valid unicode string, so this is the
    /// one string-to-string conversion where only width is in question — and it is what a dmd field
    /// flipping from astring to ustring asks for.
    /// </summary>
    [Theory]
    [InlineData(50, 50)]
    [InlineData(1, 1)]
    [InlineData(8000, 8000)]
    [InlineData(-1, -1)]   // a MAX source needs a MAX target
    [InlineData(null, -1)] // an unknown source width is treated as unbounded
    public void IsSupportedInPlaceConversion_WithVarcharToNvarchar_ShouldReportSourceWidth(
        int? fromPrecision, int expectedWidth)
    {
        // Act
        var supported = SqlTypeConversion.IsSupportedInPlaceConversion("varchar", fromPrecision, "nvarchar", out var width);

        // Assert
        supported.Should().BeTrue();
        width.Should().Be(expectedWidth);
    }

    /// <summary>
    /// Tests that the reverse direction stays off the allow-list. SQL Server performs
    /// nvarchar to varchar without complaint, replacing every character outside the target
    /// collation's code page with '?', so it loses data exactly as quietly as a too-narrow integer.
    /// </summary>
    [Fact]
    public void IsSupportedInPlaceConversion_WithNvarcharToVarchar_ShouldBeFalse()
    {
        SqlTypeConversion.IsSupportedInPlaceConversion("nvarchar", 50, "varchar").Should().BeFalse();
    }

    /// <summary>
    /// Tests the shape of the allow-list: only variable-width string targets qualify, and only
    /// integer or varchar sources. Fixed-width targets are excluded because SQL Server right-pads
    /// them.
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
    [InlineData("nvarchar", "varchar")]
    [InlineData("varchar", "char")]
    [InlineData("char", "varchar")]
    [InlineData("text", "varchar")]
    [InlineData("decimal", "varchar")]
    [InlineData("datetime", "varchar")]
    [InlineData("bit", "varchar")]
    [InlineData("float", "varchar")]
    [InlineData("uniqueidentifier", "varchar")]
    public void IsSupportedInPlaceConversion_WithUnsupportedPair_ShouldBeFalse(string fromType, string toType)
    {
        SqlTypeConversion.IsSupportedInPlaceConversion(fromType, 50, toType).Should().BeFalse();
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
    [InlineData("nvarchar", "varchar")]
    [InlineData("datetime", "varchar")]
    public void IsSupportedInPlaceConversion_WhenRejected_ShouldReportZeroWidth(string fromType, string toType)
    {
        // Act
        var supported = SqlTypeConversion.IsSupportedInPlaceConversion(fromType, 50, toType, out var width);

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
        SqlTypeConversion.IsSupportedInPlaceConversion(fromType, null, toType).Should().BeFalse();
    }

    /// <summary>
    /// Tests which conversions have to have their width guaranteed from the source type. An integer
    /// conversion fails open — SQL Server stores '*' rather than raising — so the planner must
    /// refuse a narrow target outright. A string widening fails closed, so it is safe to leave to
    /// the runner's live-data probe.
    /// </summary>
    [Theory]
    [InlineData("tinyint", true)]
    [InlineData("smallint", true)]
    [InlineData("int", true)]
    [InlineData("bigint", true)]
    [InlineData("INT", true)]
    [InlineData("varchar", false)]
    [InlineData("nvarchar", false)]
    [InlineData("decimal", false)]
    [InlineData("datetime", false)]
    public void FailsOpenOnNarrowTarget_ShouldBeTrueOnlyForIntegerSources(string fromType, bool expected)
    {
        SqlTypeConversion.FailsOpenOnNarrowTarget(fromType).Should().Be(expected);
    }

    /// <summary>
    /// Tests that the target's width is judged on what the field will actually be created at.
    /// </summary>
    [Theory]
    [InlineData(50, 11, true, 50)]    // comfortably wide
    [InlineData(11, 11, true, 11)]    // exactly wide enough
    [InlineData(10, 11, false, 10)]   // one short
    [InlineData(-1, 20, true, -1)]    // MAX holds anything
    [InlineData(8000, -1, false, 8000)] // an unbounded source needs an unbounded target
    [InlineData(-1, -1, true, -1)]
    public void IsTargetWideEnough_WithDeclaredPrecision_ShouldCompareAgainstIt(
        int targetPrecision, int requiredWidth, bool expected, int expectedEffectiveWidth)
    {
        // Act
        var wideEnough = SqlTypeConversion.IsTargetWideEnough(
            Field("varchar", targetPrecision), requiredWidth, out var effectiveWidth);

        // Assert
        wideEnough.Should().Be(expected);
        effectiveWidth.Should().Be(expectedEffectiveWidth);
    }

    /// <summary>
    /// Tests that a field declaring no precision is judged against the width it will actually be
    /// created at — the type's default — rather than skipping the check. This is the guard standing
    /// between a bigint and a silent '*', so it cannot be waived just because the dmd field left
    /// its size off.
    /// </summary>
    [Theory]
    [InlineData("varchar", 20, true, 255)]
    [InlineData("nvarchar", 20, true, 255)]
    [InlineData("varchar", 300, false, 255)]
    public void IsTargetWideEnough_WithoutDeclaredPrecision_ShouldUseTheTypeDefault(
        string targetType, int requiredWidth, bool expected, int expectedEffectiveWidth)
    {
        // Act
        var wideEnough = SqlTypeConversion.IsTargetWideEnough(
            Field(targetType, precision: null), requiredWidth, out var effectiveWidth);

        // Assert
        wideEnough.Should().Be(expected);
        effectiveWidth.Should().Be(expectedEffectiveWidth);
    }

    /// <summary>
    /// Tests that a type carrying neither a precision nor a default is refused rather than waved
    /// through, so an unrecognised target cannot bypass the width guard.
    /// </summary>
    [Fact]
    public void IsTargetWideEnough_WithUnknownTypeAndNoPrecision_ShouldBeFalse()
    {
        // Act
        var wideEnough = SqlTypeConversion.IsTargetWideEnough(
            Field("geography", precision: null), requiredWidth: 11, out var effectiveWidth);

        // Assert
        wideEnough.Should().BeFalse();
        effectiveWidth.Should().Be(0);
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

    /// <summary>
    /// Tests the types SQL Server permits an IDENTITY column to have. This decides whether the
    /// IDENTITY property blocks a conversion: converting an identity column to one of these
    /// succeeds, so refusing it would strand the column.
    /// </summary>
    [Theory]
    [InlineData("tinyint", null)]
    [InlineData("smallint", null)]
    [InlineData("int", null)]
    [InlineData("bigint", null)]
    [InlineData("decimal", 0)]
    [InlineData("numeric", 0)]
    [InlineData("DECIMAL", 0)]
    [InlineData("decimal", null)]   // an absent scale means 0
    public void CanBeIdentity_WithIdentityCapableType_ShouldBeTrue(string type, int? scale)
    {
        SqlTypeConversion.CanBeIdentity(Field(type, precision: 19, scale: scale)).Should().BeTrue();
    }

    /// <summary>
    /// Tests that everything else is rejected, including decimal and numeric carrying a scale,
    /// which SQL Server refuses with error 2749.
    /// </summary>
    [Theory]
    [InlineData("decimal", 2)]
    [InlineData("numeric", 4)]
    [InlineData("varchar", null)]
    [InlineData("nvarchar", null)]
    [InlineData("float", null)]
    [InlineData("datetime", null)]
    [InlineData("bit", null)]
    public void CanBeIdentity_WithNonIdentityCapableType_ShouldBeFalse(string type, int? scale)
    {
        SqlTypeConversion.CanBeIdentity(Field(type, precision: 19, scale: scale)).Should().BeFalse();
    }

    /// <summary>
    /// Tests that a nullable target is rejected whatever its type. SQL Server will not carry an
    /// IDENTITY on a nullable column and fails with error 8147, so a model declaring an identity
    /// column nullable must be caught here rather than at execution time.
    /// </summary>
    [Theory]
    [InlineData("int", null)]
    [InlineData("bigint", null)]
    [InlineData("decimal", 0)]
    [InlineData("numeric", 0)]
    public void CanBeIdentity_WithNullableTarget_ShouldBeFalse(string type, int? scale)
    {
        SqlTypeConversion.CanBeIdentity(Field(type, precision: 19, scale: scale, isNullable: true)).Should().BeFalse();
    }

    private static FieldModel Field(string type, int? precision = null, int? scale = null, bool isNullable = false) =>
        new() { Name = "Code", Type = type, Precision = precision, Scale = scale, IsNullable = isNullable };
}