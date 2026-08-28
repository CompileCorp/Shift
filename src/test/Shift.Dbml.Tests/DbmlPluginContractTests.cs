using Compile.Shift.Plugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace Compile.Shift.Dbml.Tests;

/// <summary>
/// Ties the plugin's declared attribute list to the attribute names the exporter actually reads.
///
/// <see cref="DbmlErdAttributes"/> is the only place in the repository that knows the erd-*
/// vocabulary and the exporter reads attributes exclusively through those constants, so requiring
/// the two sets to match means a new erd-* behaviour cannot be added without also declaring it for
/// `shift attributes` to list.
/// </summary>
public class DbmlPluginContractTests
{
    private readonly DbmlExporter _sut = new() { Logger = NullLogger.Instance };

    private static IReadOnlyList<string> ErdAttributeConstants()
    {
        return typeof(DbmlErdAttributes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
    }

    [Fact]
    public void SupportedAttributes_CoverEveryAttributeNameTheExporterKnows()
    {
        var declared = _sut.SupportedAttributes.Select(x => x.Name);

        declared.Should().BeEquivalentTo(ErdAttributeConstants());
    }

    [Fact]
    public void SupportedAttributes_EachCarryAScopeKindAndDescription()
    {
        _sut.SupportedAttributes.Should().NotBeEmpty();

        foreach (var attribute in _sut.SupportedAttributes)
        {
            attribute.Name.Should().NotBeNullOrWhiteSpace();
            attribute.Description.Should().NotBeNullOrWhiteSpace();
            attribute.Scope.Should().BeOneOf(AttributeScope.Model, AttributeScope.Field, AttributeScope.Both);
        }
    }

    [Fact]
    public void SupportedAttributes_DeclareTheAgreedScopeAndKind()
    {
        _sut.SupportedAttributes.Should().ContainEquivalentOf(
            new PluginAttributeDefinition(DbmlErdAttributes.Hide, AttributeScope.Both, true, "*"),
            options => options.Excluding(x => x.Description));

        _sut.SupportedAttributes.Single(x => x.Name == DbmlErdAttributes.Group)
            .Should().Match<PluginAttributeDefinition>(x => x.Scope == AttributeScope.Model && !x.IsFlag);

        _sut.SupportedAttributes.Single(x => x.Name == DbmlErdAttributes.Note)
            .Should().Match<PluginAttributeDefinition>(x => x.Scope == AttributeScope.Both && !x.IsFlag);

        _sut.SupportedAttributes.Single(x => x.Name == DbmlErdAttributes.Color)
            .Should().Match<PluginAttributeDefinition>(x => x.Scope == AttributeScope.Model && !x.IsFlag);
    }

    [Fact]
    public void Plugin_ExposesItsNameAndDescription()
    {
        _sut.Name.Should().Be("dbml");
        _sut.Description.Should().NotBeNullOrWhiteSpace();
    }
}