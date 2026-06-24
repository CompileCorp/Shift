using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Explicit-assertion tests for <see cref="ModelExporter"/> covering ExportToDmd file output,
/// mixin loading, and the GenerateDmdContent branches not exercised by the snapshot tests
/// (one-to-many relationships, semantic FK aliases, nullable FKs, index rendering and
/// auto-applied mixins). Assertions encode the intended DMD syntax so incorrect output fails.
/// </summary>
public class ModelExporterBranchTests : IDisposable
{
    private readonly ModelExporter _sut = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ShiftExporterTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // ---- ExportToDmd ---------------------------------------------------------

    [Fact]
    public void ExportToDmd_CreatesDirectoryAndOneFilePerTable()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", t => t.WithField("OrderID", "int", f => f.PrimaryKey().Identity()))
            .Build();

        Directory.Exists(_dir).Should().BeFalse();

        _sut.ExportToDmd(model, _dir);

        File.Exists(Path.Combine(_dir, "User.dmd")).Should().BeTrue();
        File.Exists(Path.Combine(_dir, "Order.dmd")).Should().BeTrue();
    }

    [Fact]
    public void ExportToDmd_FileContentMatchesGenerateDmdContent()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Name", "nvarchar", f => f.Precision(50)))
            .Build();

        _sut.ExportToDmd(model, _dir);

        var written = File.ReadAllText(Path.Combine(_dir, "User.dmd"));
        var expected = _sut.GenerateDmdContent(model.Tables["User"], model.Mixins.Values.ToList());
        written.Should().Be(expected);
    }

    [Fact]
    public void ExportToDmd_WithMixinFile_LoadsMixinAndAppliesIt()
    {
        // A table containing all of the mixin's (required) fields should be exported with a
        // "with <Mixin>" header and the mixin fields omitted from the body.
        Directory.CreateDirectory(_dir);
        var mixinPath = Path.Combine(_dir, "Auditable.dmdx");
        File.WriteAllText(mixinPath,
            "mixin Auditable {\n  datetime CreatedDateTime\n  int LockNumber\n}\n");

        var model = DatabaseModelBuilder.Create()
            .WithTable("Doc", t => t
                .WithField("DocID", "int", f => f.PrimaryKey().Identity())
                .WithField("Title", "nvarchar", f => f.Precision(100))
                .WithField("CreatedDateTime", "datetime")
                .WithField("LockNumber", "int"))
            .Build();

        _sut.ExportToDmd(model, _dir, new List<string> { mixinPath });

        var content = File.ReadAllText(Path.Combine(_dir, "Doc.dmd"));
        content.Should().Contain("model Doc with Auditable {");
        content.Should().Contain("Title");
        content.Should().NotContain("CreatedDateTime");
        content.Should().NotContain("LockNumber");
    }

    [Fact]
    public void ExportToDmd_WithMissingMixinFile_DoesNotThrowAndStillExports()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .Build();

        var act = () => _sut.ExportToDmd(model, _dir, new List<string> { Path.Combine(_dir, "missing.dmdx") });

        act.Should().NotThrow();
        File.Exists(Path.Combine(_dir, "User.dmd")).Should().BeTrue();
    }

    [Fact]
    public void ExportToDmd_WithMalformedMixinFile_DoesNotThrow()
    {
        Directory.CreateDirectory(_dir);
        var mixinPath = Path.Combine(_dir, "Bad.dmdx");
        // Non-numeric precision forces int.Parse to throw while parsing the mixin; ExportToDmd
        // must catch it and continue rather than propagating.
        File.WriteAllText(mixinPath, "mixin Bad {\n  string(abc) Name\n}\n");

        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .Build();

        var act = () => _sut.ExportToDmd(model, _dir, new List<string> { mixinPath });

        act.Should().NotThrow();
        File.Exists(Path.Combine(_dir, "User.dmd")).Should().BeTrue();
    }

    // ---- GenerateDmdContent: relationships -----------------------------------

    [Fact]
    public void GenerateDmdContent_OneToManyForeignKey_UsesModelsKeyword()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("Order", t => t
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int")
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Order"], new List<MixinModel>());

        dmd.Should().Contain("models User");
        dmd.Should().NotContain("model User as"); // no alias when column is exactly <Target>ID
    }

    [Fact]
    public void GenerateDmdContent_OneToOneForeignKey_UsesModelKeyword()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("Profile", t => t
                .WithField("ProfileID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int")
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToOne))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Profile"], new List<MixinModel>());

        dmd.Should().Contain("model User");
        dmd.Should().NotContain("models User");
    }

    [Fact]
    public void GenerateDmdContent_SemanticForeignKeyName_EmitsAsAlias()
    {
        // "CreatedByUserID" targeting "User" => semantic name "CreatedBy", which differs from
        // the conventional "UserID", so it must be rendered with an explicit alias.
        var model = DatabaseModelBuilder.Create()
            .WithTable("Doc", t => t
                .WithField("DocID", "int", f => f.PrimaryKey().Identity())
                .WithField("CreatedByUserID", "int")
                .WithForeignKey("CreatedByUserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Doc"], new List<MixinModel>());

        dmd.Should().Contain("models User as CreatedBy");
    }

    [Fact]
    public void GenerateDmdContent_OneToOneSemanticForeignKey_EmitsModelWithAlias()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("Profile", t => t
                .WithField("ProfileID", "int", f => f.PrimaryKey().Identity())
                .WithField("OwnerUserID", "int")
                .WithForeignKey("OwnerUserID", "User", "UserID", RelationshipType.OneToOne))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Profile"], new List<MixinModel>());

        dmd.Should().Contain("model User as Owner");
        dmd.Should().NotContain("models User");
    }

    [Fact]
    public void GenerateDmdContent_ForeignKeyColumnEndingInTargetWithoutId_DerivesSemanticName()
    {
        // "OwnerUser" ends with the target table name ("User") but not "UserID";
        // the semantic name falls back to the leading remainder ("Owner").
        var model = DatabaseModelBuilder.Create()
            .WithTable("Doc", t => t
                .WithField("DocID", "int", f => f.PrimaryKey().Identity())
                .WithField("OwnerUser", "int")
                .WithForeignKey("OwnerUser", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Doc"], new List<MixinModel>());

        dmd.Should().Contain("models User as Owner");
    }

    [Fact]
    public void GenerateDmdContent_ForeignKeyColumnUnrelatedToTarget_UsesColumnNameAsAlias()
    {
        // "Ref" matches neither "<Target>ID" nor "<Target>"; the raw column name is used.
        var model = DatabaseModelBuilder.Create()
            .WithTable("Doc", t => t
                .WithField("DocID", "int", f => f.PrimaryKey().Identity())
                .WithField("Ref", "int")
                .WithForeignKey("Ref", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Doc"], new List<MixinModel>());

        dmd.Should().Contain("models User as Ref");
    }

    [Fact]
    public void GenerateDmdContent_NullableForeignKey_AppendsQuestionMark()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("Order", t => t
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int", f => f.Nullable())
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();
        model.Tables["Order"].ForeignKeys[0].IsNullable = true;

        var dmd = _sut.GenerateDmdContent(model.Tables["Order"], new List<MixinModel>());

        dmd.Should().Contain("models User?");
    }

    // ---- GenerateDmdContent: indexes -----------------------------------------

    [Fact]
    public void GenerateDmdContent_UniqueCustomIndex_EmitsKey()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithIndex("ix", "Email", isUnique: true))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["User"], new List<MixinModel>());

        dmd.Should().Contain("key (Email)");
    }

    [Fact]
    public void GenerateDmdContent_NonUniqueCustomIndex_EmitsIndex()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithIndex("ix", "Email", isUnique: false))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["User"], new List<MixinModel>());

        dmd.Should().Contain("index (Email)");
    }

    [Fact]
    public void GenerateDmdContent_IndexOnForeignKeyColumn_UsesModelNameNotColumnName()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("OrderItem", t => t
                .WithField("OrderItemID", "int", f => f.PrimaryKey().Identity())
                .WithField("ProductID", "int")
                .WithField("Sku", "nvarchar", f => f.Precision(50))
                .WithForeignKey("ProductID", "Product", "ProductID", RelationshipType.OneToMany)
                .WithIndex("ix", new[] { "ProductID", "Sku" }, isUnique: false))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["OrderItem"], new List<MixinModel>());

        dmd.Should().Contain("index (Product, Sku)");
    }

    [Fact]
    public void GenerateDmdContent_PrimaryKeyOnlyIndex_IsSkipped()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithIndex("pk", "UserID", isUnique: true))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["User"], new List<MixinModel>());

        dmd.Should().NotContain("key (UserID)");
        dmd.Should().NotContain("index (UserID)");
    }

    [Fact]
    public void GenerateDmdContent_IndexOverOnlyForeignKeys_IsSkipped()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("OrderItem", t => t
                .WithField("OrderItemID", "int", f => f.PrimaryKey().Identity())
                .WithField("ProductID", "int")
                .WithForeignKey("ProductID", "Product", "ProductID", RelationshipType.OneToMany)
                .WithIndex("ix", "ProductID", isUnique: false))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["OrderItem"], new List<MixinModel>());

        dmd.Should().NotContain("index (");
    }

    [Fact]
    public void GenerateDmdContent_DuplicateIndexes_EmittedOnce()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f.Precision(256))
                .WithIndex("ix1", "Email", isUnique: false)
                .WithIndex("ix2", "Email", isUnique: false))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["User"], new List<MixinModel>());

        System.Text.RegularExpressions.Regex.Matches(dmd, @"index \(Email\)").Count.Should().Be(1);
    }

    // ---- GenerateDmdContent: attributes & auto-applied mixins -----------------

    [Fact]
    public void GenerateDmdContent_TableAttributes_EmittedWithAtPrefix()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithAttribute("audit", true))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["User"], new List<MixinModel>());

        dmd.Should().Contain("@audit");
    }

    [Fact]
    public void GenerateDmdContent_AutoAppliesMixinWhenAllRequiredFieldsPresent()
    {
        var mixin = new MixinModel
        {
            Name = "Auditable",
            Fields =
            {
                new FieldModel { Name = "CreatedDateTime", Type = "datetime" },
                new FieldModel { Name = "LockNumber", Type = "int" }
            }
        };
        var model = DatabaseModelBuilder.Create()
            .WithTable("Doc", t => t
                .WithField("DocID", "int", f => f.PrimaryKey().Identity())
                .WithField("Title", "nvarchar", f => f.Precision(100))
                .WithField("CreatedDateTime", "datetime")
                .WithField("LockNumber", "int"))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Doc"], new List<MixinModel> { mixin });

        dmd.Should().StartWith("model Doc with Auditable {");
        dmd.Should().Contain("Title");
        dmd.Should().NotContain("CreatedDateTime");
        dmd.Should().NotContain("LockNumber");
    }

    [Fact]
    public void GenerateDmdContent_DoesNotAutoApplyMixin_WhenRequiredFieldMissing()
    {
        var mixin = new MixinModel
        {
            Name = "Auditable",
            Fields =
            {
                new FieldModel { Name = "CreatedDateTime", Type = "datetime" },
                new FieldModel { Name = "LockNumber", Type = "int" }
            }
        };
        var model = DatabaseModelBuilder.Create()
            .WithTable("Doc", t => t
                .WithField("DocID", "int", f => f.PrimaryKey().Identity())
                .WithField("CreatedDateTime", "datetime")) // LockNumber missing
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Doc"], new List<MixinModel> { mixin });

        dmd.Should().StartWith("model Doc {");
        dmd.Should().NotContain("with Auditable");
    }

    [Fact]
    public void GenerateDmdContent_ForeignKeyThatIsAMixinField_IsOmittedFromBody()
    {
        // When an auto-applied mixin owns the FK column, the relationship is provided by the
        // mixin and must not be re-emitted on the model body.
        var mixin = new MixinModel
        {
            Name = "Owned",
            Fields = { new FieldModel { Name = "OwnerUserID", Type = "int" } }
        };
        var model = DatabaseModelBuilder.Create()
            .WithTable("Doc", t => t
                .WithField("DocID", "int", f => f.PrimaryKey().Identity())
                .WithField("OwnerUserID", "int")
                .WithForeignKey("OwnerUserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Doc"], new List<MixinModel> { mixin });

        dmd.Should().Contain("with Owned");
        dmd.Should().NotContain("model User");
    }

    [Fact]
    public void GenerateDmdContent_UnsupportedType_EmittedAsComment()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("Geo", t => t
                .WithField("GeoID", "int", f => f.PrimaryKey().Identity())
                .WithField("Shape", "geometry"))
            .Build();

        var dmd = _sut.GenerateDmdContent(model.Tables["Geo"], new List<MixinModel>());

        dmd.Should().Contain("# geometry Shape");
    }
}