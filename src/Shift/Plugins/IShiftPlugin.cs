namespace Compile.Shift.Plugins;

/// <summary>
/// A Shift plugin: anything that consumes a loaded <see cref="Model.DatabaseModel"/> and produces
/// output of its own, such as the Entity Framework generator or the DBML exporter.
///
/// The contract is deliberately small. Its purpose is discoverability: every plugin declares the
/// plugin attributes it understands in one place, so the CLI can list them and so a reviewer can see
/// at a glance that a new attribute behaviour was declared rather than hidden in a generator.
/// A plugin that consumes no attributes declares an empty list.
/// </summary>
public interface IShiftPlugin
{
    /// <summary>The plugin's short name, as it appears in CLI output (for example <c>dbml</c>).</summary>
    string Name { get; }

    /// <summary>One line describing what the plugin produces.</summary>
    string Description { get; }

    /// <summary>Every plugin attribute this plugin interprets.</summary>
    IReadOnlyList<PluginAttributeDefinition> SupportedAttributes { get; }
}