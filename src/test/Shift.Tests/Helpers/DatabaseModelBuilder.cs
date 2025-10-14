using Compile.Shift.Model;

namespace Compile.Shift.Tests.Helpers;

/// <summary>
/// Fluent builder for creating DatabaseModel instances for testing.
/// Provides a flexible way to construct test data with various configurations.
/// </summary>
public class DatabaseModelBuilder
{
    private readonly DatabaseModel _model = new();

    /// <summary>
    /// Creates a new DatabaseModelBuilder instance.
    /// </summary>
    public static DatabaseModelBuilder Create() => new();

    /// <summary>
    /// Adds a table to the model.
    /// </summary>
    public DatabaseModelBuilder WithTable(string name, Action<TableModelBuilder> configure)
    {
        var tableBuilder = new TableModelBuilder(name);
        configure(tableBuilder);
        _model.Tables.Add(name, tableBuilder.Build());
        return this;
    }

    /// <summary>
    /// Adds a mixin to the model.
    /// </summary>
    public DatabaseModelBuilder WithMixin(string name, Action<MixinModelBuilder> configure)
    {
        var mixinBuilder = new MixinModelBuilder(name);
        configure(mixinBuilder);
        _model.Mixins.Add(name, mixinBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds the final DatabaseModel.
    /// </summary>
    public DatabaseModel Build() => _model;
}

/// <summary>
/// Fluent builder for creating TableModel instances.
/// </summary>
public class TableModelBuilder
{
    private readonly TableModel _table;

    public TableModelBuilder(string name)
    {
        _table = new TableModel { Name = name };
    }

    /// <summary>
    /// Adds a field to the table.
    /// </summary>
    public TableModelBuilder WithField(string name, string type, Action<FieldModelBuilder>? configure = null)
    {
        var fieldBuilder = new FieldModelBuilder(name, type);
        configure?.Invoke(fieldBuilder);
        _table.Fields.Add(fieldBuilder.Build());
        return this;
    }

    /// <summary>
    /// Adds a foreign key to the table.
    /// </summary>
    public TableModelBuilder WithForeignKey(string columnName, string targetTable, string targetColumn, RelationshipType relationshipType = RelationshipType.OneToMany)
    {
        _table.ForeignKeys.Add(new ForeignKeyModel
        {
            ColumnName = columnName,
            TargetTable = targetTable,
            TargetColumnName = targetColumn,
            RelationshipType = relationshipType
        });
        return this;
    }

    /// <summary>
    /// Adds an index to the table.
    /// </summary>
    public TableModelBuilder WithIndex(string name, string columnName, bool isUnique = false)
    {
        _table.Indexes.Add(new IndexModel
        {
            Fields = new List<string> { columnName },
            IsUnique = isUnique
        });
        return this;
    }

    /// <summary>
    /// Adds a multi-column index to the table.
    /// </summary>
    public TableModelBuilder WithIndex(string name, IEnumerable<string> columnNames, bool isUnique = false)
    {
        _table.Indexes.Add(new IndexModel
        {
            Fields = columnNames.ToList(),
            IsUnique = isUnique
        });
        return this;
    }

    /// <summary>
    /// Adds an attribute to the table.
    /// </summary>
    public TableModelBuilder WithAttribute(string key, bool value)
    {
        _table.Attributes.Add(key, value);
        return this;
    }

    /// <summary>
    /// Builds the final TableModel.
    /// </summary>
    public TableModel Build() => _table;
}

/// <summary>
/// Fluent builder for creating FieldModel instances.
/// </summary>
public class FieldModelBuilder
{
    private readonly FieldModel _field;

    public FieldModelBuilder(string name, string type)
    {
        _field = new FieldModel { Name = name, Type = type };
    }

    /// <summary>
    /// Sets the field as nullable.
    /// </summary>
    public FieldModelBuilder Nullable(bool nullable = true)
    {
        _field.IsNullable = nullable;
        return this;
    }

    /// <summary>
    /// Sets the field as primary key.
    /// </summary>
    public FieldModelBuilder PrimaryKey(bool isPrimaryKey = true)
    {
        _field.IsPrimaryKey = isPrimaryKey;
        return this;
    }

    /// <summary>
    /// Sets the field as identity.
    /// </summary>
    public FieldModelBuilder Identity(bool isIdentity = true)
    {
        _field.IsIdentity = isIdentity;
        return this;
    }

    /// <summary>
    /// Sets precision and scale for the field.
    /// </summary>
    public FieldModelBuilder Precision(int precision, int? scale = null)
    {
        _field.Precision = precision;
        if (scale.HasValue)
            _field.Scale = scale.Value;
        return this;
    }

    /// <summary>
    /// Adds an attribute to the field.
    /// Note: FieldModel doesn't support attributes in the current implementation.
    /// </summary>
    public FieldModelBuilder WithAttribute(string key, string value)
    {
        // FieldModel doesn't have Attributes property in current implementation
        // This method is kept for API compatibility but does nothing
        return this;
    }

    /// <summary>
    /// Builds the final FieldModel.
    /// </summary>
    public FieldModel Build() => _field;
}

/// <summary>
/// Fluent builder for creating MixinModel instances.
/// </summary>
public class MixinModelBuilder
{
    private readonly MixinModel _mixin;

    public MixinModelBuilder(string name)
    {
        _mixin = new MixinModel { Name = name };
    }

    /// <summary>
    /// Adds a field to the mixin.
    /// </summary>
    public MixinModelBuilder WithField(string name, string type, Action<FieldModelBuilder>? configure = null)
    {
        var fieldBuilder = new FieldModelBuilder(name, type);
        configure?.Invoke(fieldBuilder);
        _mixin.Fields.Add(fieldBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds the final MixinModel.
    /// </summary>
    public MixinModel Build() => _mixin;
}
