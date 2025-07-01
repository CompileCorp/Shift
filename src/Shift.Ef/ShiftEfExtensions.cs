using Compile.Shift.Model;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Ef;

public static class ShiftEfExtensions
{
    /// <summary>
    /// Generates Entity Framework code from a DatabaseModel loaded via Shift
    /// </summary>
    /// <param name="shift">The Shift instance</param>
    /// <param name="model">The loaded DatabaseModel</param>
    /// <param name="outputPath">The output directory for generated files</param>
    /// <param name="logger">Logger instance for output</param>
    /// <param name="options">Code generation options</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task GenerateEfCodeAsync(this Shift shift, DatabaseModel model, string outputPath, ILogger logger, EfCodeGenerationOptions options)
    {
        var generator = new EfCodeGenerator { Logger = logger };
        await generator.GenerateEfCodeAsync(model, outputPath, options);
    }

    /// <summary>
    /// Generates Entity Framework code from a DatabaseModel loaded via Shift
    /// </summary>
    /// <param name="shift">The Shift instance</param>
    /// <param name="model">The loaded DatabaseModel</param>
    /// <param name="outputPath">The output directory for generated files</param>
    /// <param name="logger">Logger instance for output</param>
    /// <param name="namespaceName">The namespace for generated classes</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task GenerateEfCodeAsync(this Shift shift, DatabaseModel model, string outputPath, ILogger logger, string namespaceName = "Generated")
    {
        var generator = new EfCodeGenerator { Logger = logger };
        await generator.GenerateEfCodeAsync(model, outputPath, namespaceName);
    }

    /// <summary>
    /// Loads a DatabaseModel from SQL Server and generates Entity Framework code
    /// </summary>
    /// <param name="shift">The Shift instance</param>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <param name="outputPath">The output directory for generated files</param>
    /// <param name="logger">Logger instance for output</param>
    /// <param name="options">Code generation options</param>
    /// <param name="schema">Database schema (default: "dbo")</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task GenerateEfCodeFromSqlAsync(this Shift shift, string connectionString, string outputPath, ILogger logger, EfCodeGenerationOptions options, string schema = "dbo")
    {
        var model = await shift.LoadFromSqlAsync(connectionString, schema);
        await shift.GenerateEfCodeAsync(model, outputPath, logger, options);
    }

    /// <summary>
    /// Loads a DatabaseModel from SQL Server and generates Entity Framework code
    /// </summary>
    /// <param name="shift">The Shift instance</param>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <param name="outputPath">The output directory for generated files</param>
    /// <param name="logger">Logger instance for output</param>
    /// <param name="namespaceName">The namespace for generated classes</param>
    /// <param name="schema">Database schema (default: "dbo")</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task GenerateEfCodeFromSqlAsync(this Shift shift, string connectionString, string outputPath, ILogger logger, string namespaceName = "Generated", string schema = "dbo")
    {
        var model = await shift.LoadFromSqlAsync(connectionString, schema);
        await shift.GenerateEfCodeAsync(model, outputPath, logger, namespaceName);
    }

    /// <summary>
    /// Loads a DatabaseModel from file paths and generates Entity Framework code
    /// </summary>
    /// <param name="shift">The Shift instance</param>
    /// <param name="paths">Paths to model files</param>
    /// <param name="outputPath">The output directory for generated files</param>
    /// <param name="logger">Logger instance for output</param>
    /// <param name="options">Code generation options</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task GenerateEfCodeFromPathAsync(this Shift shift, IEnumerable<string> paths, string outputPath, ILogger logger, EfCodeGenerationOptions options)
    {
        var model = await shift.LoadFromPathAsync(paths);
        await shift.GenerateEfCodeAsync(model, outputPath, logger, options);
    }

    /// <summary>
    /// Loads a DatabaseModel from file paths and generates Entity Framework code
    /// </summary>
    /// <param name="shift">The Shift instance</param>
    /// <param name="paths">Paths to model files</param>
    /// <param name="outputPath">The output directory for generated files</param>
    /// <param name="logger">Logger instance for output</param>
    /// <param name="namespaceName">The namespace for generated classes</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task GenerateEfCodeFromPathAsync(this Shift shift, IEnumerable<string> paths, string outputPath, ILogger logger, string namespaceName = "Generated")
    {
        var model = await shift.LoadFromPathAsync(paths);
        await shift.GenerateEfCodeAsync(model, outputPath, logger, namespaceName);
    }
}