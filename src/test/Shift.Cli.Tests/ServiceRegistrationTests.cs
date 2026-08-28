using Compile.Shift.Cli;
using Compile.Shift.Cli.Commands;
using Compile.Shift.Dbml;
using Compile.Shift.Ef;
using Compile.Shift.Plugins;
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
    [InlineData(typeof(IRequestHandler<DbmlCommand, Unit>))]
    [InlineData(typeof(IRequestHandler<AttributesCommand, Unit>))]
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
    public void DbmlExporter_IsRegisteredAndImplementsInterface()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var exporter = scope.ServiceProvider.GetService<IDbmlExporter>();

        exporter.Should().NotBeNull();
        exporter.Should().BeOfType<DbmlExporter>();
    }

    /// <summary>
    /// `shift attributes` resolves IEnumerable&lt;IShiftPlugin&gt;, so every plugin must be visible
    /// through the contract as well as through its own interface.
    /// </summary>
    [Fact]
    public void EveryPlugin_IsDiscoverableThroughThePluginContract()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var plugins = scope.ServiceProvider.GetServices<IShiftPlugin>().ToList();

        plugins.Select(x => x.Name).Should().BeEquivalentTo(["dbml", "ef"]);
        plugins.Should().AllSatisfy(plugin => plugin.Description.Should().NotBeNullOrWhiteSpace());
    }

    /// <summary>
    /// The plugin registrations forward to the same scoped instances rather than creating second
    /// copies of each generator.
    /// </summary>
    [Fact]
    public void PluginRegistrations_ResolveToTheSameInstancesAsTheirOwnInterfaces()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var plugins = scope.ServiceProvider.GetServices<IShiftPlugin>().ToList();

        plugins.Should().Contain(scope.ServiceProvider.GetRequiredService<IEfCodeGenerator>());
        plugins.Should().Contain(scope.ServiceProvider.GetRequiredService<IDbmlExporter>());
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