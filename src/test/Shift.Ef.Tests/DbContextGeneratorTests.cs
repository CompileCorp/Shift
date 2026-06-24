using Compile.Shift.Ef;
using FluentAssertions;
using static Compile.Shift.Ef.Tests.TestModels;

namespace Compile.Shift.Ef.Tests;

/// <summary>
/// Tests for <see cref="DbContextGenerator"/>.
///
/// Intent: emit a partial DbContext that derives from a base class (DbContext by default,
/// or a configured base) and the generated interface, exposes a DbSet per table, applies one
/// entity configuration per table in OnModelCreating, and provides an OnConfiguring override.
/// </summary>
public class DbContextGeneratorTests
{
    private readonly DbContextGenerator _sut = new();

    private static EfCodeGenerationOptions Options(string? baseClass = null) => new()
    {
        NamespaceName = "My.Data",
        ContextClassName = "AppDbContext",
        InterfaceName = "IAppDbContext",
        BaseClassName = baseClass
    };

    [Fact]
    public void GenerateDbContext_DefaultBase_DerivesFromDbContextAndInterface()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContext(model, Options());

        code.Should().Contain("namespace My.Data;");
        code.Should().Contain("public partial class AppDbContext : DbContext, IAppDbContext");
    }

    [Fact]
    public void GenerateDbContext_CustomBaseClass_DerivesFromIt()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContext(model, Options(baseClass: "MyBaseContext"));

        code.Should().Contain("public partial class AppDbContext : MyBaseContext, IAppDbContext");
    }

    [Fact]
    public void GenerateDbContext_EmitsBothConstructors()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContext(model, Options());

        code.Should().Contain("public AppDbContext()");
        code.Should().Contain("public AppDbContext(DbContextOptions<AppDbContext> options)");
        code.Should().Contain(": base(options)");
    }

    [Fact]
    public void GenerateDbContext_EmitsDbSetPerTable()
    {
        var model = Database(
            Table("User", new[] { Field("Id", "int", primaryKey: true) }),
            Table("Order", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContext(model, Options());

        code.Should().Contain("public virtual DbSet<UserEntity> User { get; set; }");
        code.Should().Contain("public virtual DbSet<OrderEntity> Order { get; set; }");
    }

    [Fact]
    public void GenerateDbContext_AppliesConfigurationPerTableInOnModelCreating()
    {
        var model = Database(
            Table("User", new[] { Field("Id", "int", primaryKey: true) }),
            Table("Order", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContext(model, Options());

        code.Should().Contain("protected override void OnModelCreating(ModelBuilder modelBuilder)");
        code.Should().Contain("base.OnModelCreating(modelBuilder);");
        code.Should().Contain("modelBuilder.ApplyConfiguration(new UserEntityMap());");
        code.Should().Contain("modelBuilder.ApplyConfiguration(new OrderEntityMap());");
    }

    [Fact]
    public void GenerateDbContext_EmitsOnConfiguringOverride()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        var code = _sut.GenerateDbContext(model, Options());

        code.Should().Contain("protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)");
        code.Should().Contain("if (!optionsBuilder.IsConfigured)");
        code.Should().Contain("base.OnConfiguring(optionsBuilder);");
    }
}