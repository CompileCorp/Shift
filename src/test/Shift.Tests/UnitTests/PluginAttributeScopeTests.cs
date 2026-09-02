using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
using Compile.Shift.Plugins;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Tests for the namespace-scoped attribute view a plugin is handed.
///
/// Intent: a plugin declares its namespace once and never matches a prefix itself. The scoped view
/// is a projection — it filters to one namespace and strips it — so the attributes on the model keep
/// their full names and round-tripping is unaffected, while the plugin sees bare local names and the
/// existing flag-vs-valued and last-wins readings behave identically through it.
/// </summary>
public class PluginAttributeScopeTests
{
    private static List<AttributeModel> Attributes(params (string Name, string? Value)[] attributes)
    {
        return attributes.Select(x => new AttributeModel(x.Name, x.Value)).ToList();
    }

    [Fact]
    public void InNamespace_StripsTheNamespaceFromEveryName()
    {
        var attributes = Attributes(("erd:hide", null), ("erd:group", "Billing"));

        var scoped = attributes.InNamespace("erd");

        scoped.Select(x => x.Name).Should().Equal("hide", "group");
    }

    [Fact]
    public void InNamespace_ExcludesOtherNamespaces()
    {
        var attributes = Attributes(("erd:group", "Billing"), ("audit:owner", "Platform"));

        var scoped = attributes.InNamespace("erd");

        scoped.Should().ContainSingle().Which.Name.Should().Be("group");
    }

    /// <summary>
    /// The projection must not mutate: the model still carries the authored spelling, which is what
    /// makes the DMD export round trip byte-exact.
    /// </summary>
    [Fact]
    public void InNamespace_LeavesTheUnderlyingNamesIntact()
    {
        var attributes = Attributes(("erd:group", "Billing"));

        attributes.InNamespace("erd");

        attributes.Single().Name.Should().Be("erd:group");
    }

    /// <summary>
    /// Passing null selects the un-namespaced attributes, which is what a plugin claiming no
    /// namespace is handed — @NoIdentity, not everything.
    /// </summary>
    [Fact]
    public void InNamespace_Null_SelectsOnlyUnNamespacedAttributes()
    {
        var attributes = Attributes(("NoIdentity", null), ("erd:hide", null));

        var scoped = attributes.InNamespace(null);

        scoped.Should().ContainSingle().Which.Name.Should().Be("NoIdentity");
    }

    [Fact]
    public void InNamespace_IsCaseInsensitiveOnTheNamespace()
    {
        var attributes = Attributes(("ERD:hide", null));

        attributes.InNamespace("erd").Should().ContainSingle();
    }

    [Fact]
    public void InNamespace_PreservesTheFlagAndValuedDistinction()
    {
        var attributes = Attributes(("erd:hide", null), ("erd:note", "text"));

        var scoped = attributes.InNamespace("erd");

        scoped.Single(x => x.Name == "hide").IsFlag.Should().BeTrue();
        scoped.Single(x => x.Name == "note").IsFlag.Should().BeFalse();
    }

    /// <summary>
    /// Order is preserved through the projection, so composing it with AttributeValue keeps the same
    /// last-wins reading of a duplicate that the unfiltered list gives.
    /// </summary>
    [Fact]
    public void InNamespace_KeepsLastWinsForADuplicate()
    {
        var attributes = Attributes(("erd:group", "First"), ("erd:group", "Second"));

        attributes.InNamespace("erd").AttributeValue("group").Should().Be("Second");
    }

    /// <summary>
    /// The unfiltered helpers still work on full names, for anything that wants the whole list.
    /// </summary>
    [Fact]
    public void HasAttributeAndAttributeValue_StillMatchFullNames()
    {
        var attributes = Attributes(("erd:hide", null), ("erd:group", "Billing"));

        attributes.HasAttribute("erd:hide").Should().BeTrue();
        attributes.AttributeValue("erd:group").Should().Be("Billing");
    }

    /// <summary>
    /// A declaration composes its authored spelling from the two halves it carries, so the CLI can
    /// print a line that can be copied into a .dmd file.
    /// </summary>
    [Fact]
    public void PluginAttributeDefinition_Namespaced_ComposesTheAuthoredName()
    {
        var definition = new PluginAttributeDefinition("erd", "hide", AttributeScope.Both, true, "Hides it");

        definition.Name.Should().Be("erd:hide");
    }

    /// <summary>
    /// An un-namespaced declaration is just its local name — no stray leading colon.
    /// </summary>
    [Fact]
    public void PluginAttributeDefinition_UnNamespaced_IsJustTheLocalName()
    {
        var definition = new PluginAttributeDefinition(null, "NoIdentity", AttributeScope.Model, true, "A flag");

        definition.Name.Should().Be("NoIdentity");
    }

    /// <summary>
    /// An attribute in a namespace no plugin claims is preserved on the model but delivered to
    /// nobody: it is absent from every claimed namespace's view.
    /// </summary>
    [Fact]
    public void InNamespace_UnclaimedNamespace_IsDeliveredToNoOne()
    {
        var attributes = Attributes(("audit:owner", "Platform"));

        attributes.InNamespace("erd").Should().BeEmpty();
        attributes.InNamespace(null).Should().BeEmpty();
        attributes.Single().Name.Should().Be("audit:owner");
    }
}