using Compile.VnumEnumeration;

namespace Compile.Shift.Vnums;

public enum CliCmdAliasId // Not really needed, but keeps things consistent
{
    Alias_Ef = 1,
    Alias_EfGenerate,
    Alias_GenerateEf,
}

public class CliCmdAlias : Vnum<CliCmdAliasId>
{
    public CliCmd CliCmdType { get; } = null!;

    private CliCmdAlias(
        CliCmdAliasId id,
        string code,
        CliCmd mapTo
    ) : base(id, code)
    {
        CliCmdType = mapTo;
    }

    public static readonly CliCmdAlias Alias_Ef = new(CliCmdAliasId.Alias_Ef, "ef", mapTo: CliCmd.EfGenerate);
    public static readonly CliCmdAlias Alias_EfGenerate = new(CliCmdAliasId.Alias_EfGenerate, "ef-generate", mapTo: CliCmd.EfGenerate);
    public static readonly CliCmdAlias Alias_GenerateEf = new(CliCmdAliasId.Alias_GenerateEf, "generate-ef", mapTo: CliCmd.EfGenerate);
}