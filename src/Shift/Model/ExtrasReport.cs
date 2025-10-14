namespace Compile.Shift.Model;

public class ExtrasReport
{
    public List<string> ExtraTables { get; set; } = new List<string>();
    public List<ExtraColumnReport> ExtraColumns { get; set; } = new List<ExtraColumnReport>();
    public List<ExtraIndexReport> ExtraIndexes { get; set; } = new List<ExtraIndexReport>();
}