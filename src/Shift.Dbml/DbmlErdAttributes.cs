namespace Compile.Shift.Dbml;

/// <summary>
/// The plugin attribute names this exporter understands, as <em>local</em> names within the
/// namespace declared by <see cref="Namespace"/>.
///
/// This is the only place in the repository that knows the <c>erd</c> vocabulary: Shift core
/// parses and preserves attributes without interpreting them, so a new diagram concern is added
/// here rather than in the parser or the DMD exporter.
///
/// The constants are deliberately unqualified — <c>hide</c>, not <c>erd:hide</c> — because the
/// exporter is handed its namespace's attributes already filtered and stripped, so nothing in its
/// lookup paths has to know the prefix. <see cref="Namespace"/> is declared once and applied by the
/// plugin registration.
/// </summary>
public static class DbmlErdAttributes
{
    /// <summary>
    /// The attribute namespace this exporter claims. The single place the prefix is spelled out.
    /// </summary>
    public const string Namespace = "erd";

    /// <summary>Flag. On a model, omits the table; on a field, omits the column.</summary>
    public const string Hide = "hide";

    /// <summary>Valued. On a model, the <c>TableGroup</c> the table belongs to. Ignored on a field.</summary>
    public const string Group = "group";

    /// <summary>Valued. A DBML note on the table or column.</summary>
    public const string Note = "note";

    /// <summary>
    /// Valued. A model-level header colour, as 3 or 6 hex digits. Written without a leading <c>#</c>
    /// in a .dmd file, because <c>#</c> is not a permitted attribute-value character.
    /// </summary>
    public const string Color = "color";
}