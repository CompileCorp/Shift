using Compile.Shift.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Compile.Shift;

public class SqlServerLoader
{
    private readonly string _connectionString;

    public required ILogger Logger { private get; init; }

    public SqlServerLoader(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<DatabaseModel> LoadDatabaseAsync(string schema = "dbo")
    {
        var databaseModel = new DatabaseModel();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Load tables
        var tables = LoadTables(connection, schema);

        foreach (var table in tables.OrderBy(x => x.Name))
        {
            databaseModel.Tables.Add(table.Name, table);
        }

        // Load all columns for all tables in one query
        var allColumns = LoadAllColumns(connection, schema);

        // Load all foreign keys for all tables in one query
        var allForeignKeys = LoadAllForeignKeys(connection, schema);

        // Load all indexes for all tables in one query
        var allIndexes = LoadAllIndexes(connection, schema);

        // Assign columns, foreign keys, and indexes to each table
        foreach (var table in databaseModel.Tables.Values)
        {
            if (allColumns.TryGetValue(table.Name, out var columns))
            {
                table.Fields.AddRange(columns);
            }

            if (allForeignKeys.TryGetValue(table.Name, out var foreignKeys))
            {
                table.ForeignKeys.AddRange(foreignKeys);
            }

            if (allIndexes.TryGetValue(table.Name, out var indexes))
            {
                table.Indexes.AddRange(indexes);
            }

            Logger.LogDebug("Sql Loaded Model \n{table}", table);

            /*
						Console.WriteLine($"Loaded table: {table.Name}");
						foreach (var field in table.Fields)
						{
							Console.WriteLine($"  {field.Name}: {field.Type}");
						}
						foreach (var foreignKey in table.ForeignKeys)
						{
							Console.WriteLine($"  {foreignKey.ColumnName}: {foreignKey.TargetTable}");
						}
			*/
        }

        return databaseModel;
    }

    private List<TableModel> LoadTables(SqlConnection connection, string? schema)
    {
        var tables = new List<TableModel>();

        var query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE' 
            AND TABLE_NAME <> 'sysdiagrams'
            AND TABLE_CATALOG = @DatabaseName";

        if (!string.IsNullOrEmpty(schema))
        {
            query += " AND TABLE_SCHEMA = @Schema";
        }

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@DatabaseName", connection.Database);

        if (!string.IsNullOrEmpty(schema))
        {
            command.Parameters.AddWithValue("@Schema", schema);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader.GetString("TABLE_NAME");
            tables.Add(new TableModel { Name = tableName });
        }

        return tables;
    }

    private Dictionary<string, List<FieldModel>> LoadAllColumns(SqlConnection connection, string? schema)
    {
        var columnsByTable = new Dictionary<string, List<FieldModel>>(StringComparer.OrdinalIgnoreCase);

        var query = @"
            SELECT 
                TABLE_NAME,
                COLUMN_NAME,
                DATA_TYPE,
                IS_NULLABLE,
                CHARACTER_MAXIMUM_LENGTH,
                NUMERIC_PRECISION,
                NUMERIC_SCALE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_CATALOG = @DatabaseName";

        if (!string.IsNullOrEmpty(schema))
        {
            query += " AND TABLE_SCHEMA = @Schema";
        }

        query += " ORDER BY TABLE_NAME, ORDINAL_POSITION";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@DatabaseName", connection.Database);

        if (!string.IsNullOrEmpty(schema))
        {
            command.Parameters.AddWithValue("@Schema", schema);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader.GetString(reader.GetOrdinal("TABLE_NAME"));
            var columnName = reader.GetString(reader.GetOrdinal("COLUMN_NAME"));
            var dataType = reader.GetString(reader.GetOrdinal("DATA_TYPE"));
            var isNullable = reader.GetString(reader.GetOrdinal("IS_NULLABLE")) == "YES";

            int? length = null;
            int? precision = null;
            int? scale = null;

            if (!reader.IsDBNull(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH")))
            {
                length = reader.GetInt32(reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH"));
            }
            if (!reader.IsDBNull(reader.GetOrdinal("NUMERIC_PRECISION")))
            {
                precision = Convert.ToInt32(reader["NUMERIC_PRECISION"]);
            }
            if (!reader.IsDBNull(reader.GetOrdinal("NUMERIC_SCALE")))
            {
                scale = Convert.ToInt32(reader["NUMERIC_SCALE"]);
            }

            if (dataType == "int" || dataType == "bit" || dataType == "datetime" || dataType == "datetime2" || dataType == "date" || dataType == "time" || dataType == "bigint")
            {
                length = null;
                precision = null;
                scale = null;
            }

            var field = new FieldModel
            {
                Name = columnName,
                Type = dataType,
                IsNullable = isNullable,
                Precision = precision ?? length,
                Scale = scale
            };

            if (!columnsByTable.TryGetValue(tableName, out var list))
            {
                list = new List<FieldModel>();
                columnsByTable[tableName] = list;
            }
            list.Add(field);
        }

        return columnsByTable;
    }

    private Dictionary<string, List<ForeignKeyModel>> LoadAllForeignKeys(SqlConnection connection, string? schema)
    {
        var foreignKeysByTable = new Dictionary<string, List<ForeignKeyModel>>(StringComparer.OrdinalIgnoreCase);

        var query = @"
            SELECT 
                fk.CONSTRAINT_NAME,
                fk.TABLE_NAME,
                fk.COLUMN_NAME,
                pk.TABLE_NAME AS REFERENCED_TABLE_NAME,
                pk.COLUMN_NAME AS REFERENCED_COLUMN_NAME,
                rc.UPDATE_RULE,
                rc.DELETE_RULE,
                c.IS_NULLABLE
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE fk
            INNER JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc 
                ON fk.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE pk 
                ON rc.UNIQUE_CONSTRAINT_NAME = pk.CONSTRAINT_NAME
            INNER JOIN INFORMATION_SCHEMA.COLUMNS c
                ON fk.TABLE_NAME = c.TABLE_NAME 
                AND fk.COLUMN_NAME = c.COLUMN_NAME
                AND fk.TABLE_CATALOG = c.TABLE_CATALOG";

        if (!string.IsNullOrEmpty(schema))
        {
            query += " AND fk.TABLE_SCHEMA = c.TABLE_SCHEMA";
        }

        query += @"
            WHERE fk.TABLE_CATALOG = @DatabaseName";

        if (!string.IsNullOrEmpty(schema))
        {
            query += " AND fk.TABLE_SCHEMA = @Schema";
        }

        Logger.LogDebug(query);

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@DatabaseName", connection.Database);

        if (!string.IsNullOrEmpty(schema))
        {
            command.Parameters.AddWithValue("@Schema", schema);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var tableName = reader.GetString("TABLE_NAME");
            var columnName = reader.GetString("COLUMN_NAME");
            var referencedTable = reader.GetString("REFERENCED_TABLE_NAME");
            var isNullable = reader.GetString("IS_NULLABLE") == "YES";

            var foreignKey = new ForeignKeyModel
            {
                TargetTable = referencedTable,
                ColumnName = columnName,
                TargetColumnName = referencedTable + "ID",
                IsNullable = isNullable,
                RelationshipType = RelationshipType.OneToOne // Default assumption
            };

            if (!foreignKeysByTable.TryGetValue(tableName, out var list))
            {
                list = new List<ForeignKeyModel>();
                foreignKeysByTable[tableName] = list;
            }
            list.Add(foreignKey);
        }

        return foreignKeysByTable;
    }

    private Dictionary<string, List<IndexModel>> LoadAllIndexes(SqlConnection connection, string? schema)
    {
        var indexesByTable = new Dictionary<string, List<IndexModel>>(StringComparer.OrdinalIgnoreCase);

        var query = @"
            SELECT 
                t.name AS TableName,
                i.name AS IndexName,
                i.is_unique,
                c.name AS ColumnName
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE i.is_primary_key = 0";

        if (!string.IsNullOrEmpty(schema))
        {
            query += " AND s.name = @Schema";
        }

        query += " ORDER BY t.name, i.name, ic.key_ordinal";

        Logger.LogDebug(query);

        using var command = new SqlCommand(query, connection);

        if (!string.IsNullOrEmpty(schema))
        {
            command.Parameters.AddWithValue("@Schema", schema);
        }

        using var reader = command.ExecuteReader();
        var indexGroups = new Dictionary<string, Dictionary<string, IndexModel>>();

        while (reader.Read())
        {
            var tableName = reader.GetString("TableName");
            var indexName = reader.GetString("IndexName");
            var isUnique = reader.GetBoolean("is_unique");
            var columnName = reader.GetString("ColumnName");

            if (!indexGroups.TryGetValue(tableName, out var tableIndexes))
            {
                tableIndexes = new Dictionary<string, IndexModel>();
                indexGroups[tableName] = tableIndexes;
            }

            if (!tableIndexes.TryGetValue(indexName, out var index))
            {
                index = new IndexModel
                {
                    Fields = new List<string>(),
                    IsUnique = isUnique
                };
                tableIndexes[indexName] = index;
            }

            index.Fields.Add(columnName);
        }

        // Convert the nested dictionaries to the final format
        foreach (var tableGroup in indexGroups)
        {
            indexesByTable[tableGroup.Key] = tableGroup.Value.Values.ToList();
        }

        return indexesByTable;
    }
}