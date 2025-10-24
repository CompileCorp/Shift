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

    public override string ToString()
    {
        return $"Field:\"{Name}\" Type:\"{Type}\"";
    }
}