namespace Compile.Shift.Plugins;

/// <summary>
/// Where a plugin attribute may be declared in a .dmd or .dmdx file.
/// </summary>
public enum AttributeScope
{
    /// <summary>On its own line inside a model or mixin block.</summary>
    Model = 1,

    /// <summary>As a trailing token on a field declaration.</summary>
    Field,

    /// <summary>Both of the above.</summary>
    Both,
}