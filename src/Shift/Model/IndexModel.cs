namespace Compile.Shift.Model;

public class IndexModel
{
    public List<string> Fields { get; set; } = new List<string>();
    public bool IsUnique { get; set; }
    public IndexKind Kind { get; set; }
}