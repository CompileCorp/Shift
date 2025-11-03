using Compile.Shift.Vnums;
using Compile.VnumEnumeration;

namespace Compile.Shift.Cli;

/// <summary>
/// Encapsulates and parses command-line arguments from the user.
/// Responsible for initial parsing of CLI commands and sub-commands.
/// </summary>
internal class UserInput
{
    /// <summary>
    /// The parsed CLI command from the first argument.
    /// </summary>
    public CliCmd Command { get; } = CliCmd.Help;

    /// <summary>
    /// The parsed EF sub-command, if the command is <see cref="CliCmdId.EfGenerate"/>.
    /// Otherwise, null.
    /// </summary>
    public CliSubCmd? SubCommand { get; }

    /// <summary>
    /// The remaining arguments after parsing the command and sub-command (if applicable).
    /// </summary>
    public string[] RemainingArgs { get; } = [];

    /// <summary>
    /// Initializes a new instance of <see cref="UserInput"/> by parsing the provided arguments.
    /// </summary>
    /// <param name="args">The command-line arguments to parse.</param>
    public UserInput(string[]? args)
    {
        if (args == null || args.Length == 0)
            return;

        // Parse the main command from the first argument
        Command = ParseCommand(args[0]);

        // If it's an EF command, parse the sub-command from the second argument
        if (Command.Id == CliCmdId.EfGenerate && args.Length > 1)
        {
            SubCommand = ParseEfSubCommand(args[1]);
            RemainingArgs = args[2..]; // Skip both command and sub-command
        }
        else
        {
            SubCommand = null;
            RemainingArgs = args[1..]; // Skip only the command
        }
    }

    private static CliCmd ParseCommand(string firstArg)
    {
        if (Vnum.TryFromCode<CliCmd>(firstArg, ignoreCase: true, out var parsedCmd))
            return parsedCmd;

        // Fallback to checking aliases
        if (Vnum.TryFromCode<CliCmdAlias>(firstArg, ignoreCase: true, out var parsedAlias))
            return parsedAlias.CliCmdType;

        // Default to Help if parsing fails
        return CliCmd.Help;
    }

    private static CliSubCmd ParseEfSubCommand(string secondArg)
    {
        if (Vnum.TryFromCode<CliSubCmd>(secondArg, ignoreCase: true, out var parsedSubCmd))
            return parsedSubCmd;

        return CliSubCmd.Help;
    }
}