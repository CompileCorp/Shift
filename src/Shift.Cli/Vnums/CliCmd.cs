using Compile.VnumEnumeration;

namespace Compile.Shift.Vnums;

public enum CliCmdId
{
    Help = 1,
    Apply,
    Export,
    EfGenerate,
    ApplyAssemblies,
}

public class CliCmd : Vnum<CliCmdId>
{
    private string Description { get; } = null!;
    private string UsageFormat { get; } = null!;

    private CliCmd(
        CliCmdId id,
        string code,
        string description,
        string usageFormat
    ) : base(id, code)
    {
        Description = description;
        UsageFormat = usageFormat;
    }

    /// <summary>
    /// Print help to console
    /// </summary>
    public static void PrintHelp()
    {
        var allCommands = GetAll<CliCmd>(cmd => cmd != Help).ToList();

        Console.WriteLine("Usage: shift <command> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        allCommands.ForEach(cmd => Console.WriteLine($"  {cmd.UsageFormat.PadRight(60)} {cmd.Description}"));
    }


    public static readonly CliCmd Help =
        new(id: CliCmdId.Help,
            code: "help",
            description: "Print help",
            usageFormat: "help");

    public static readonly CliCmd Apply =
        new(id: CliCmdId.Apply,
            code: "apply",
            description: "Apply DMD/DMDX files from paths to database",
            usageFormat: "apply <connection_string> <path1> [path2] ...");

    public static readonly CliCmd Export =
        new(id: CliCmdId.Export,
            code: "export",
            description: "Export database to DMD/DMDX files",
            usageFormat: "export <connection_string> <schema> <path>");

    public static readonly CliCmd EfGenerate =
        new(id: CliCmdId.EfGenerate,
            code: "ef",
            description: "Entity Framework code generation",
            usageFormat: "ef <sub-command> [options]");

    public static readonly CliCmd ApplyAssemblies =
        new(id: CliCmdId.ApplyAssemblies,
            code: "apply-assemblies",
            description: "Apply DMD/DMDX files from assembly resources to database",
            usageFormat: "apply-assemblies <connection_string> <dll1> [dll2] ... [filter1] [filter2] ...");
}