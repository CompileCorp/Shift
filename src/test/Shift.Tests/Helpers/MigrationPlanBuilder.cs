using Compile.Shift.Model;

namespace Compile.Shift.Tests.Helpers;

/// <summary>
/// Fluent builder for creating MigrationPlan instances for testing.
/// Provides a flexible way to construct migration plans with various steps.
/// </summary>
public class MigrationPlanBuilder
{
    private readonly MigrationPlan _plan = new();

    /// <summary>
    /// Creates a new MigrationPlanBuilder instance.
    /// </summary>
    public static MigrationPlanBuilder Create() => new();

    /// <summary>
    /// Adds a CreateTable step to the plan.
    /// </summary>
    public MigrationPlanBuilder WithCreateTable(string tableName, Action<TableModelBuilder> configure)
    {
        var tableBuilder = new TableModelBuilder(tableName);
        configure(tableBuilder);
        var table = tableBuilder.Build();

        _plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.CreateTable,
            TableName = tableName,
            Fields = table.Fields
        });
        return this;
    }

    /// <summary>
    /// Adds an AddColumn step to the plan.
    /// </summary>
    public MigrationPlanBuilder WithAddColumn(string tableName, string fieldName, string fieldType, Action<FieldModelBuilder>? configure = null)
    {
        var fieldBuilder = new FieldModelBuilder(fieldName, fieldType);
        configure?.Invoke(fieldBuilder);
        var field = fieldBuilder.Build();

        _plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.AddColumn,
            TableName = tableName,
            Fields = new List<FieldModel> { field }
        });
        return this;
    }

    /// <summary>
    /// Adds an AlterColumn step to the plan.
    /// </summary>
    public MigrationPlanBuilder WithAlterColumn(string tableName, string fieldName, string fieldType, Action<FieldModelBuilder>? configure = null)
    {
        var fieldBuilder = new FieldModelBuilder(fieldName, fieldType);
        configure?.Invoke(fieldBuilder);
        var field = fieldBuilder.Build();

        _plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.AlterColumn,
            TableName = tableName,
            Fields = new List<FieldModel> { field }
        });
        return this;
    }

    /// <summary>
    /// Adds an AddForeignKey step to the plan.
    /// </summary>
    public MigrationPlanBuilder WithAddForeignKey(string tableName, string columnName, string targetTable, string targetColumn, RelationshipType relationshipType = RelationshipType.OneToMany)
    {
        _plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.AddForeignKey,
            TableName = tableName,
            ForeignKey = new ForeignKeyModel
            {
                ColumnName = columnName,
                TargetTable = targetTable,
                TargetColumnName = targetColumn,
                RelationshipType = relationshipType
            }
        });
        return this;
    }

    /// <summary>
    /// Adds an AddIndex step to the plan.
    /// </summary>
    public MigrationPlanBuilder WithAddIndex(string tableName, string indexName, string columnName, bool isUnique = false, IndexKind kind = IndexKind.NonClustered)
    {
        _plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.AddIndex,
            TableName = tableName,
            Index = new IndexModel
            {
                Fields = new List<string> { columnName },
                IsUnique = isUnique,
                Kind = kind
            }
        });
        return this;
    }

    /// <summary>
    /// Adds an AddIndex step to the plan with multiple columns.
    /// </summary>
    public MigrationPlanBuilder WithAddIndex(string tableName, string indexName, IEnumerable<string> columnNames, bool isUnique = false, IndexKind kind = IndexKind.NonClustered)
    {
        _plan.Steps.Add(new MigrationStep
        {
            Action = MigrationAction.AddIndex,
            TableName = tableName,
            Index = new IndexModel
            {
                Fields = columnNames.ToList(),
                IsUnique = isUnique,
                Kind = kind
            }
        });
        return this;
    }

    /// <summary>
    /// Builds the final MigrationPlan.
    /// </summary>
    public MigrationPlan Build() => _plan;
}