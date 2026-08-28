using Compile.Shift.Model;

namespace Compile.Shift.Dbml.Tests;

/// <summary>
/// Small builders for constructing model fixtures used across the DBML exporter tests.
/// </summary>
internal static class TestModels
{
    public static AttributeModel Attr(string name, string? value = null) => new(name, value);

    public static FieldModel Field(
        string name,
        string type,
        bool nullable = false,
        bool primaryKey = false,
        bool identity = false,
        int? precision = null,
        int? scale = null,
        params AttributeModel[] attributes) =>
        new()
        {
            Name = name,
            Type = type,
            IsNullable = nullable,
            IsPrimaryKey = primaryKey,
            IsIdentity = identity,
            Precision = precision,
            Scale = scale,
            Attributes = attributes.ToList()
        };

    public static ForeignKeyModel ForeignKey(
        string columnName,
        string targetTable,
        string targetColumnName,
        RelationshipType relationshipType = RelationshipType.OneToMany,
        bool nullable = false) =>
        new()
        {
            ColumnName = columnName,
            TargetTable = targetTable,
            TargetColumnName = targetColumnName,
            IsNullable = nullable,
            RelationshipType = relationshipType
        };

    public static IndexModel Index(bool unique, params string[] fields) =>
        new() { Fields = fields.ToList(), IsUnique = unique };

    public static TableModel Table(
        string name,
        IEnumerable<FieldModel>? fields = null,
        IEnumerable<ForeignKeyModel>? foreignKeys = null,
        IEnumerable<IndexModel>? indexes = null,
        IEnumerable<AttributeModel>? attributes = null) =>
        new()
        {
            Name = name,
            Fields = (fields ?? []).ToList(),
            ForeignKeys = (foreignKeys ?? []).ToList(),
            Indexes = (indexes ?? []).ToList(),
            Attributes = (attributes ?? []).ToList()
        };

    public static DatabaseModel Database(params TableModel[] tables)
    {
        var model = new DatabaseModel();

        foreach (var table in tables)
        {
            model.Tables[table.Name] = table;
        }

        return model;
    }
}