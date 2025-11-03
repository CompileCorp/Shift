using Compile.Shift.Vnums;
using FluentAssertions;

namespace Compile.Shift.Cli.Tests.Input;

/// <summary>
/// Unit tests for UserInput.
/// Tests the command-line argument parsing logic for all supported commands and sub-commands.
/// </summary>
public class UserInputTests
{
    #region Empty Args Tests

    /// <summary>
    /// Tests that UserInput defaults to Help command when no arguments are provided.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyArgs_ShouldDefaultToHelpCommand()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Help);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that UserInput defaults to Help command when null arguments are provided.
    /// </summary>
    [Fact]
    public void Constructor_WithNullArgs_ShouldDefaultToHelpCommand()
    {
        // Arrange
        string[]? args = null;

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Help);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().BeEmpty();
    }

    #endregion

    #region Apply Command Tests

    /// <summary>
    /// Tests that UserInput parses Apply command correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithApplyCommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "apply", "Server=.;Database=Test;", "./Models", "./Models2" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Apply);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Models", "./Models2");
    }

    /// <summary>
    /// Tests that UserInput parses Apply command with single argument.
    /// </summary>
    [Fact]
    public void Constructor_WithApplyCommandSingleArg_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "apply", "Server=.;Database=Test;" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Apply);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;");
    }

    /// <summary>
    /// Tests that UserInput parses Apply command case-insensitively.
    /// </summary>
    [Fact]
    public void Constructor_WithApplyCommandCaseInsensitive_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "APPLY", "Server=.;Database=Test;", "./Models" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Apply);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Models");
    }

    #endregion

    #region Export Command Tests

    /// <summary>
    /// Tests that UserInput parses Export command correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithExportCommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "export", "Server=.;Database=Test;", "dbo", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Export);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "dbo", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses Export command case-insensitively.
    /// </summary>
    [Fact]
    public void Constructor_WithExportCommandCaseInsensitive_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "EXPORT", "Server=.;Database=Test;", "dbo", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Export);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "dbo", "./Output");
    }

    #endregion

    #region ApplyAssemblies Command Tests

    /// <summary>
    /// Tests that UserInput parses ApplyAssemblies command correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithApplyAssembliesCommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./TestLibrary.dll", "./AnotherLibrary.dll" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.ApplyAssemblies);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./TestLibrary.dll", "./AnotherLibrary.dll");
    }

    /// <summary>
    /// Tests that UserInput parses ApplyAssemblies command with namespace filters.
    /// </summary>
    [Fact]
    public void Constructor_WithApplyAssembliesCommandWithFilters_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./TestLibrary.dll", "Namespace1", "Namespace2" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.ApplyAssemblies);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./TestLibrary.dll", "Namespace1", "Namespace2");
    }

    #endregion

    #region EF Command Tests

    /// <summary>
    /// Tests that UserInput parses EF command without sub-command (should be null).
    /// </summary>
    [Fact]
    public void Constructor_WithEfCommandOnly_ShouldHaveNullSubcommand()
    {
        // Arrange
        var args = new[] { "ef" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that UserInput parses EF command with sql sub-command.
    /// </summary>
    [Fact]
    public void Constructor_WithEfSqlCommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "sql", "Server=.;Database=Test;", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Sql);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses EF command with sql sub-command and schema.
    /// </summary>
    [Fact]
    public void Constructor_WithEfSqlCommandWithSchema_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "sql", "Server=.;Database=Test;", "./Output", "custom" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Sql);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output", "custom");
    }

    /// <summary>
    /// Tests that UserInput parses EF command with files sub-command.
    /// </summary>
    [Fact]
    public void Constructor_WithEfFilesCommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "files", "./Models/User.dmd", "./Models/Product.dmd", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Files);
        userInput.RemainingArgs.Should().Equal("./Models/User.dmd", "./Models/Product.dmd", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses EF command with files sub-command and single file.
    /// </summary>
    [Fact]
    public void Constructor_WithEfFilesCommandSingleFile_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "files", "./Models/User.dmd", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Files);
        userInput.RemainingArgs.Should().Equal("./Models/User.dmd", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses EF command with sql-custom sub-command.
    /// </summary>
    [Fact]
    public void Constructor_WithEfSqlCustomCommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.SqlCustom);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses EF command with sql-custom sub-command and options.
    /// </summary>
    [Fact]
    public void Constructor_WithEfSqlCustomCommandWithOptions_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output", "--namespace", "MyApp.Data", "--context", "MyDbContext" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.SqlCustom);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output", "--namespace", "MyApp.Data", "--context", "MyDbContext");
    }

    /// <summary>
    /// Tests that UserInput parses EF command with help sub-command.
    /// </summary>
    [Fact]
    public void Constructor_WithEfHelpSubcommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "help" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Help);
        userInput.RemainingArgs.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that UserInput parses EF command case-insensitively.
    /// </summary>
    [Fact]
    public void Constructor_WithEfCommandCaseInsensitive_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "EF", "SQL", "Server=.;Database=Test;", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Sql);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses EF sub-command case-insensitively.
    /// </summary>
    [Fact]
    public void Constructor_WithSubCommandCaseInsensitive_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "ef", "FILES", "./Models/User.dmd", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Files);
        userInput.RemainingArgs.Should().Equal("./Models/User.dmd", "./Output");
    }

    #endregion

    #region Command Alias Tests

    /// <summary>
    /// Tests that UserInput parses EF command alias "ef" correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithEfAlias_ShouldParseAsEfGenerateCommand()
    {
        // Arrange
        var args = new[] { "ef", "sql", "Server=.;Database=Test;", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Sql);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses EF command alias "ef-generate" correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithEfGenerateAlias_ShouldParseAsEfGenerateCommand()
    {
        // Arrange
        var args = new[] { "ef-generate", "sql", "Server=.;Database=Test;", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Sql);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses EF command alias "generate-ef" correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithGenerateEfAlias_ShouldParseAsEfGenerateCommand()
    {
        // Arrange
        var args = new[] { "generate-ef", "files", "./Models/User.dmd", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Files);
        userInput.RemainingArgs.Should().Equal("./Models/User.dmd", "./Output");
    }

    /// <summary>
    /// Tests that UserInput parses command aliases case-insensitively.
    /// </summary>
    [Fact]
    public void Constructor_WithEfGenerateAliasCaseInsensitive_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "EF-GENERATE", "sql", "Server=.;Database=Test;", "./Output" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Sql);
        userInput.RemainingArgs.Should().Equal("Server=.;Database=Test;", "./Output");
    }

    #endregion

    #region Unknown Command Tests

    /// <summary>
    /// Tests that UserInput defaults to Help command for unknown commands.
    /// </summary>
    [Fact]
    public void Constructor_WithUnknownCommand_ShouldDefaultToHelpCommand()
    {
        // Arrange
        var args = new[] { "unknown", "arg1", "arg2" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Help);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("arg1", "arg2");
    }

    /// <summary>
    /// Tests that UserInput defaults to Help sub-command for invalid EF sub-commands.
    /// </summary>
    [Fact]
    public void Constructor_WithInvalidSubCommand_ShouldDefaultToHelpSubcommand()
    {
        // Arrange
        var args = new[] { "ef", "invalid", "arg1", "arg2" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.EfGenerate);
        userInput.SubCommand.Should().Be(CliSubCmd.Help);
        userInput.RemainingArgs.Should().Equal("arg1", "arg2");
    }

    #endregion

    #region Help Command Tests

    /// <summary>
    /// Tests that UserInput parses Help command correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithHelpCommand_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "help" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Help);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that UserInput parses Help command with extra arguments.
    /// </summary>
    [Fact]
    public void Constructor_WithHelpCommandWithArgs_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "help", "extra", "args" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Help);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("extra", "args");
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Tests that UserInput handles empty string arguments.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyStringArgs_ShouldDefaultToHelpCommand()
    {
        // Arrange
        var args = new[] { "", "", "" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Help);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("", "");
    }

    /// <summary>
    /// Tests that UserInput handles whitespace-only arguments.
    /// </summary>
    [Fact]
    public void Constructor_WithWhitespaceArgs_ShouldDefaultToHelpCommand()
    {
        // Arrange
        var args = new[] { "   ", "  ", " " };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Help);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("  ", " ");
    }

    /// <summary>
    /// Tests that UserInput handles non-EF commands with multiple arguments correctly.
    /// </summary>
    [Fact]
    public void Constructor_WithNonEfCommandWithMultipleArgs_ShouldNotParseSubcommand()
    {
        // Arrange
        var args = new[] { "apply", "arg1", "arg2", "arg3" };

        // Act
        var userInput = new UserInput(args);

        // Assert
        userInput.Command.Should().Be(CliCmd.Apply);
        userInput.SubCommand.Should().BeNull();
        userInput.RemainingArgs.Should().Equal("arg1", "arg2", "arg3");
    }

    #endregion
}