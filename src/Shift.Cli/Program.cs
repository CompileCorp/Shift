using Microsoft.Extensions.Logging;
using Compile.Shift.Plugins;

namespace Compile.Shift.Cli;

internal class Program
{
    private static PluginLoader? _pluginLoader;
    private static Shift? _shiftInstance;

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

        var logger = loggerFactory.CreateLogger("Shift.Cli");

        // Initialize Shift instance
        _shiftInstance = new Shift() { Logger = logger };

        // Initialize plugin loader and load plugins
        _pluginLoader = new PluginLoader(logger);
        LoadPlugins(logger);

        // change files to dmd and dmdx
        Console.WriteLine("🧠 Domain Migration Definition (DMD) System");

        if (args.Length == 0)
        {
            CommandUsage();
            return;
        }

        var command = args[0].ToLowerInvariant();

        // Try to handle command with plugins first
        var plugin = _pluginLoader.FindPluginForCommand(command);
        if (plugin != null)
        {
            var success = await plugin.ExecuteAsync(command, args[1..], _shiftInstance);
            if (success)
                return;
        }

        // Fall back to built-in commands
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
            case "plugins":
                CommandShowPlugins();
                break;
            default:
                CommandUsage();
                break;
        }
    }

    private static void LoadPlugins(ILogger logger)
    {
        try
        {
            // Load plugins from the plugins directory next to the executable
            var pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            _pluginLoader?.LoadPluginsFromDirectory(pluginDirectory);

            // Also try to load from the same directory as the executable
            var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var potentialPlugins = Directory.GetFiles(currentDirectory, "Shift.*.dll", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).Equals("Shift.dll", StringComparison.OrdinalIgnoreCase))
                .Where(f => !Path.GetFileName(f).Equals("Shift.Cli.dll", StringComparison.OrdinalIgnoreCase));

            foreach (var pluginFile in potentialPlugins)
            {
                _pluginLoader?.LoadPluginFromAssembly(pluginFile);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load plugins");
        }
    }

    private static void CommandUsage()
    {
        Console.WriteLine("Usage: shift <command> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Built-in commands:");
        Console.WriteLine("  apply <connection-string> <paths...>  Apply migration files to database");
        Console.WriteLine("  export <connection-string> <schema>   Export database schema (not implemented)");
        Console.WriteLine("  plugins                               Show loaded plugins");
        Console.WriteLine();
        
        if (_pluginLoader != null)
        {
            var pluginUsage = _pluginLoader.GetAllPluginUsage();
            if (!string.IsNullOrEmpty(pluginUsage) && !pluginUsage.Contains("No plugins"))
            {
                Console.WriteLine(pluginUsage);
            }
        }
    }

    private static void CommandShowPlugins()
    {
        Console.WriteLine("=== Loaded Plugins ===");
        
        if (_pluginLoader == null)
        {
            Console.WriteLine("Plugin loader not initialized.");
            return;
        }

        var plugins = _pluginLoader.GetLoadedPlugins();
        if (!plugins.Any())
        {
            Console.WriteLine("No plugins loaded.");
            Console.WriteLine();
            Console.WriteLine("To load plugins:");
            Console.WriteLine("1. Place plugin DLLs in the 'plugins' subdirectory");
            Console.WriteLine("2. Or place plugin DLLs in the same directory as shift.exe");
            return;
        }

        foreach (var plugin in plugins)
        {
            Console.WriteLine($"📦 {plugin.Name} v{plugin.Version}");
            Console.WriteLine($"   {plugin.Description}");
            Console.WriteLine($"   Commands: {string.Join(", ", plugin.SupportedCommands)}");
            Console.WriteLine();
        }
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