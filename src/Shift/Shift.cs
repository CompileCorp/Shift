using Compile.Shift.Model;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Compile.Shift;

public class Shift
{
    private const string ModelFileExtension = ".dmd";
    private const string MixinFileExtension = ".dmdx";

    private readonly Parser _parser = new Parser();

    public required ILogger Logger { private get; init; }

    public async Task<DatabaseModel> LoadFromAssembly(Assembly assembly)
    {
        return await LoadFromAssembliesAsync(new[] { assembly });
    }

    public async Task<DatabaseModel> LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies)
    {
        var model = new DatabaseModel();

        // Process assemblies in order to respect priority
        foreach (var assembly in assemblies)
        {
            var resourceNames = assembly.GetManifestResourceNames();
            
            // Load mixin files first (.dmdx)
            var mixinResources = resourceNames
                .Where(name => name.EndsWith(MixinFileExtension, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var mixinResource in mixinResources)
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(mixinResource);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var content = await reader.ReadToEndAsync();
                        var mixin = _parser.ParseMixin(content);
                        
                        // Only add if not already present (first assembly wins)
                        if (!model.Mixins.ContainsKey(mixin.Name))
                        {
                            model.Mixins.Add(mixin.Name, mixin);
                            Logger.LogDebug("Loaded mixin {MixinName} from assembly {AssemblyName}", mixin.Name, assembly.GetName().Name);
                        }
                        else
                        {
                            Logger.LogDebug("Skipped mixin {MixinName} from assembly {AssemblyName} (already loaded)", mixin.Name, assembly.GetName().Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to load mixin resource {ResourceName} from assembly {AssemblyName}", mixinResource, assembly.GetName().Name);
                }
            }

            // Load model files (.dmd)
            var modelResources = resourceNames
                .Where(name => name.EndsWith(ModelFileExtension, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var modelResource in modelResources)
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(modelResource);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var content = await reader.ReadToEndAsync();
                        
                        // Create a temporary model with current mixins to properly apply them
                        var tempModel = new DatabaseModel();
                        // Copy existing mixins to temp model
                        foreach (var mixin in model.Mixins)
                        {
                            tempModel.Mixins.Add(mixin.Key, mixin.Value);
                        }
                        
                        _parser.ParseTable(tempModel, content);
                        
                        // Only add tables that don't already exist (first assembly wins)
                        foreach (var table in tempModel.Tables)
                        {
                            if (!model.Tables.ContainsKey(table.Key))
                            {
                                model.Tables.Add(table.Key, table.Value);
                                Logger.LogDebug("Loaded table {TableName} from resource {ResourceName} from assembly {AssemblyName}", table.Key, modelResource, assembly.GetName().Name);
                            }
                            else
                            {
                                Logger.LogDebug("Skipped table {TableName} from resource {ResourceName} from assembly {AssemblyName} (already loaded)", table.Key, modelResource, assembly.GetName().Name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to load model resource {ResourceName} from assembly {AssemblyName}", modelResource, assembly.GetName().Name);
                }
            }
        }

        Logger.LogInformation("Loaded {MixinCount} mixins and {TableCount} tables from {AssemblyCount} assemblies", 
            model.Mixins.Count, model.Tables.Count, assemblies.Count());

        return model;
    }

    public async Task<DatabaseModel> LoadFromPathAsync(IEnumerable<string> paths)
    {
        //StreamLoader
        var model = new DatabaseModel();

        var directories = paths
            .Select(x => new DirectoryInfo(x))
            .ToList();

        var failed = false;
        foreach (var missingDirectory in directories.Where(x => !x.Exists))
        {
            Logger.LogError("Directory does not exist {Directory}", missingDirectory.FullName);
            failed = true;
        }

        if (failed)
        {
            return model;
        }

        var mixinFiles = directories
            .SelectMany(x => Directory.EnumerateFiles(x.FullName, $"*{MixinFileExtension}", SearchOption.AllDirectories))
            .AsEnumerable();

        await _parser.ParseMixinsAsync(model, mixinFiles);

        var modelFiles = directories
            .SelectMany(x => Directory.EnumerateFiles(x.FullName, $"*{ModelFileExtension}", SearchOption.AllDirectories))
            .AsEnumerable();

        await _parser.ParseModelsAsync(model, modelFiles);

        return model;
    }

    public async Task<DatabaseModel> LoadFromSqlAsync(string connectionString, string schema = "dbo")
    {
        var sqlLoader = new SqlServerLoader(connectionString) { Logger = Logger };
        var model = await sqlLoader.LoadDatabaseAsync(schema);
        return model;
    }

    public async Task ApplyToSqlAsync(DatabaseModel targetModel, string connectionString, string schema = "dbo")
    {
        var sourceModel = await LoadFromSqlAsync(connectionString, schema);
        var migrationPlanner = new MigrationPlanner();
        var plan = migrationPlanner.GeneratePlan(targetModel, sourceModel);
        var sql = new SqlMigrationPlanRunner(connectionString, plan) { Logger = Logger };
        sql.Run();

        var effects = plan.Steps
            .OrderBy(x => x.Action)
            .GroupBy(x => x.Action)
            .Select(x => (x.Key, x.Count()))
            .ToList();

        if (effects.Count > 0)
        {
            Logger.LogInformation("Apply completed");
            foreach (var effect in effects)
            {
                Logger.LogInformation("{action} {count}", effect.Key, effect.Item2);
            }
        }
        else
        {
            Logger.LogInformation("Already up-to date");
        }
    }

    public void SaveToPathAsync()
    {
        throw new NotImplementedException();
    }
}