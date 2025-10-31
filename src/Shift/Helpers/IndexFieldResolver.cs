using Compile.Shift.Model;

namespace Compile.Shift.Helpers;

/// <summary>
/// Utility class for resolving index field names from model names to actual column names.
/// Handles the translation between DMD model names (used in index definitions) and 
/// actual SQL Server column names (used in foreign key relationships).
/// </summary>
public static class IndexFieldResolver
{
    /// <summary>
    /// Resolves index field names from model names to actual column names.
    /// For foreign key relationships, model names are translated to their corresponding
    /// foreign key column names (e.g., "ClientStatus" -> "ClientStatusID").
    /// </summary>
    /// <param name="fields">The list of field names from the index definition</param>
    /// <param name="table">The table model containing foreign key definitions</param>
    /// <returns>A new list with resolved field names</returns>
    public static List<string> ResolveIndexFieldNames(IEnumerable<string> fields, TableModel? table)
    {
        if (table == null)
        {
            return fields.ToList();
        }

        // Build a mapping from model names to foreign key column names
        var modelToColumnMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in table.ForeignKeys)
        {
            // Map by TargetTable (model name) to ColumnName (foreign key column name)
            // e.g., "ClientStatus" -> "ClientStatusID"
            modelToColumnMap[fk.TargetTable] = fk.ColumnName;
        }

        // Resolve each field name
        var resolvedFields = new List<string>();
        foreach (var field in fields)
        {
            if (modelToColumnMap.TryGetValue(field, out var resolvedName))
            {
                resolvedFields.Add(resolvedName);
            }
            else
            {
                resolvedFields.Add(field);
            }
        }

        return resolvedFields;
    }

}