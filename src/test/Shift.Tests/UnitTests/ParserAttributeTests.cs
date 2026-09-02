using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Tests for plugin attributes in DMD and DMDX files.
///
/// Intent: Shift parses and preserves attributes exactly as written — order, duplicates and the
/// difference between a flag and a value — and interprets none of them except @NoIdentity. Their
/// meaning belongs to whichever plugin consumes them.
/// </summary>
public class ParserAttributeTests
{
    private readonly Parser _sut = new();

    private static TableModel Parse(string content)
    {
        var model = new DatabaseModel();
        new Parser().ParseTable(model, content);
        return model.Tables.Values.Single();
    }

    // ---- model level ---------------------------------------------------------

    [Fact]
    public void ParseTable_FlagAttribute_HasNoValue()
    {
        var table = Parse("model User {\n  ustring(50) Name\n  @erd:hide\n}");

        var attribute = table.Attributes.Should().ContainSingle().Subject;
        attribute.Name.Should().Be("erd:hide");
        attribute.Value.Should().BeNull();
        attribute.IsFlag.Should().BeTrue();
    }

    [Fact]
    public void ParseTable_ValuedAttribute_KeepsTheBareToken()
    {
        var table = Parse("model User {\n  @erd:group Billing\n}");

        var attribute = table.Attributes.Should().ContainSingle().Subject;
        attribute.Name.Should().Be("erd:group");
        attribute.Value.Should().Be("Billing");
        attribute.IsFlag.Should().BeFalse();
    }

    [Fact]
    public void ParseTable_QuotedAttributeValue_KeepsTheSpaces()
    {
        var table = Parse("model User {\n  @erd:group 'Billing Ops'\n}");

        table.Attributes.Single().Value.Should().Be("Billing Ops");
    }

    [Fact]
    public void ParseTable_MultipleAttributes_ArePreservedInDeclarationOrder()
    {
        var table = Parse("model User {\n  @erd:hide\n  @erd:group Billing\n  @erd:color 3498DB\n}");

        table.Attributes.Select(x => x.Name).Should().Equal("erd:hide", "erd:group", "erd:color");
    }

    /// <summary>
    /// Shift does not arbitrate plugin semantics, so a repeated attribute is preserved rather than
    /// collapsed. AttributeValue resolves the effective value last-wins for plugins that want one.
    /// </summary>
    [Fact]
    public void ParseTable_DuplicateAttributes_ArePreservedAndResolveLastWins()
    {
        var table = Parse("model User {\n  @erd:group First\n  @erd:group Second\n}");

        table.Attributes.Should().HaveCount(2);
        table.Attributes.AttributeValue("erd:group").Should().Be("Second");
        table.Attributes.HasAttribute("ERD:GROUP").Should().BeTrue();
    }

    [Fact]
    public void ParseTable_NoIdentity_StillDisablesIdentityAndIsAnOrdinaryFlag()
    {
        var table = Parse("model User {\n  ustring(50) Name\n  @NoIdentity\n}");

        table.Fields.Single(f => f.IsPrimaryKey).IsIdentity.Should().BeFalse();
        table.Attributes.Should().ContainSingle().Which.Should().Be(new AttributeModel("NoIdentity", null));
    }

    /// <summary>
    /// @unique is matched on the index line before the attribute branch is reached, so it stays an
    /// index modifier and never becomes a plugin attribute.
    /// </summary>
    [Fact]
    public void ParseTable_UniqueOnAnIndexLine_IsNotAnAttribute()
    {
        var table = Parse("model User {\n  ustring(256) Email\n  index (Email) @unique\n}");

        table.Attributes.Should().BeEmpty();
        table.Indexes.Single().IsUnique.Should().BeTrue();
    }

    // ---- validation ----------------------------------------------------------

    [Theory]
    [InlineData("@1bad")]
    [InlineData("@-bad")]
    [InlineData("@bad.name")]
    [InlineData("@bad!name")]
    public void ParseTable_InvalidAttributeName_Throws(string attributeLine)
    {
        var act = () => Parse($"model User {{\n  {attributeLine}\n}}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*attribute name*");
    }

    [Theory]
    // A quote would let a value close the string it is embedded in.
    [InlineData("@erd:note 'it''s'")]
    // A bracket would let it terminate a DBML settings list.
    [InlineData("@erd:note 'a]b'")]
    // Path traversal.
    [InlineData("@erd:note 'a..b'")]
    [InlineData("@erd:note 'a/b'")]
    [InlineData("@erd:note 'a\\b'")]
    [InlineData("@erd:note 'a:b'")]
    // A second attribute or a comment forged on re-parse.
    [InlineData("@erd:note 'a@b'")]
    [InlineData("@erd:note 'a#b'")]
    [InlineData("@erd:note 'a,b'")]
    public void ParseTable_InvalidAttributeValue_Throws(string attributeLine)
    {
        var act = () => Parse($"model User {{\n  {attributeLine}\n}}");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseTable_MalformedAttributeLine_Throws()
    {
        var act = () => Parse("model User {\n  @erd:note 'one' two\n}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Malformed attribute*");
    }

    // ---- namespaces ----------------------------------------------------------

    /// <summary>
    /// The namespace is structural: it is split off at the first colon and exposed separately, so a
    /// consumer never re-parses the name. <c>Name</c> keeps the full spelling so exporting is
    /// unchanged.
    /// </summary>
    [Fact]
    public void ParseTable_NamespacedAttribute_ExposesBothHalves()
    {
        var table = Parse("model User {\n  ustring(50) Name\n  @erd:group Billing\n}");

        var attribute = table.Attributes.Single();
        attribute.Name.Should().Be("erd:group");
        attribute.Namespace.Should().Be("erd");
        attribute.LocalName.Should().Be("group");
        attribute.Value.Should().Be("Billing");
    }

    /// <summary>
    /// @NoIdentity is a real un-namespaced attribute that Shift itself reads, so a name with no colon
    /// stays valid and means "no namespace" — null rather than an empty string.
    /// </summary>
    [Fact]
    public void ParseTable_UnNamespacedAttribute_HasNullNamespace()
    {
        var table = Parse("model User {\n  ustring(50) Name\n  @NoIdentity\n}");

        var attribute = table.Attributes.Single();
        attribute.Name.Should().Be("NoIdentity");
        attribute.Namespace.Should().BeNull();
        attribute.LocalName.Should().Be("NoIdentity");
    }

    /// <summary>
    /// A .dmd file must stay readable by a Shift build that has never heard of the plugin it was
    /// annotated for, so an attribute in a namespace no registered plugin claims parses and reaches
    /// the model unchanged rather than being an error.
    /// </summary>
    [Fact]
    public void ParseTable_UnknownNamespace_IsPreservedNotRejected()
    {
        var table = Parse("model User {\n  ustring(50) Name\n  @audit:owner Platform\n}");

        var attribute = table.Attributes.Single();
        attribute.Name.Should().Be("audit:owner");
        attribute.Namespace.Should().Be("audit");
        attribute.LocalName.Should().Be("owner");
        attribute.Value.Should().Be("Platform");
    }

    /// <summary>
    /// The same, round-tripped: an unknown namespace survives a parse/export cycle byte-exact, so
    /// running `shift export` over a file annotated for a plugin you do not have cannot silently
    /// strip its annotations.
    /// </summary>
    [Fact]
    public void ParseTable_UnknownNamespace_SurvivesAnExportRoundTrip()
    {
        var original = "model User {\n  ustring(50) Name @audit:pii\n  @audit:owner Platform\n}";

        var table = Parse(original);
        var reparsed = Parse(new ModelExporter().GenerateDmdContent(table, []));

        reparsed.Attributes.Should().BeEquivalentTo(table.Attributes);
        reparsed.Fields.Single(x => x.Name == "Name").Attributes
            .Should().BeEquivalentTo(table.Fields.Single(x => x.Name == "Name").Attributes);
    }

    [Theory]
    // Both halves must be non-empty.
    [InlineData("@:hide")]
    [InlineData("@erd:")]
    [InlineData("@:")]
    // At most one colon separates a namespace from a name.
    [InlineData("@erd:sub:hide")]
    // Each half still obeys the identifier rule.
    [InlineData("@erd:1hide")]
    [InlineData("@1erd:hide")]
    [InlineData("@erd:bad.name")]
    [InlineData("@erd :hide")]
    public void ParseTable_InvalidNamespacedAttributeName_Throws(string attributeLine)
    {
        var act = () => Parse($"model User {{\n  {attributeLine}\n}}");

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// The length bound covers the whole spelling, the separator included, so a namespace cannot be
    /// used to smuggle a name past it.
    /// </summary>
    [Fact]
    public void ParseTable_OverlongNamespacedAttributeName_Throws()
    {
        var name = "erd:" + new string('a', 62);

        var act = () => Parse($"model User {{\n  @{name}\n}}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*attribute name*");
    }

    /// <summary>
    /// The colon is a name character only. It stays forbidden in a value, so a value can never be
    /// read as a namespaced name or as a path.
    /// </summary>
    [Fact]
    public void ParseTable_ColonInAttributeValue_StillThrows()
    {
        var act = () => Parse("model User {\n  @erd:group 'a:b'\n}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid value*");
    }

    // ---- field level ---------------------------------------------------------

    [Fact]
    public void ParseTable_TrailingFlagOnAField_BecomesAFieldAttribute()
    {
        var table = Parse("model User {\n  ustring(100) Email @erd:hide\n}");

        var field = table.Fields.Single(f => f.Name == "Email");
        field.Type.Should().Be("nvarchar");
        field.Precision.Should().Be(100);
        field.Attributes.Should().ContainSingle().Which.Should().Be(new AttributeModel("erd:hide", null));
    }

    [Fact]
    public void ParseTable_MultipleTrailingAttributesOnAField_ArePreservedInOrder()
    {
        var table = Parse("model User {\n  ustring(100) Email @erd:hide @erd:note 'PII'\n}");

        var field = table.Fields.Single(f => f.Name == "Email");
        field.Attributes.Should().Equal(
            new AttributeModel("erd:hide", null),
            new AttributeModel("erd:note", "PII"));
    }

    [Fact]
    public void ParseTable_TrailingValuedAttributeOnANullableField_KeepsBothNullabilityAndValue()
    {
        var table = Parse("model User {\n  ustring(100)? Nickname @erd:note Optional\n}");

        var field = table.Fields.Single(f => f.Name == "Nickname");
        field.IsNullable.Should().BeTrue();
        field.Attributes.Single().Value.Should().Be("Optional");
    }

    /// <summary>
    /// The alias form is recognised by token count, so an attribute left in the line would have
    /// silently dropped the alias and produced the column "UserID" instead of "CreatedByUserID".
    /// </summary>
    [Fact]
    public void ParseTable_TrailingAttributeOnAnAliasedForeignKey_KeepsTheAlias()
    {
        var table = Parse("model Task {\n  model User? as CreatedBy @erd:hide\n}");

        var fk = table.ForeignKeys.Should().ContainSingle().Subject;
        fk.ColumnName.Should().Be("CreatedByUserID");
        fk.TargetTable.Should().Be("User");
        fk.IsNullable.Should().BeTrue();

        var field = table.Fields.Single(f => f.Name == "CreatedByUserID");
        field.Attributes.Should().ContainSingle().Which.Name.Should().Be("erd:hide");
    }

    [Fact]
    public void ParseTable_TrailingAttributeOnAModelsLine_KeepsTheRelationshipType()
    {
        var table = Parse("model User {\n  models Task @erd:group 'Work Items'\n}");

        var fk = table.ForeignKeys.Should().ContainSingle().Subject;
        fk.RelationshipType.Should().Be(RelationshipType.OneToMany);
        fk.ColumnName.Should().Be("TaskID");

        table.Fields.Single(f => f.Name == "TaskID").Attributes.Single().Value.Should().Be("Work Items");
    }

    [Fact]
    public void ParseTable_TrailingAttributeOnAnOptionalModelLine_IsKept()
    {
        var table = Parse("model Task {\n  !model User? as CreatedBy @erd:hide\n}");

        table.ForeignKeys.Single().ColumnName.Should().Be("CreatedByUserID");
        table.Fields.Single(f => f.Name == "CreatedByUserID").Attributes.Should().ContainSingle();
    }

    [Fact]
    public void ParseTable_FieldWithoutAttributes_HasAnEmptyList()
    {
        var table = Parse("model User {\n  ustring(100) Email\n}");

        table.Fields.Single(f => f.Name == "Email").Attributes.Should().BeEmpty();
    }

    // ---- mixin level ---------------------------------------------------------

    /// <summary>
    /// Regression: a mixin-level attribute used to fall through to the field parser, so @erd:hide was
    /// silently dropped and "@erd:group Billing" created a field named Billing of type @erd:group.
    /// </summary>
    [Fact]
    public void ParseMixin_AttributeLines_BecomeAttributesNotFields()
    {
        var mixin = _sut.ParseMixin("mixin Auditable {\n  @erd:hide\n  @erd:group Billing\n  datetime CreatedDateTime\n}");

        mixin.Attributes.Should().Equal(
            new AttributeModel("erd:hide", null),
            new AttributeModel("erd:group", "Billing"));

        mixin.Fields.Should().ContainSingle().Which.Name.Should().Be("CreatedDateTime");
        mixin.Fields.Should().NotContain(f => f.Name == "Billing");
    }

    /// <summary>
    /// Regression: comment lines in a .dmdx used to become fields too.
    /// </summary>
    [Theory]
    [InlineData("// a comment")]
    [InlineData("# a comment")]
    public void ParseMixin_CommentLines_AreSkipped(string comment)
    {
        var mixin = _sut.ParseMixin($"mixin Auditable {{\n  {comment}\n  datetime CreatedDateTime\n}}");

        mixin.Fields.Should().ContainSingle().Which.Name.Should().Be("CreatedDateTime");
    }

    [Fact]
    public void ParseMixin_FieldAttributes_AreKept()
    {
        var mixin = _sut.ParseMixin("mixin Auditable {\n  ustring(50) CreatedBy @erd:hide\n}");

        mixin.Fields.Single().Attributes.Single().Name.Should().Be("erd:hide");
    }

    [Fact]
    public void ParseMixin_InvalidAttribute_Throws()
    {
        var act = () => _sut.ParseMixin("mixin Auditable {\n  @1bad\n}");

        act.Should().Throw<InvalidOperationException>();
    }

    // ---- mixin application ---------------------------------------------------

    private static TableModel ParseWithMixin(string mixinContent, string tableContent)
    {
        var parser = new Parser();
        var model = new DatabaseModel();
        var mixin = parser.ParseMixin(mixinContent);
        model.Mixins.Add(mixin.Name, mixin);
        parser.ParseTable(model, tableContent);
        return model.Tables.Values.Single();
    }

    [Fact]
    public void ApplyMixin_MixinAttributes_AreMergedOntoTheTable()
    {
        var table = ParseWithMixin(
            "mixin Auditable {\n  @erd:group Audit\n  datetime CreatedDateTime\n}",
            "model User with Auditable {\n  ustring(50) Name\n}");

        table.Attributes.AttributeValue("erd:group").Should().Be("Audit");
    }

    [Fact]
    public void ApplyMixin_OnCollision_TheModelWins()
    {
        var table = ParseWithMixin(
            "mixin Auditable {\n  @erd:group Audit\n  datetime CreatedDateTime\n}",
            "model User with Auditable {\n  @erd:group Billing\n  ustring(50) Name\n}");

        table.Attributes.Should().ContainSingle().Which.Value.Should().Be("Billing");
    }

    [Fact]
    public void ApplyMixin_CollisionIsCaseInsensitive()
    {
        var table = ParseWithMixin(
            "mixin Auditable {\n  @ERD:GROUP Audit\n  datetime CreatedDateTime\n}",
            "model User with Auditable {\n  @erd:group Billing\n  ustring(50) Name\n}");

        table.Attributes.Should().ContainSingle().Which.Value.Should().Be("Billing");
    }

    [Fact]
    public void ApplyMixin_MixinFieldAttributes_LandOnTheCopiedField()
    {
        var table = ParseWithMixin(
            "mixin Auditable {\n  ustring(50) CreatedBy @erd:hide\n}",
            "model User with Auditable {\n  ustring(50) Name\n}");

        table.Fields.Single(f => f.Name == "CreatedBy").Attributes.Single().Name.Should().Be("erd:hide");
    }

    /// <summary>
    /// model.Mixins holds one shared MixinModel instance, so every table using a mixin must get its
    /// own attribute list. Handing over the mixin's own list would leak a mutation from one table
    /// into every other table using that mixin.
    /// </summary>
    [Fact]
    public void ApplyMixin_MutatingTheCopiedFieldAttributes_DoesNotAffectTheSharedMixin()
    {
        var parser = new Parser();
        var model = new DatabaseModel();
        var mixin = parser.ParseMixin("mixin Auditable {\n  ustring(50) CreatedBy @erd:hide\n}");
        model.Mixins.Add(mixin.Name, mixin);

        parser.ParseTable(model, "model User with Auditable {\n  ustring(50) Name\n}");
        parser.ParseTable(model, "model Task with Auditable {\n  ustring(50) Title\n}");

        model.Tables["User"].Fields.Single(f => f.Name == "CreatedBy").Attributes
            .Add(new AttributeModel("erd:note", "leaked"));

        mixin.Fields.Single().Attributes.Should().ContainSingle().Which.Name.Should().Be("erd:hide");
        model.Tables["Task"].Fields.Single(f => f.Name == "CreatedBy").Attributes
            .Should().ContainSingle().Which.Name.Should().Be("erd:hide");
    }

    [Fact]
    public void ApplyMixin_MixinDeclaringAnAttributeTwice_KeepsBoth()
    {
        var table = ParseWithMixin(
            "mixin Auditable {\n  @erd:group First\n  @erd:group Second\n  datetime CreatedDateTime\n}",
            "model User with Auditable {\n  ustring(50) Name\n}");

        table.Attributes.Should().HaveCount(2);
        table.Attributes.AttributeValue("erd:group").Should().Be("Second");
    }

    // ---- helpers -------------------------------------------------------------

    [Fact]
    public void AttributeValue_ForAFlag_IsNull()
    {
        var attributes = new List<AttributeModel> { new("erd:hide", null) };

        attributes.AttributeValue("erd:hide").Should().BeNull();
        attributes.AttributeValue("missing").Should().BeNull();
        attributes.HasAttribute("missing").Should().BeFalse();
    }

    [Fact]
    public void AttributeModel_ToString_ShowsTheDeclaredForm()
    {
        new AttributeModel("erd:hide", null).ToString().Should().Be("@erd:hide");
        new AttributeModel("erd:group", "Billing").ToString().Should().Be("@erd:group Billing");
    }

    [Fact]
    public void DmdAttributeValidator_TrimsTheValue()
    {
        var attribute = DmdAttributeValidator.Create("erd:group", "  Billing  ", "line");

        attribute.Value.Should().Be("Billing");
    }

    [Fact]
    public void DmdAttributeValidator_RejectsAnEmptyValue()
    {
        var act = () => DmdAttributeValidator.Create("erd:group", "", "@erd:group ''");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid value*");
    }
}