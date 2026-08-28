namespace Compile.Shift.Plugins;

/// <summary>
/// One plugin attribute, as declared by the plugin that interprets it.
///
/// Shift core never interprets these names; the declaration exists so the CLI can tell an author
/// which attributes are available without them having to read plugin source.
/// </summary>
/// <param name="Name">The attribute name without the leading <c>@</c>.</param>
/// <param name="Scope">Where the attribute may be declared.</param>
/// <param name="IsFlag">True when the attribute takes no value.</param>
/// <param name="Description">One line describing what the plugin does with it.</param>
public sealed record PluginAttributeDefinition(
    string Name,
    AttributeScope Scope,
    bool IsFlag,
    string Description);