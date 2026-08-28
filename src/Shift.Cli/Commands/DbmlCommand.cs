using Compile.Shift.Dbml;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli.Commands;

public record DbmlCommand(
    IEnumerable<string> DmdLocationPaths,
    string OutputPath) : IRequest<Unit>;

public class DbmlCommandHandler : IRequestHandler<DbmlCommand, Unit>
{
    private readonly IShift _shift;
    private readonly IDbmlExporter _dbmlExporter;
    private readonly ILogger<DbmlCommandHandler> _logger;

    public DbmlCommandHandler(
        IShift shift,
        IDbmlExporter dbmlExporter,
        ILogger<DbmlCommandHandler> logger)
    {
        _shift = shift;
        _dbmlExporter = dbmlExporter;
        _logger = logger;
    }

    public async Task<Unit> Handle(DbmlCommand request, CancellationToken cancellationToken)
    {
        var model = await _shift.LoadFromPathAsync(request.DmdLocationPaths);

        var filePath = await _dbmlExporter.ExportAsync(model, request.OutputPath);

        _logger.LogInformation("DBML diagram written to {Path}", filePath);

        return Unit.Value;
    }
}