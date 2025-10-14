using Compile.Shift.Ef;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli.Commands;

public record EfFromFilesCommand(
    IEnumerable<string> DmdLocationPaths,
    string OutputDirectoryPath) : IRequest<Unit>;

public class EfFromFilesCommandHandler : IRequestHandler<EfFromFilesCommand, Unit>
{
    private readonly IShift _shift;
    private readonly IEfCodeGenerator _efCodeGenerator;
    private readonly ILogger<EfFromFilesCommandHandler> _logger;

    public EfFromFilesCommandHandler(
        IShift shift,
        IEfCodeGenerator efCodeGenerator,
        ILogger<EfFromFilesCommandHandler> logger)
    {
        _shift = shift;
        _efCodeGenerator = efCodeGenerator;
        _logger = logger;
    }

    public async Task<Unit> Handle(EfFromFilesCommand request, CancellationToken cancellationToken)
    {
        var model = await _shift.LoadFromPathAsync(request.DmdLocationPaths);

        await _efCodeGenerator.GenerateEfCodeAsync(model, outputPath: request.OutputDirectoryPath);

        return Unit.Value;
    }
}
