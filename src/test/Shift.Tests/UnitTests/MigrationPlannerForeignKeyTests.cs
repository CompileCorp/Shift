using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Covers MigrationPlanner's "add missing foreign key to an existing table" branch: when both
/// tables already exist in the database but the foreign key constraint is absent.
/// </summary>
public class MigrationPlannerForeignKeyTests
{
    private readonly MigrationPlanner _sut = new();

    [Fact]
    public void GeneratePlan_ExistingTableMissingForeignKey_AddsForeignKeyStep()
    {
        var target = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", o => o
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int")
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var actual = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", o => o
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int")) // FK constraint missing
            .Build();

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().Contain(s =>
            s.Action == MigrationAction.AddForeignKey &&
            s.TableName == "Order" &&
            s.ForeignKey != null &&
            s.ForeignKey.TargetTable == "User");
    }

    [Fact]
    public void GeneratePlan_ExistingForeignKey_DoesNotAddDuplicate()
    {
        var target = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", o => o
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int")
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var actual = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", o => o
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("UserID", "int")
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany))
            .Build();

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().NotContain(s => s.Action == MigrationAction.AddForeignKey);
    }
}