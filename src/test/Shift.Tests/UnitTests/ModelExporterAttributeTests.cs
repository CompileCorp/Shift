using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Tests that plugin attributes survive a DMD export.
///
/// Intent: whatever the author wrote comes back out — model-level attributes on their own lines,
/// field-level attributes as trailing tokens, in declaration order, with a value single-quoted only
/// when it needs to be.
/// </summary>
public class ModelExporterAttributeTests : UnitTestContext<ModelExporter>
{
    [Fact]
    public async Task GenerateDmdContent_WithModelAndFieldAttributes_RendersThemBack()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("Email", "nvarchar", f => f
                    .Precision(256)
                    .WithAttribute("erd-hide")
                    .WithAttribute("erd-note", "PII"))
                .WithField("Nickname", "nvarchar", f => f.Precision(50).Nullable())
                .WithForeignKey("CreatedByUserID", "User", "UserID", RelationshipType.OneToOne)
                .WithAttribute("erd-hide")
                .WithAttribute("erd-group", "Billing Ops")
                .WithAttribute("erd-color", "3498DB"))
            .Build();

        var table = model.Tables["User"];
        table.Fields.Add(new FieldModel
        {
            Name = "CreatedByUserID",
            Type = "int",
            Attributes = [new AttributeModel("erd-hide", null)]
        });

        var dmd = Sut.GenerateDmdContent(table, []);

        await Verify(dmd);
    }

    [Fact]
    public void GenerateDmdContent_FlagAndValuedAttributes_AreQuotedOnlyWhenNeeded()
    {
        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithAttribute("erd-hide")
                .WithAttribute("erd-group", "Billing")
                .WithAttribute("erd-note", "Two words"))
            .Build();

        var dmd = Sut.GenerateDmdContent(model.Tables["User"], []);

        dmd.Should().Contain("  @erd-hide");
        dmd.Should().Contain("  @erd-group Billing");
        dmd.Should().Contain("  @erd-note 'Two words'");
    }

    /// <summary>
    /// The round trip is what makes attributes safe to add: Shift must be able to read back
    /// everything it writes, with the same structure.
    /// </summary>
    [Fact]
    public void GenerateDmdContent_ParseExportParse_PreservesEveryAttribute()
    {
        const string dmd = """
            model Task {
              model User? as CreatedBy @erd-hide
              models Comment @erd-group 'Work Items'
              ustring(100) Title @erd-note 'Short title'
              ustring(500)? Description @erd-hide @erd-note PII
              decimal(10,2) Estimate
              index (Title)
              @erd-hide
              @erd-group 'Work Items'
              @erd-note 'A unit of work'
            }
            """;

        var parser = new Parser();
        var first = new DatabaseModel();
        parser.ParseTable(first, dmd);

        var exported = Sut.GenerateDmdContent(first.Tables["Task"], []);

        var second = new DatabaseModel();
        parser.ParseTable(second, exported);

        var before = first.Tables["Task"];
        var after = second.Tables["Task"];

        after.Attributes.Should().Equal(before.Attributes);

        foreach (var field in before.Fields)
        {
            after.Fields.Should().ContainSingle(f => f.Name == field.Name)
                .Which.Attributes.Should().Equal(field.Attributes, $"field {field.Name} keeps its attributes");
        }

        after.ForeignKeys.Select(x => x.ColumnName).Should().BeEquivalentTo(before.ForeignKeys.Select(x => x.ColumnName));
    }

    /// <summary>
    /// When the exporter auto-detects a mixin it writes "with &lt;Mixin&gt;" instead of the mixin's
    /// members, so the mixin's own attributes must not also be written inline.
    /// </summary>
    [Fact]
    public void GenerateDmdContent_AutoAppliedMixinAttributes_AreNotEmittedTwice()
    {
        var mixin = new MixinModel
        {
            Name = "Auditable",
            Fields = [new FieldModel { Name = "CreatedDateTime", Type = "datetime" }],
            Attributes = [new AttributeModel("erd-group", "Audit")]
        };

        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("CreatedDateTime", "datetime")
                .WithAttribute("erd-group", "Audit")
                .WithAttribute("erd-hide"))
            .Build();

        var dmd = Sut.GenerateDmdContent(model.Tables["User"], [mixin]);

        dmd.Should().Contain("model User with Auditable {");
        dmd.Should().NotContain("@erd-group");
        dmd.Should().Contain("@erd-hide");
    }

    [Fact]
    public void GenerateDmdContent_TableAttributeDifferingFromTheMixin_IsStillEmitted()
    {
        var mixin = new MixinModel
        {
            Name = "Auditable",
            Fields = [new FieldModel { Name = "CreatedDateTime", Type = "datetime" }],
            Attributes = [new AttributeModel("erd-group", "Audit")]
        };

        var model = DatabaseModelBuilder.Create()
            .WithTable("User", t => t
                .WithField("UserID", "int", f => f.PrimaryKey().Identity())
                .WithField("CreatedDateTime", "datetime")
                .WithAttribute("erd-group", "Billing"))
            .Build();

        var dmd = Sut.GenerateDmdContent(model.Tables["User"], [mixin]);

        dmd.Should().Contain("@erd-group Billing");
    }
}