using Compile.Shift.Model;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Tests for behaviour on the model classes themselves: the FieldModel type guard and the
/// diagnostic ToString overrides (which must be robust, including on empty collections).
/// </summary>
public class ModelClassTests
{
    [Fact]
    public void FieldModel_SettingTypeToMixin_Throws()
    {
        // "mixin" is a DSL keyword, not a storable field type; assigning it is rejected.
        var act = () => new FieldModel { Name = "X", Type = "mixin" };

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void FieldModel_ToString_IncludesNameAndType()
    {
        var field = new FieldModel { Name = "Email", Type = "nvarchar" };

        field.ToString().Should().Be("Field:\"Email\" Type:\"nvarchar\"");
    }

    [Fact]
    public void TableModel_ToString_IncludesNameAndFields()
    {
        var table = new TableModel
        {
            Name = "User",
            Fields =
            {
                new FieldModel { Name = "UserID", Type = "int" },
                new FieldModel { Name = "Name", Type = "nvarchar" }
            }
        };

        var text = table.ToString();

        text.Should().Contain("Name:\"User\"");
        text.Should().Contain("Field:\"UserID\"");
        text.Should().Contain("Field:\"Name\"");
    }

    [Fact]
    public void TableModel_ToString_WithNoFields_DoesNotThrow()
    {
        var table = new TableModel { Name = "Empty" };

        var act = () => table.ToString();

        act.Should().NotThrow();
        table.ToString().Should().Contain("Name:\"Empty\"");
    }

    [Fact]
    public void ForeignKeyModel_ToString_IncludesColumnAndTarget()
    {
        var fk = new ForeignKeyModel
        {
            ColumnName = "UserID",
            TargetTable = "User",
            TargetColumnName = "UserID"
        };

        fk.ToString().Should().Be("UserID User UserID");
    }
}