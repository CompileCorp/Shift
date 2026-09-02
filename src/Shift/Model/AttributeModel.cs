namespace Compile.Shift.Model;

/// <summary>
/// A plugin attribute declared in a DMD/DMDX file, for example <c>@erd:hide</c> or
/// <c>@erd:group 'Billing Ops'</c>.
///
/// An attribute name is either namespaced (<c>erd:hide</c> — namespace <c>erd</c>, local name
/// <c>hide</c>) or un-namespaced (<c>NoIdentity</c>). <see cref="Name"/> keeps the full spelling
/// exactly as written so exporting is unchanged and a parse/export round trip stays byte-exact;
/// <see cref="Namespace"/> and <see cref="LocalName"/> expose the two halves so a consumer never has
/// to re-parse the string.
///
/// Shift parses and preserves attributes but does not interpret them, with the single exception of
/// <c>@NoIdentity</c>. Their meaning belongs to whichever plugin consumes them (the DBML exporter,
/// for example), which keeps the core model free of plugin-specific vocabulary. An attribute in a
/// namespace no registered plugin claims is preserved just the same rather than being an error.
/// </summary>
/// <param name="Name">
/// The attribute name without the leading <c>@</c>, as written: <c>name</c> or <c>namespace:name</c>.
/// </param>
/// <param name="Value">The attribute value, or <c>null</c> when the attribute is a bare flag.</param>
public sealed record AttributeModel(string Name, string? Value)
{
    /// <summary>
    /// True when the attribute was declared without a value (a flag such as <c>@erd:hide</c>).
    /// A declared-but-empty value is deliberately not a flag.
    /// </summary>
    public bool IsFlag => Value is null;

    /// <summary>
    /// The namespace: everything before the first <c>:</c> in <see cref="Name"/>, or <c>null</c> when
    /// the name is un-namespaced. <c>null</c> rather than an empty string, so "no namespace" cannot be
    /// confused with a namespace that happens to be blank — the validator rejects the latter.
    /// </summary>
    public string? Namespace
    {
        get
        {
            var colon = Name.IndexOf(':');
            return colon < 0 ? null : Name[..colon];
        }
    }

    /// <summary>
    /// The local name: everything after the first <c>:</c> in <see cref="Name"/>, or the whole name
    /// when it is un-namespaced.
    /// </summary>
    public string LocalName
    {
        get
        {
            var colon = Name.IndexOf(':');
            return colon < 0 ? Name : Name[(colon + 1)..];
        }
    }

    public override string ToString() => IsFlag ? $"@{Name}" : $"@{Name} {Value}";
}