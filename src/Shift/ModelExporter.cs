using Compile.Shift.Model;
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

            string fieldType = GetFieldTypeString(field, sqlFieldType);
            sb.AppendLine($"  {fieldType}{(field.IsNullable ? "?" : "")} {field.Name}");
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

    private string GetFieldTypeString(FieldModel fieldModel, SqlFieldType sqlFieldType)
    {
        string dmdTypeCode = sqlFieldType.DmdType.Code;

        // TEXT/NTEXT always use (max)
        if (sqlFieldType == SqlFieldType.TEXT || sqlFieldType == SqlFieldType.NTEXT)
            return $"{dmdTypeCode}(max)";

        // Check for MAX length marker
        if (sqlFieldType.SupportsMaxLength &&
            fieldModel.Precision.HasValue &&
            fieldModel.Precision == sqlFieldType.MaxLengthMarker)
        {
            return $"{dmdTypeCode}(max)";
        }

        // Handle precision/scale based on type
        return sqlFieldType.PrecisionType switch
        {
            PrecisionType.PrecisionOnlyAlwaysRequired =>
                $"{dmdTypeCode}({fieldModel.Precision ?? sqlFieldType.DefaultPrecision})",

            PrecisionType.PrecisionWithScaleAlwaysRequired =>
                FormatPrecisionAndScale(dmdTypeCode, fieldModel, sqlFieldType),

            PrecisionType.PrecisionOnlyOptional when fieldModel.Precision.HasValue =>
                $"{dmdTypeCode}({fieldModel.Precision.Value})",

            _ => dmdTypeCode
        };
    }

    private string FormatPrecisionAndScale(string dmdTypeCode, FieldModel fieldModel, SqlFieldType sqlFieldType)
    {
        var precision = fieldModel.Precision ?? sqlFieldType.DefaultPrecision ?? sqlFieldType.DmdType.DefaultPrecision;

        var scale = fieldModel.Scale ?? sqlFieldType.DefaultScale ?? sqlFieldType.DmdType.DefaultScale;

        return $"{dmdTypeCode}({precision},{scale})";
    }
}