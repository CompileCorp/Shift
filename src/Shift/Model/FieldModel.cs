namespace Compile.Shift.Model;

public class FieldModel
{
    private string _type = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public required string Name { get; set; } = string.Empty;

    public required string Type
    {
        get => _type;
        set
        {
            if (value == "mixin")
                throw new Exception();

            _type = value;

        }
    }

    public bool IsNullable { get; set; }
    public bool IsOptional { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }

    /// <summary>
    /// Plugin attributes declared as trailing tokens on the field declaration, for example
    /// <c>ustring(100) Email @erd-hide @erd-note 'PII'</c>.
    /// </summary>
    public List<AttributeModel> Attributes { get; set; } = new List<AttributeModel>();

    public override string ToString()
    {
        return $"Field:\"{Name}\" Type:\"{Type}\"";
    }
}