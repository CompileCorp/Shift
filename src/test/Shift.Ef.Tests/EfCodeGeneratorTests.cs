using Compile.Shift.Ef;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static Compile.Shift.Ef.Tests.TestModels;

namespace Compile.Shift.Ef.Tests;

/// <summary>
/// Tests for <see cref="EfCodeGenerator"/>, the orchestrator that writes the generated
/// files to disk.
///
/// Intent: for each table write {Table}Entity.g.cs and {Table}EntityMap.g.cs, plus one
/// interface file ({InterfaceName}.g.cs) and one context file ({ContextClassName}.g.cs),
/// creating the output directory if necessary.
/// </summary>
public class EfCodeGeneratorTests : IDisposable
{
    private readonly string _outputDir;
    private readonly EfCodeGenerator _sut;

    public EfCodeGeneratorTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "ShiftEfTests", Guid.NewGuid().ToString("N"));
        _sut = new EfCodeGenerator { Logger = NullLogger.Instance };
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateEfCodeAsync_WithOptions_WritesEntityMapInterfaceAndContextFiles()
    {
        var model = Database(
            Table("User", new[] { Field("Id", "int", primaryKey: true, identity: true) }),
            Table("Order", new[] { Field("Id", "int", primaryKey: true) }));
        var options = new EfCodeGenerationOptions
        {
            NamespaceName = "My.Data",
            ContextClassName = "AppDbContext",
            InterfaceName = "IAppDbContext"
        };

        await _sut.GenerateEfCodeAsync(model, _outputDir, options);

        File.Exists(Path.Combine(_outputDir, "UserEntity.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "UserEntityMap.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "OrderEntity.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "OrderEntityMap.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "IAppDbContext.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "AppDbContext.g.cs")).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEfCodeAsync_WithOptions_FileContentReflectsModelAndNamespace()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));
        var options = new EfCodeGenerationOptions
        {
            NamespaceName = "My.Data",
            ContextClassName = "AppDbContext",
            InterfaceName = "IAppDbContext"
        };

        await _sut.GenerateEfCodeAsync(model, _outputDir, options);

        var entity = await File.ReadAllTextAsync(Path.Combine(_outputDir, "UserEntity.g.cs"));
        entity.Should().Contain("namespace My.Data;");
        entity.Should().Contain("public partial class UserEntity");

        var context = await File.ReadAllTextAsync(Path.Combine(_outputDir, "AppDbContext.g.cs"));
        context.Should().Contain("public partial class AppDbContext : DbContext, IAppDbContext");
    }

    [Fact]
    public async Task GenerateEfCodeAsync_DefaultOverload_UsesGeneratedDefaults()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        await _sut.GenerateEfCodeAsync(model, _outputDir);

        File.Exists(Path.Combine(_outputDir, "IGeneratedDbContext.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "GeneratedDbContext.g.cs")).Should().BeTrue();

        var entity = await File.ReadAllTextAsync(Path.Combine(_outputDir, "UserEntity.g.cs"));
        entity.Should().Contain("namespace Generated;");
    }

    [Fact]
    public async Task GenerateEfCodeAsync_CreatesOutputDirectoryWhenMissing()
    {
        Directory.Exists(_outputDir).Should().BeFalse();
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        await _sut.GenerateEfCodeAsync(model, _outputDir);

        Directory.Exists(_outputDir).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEfCodeAsync_EmptyModel_WritesOnlyContextAndInterface()
    {
        var model = Database();

        await _sut.GenerateEfCodeAsync(model, _outputDir);

        Directory.GetFiles(_outputDir, "*Entity.g.cs").Should().BeEmpty();
        File.Exists(Path.Combine(_outputDir, "IGeneratedDbContext.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "GeneratedDbContext.g.cs")).Should().BeTrue();
    }
}