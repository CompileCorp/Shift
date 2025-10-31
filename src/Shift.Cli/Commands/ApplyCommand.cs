using MediatR;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli.Commands;

public record ApplyCommand(string ConnectionString, string[] ModelLocationPaths) : IRequest<Unit>;

public class ApplyCommandHandler : IRequestHandler<ApplyCommand, Unit>
{
    private readonly IShift _shift;
    private readonly ILogger<ApplyCommandHandler> _logger;

    public ApplyCommandHandler(IShift shift, ILogger<ApplyCommandHandler> logger)
    {
        _shift = shift;
        _logger = logger;
    }

    public async Task<Unit> Handle(ApplyCommand request, CancellationToken cancellationToken)
    {
        var targetModel = await _shift.LoadFromPathAsync(request.ModelLocationPaths);
        await _shift.ApplyToSqlAsync(targetModel, request.ConnectionString);
        return Unit.Value;
    }
}