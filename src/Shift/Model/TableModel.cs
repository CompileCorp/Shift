namespace Compile.Shift.Model;

public class TableModel : IModel
{
    public string Name { get; set; } = string.Empty;
    public List<FieldModel> Fields { get; set; } = new List<FieldModel>();
    public List<ForeignKeyModel> ForeignKeys { get; set; } = new List<ForeignKeyModel>();
    public List<IndexModel> Indexes { get; set; } = new List<IndexModel>();
    public List<AttributeModel> Attributes { get; set; } = new List<AttributeModel>();
    public List<string> Mixins { get; set; } = new List<string>();

    public override string ToString()
    {
        // string.Join (not Aggregate) so ToString never throws on a table with no fields.
        return $"Name:\"{Name}\"\nFields:{{\n\t{string.Join("\n\t", Fields.Select(x => x.ToString()))}\n\t}}";
    }
}