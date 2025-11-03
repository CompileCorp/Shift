using Compile.Shift.Vnums;
using MediatR;

namespace Compile.Shift.Cli.Commands;

public record PrintHelpCommand(
    List<string>? Messages = null
) : IRequest<Unit>;

public class PrintHelpCommandHandler : IRequestHandler<PrintHelpCommand, Unit>
{
    public Task<Unit> Handle(PrintHelpCommand request, CancellationToken cancellationToken)
    {
        if (request.Messages?.Any() == true)
        {
            request.Messages.ForEach(m => Console.WriteLine(m));
            Console.WriteLine();
        }

        // Dynamically generate help 
        CliCmd.PrintHelp();

        Console.WriteLine();

        CliSubCmd.PrintHelp();

        return Task.FromResult(Unit.Value);
    }
}