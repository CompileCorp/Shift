using Compile.Shift.Model;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Edge-case and error-path tests for <see cref="Parser"/> that complement the main ParserTests:
/// the `extends` directive, `key(...)` alternate keys, malformed key/index lines, stray tokens,
/// and the file-based async loaders.
/// </summary>
public class ParserEdgeTests : IDisposable
{
    private readonly Parser _sut = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ShiftParserTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // ---- extends -------------------------------------------------------------

    [Fact]
    public void ParseTable_ExtendsExistingTable_AddsFieldsToThatTable()
    {
        var model = new DatabaseModel();
        _sut.ParseTable(model, "model User {\n  string(50) Name\n}");

        _sut.ParseTable(model, "extends User {\n  string(10) Note\n}");

        var user = model.Tables["User"];
        user.Fields.Should().Contain(f => f.Name == "Name");
        user.Fields.Should().Contain(f => f.Name == "Note");
        // No duplicate table was created.
        model.Tables.Should().HaveCount(1);
    }

    [Fact]
    public void ParseTable_ExtendsMissingTable_Throws()
    {
        var model = new DatabaseModel();

        var act = () => _sut.ParseTable(model, "extends Ghost {\n  string(10) Note\n}");

        act.Should().Throw<Exception>().WithMessage("*Ghost*");
    }

    // ---- key / index parsing -------------------------------------------------

    [Fact]
    public void ParseTable_KeyDirective_CreatesUniqueAlternateKeyIndex()
    {
        var model = new DatabaseModel();

        _sut.ParseTable(model, "model User {\n  string(256) Email\n  key (Email)\n}");

        var index = model.Tables["User"].Indexes.Single();
        index.Fields.Should().ContainSingle().Which.Should().Be("Email");
        index.IsUnique.Should().BeTrue();
        index.IsAlternateKey.Should().BeTrue();
    }

    [Fact]
    public void ParseTable_CompositeKeyDirective_KeepsAllFields()
    {
        var model = new DatabaseModel();

        _sut.ParseTable(model, "model User {\n  string(50) First\n  string(50) Last\n  key (First, Last)\n}");

        var index = model.Tables["User"].Indexes.Single();
        index.Fields.Should().Equal("First", "Last");
        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void ParseTable_IndexWithUniqueMarker_IsUnique()
    {
        var model = new DatabaseModel();

        _sut.ParseTable(model, "model User {\n  string(256) Email\n  index (Email) @unique\n}");

        model.Tables["User"].Indexes.Single().IsUnique.Should().BeTrue();
    }

    [Fact]
    public void ParseTable_MalformedKeyLine_IsIgnored()
    {
        var model = new DatabaseModel();

        // "key" without parentheses does not match the key regex and must not produce an index.
        _sut.ParseTable(model, "model User {\n  string(50) Name\n  key Name\n}");

        model.Tables["User"].Indexes.Should().BeEmpty();
    }

    [Fact]
    public void ParseTable_MalformedIndexLine_IsIgnored()
    {
        var model = new DatabaseModel();

        _sut.ParseTable(model, "model User {\n  string(50) Name\n  index Name\n}");

        model.Tables["User"].Indexes.Should().BeEmpty();
    }

    // ---- stray / malformed field lines --------------------------------------

    [Fact]
    public void ParseTable_StraySingleTokenLine_IsIgnored()
    {
        var model = new DatabaseModel();

        _sut.ParseTable(model, "model User {\n  garbage\n  string(50) Name\n}");

        var user = model.Tables["User"];
        // Only the PK and the valid Name field; the lone token produced no field.
        user.Fields.Select(f => f.Name).Should().BeEquivalentTo("UserID", "Name");
    }

    // ---- model with both an explicit PK type and a mixin --------------------

    [Fact]
    public void ParseTable_ModelWithTypeAndMixin_UsesExplicitPrimaryKeyType()
    {
        var model = new DatabaseModel();
        model.Mixins["Auditable"] = new MixinModel
        {
            Name = "Auditable",
            Fields = { new FieldModel { Name = "CreatedDateTime", Type = "datetime" } }
        };

        _sut.ParseTable(model, "model Order guid with Auditable {\n  string(50) Code\n}");

        var pk = model.Tables["Order"].Fields.Single(f => f.IsPrimaryKey);
        pk.Name.Should().Be("OrderID");
        pk.Type.Should().Be("uniqueidentifier");
        pk.IsIdentity.Should().BeFalse(); // guid PKs are not identity
        model.Tables["Order"].Mixins.Should().Contain("Auditable");
    }

    // ---- mixin relationships -------------------------------------------------

    [Fact]
    public void ParseMixin_RelationshipWithNullableAlias_MarksForeignKeyNullable()
    {
        // The alias itself carries the nullable marker ("as Owner?").
        var mixin = _sut.ParseMixin("mixin Rel {\n  model User as Owner?\n}");

        var fk = mixin.ForeignKeys.Single();
        fk.TargetTable.Should().Be("User");
        fk.IsNullable.Should().BeTrue();
        fk.ColumnName.Should().Be("OwnerUserID");
    }

    [Fact]
    public void ParseTable_WithMixinContainingForeignKeys_CopiesForeignKeysToTable()
    {
        var model = new DatabaseModel();
        model.Mixins["Rel"] = _sut.ParseMixin("mixin Rel {\n  model User as Owner\n}");

        _sut.ParseTable(model, "model Doc with Rel {\n  string(50) Title\n}");

        var doc = model.Tables["Doc"];
        doc.ForeignKeys.Should().Contain(fk => fk.TargetTable == "User");
    }

    // ---- comments --------------------------------------------------------------

    [Fact]
    public void ParseMixin_CommentLines_AreIgnored()
    {
        var mixin = _sut.ParseMixin(
            "mixin Auditable {\n" +
            "  // erd: hide — stamp columns mixed into almost every table\n" +
            "  # hash comment\n" +
            "  datetime CreatedDateTime\n" +
            "}");

        mixin.Fields.Select(f => f.Name).Should().BeEquivalentTo("CreatedDateTime");
    }

    [Fact]
    public void ParseMixin_TrailingComment_IsStrippedFromFieldLine()
    {
        var mixin = _sut.ParseMixin("mixin Auditable {\n  datetime CreatedDateTime // when the row was created\n}");

        var field = mixin.Fields.Single();
        field.Name.Should().Be("CreatedDateTime");
        field.Type.Should().Be("datetime");
    }

    [Fact]
    public void ParseTable_CommentLinesAndTrailingComments_AreIgnored()
    {
        var model = new DatabaseModel();

        _sut.ParseTable(model,
            "model User { // main user table\n" +
            "  // full-line comment\n" +
            "  string(50) Name // trailing comment\n" +
            "}");

        var user = model.Tables["User"];
        user.Fields.Select(f => f.Name).Should().BeEquivalentTo("UserID", "Name");
    }

    // ---- async file loaders --------------------------------------------------

    [Fact]
    public async Task ParseModelsAsync_ReadsAndParsesEachFile()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "User.dmd");
        await File.WriteAllTextAsync(path, "model User {\n  string(50) Name\n}");
        var model = new DatabaseModel();

        await _sut.ParseModelsAsync(model, new[] { path });

        model.Tables.Should().ContainKey("User");
        model.Tables["User"].Fields.Should().Contain(f => f.Name == "Name");
    }

    [Fact]
    public async Task ParseMixinsAsync_ReadsAndParsesEachFile()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "Auditable.dmdx");
        await File.WriteAllTextAsync(path, "mixin Auditable {\n  datetime CreatedDateTime\n}");
        var model = new DatabaseModel();

        await _sut.ParseMixinsAsync(model, new[] { path });

        model.Mixins.Should().ContainKey("Auditable");
        model.Mixins["Auditable"].Fields.Should().Contain(f => f.Name == "CreatedDateTime");
    }
}