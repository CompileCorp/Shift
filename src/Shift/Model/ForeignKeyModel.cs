namespace Compile.Shift.Model;

public class ForeignKeyModel
{
    public string TargetTable { get; set; } = string.Empty;
    public string TargetColumnName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public RelationshipType RelationshipType { get; set; }

    public override string ToString() => $"{ColumnName} {TargetTable} {TargetColumnName}";
}