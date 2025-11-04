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
    public required ILogger Logger { private get; init; }

    public SqlMigrationPlanRunner(string connectionString, MigrationPlan plan)
    {
        _connectionString = connectionString;
        _plan = plan;
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
                        Logger.LogInformation($"{step.Action} {step.TableName} {field}");
                        sqls.AddRange(GenerateColumnSql(step.TableName, field));
                    }
                }
                else if (step.Action == MigrationAction.CreateTable)
                {
                    Logger.LogInformation($"{step.Action} {step.TableName}");
                    sqls.AddRange(GenerateCreateTableSql(step.TableName, step.Fields));
                }
                else if (step.Action == MigrationAction.AlterColumn)
                {
                    foreach (var field in step.Fields)
                    {
                        Logger.LogInformation($"{step.Action} {step.TableName} {field}");
                        // Safety check: skip alters that would cause data loss
                        if (IsAlterColumnPotentiallyUnsafe(connection, step.TableName, field))
                        {
                            Logger.LogWarning("Skipping ALTER COLUMN {table}.{column}: would cause data loss", step.TableName, field.Name);
                            continue;
                        }

                        sqls.AddRange(GenerateAlterColumnSql(step.TableName, field));
                    }
                }
                else if (step is { Action: MigrationAction.AddForeignKey, ForeignKey: not null })
                {
                    Logger.LogInformation($"{step.Action} {step.TableName} {step.ForeignKey.ColumnName}");
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
                    Logger.LogInformation($"{step.Action} {step.TableName} {string.Join(",", step.Index.Fields)}");
                    sqls.AddRange(GenerateIndexSql(step.TableName, step.Index, step.Table));
                }

                foreach (var xsql in sqls)
                {
                    sql = xsql;
                    Logger.LogDebug(sql);
                    using var cmd = new SqlCommand(sql, connection);
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

    private IEnumerable<string> CreateForeignKeySql(string tableName, ForeignKeyModel foreignKey)
    {
        var fkName = $"FK_{tableName}_{foreignKey.ColumnName}";

        yield return $@"ALTER TABLE [dbo].[{tableName}] WITH NOCHECK ADD CONSTRAINT [{fkName}] FOREIGN KEY ([{foreignKey.ColumnName}])
		REFERENCES [dbo].[{foreignKey.TargetTable}]([{foreignKey.TargetColumnName}])";

        yield return $"ALTER TABLE[dbo].[{tableName}] CHECK CONSTRAINT[{fkName}]";
    }

    private IEnumerable<string> GenerateCreateTableSql(string tableName, List<FieldModel> fields)
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
        yield return $"CREATE TABLE [{tableName}] (\n  {string.Join(",\n  ", tableColSql)}{pkConstraint}\n)";
    }

    private IEnumerable<string> GenerateColumnSql(string tableName, FieldModel field)
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

        yield return $"ALTER TABLE [{tableName}] ADD [{field.Name}] {typeSql} {nullSql} {defaultSql}";

        if (field.IsNullable)
        {
            // We must drop the default constraint (if any) after adding the column
            // Find and drop the default constraint for this column
            yield return $@"
DECLARE @dfname nvarchar(128);
SELECT @dfname = df.name
FROM sys.default_constraints df
INNER JOIN sys.columns c ON df.parent_object_id = c.object_id AND df.parent_column_id = c.column_id
WHERE df.parent_object_id = OBJECT_ID('{tableName}') AND c.name = '{field.Name}';
IF @dfname IS NOT NULL EXEC('ALTER TABLE [{tableName}] DROP CONSTRAINT [' + @dfname + ']');
";
        }
    }

    private IEnumerable<string> GenerateAlterColumnSql(string tableName, FieldModel field)
    {
        var typeSql =
            Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType)
                ? SqlTypeHelper.GetSqlTypeString(field, sqlFieldType)
                : SqlTypeHelper.GetUnknownSqlTypeString(field);

        var nullSql = field.IsNullable ? "NULL" : "NOT NULL";
        yield return $"ALTER TABLE [dbo].[{tableName}] ALTER COLUMN [{field.Name}] {typeSql} {nullSql}";
    }

    private bool IsAlterColumnPotentiallyUnsafe(SqlConnection connection, string tableName, FieldModel field)
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

            var sql = $"SELECT TOP 1 1 FROM [dbo].[{tableName}] WITH (READPAST) WHERE [{field.Name}] IS NOT NULL AND {predicate}";
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

            var sql = $"SELECT TOP 1 1 FROM [dbo].[{tableName}] WITH (READPAST) WHERE [{field.Name}] IS NOT NULL AND (TRY_CONVERT(decimal({precision},{scale}), [{field.Name}]) IS NULL OR TRY_CONVERT(decimal({precision},{scale}), [{field.Name}]) <> [{field.Name}])";
            using var cmd = new SqlCommand(sql, connection);
            var result = cmd.ExecuteScalar();
            return result != null;
        }

        return false;
    }

    private IEnumerable<string> GenerateIndexSql(string tableName, IndexModel index, TableModel? table = null)
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
$@"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('dbo.{tableName}'))
BEGIN
    CREATE {uniqueKeyword}{kindKeyword}INDEX [{indexName}] ON [dbo].[{tableName}]({columnList})
END";
    }

}