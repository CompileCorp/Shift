using Compile.Shift.Plugins;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli.Commands;

/// <summary>
/// Lists the plugin attributes every registered plugin understands, optionally filtered to one
/// plugin. This is the authored-facing counterpart to <see cref="IShiftPlugin.SupportedAttributes"/>:
/// attribute names can be discovered from the CLI instead of by reading plugin source.
/// </summary>
public record AttributesCommand(string? PluginName = null) : IRequest<Unit>;

public class AttributesCommandHandler : IRequestHandler<AttributesCommand, Unit>
{
    private readonly IEnumerable<IShiftPlugin> _plugins;
    private readonly ILogger<AttributesCommandHandler> _logger;

    public AttributesCommandHandler(
        IEnumerable<IShiftPlugin> plugins,
        ILogger<AttributesCommandHandler> logger)
    {
        _plugins = plugins;
        _logger = logger;
    }

    public Task<Unit> Handle(AttributesCommand request, CancellationToken cancellationToken)
    {
        var plugins = _plugins
            .Where(plugin => request.PluginName == null ||
                             string.Equals(plugin.Name, request.PluginName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (plugins.Count == 0)
        {
            _logger.LogWarning("No plugin named '{PluginName}' is registered", request.PluginName);
            return Task.FromResult(Unit.Value);
        }

        foreach (var plugin in plugins)
        {
            _logger.LogInformation("{PluginName} - {PluginDescription}", plugin.Name, plugin.Description);

            if (plugin.SupportedAttributes.Count == 0)
            {
                _logger.LogInformation("  (no plugin attributes)");
                continue;
            }

            // Grouped by namespace so the structure of the vocabulary is visible in the listing: a
            // plugin normally declares one namespace, but grouping is on the attribute's own
            // namespace rather than the plugin's claim, so an un-namespaced declaration still lists.
            var namespaces = plugin.SupportedAttributes
                .GroupBy(attribute => attribute.Namespace, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            foreach (var attributeNamespace in namespaces)
            {
                _logger.LogInformation(
                    "  namespace: {AttributeNamespace}",
                    attributeNamespace.Key ?? "(none)");

                foreach (var attribute in attributeNamespace.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // The full spelling, so the line can be copied straight into a .dmd file.
                    _logger.LogInformation(
                        "    @{AttributeName} scope={Scope} kind={Kind} - {AttributeDescription}",
                        attribute.Name,
                        DescribeScope(attribute.Scope),
                        attribute.IsFlag ? "flag" : "valued",
                        attribute.Description);
                }
            }
        }

        return Task.FromResult(Unit.Value);
    }

    private static string DescribeScope(AttributeScope scope)
    {
        return scope switch
        {
            AttributeScope.Model => "model",
            AttributeScope.Field => "field",
            _ => "both"
        };
    }
}