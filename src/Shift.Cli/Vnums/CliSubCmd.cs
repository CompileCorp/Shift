using Compile.VnumEnumeration;

namespace Compile.Shift.Vnums;

public enum CliSubCmdId
{
    Help = 1,
    Sql,
    SqlCustom,
    Files,
}

public class CliSubCmd : Vnum<CliSubCmdId>
{
    private string Description { get; } = null!;
    private string Example { get; } = null!;
    private string UsageFormat { get; } = null!;
    private List<string>? ParamsUsageFormat { get; }

    private CliSubCmd(
        CliSubCmdId id,
        string code,
        string description,
        string example,
        string usageFormat,
        List<string>? paramsUsage = null

    ) : base(id, code)
    {
        Description = description;
        Example = example;
        UsageFormat = usageFormat;
        ParamsUsageFormat = paramsUsage;
    }

    /// <summary>
    /// Print help to console
    /// </summary>
    public static void PrintHelp()
    {
        var allSubCommands = GetAll<CliSubCmd>(c => c != Help).ToList();

        Console.WriteLine("EF Commands:");
        allSubCommands.ForEach(c =>
        {
            Console.WriteLine($"  {c.UsageFormat.PadRight(60)} {c.Description}");
            c.ParamsUsageFormat?.ForEach(p => Console.WriteLine($"    {p}"));
        });

        Console.WriteLine();
        Console.WriteLine("Examples:");
        allSubCommands.ForEach(c => Console.WriteLine($"  {c.Example}"));
    }


    public static readonly CliSubCmd Help =
        new(id: CliSubCmdId.Help,
            code: "help",
            description: "Print help",
            example: "",
            usageFormat: "ef help");

    public static readonly CliSubCmd Sql =
        new(id: CliSubCmdId.Sql,
            code: "sql",
            description: "Generate EF code from SQL Server",
            example: "shift ef sql \"Server=.;Database=MyDb;\" ./Generated",
            usageFormat: "ef sql <connection-string> <output-path>");

    public static readonly CliSubCmd SqlCustom =
        new(id: CliSubCmdId.SqlCustom,
            code: "sql-custom",
            description: "Generate with custom options",
            example: "shift ef sql-custom \"Server=.;Database=MyDb;\" ./Generated \r\n    --namespace MyApp.Data --context MyDbContext --interface IMyDbContext",
            usageFormat: "ef sql-custom <connection-string> <output-path>",
            paramsUsage: ["[--namespace <name>] [--context <name>]", "[--interface <name>] [--base-class <name>]"]);

    public static readonly CliSubCmd Files =
       new(id: CliSubCmdId.Files,
           code: "files",
           description: "Generate EF code from model files",
           example: "shift ef files ./Models/User.yaml ./Models/Order.yaml ./Generated",
           usageFormat: "ef files <paths...> <output-path>");
}