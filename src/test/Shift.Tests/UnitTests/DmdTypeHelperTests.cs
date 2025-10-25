using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
using Compile.Shift.Model.Vnums;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Comprehensive test harness for ModelExporter.GetFieldTypeString method.
/// Tests all precision type scenarios, edge cases, and field property handling.
/// </summary>
public class DmdTypeHelperTests
{
    #region TEXT/NTEXT Max Length Tests

    /// <summary>
    /// Tests that TEXT type always returns with (max) regardless of precision.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_TextType_ShouldAlwaysReturnMax()
    {
        // Arrange
        var field = CreateFieldModel("text", precision: 100);
        var sqlFieldType = SqlFieldType.TEXT;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("astring(max)");
    }

    /// <summary>
    /// Tests that NTEXT type always returns with (max) regardless of precision.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_NtextType_ShouldAlwaysReturnMax()
    {
        // Arrange
        var field = CreateFieldModel("ntext", precision: 50);
        var sqlFieldType = SqlFieldType.NTEXT;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("string(max)");
    }

    #endregion

    #region Max Length Marker Tests

    /// <summary>
    /// Tests that VARCHAR with max length marker (-1) returns (max).
    /// </summary>
    [Fact]
    public void GetDmdTypeString_VarcharWithMaxLengthMarker_ShouldReturnMax()
    {
        // Arrange
        var field = CreateFieldModel("varchar", precision: -1);
        var sqlFieldType = SqlFieldType.VARCHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("astring(max)");
    }

    /// <summary>
    /// Tests that NVARCHAR with max length marker (-1) returns (max).
    /// </summary>
    [Fact]
    public void GetDmdTypeString_NvarcharWithMaxLengthMarker_ShouldReturnMax()
    {
        // Arrange
        var field = CreateFieldModel("nvarchar", precision: -1);
        var sqlFieldType = SqlFieldType.NVARCHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("string(max)");
    }

    /// <summary>
    /// Tests that VARCHAR with regular precision returns normal format.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_VarcharWithRegularPrecision_ShouldReturnNormalFormat()
    {
        // Arrange
        var field = CreateFieldModel("varchar", precision: 100);
        var sqlFieldType = SqlFieldType.VARCHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("astring(100)");
    }

    #endregion

    #region PrecisionType.None Tests

    /// <summary>
    /// Tests that types with PrecisionType.None return just the DMD type code.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_PrecisionTypeNone_ShouldReturnTypeCodeOnly()
    {
        // Arrange
        var field = CreateFieldModel("bit");
        var sqlFieldType = SqlFieldType.BIT;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("bool");
    }

    /// <summary>
    /// Tests that INT type returns just the DMD type code.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_IntType_ShouldReturnTypeCodeOnly()
    {
        // Arrange
        var field = CreateFieldModel("int");
        var sqlFieldType = SqlFieldType.INT;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("int");
    }

    /// <summary>
    /// Tests that BIGINT type returns just the DMD type code.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_BigintType_ShouldReturnTypeCodeOnly()
    {
        // Arrange
        var field = CreateFieldModel("bigint");
        var sqlFieldType = SqlFieldType.BIGINT;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("long");
    }

    /// <summary>
    /// Tests that DATETIME type returns just the DMD type code.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_DatetimeType_ShouldReturnTypeCodeOnly()
    {
        // Arrange
        var field = CreateFieldModel("datetime");
        var sqlFieldType = SqlFieldType.DATETIME;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("datetime");
    }

    /// <summary>
    /// Tests that FLOAT returns just type code.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_FloatWithoutPrecision_ShouldReturnTypeCodeOnly()
    {
        // Arrange
        var field = CreateFieldModel("float", precision: null);
        var sqlFieldType = SqlFieldType.FLOAT;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("float");
    }

    #endregion

    #region PrecisionType.PrecisionOnlyRequired Tests

    /// <summary>
    /// Tests that CHAR with precision returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_CharWithPrecision_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("char", precision: 5);
        var sqlFieldType = SqlFieldType.CHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("achar(5)");
    }

    /// <summary>
    /// Tests that NCHAR with precision returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_NcharWithPrecision_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("nchar", precision: 10);
        var sqlFieldType = SqlFieldType.NCHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("char(10)");
    }

    /// <summary>
    /// Tests that VARCHAR with precision returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_VarcharWithPrecision_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("varchar", precision: 255);
        var sqlFieldType = SqlFieldType.VARCHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("astring(255)");
    }

    /// <summary>
    /// Tests that NVARCHAR with precision returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_NvarcharWithPrecision_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("nvarchar", precision: 100);
        var sqlFieldType = SqlFieldType.NVARCHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("string(100)");
    }

    /// <summary>
    /// Tests that when field precision is null, it uses default precision.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_CharWithNullPrecision_ShouldUseDefaultPrecision()
    {
        // Arrange
        var field = CreateFieldModel("char", precision: null);
        var sqlFieldType = SqlFieldType.CHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("achar(1)"); // Default precision for CHAR is 1
    }

    #endregion

    #region PrecisionType.PrecisionWithScaleRequired Tests

    /// <summary>
    /// Tests that DECIMAL with precision and scale returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_DecimalWithPrecisionAndScale_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("decimal", precision: 18, scale: 2);
        var sqlFieldType = SqlFieldType.DECIMAL;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("decimal(18,2)");
    }

    /// <summary>
    /// Tests that NUMERIC with precision and scale returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_NumericWithPrecisionAndScale_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("numeric", precision: 10, scale: 4);
        var sqlFieldType = SqlFieldType.NUMERIC;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("decimal(10,4)");
    }

    /// <summary>
    /// Tests that MONEY with precision and scale returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_MoneyWithPrecisionAndScale_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("money", precision: 19, scale: 4);
        var sqlFieldType = SqlFieldType.MONEY;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("decimal(19,4)");
    }

    /// <summary>
    /// Tests that SMALLMONEY with precision and scale returns formatted string.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_SmallmoneyWithPrecisionAndScale_ShouldReturnFormattedString()
    {
        // Arrange
        var field = CreateFieldModel("smallmoney", precision: 10, scale: 4);
        var sqlFieldType = SqlFieldType.SMALLMONEY;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("decimal(10,4)");
    }

    /// <summary>
    /// Tests that when field precision is null, it uses default precision and scale.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_DecimalWithNullPrecision_ShouldUseDefaultPrecisionAndScale()
    {
        // Arrange
        var field = CreateFieldModel("decimal", precision: null, scale: null);
        var sqlFieldType = SqlFieldType.DECIMAL;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("decimal(18,0)"); // Default precision 18, scale 0
    }

    /// <summary>
    /// Tests that when field scale is null, it uses default scale.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_DecimalWithNullScale_ShouldUseDefaultScale()
    {
        // Arrange
        var field = CreateFieldModel("decimal", precision: 10, scale: null);
        var sqlFieldType = SqlFieldType.DECIMAL;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("decimal(10,0)"); // Field precision 10, default scale 0
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    /// <summary>
    /// Tests that zero precision is handled correctly.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_WithZeroPrecision_ShouldHandleCorrectly()
    {
        // Arrange
        var field = CreateFieldModel("char", precision: 0);
        var sqlFieldType = SqlFieldType.CHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("achar(0)");
    }

    /// <summary>
    /// Tests that negative precision is handled correctly.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_WithNegativePrecision_ShouldHandleCorrectly()
    {
        // Arrange
        var field = CreateFieldModel("char", precision: -5);
        var sqlFieldType = SqlFieldType.CHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("achar(-5)");
    }

    /// <summary>
    /// Tests that large precision values are handled correctly.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_WithLargePrecision_ShouldHandleCorrectly()
    {
        // Arrange
        var field = CreateFieldModel("varchar", precision: 8000);
        var sqlFieldType = SqlFieldType.VARCHAR;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("astring(8000)");
    }

    /// <summary>
    /// Tests that large scale values are handled correctly.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_WithLargeScale_ShouldHandleCorrectly()
    {
        // Arrange
        var field = CreateFieldModel("decimal", precision: 38, scale: 38);
        var sqlFieldType = SqlFieldType.DECIMAL;

        // Act
        var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

        // Assert
        result.Should().Be("decimal(38,38)");
    }

    #endregion

    #region Comprehensive Type Coverage Tests

    /// <summary>
    /// Tests all supported SQL field types in a single comprehensive test.
    /// This ensures all switch cases and precision types are covered.
    /// </summary>
    [Fact]
    public void GetDmdTypeString_AllSupportedTypes_ShouldConvertCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            // PrecisionType.None
            (SqlFieldType.BIT, "bool", null, null, "bool"),
            (SqlFieldType.INT, "int", null, null, "int"),
            (SqlFieldType.BIGINT, "bigint", null, null, "long"),
            (SqlFieldType.DATETIME, "datetime", null, null, "datetime"),
            (SqlFieldType.FLOAT, "float", null, null, "float"),

            // PrecisionType.PrecisionOnlyRequired
            (SqlFieldType.CHAR, "char", 5, null, "achar(5)"),
            (SqlFieldType.NCHAR, "nchar", 10, null, "char(10)"),
            (SqlFieldType.VARCHAR, "varchar", 100, null, "astring(100)"),
            (SqlFieldType.NVARCHAR, "nvarchar", 255, null, "string(255)"),
            
            // PrecisionType.PrecisionWithScaleRequired
            (SqlFieldType.DECIMAL, "decimal", 18, 2, "decimal(18,2)"),
            (SqlFieldType.NUMERIC, "numeric", 10, 4, "decimal(10,4)"),
            (SqlFieldType.MONEY, "money", 19, 4, "decimal(19,4)"),
            (SqlFieldType.SMALLMONEY, "smallmoney", 10, 4, "decimal(10,4)"),
            
            // Special cases
            (SqlFieldType.TEXT, "text", 100, null, "astring(max)"),
            (SqlFieldType.NTEXT, "ntext", 50, null, "string(max)"),
            (SqlFieldType.VARCHAR, "varchar", -1, null, "astring(max)"),
            (SqlFieldType.NVARCHAR, "nvarchar", (int?)-1, (int?)null, "string(max)")
        };

        foreach (var (sqlFieldType, fieldType, precision, scale, expectedResult) in testCases)
        {
            // Arrange
            var field = CreateFieldModel(fieldType, precision, scale);

            // Act
            var result = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);

            // Assert
            result.Should().Be(expectedResult, 
                $"SQL field type '{sqlFieldType.Code}' with precision {precision} and scale {scale} should return '{expectedResult}'");
        }
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
