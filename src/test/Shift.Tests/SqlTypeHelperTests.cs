using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
using Compile.Shift.Model.Vnums;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;
using Xunit;

namespace Compile.Shift.Tests;

public class SqlTypeHelperTests : UnitTestContext<object>
{
    #region GetSqlTypeString Tests

    [Fact]
    public void GetSqlTypeString_NullSqlFieldType_ShouldReturnUnknownTypeString()
    {
        // Arrange
        var fieldModel = CreateFieldModel("unknown", precision: 10, scale: 2);

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, null);

        // Assert
        result.Should().Be("unknown(10,2)");
    }

    [Fact]
    public void GetSqlTypeString_TextType_ShouldReturnNvarcharMax()
    {
        // Arrange
        var fieldModel = CreateFieldModel("text", precision: -1);
        var sqlFieldType = SqlFieldType.TEXT;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("varchar(max)");
    }

    [Fact]
    public void GetSqlTypeString_NtextType_ShouldReturnNvarcharMax()
    {
        // Arrange
        var fieldModel = CreateFieldModel("ntext", precision: -1);
        var sqlFieldType = SqlFieldType.NTEXT;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("nvarchar(max)");
    }

    [Fact]
    public void GetSqlTypeString_MoneyType_ShouldReturnDecimalWithPrecisionAndScale()
    {
        // Arrange
        var fieldModel = CreateFieldModel("money");
        var sqlFieldType = SqlFieldType.MONEY;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("decimal(19,4)");
    }

    [Fact]
    public void GetSqlTypeString_SmallMoneyType_ShouldReturnDecimalWithPrecisionAndScale()
    {
        // Arrange
        var fieldModel = CreateFieldModel("smallmoney");
        var sqlFieldType = SqlFieldType.SMALLMONEY;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("decimal(10,4)");
    }

    [Fact]
    public void GetSqlTypeString_SupportsMaxLengthWithMaxPrecision_ShouldReturnMax()
    {
        // Arrange
        var fieldModel = CreateFieldModel("nvarchar", precision: -1);
        var sqlFieldType = SqlFieldType.NVARCHAR;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("nvarchar(max)");
    }

    [Fact]
    public void GetSqlTypeString_PrecisionOnlyRequired_ShouldReturnTypeWithPrecision()
    {
        // Arrange
        var fieldModel = CreateFieldModel("nvarchar", precision: 100);
        var sqlFieldType = SqlFieldType.NVARCHAR;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("nvarchar(100)");
    }

    [Fact]
    public void GetSqlTypeString_PrecisionOnlyRequiredWithNullPrecision_ShouldUseDefaultPrecision()
    {
        // Arrange
        var fieldModel = CreateFieldModel("nvarchar", precision: null);
        var sqlFieldType = SqlFieldType.NVARCHAR;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("nvarchar(255)"); // Default precision for NVARCHAR
    }

    [Fact]
    public void GetSqlTypeString_PrecisionWithScaleRequired_ShouldReturnTypeWithPrecisionAndScale()
    {
        // Arrange
        var fieldModel = CreateFieldModel("decimal", precision: 18, scale: 2);
        var sqlFieldType = SqlFieldType.DECIMAL;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("decimal(18,2)");
    }

    [Fact]
    public void GetSqlTypeString_PrecisionWithScaleRequiredWithNullValues_ShouldUseDefaults()
    {
        // Arrange
        var fieldModel = CreateFieldModel("decimal", precision: null, scale: null);
        var sqlFieldType = SqlFieldType.DECIMAL;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("decimal(18,0)"); // Default precision and scale for DECIMAL
    }

    [Fact]
    public void GetSqlTypeString_NoPrecisionType_ShouldReturnTypeOnly()
    {
        // Arrange
        var fieldModel = CreateFieldModel("int");
        var sqlFieldType = SqlFieldType.INT;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("int");
    }

    [Fact]
    public void GetSqlTypeString_BitType_ShouldReturnBit()
    {
        // Arrange
        var fieldModel = CreateFieldModel("bit");
        var sqlFieldType = SqlFieldType.BIT;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("bit");
    }

    [Fact]
    public void GetSqlTypeString_UniqueIdentifierType_ShouldReturnUniqueidentifier()
    {
        // Arrange
        var fieldModel = CreateFieldModel("uniqueidentifier");
        var sqlFieldType = SqlFieldType.UNIQUEIDENTIFIER;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("uniqueidentifier");
    }

    [Fact]
    public void GetSqlTypeString_DateTimeType_ShouldReturnDatetime()
    {
        // Arrange
        var fieldModel = CreateFieldModel("datetime");
        var sqlFieldType = SqlFieldType.DATETIME;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("datetime");
    }

    #endregion

    #region GetUnknownSqlTypeString Tests

    [Fact]
    public void GetUnknownSqlTypeString_NoPrecision_ShouldReturnTypeOnly()
    {
        // Arrange
        var fieldModel = CreateFieldModel("unknown");

        // Act
        var result = SqlTypeHelper.GetUnknownSqlTypeString(fieldModel);

        // Assert
        result.Should().Be("unknown");
    }

    [Fact]
    public void GetUnknownSqlTypeString_MaxPrecision_ShouldReturnTypeWithMax()
    {
        // Arrange
        var fieldModel = CreateFieldModel("unknown", precision: -1);

        // Act
        var result = SqlTypeHelper.GetUnknownSqlTypeString(fieldModel);

        // Assert
        result.Should().Be("unknown(max)");
    }

    [Fact]
    public void GetUnknownSqlTypeString_WithPrecisionAndScale_ShouldReturnTypeWithBoth()
    {
        // Arrange
        var fieldModel = CreateFieldModel("unknown", precision: 18, scale: 2);

        // Act
        var result = SqlTypeHelper.GetUnknownSqlTypeString(fieldModel);

        // Assert
        result.Should().Be("unknown(18,2)");
    }

    [Fact]
    public void GetUnknownSqlTypeString_WithPrecisionOnly_ShouldReturnTypeWithPrecision()
    {
        // Arrange
        var fieldModel = CreateFieldModel("unknown", precision: 100);

        // Act
        var result = SqlTypeHelper.GetUnknownSqlTypeString(fieldModel);

        // Assert
        result.Should().Be("unknown(100)");
    }

    [Fact]
    public void GetUnknownSqlTypeString_WithPrecisionButNoScale_ShouldReturnTypeWithPrecision()
    {
        // Arrange
        var fieldModel = CreateFieldModel("unknown", precision: 50, scale: null);

        // Act
        var result = SqlTypeHelper.GetUnknownSqlTypeString(fieldModel);

        // Assert
        result.Should().Be("unknown(50)");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void GetSqlTypeString_FieldModelWithNullPrecision_ShouldHandleGracefully()
    {
        // Arrange
        var fieldModel = CreateFieldModel("nvarchar", precision: null);
        var sqlFieldType = SqlFieldType.NVARCHAR;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("nvarchar(255)"); // Should use default precision
    }

    [Fact]
    public void GetSqlTypeString_FieldModelWithNullScale_ShouldHandleGracefully()
    {
        // Arrange
        var fieldModel = CreateFieldModel("decimal", precision: 18, scale: null);
        var sqlFieldType = SqlFieldType.DECIMAL;

        // Act
        var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);

        // Assert
        result.Should().Be("decimal(18,0)"); // Should use default scale
    }

    [Fact]
    public void GetSqlTypeString_AllSupportedTypes_ShouldReturnCorrectSqlTypes()
    {
        // Arrange & Act & Assert
        var testCases = new[]
        {
            (SqlFieldType.BIT, "bit"),
            (SqlFieldType.UNIQUEIDENTIFIER, "uniqueidentifier"),
            (SqlFieldType.CHAR, "char(1)"),
            (SqlFieldType.VARCHAR, "varchar(255)"),
            (SqlFieldType.TEXT, "varchar(max)"),
            (SqlFieldType.NCHAR, "nchar(1)"),
            (SqlFieldType.NVARCHAR, "nvarchar(255)"),
            (SqlFieldType.NTEXT, "nvarchar(max)"),
            (SqlFieldType.INT, "int"),
            (SqlFieldType.BIGINT, "bigint"),
            (SqlFieldType.DECIMAL, "decimal(18,0)"),
            (SqlFieldType.NUMERIC, "numeric(18,0)"),
            (SqlFieldType.FLOAT, "float"),
            (SqlFieldType.MONEY, "decimal(19,4)"),
            (SqlFieldType.SMALLMONEY, "decimal(10,4)"),
            (SqlFieldType.DATETIME, "datetime")
        };

        foreach (var (sqlFieldType, expectedResult) in testCases)
        {
            var fieldModel = CreateFieldModel(sqlFieldType.Code);
            var result = SqlTypeHelper.GetSqlTypeString(fieldModel, sqlFieldType);
            result.Should().Be(expectedResult, because: $"{sqlFieldType.Code} should map to {expectedResult}");
        }
    }

    #endregion

    #region Helper Methods

    private static FieldModel CreateFieldModel(string type, int? precision = null, int? scale = null)
    {
        return new FieldModel
        {
            Name = "TestField",
            Type = type,
            Precision = precision,
            Scale = scale,
            IsNullable = false
        };
    }

    #endregion
}
