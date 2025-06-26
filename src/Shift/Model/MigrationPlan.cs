namespace Compile.Shift.Model;

public class MigrationPlan
{
    public List<MigrationStep> Steps { get; set; } = new List<MigrationStep>();
    public ExtrasReport ExtrasInSqlServer { get; set; } = new ExtrasReport();
}