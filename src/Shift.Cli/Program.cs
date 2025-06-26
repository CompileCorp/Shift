using Microsoft.Extensions.Logging;

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
            default:
                CommandUsage();
                break;
        }
    }

    private static void CommandUsage()
    {
        Console.WriteLine("usage");
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