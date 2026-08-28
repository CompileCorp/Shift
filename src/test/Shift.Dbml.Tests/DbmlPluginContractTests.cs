using Compile.Shift.Plugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace Compile.Shift.Dbml.Tests;

/// <summary>
/// Ties the plugin's declared attribute list to the attribute names the exporter actually reads.
///
/// <see cref="DbmlErdAttributes"/> is the only place in the repository that knows the erd:*
/// vocabulary and the exporter reads attributes exclusively through those constants, so requiring
/// the two sets to match means a new erd:* behaviour cannot be added without also declaring it for
/// `shift attributes` to list.
/// </summary>
public class DbmlPluginContractTests
{
    private readonly DbmlExporter _sut = new() { Logger = NullLogger.Instance };

    /// <summary>
    /// Every attribute-name constant on <see cref="DbmlErdAttributes"/>. The namespace constant is
    /// excluded by name: it is the namespace the other constants are built from, not an attribute.
    /// </summary>
    private static IReadOnlyList<string> ErdAttributeConstants()
    {
        return typeof(DbmlErdAttributes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Where(field => field.Name != nameof(DbmlErdAttributes.Namespace))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
    }

    [Fact]
    public void SupportedAttributes_CoverEveryAttributeNameTheExporterKnows()
    {
        var declared = _sut.SupportedAttributes.Select(x => x.LocalName);

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
            new PluginAttributeDefinition(
                DbmlErdAttributes.Namespace, DbmlErdAttributes.Hide, AttributeScope.Both, true, "*"),
            options => options.Excluding(x => x.Description));

        _sut.SupportedAttributes.Single(x => x.LocalName == DbmlErdAttributes.Group)
            .Should().Match<PluginAttributeDefinition>(x => x.Scope == AttributeScope.Model && !x.IsFlag);

        _sut.SupportedAttributes.Single(x => x.LocalName == DbmlErdAttributes.Note)
            .Should().Match<PluginAttributeDefinition>(x => x.Scope == AttributeScope.Both && !x.IsFlag);

        _sut.SupportedAttributes.Single(x => x.LocalName == DbmlErdAttributes.Color)
            .Should().Match<PluginAttributeDefinition>(x => x.Scope == AttributeScope.Model && !x.IsFlag);
    }

    [Fact]
    public void Plugin_ExposesItsNameAndDescription()
    {
        _sut.Name.Should().Be("dbml");
        _sut.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Plugin_ClaimsTheErdNamespace()
    {
        _sut.AttributeNamespace.Should().Be("erd");
    }

    /// <summary>
    /// Every declared attribute sits in the namespace the plugin claims, so a constant cannot drift
    /// into a namespace `shift attributes` would file under a different heading.
    /// </summary>
    [Fact]
    public void SupportedAttributes_AllSitInTheClaimedNamespace()
    {
        foreach (var attribute in _sut.SupportedAttributes)
        {
            attribute.Namespace.Should().Be(_sut.AttributeNamespace);
            attribute.LocalName.Should().NotBeNullOrWhiteSpace();
            attribute.Name.Should().Be($"{attribute.Namespace}:{attribute.LocalName}");
        }
    }

    /// <summary>
    /// The constants are local names, so the namespace is applied in exactly one place. A constant
    /// that reintroduced the prefix would double it to <c>erd:erd:hide</c> once the registration
    /// applies the namespace, so it is rejected here.
    /// </summary>
    [Fact]
    public void ErdAttributeConstants_AreLocalNamesWithNoPrefix()
    {
        ErdAttributeConstants().Should().OnlyContain(name => !name.Contains(':'));
    }

    /// <summary>
    /// The authored spelling the CLI prints is still the namespaced one, composed from the two halves.
    /// </summary>
    [Fact]
    public void SupportedAttributes_ComposeTheAuthoredSpelling()
    {
        _sut.SupportedAttributes.Select(x => x.Name)
            .Should().Contain("erd:hide").And.Contain("erd:group");
    }
}