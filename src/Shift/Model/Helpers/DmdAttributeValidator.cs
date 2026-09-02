using System.Text.RegularExpressions;

namespace Compile.Shift.Model.Helpers;

/// <summary>
/// Validates plugin attribute names and values as they are parsed.
///
/// An attribute name is either a bare local name (<c>NoIdentity</c>) or a namespaced
/// <c>&lt;namespace&gt;:&lt;name&gt;</c> pair (<c>erd:hide</c>). The namespace is structural, not a
/// naming convention: it is split off at the first colon and each half is validated on its own, so
/// a plugin can claim a namespace rather than a prefix. An un-namespaced name stays valid and means
/// "no namespace" — <c>@NoIdentity</c> is a real attribute that Shift itself reads.
///
/// Attribute text flows out of a .dmd file into whatever a plugin generates — DBML notes, group
/// identifiers, generated C#, SQL — so the value allow-list is deliberately narrow and the
/// exclusions are the point:
/// <list type="bullet">
/// <item><description>no <c>'</c> or <c>"</c>, so a value cannot close a quoted string it is embedded in;</description></item>
/// <item><description>no <c>[ ] { }</c>, so a value cannot terminate a settings list or a model block;</description></item>
/// <item><description>no <c>/ \ :</c> and no <c>..</c>, so a value can never be read as a path;</description></item>
/// <item><description>no <c>@ # ,</c>, so a value cannot forge a second attribute, a comment or a list separator when the file is re-parsed.</description></item>
/// </list>
/// The colon is permitted in a <em>name</em> only. It stays forbidden in a value, so a value can
/// never be mistaken for a namespaced name or read as a path.
///
/// Validation happens at construction time in the parser, so an invalid attribute fails loudly at
/// the offending line rather than silently reaching a generator.
/// </summary>
public static class DmdAttributeValidator
{
    /// <summary>
    /// The bound on the whole name as written, the namespace separator included.
    /// </summary>
    private const int MaxNameLength = 64;

    /// <summary>
    /// One half of a name: the namespace, the local name, or a whole un-namespaced name. The colon
    /// is absent here by design — it is consumed by the split, never matched as a name character.
    /// </summary>
    private static readonly Regex NamePartRegex =
        new(@"^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.Compiled);

    private static readonly Regex ValueRegex =
        new(@"^[A-Za-z0-9][A-Za-z0-9 ._-]{0,255}$", RegexOptions.Compiled);

    /// <summary>
    /// Creates a validated <see cref="AttributeModel"/>.
    /// </summary>
    /// <param name="name">
    /// The attribute name without the leading <c>@</c>, either <c>name</c> or <c>namespace:name</c>.
    /// </param>
    /// <param name="value">The attribute value, already unquoted, or <c>null</c> for a flag.</param>
    /// <param name="line">The source line, quoted back in the exception message.</param>
    /// <exception cref="InvalidOperationException">The name or value is not permitted.</exception>
    public static AttributeModel Create(string name, string? value, string line)
    {
        ValidateName(name, line);

        if (value == null)
        {
            return new AttributeModel(name, null);
        }

        var trimmed = value.Trim();

        // The value regex already rejects a bare "." or ".."; this also rejects "a..b".
        if (!ValueRegex.IsMatch(trimmed) || trimmed.Contains(".."))
        {
            throw new InvalidOperationException(
                $"Invalid value '{value}' for attribute '@{name}'. A value must start with a letter or digit and contain only letters, digits, spaces, '.', '_' or '-' (max 256 characters), and may not contain '..'. Line: {line}");
        }

        return new AttributeModel(name, trimmed);
    }

    private static void ValidateName(string name, string line)
    {
        if (name.Length > MaxNameLength)
        {
            throw new InvalidOperationException(
                $"Invalid attribute name '{name}'. An attribute name may be at most {MaxNameLength} characters, including any namespace. Line: {line}");
        }

        var colonCount = name.Count(character => character == ':');

        if (colonCount > 1)
        {
            throw new InvalidOperationException(
                $"Invalid attribute name '{name}'. An attribute name may contain at most one ':', separating a namespace from a name ('erd:hide'). Line: {line}");
        }

        if (colonCount == 0)
        {
            // An un-namespaced name is legitimate: @NoIdentity is read by Shift itself.
            if (!NamePartRegex.IsMatch(name))
            {
                throw new InvalidOperationException(
                    $"Invalid attribute name '{name}'. {NameRuleText} Line: {line}");
            }

            return;
        }

        var colon = name.IndexOf(':');
        var attributeNamespace = name[..colon];
        var localName = name[(colon + 1)..];

        if (!NamePartRegex.IsMatch(attributeNamespace) || !NamePartRegex.IsMatch(localName))
        {
            throw new InvalidOperationException(
                $"Invalid attribute name '{name}'. Both halves of a namespaced attribute name must be non-empty. {NameRuleText} Line: {line}");
        }
    }

    private const string NameRuleText =
        "Each part must start with a letter and contain only letters, digits, '_' or '-'.";
}