namespace Compile.Shift.Model;

public class DatabaseModel
{
    public Dictionary<string, TableModel> Tables { get; set; } = new();

    public Dictionary<string, MixinModel> Mixins { get; set; } = new();
}