using MediatR;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Compile.Shift.Cli.Commands;

public record ApplyAssembliesCommand(string ConnectionString, string[] DllPaths) : IRequest<Unit>;

public class ApplyAssembliesCommandHandler : IRequestHandler<ApplyAssembliesCommand, Unit>
{
    private readonly IShift _shift;
    private readonly ILogger<ApplyAssembliesCommandHandler> _logger;

    public ApplyAssembliesCommandHandler(IShift shift, ILogger<ApplyAssembliesCommandHandler> logger)
    {
        _shift = shift;
        _logger = logger;
    }

    public async Task<Unit> Handle(ApplyAssembliesCommand request, CancellationToken cancellationToken)
    {
        var assemblies = new List<Assembly>();
        foreach (var dllPath in request.DllPaths)
        {
            var fullPath = Path.GetFullPath(dllPath);
            if (!File.Exists(fullPath))
            {
                _logger.LogError("Assembly file does not exist: {DllPath}", fullPath);
                throw new FileNotFoundException($"Assembly file not found: {fullPath}");
            }

            var assembly = Assembly.LoadFrom(fullPath);
            assemblies.Add(assembly);
            _logger.LogInformation("Loaded assembly: {AssemblyName} from {DllPath}", assembly.GetName().Name, fullPath);
        }

        var targetModel = await _shift.LoadFromAssembliesAsync(assemblies);
        await _shift.ApplyToSqlAsync(targetModel, request.ConnectionString);
        return Unit.Value;
    }
}