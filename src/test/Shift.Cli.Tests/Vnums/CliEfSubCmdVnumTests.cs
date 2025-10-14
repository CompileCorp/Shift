using Compile.Shift.Vnums;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Vnums;

public class CliEfSubCmdVnumTests : UnitTestContext<VnumTestingHelper<CliEfSubCmd, CliEfSubCmdId>>
{
    [Fact]
    public void Run_CliEfSubCmdVnum_Tests()
    {
        Sut.Vnum_Instances_Must_Have_Unique_Values();
        Sut.Vnum_Instances_Must_Have_Unique_Codes();
        Sut.All_Vnum_Instances_Must_Have_Matching_Enum();
        Sut.All_Enum_Instances_Must_Convert_To_Vnum();
    }
}
