using Compile.Shift.Model;
using Compile.Shift.Tests.Helpers;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

public class MigrationPlannerDriftTests
{
    private readonly MigrationPlanner _sut = new();

    /// <summary>
    /// Table names are case-insensitive in SQL Server and everywhere else in the planner. An FK
    /// whose target differs only in case from the model's table must not be silently dropped.
    /// </summary>
    [Fact]
    public void GeneratePlan_ForeignKeyTargetCasingDiffersFromModel_StillAddsForeignKey()
    {
        var target = DatabaseModelBuilder.Create()
            .WithTable("User", t => t.WithField("UserID", "int", f => f.PrimaryKey().Identity()))
            .WithTable("Order", o => o
                .WithField("OrderID", "int", f => f.PrimaryKey().Identity())
                .WithField("userID", "int")
                .WithForeignKey("userID", "user", "userID"))
            .Build();

        var plan = _sut.GeneratePlan(target, new DatabaseModel());

        plan.Steps.Should().Contain(s => s.Action == MigrationAction.AddForeignKey && s.TableName == "Order");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GeneratePlan_NullabilityChangedOnly_AddsAlterColumnStep(bool targetNullable, bool actualNullable)
    {
        var target = UserWithNickname(50, targetNullable);
        var actual = UserWithNickname(50, actualNullable);

        var plan = _sut.GeneratePlan(target, actual);

        plan.Steps.Should().ContainSingle(s => s.Action == MigrationAction.AlterColumn)
            .Which.Fields.Should().ContainSingle(f => f.Name == "Nickname" && f.IsNullable == targetNullable);
    }

    /// <summary>
    /// A size change already carries the field's nullability, so a field that changes both must
    /// still produce a single alter.
    /// </summary>
    [Fact]
    public void GeneratePlan_SizeAndNullabilityChanged_AddsOneAlterColumnStep()
    {
        var plan = _sut.GeneratePlan(UserWithNickname(100, nullable: true), UserWithNickname(50, nullable: false));

        plan.Steps.Where(s => s.Action == MigrationAction.AlterColumn).Should().ContainSingle();
    }

    [Fact]
    public void GeneratePlan_NullabilityUnchanged_AddsNoStep()
    {
        var plan = _sut.GeneratePlan(UserWithNickname(50, nullable: true), UserWithNickname(50, nullable: true));

        plan.Steps.Should().BeEmpty();
    }

    private static DatabaseModel UserWithNickname(int length, bool nullable) => DatabaseModelBuilder.Create()
        .WithTable("User", t => t
            .WithField("UserID", "int", f => f.PrimaryKey().Identity())
            .WithField("Nickname", "nvarchar", f => f.Precision(length).Nullable(nullable)))
        .Build();
}