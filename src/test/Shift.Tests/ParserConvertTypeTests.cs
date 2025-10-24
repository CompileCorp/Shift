using Compile.Shift.Model;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Tests;

/// <summary>
/// Comprehensive test harness for Parser.ConvertType method.
/// Tests all type conversion scenarios, edge cases, and field property preservation.
/// </summary>
public class ParserConvertTypeTests : UnitTestContext<Parser>
{
    #region Basic Type Conversion Tests

    /// <summary>
    /// Tests that 'bool' type is correctly converted to 'bit' SQL type.
    /// </summary>
    [Fact]
    public void ConvertType_Bool_ShouldConvertToBit()
    {
        // Arrange
        var field = CreateFieldModel("bool");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("bit");
        field.Name.Should().Be("TestField"); // Name should be preserved
        field.IsNullable.Should().BeFalse(); // Other properties should be preserved
    }

    /// <summary>
    /// Tests that 'string' type with precision -1 (max) converts to 'nvarchar'.
    /// </summary>
    [Fact]
    public void ConvertType_StringWithMaxPrecision_ShouldConvertToNvarchar()
    {
        // Arrange
        var field = CreateFieldModel("string", precision: -1);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("nvarchar");
        field.Precision.Should().Be(-1);
    }

    /// <summary>
    /// Tests that 'string' type with precision 1 converts to 'nchar'.
    /// </summary>
    [Fact]
    public void ConvertType_StringWithPrecisionOne_ShouldConvertToNchar()
    {
        // Arrange
        var field = CreateFieldModel("string", precision: 1);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("nchar");
        field.Precision.Should().Be(1);
    }

    /// <summary>
    /// Tests that 'string' type with precision > 1 converts to 'nvarchar'.
    /// </summary>
    [Fact]
    public void ConvertType_StringWithPrecisionGreaterThanOne_ShouldConvertToNvarchar()
    {
        // Arrange
        var field = CreateFieldModel("string", precision: 100);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("nvarchar");
        field.Precision.Should().Be(100);
    }

    /// <summary>
    /// Tests that 'astring' type with precision -1 (max) converts to 'varchar'.
    /// </summary>
    [Fact]
    public void ConvertType_AstringWithMaxPrecision_ShouldConvertToVarchar()
    {
        // Arrange
        var field = CreateFieldModel("astring", precision: -1);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("varchar");
        field.Precision.Should().Be(-1);
    }

    /// <summary>
    /// Tests that 'astring' type with precision 1 converts to 'char'.
    /// </summary>
    [Fact]
    public void ConvertType_AstringWithPrecisionOne_ShouldConvertToChar()
    {
        // Arrange
        var field = CreateFieldModel("astring", precision: 1);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("char");
        field.Precision.Should().Be(1);
    }

    /// <summary>
    /// Tests that 'astring' type with precision > 1 converts to 'varchar'.
    /// </summary>
    [Fact]
    public void ConvertType_AstringWithPrecisionGreaterThanOne_ShouldConvertToVarchar()
    {
        // Arrange
        var field = CreateFieldModel("astring", precision: 50);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("varchar");
        field.Precision.Should().Be(50);
    }

    /// <summary>
    /// Tests that 'char' type converts to 'nchar'.
    /// </summary>
    [Fact]
    public void ConvertType_Char_ShouldConvertToNchar()
    {
        // Arrange
        var field = CreateFieldModel("char");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("nchar");
    }

    /// <summary>
    /// Tests that 'achar' type converts to 'char'.
    /// </summary>
    [Fact]
    public void ConvertType_Achar_ShouldConvertToChar()
    {
        // Arrange
        var field = CreateFieldModel("achar");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("char");
    }

    /// <summary>
    /// Tests that 'long' type converts to 'bigint'.
    /// </summary>
    [Fact]
    public void ConvertType_Long_ShouldConvertToBigint()
    {
        // Arrange
        var field = CreateFieldModel("long");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("bigint");
    }

    /// <summary>
    /// Tests that 'guid' type converts to 'uniqueidentifier'.
    /// </summary>
    [Fact]
    public void ConvertType_Guid_ShouldConvertToUniqueidentifier()
    {
        // Arrange
        var field = CreateFieldModel("guid");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("uniqueidentifier");
    }

    /// <summary>
    /// Tests that 'guid' type with precision/scale is handled correctly (should ignore precision/scale).
    /// </summary>
    [Fact]
    public void ConvertType_GuidWithPrecisionAndScale_ShouldConvertToUniqueidentifier()
    {
        // Arrange
        var field = CreateFieldModel("guid", precision: 10, scale: 2);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("uniqueidentifier");
        field.Precision.Should().Be(10); // Precision should be preserved
        field.Scale.Should().Be(2); // Scale should be preserved
    }

    /// <summary>
    /// Tests that 'guid' type preserves all field properties correctly.
    /// </summary>
    [Fact]
    public void ConvertType_Guid_ShouldPreserveAllFieldProperties()
    {
        // Arrange
        var field = CreateFieldModel("guid", precision: 5, scale: 1);
        field.IsPrimaryKey = true;
        field.IsIdentity = true;
        field.IsNullable = true;
        field.IsOptional = true;

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("uniqueidentifier");
        field.Name.Should().Be("TestField");
        field.Precision.Should().Be(5);
        field.Scale.Should().Be(1);
        field.IsPrimaryKey.Should().BeTrue();
        field.IsIdentity.Should().BeTrue();
        field.IsNullable.Should().BeTrue();
        field.IsOptional.Should().BeTrue();
    }

    /// <summary>
    /// Tests that 'float' type converts to 'float'.
    /// </summary>
    [Fact]
    public void ConvertType_Float_ShouldConvertToFloat()
    {
        // Arrange
        var field = CreateFieldModel("float");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("float");
    }

    /// <summary>
    /// Tests that 'datetime' type converts to 'datetime'.
    /// </summary>
    [Fact]
    public void ConvertType_Datetime_ShouldConvertToDatetime()
    {
        // Arrange
        var field = CreateFieldModel("datetime");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("datetime");
    }

    /// <summary>
    /// Tests that 'int' type remains unchanged.
    /// </summary>
    [Fact]
    public void ConvertType_Int_ShouldRemainUnchanged()
    {
        // Arrange
        var field = CreateFieldModel("int");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("int");
    }

    #endregion

    #region Decimal Type Conversion Tests

    /// <summary>
    /// Tests that 'decimal' type with precision 19 and scale 4 converts to 'money'.
    /// </summary>
    [Fact]
    public void ConvertType_DecimalWithMoneyPrecision_ShouldConvertToMoney()
    {
        // Arrange
        var field = CreateFieldModel("decimal", precision: 19, scale: 4);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("money");
        field.Precision.Should().Be(19);
        field.Scale.Should().Be(4);
    }

    /// <summary>
    /// Tests that 'decimal' type with precision 10 and scale 4 converts to 'smallmoney'.
    /// </summary>
    [Fact]
    public void ConvertType_DecimalWithSmallMoneyPrecision_ShouldConvertToSmallmoney()
    {
        // Arrange
        var field = CreateFieldModel("decimal", precision: 10, scale: 4);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("smallmoney");
        field.Precision.Should().Be(10);
        field.Scale.Should().Be(4);
    }

    /// <summary>
    /// Tests that 'decimal' type with other precision/scale combinations remains 'decimal'.
    /// </summary>
    [Theory]
    [InlineData(18, 2)]
    [InlineData(10, 2)]
    [InlineData(5, 0)]
    [InlineData(20, 6)]
    public void ConvertType_DecimalWithOtherPrecision_ShouldRemainDecimal(int precision, int scale)
    {
        // Arrange
        var field = CreateFieldModel("decimal", precision: precision, scale: scale);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("decimal");
        field.Precision.Should().Be(precision);
        field.Scale.Should().Be(scale);
    }

    /// <summary>
    /// Tests that 'decimal' type without precision/scale remains 'decimal'.
    /// </summary>
    [Fact]
    public void ConvertType_DecimalWithoutPrecision_ShouldRemainDecimal()
    {
        // Arrange
        var field = CreateFieldModel("decimal");

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be("decimal");
        field.Precision.Should().BeNull();
        field.Scale.Should().BeNull();
    }

    #endregion

    #region Case Sensitivity Tests

    /// <summary>
    /// Tests that type conversion is case-insensitive for all supported types.
    /// </summary>
    [Theory]
    [InlineData("BOOL", "bit")]
    [InlineData("String", "nvarchar")]
    [InlineData("ASTRING", "varchar")]
    [InlineData("CHAR", "nchar")]
    [InlineData("ACHAR", "char")]
    [InlineData("LONG", "bigint")]
    [InlineData("GUID", "uniqueidentifier")]
    [InlineData("DECIMAL", "decimal")]
    [InlineData("INT", "int")]
    [InlineData("FLOAT", "float")]
    [InlineData("DATETIME", "datetime")]
    public void ConvertType_CaseInsensitive_ShouldConvertCorrectly(string inputType, string expectedType)
    {
        // Arrange
        var field = CreateFieldModel(inputType);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be(expectedType);
    }

    #endregion

    #region Field Property Preservation Tests

    /// <summary>
    /// Tests that ConvertType preserves all FieldModel properties except Type.
    /// </summary>
    [Fact]
    public void ConvertType_ShouldPreserveAllFieldProperties()
    {
        // Arrange
        var field = new FieldModel
        {
            Name = "TestField",
            Type = "string",
            IsNullable = true,
            IsOptional = true,
            IsPrimaryKey = true,
            IsIdentity = true,
            Precision = 100,
            Scale = 2
        };

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Name.Should().Be("TestField");
        field.IsNullable.Should().BeTrue();
        field.IsOptional.Should().BeTrue();
        field.IsPrimaryKey.Should().BeTrue();
        field.IsIdentity.Should().BeTrue();
        field.Precision.Should().Be(100);
        field.Scale.Should().Be(2);
        // Only Type should change
        field.Type.Should().Be("nvarchar");
    }

    /// <summary>
    /// Tests that ConvertType preserves null precision and scale values.
    /// </summary>
    [Fact]
    public void ConvertType_ShouldPreserveNullPrecisionAndScale()
    {
        // Arrange
        var field = new FieldModel
        {
            Name = "TestField",
            Type = "bool",
            Precision = null,
            Scale = null
        };

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Precision.Should().BeNull();
        field.Scale.Should().BeNull();
        field.Type.Should().Be("bit");
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    /// <summary>
    /// Tests that ConvertType handles unsupported types by leaving them unchanged.
    /// </summary>
    [Theory]
    [InlineData("unsupported")]
    [InlineData("customtype")]
    [InlineData("")]
    [InlineData(" ")]
    public void ConvertType_UnsupportedTypes_ShouldLeaveUnchanged(string unsupportedType)
    {
        // Arrange
        var field = CreateFieldModel(unsupportedType);

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be(unsupportedType);
    }

    /// <summary>
    /// Tests that ConvertType handles null field gracefully.
    /// </summary>
    [Fact]
    public void ConvertType_NullField_ShouldThrowNullReferenceException()
    {
        // Act & Assert
        var act = () => Sut.ConvertType(null!);
        act.Should().Throw<NullReferenceException>();
    }

    /// <summary>
    /// Tests that ConvertType handles field with empty type gracefully.
    /// </summary>
    [Fact]
    public void ConvertType_EmptyType_ShouldHandleGracefully()
    {
        // Arrange
        var field = new FieldModel { Name = string.Empty, Type = string.Empty };

        // Act
        Sut.ConvertType(field);

        // Assert
        field.Type.Should().Be(string.Empty);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a FieldModel with the specified type and optional precision/scale.
    /// </summary>
    private static FieldModel CreateFieldModel(string type, int? precision = null, int? scale = null)
    {
        return new FieldModel
        {
            Name = "TestField",
            Type = type,
            IsNullable = false,
            IsOptional = false,
            IsPrimaryKey = false,
            IsIdentity = false,
            Precision = precision,
            Scale = scale
        };
    }

    #endregion
}
