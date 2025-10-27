using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.UnitTests;

public class SqlFieldTypeVnumTests : UnitTestContext<VnumTestingHelper<SqlFieldType, SqlFieldTypeId>>
{
    [Fact]
    public void Run_SqlFieldTypeVnum_Tests()
    {
        Sut.Vnum_Instances_Must_Have_Unique_Values();
        Sut.Vnum_Instances_Must_Have_Unique_Codes();
        Sut.All_Vnum_Instances_Must_Have_Matching_Enum();
        Sut.All_Enum_Instances_Must_Convert_To_Vnum();
    }

    [Fact]
    public void RoundTrip_Should_ReturnTheExpectedType()
    {
        foreach (var originalSqlType in Vnum.GetAll<SqlFieldType>())
        {
            // Act
            var dmdType = originalSqlType.DmdType;
            var result = dmdType.SqlFieldType;

            // Assert
            var expected = originalSqlType.Id switch
            {
                SqlFieldTypeId.TEXT => SqlFieldType.VARCHAR,
                SqlFieldTypeId.NTEXT => SqlFieldType.NVARCHAR,
                SqlFieldTypeId.MONEY => SqlFieldType.DECIMAL,
                SqlFieldTypeId.SMALLMONEY => SqlFieldType.DECIMAL,
                SqlFieldTypeId.NUMERIC => SqlFieldType.DECIMAL,
                _ => originalSqlType
            };

            result.Should().Be(expected);
        }
    }
}
