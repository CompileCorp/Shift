namespace Compile.Shift.Model;

public class DatabaseModel
{
    /// <summary>
    /// Keyed case-insensitively, as SQL Server table names are: a DSL reference to <c>user</c> is the
    /// <c>User</c> table, and treating it as a different, unknown table silently dropped the FK.
    /// </summary>
    public Dictionary<string, TableModel> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, MixinModel> Mixins { get; set; } = new();
}