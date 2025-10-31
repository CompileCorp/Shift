using Compile.Shift.Ef;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli.Commands;

public record EfFromSqlCommand(
    string ConnectionString,
    string Schema,
    string OutputDirectoryPath) : IRequest<Unit>;

public class EfFromSqlCommandHandler : IRequestHandler<EfFromSqlCommand, Unit>
{
    private readonly IShift _shift;
    private readonly IEfCodeGenerator _efCodeGenerator;
    private readonly ILogger<EfFromSqlCommandHandler> _logger;

    public EfFromSqlCommandHandler(
        IShift shift,
        IEfCodeGenerator efCodeGenerator,
        ILogger<EfFromSqlCommandHandler> logger)
    {
        _shift = shift;
        _efCodeGenerator = efCodeGenerator;
        _logger = logger;
    }

    public async Task<Unit> Handle(EfFromSqlCommand request, CancellationToken cancellationToken)
    {
        var model = await _shift.LoadFromSqlAsync(request.ConnectionString, request.Schema);

        await _efCodeGenerator.GenerateEfCodeAsync(model, outputPath: request.OutputDirectoryPath);

        return Unit.Value;
    }
}