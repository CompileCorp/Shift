using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Compile.Shift.Plugins;

/// <summary>
/// Loads and manages Shift plugins
/// </summary>
public class PluginLoader
{
    private readonly ILogger _logger;
    private readonly List<IShiftPlugin> _loadedPlugins = new();

    public PluginLoader(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load plugins from the specified directory
    /// </summary>
    public void LoadPluginsFromDirectory(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogDebug("Plugin directory does not exist: {Directory}", pluginDirectory);
            return;
        }

        var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllFile in dllFiles)
        {
            try
            {
                LoadPluginFromAssembly(dllFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin from {File}", dllFile);
            }
        }
    }

    /// <summary>
    /// Load a specific plugin assembly
    /// </summary>
    public void LoadPluginFromAssembly(string assemblyPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IShiftPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var pluginType in pluginTypes)
            {
                var plugin = (IShiftPlugin?)Activator.CreateInstance(pluginType);
                if (plugin != null)
                {
                    plugin.Initialize(_logger);
                    _loadedPlugins.Add(plugin);
                    _logger.LogInformation("Loaded plugin: {Name} v{Version}", plugin.Name, plugin.Version);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin assembly: {AssemblyPath}", assemblyPath);
        }
    }

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    public IReadOnlyList<IShiftPlugin> GetLoadedPlugins() => _loadedPlugins.AsReadOnly();

    /// <summary>
    /// Find a plugin that supports the given command
    /// </summary>
    public IShiftPlugin? FindPluginForCommand(string command)
    {
        return _loadedPlugins.FirstOrDefault(p => 
            p.SupportedCommands.Contains(command, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get usage information for all loaded plugins
    /// </summary>
    public string GetAllPluginUsage()
    {
        if (!_loadedPlugins.Any())
        {
            return "No plugins loaded.";
        }

        var usage = "Available plugins:\n\n";
        foreach (var plugin in _loadedPlugins)
        {
            usage += $"=== {plugin.Name} v{plugin.Version} ===\n";
            usage += $"{plugin.Description}\n";
            usage += $"{plugin.GetUsage()}\n\n";
        }

        return usage;
    }
}