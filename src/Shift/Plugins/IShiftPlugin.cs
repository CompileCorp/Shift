using Compile.Shift.Model;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Plugins;

/// <summary>
/// Interface for Shift plugins that can be dynamically loaded
/// </summary>
public interface IShiftPlugin
{
    /// <summary>
    /// Name of the plugin
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what the plugin does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Version of the plugin
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Commands supported by this plugin
    /// </summary>
    IEnumerable<string> SupportedCommands { get; }
    
    /// <summary>
    /// Initialize the plugin with logger
    /// </summary>
    void Initialize(ILogger logger);
    
    /// <summary>
    /// Execute a command with the given arguments
    /// </summary>
    Task<bool> ExecuteAsync(string command, string[] args, Shift shiftInstance);
    
    /// <summary>
    /// Get usage information for the plugin commands
    /// </summary>
    string GetUsage();
}