using Compile.Shift;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Ef.Examples;

/// <summary>
/// Example demonstrating how to use Shift.Ef to generate Entity Framework code
/// </summary>
public class EfGeneratorExample
{
    private readonly ILogger<EfGeneratorExample> _logger;

    public EfGeneratorExample(ILogger<EfGeneratorExample> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Example: Generate EF code from SQL Server database
    /// </summary>
    public async Task GenerateFromSqlServerAsync()
    {
        var connectionString = "Server=localhost;Database=Northwind;Integrated Security=true;TrustServerCertificate=true;";
        var outputPath = "./Generated/Entities";
        var namespaceName = "Northwind.Data.Generated";

        // Create Shift instance
        var shift = new Shift { Logger = _logger };

        try
        {
            // Generate EF code directly from SQL Server with custom options
            var options = new EfCodeGenerationOptions
            {
                NamespaceName = namespaceName,
                ContextClassName = "NorthwindDbContext",
                InterfaceName = "INorthwindDbContext"
            };

            await shift.GenerateEfCodeFromSqlAsync(
                connectionString: connectionString,
                outputPath: outputPath,
                logger: _logger,
                options: options
            );

            _logger.LogInformation("Entity Framework code generation completed successfully!");
            _logger.LogInformation("Generated files are located in: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Entity Framework code from SQL Server");
            throw;
        }
    }

    /// <summary>
    /// Example: Generate EF code from model definition files
    /// </summary>
    public async Task GenerateFromModelFilesAsync()
    {
        var modelPaths = new[] { "./Models", "./Data/Models" };
        var outputPath = "./Generated/Entities";
        var namespaceName = "MyApp.Data.Generated";

        // Create Shift instance
        var shift = new Shift { Logger = _logger };

        try
        {
            // Generate EF code from model files with custom options
            var options = new EfCodeGenerationOptions
            {
                NamespaceName = namespaceName,
                ContextClassName = "MyAppDbContext",
                InterfaceName = "IMyAppDbContext",
                BaseClassName = "MyCustomBaseContext" // Example of custom base class
            };

            await shift.GenerateEfCodeFromPathAsync(
                paths: modelPaths,
                outputPath: outputPath,
                logger: _logger,
                options: options
            );

            _logger.LogInformation("Entity Framework code generation from model files completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Entity Framework code from model files");
            throw;
        }
    }

    /// <summary>
    /// Example: Advanced usage with custom processing
    /// </summary>
    public async Task AdvancedGenerationAsync()
    {
        var connectionString = "Server=localhost;Database=MyDb;Integrated Security=true;TrustServerCertificate=true;";
        var outputPath = "./Generated/Entities";
        var namespaceName = "MyApp.Data.Generated";

        // Create Shift instance
        var shift = new Shift { Logger = _logger };

        try
        {
            // Load the model first
            var model = await shift.LoadFromSqlAsync(connectionString, schema: "dbo");

            _logger.LogInformation("Loaded {TableCount} tables from database", model.Tables.Count);

            // You can inspect or modify the model here if needed
            foreach (var table in model.Tables.Values)
            {
                _logger.LogDebug("Table: {TableName} with {FieldCount} fields",
                    table.Name, table.Fields.Count);
            }

            // Generate EF code from the loaded model with custom options
            var options = new EfCodeGenerationOptions
            {
                NamespaceName = namespaceName,
                ContextClassName = "CustomDbContext",
                InterfaceName = "ICustomDbContext"
            };

            await shift.GenerateEfCodeAsync(model, outputPath, _logger, options);

            _logger.LogInformation("Advanced Entity Framework code generation completed!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in advanced Entity Framework code generation");
            throw;
        }
    }

    /// <summary>
    /// Example: Generate code for multiple schemas or databases
    /// </summary>
    public async Task GenerateMultipleSourcesAsync()
    {
        var sources = new[]
        {
            (ConnectionString: "Server=localhost;Database=Db1;Integrated Security=true;TrustServerCertificate=true;",
             Schema: "dbo",
             Namespace: "App.Data.Db1"),
            (ConnectionString: "Server=localhost;Database=Db2;Integrated Security=true;TrustServerCertificate=true;",
             Schema: "sales",
             Namespace: "App.Data.Db2")
        };

        var shift = new Shift { Logger = _logger };

        foreach (var (connectionString, schema, namespaceName) in sources)
        {
            try
            {
                var outputPath = $"./Generated/{namespaceName.Split('.').Last()}";

                var options = new EfCodeGenerationOptions
                {
                    NamespaceName = namespaceName,
                    ContextClassName = $"{schema}DbContext",
                    InterfaceName = $"I{schema}DbContext"
                };

                await shift.GenerateEfCodeFromSqlAsync(
                    connectionString: connectionString,
                    outputPath: outputPath,
                    logger: _logger,
                    options: options,
                    schema: schema
                );

                _logger.LogInformation("Generated EF code for {Schema} schema in {Namespace}",
                    schema, namespaceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating EF code for schema {Schema}", schema);
            }
        }
    }
}