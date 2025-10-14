using Compile.Shift.Ef;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli.Commands;

public record EfFromSqlCustomCommand(
    string ConnectionString,
    string Schema,
    string OutputDirectoryPath,
    EfCodeGenerationOptions Options) : IRequest<Unit>;

public class EfFromSqlCustomCommandHandler : IRequestHandler<EfFromSqlCustomCommand, Unit>
{
    private readonly IShift _shift;
    private readonly IEfCodeGenerator _efCodeGenerator;
    private readonly ILogger<EfFromSqlCustomCommandHandler> _logger;

    public EfFromSqlCustomCommandHandler(
        IShift shift,
        IEfCodeGenerator efCodeGenerator,
        ILogger<EfFromSqlCustomCommandHandler> logger)
    {
        _shift = shift;
        _efCodeGenerator = efCodeGenerator;
        _logger = logger;
    }

    public async Task<Unit> Handle(EfFromSqlCustomCommand request, CancellationToken cancellationToken)
    {
        var model = await _shift.LoadFromSqlAsync(request.ConnectionString, request.Schema);

        await _efCodeGenerator.GenerateEfCodeAsync(
            model,
            outputPath: request.OutputDirectoryPath,
            options: request.Options);

        return Unit.Value;
    }
}
