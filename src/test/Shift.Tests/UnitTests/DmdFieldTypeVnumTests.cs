using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.UnitTests;

public class DmdFieldTypeVnumTests : UnitTestContext<VnumTestingHelper<DmdFieldType, DmdFieldTypeId>>
{
    [Fact]
    public void Run_DmdFieldTypeVnum_Tests()
    {
        Sut.Vnum_Instances_Must_Have_Unique_Values();
        Sut.Vnum_Instances_Must_Have_Unique_Codes();
        Sut.All_Vnum_Instances_Must_Have_Matching_Enum();
        Sut.All_Enum_Instances_Must_Convert_To_Vnum();
    }

    [Fact]
    public void RoundTrip_Should_ReturnTheOriginalType()
    {
        foreach (var originalDmdType in Vnum.GetAll<DmdFieldType>())
        {
            // Act & Assert
            var sqlType = originalDmdType.SqlFieldType;
            var result = sqlType.DmdType;

            // Assert
            var expected = originalDmdType.Id switch
            {
                DmdFieldTypeId.STRING => DmdFieldType.USTRING,
                DmdFieldTypeId.CHAR => DmdFieldType.UCHAR,
                _ => originalDmdType
            };

            result.Should().Be(expected);
        }
    }
}