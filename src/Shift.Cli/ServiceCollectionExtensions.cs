using Compile.Shift.Dbml;
using Compile.Shift.Ef;
using Compile.Shift.Plugins;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli;

/// <summary>
/// Registers the services required by the Shift CLI. Kept separate from <c>Program</c> so the
/// dependency graph (every MediatR command handler and its dependencies) can be resolved and
/// verified in tests.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShiftCli(this IServiceCollection services)
    {
        // MediatR command handlers live in this assembly.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        // Core Shift engine. The Logger is a required init-only property, so it is supplied here.
        services.AddScoped<IShift, Shift>(sp =>
            new Shift { Logger = sp.GetRequiredService<ILogger<Shift>>() });

        // Entity Framework code generator used by the `ef ...` commands.
        services.AddScoped<IEfCodeGenerator>(sp =>
            new EfCodeGenerator { Logger = sp.GetRequiredService<ILogger<EfCodeGenerator>>() });

        // DBML exporter used by the `dbml ...` command.
        services.AddScoped<IDbmlExporter>(sp =>
            new DbmlExporter { Logger = sp.GetRequiredService<ILogger<DbmlExporter>>() });

        // Both generators are also plugins, so `shift attributes` can enumerate them. They resolve
        // to the same scoped instances registered above rather than to second copies.
        services.AddScoped<IShiftPlugin>(sp => sp.GetRequiredService<IEfCodeGenerator>());
        services.AddScoped<IShiftPlugin>(sp => sp.GetRequiredService<IDbmlExporter>());

        services.AddTransient<ModelExporter>();

        return services;
    }
}