using Compile.Shift.Cli.Commands;
using Compile.Shift.Commands;
using FluentAssertions;

namespace Compile.Shift.Cli.Tests.UserInput;

/// <summary>
/// Unit tests for StringCommandParser.
/// Tests the command-line argument parsing logic for all supported commands.
/// </summary>
public class RequestHelperTests
{
    #region Empty Args Tests

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when no arguments are provided.
    /// </summary>
    [Fact]
    public void GetCommand_WithEmptyArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region Apply Command Tests

    /// <summary>
    /// Tests that StringCommandParser returns ApplyCommand with correct parameters.
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyCommand_ShouldReturnApplyCommandWithCorrectParameters()
    {
        // Arrange
        var args = new[] { "apply", "Server=.;Database=Test;", "./Models", "./Models2" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyCommand>();
        var applyCommand = (ApplyCommand)result;
        applyCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyCommand.ModelLocationPaths.Should().Equal("./Models", "./Models2");
    }

    /// <summary>
    /// Tests that StringCommandParser returns ApplyCommand with single model path.
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyCommandSinglePath_ShouldReturnApplyCommandWithSinglePath()
    {
        // Arrange
        var args = new[] { "apply", "Server=.;Database=Test;", "./Models" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyCommand>();
        var applyCommand = (ApplyCommand)result;
        applyCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyCommand.ModelLocationPaths.Should().Equal("./Models");
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when apply command has insufficient arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithInsufficientApplyArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "apply", "Server=.;Database=Test;" }; // Missing paths

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region ApplyAssemblies Command Tests

    /// <summary>
    /// Tests that StringCommandParser returns ApplyAssembliesCommand with correct parameters.
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyAssembliesCommand_ShouldReturnApplyAssembliesCommandWithCorrectParameters()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./TestLibrary.dll", "./AnotherLibrary.dll" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyAssembliesCommand>();
        var applyAssembliesCommand = (ApplyAssembliesCommand)result;
        applyAssembliesCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyAssembliesCommand.DllPaths.Should().Equal("./TestLibrary.dll", "./AnotherLibrary.dll");
        applyAssembliesCommand.Namespaces.Should().BeNull();
    }

    /// <summary>
    /// Tests that StringCommandParser parses namespace filters from separate arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyAssembliesCommandWithNamespaceFilters_ShouldParseNamespaceFilters()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./TestLibrary.dll", "./AnotherLibrary.dll", "Namespace1", "Namespace2", "Namespace3" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyAssembliesCommand>();
        var applyAssembliesCommand = (ApplyAssembliesCommand)result;
        applyAssembliesCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyAssembliesCommand.DllPaths.Should().Equal("./TestLibrary.dll", "./AnotherLibrary.dll");
        applyAssembliesCommand.Namespaces.Should().NotBeNull();
        applyAssembliesCommand.Namespaces.Should().HaveCount(3);
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace1");
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace2");
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace3");
    }

    /// <summary>
    /// Tests that StringCommandParser handles DLL arguments with mixed order (DLL, filter, DLL).
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyAssembliesCommandMixedFilters_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./TestLibrary.dll", "Namespace1", "./AnotherLibrary.dll" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyAssembliesCommand>();
        var applyAssembliesCommand = (ApplyAssembliesCommand)result;
        applyAssembliesCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyAssembliesCommand.DllPaths.Should().Equal("./TestLibrary.dll", "./AnotherLibrary.dll");
        applyAssembliesCommand.Namespaces.Should().NotBeNull();
        applyAssembliesCommand.Namespaces.Should().Equal("Namespace1");
    }

    /// <summary>
    /// Tests that StringCommandParser handles duplicate namespace filters correctly.
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyAssembliesCommandDuplicateFilters_ShouldDeduplicateNamespaces()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./TestLibrary.dll", "./AnotherLibrary.dll", "Namespace1", "Namespace2", "Namespace1" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyAssembliesCommand>();
        var applyAssembliesCommand = (ApplyAssembliesCommand)result;
        applyAssembliesCommand.Namespaces.Should().NotBeNull();
        applyAssembliesCommand.Namespaces.Should().HaveCount(2);
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace1");
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace2");
    }

    /// <summary>
    /// Tests that StringCommandParser handles filters before DLLs.
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyAssembliesCommandFiltersBeforeDlls_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "Namespace1", "Namespace2", "./TestLibrary.dll", "./AnotherLibrary.dll" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyAssembliesCommand>();
        var applyAssembliesCommand = (ApplyAssembliesCommand)result;
        applyAssembliesCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyAssembliesCommand.DllPaths.Should().Equal("./TestLibrary.dll", "./AnotherLibrary.dll");
        applyAssembliesCommand.Namespaces.Should().NotBeNull();
        applyAssembliesCommand.Namespaces.Should().HaveCount(2);
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace1");
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace2");
    }

    /// <summary>
    /// Tests that StringCommandParser handles completely mixed order (DLL, filter, DLL, filter, filter).
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyAssembliesCommandCompletelyMixed_ShouldParseCorrectly()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "./TestLibrary.dll", "Namespace1", "./AnotherLibrary.dll", "Namespace2", "Namespace3" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyAssembliesCommand>();
        var applyAssembliesCommand = (ApplyAssembliesCommand)result;
        applyAssembliesCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyAssembliesCommand.DllPaths.Should().Equal("./TestLibrary.dll", "./AnotherLibrary.dll");
        applyAssembliesCommand.Namespaces.Should().NotBeNull();
        applyAssembliesCommand.Namespaces.Should().HaveCount(3);
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace1");
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace2");
        applyAssembliesCommand.Namespaces.Should().Contain("Namespace3");
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when apply-assemblies command has insufficient arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithInsufficientApplyAssembliesArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;" }; // Missing DLL paths

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when only filters are provided without DLLs.
    /// </summary>
    [Fact]
    public void GetCommand_WithApplyAssembliesCommandOnlyFilters_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "apply-assemblies", "Server=.;Database=Test;", "Namespace1", "Namespace2" }; // Only filters, no DLLs

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region Export Command Tests

    /// <summary>
    /// Tests that StringCommandParser returns ExportCommand with correct parameters.
    /// </summary>
    [Fact]
    public void GetCommand_WithExportCommand_ShouldReturnExportCommandWithCorrectParameters()
    {
        // Arrange
        var args = new[] { "export", "Server=.;Database=Test;", "dbo", "./Output" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ExportCommand>();
        var exportCommand = (ExportCommand)result;
        exportCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        exportCommand.Schema.Should().Be("dbo");
        exportCommand.OutputDirectoryPath.Should().Be("./Output");
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when export command has insufficient arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithInsufficientExportArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "export", "Server=.;Database=Test;", "dbo" }; // Missing output path

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region EF Sql Command Tests

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCommand with correct parameters.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCommand_ShouldReturnEfFromSqlCommandWithCorrectParameters()
    {
        // Arrange
        var args = new[] { "ef", "sql", "Server=.;Database=Test;", "./Output" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCommand>();
        var efCommand = (EfFromSqlCommand)result;
        efCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        efCommand.Schema.Should().Be("dbo");
        efCommand.OutputDirectoryPath.Should().Be("./Output");
    }

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCommand with custom schema.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCommandCustomSchema_ShouldReturnEfFromSqlCommandWithCustomSchema()
    {
        // Arrange
        var args = new[] { "ef", "sql", "Server=.;Database=Test;", "./Output", "custom" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCommand>();
        var efCommand = (EfFromSqlCommand)result;
        efCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        efCommand.Schema.Should().Be("custom");
        efCommand.OutputDirectoryPath.Should().Be("./Output");
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when ef sql command has insufficient arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithInsufficientEfSqlArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "ef", "sql", "Server=.;Database=Test;" }; // Missing output path

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region EF Files Command Tests

    /// <summary>
    /// Tests that StringCommandParser returns EfFromFilesCommand with correct parameters.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfFilesCommand_ShouldReturnEfFromFilesCommandWithCorrectParameters()
    {
        // Arrange
        var args = new[] { "ef", "files", "./Models/User.dmd", "./Models/Product.dmd", "./Output" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromFilesCommand>();
        var efCommand = (EfFromFilesCommand)result;
        efCommand.DmdLocationPaths.Should().Equal("./Models/User.dmd", "./Models/Product.dmd");
        efCommand.OutputDirectoryPath.Should().Be("./Output");
    }

    /// <summary>
    /// Tests that StringCommandParser returns EfFromFilesCommand with single file.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfFilesCommandSingleFile_ShouldReturnEfFromFilesCommandWithSingleFile()
    {
        // Arrange
        var args = new[] { "ef", "files", "./Models/User.dmd", "./Output" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromFilesCommand>();
        var efCommand = (EfFromFilesCommand)result;
        efCommand.DmdLocationPaths.Should().Equal("./Models/User.dmd");
        efCommand.OutputDirectoryPath.Should().Be("./Output");
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when ef files command has insufficient arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithInsufficientEfFilesArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "ef", "files", "./Models/User.dmd" }; // Missing output path

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region EF SqlCustom Command Tests

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCustomCommand with basic parameters.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCustomCommand_ShouldReturnEfFromSqlCustomCommandWithBasicParameters()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCustomCommand>();
        var efCommand = (EfFromSqlCustomCommand)result;
        efCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        efCommand.Schema.Should().Be("dbo");
        efCommand.OutputDirectoryPath.Should().Be("./Output");
        efCommand.Options.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCustomCommand with namespace option.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCustomCommandNamespace_ShouldParseNamespaceOption()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output", "--namespace", "MyApp.Data" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCustomCommand>();
        var efCommand = (EfFromSqlCustomCommand)result;
        efCommand.Options.NamespaceName.Should().Be("MyApp.Data");
    }

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCustomCommand with context option.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCustomCommandContext_ShouldParseContextOption()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output", "--context", "MyDbContext" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCustomCommand>();
        var efCommand = (EfFromSqlCustomCommand)result;
        efCommand.Options.ContextClassName.Should().Be("MyDbContext");
    }

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCustomCommand with interface option.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCustomCommandInterface_ShouldParseInterfaceOption()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output", "--interface", "IMyDbContext" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCustomCommand>();
        var efCommand = (EfFromSqlCustomCommand)result;
        efCommand.Options.InterfaceName.Should().Be("IMyDbContext");
    }

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCustomCommand with base-class option.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCustomCommandBaseClass_ShouldParseBaseClassOption()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;", "./Output", "--base-class", "BaseDbContext" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCustomCommand>();
        var efCommand = (EfFromSqlCustomCommand)result;
        efCommand.Options.BaseClassName.Should().Be("BaseDbContext");
    }

    /// <summary>
    /// Tests that StringCommandParser returns EfFromSqlCustomCommand with multiple options.
    /// </summary>
    [Fact]
    public void GetCommand_WithEfSqlCustomCommandMultipleOptions_ShouldParseAllOptions()
    {
        // Arrange
        var args = new[]
        {
            "ef", "sql-custom",
            "Server=.;Database=Test;",
            "./Output",
            "--namespace", "MyApp.Data",
            "--context", "MyDbContext",
            "--interface", "IMyDbContext",
            "--base-class", "BaseDbContext"
        };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<EfFromSqlCustomCommand>();
        var efCommand = (EfFromSqlCustomCommand)result;
        efCommand.Options.NamespaceName.Should().Be("MyApp.Data");
        efCommand.Options.ContextClassName.Should().Be("MyDbContext");
        efCommand.Options.InterfaceName.Should().Be("IMyDbContext");
        efCommand.Options.BaseClassName.Should().Be("BaseDbContext");
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand when ef sql-custom command has insufficient arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithInsufficientEfSqlCustomArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "ef", "sql-custom", "Server=.;Database=Test;" }; // Missing output path

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region Unknown Command Tests

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand for unknown commands.
    /// </summary>
    [Fact]
    public void GetCommand_WithUnknownCommand_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "unknown", "arg1", "arg2" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    /// <summary>
    /// Tests that StringCommandParser returns PrintHelpCommand for invalid EF subcommands.
    /// </summary>
    [Fact]
    public void GetCommand_WithInvalidEfSubcommand_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "ef", "invalid", "arg1", "arg2" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    #endregion

    #region Edge Case Tests

    /// <summary>
    /// Tests that StringCommandParser handles null arguments gracefully.
    /// </summary>
    [Fact]
    public void GetCommand_WithNullArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        string[]? args = null;

        // Act
        var result = RequestHelper.GetCommand(args!);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    /// <summary>
    /// Tests that StringCommandParser handles empty string arguments.
    /// </summary>
    [Fact]
    public void GetCommand_WithEmptyStringArgs_ShouldReturnPrintHelpCommand()
    {
        // Arrange
        var args = new[] { "", "", "" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<PrintHelpCommand>();
    }

    /// <summary>
    /// Tests that StringCommandParser handles case-insensitive command matching.
    /// </summary>
    [Fact]
    public void GetCommand_WithCaseInsensitiveCommand_ShouldReturnCorrectCommand()
    {
        // Arrange
        var args = new[] { "APPLY", "Server=.;Database=Test;", "./Models" };

        // Act
        var result = RequestHelper.GetCommand(args);

        // Assert
        result.Should().BeOfType<ApplyCommand>();
        var applyCommand = (ApplyCommand)result;
        applyCommand.ConnectionString.Should().Be("Server=.;Database=Test;");
        applyCommand.ModelLocationPaths.Should().Equal("./Models");
    }

    #endregion
}