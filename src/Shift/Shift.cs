using Compile.Shift.Model;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Compile.Shift;

public interface IShift
{
    Task<DatabaseModel> LoadFromAssembly(Assembly assembly, IEnumerable<string>? namespaces = null);
    Task<DatabaseModel> LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies, IEnumerable<string>? namespaces = null);
    Task<DatabaseModel> LoadFromPathAsync(IEnumerable<string> paths);
    Task<DatabaseModel> LoadFromSqlAsync(string connectionString, string schema = "dbo");
    Task<MigrationRunResult> ApplyToSqlAsync(DatabaseModel targetModel, string connectionString, string schema = "dbo");
}


public class Shift : IShift
{
    private const string ModelFileExtension = ".dmd";
    private const string MixinFileExtension = ".dmdx";

    private readonly Parser _parser = new Parser();

    public required ILogger Logger { private get; init; }

    public async Task<DatabaseModel> LoadFromAssembly(Assembly assembly, IEnumerable<string>? namespaces = null)
    {
        return await LoadFromAssembliesAsync(new[] { assembly }, namespaces);
    }

    public async Task<DatabaseModel> LoadFromAssembliesAsync(IEnumerable<Assembly> assemblies, IEnumerable<string>? namespaces = null)
    {
        var model = new DatabaseModel();

        // Process assemblies in order to respect priority
        foreach (var assembly in assemblies)
        {
            IEnumerable<string> resourceNames = assembly.GetManifestResourceNames();

            // Filter by namespace if provided
            if (namespaces != null)
            {
                var namespaceList = namespaces.ToList();
                if (namespaceList.Count > 0)
                {
                    resourceNames = resourceNames.Where(name => IsResourceInNamespace(name, namespaceList)).ToList();
                }
            }

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

        // Normalize FK column types to match referenced PK types
        NormalizeForeignKeyTypes(model);

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

        // Normalize FK column types to match referenced PK types
        NormalizeForeignKeyTypes(model);

        return model;
    }

    public async Task<DatabaseModel> LoadFromSqlAsync(string connectionString, string schema = "dbo")
    {
        var sqlLoader = new SqlServerLoader(connectionString) { Logger = Logger };
        var model = await sqlLoader.LoadDatabaseAsync(schema);
        return model;
    }

    public async Task<MigrationRunResult> ApplyToSqlAsync(DatabaseModel targetModel, string connectionString, string schema = "dbo")
    {
        var sourceModel = await LoadFromSqlAsync(connectionString, schema);
        var migrationPlanner = new MigrationPlanner { Logger = Logger };
        var plan = migrationPlanner.GeneratePlan(targetModel, sourceModel);
        var sql = new SqlMigrationPlanRunner(connectionString, plan, schema) { Logger = Logger };
        var result = sql.Run();

        // Changes the planner refused never became steps, so they are only visible here. Carrying
        // them onto the result means one object describes everything the model asked for that did
        // not happen, whichever stage declined it.
        result.Skipped.InsertRange(0, plan.Diagnostics);

        // Counted from what the runner actually executed, not from what the plan asked for. Steps
        // the runner skipped are still in plan.Steps, so counting those would have the log announce
        // "AlterColumn 1" for a column it left untouched - and report it right next to the warning
        // saying it did not.
        var effects = result.Applied
            .OrderBy(x => x.Action)
            .GroupBy(x => x.Action)
            .Select(x => (x.Key, x.Count()))
            .ToList();

        if (plan.Steps.Count == 0 && result.Skipped.Count == 0)
        {
            Logger.LogInformation("Already up-to date");
            return result;
        }

        // Steps that threw are reported here as well as by the runner: without this the apply
        // announces "Apply completed" whether or not every step failed.
        if (result.Failures.Count > 0)
        {
            Logger.LogError("Apply completed with {count} failed step(s)", result.Failures.Count);
            foreach (var (step, exception) in result.Failures)
            {
                Logger.LogError("{action} {table} failed: {message}", step.Action, step.TableName, exception.Message);
            }
        }
        else if (effects.Count > 0)
        {
            Logger.LogInformation("Apply completed");
        }
        else
        {
            // Nothing failed, but nothing was applied either - everything the model asked for was
            // refused by one stage or the other. Saying "Apply completed" here is what made a
            // fully skipped run read as a successful one.
            Logger.LogWarning("Apply made no changes");
        }

        // Skipped work is neither an application nor a failure, and reporting only the step counts
        // would present a run in which nothing changed as a run in which everything did.
        if (result.Skipped.Count > 0)
        {
            Logger.LogWarning("{count} column change(s) not applied", result.Skipped.Count);
            foreach (var diagnostic in result.Skipped)
            {
                Logger.LogWarning(
                    "{kind} {table}.{column}: {reason}",
                    diagnostic.Kind, diagnostic.TableName, diagnostic.ColumnName, diagnostic.Reason);
            }
        }

        foreach (var effect in effects)
        {
            Logger.LogInformation("{action} {count}", effect.Key, effect.Item2);
        }

        return result;
    }

    public void SaveToPathAsync()
    {
        throw new NotImplementedException();
    }

    private static bool IsResourceInNamespace(string resourceName, IEnumerable<string> namespaces)
    {
        foreach (var ns in namespaces)
        {
            // Check if resource name starts with namespace followed by a dot (namespace.file.dmd)
            // or matches exactly (namespace.dmd)
            if (resourceName.StartsWith(ns + ".", StringComparison.Ordinal) ||
                resourceName.Equals(ns, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void NormalizeForeignKeyTypes(DatabaseModel model)
    {
        // Build PK type map per table
        var primaryKeyTypeByTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in model.Tables.Values)
        {
            var primaryKeyField = table.Fields.FirstOrDefault(f => f.IsPrimaryKey)
                ?? table.Fields.FirstOrDefault(f => f.Name.Equals($"{table.Name}ID", StringComparison.OrdinalIgnoreCase));

            var pkType = primaryKeyField?.Type ?? "int";
            primaryKeyTypeByTable[table.Name] = pkType;
        }

        // Align FK field types to target PK types
        foreach (var table in model.Tables.Values)
        {
            foreach (var fk in table.ForeignKeys)
            {
                if (!primaryKeyTypeByTable.TryGetValue(fk.TargetTable, out var targetPkType))
                {
                    continue;
                }

                var fkField = table.Fields.FirstOrDefault(f => f.Name.Equals(fk.ColumnName, StringComparison.OrdinalIgnoreCase));
                if (fkField == null)
                {
                    continue;
                }

                fkField.Type = targetPkType;
                fkField.Precision = null;
                fkField.Scale = null;
            }
        }
    }
}