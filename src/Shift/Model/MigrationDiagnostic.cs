namespace Compile.Shift.Model;

/// <summary>
/// Why a column the model wants changed was not changed. Each value names a distinct decision so
/// callers can react to them differently rather than pattern-matching log prose.
/// </summary>
public enum MigrationDiagnosticKind
{
    /// <summary>
    /// The base type differs and the conversion is not one Shift will apply in place, so the
    /// column is left as it is. Reported by the planner.
    /// </summary>
    UnsupportedTypeChange,

    /// <summary>
    /// The conversion is supported, but the target is too narrow to hold every value the source
    /// type can represent, so it is refused outright rather than applied against whatever happens
    /// to be stored today. Reported by the planner.
    /// </summary>
    TargetTooNarrow,

    /// <summary>
    /// The alter was planned, but live data would not survive it. Reported by the runner.
    /// </summary>
    DataLossRisk,

    /// <summary>
    /// The alter was planned, but another object depends on the column and SQL Server would reject
    /// the change. Reported by the runner.
    /// </summary>
    BlockedByDependency
}

/// <summary>
/// A column change that was wanted but not made, as structured data rather than a log message.
///
/// The planner and the runner both refuse work for reasons the caller cannot otherwise see: no
/// step is emitted, or a step is emitted and then skipped, and in both cases the run reports
/// success. These records are how that becomes visible to a caller, a test, or an operator.
/// </summary>
public class MigrationDiagnostic
{
    public required MigrationDiagnosticKind Kind { get; init; }
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }

    /// <summary>The column's current type, as the database reports it.</summary>
    public string? ActualType { get; init; }

    /// <summary>The type the model asked for.</summary>
    public string? TargetType { get; init; }

    /// <summary>Human-readable explanation, including what to do about it where there is an action to take.</summary>
    public required string Reason { get; init; }

    public override string ToString() => $"{Kind} {TableName}.{ColumnName}: {Reason}";
}