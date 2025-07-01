using Compile.Shift.Model;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Ef;

public class EfCodeGenerator
{
    private readonly EntityGenerator _entityGenerator;
    private readonly DbContextGenerator _dbContextGenerator;
    private readonly EntityMapGenerator _entityMapGenerator;
    private readonly DbContextInterfaceGenerator _dbContextInterfaceGenerator;

    public required ILogger Logger { private get; init; }

    public EfCodeGenerator()
    {
        _entityGenerator = new EntityGenerator();
        _dbContextGenerator = new DbContextGenerator();
        _entityMapGenerator = new EntityMapGenerator();
        _dbContextInterfaceGenerator = new DbContextInterfaceGenerator();
    }

    public async Task GenerateEfCodeAsync(DatabaseModel model, string outputPath, EfCodeGenerationOptions options)
    {
        Logger.LogInformation("Starting Entity Framework code generation for {TableCount} tables", model.Tables.Count);

        // Ensure output directory exists
        Directory.CreateDirectory(outputPath);

        // Generate entity classes
        await GenerateEntitiesAsync(model, outputPath, options.NamespaceName);

        // Generate entity maps
        await GenerateEntityMapsAsync(model, outputPath, options.NamespaceName);

        // Generate DbContext interface
        await GenerateDbContextInterfaceAsync(model, outputPath, options);

        // Generate DbContext
        await GenerateDbContextAsync(model, outputPath, options);

        Logger.LogInformation("Entity Framework code generation completed");
    }

    public async Task GenerateEfCodeAsync(DatabaseModel model, string outputPath, string namespaceName = "Generated")
    {
        var options = new EfCodeGenerationOptions
        {
            NamespaceName = namespaceName,
            ContextClassName = "GeneratedDbContext",
            InterfaceName = "IGeneratedDbContext"
        };
        
        await GenerateEfCodeAsync(model, outputPath, options);
    }

    private async Task GenerateEntitiesAsync(DatabaseModel model, string outputPath, string namespaceName)
    {
        foreach (var table in model.Tables.Values)
        {
            var entityCode = _entityGenerator.GenerateEntity(table, namespaceName);
            var fileName = Path.Combine(outputPath, $"{table.Name}Entity.g.cs");
            await File.WriteAllTextAsync(fileName, entityCode);
            Logger.LogDebug("Generated entity class: {FileName}", fileName);
        }
    }

    private async Task GenerateEntityMapsAsync(DatabaseModel model, string outputPath, string namespaceName)
    {
        foreach (var table in model.Tables.Values)
        {
            var mapCode = _entityMapGenerator.GenerateEntityMap(table, namespaceName);
            var fileName = Path.Combine(outputPath, $"{table.Name}EntityMap.g.cs");
            await File.WriteAllTextAsync(fileName, mapCode);
            Logger.LogDebug("Generated entity map: {FileName}", fileName);
        }
    }

    private async Task GenerateDbContextInterfaceAsync(DatabaseModel model, string outputPath, EfCodeGenerationOptions options)
    {
        var interfaceCode = _dbContextInterfaceGenerator.GenerateDbContextInterface(model, options);
        var fileName = Path.Combine(outputPath, $"{options.InterfaceName}.g.cs");
        await File.WriteAllTextAsync(fileName, interfaceCode);
        Logger.LogDebug("Generated DbContext interface: {FileName}", fileName);
    }

    private async Task GenerateDbContextAsync(DatabaseModel model, string outputPath, EfCodeGenerationOptions options)
    {
        var contextCode = _dbContextGenerator.GenerateDbContext(model, options);
        var fileName = Path.Combine(outputPath, $"{options.ContextClassName}.g.cs");
        await File.WriteAllTextAsync(fileName, contextCode);
        Logger.LogDebug("Generated DbContext: {FileName}", fileName);
    }
}