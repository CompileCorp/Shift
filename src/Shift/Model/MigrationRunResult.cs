namespace Compile.Shift.Model;

/// <summary>
/// The outcome of running a migration plan, split three ways.
///
/// A plain failure list is not enough to describe a run: the runner also skips steps it judges
/// unsafe or impossible, and a skip is neither a failure nor an application. Reporting only
/// failures made a run in which every step was skipped indistinguishable from one in which every
/// step succeeded.
/// </summary>
public class MigrationRunResult
{
    /// <summary>Steps whose SQL was executed without error.</summary>
    public List<MigrationStep> Applied { get; } = new();

    /// <summary>Steps the runner declined to execute, with the reason for each.</summary>
    public List<MigrationDiagnostic> Skipped { get; } = new();

    /// <summary>Steps whose SQL was executed and threw.</summary>
    public List<(MigrationStep Step, Exception Exception)> Failures { get; } = new();

    /// <summary>True when anything the plan asked for did not happen, for whatever reason.</summary>
    public bool HasUnappliedWork => Failures.Count > 0 || Skipped.Count > 0;
}