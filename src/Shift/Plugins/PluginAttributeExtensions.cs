using Compile.Shift.Model;

namespace Compile.Shift.Plugins;

/// <summary>
/// Delivers a plugin the attributes in its own namespace, already filtered and with the namespace
/// stripped, so no plugin ever matches a prefix itself.
///
/// This is an extension method rather than an injected service on purpose: reading an attribute list
/// is a pure projection with no dependencies and no lifetime, and the codebase already reads
/// attributes through extensions
/// (<see cref="Model.Helpers.AttributeExtensions.HasAttribute"/> /
/// <see cref="Model.Helpers.AttributeExtensions.AttributeValue"/>). Making it an extension also means
/// the scoped view composes with those two directly instead of duplicating them, which is what keeps
/// this a projection rather than a resolver.
/// </summary>
public static class PluginAttributeExtensions
{
    /// <summary>
    /// The attributes belonging to one namespace, in declaration order, each renamed to its local
    /// name — a consumer of the <c>erd</c> namespace sees <c>hide</c> and <c>group</c>, never
    /// <c>erd:hide</c>.
    ///
    /// The projection returns new <see cref="AttributeModel"/> instances; the attributes on the model
    /// keep their full <c>Name</c>, so export and round-tripping are unaffected. Order is preserved,
    /// so composing this with <see cref="Model.Helpers.AttributeExtensions.AttributeValue"/> keeps the
    /// same last-wins reading of a duplicate, and a flag stays a flag because only the name changes.
    ///
    /// Passing <c>null</c> selects the un-namespaced attributes (<c>@NoIdentity</c>), which is what a
    /// plugin claiming no namespace would be handed.
    /// </summary>
    /// <param name="attributes">The unfiltered attribute list from a model, mixin or field.</param>
    /// <param name="attributeNamespace">
    /// The namespace to select, as declared by the plugin, or <c>null</c> for the un-namespaced ones.
    /// </param>
    public static IReadOnlyList<AttributeModel> InNamespace(
        this IEnumerable<AttributeModel> attributes,
        string? attributeNamespace)
    {
        return attributes
            .Where(attribute => string.Equals(
                attribute.Namespace,
                attributeNamespace,
                StringComparison.OrdinalIgnoreCase))
            .Select(attribute => new AttributeModel(attribute.LocalName, attribute.Value))
            .ToList();
    }
}