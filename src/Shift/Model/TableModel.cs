namespace Compile.Shift.Model;

public class TableModel : IModel
{
    public string Name { get; set; } = string.Empty;
    public List<FieldModel> Fields { get; set; } = new List<FieldModel>();
    public List<ForeignKeyModel> ForeignKeys { get; set; } = new List<ForeignKeyModel>();
    public List<IndexModel> Indexes { get; set; } = new List<IndexModel>();
    public Dictionary<string, bool> Attributes { get; set; } = new Dictionary<string, bool>();
    public List<string> Mixins { get; set; } = new List<string>();

    public override string ToString()
    {
        return $"Name:\"{Name}\"\nFields:{{\n\t{Fields.Select(x => x.ToString()).Aggregate((a, b) => a + "\n\t" + b)}\n\t}}";
    }
}