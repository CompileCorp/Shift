using Compile.Shift;
using Compile.Shift.Ef;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static Compile.Shift.Ef.Tests.TestModels;

namespace Compile.Shift.Ef.Tests;

/// <summary>
/// Tests for <see cref="ShiftEfExtensions"/>.
///
/// Intent: convenience wrappers on Shift that build an EfCodeGenerator and either generate
/// from an already-loaded model, or first load a model (from files) and then generate.
/// The SQL-loading wrappers require a live database and are exercised by the integration suite.
/// </summary>
public class ShiftEfExtensionsTests : IDisposable
{
    private readonly string _outputDir;
    private readonly string _modelDir;
    private readonly Shift _shift = new() { Logger = NullLogger.Instance };

    public ShiftEfExtensionsTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "ShiftEfExtTests", Guid.NewGuid().ToString("N"));
        _outputDir = Path.Combine(root, "out");
        _modelDir = Path.Combine(root, "models");
        Directory.CreateDirectory(_modelDir);
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_outputDir)!.FullName;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateEfCodeAsync_WithOptions_WritesFiles()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));
        var options = new EfCodeGenerationOptions { NamespaceName = "X", ContextClassName = "Ctx", InterfaceName = "ICtx" };

        await _shift.GenerateEfCodeAsync(model, _outputDir, NullLogger.Instance, options);

        File.Exists(Path.Combine(_outputDir, "UserEntity.g.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "Ctx.g.cs")).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEfCodeAsync_WithNamespace_WritesFilesUsingNamespace()
    {
        var model = Database(Table("User", new[] { Field("Id", "int", primaryKey: true) }));

        await _shift.GenerateEfCodeAsync(model, _outputDir, NullLogger.Instance, "Custom.Ns");

        var entity = await File.ReadAllTextAsync(Path.Combine(_outputDir, "UserEntity.g.cs"));
        entity.Should().Contain("namespace Custom.Ns;");
    }

    [Fact]
    public async Task GenerateEfCodeFromPathAsync_WithOptions_LoadsModelFromFilesThenGenerates()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_modelDir, "User.dmd"),
            "model User guid {\n  string(100) Username\n}\n");
        var options = new EfCodeGenerationOptions { NamespaceName = "X", ContextClassName = "Ctx", InterfaceName = "ICtx" };

        await _shift.GenerateEfCodeFromPathAsync(new[] { _modelDir }, _outputDir, NullLogger.Instance, options);

        var entity = await File.ReadAllTextAsync(Path.Combine(_outputDir, "UserEntity.g.cs"));
        entity.Should().Contain("public partial class UserEntity");
        entity.Should().Contain("Username");
    }

    [Fact]
    public async Task GenerateEfCodeFromPathAsync_WithNamespace_LoadsModelFromFilesThenGenerates()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_modelDir, "User.dmd"),
            "model User guid {\n  string(100) Username\n}\n");

        await _shift.GenerateEfCodeFromPathAsync(new[] { _modelDir }, _outputDir, NullLogger.Instance, "Custom.Ns");

        var entity = await File.ReadAllTextAsync(Path.Combine(_outputDir, "UserEntity.g.cs"));
        entity.Should().Contain("namespace Custom.Ns;");
    }
}