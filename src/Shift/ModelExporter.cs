using Compile.Shift.Model;
using Compile.Shift.Model.Helpers;
using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;
using System.Text;

namespace Compile.Shift;

public class ModelExporter
{
    public void ExportToDmd(DatabaseModel model, string outputDirectory, List<string>? mixinFiles = null)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Load and parse mixins if provided
        if (mixinFiles != null && mixinFiles.Any())
        {
            LoadMixins(model, mixinFiles);
        }
        else
        {
            Console.WriteLine("No mixins specified");
        }

        // Export each table as a separate DMD file
        foreach (var table in model.Tables.Values.OrderBy(x => x.Name))
        {
            var fileName = $"{table.Name}.dmd";
            var filePath = Path.Combine(outputDirectory, fileName);

            var dmdContent = GenerateDmdContent(table, model.Mixins.Values.ToList());
            File.WriteAllText(filePath, dmdContent);
        }
    }

    private void LoadMixins(DatabaseModel model, List<string> mixinFiles)
    {
        var dmdParser = new Parser();

        Console.WriteLine($"Loading mixins from {mixinFiles.Count} files");

        foreach (var mixinFile in mixinFiles)
        {
            if (File.Exists(mixinFile))
            {
                try
                {
                    var mixinContent = File.ReadAllText(mixinFile);
                    var mixinModel = dmdParser.ParseMixin(mixinContent);
                    if (mixinModel != null)
                    {
                        model.Mixins.Add(mixinModel.Name, mixinModel);
                        Console.WriteLine($"✅ Loaded mixin: {mixinModel.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Failed to load mixin {mixinFile}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"⚠️  Mixin file not found: {mixinFile}");
            }
        }
    }

    public string GenerateDmdContent(TableModel table, List<MixinModel> mixins)
    {
        var sb = new StringBuilder();

        // Apply mixins first - check if table contains all fields of any mixin
        var appliedMixins = new List<string>();
        var fieldsToExclude = new List<FieldModel>();
        // Attributes the mixin contributes. They are already re-emitted by the "with <Mixin>" header,
        // so emitting them inline as well would both duplicate them and break the round trip.
        var attributesToExclude = new HashSet<AttributeModel>();

        if (table.Mixins.Count == 0)
        {
            foreach (var mixin in mixins)
            {
                bool containsAll = ContainsAllMixinFields(table, mixin);
                if (containsAll)
                {
                    appliedMixins.Add(mixin.Name);
                    foreach (var mf in mixin.Fields)
                    {
                        //Console.WriteLine($"{mf.Name} {mf.Model} {mf.Type}");
                        fieldsToExclude.Add(mf);
                    }

                    foreach (var ma in mixin.Attributes)
                    {
                        attributesToExclude.Add(ma);
                    }
                }
            }
        }

        // Start model definition with mixins in header if any
        if (appliedMixins.Count > 0)
        {
            sb.Append($"model {table.Name} with {string.Join(", ", appliedMixins)} {{\n");
        }
        else
        {
            sb.Append($"model {table.Name} {{\n");
        }

        var fkGroups = table.ForeignKeys
            .GroupBy(fk => fk.TargetTable)
            .ToList();

        foreach (var fk in table.ForeignKeys.OrderBy(x => x.TargetTable).ThenBy(x => x.ColumnName))
        {
            if (fieldsToExclude.Any(x => x.Name == fk.ColumnName))
            {
                continue;
            }

            var fkAttributes = RenderTrailingAttributes(
                table.Fields.FirstOrDefault(f => string.Equals(f.Name, fk.ColumnName, StringComparison.OrdinalIgnoreCase))?.Attributes);

            var semanticName = ExtractSemanticName(fk.ColumnName, fk.TargetTable);
            var idField = $"{new string(fk.TargetTable.Where(char.IsLetter).ToArray())}ID";

            var nullableSuffix = fk.IsNullable ? "?" : "";
            bool needsAs = !string.IsNullOrWhiteSpace(semanticName) && !string.Equals(semanticName, idField, StringComparison.OrdinalIgnoreCase);

            if (fk.RelationshipType == RelationshipType.OneToMany)
            {
                if (needsAs)
                    sb.AppendLine($"  models {fk.TargetTable}{nullableSuffix} as {semanticName}{fkAttributes}");
                else
                    sb.AppendLine($"  models {fk.TargetTable}{nullableSuffix}{fkAttributes}");
            }
            else
            {
                if (needsAs)
                    sb.AppendLine($"  model {fk.TargetTable}{nullableSuffix} as {semanticName}{fkAttributes}");
                else
                    sb.AppendLine($"  model {fk.TargetTable}{nullableSuffix}{fkAttributes}");
            }
        }

        // Build a mapping from FK column names to model names
        var fkColumnToModel = table.ForeignKeys
            .Where(fk => !string.IsNullOrEmpty(fk.ColumnName))
            .ToDictionary(fk => fk.ColumnName, fk => fk.TargetTable, StringComparer.OrdinalIgnoreCase);

        var pkField = $"{new string(table.Name.Where(char.IsLetter).ToArray())}ID";

        // Add fields (excluding the auto-generated ID field, FK columns, and mixin fields) in alphabetical order
        var sortedFields = table.Fields
            .Where(f => f.Name != pkField &&
                       !fkColumnToModel.ContainsKey(f.Name)
                        && !fieldsToExclude.Any(x => x.Name == f.Name)
                       )
            .OrderBy(f => f.Name)
            .ToList();

        foreach (var field in sortedFields)
        {
            var isSupportedDataType = Vnum.TryFromCode<SqlFieldType>(field.Type, ignoreCase: true, out var sqlFieldType);

            if (!isSupportedDataType)
            {
                // Omit unsupported types (e.g., geometry)
                sb.AppendLine($"# {field.Type.ToLower()} {field.Name}");
                Console.WriteLine($"Skipping unsupported type: {table.Name} {field.Name} {field.Type}");
                continue;
            }

            string fieldType = DmdTypeHelper.GetDmdTypeString(field, sqlFieldType);
            sb.AppendLine($"  {fieldType}{(field.IsNullable ? "?" : "")} {field.Name}{RenderTrailingAttributes(field.Attributes)}");
        }

        // Determine PK and FK columns
        // Console.WriteLine($"{table.Name} PK: {pkField}");
        var fkFields = new HashSet<string>(table.ForeignKeys.Select(fk => fk.ColumnName), StringComparer.OrdinalIgnoreCase);
        var seenIndexes = new HashSet<string>();

        // Add only custom indexes (not PK or FK)
        foreach (var index in table.Indexes)
        {
            // Skip if this is just the PK
            if (index.Fields.Count == 1 && string.Equals(index.Fields[0], pkField, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip if all fields are FKs
            if (index.Fields.All(f => fkFields.Contains(f)))
                continue;

            // Skip if this index is a duplicate (same fields in same order)
            var indexKey = string.Join(",", index.Fields).ToLowerInvariant();
            if (!seenIndexes.Add(indexKey))
                continue;

            // Replace FK columns with model names if available
            var fields = string.Join(", ", index.Fields.Select(f => fkColumnToModel.TryGetValue(f, out var modelName) ? modelName : f));
            if (index.IsUnique)
            {
                sb.AppendLine($"  key ({fields})");
            }
            else
            {
                sb.AppendLine($"  index ({fields})");
            }
        }

        // Add attributes
        foreach (var attribute in table.Attributes.Where(a => !attributesToExclude.Contains(a)))
        {
            sb.AppendLine($"  {RenderAttribute(attribute)}");
        }

        // Close model definition
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Renders one plugin attribute back to DMD. A value is single-quoted only when it contains
    /// whitespace, so a bare value round trips unchanged.
    /// </summary>
    private static string RenderAttribute(AttributeModel attribute)
    {
        if (attribute.IsFlag)
        {
            return $"@{attribute.Name}";
        }

        var value = attribute.Value!;

        return value.Any(char.IsWhiteSpace)
            ? $"@{attribute.Name} '{value}'"
            : $"@{attribute.Name} {value}";
    }

    /// <summary>
    /// Renders field-level attributes as trailing tokens, preserving declaration order. Returns an
    /// empty string when there are none, so it can be interpolated unconditionally.
    /// </summary>
    private static string RenderTrailingAttributes(List<AttributeModel>? attributes)
    {
        if (attributes == null || attributes.Count == 0)
        {
            return string.Empty;
        }

        return " " + string.Join(" ", attributes.Select(RenderAttribute));
    }

    private bool ContainsAllMixinFields(TableModel table, MixinModel mixin)
    {
        var tableFieldNames = table.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tableForeignKeyTargets = table.ForeignKeys.Select(fk => fk.TargetTable).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mixinField in mixin.Fields)
        {
            var found = table.Fields.Where(x => x.Name == mixinField.Name).Any();
            if (!found && !mixinField.IsOptional)
            {
                return false;
            }
        }
        return true;
    }

    private string ExtractSemanticName(string columnName, string targetTable)
    {
        // Remove the target table name from the column name
        // e.g., "CreatedByUserID" -> "CreatedBy" (when targetTable is "User")
        // e.g., "LastModifiedByUserID" -> "LastModifiedBy" (when targetTable is "User")

        var suffix = targetTable + "ID";
        if (columnName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return columnName.Substring(0, columnName.Length - suffix.Length);
        }

        // If no "ID" suffix, try just the table name
        if (columnName.EndsWith(targetTable, StringComparison.OrdinalIgnoreCase))
        {
            return columnName.Substring(0, columnName.Length - targetTable.Length);
        }

        // Fallback: return the original column name
        return columnName;
    }
}