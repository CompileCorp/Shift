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

    public DatabaseModel LoadFromAssembly(Assembly assembly)
    {
        //StreamLoader

        throw new NotImplementedException();
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