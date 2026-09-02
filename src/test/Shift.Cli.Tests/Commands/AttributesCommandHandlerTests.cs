using Compile.Shift.Cli.Commands;
using Compile.Shift.Dbml;
using Compile.Shift.Ef;
using Compile.Shift.Plugins;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for AttributesCommandHandler, the `shift attributes` command.
///
/// Intent: every registered plugin appears in the output with each attribute it declares, its scope
/// and whether it is a flag, so an author can discover attribute names without reading source.
/// </summary>
public class AttributesCommandHandlerTests
{
    /// <summary>
    /// Captures formatted log messages so the rendered output can be asserted on directly.
    /// </summary>
    private sealed class RecordingLogger : ILogger<AttributesCommandHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private static readonly IShiftPlugin DbmlPlugin = new DbmlExporter { Logger = NullLogger.Instance };
    private static readonly IShiftPlugin EfPlugin = new EfCodeGenerator { Logger = NullLogger.Instance };

    private static (AttributesCommandHandler Handler, RecordingLogger Logger) Build(params IShiftPlugin[] plugins)
    {
        var logger = new RecordingLogger();
        return (new AttributesCommandHandler(plugins, logger), logger);
    }

    [Fact]
    public async Task Handle_ListsEveryRegisteredPlugin()
    {
        var (handler, logger) = Build(DbmlPlugin, EfPlugin);

        var result = await handler.Handle(new AttributesCommand(), CancellationToken.None);

        result.Should().Be(Unit.Value);
        logger.Messages.Should().Contain(m => m.StartsWith("dbml - "));
        logger.Messages.Should().Contain(m => m.StartsWith("ef - "));
    }

    [Fact]
    public async Task Handle_ListsEveryAttributeWithItsScopeAndKind()
    {
        var (handler, logger) = Build(DbmlPlugin);

        await handler.Handle(new AttributesCommand(), CancellationToken.None);

        var output = string.Join("\n", logger.Messages);

        foreach (var attribute in DbmlPlugin.SupportedAttributes)
        {
            output.Should().Contain($"@{attribute.Name}");
            output.Should().Contain(attribute.Description);
        }

        output.Should().Contain("@erd:hide scope=both kind=flag");
        output.Should().Contain("@erd:group scope=model kind=valued");
    }

    [Fact]
    public async Task Handle_PluginWithNoAttributes_SaysSo()
    {
        var (handler, logger) = Build(EfPlugin);

        await handler.Handle(new AttributesCommand(), CancellationToken.None);

        logger.Messages.Should().Contain("  (no plugin attributes)");
    }

    [Fact]
    public async Task Handle_WithAPluginName_FiltersToThatPlugin()
    {
        var (handler, logger) = Build(DbmlPlugin, EfPlugin);

        await handler.Handle(new AttributesCommand("DBML"), CancellationToken.None);

        logger.Messages.Should().Contain(m => m.StartsWith("dbml - "));
        logger.Messages.Should().NotContain(m => m.StartsWith("ef - "));
    }

    [Fact]
    public async Task Handle_WithAnUnknownPluginName_WarnsAndListsNothing()
    {
        var (handler, logger) = Build(DbmlPlugin, EfPlugin);

        var result = await handler.Handle(new AttributesCommand("nope"), CancellationToken.None);

        result.Should().Be(Unit.Value);
        logger.Messages.Should().ContainSingle().Which.Should().Contain("nope");
    }

    /// <summary>
    /// The scope of a Field-only attribute has no declaration today, so the rendering is exercised
    /// directly to keep every branch honest if one is added.
    /// </summary>
    [Fact]
    public async Task Handle_FieldScopedAttribute_RendersAsField()
    {
        var plugin = new StubPlugin("stub", [new PluginAttributeDefinition(null, "only-field", AttributeScope.Field, false, "Field only")]);
        var (handler, logger) = Build(plugin);

        await handler.Handle(new AttributesCommand(), CancellationToken.None);

        string.Join("\n", logger.Messages).Should().Contain("@only-field scope=field kind=valued");
    }

    private sealed record StubPlugin(
        string Name,
        IReadOnlyList<PluginAttributeDefinition> SupportedAttributes,
        string? AttributeNamespace = null)
        : IShiftPlugin
    {
        public string Description => "A stub plugin";
    }
}