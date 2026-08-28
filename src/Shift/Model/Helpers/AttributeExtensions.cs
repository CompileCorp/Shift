namespace Compile.Shift.Model.Helpers;

/// <summary>
/// Read helpers for plugin attribute lists.
///
/// Attributes are stored as an ordered list rather than a dictionary so declaration order and
/// duplicates survive a parse/export round trip: Shift preserves exactly what the author wrote and
/// leaves the semantics of a repeated attribute to the plugin that consumes it. These helpers give
/// plugins the two conventional readings — "is this flag present?" and "what is the effective
/// value?" — with the latter resolving duplicates last-wins.
/// </summary>
public static class AttributeExtensions
{
    /// <summary>
    /// True when an attribute with the given name is present, regardless of whether it carries a
    /// value. Names are compared case-insensitively.
    /// </summary>
    public static bool HasAttribute(this IEnumerable<AttributeModel> attributes, string name)
    {
        return attributes.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The value of the named attribute, or <c>null</c> when it is absent or was declared as a bare
    /// flag. When the attribute is declared more than once the last declaration wins.
    /// </summary>
    public static string? AttributeValue(this IEnumerable<AttributeModel> attributes, string name)
    {
        return attributes
            .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .LastOrDefault();
    }
}