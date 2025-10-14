using Compile.Shift.Model;

namespace Compile.Shift.Tests.Helpers;

/// <summary>
/// Fluent builder for creating MigrationPlan instances for testing.
/// Provides a clean, readable API for constructing migration plans with various step types.
/// </summary>
public class MigrationPlanBuilder
{
    private readonly MigrationPlan _plan = new MigrationPlan();

    private MigrationPlanBuilder() { }

    public static MigrationPlanBuilder Create() => new MigrationPlanBuilder();

    /// <summary>
    /// Adds a CreateTable step to the migration plan.
    /// </summary>
    public MigrationPlanBuilder WithCreateTableStep(string tableName, Action<CreateTableStepBuilder> configure)
    {
        var stepBuilder = new CreateTableStepBuilder(tableName);
        configure(stepBuilder);
        var step = stepBuilder.Build();
        _plan.Steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds an AddColumn step to the migration plan.
    /// </summary>
    public MigrationPlanBuilder WithAddColumnStep(string tableName, Action<AddColumnStepBuilder> configure)
    {
        var stepBuilder = new AddColumnStepBuilder(tableName);
        configure(stepBuilder);
        var step = stepBuilder.Build();
        _plan.Steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds an AlterColumn step to the migration plan.
    /// </summary>
    public MigrationPlanBuilder WithAlterColumnStep(string tableName, Action<AlterColumnStepBuilder> configure)
    {
        var stepBuilder = new AlterColumnStepBuilder(tableName);
        configure(stepBuilder);
        var step = stepBuilder.Build();
        _plan.Steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds an AddForeignKey step to the migration plan.
    /// </summary>
    public MigrationPlanBuilder WithAddForeignKeyStep(string tableName, string columnName, string targetTable, string targetColumnName, RelationshipType relationshipType)
    {
        _plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.AddForeignKey,
            TableName = tableName,
            ForeignKey = new ForeignKeyModel
            {
                ColumnName = columnName,
                TargetTable = targetTable,
                TargetColumnName = targetColumnName,
                RelationshipType = relationshipType
            }
        });
        return this;
    }

    /// <summary>
    /// Adds an AddIndex step to the migration plan.
    /// </summary>
    public MigrationPlanBuilder WithAddIndexStep(string tableName, Action<AddIndexStepBuilder> configure)
    {
        var stepBuilder = new AddIndexStepBuilder(tableName);
        configure(stepBuilder);
        var step = stepBuilder.Build();
        _plan.Steps.Add(step);
        return this;
    }

    /// <summary>
    /// Builds the final MigrationPlan.
    /// </summary>
    public MigrationPlan Build() => _plan;
}

/// <summary>
/// Fluent builder for creating CreateTable migration steps.
/// </summary>
public class CreateTableStepBuilder
{
    private readonly MigrationStep _step;

    public CreateTableStepBuilder(string tableName)
    {
        _step = new MigrationStep
        {
            Action = MigrationAction.CreateTable,
            TableName = tableName
        };
    }

    /// <summary>
    /// Adds a field to the table.
    /// </summary>
    public CreateTableStepBuilder WithField(string name, string type, Action<FieldModelBuilder>? configure = null)
    {
        var fieldBuilder = new FieldModelBuilder(name, type);
        configure?.Invoke(fieldBuilder);
        _step.Fields.Add(fieldBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds the final MigrationStep.
    /// </summary>
    public MigrationStep Build() => _step;
}

/// <summary>
/// Fluent builder for creating AddColumn migration steps.
/// </summary>
public class AddColumnStepBuilder
{
    private readonly MigrationStep _step;

    public AddColumnStepBuilder(string tableName)
    {
        _step = new MigrationStep
        {
            Action = MigrationAction.AddColumn,
            TableName = tableName
        };
    }

    /// <summary>
    /// Adds a field to the column addition.
    /// </summary>
    public AddColumnStepBuilder WithField(string name, string type, Action<FieldModelBuilder>? configure = null)
    {
        var fieldBuilder = new FieldModelBuilder(name, type);
        configure?.Invoke(fieldBuilder);
        _step.Fields.Add(fieldBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds the final MigrationStep.
    /// </summary>
    public MigrationStep Build() => _step;
}

/// <summary>
/// Fluent builder for creating AlterColumn migration steps.
/// </summary>
public class AlterColumnStepBuilder
{
    private readonly MigrationStep _step;

    public AlterColumnStepBuilder(string tableName)
    {
        _step = new MigrationStep
        {
            Action = MigrationAction.AlterColumn,
            TableName = tableName
        };
    }

    /// <summary>
    /// Adds a field to the column alteration.
    /// </summary>
    public AlterColumnStepBuilder WithField(string name, string type, Action<FieldModelBuilder>? configure = null)
    {
        var fieldBuilder = new FieldModelBuilder(name, type);
        configure?.Invoke(fieldBuilder);
        _step.Fields.Add(fieldBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds the final MigrationStep.
    /// </summary>
    public MigrationStep Build() => _step;
}

/// <summary>
/// Fluent builder for creating AddIndex migration steps.
/// </summary>
public class AddIndexStepBuilder
{
    private readonly MigrationStep _step;

    public AddIndexStepBuilder(string tableName)
    {
        _step = new MigrationStep
        {
            Action = MigrationAction.AddIndex,
            TableName = tableName
        };
    }

    /// <summary>
    /// Adds a single-column index.
    /// </summary>
    public AddIndexStepBuilder WithIndex(string columnName, bool isUnique = false)
    {
        _step.Index = new IndexModel
        {
            Fields = new List<string> { columnName },
            IsUnique = isUnique
        };
        return this;
    }

    /// <summary>
    /// Adds a multi-column index.
    /// </summary>
    public AddIndexStepBuilder WithIndex(IEnumerable<string> columnNames, bool isUnique = false)
    {
        _step.Index = new IndexModel
        {
            Fields = columnNames.ToList(),
            IsUnique = isUnique
        };
        return this;
    }

    /// <summary>
    /// Builds the final MigrationStep.
    /// </summary>
    public MigrationStep Build() => _step;
}
