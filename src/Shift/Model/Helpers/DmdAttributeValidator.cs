using System.Text.RegularExpressions;

namespace Compile.Shift.Model.Helpers;

/// <summary>
/// Validates plugin attribute names and values as they are parsed.
///
/// Attribute text flows out of a .dmd file into whatever a plugin generates — DBML notes, group
/// identifiers, generated C#, SQL — so the allow-list is deliberately narrow and the exclusions are
/// the point:
/// <list type="bullet">
/// <item><description>no <c>'</c> or <c>"</c>, so a value cannot close a quoted string it is embedded in;</description></item>
/// <item><description>no <c>[ ] { }</c>, so a value cannot terminate a settings list or a model block;</description></item>
/// <item><description>no <c>/ \ :</c> and no <c>..</c>, so a value can never be read as a path;</description></item>
/// <item><description>no <c>@ # ,</c>, so a value cannot forge a second attribute, a comment or a list separator when the file is re-parsed.</description></item>
/// </list>
/// Validation happens at construction time in the parser, so an invalid attribute fails loudly at
/// the offending line rather than silently reaching a generator.
/// </summary>
public static class DmdAttributeValidator
{
    private static readonly Regex NameRegex =
        new(@"^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.Compiled);

    private static readonly Regex ValueRegex =
        new(@"^[A-Za-z0-9][A-Za-z0-9 ._-]{0,255}$", RegexOptions.Compiled);

    /// <summary>
    /// Creates a validated <see cref="AttributeModel"/>.
    /// </summary>
    /// <param name="name">The attribute name without the leading <c>@</c>.</param>
    /// <param name="value">The attribute value, already unquoted, or <c>null</c> for a flag.</param>
    /// <param name="line">The source line, quoted back in the exception message.</param>
    /// <exception cref="InvalidOperationException">The name or value is not permitted.</exception>
    public static AttributeModel Create(string name, string? value, string line)
    {
        if (!NameRegex.IsMatch(name))
        {
            throw new InvalidOperationException(
                $"Invalid attribute name '{name}'. An attribute name must start with a letter and contain only letters, digits, '_' or '-' (max 64 characters). Line: {line}");
        }

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
}