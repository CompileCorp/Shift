using Compile.Shift.Model.Vnums;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Vnums;

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
}
