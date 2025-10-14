namespace Compile.Shift.Model;

public class MigrationStep
{
    public MigrationAction Action { get; set; }
    public string TableName { get; set; } = string.Empty;
    public List<FieldModel> Fields { get; set; } = new List<FieldModel>();
    public ForeignKeyModel? ForeignKey { get; set; }
    public IndexModel? Index { get; set; }
}