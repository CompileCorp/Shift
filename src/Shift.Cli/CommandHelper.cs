using Compile.Shift.Cli;
using Compile.Shift.Cli.Commands;
using Compile.Shift.Ef;
using Compile.Shift.Vnums;
using MediatR;

namespace Compile.Shift.Commands;

internal static class CommandHelper
{
    /// <summary>
    /// Creates and returns an <see cref="IRequest{T}"/> representing the command to execute based on the provided arguments.
    /// </summary>
    /// <param name="args">An array of command-line arguments used to determine the command to execute. The first argument specifies the
    /// command type, and subsequent arguments are passed to the corresponding command handler.</param>
    /// <returns>An <see cref="IRequest{T}"/> representing the command to execute. The specific command returned depends on the first argument:
    /// <list type="bullet">
    /// <item><description><see cref="CliCmdId.Apply"/>: Returns a command to apply changes.</description></item>
    /// <item><description><see cref="CliCmdId.Export"/>: Returns a command to export data.</description></item>
    /// <item><description><see cref="CliCmdId.EfGenerate"/>: Returns a command to generate Entity Framework-related artifacts.</description></item>
    /// <item><description><see cref="CliCmdId.ApplyAssemblies"/>: Returns a command to apply assemblies.</description></item>
    /// <item><description>Any other value: Returns a command to display help information.</description></item>
    /// </list></returns>
    internal static IRequest<Unit> GetCommand(string[] args)
    {
        var userInput = new UserInput(args);

        // Handle simple commands (non-EF)
        if (userInput.Command != CliCmd.EfGenerate)
        {
            return userInput.Command.Id switch
            {
                CliCmdId.Help => new PrintHelpCommand(),
                CliCmdId.Apply => GetApplyCommand(userInput),
                CliCmdId.Export => GetExportCommand(userInput),
                CliCmdId.ApplyAssemblies => GetApplyAssembliesCommand(userInput),
                _ => new PrintHelpCommand(["Error: Unknown command"])
            };
        }

        if (userInput.SubCommand == null)
        {
            return new PrintHelpCommand(["Error: EF sub-command required"]);
        }

        // Handle EF commands
        return userInput.SubCommand.Id switch
        {
            CliSubCmdId.Help => new PrintHelpCommand(),
            CliSubCmdId.Sql => GetEfFromSqlCommand(userInput),
            CliSubCmdId.Files => GetEfFromFilesCommand(userInput),
            CliSubCmdId.SqlCustom => GetEfFromSqlCustomCommand(userInput),
            _ => new PrintHelpCommand(["Error: Unknown EF sub-command"])
        };

    }

    private static IRequest<Unit> GetApplyCommand(UserInput userInput)
    {
        var args = userInput.RemainingArgs;
        if (args.Length < 2)
        {
            return new PrintHelpCommand(["Error: Command requires a connection string and at least one dmd model location path"]);
        }

        return new ApplyCommand(
            ConnectionString: args[0],
            ModelLocationPaths: args[1..]);
    }

    private static IRequest<Unit> GetApplyAssembliesCommand(UserInput userInput)
    {
        var args = userInput.RemainingArgs;
        if (args.Length < 2)
        {
            return new PrintHelpCommand([$"Error: Command requires a connection string and at least one DLL path"]);
        }

        var connectionString = args[0];
        var remainingArgs = args[1..];
        var dllPaths = new List<string>();
        var allNamespaces = new HashSet<string>(StringComparer.Ordinal);

        // Parse arguments: anything ending with .dll is a DLL, anything else is a filter
        foreach (var arg in remainingArgs)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue; // Skip empty arguments
            }

            // Case-insensitive check for .dll extension
            if (arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                dllPaths.Add(arg);
            }
            else
            {
                allNamespaces.Add(arg);
            }
        }

        // Validation: must have at least one DLL
        if (dllPaths.Count == 0)
        {
            return new PrintHelpCommand([$"Error: Command requires at least one DLL path (file ending with .dll)"]);
        }

        return new ApplyAssembliesCommand(
            ConnectionString: connectionString,
            DllPaths: dllPaths.ToArray(),
            Namespaces: allNamespaces.Count > 0 ? allNamespaces.ToArray() : null);
    }

    private static IRequest<Unit> GetExportCommand(UserInput userInput)
    {
        var args = userInput.RemainingArgs;
        if (args.Length < 3)
        {
            return new PrintHelpCommand([$"Error: Command requires a connection string, schema and output directory path"]);
        }

        return new ExportCommand(
            ConnectionString: args[0],
            Schema: args[1],
            OutputDirectoryPath: args[2]);
    }

    private static IRequest<Unit> GetEfFromSqlCommand(UserInput userInput)
    {
        var args = userInput.RemainingArgs;
        if (args.Length < 2)
        {
            return new PrintHelpCommand(["Error: ef sql requires <connection-string> <output-path>"]);
        }

        var connectionString = args[0];
        var outputPath = args[1];
        var schema = args.Length > 2 ? args[2] : "dbo";

        Console.WriteLine($"Generating Entity Framework code from SQL Server...");
        Console.WriteLine($"   Connection: {connectionString}");
        Console.WriteLine($"   Schema: {schema}");
        Console.WriteLine($"   Output: {outputPath}");

        return new EfFromSqlCommand(
            ConnectionString: connectionString,
            Schema: schema,
            OutputDirectoryPath: outputPath);
    }

    private static IRequest<Unit> GetEfFromFilesCommand(UserInput userInput)
    {
        var args = userInput.RemainingArgs;
        if (args.Length < 2)
        {
            return new PrintHelpCommand(
            [
                "Error: ef files requires <path1> [path2] [...] <output-path>",
                "       Last argument is the output path, all others are input model files"
            ]);
        }

        var outputPath = args[^1]; // Last argument is output path
        var inputPaths = args[..^1]; // All but last are input paths

        Console.WriteLine($"Generating Entity Framework code from model files...");
        Console.WriteLine($"   Input files: {string.Join(", ", inputPaths)}");
        Console.WriteLine($"   Output: {outputPath}");

        return new EfFromFilesCommand(
            DmdLocationPaths: inputPaths,
            OutputDirectoryPath: outputPath);
    }

    private static IRequest<Unit> GetEfFromSqlCustomCommand(UserInput userInput)
    {
        var args = userInput.RemainingArgs;
        if (args.Length < 2)
        {
            return new PrintHelpCommand(["Error: ef sql-custom requires <connection-string> <output-path> [options]"]);
        }

        var connectionString = args[0];
        var outputPath = args[1];
        var remainingArgs = args[2..];

        // Parse options
        var options = new EfCodeGenerationOptions();
        for (int i = 0; i < remainingArgs.Length; i += 2)
        {
            if (i + 1 >= remainingArgs.Length) break;

            var option = remainingArgs[i].ToLowerInvariant();
            var value = remainingArgs[i + 1];

            switch (option)
            {
                case "--namespace":
                    options.NamespaceName = value;
                    break;
                case "--context":
                    options.ContextClassName = value;
                    break;
                case "--interface":
                    options.InterfaceName = value;
                    break;
                case "--base-class":
                    options.BaseClassName = value;
                    break;
                default:
                    Console.WriteLine($"Warning: Unknown option '{option}'");
                    break;
            }
        }

        Console.WriteLine($"Generating Entity Framework code from SQL Server with custom options...");
        Console.WriteLine($"   Connection: {connectionString}");
        Console.WriteLine($"   Output: {outputPath}");
        Console.WriteLine($"   Namespace: {options.NamespaceName}");
        Console.WriteLine($"   Context: {options.ContextClassName}");
        Console.WriteLine($"   Interface: {options.InterfaceName}");
        if (!string.IsNullOrEmpty(options.BaseClassName))
            Console.WriteLine($"   Base Class: {options.BaseClassName}");

        return new EfFromSqlCustomCommand(
            ConnectionString: connectionString,
            Schema: "dbo", //TODO: make schema configurable
            OutputDirectoryPath: outputPath,
            Options: options);
    }
}