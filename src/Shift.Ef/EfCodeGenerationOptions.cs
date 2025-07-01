namespace Compile.Shift.Ef;

public class EfCodeGenerationOptions
{
    public string NamespaceName { get; set; } = "Generated";
    public string ContextClassName { get; set; } = "GeneratedDbContext";
    public string InterfaceName { get; set; } = "IGeneratedDbContext";
    public string? BaseClassName { get; set; }
}