using Compile.Shift.Vnums;
using MediatR;

namespace Compile.Shift.Cli.Commands;

public record PrintHelpCommand() : IRequest<Unit>;

public class PrintHelpCommandHandler : IRequestHandler<PrintHelpCommand, Unit>
{
    public Task<Unit> Handle(PrintHelpCommand request, CancellationToken cancellationToken)
    {
        // Dynamically generate help 
        CliCmd.PrintHelp();

        Console.WriteLine();

        CliEfSubCmd.PrintHelp();

        return Task.FromResult(Unit.Value);
    }
}