namespace Compile.Shift.Model;

public class MixinModel : IModel
{
    public string Name { get; set; } = string.Empty;
    public List<FieldModel> Fields { get; set; } = new();
    public List<ForeignKeyModel> ForeignKeys { get; set; } = new();
}