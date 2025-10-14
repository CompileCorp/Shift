namespace Compile.Shift.Model;

public class ExtraIndexReport
{
    public required string TableName { get; init; }
    public required bool IsUnique { get; init; }
    public required IEnumerable<string> Fields { get; init; }
}