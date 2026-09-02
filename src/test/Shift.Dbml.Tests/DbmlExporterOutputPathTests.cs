using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static Compile.Shift.Dbml.Tests.TestModels;

namespace Compile.Shift.Dbml.Tests;

/// <summary>
/// Tests for how <see cref="DbmlExporter"/> resolves and writes its output file.
///
/// Intent: the output argument is either a .dbml file or a directory that receives a file with a
/// compile-time-constant name. Nothing an author controls — a table name, a column name, an
/// attribute value — may influence the path, and the resolved path is asserted to sit inside the
/// output root before anything is written.
/// </summary>
public class DbmlExporterOutputPathTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "ShiftDbmlTests", Guid.NewGuid().ToString("N"));
    private readonly DbmlExporter _sut = new() { Logger = NullLogger.Instance };

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_WithDirectory_WritesTheDefaultFileName()
    {
        var filePath = await _sut.ExportAsync(
            Database(Table("User", [Field("UserID", "int", primaryKey: true)])),
            _outputDir);

        filePath.Should().Be(Path.Combine(_outputDir, "model.dbml"));
        (await File.ReadAllTextAsync(filePath)).Should().Contain("Table User");
    }

    [Fact]
    public async Task ExportAsync_WithDbmlFilePath_UsesThatFile()
    {
        var requested = Path.Combine(_outputDir, "nested", "Diagram.DBML");

        var filePath = await _sut.ExportAsync(
            Database(Table("User", [Field("UserID", "int", primaryKey: true)])),
            requested);

        filePath.Should().Be(requested);
        File.Exists(requested).Should().BeTrue();
    }

    /// <summary>
    /// A table name is never a path component, so even a name built to traverse directories cannot
    /// place a file outside the output directory.
    /// </summary>
    [Fact]
    public async Task ExportAsync_WithHostileTableName_CannotEscapeTheOutputDirectory()
    {
        var model = Database(Table("../../../../tmp/pwned", [Field("Id", "int", primaryKey: true)]));

        var filePath = await _sut.ExportAsync(model, _outputDir);

        filePath.Should().Be(Path.Combine(_outputDir, "model.dbml"));
        Directory.GetFiles(_outputDir, "*", SearchOption.AllDirectories)
            .Should().BeEquivalentTo([filePath]);
        // The hostile name is quoted into the document rather than acted on.
        (await File.ReadAllTextAsync(filePath)).Should().Contain("Table \"../../../../tmp/pwned\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveOutputPath_WithBlankPath_Throws(string outputPath)
    {
        var act = () => DbmlExporter.ResolveOutputPath(outputPath);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveOutputPath_WithDirectory_TargetsTheConstantFileNameInsideIt()
    {
        var (directory, filePath) = DbmlExporter.ResolveOutputPath(_outputDir);

        directory.Should().Be(_outputDir);
        filePath.Should().Be(Path.Combine(_outputDir, "model.dbml"));
    }

    [Fact]
    public void ResolveOutputPath_WithDbmlFile_TargetsItsContainingDirectory()
    {
        var requested = Path.Combine(_outputDir, "diagram.dbml");

        var (directory, filePath) = DbmlExporter.ResolveOutputPath(requested);

        directory.Should().Be(_outputDir);
        filePath.Should().Be(requested);
    }

    [Fact]
    public void EnsureWithinDirectory_WithFileInsideTheDirectory_ReturnsTheFullPath()
    {
        var result = DbmlExporter.EnsureWithinDirectory(_outputDir, Path.Combine(_outputDir, "model.dbml"));

        result.Should().Be(Path.Combine(_outputDir, "model.dbml"));
    }

    [Fact]
    public void EnsureWithinDirectory_WithFileOutsideTheDirectory_Throws()
    {
        var escaping = Path.Combine(_outputDir, "..", "escaped.dbml");

        var act = () => DbmlExporter.EnsureWithinDirectory(_outputDir, escaping);

        act.Should().Throw<InvalidOperationException>().WithMessage("*outside the output directory*");
    }
}