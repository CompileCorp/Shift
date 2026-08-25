using System.Diagnostics;
using Compile.Shift.Helpers;
using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Compile.Shift;

public class SqlMigrationPlanRunner
{
    private readonly string _connectionString;
    private readonly MigrationPlan _plan;
    private readonly string _schema;
    public required ILogger Logger { private get; init; }

    public SqlMigrationPlanRunner(string connectionString, MigrationPlan plan, string schema = "dbo")
    {
        _connectionString = connectionString;
        _plan = plan;
        _schema = schema;
    }

    public List<(MigrationStep Step, Exception Exception)> Run()
    {
        var failures = new List<(MigrationStep, Exception)>();

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        foreach (var step in _plan.Steps.OrderBy(x => x.Action))
        {
            var sql = "";
            try
            {
                var sqls = new List<string>();

                if (step.Action == MigrationAction.AddColumn)
                {
                    foreach (var field in step.Fields)
                    {
                        Logger.LogWarning($"{step.Action} {step.TableName} {field}");
                        sqls.AddRange(GenerateColumnSql(step.TableName, field));
                    }
                }
                else if (step.Action == MigrationAction.CreateTable)
                {
                    Logger.LogWarning($"{step.Action} {step.TableName}");
                    sqls.AddRange(GenerateCreateTableSql(step.TableName, step.Fields));
                }
                else if (step.Action == MigrationAction.AlterColumn)
                {
                    foreach (var field in step.Fields)
                    {
                        Logger.LogWarning($"{step.Action} {step.TableName} {field}");

                        var actualDataType = GetActualColumnDataType(connection, step.TableName, field.Name);

                        // A change of base type is rejected outright by SQL Server when any other
                        // object depends on the column, so it is checked before the data-loss probe.
                        // Only base-type changes are checked: widening an indexed string succeeds,
                        // and blocking it here would refuse alters that work today.
                        if (IsBaseTypeChange(actualDataType, field))
                        {
                            var blockers = GetAlterColumnBlockers(connection, step.TableName, field.Name);
                            if (blockers.Count > 0)
                            {
                                Logger.LogWarning(
                                    "Skipping ALTER COLUMN {table}.{column}: cannot convert {actualType} to {targetType} because {blockers} depend(s) on it. Drop the dependent object(s) and re-apply.",
                                    step.TableName, field.Name, actualDataType, field.Type, string.Join(", ", blockers));
                                continue;
                            }
                        }

                        // Safety check: skip alters that would cause data loss
                        if (IsAlterColumnPotentiallyUnsafe(connection, step.TableName, field, actualDataType))
                        {
                            Logger.LogWarning("Skipping ALTER COLUMN {table}.{column}: would cause data loss", step.TableName, field.Name);
                            continue;
                        }

                        sqls.AddRange(GenerateAlterColumnSql(step.TableName, field));
                    }
                }
                else if (step is { Action: MigrationAction.AddForeignKey, ForeignKey: not null })
                {
                    Logger.LogWarning($"{step.Action} {step.TableName} {step.ForeignKey.ColumnName}");
                    sqls.AddRange(CreateForeignKeySql(step.TableName, step.ForeignKey));
                    sqls.AddRange(GenerateIndexSql(step.TableName, new IndexModel()
                    {
                        Fields = [step.ForeignKey.ColumnName],
                        IsUnique = false,
                        Kind = IndexKind.NonClustered
                    }, step.Table));
                }
                else if (step is { Action: MigrationAction.AddIndex, Index: not null })
                {
                    Logger.LogWarning($"{step.Action} {step.TableName} {string.Join(",", step.Index.Fields)}");
                    sqls.AddRange(GenerateIndexSql(step.TableName, step.Index, step.Table));
                }

                foreach (var xsql in sqls)
                {
                    sql = xsql;
                    Logger.LogDebug(sql);
                    using var cmd = new SqlCommand(sql, connection);
                    cmd.CommandTimeout = 600;
                    cmd.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                Logger.LogError(ex, "{action} failed {sql}", step.Action.ToString(), sql);
                failures.Add((step, ex));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{action} failed", step.Action.ToString());
                failures.Add((step, ex));
            }
        }

        return failures;
    }

    internal IEnumerable<string> CreateForeignKeySql(string tableName, ForeignKeyModel foreignKey)
    {
        var fkName = $"FK_{tableName}_{foreignKey.ColumnName}";

        yield return $@"ALTER TABLE [{_schema}].[{tableName}] WITH NOCHECK ADD CONSTRAINT [{fkName}] FOREIGN KEY ([{foreignKey.ColumnName}])
		REFERENCES [{_schema}].[{foreignKey.TargetTable}]([{foreignKey.TargetColumnName}])";

        yield return $"ALTER TABLE [{_schema}].[{tableName}] CHECK CONSTRAINT [{fkName}]";
    }

    internal IEnumerable<string> GenerateCreateTableSql(string tableName, List<FieldModel> fields)
    {
        string? pkField = null;
        var tableColSql = new List<string>();

        foreach (var field in fields)
        {
            var typeSql =
                Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType)
                    ? SqlTypeHelper.GetSqlTypeString(field, sqlFieldType)
                    : SqlTypeHelper.GetUnknownSqlTypeString(field);

            var identitySql = field.IsIdentity ? " IDENTITY(1,1)" : string.Empty;
            var nullSql = field.IsNullable ? "NULL" : "NOT NULL";
            var colSql = $"[{field.Name}] {typeSql}{identitySql} {nullSql}";
            tableColSql.Add(colSql);
            if (field.IsPrimaryKey)
                pkField = field.Name;
        }
        var pkConstraint = pkField != null ? $",\n  CONSTRAINT [PK_{tableName}] PRIMARY KEY ([{pkField}])" : string.Empty;
        yield return $"CREATE TABLE [{_schema}].[{tableName}] (\n  {string.Join(",\n  ", tableColSql)}{pkConstraint}\n)";
    }

    internal IEnumerable<string> GenerateColumnSql(string tableName, FieldModel field)
    {
        var typeSql =
            Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType)
                ? SqlTypeHelper.GetSqlTypeString(field, sqlFieldType)
                : SqlTypeHelper.GetUnknownSqlTypeString(field);

        var nullSql = field.IsNullable ? "NULL" : "NOT NULL";
        var defaultSql = string.Empty;

        if (!field.IsNullable)
        {
            switch (typeSql.ToLowerInvariant())
            {
                case var t when t.StartsWith("int"):
                case var t2 when t2.StartsWith("bigint"):
                case var t3 when t3.StartsWith("smallint"):
                case var t4 when t4.StartsWith("tinyint"):
                case var t5 when t5.StartsWith("decimal"):
                case var t6 when t6.StartsWith("numeric"):
                case var t7 when t7.StartsWith("float"):
                case var t8 when t8.StartsWith("real"):
                    if (field.Name.EndsWith("ID", StringComparison.OrdinalIgnoreCase))
                        defaultSql = " DEFAULT 1";
                    else
                        defaultSql = " DEFAULT 0";
                    break;
                case var t when t.StartsWith("bit"):
                    defaultSql = " DEFAULT 0";
                    break;
                case var t when t.StartsWith("datetime"):
                case var t2 when t2.StartsWith("smalldatetime"):
                case var t3 when t3.StartsWith("date"):
                case var t4 when t4.StartsWith("datetime2"):
                case var t5 when t5.StartsWith("datetimeoffset"):
                    defaultSql = " DEFAULT GETDATE()";
                    break;
                case var t when t.StartsWith("char"):
                case var t2 when t2.StartsWith("nchar"):
                case var t3 when t3.StartsWith("varchar"):
                case var t4 when t4.StartsWith("nvarchar"):
                case var t5 when t5.StartsWith("text"):
                case var t6 when t6.StartsWith("ntext"):
                    defaultSql = " DEFAULT ''";
                    break;
                case var t when t.StartsWith("uniqueidentifier"):
                    defaultSql = " DEFAULT NEWID()";
                    break;
            }
        }

        yield return $"ALTER TABLE [{_schema}].[{tableName}] ADD [{field.Name}] {typeSql} {nullSql} {defaultSql}";

        if (field.IsNullable)
        {
            // We must drop the default constraint (if any) after adding the column
            // Find and drop the default constraint for this column
            yield return $@"
DECLARE @dfname nvarchar(128);
SELECT @dfname = df.name
FROM sys.default_constraints df
INNER JOIN sys.columns c ON df.parent_object_id = c.object_id AND df.parent_column_id = c.column_id
WHERE df.parent_object_id = OBJECT_ID('{_schema}.{tableName}') AND c.name = '{field.Name}';
IF @dfname IS NOT NULL EXEC('ALTER TABLE [{_schema}].[{tableName}] DROP CONSTRAINT [' + @dfname + ']');
";
        }
    }

    internal IEnumerable<string> GenerateAlterColumnSql(string tableName, FieldModel field)
    {
        var typeSql =
            Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType)
                ? SqlTypeHelper.GetSqlTypeString(field, sqlFieldType)
                : SqlTypeHelper.GetUnknownSqlTypeString(field);

        var nullSql = field.IsNullable ? "NULL" : "NOT NULL";

        yield return $@"
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = '{_schema}'
      AND TABLE_NAME = '{tableName}'
      AND COLUMN_NAME = '{field.Name}'
      AND (
          DATA_TYPE <> '{field.Type}'
          OR COALESCE(CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, 0) <> {field.Precision ?? -1}
          OR COALESCE(NUMERIC_SCALE, 0) <> {field.Scale ?? 0}
          OR IS_NULLABLE <> '{(field.IsNullable ? "YES" : "NO")}'
      )
)
BEGIN
    ALTER TABLE [{_schema}].[{tableName}] ALTER COLUMN [{field.Name}] {typeSql} {nullSql}
END";
    }

    /// <summary>
    /// True when the column's live base type differs from the type the plan wants, i.e. this alter
    /// is a conversion rather than a resize. False when the column does not exist, in which case
    /// the alter's own IF EXISTS guard makes it a no-op.
    /// </summary>
    private static bool IsBaseTypeChange(string? actualDataType, FieldModel field) =>
        actualDataType != null
        && !string.Equals(actualDataType, field.Type, StringComparison.OrdinalIgnoreCase);

    internal bool IsAlterColumnPotentiallyUnsafe(SqlConnection connection, string tableName, FieldModel field, string? actualDataType = null)
    {
        // Only guard for types where resizing/precision can cause truncation or rounding
        var baseType = field.Type.ToLowerInvariant();

        // Strings and binaries: if shrinking, ensure no existing value exceeds new limit
        if (baseType is "varchar" or "nvarchar" or "char" or "nchar" or "binary" or "varbinary")
        {
            if (!field.Precision.HasValue)
                return false; // nothing to check

            // Compute byte limit appropriately
            int targetBytes;
            bool isUnicode = baseType is "nvarchar" or "nchar";
            if (field.Precision == -1)
            {
                return false; // to MAX is never unsafe
            }

            // A change of base type is measured in rendered characters, not storage bytes:
            // DATALENGTH on an int is always 4 and would wave through a target too narrow to
            // hold the rendered value. SQL Server does not raise on that conversion — it stores
            // '*' in place of the number — so this probe is the only thing standing between a
            // too-narrow target and silent data loss. CHARACTER_MAXIMUM_LENGTH counts characters
            // for nvarchar and bytes for varchar, and a rendered integer is ASCII, so a character
            // count is the correct limit for both.
            var actualType = baseType is "varchar" or "nvarchar"
                ? actualDataType ?? GetActualColumnDataType(connection, tableName, field.Name)
                : null;
            if (actualType != null && SqlTypeConversion.IsSupportedInPlaceConversion(actualType, baseType))
            {
                var conversionSql = $"SELECT TOP 1 1 FROM [{_schema}].[{tableName}] WITH (READPAST) WHERE [{field.Name}] IS NOT NULL AND LEN(CONVERT(varchar(50), [{field.Name}])) > @limitChars";
                using var conversionCmd = new SqlCommand(conversionSql, connection);
                conversionCmd.Parameters.AddWithValue("@limitChars", field.Precision.Value);
                return conversionCmd.ExecuteScalar() != null;
            }

            targetBytes = isUnicode ? field.Precision.Value * 2 : field.Precision.Value;

            // For char/nchar use LEN to avoid fixed padding interference for equality
            string predicate;
            if (baseType is "char" or "nchar")
            {
                // LEN returns character count ignoring trailing spaces; use chars threshold
                predicate = $"LEN([{field.Name}]) > @limitChars";
            }
            else
            {
                predicate = $"DATALENGTH([{field.Name}]) > @limitBytes";
            }

            var sql = $"SELECT TOP 1 1 FROM [{_schema}].[{tableName}] WITH (READPAST) WHERE [{field.Name}] IS NOT NULL AND {predicate}";
            using var cmd = new SqlCommand(sql, connection);
            if (baseType is "char" or "nchar")
            {
                cmd.Parameters.AddWithValue("@limitChars", field.Precision!.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@limitBytes", targetBytes);
            }
            var result = cmd.ExecuteScalar();
            return result != null;
        }

        // Decimal/numeric: ensure values fit exactly in target precision/scale without rounding
        if (baseType is "decimal" or "numeric")
        {
            int precision = field.Precision ?? 18;
            int scale = field.Scale ?? 0;

            var sql = $"SELECT TOP 1 1 FROM [{_schema}].[{tableName}] WITH (READPAST) WHERE [{field.Name}] IS NOT NULL AND (TRY_CONVERT(decimal({precision},{scale}), [{field.Name}]) IS NULL OR TRY_CONVERT(decimal({precision},{scale}), [{field.Name}]) <> [{field.Name}])";
            using var cmd = new SqlCommand(sql, connection);
            var result = cmd.ExecuteScalar();
            return result != null;
        }

        return false;
    }

    /// <summary>
    /// Reads the base type the column currently has, so the safety probe can tell an existing
    /// string apart from a value that is only about to become one. Returns null when the column
    /// does not exist.
    /// </summary>
    private string? GetActualColumnDataType(SqlConnection connection, string tableName, string columnName)
    {
        const string sql = @"
SELECT DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table AND COLUMN_NAME = @column";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@schema", _schema);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@column", columnName);
        return cmd.ExecuteScalar() as string;
    }

    /// <summary>
    /// Lists the objects that would make SQL Server reject a change of this column's base type.
    /// Converting a column that anything else depends on fails with error 4922 (or 2749 for an
    /// identity column) rather than doing anything useful, so the alter is skipped and the
    /// dependency named instead of being attempted and failing.
    ///
    /// Every entry here was confirmed against SQL Server 2022 to block an int-to-varchar
    /// conversion. Auto-created statistics are excluded because SQL Server drops and recreates
    /// them itself; only explicitly created statistics block. Views that are not schema-bound do
    /// not block either.
    /// </summary>
    internal List<string> GetAlterColumnBlockers(SqlConnection connection, string tableName, string columnName)
    {
        const string sql = @"
DECLARE @objectId int = OBJECT_ID(QUOTENAME(@schema) + '.' + QUOTENAME(@table));
DECLARE @columnId int = COLUMNPROPERTY(@objectId, @column, 'ColumnId');

SELECT DISTINCT Blocker FROM (
    SELECT 'the IDENTITY property' AS Blocker
    FROM sys.columns
    WHERE object_id = @objectId AND column_id = @columnId AND is_identity = 1

    UNION ALL
    SELECT 'index [' + i.name + ']'
    FROM sys.index_columns ic
    JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    WHERE ic.object_id = @objectId AND ic.column_id = @columnId AND i.name IS NOT NULL

    UNION ALL
    SELECT 'foreign key [' + fk.name + ']'
    FROM sys.foreign_key_columns fkc
    JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
    WHERE (fkc.parent_object_id = @objectId AND fkc.parent_column_id = @columnId)
       OR (fkc.referenced_object_id = @objectId AND fkc.referenced_column_id = @columnId)

    UNION ALL
    SELECT 'default constraint [' + dc.name + ']'
    FROM sys.default_constraints dc
    WHERE dc.parent_object_id = @objectId AND dc.parent_column_id = @columnId

    UNION ALL
    SELECT 'check constraint [' + cc.name + ']'
    FROM sys.check_constraints cc
    JOIN sys.sql_expression_dependencies d ON d.referencing_id = cc.object_id
    WHERE d.referenced_id = @objectId AND d.referenced_minor_id = @columnId

    UNION ALL
    SELECT 'computed column [' + col.name + ']'
    FROM sys.sql_expression_dependencies d
    JOIN sys.columns col ON col.object_id = d.referencing_id AND col.column_id = d.referencing_minor_id
    WHERE d.referenced_id = @objectId AND d.referenced_minor_id = @columnId AND col.is_computed = 1

    UNION ALL
    SELECT 'statistics [' + s.name + ']'
    FROM sys.stats_columns sc
    JOIN sys.stats s ON s.object_id = sc.object_id AND s.stats_id = sc.stats_id
    WHERE sc.object_id = @objectId AND sc.column_id = @columnId
      AND s.auto_created = 0 AND s.user_created = 1

    UNION ALL
    SELECT 'schema-bound object [' + OBJECT_NAME(d.referencing_id) + ']'
    FROM sys.sql_expression_dependencies d
    WHERE d.referenced_id = @objectId AND d.referenced_minor_id = @columnId
      AND d.referencing_id <> @objectId
      AND OBJECTPROPERTY(d.referencing_id, 'IsSchemaBound') = 1
) blockers
ORDER BY Blocker";

        var blockers = new List<string>();

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@schema", _schema);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@column", columnName);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            blockers.Add(reader.GetString(0));
        }

        return blockers;
    }

    internal IEnumerable<string> GenerateIndexSql(string tableName, IndexModel index, TableModel? table = null)
    {
        // Resolve field names to actual column names
        var resolvedFields = IndexFieldResolver.ResolveIndexFieldNames(index.Fields, table);

        // Generate index name: IX/AK_TableName_Field1_Field2... (with 128-character limit and hashing)
        var indexName = IndexNameHelper.GenerateIndexName(index.IsAlternateKey, tableName, resolvedFields);

        // Generate column list: [Column1], [Column2]
        var columnList = string.Join(", ", resolvedFields.Select(f => $"[{f}]"));

        // Generate CREATE INDEX statement with IF NOT EXISTS to prevent duplicate index errors
        var uniqueKeyword = index.IsUnique ? "UNIQUE " : "";
        var kindKeyword = index.Kind switch
        {
            IndexKind.NonClustered => "NONCLUSTERED ",
            IndexKind.Clustered => "CLUSTERED ",
            _ => throw new NotImplementedException($"Index kind '{index.Kind}' is not supported.")
        };

        yield return
$@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{_schema}.{tableName}'))
BEGIN
    CREATE {uniqueKeyword}{kindKeyword}INDEX [{indexName}] ON [{_schema}].[{tableName}]({columnList})
END";
    }

}