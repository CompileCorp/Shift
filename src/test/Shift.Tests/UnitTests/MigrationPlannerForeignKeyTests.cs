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

    /// <summary>
    /// Reproduces the reported defect: when the FK constraint already exists but its supporting
    /// index is missing (e.g. dropped, or never created), the planner used to skip creating the
    /// index entirely because it only checked whether the FK constraint (not the index) was
    /// present. The missing index must still be planned, independent of the FK's own presence.
    /// </summary>
    [Fact]
    public void GeneratePlan_ExistingForeignKeyWithoutSupportingIndex_AddsIndexStep()
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
            // Supporting index on UserID is missing in the actual database.
            .Build();

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().NotContain(s => s.Action == MigrationAction.AddForeignKey);
        plan.Steps.Should().Contain(s =>
            s.Action == MigrationAction.AddIndex &&
            s.TableName == "Order" &&
            s.Index != null &&
            s.Index.Fields.SequenceEqual(new[] { "UserID" }));
    }

    /// <summary>
    /// When the FK constraint already exists and its supporting index is already present too,
    /// nothing should be (re-)planned.
    /// </summary>
    [Fact]
    public void GeneratePlan_ExistingForeignKeyWithSupportingIndex_DoesNotAddIndexStep()
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
                .WithForeignKey("UserID", "User", "UserID", RelationshipType.OneToMany)
                .WithIndex("IX_Order_UserID", "UserID"))
            .Build();

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().NotContain(s => s.Action == MigrationAction.AddIndex);
    }
}