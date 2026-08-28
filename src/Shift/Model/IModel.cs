namespace Compile.Shift.Model;

public interface IModel
{
    public string Name { get; set; }
    public List<FieldModel> Fields { get; set; }
    public List<ForeignKeyModel> ForeignKeys { get; set; }

    /// <summary>
    /// Plugin attributes declared on the model or mixin itself. Declared on the interface so a
    /// single parsing path can serve both models and mixins.
    /// </summary>
    public List<AttributeModel> Attributes { get; set; }
}