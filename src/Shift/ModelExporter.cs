using Compile.Shift.Model;
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

            var semanticName = ExtractSemanticName(fk.ColumnName, fk.TargetTable);
            var idField = $"{new string(fk.TargetTable.Where(char.IsLetter).ToArray())}ID";

            var nullableSuffix = fk.IsNullable ? "?" : "";
            bool needsAs = !string.IsNullOrWhiteSpace(semanticName) && !string.Equals(semanticName, idField, StringComparison.OrdinalIgnoreCase);

            if (fk.RelationshipType == RelationshipType.OneToMany)
            {
                if (needsAs)
                    sb.AppendLine($"  models {fk.TargetTable}{nullableSuffix} as {semanticName}");
                else
                    sb.AppendLine($"  models {fk.TargetTable}{nullableSuffix}");
            }
            else
            {
                if (needsAs)
                    sb.AppendLine($"  model {fk.TargetTable}{nullableSuffix} as {semanticName}");
                else
                    sb.AppendLine($"  model {fk.TargetTable}{nullableSuffix}");
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

        // Supported DMD field types (extend as needed)
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bool", "string", "astring", "long", "datetime", "int", "decimal", "ntext", "float", "achar", "char"
        };

        // consider making char = astring(1) and nchar = string(1)

        foreach (var field in sortedFields)
        {
            string fieldType = field.Type;
            // Map SQL types to simplified types
            switch (fieldType.ToLowerInvariant())
            {
                case "bit":
                    fieldType = "bool";
                    break;
                case "nvarchar":
                    fieldType = "string";
                    break;
                case "varchar":
                    fieldType = "astring";
                    break;
                case "bigint":
                    fieldType = "long";
                    break;
                case "text":
                    fieldType = "astring";
                    break;
                case "ntext":
                    fieldType = "string";
                    break;
                case "char":
                    fieldType = "astring";
                    field.Precision = 1;
                    break;
                case "nchar":
                    fieldType = "string";
                    field.Precision = 1;
                    break;
                case "money":
                    fieldType = "decimal";
                    field.Precision = 19;
                    field.Scale = 4;
                    break;
                case "smallmoney":
                    fieldType = "decimal";
                    field.Precision = 10;
                    field.Scale = 4;
                    break;
            }

            // Omit unsupported types (e.g., geometry)
            if (!supportedTypes.Contains(fieldType.ToLowerInvariant()))
            {
                sb.AppendLine($"# {fieldType} {field.Name}");
                Console.WriteLine($"Skipping unsupported type: {table.Name} {field.Name} {fieldType}");
                continue;
            }

            // Add length/precision/scale if not default
            if (field.Type.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) ||
                field.Type.Equals("varchar", StringComparison.OrdinalIgnoreCase) ||
                field.Type.Equals("char", StringComparison.OrdinalIgnoreCase) ||
                field.Type.Equals("nchar", StringComparison.OrdinalIgnoreCase))
            {
                // Default is 1 if not specified
                if (field.Precision.HasValue && field.Precision.Value != 1 && field.Precision.Value != -1)
                {
                    fieldType += $"({field.Precision.Value})";
                }
                else if (field.Precision == -1) // -1 means MAX
                {
                    fieldType += "(max)";
                }
            }
            else if (field.Type.Equals("decimal", StringComparison.OrdinalIgnoreCase) ||
                     field.Type.Equals("numeric", StringComparison.OrdinalIgnoreCase))
            {
                // Default is (18,0)
                if (field.Precision.HasValue && field.Precision.Value != 18 ||
                    field.Scale.HasValue && field.Scale.Value != 0)
                {
                    int precision = field.Precision ?? 18;
                    int scale = field.Scale ?? 0;
                    fieldType += $"({precision},{scale})";
                }
            }
            if (field.IsNullable)
            {
                fieldType += "?";
            }
            sb.AppendLine($"  {fieldType} {field.Name}");
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
        foreach (var attribute in table.Attributes)
        {
            sb.AppendLine($"  @{attribute.Key}");
        }

        // Close model definition
        sb.AppendLine("}");

        return sb.ToString();
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

    private IEnumerable<FieldModel> GetFieldsExcludingMixinFields(TableModel table, List<MixinModel> appliedMixins)
    {
        var mixinFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mixin in appliedMixins)
        {
            foreach (var mixinField in mixin.Fields)
            {
                // Handle optional fields (remove '!' prefix)
                var fieldName = mixinField.Name.StartsWith("!") ? mixinField.Name.Substring(1) : mixinField.Name;
                mixinFieldNames.Add(fieldName);
            }
        }

        return table.Fields.Where(f => !mixinFieldNames.Contains(f.Name));
    }
}