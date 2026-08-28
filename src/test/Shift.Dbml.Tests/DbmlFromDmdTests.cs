using Compile.Shift.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Compile.Shift.Dbml.Tests;

/// <summary>
/// End-to-end tests from DMD source through the parser to DBML output.
///
/// Intent: the documented attribute syntax must actually parse. A DBML-only test can be satisfied by
/// a hand-built model that no .dmd file could ever produce — this catches that, including the case
/// where a documented value contains a character the attribute validator rejects.
/// </summary>
public class DbmlFromDmdTests
{
    private readonly DbmlExporter _sut = new() { Logger = NullLogger.Instance };
    private readonly Parser _parser = new();

    private DatabaseModel Parse(string mixin, params string[] models)
    {
        var model = new DatabaseModel();

        if (mixin.Length > 0)
        {
            var parsed = _parser.ParseMixin(mixin);
            model.Mixins.Add(parsed.Name, parsed);
        }

        foreach (var content in models)
        {
            _parser.ParseTable(model, content);
        }

        return model;
    }

    [Fact]
    public Task Dmd_WithEveryErdAttribute_ProducesTheExpectedDiagram()
    {
        const string user = """
            model User {
              ustring(256) Email
              ustring(64) PasswordHash @erd:hide
              ustring(100)? DisplayName @erd:note 'Shown in the UI'
              key (Email)
              @erd:note 'Application user'
              @erd:color 3498DB
            }
            """;

        const string invoice = """
            model Invoice {
              model User? as RaisedBy @erd:note 'Who raised it'
              ustring(50) Reference
              decimal(19,4) Total
              index (Reference)
              @erd:group 'Billing Ops'
            }
            """;

        const string payment = """
            model Payment {
              model Invoice
              decimal(19,4) Amount
              @erd:group 'Billing Ops'
            }
            """;

        const string auditLog = """
            model AuditLog {
              ustring(200) Message
              @erd:hide
            }
            """;

        var dbml = _sut.GenerateDbml(Parse("", user, invoice, payment, auditLog));

        return Verify(dbml);
    }

    /// <summary>
    /// A mixin-level attribute reaches the diagram through every model that uses the mixin.
    /// </summary>
    [Fact]
    public void Dmd_MixinLevelGroup_GroupsEveryModelUsingIt()
    {
        const string auditable = """
            mixin Auditable {
              datetime CreatedDateTime
              @erd:group Audit
            }
            """;

        var model = Parse(
            auditable,
            "model User with Auditable {\n  ustring(50) Name\n}",
            "model Task with Auditable {\n  ustring(50) Title\n}");

        var dbml = _sut.GenerateDbml(model);

        dbml.Should().Contain("TableGroup Audit {\n  Task\n  User\n}".ReplaceLineEndings());
    }

    [Fact]
    public void Dmd_ModelOverridingItsMixinGroup_UsesTheModelsGroup()
    {
        const string auditable = """
            mixin Auditable {
              datetime CreatedDateTime
              @erd:group Audit
            }
            """;

        var model = Parse(
            auditable,
            "model User with Auditable {\n  @erd:group Billing\n  ustring(50) Name\n}");

        var dbml = _sut.GenerateDbml(model);

        dbml.Should().Contain("TableGroup Billing {");
        dbml.Should().NotContain("TableGroup Audit");
    }

    /// <summary>
    /// The colour is written without a leading '#' in DMD, because '#' is not a permitted
    /// attribute-value character. This is the documented form, so it has to parse.
    /// </summary>
    [Fact]
    public void Dmd_ColorWithoutAHash_ParsesAndGainsTheHashOnOutput()
    {
        var model = Parse("", "model User {\n  ustring(50) Name\n  @erd:color 38D\n}");

        _sut.GenerateDbml(model).Should().Contain("Table User [headercolor: #38D] {");
    }

    [Fact]
    public void Dmd_ColorWithAHash_IsRejectedByTheParser()
    {
        var act = () => Parse("", "model User {\n  ustring(50) Name\n  @erd:color #3498DB\n}");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid value*");
    }
}