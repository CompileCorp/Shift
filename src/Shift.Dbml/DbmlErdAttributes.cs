namespace Compile.Shift.Dbml;

/// <summary>
/// The plugin attribute names this exporter understands.
///
/// This is the only place in the repository that knows the <c>erd-*</c> vocabulary: Shift core
/// parses and preserves attributes without interpreting them, so a new diagram concern is added
/// here rather than in the parser or the DMD exporter.
/// </summary>
public static class DbmlErdAttributes
{
    /// <summary>Flag. On a model, omits the table; on a field, omits the column.</summary>
    public const string Hide = "erd-hide";

    /// <summary>Valued. On a model, the <c>TableGroup</c> the table belongs to. Ignored on a field.</summary>
    public const string Group = "erd-group";

    /// <summary>Valued. A DBML note on the table or column.</summary>
    public const string Note = "erd-note";

    /// <summary>
    /// Valued. A model-level header colour, as 3 or 6 hex digits. Written without a leading <c>#</c>
    /// in a .dmd file, because <c>#</c> is not a permitted attribute-value character.
    /// </summary>
    public const string Color = "erd-color";
}