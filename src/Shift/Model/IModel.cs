namespace Compile.Shift.Model;

public interface IModel
{
	public string Name { get; set; }
	public List<FieldModel> Fields { get; set; }
	public List<ForeignKeyModel> ForeignKeys { get; set; }
}