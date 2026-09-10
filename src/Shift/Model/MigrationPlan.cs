namespace Compile.Shift.Model;

public class MigrationPlan
{
    public List<MigrationStep> Steps { get; set; } = new List<MigrationStep>();
    public ExtrasReport ExtrasInSqlServer { get; set; } = new ExtrasReport();

    /// <summary>
    /// Column changes the model asked for that the planner refused to migrate. These produce no
    /// step, so without recording them the refusal would be invisible to anything but the log.
    /// </summary>
    public List<MigrationDiagnostic> Diagnostics { get; set; } = new List<MigrationDiagnostic>();
}