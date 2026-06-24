using Compile.Shift.Cli;
using Compile.Shift.Cli.Commands;
using Compile.Shift.Ef;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;

namespace Compile.Shift.Cli.Tests;

/// <summary>
/// Verifies the CLI dependency graph is fully wired. These tests construct the real service
/// provider (the same registrations used by Program) and resolve every MediatR command handler.
///
/// This guards against a class of bug where a command handler depends on a service that is
/// declared as an interface but never registered (e.g. IEfCodeGenerator) — the handler then
/// throws only at runtime when the command is invoked, which mocked handler tests never exercise.
/// </summary>
public class ServiceRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShiftCli();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    [Theory]
    [InlineData(typeof(IRequestHandler<ApplyCommand, Unit>))]
    [InlineData(typeof(IRequestHandler<ApplyAssembliesCommand, Unit>))]
    [InlineData(typeof(IRequestHandler<ExportCommand, Unit>))]
    [InlineData(typeof(IRequestHandler<PrintHelpCommand, Unit>))]
    [InlineData(typeof(IRequestHandler<EfFromSqlCommand, Unit>))]
    [InlineData(typeof(IRequestHandler<EfFromFilesCommand, Unit>))]
    [InlineData(typeof(IRequestHandler<EfFromSqlCustomCommand, Unit>))]
    public void AllCommandHandlers_CanBeResolved(Type handlerType)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetService(handlerType);

        handler.Should().NotBeNull($"the handler {handlerType.Name} and all of its dependencies must be registered");
    }

    [Fact]
    public void EfCodeGenerator_IsRegisteredAndImplementsInterface()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var generator = scope.ServiceProvider.GetService<IEfCodeGenerator>();

        generator.Should().NotBeNull();
        generator.Should().BeOfType<EfCodeGenerator>();
    }

    [Fact]
    public void CoreServices_AreRegistered()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<IShift>().Should().NotBeNull();
        scope.ServiceProvider.GetService<ModelExporter>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IMediator>().Should().NotBeNull();
    }
}