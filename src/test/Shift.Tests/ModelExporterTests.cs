namespace Compile.Shift.Tests;

public class ModelExporterTests : IDisposable
{
    private readonly ModelExporter _exporter;
    private readonly string _testOutputDir;

    public ModelExporterTests()
    {
        _exporter = new ModelExporter();
        _testOutputDir = Path.Combine(Path.GetTempPath(), "DmdSystemTests");
        Directory.CreateDirectory(_testOutputDir);
    }
    public void Dispose()
    {
        if (Directory.Exists(_testOutputDir))
        {
            Directory.Delete(_testOutputDir, true);
        }
    }
}