using Compile.Shift.Cli.Commands;
using Compile.Shift.Ef;
using Compile.Shift.Vnums;
using Compile.VnumEnumeration;
using MediatR;

namespace Compile.Shift.Commands;

internal static class RequestHelper
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
        var cliCmd = GetCliCmd(args);

        return cliCmd.Id switch
        {
            CliCmdId.Apply => GetApplyCommand(args[1..]),
            CliCmdId.Export => GetExportCommand(args[1..]),
            CliCmdId.EfGenerate => GetEfCommand(args[1..]),
            CliCmdId.ApplyAssemblies => GetApplyAssembliesCommand(args[1..]),
            _ => new PrintHelpCommand()
        };
    }

    /// <summary>
    /// Parses the first argument to determine the CLI command.
    /// </summary>
    private static CliCmd GetCliCmd(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return CliCmd.Help;
        }

        var firstArg = args[0];

        if (!Vnum.TryFromCode<CliCmd>(firstArg, ignoreCase: true, out var parsedCmd))
        {
            if (Vnum.TryFromCode<CliCmdAlias>(firstArg, ignoreCase: true, out var parsedAlias))
            {
                parsedCmd = parsedAlias.CliCmdType;
            }
        }

        return parsedCmd ?? CliCmd.Help;
    }

    private static IRequest<Unit> GetApplyCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: Command requires a connection string and at least one dmd model location path");
            return new PrintHelpCommand();
        }

        return new ApplyCommand(
            ConnectionString: args[0],
            ModelLocationPaths: args[1..]);
    }

    private static IRequest<Unit> GetApplyAssembliesCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine($"Error: Command requires a connection string and at least one DLL path");
            return new PrintHelpCommand();
        }

        return new ApplyAssembliesCommand(
            ConnectionString: args[0],
            DllPaths: args[1..]);
    }

    private static IRequest<Unit> GetExportCommand(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine($"Error: Command requires a connection string, schema and output directory path");
            return new PrintHelpCommand();
        }

        return new ExportCommand(
            ConnectionString: args[0],
            Schema: args[1],
            OutputDirectoryPath: args[2]);
    }

    private static IRequest<Unit> GetEfCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Error: EF subcommand required");
            return new PrintHelpCommand();
        }

        var subCommand = CliEfSubCmd.Help;
        if (Vnum.TryFromCode<CliEfSubCmd>(args[0], ignoreCase: true, out var parsedSubCmd))
        {
            subCommand = parsedSubCmd ?? CliEfSubCmd.Help;
        }

        return subCommand.Id switch
        {
            CliEfSubCmdId.Sql => GetEfFromSqlCommand(args[1..]),
            CliEfSubCmdId.Files => GetEfFromFilesCommand(args[1..]),
            CliEfSubCmdId.SqlCustom => GetEfFromSqlCustomCommand(args[1..]),
            _ => new PrintHelpCommand()
        };
    }

    private static IRequest<Unit> GetEfFromSqlCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: ef sql requires <connection-string> <output-path>");
            return new PrintHelpCommand();
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

    private static IRequest<Unit> GetEfFromFilesCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: ef files requires <path1> [path2] [...] <output-path>");
            Console.WriteLine("       Last argument is the output path, all others are input model files");
            return new PrintHelpCommand();
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

    private static IRequest<Unit> GetEfFromSqlCustomCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: ef sql-custom requires <connection-string> <output-path> [options]");
            return new PrintHelpCommand();
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