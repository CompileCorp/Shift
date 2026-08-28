namespace Compile.Shift.Model;

/// <summary>
/// A plugin attribute declared in a DMD/DMDX file, for example <c>@erd-hide</c> or
/// <c>@erd-group 'Billing Ops'</c>.
///
/// Shift parses and preserves attributes but does not interpret them, with the single exception of
/// <c>@NoIdentity</c>. Their meaning belongs to whichever plugin consumes them (the DBML exporter,
/// for example), which keeps the core model free of plugin-specific vocabulary.
/// </summary>
/// <param name="Name">The attribute name without the leading <c>@</c>.</param>
/// <param name="Value">The attribute value, or <c>null</c> when the attribute is a bare flag.</param>
public sealed record AttributeModel(string Name, string? Value)
{
    /// <summary>
    /// True when the attribute was declared without a value (a flag such as <c>@erd-hide</c>).
    /// A declared-but-empty value is deliberately not a flag.
    /// </summary>
    public bool IsFlag => Value is null;

    public override string ToString() => IsFlag ? $"@{Name}" : $"@{Name} {Value}";
}