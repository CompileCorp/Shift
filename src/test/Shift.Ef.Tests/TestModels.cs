using Compile.Shift.Model;

namespace Compile.Shift.Ef.Tests;

/// <summary>
/// Small builders for constructing model fixtures used across the generator tests.
/// </summary>
internal static class TestModels
{
    public static FieldModel Field(
        string name,
        string type,
        bool nullable = false,
        bool optional = false,
        bool primaryKey = false,
        bool identity = false,
        int? precision = null,
        int? scale = null) =>
        new()
        {
            Name = name,
            Type = type,
            IsNullable = nullable,
            IsOptional = optional,
            IsPrimaryKey = primaryKey,
            IsIdentity = identity,
            Precision = precision,
            Scale = scale
        };

    public static ForeignKeyModel ForeignKey(string columnName, string targetTable, bool nullable = false) =>
        new()
        {
            ColumnName = columnName,
            TargetTable = targetTable,
            TargetColumnName = "Id",
            IsNullable = nullable
        };

    public static IndexModel Index(bool unique, params string[] fields) =>
        new() { Fields = fields.ToList(), IsUnique = unique };

    public static TableModel Table(
        string name,
        IEnumerable<FieldModel>? fields = null,
        IEnumerable<ForeignKeyModel>? foreignKeys = null,
        IEnumerable<IndexModel>? indexes = null) =>
        new()
        {
            Name = name,
            Fields = (fields ?? Enumerable.Empty<FieldModel>()).ToList(),
            ForeignKeys = (foreignKeys ?? Enumerable.Empty<ForeignKeyModel>()).ToList(),
            Indexes = (indexes ?? Enumerable.Empty<IndexModel>()).ToList()
        };

    public static DatabaseModel Database(params TableModel[] tables)
    {
        var db = new DatabaseModel();
        foreach (var t in tables)
        {
            db.Tables[t.Name] = t;
        }
        return db;
    }
}