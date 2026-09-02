namespace Compile.Shift.Plugins;

/// <summary>
/// One plugin attribute, as declared by the plugin that interprets it.
///
/// Shift core never interprets these names; the declaration exists so the CLI can tell an author
/// which attributes are available without them having to read plugin source.
///
/// The namespace and the local name are carried separately, which mirrors how a plugin actually
/// works: it declares its namespace once and names only local attributes thereafter.
/// <see cref="Name"/> composes them back into the spelling the author writes, so `shift attributes`
/// can group by namespace and still print a line that can be copied into a .dmd file.
/// </summary>
/// <param name="Namespace">
/// The namespace the declaring plugin claims, or <c>null</c> for an un-namespaced attribute.
/// </param>
/// <param name="LocalName">The attribute name within the namespace, without the leading <c>@</c>.</param>
/// <param name="Scope">Where the attribute may be declared.</param>
/// <param name="IsFlag">True when the attribute takes no value.</param>
/// <param name="Description">One line describing what the plugin does with it.</param>
public sealed record PluginAttributeDefinition(
    string? Namespace,
    string LocalName,
    AttributeScope Scope,
    bool IsFlag,
    string Description)
{
    /// <summary>
    /// The full spelling as authored in a .dmd file, without the leading <c>@</c>:
    /// <c>namespace:name</c>, or just the local name when there is no namespace.
    /// </summary>
    public string Name => Namespace is null ? LocalName : $"{Namespace}:{LocalName}";
}