using Compile.VnumEnumeration;

namespace Compile.Shift.Vnums;

public enum CliEfSubCmdId // Not really needed, but keeps things consistent
{
    Help = 1,
    Sql,
    SqlCustom,
    Files,
}

public class CliEfSubCmd : Vnum<CliEfSubCmdId>
{
    private string Description { get; } = null!;
    private string Example { get; } = null!;
    private string UsageFormat { get; } = null!;
    private List<string>? ParamsUsageFormat { get; }

    private CliEfSubCmd(
        CliEfSubCmdId id,
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
        var allSubCommands = GetAll<CliEfSubCmd>(c => c != Help).ToList();

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


    public static readonly CliEfSubCmd Help =
        new(id: CliEfSubCmdId.Help,
            code: "help",
            description: "Print help",
            example: "",
            usageFormat: "ef help");

    public static readonly CliEfSubCmd Sql =
        new(id: CliEfSubCmdId.Sql,
            code: "sql",
            description: "Generate EF code from SQL Server",
            example: "shift ef sql \"Server=.;Database=MyDb;\" ./Generated",
            usageFormat: "ef sql <connection-string> <output-path>");

    public static readonly CliEfSubCmd SqlCustom =
        new(id: CliEfSubCmdId.SqlCustom,
            code: "sql-custom",
            description: "Generate with custom options",
            example: "shift ef sql-custom \"Server=.;Database=MyDb;\" ./Generated \r\n    --namespace MyApp.Data --context MyDbContext --interface IMyDbContext",
            usageFormat: "ef sql-custom <connection-string> <output-path>",
            paramsUsage: ["[--namespace <name>] [--context <name>]", "[--interface <name>] [--base-class <name>]"]);

    public static readonly CliEfSubCmd Files =
       new(id: CliEfSubCmdId.Files,
           code: "files",
           description: "Generate EF code from model files",
           example: "shift ef files ./Models/User.yaml ./Models/Order.yaml ./Generated",
           usageFormat: "ef files <paths...> <output-path>");
}