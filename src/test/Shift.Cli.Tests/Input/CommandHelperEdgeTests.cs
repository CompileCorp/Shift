using Compile.Shift.Cli.Commands;
using Compile.Shift.Commands;
using FluentAssertions;

namespace Compile.Shift.Cli.Tests.Input;

/// <summary>
/// Edge-case tests for CommandHelper covering the --schema option handling, the unknown-option
/// path of `ef sql-custom`, and the missing EF sub-command case — branches not exercised by the
/// main CommandHelperTests.
/// </summary>
public class CommandHelperEdgeTests
{
    [Fact]
    public void GetCommand_ApplyWithSchemaOption_ParsesSchemaAndRemovesItFromPaths()
    {
        var args = new[] { "apply", "Server=.;Database=Test;", "./Models", "--schema", "custom" };

        var result = CommandHelper.GetCommand(args);

        var apply = result.Should().BeOfType<ApplyCommand>().Subject;
        apply.Schema.Should().Be("custom");
        apply.ModelLocationPaths.Should().Equal("./Models");
    }

    [Fact]
    public void GetCommand_ApplyWithSchemaButNoPaths_ReturnsPrintHelp()
    {
        // After consuming "--schema custom" there are no model paths left.
        var args = new[] { "apply", "Server=.;Database=Test;", "--schema", "custom" };

        var result = CommandHelper.GetCommand(args);

        result.Should().BeOfType<PrintHelpCommand>();
    }

    [Fact]
    public void GetCommand_ApplyAssembliesWithSchemaOption_ParsesSchema()
    {
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./Lib.dll", "--schema", "custom" };

        var result = CommandHelper.GetCommand(args);

        var cmd = result.Should().BeOfType<ApplyAssembliesCommand>().Subject;
        cmd.Schema.Should().Be("custom");
        cmd.DllPaths.Should().Equal("./Lib.dll");
    }

    [Fact]
    public void GetCommand_EfSqlCustomWithSchemaOption_ParsesSchema()
    {
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output", "--schema", "custom" };

        var result = CommandHelper.GetCommand(args);

        var cmd = result.Should().BeOfType<EfFromSqlCustomCommand>().Subject;
        cmd.Schema.Should().Be("custom");
    }

    [Fact]
    public void GetCommand_EfSqlCustomWithUnknownOption_StillReturnsCommand()
    {
        // An unknown option logs a warning but does not abort command construction.
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output", "--bogus", "value" };

        var result = CommandHelper.GetCommand(args);

        result.Should().BeOfType<EfFromSqlCustomCommand>();
    }

    [Fact]
    public void GetCommand_ApplyAssembliesWithEmptyArgument_SkipsIt()
    {
        // An empty/whitespace argument among the DLL/namespace list is ignored.
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "", "./Lib.dll" };

        var result = CommandHelper.GetCommand(args);

        var cmd = result.Should().BeOfType<ApplyAssembliesCommand>().Subject;
        cmd.DllPaths.Should().Equal("./Lib.dll");
    }

    [Fact]
    public void GetCommand_EfWithoutSubCommand_ReturnsPrintHelp()
    {
        var args = new[] { "ef" };

        var result = CommandHelper.GetCommand(args);

        result.Should().BeOfType<PrintHelpCommand>();
    }
}