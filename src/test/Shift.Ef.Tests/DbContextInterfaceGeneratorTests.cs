using Compile.Shift.Ef;
using FluentAssertions;
using static Compile.Shift.Ef.Tests.TestModels;

namespace Compile.Shift.Ef.Tests;

/// <summary>
/// Tests for <see cref="DbContextInterfaceGenerator"/>.
///
/// Intent: emit a partial interface exposing a DbSet property per table (no base type),
/// so the generated context can be depended upon abstractly.
/// </summary>
public class DbContextInterfaceGeneratorTests
{
    private readonly DbContextInterfaceGenerator _sut = new();

    private static EfCodeGenerationOptions Options() => new()
    {
        NamespaceName = "My.Data",
        InterfaceName = "IAppDbContext"
    };

    [Fact]
    public void GenerateDbContextInterface_EmitsInterfaceDeclaration()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContextInterface(model, Options());

        code.Should().Contain("namespace My.Data;");
        code.Should().Contain("using Microsoft.EntityFrameworkCore;");
        code.Should().Contain("public partial interface IAppDbContext");
    }

    [Fact]
    public void GenerateDbContextInterface_EmitsDbSetPerTable()
    {
        var model = Database(
            Table("User", new[] { Field("Id", "int", primaryKey: true) }),
            Table("Order", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContextInterface(model, Options());

        code.Should().Contain("DbSet<UserEntity> User { get; set; }");
        code.Should().Contain("DbSet<OrderEntity> Order { get; set; }");
    }

    [Fact]
    public void GenerateDbContextInterface_EmptyModel_ProducesEmptyInterfaceBody()
    {
        var model = Database();

        var code = _sut.GenerateDbContextInterface(model, Options());

        code.Should().Contain("public partial interface IAppDbContext");
        code.Should().NotContain("DbSet<");
    }
}