using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// An FK is identified by its column and referenced table. Each test pairs a target with a database
/// that matches on the referenced table alone, and expects the missing FK to be planned.
/// </summary>
public class MigrationPlannerForeignKeyIdentityTests
{
    private readonly MigrationPlanner _sut = new();

    private static DatabaseModel AuditedTask(bool includeReviewerFk) => DatabaseModelBuilder.Create()
        .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
        .WithTable("Task", t =>
        {
            t.WithField("TaskID", "int", f => f.PrimaryKey().Identity())
                .WithField("AssignedUserID", "int")
                .WithField("ReviewerUserID", "int", f => f.Nullable())
                .WithForeignKey("AssignedUserID", "User", "UserID");
            if (includeReviewerFk)
                t.WithForeignKey("ReviewerUserID", "User", "UserID");
        })
        .Build();

    /// <summary>
    /// Two FKs from one table to the same target (the Auditable mixin's CreatedBy/LastModifiedBy
    /// pattern). The database has one of them; the other must still be planned. Matching FKs on
    /// TargetTable alone makes the existing one stand in for both.
    /// </summary>
    [Fact]
    public void GeneratePlan_SecondForeignKeyToSameTableMissing_AddsForeignKeyStep()
    {
        var target = AuditedTask(includeReviewerFk: true);
        var actual = AuditedTask(includeReviewerFk: false);

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().Contain(s =>
            s.Action == MigrationAction.AddForeignKey &&
            s.TableName == "Task" &&
            s.ForeignKey!.ColumnName == "ReviewerUserID");
    }

    /// <summary>
    /// The supporting-index check (269119b) only applies to FKs whose constraint already exists. The
    /// missing second FK must get its index from its AddForeignKey step, not as a bare AddIndex
    /// with no constraint behind it.
    /// </summary>
    [Fact]
    public void GeneratePlan_SecondForeignKeyToSameTableMissing_DoesNotPlanIndexWithoutConstraint()
    {
        var target = AuditedTask(includeReviewerFk: true);
        var actual = AuditedTask(includeReviewerFk: false);
        actual.Tables["Task"].Indexes.Add(new IndexModel { Fields = ["AssignedUserID"] });

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().ContainSingle(s => s.Action == MigrationAction.AddForeignKey)
            .Which.ForeignKey!.ColumnName.Should().Be("ReviewerUserID");
        plan.Steps.Should().NotContain(s => s.Action == MigrationAction.AddIndex);
    }

    /// <summary>
    /// The database has an FK to the right table but on a different column (e.g. the FK was renamed
    /// in the DSL). The target FK on the new column is not present and must be planned.
    /// </summary>
    [Fact]
    public void GeneratePlan_ForeignKeyOnDifferentColumnToSameTable_AddsForeignKeyStep()
    {
        var target = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", o => o
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("OwnerUserID", "int")
                .WithField("LegacyUserID", "int")
                .WithForeignKey("OwnerUserID", "User", "UserID"))
            .Build();

        var actual = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", o => o
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("OwnerUserID", "int")
                .WithField("LegacyUserID", "int")
                .WithForeignKey("LegacyUserID", "User", "UserID"))
            .Build();

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().Contain(s =>
            s.Action == MigrationAction.AddForeignKey && s.ForeignKey!.ColumnName == "OwnerUserID");
    }

}