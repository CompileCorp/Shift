using Microsoft.Extensions.Logging;
using Compile.Shift.Ef;

namespace Compile.Shift.Cli;

internal class Program
{
    static async Task Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information) // or Information, etc.
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
        });

        // change files to dmd and dmdx
        Console.WriteLine("🧠 Domain Migration Definition (DMD) System");

        if (args.Length == 0)
        {
            CommandUsage();
            return;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            //dryapply
            case "apply":
                await CommandApplyAsync(args[1..], loggerFactory);
                break;
            //dryexport
            case "export":
                await CommandExportAsync(args[1..], loggerFactory);
                break;
            case "generate-ef":
            case "ef-generate":
            case "ef":
                await CommandGenerateEfAsync(args[1..], loggerFactory);
                break;
            default:
                CommandUsage();
                break;
        }
    }

    private static void CommandUsage()
    {
        Console.WriteLine("Usage: shift <command> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  apply <connection-string> <paths...>             Apply migration files to database");
        Console.WriteLine("  export <connection-string> <schema>              Export database schema (not implemented)");
        Console.WriteLine("  ef <sub-command> [options]                       Entity Framework code generation");
        Console.WriteLine();
        Console.WriteLine("EF Commands:");
        Console.WriteLine("  ef sql <connection-string> <output-path>         Generate EF code from SQL Server");
        Console.WriteLine("  ef files <paths...> <output-path>                Generate EF code from model files");
        Console.WriteLine("  ef sql-custom <connection-string> <output-path>  Generate with custom options");
        Console.WriteLine("    [--namespace <name>] [--context <name>]");
        Console.WriteLine("    [--interface <name>] [--base-class <name>]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  shift ef sql \"Server=.;Database=MyDb;\" ./Generated");
        Console.WriteLine("  shift ef files ./Models/User.yaml ./Models/Order.yaml ./Generated");
        Console.WriteLine("  shift ef sql-custom \"Server=.;Database=MyDb;\" ./Generated \\");
        Console.WriteLine("    --namespace MyApp.Data --context MyDbContext --interface IMyDbContext");
    }

    private static async Task CommandApplyAsync(string[] args, ILoggerFactory loggerFactory)
    {
        var connectionString = args[0];
        var paths = args[1..];

        var logger = loggerFactory.CreateLogger("Logger");

        var system = new Shift() { Logger = logger };

        var targetModel = await system.LoadFromPathAsync(paths);
        await system.ApplyToSqlAsync(targetModel, connectionString);
    }

    private static Task CommandExportAsync(string[] args, ILoggerFactory loggerFactory)
    {
        var connectionString = args[0];
        var schema = args[1];
        var path = args[2];
        throw new NotImplementedException();
    }

    private static async Task CommandGenerateEfAsync(string[] args, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Shift.Ef");

        if (args.Length == 0)
        {
            Console.WriteLine("Error: EF subcommand required");
            Console.WriteLine();
            CommandUsage();
            return;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            switch (subCommand)
            {
                case "sql":
                    await CommandEfFromSqlAsync(args[1..], logger);
                    break;
                case "files":
                    await CommandEfFromFilesAsync(args[1..], logger);
                    break;
                case "sql-custom":
                    await CommandEfFromSqlCustomAsync(args[1..], logger);
                    break;
                default:
                    Console.WriteLine($"Error: Unknown EF subcommand '{subCommand}'");
                    Console.WriteLine();
                    CommandUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Entity Framework code generation failed");
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    private static async Task CommandEfFromSqlAsync(string[] args, ILogger logger)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: ef sql requires <connection-string> <output-path>");
            return;
        }

        var connectionString = args[0];
        var outputPath = args[1];
        var schema = args.Length > 2 ? args[2] : "dbo";

        Console.WriteLine($"🏗️  Generating Entity Framework code from SQL Server...");
        Console.WriteLine($"   Connection: {connectionString}");
        Console.WriteLine($"   Schema: {schema}");
        Console.WriteLine($"   Output: {outputPath}");

        var shift = new Shift { Logger = logger };
        await shift.GenerateEfCodeFromSqlAsync(connectionString, outputPath, logger);

        Console.WriteLine("✅ Entity Framework code generation completed!");
    }

    private static async Task CommandEfFromFilesAsync(string[] args, ILogger logger)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: ef files requires <path1> [path2] [...] <output-path>");
            Console.WriteLine("       Last argument is the output path, all others are input model files");
            return;
        }

        var outputPath = args[^1]; // Last argument is output path
        var inputPaths = args[..^1]; // All but last are input paths

        Console.WriteLine($"🏗️  Generating Entity Framework code from model files...");
        Console.WriteLine($"   Input files: {string.Join(", ", inputPaths)}");
        Console.WriteLine($"   Output: {outputPath}");

        var shift = new Shift { Logger = logger };
        await shift.GenerateEfCodeFromPathAsync(inputPaths, outputPath, logger);

        Console.WriteLine("✅ Entity Framework code generation completed!");
    }

    private static async Task CommandEfFromSqlCustomAsync(string[] args, ILogger logger)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: ef sql-custom requires <connection-string> <output-path> [options]");
            return;
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

        Console.WriteLine($"🏗️  Generating Entity Framework code from SQL Server with custom options...");
        Console.WriteLine($"   Connection: {connectionString}");
        Console.WriteLine($"   Output: {outputPath}");
        Console.WriteLine($"   Namespace: {options.NamespaceName}");
        Console.WriteLine($"   Context: {options.ContextClassName}");
        Console.WriteLine($"   Interface: {options.InterfaceName}");
        if (!string.IsNullOrEmpty(options.BaseClassName))
            Console.WriteLine($"   Base Class: {options.BaseClassName}");

        var shift = new Shift { Logger = logger };
        await shift.GenerateEfCodeFromSqlAsync(connectionString, outputPath, logger, options);

        Console.WriteLine("✅ Entity Framework code generation completed!");
    }

    /*
	// Check for export command
	if (args.Length >= 3 && args[0] == "export")
	{
		var connectionString = args[1];
		var outputFolder = args[2];
		var schema = args.Length >= 4 ? args[3] : null;

		// Parse mixin files if provided
		var mixinFiles = new List<string>();
		var mixinsIndex = Array.IndexOf(args, "--mixins");
		if (mixinsIndex >= 0 && mixinsIndex + 1 < args.Length)
		{
			for (int i = mixinsIndex + 1; i < args.Length; i++)
			{
				if (args[i].StartsWith("--"))
					break;
				mixinFiles.Add(args[i]);
			}
		}

		ExportDatabaseToDmd(connectionString, outputFolder, schema, mixinFiles);
		return;
	}

static void DisplayExtrasReport(Models.ExtrasReport extras)
{
	if (!extras.ExtraTables.Any() && !extras.ExtraColumns.Any())
	{
		return;
	}

	Console.WriteLine("📊 EXTRAS IN SQL SERVER (Not in DMD files)");
	Console.WriteLine("==========================================");

	if (extras.ExtraTables.Any())
	{
		Console.WriteLine("📦 Extra Tables:");
		foreach (var table in extras.ExtraTables)
		{
			Console.WriteLine($"   - {table}");
		}
		Console.WriteLine();
	}

	if (extras.ExtraColumns.Any())
	{
		Console.WriteLine("➕ Extra Columns:");
		foreach (var column in extras.ExtraColumns)
		{
			Console.WriteLine($"   - {column.TableName}.{column.ColumnName} ({column.DataType})");
		}
		Console.WriteLine();
	}

	Console.WriteLine("ℹ️  These items exist in SQL Server but are not defined in your DMD files.");
	Console.WriteLine("   They will not be included in the migration plan.");
	*/

}