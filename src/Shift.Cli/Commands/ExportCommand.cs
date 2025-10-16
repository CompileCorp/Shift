using MediatR;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli.Commands;

public record ExportCommand(string ConnectionString, string Schema, string OutputDirectoryPath) : IRequest<Unit>;

public class ExportCommandHandler : IRequestHandler<ExportCommand, Unit>
{
    private readonly IShift _shift;
    private readonly ILogger<ExportCommandHandler> _logger;

    public ExportCommandHandler(IShift shift, ILogger<ExportCommandHandler> logger)
    {
        _shift = shift;
        _logger = logger;
    }

    public async Task<Unit> Handle(ExportCommand request, CancellationToken cancellationToken)
    {
        var model = await _shift.LoadFromSqlAsync(request.ConnectionString, request.Schema);

        new ModelExporter().ExportToDmd(model, request.OutputDirectoryPath);

        _logger.LogInformation("Database exported to {Path}", request.OutputDirectoryPath);
        return Unit.Value;
    }
}
