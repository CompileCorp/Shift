using Compile.Shift.Cli.Commands;
using Compile.Shift.Commands;
using FluentAssertions;

namespace Compile.Shift.Cli.Tests.Input;

/// <summary>
/// Unit tests for the CommandHelper argument parsing of the plugin-facing commands: `dbml` and
/// `attributes`.
/// </summary>
public class CommandHelperPluginTests
{
    #region dbml

    [Fact]
    public void GetCommand_WithDbmlCommand_ShouldReturnDbmlCommandWithCorrectParameters()
    {
        var result = CommandHelper.GetCommand(["dbml", "./Models", "./Mixins", "./Diagrams/model.dbml"]);

        result.Should().BeOfType<DbmlCommand>();
        var command = (DbmlCommand)result;
        command.DmdLocationPaths.Should().Equal("./Models", "./Mixins");
        command.OutputPath.Should().Be("./Diagrams/model.dbml");
    }

    [Fact]
    public void GetCommand_WithDbmlCommandSinglePath_ShouldReturnDbmlCommand()
    {
        var result = CommandHelper.GetCommand(["dbml", "./Models", "./Diagrams"]);

        result.Should().BeOfType<DbmlCommand>();
        var command = (DbmlCommand)result;
        command.DmdLocationPaths.Should().Equal("./Models");
        command.OutputPath.Should().Be("./Diagrams");
    }

    [Fact]
    public void GetCommand_WithNoDbmlArgs_ShouldReturnPrintHelpCommand()
    {
        CommandHelper.GetCommand(["dbml"]).Should().BeOfType<PrintHelpCommand>();
    }

    [Fact]
    public void GetCommand_WithOnlyAnOutputPath_ShouldReturnPrintHelpCommand()
    {
        CommandHelper.GetCommand(["dbml", "./Models"]).Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region attributes

    [Fact]
    public void GetCommand_WithAttributesCommand_ShouldReturnAttributesCommandWithNoFilter()
    {
        var result = CommandHelper.GetCommand(["attributes"]);

        result.Should().BeOfType<AttributesCommand>();
        ((AttributesCommand)result).PluginName.Should().BeNull();
    }

    [Fact]
    public void GetCommand_WithAttributesCommandAndPluginName_ShouldFilterToThatPlugin()
    {
        var result = CommandHelper.GetCommand(["attributes", "dbml"]);

        result.Should().BeOfType<AttributesCommand>();
        ((AttributesCommand)result).PluginName.Should().Be("dbml");
    }

    #endregion
}