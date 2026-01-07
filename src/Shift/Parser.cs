using Compile.Shift.Model;
using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;
using System.Text.RegularExpressions;

namespace Compile.Shift;

public class Parser
{
    /// <summary>
    /// Checks if the given type name is a valid primary key type (simple types without precision).
    /// Valid PK types: guid, long, bool, float, datetime
    /// Invalid: int (default, must not be explicit), string types (require precision), decimal (requires precision)
    /// </summary>
    private static bool IsValidPrimaryKeyType(string typeName)
    {
        // Only allow simple types that don't require precision and are valid for primary keys
        // int is explicitly NOT allowed here because it's the default
        var validTypes = new[] { "guid", "long", "bool", "float", "datetime" };
        return validTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task ParseMixinsAsync(DatabaseModel model, IEnumerable<string> mixinFiles)
    {
        foreach (var mixinFile in mixinFiles)
        {
            var content = await File.ReadAllTextAsync(mixinFile);
            var mixin = ParseMixin(content);
            model.Mixins.Add(mixin.Name, mixin);
        }
    }

    public MixinModel ParseMixin(string content)
    {
        var model = new MixinModel();

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.StartsWith("mixin "))
            {
                model.Name = line.Substring(6).Split('{')[0].Trim();
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                ParseField(line, model);
            }

            if (line == "}")
                break;
        }

        return model;
    }

    public async Task ParseModelsAsync(DatabaseModel model, IEnumerable<string> modelFiles)
    {
        foreach (var modelFile in modelFiles)
        {
            var content = await File.ReadAllTextAsync(modelFile);
            ParseTable(model, content);
        }
    }

    public void ParseTable(DatabaseModel model, string content)
    {
        var table = new TableModel();
        var withMixin = "";
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.StartsWith("model ") && string.IsNullOrEmpty(table.Name))
            {
                var parts = line.Substring(6)
                    .Split(' ')
                    .Where(x => x != "{")
                    .ToArray();

                // Parse: model [Type] Name [with Mixin]
                string? primaryKeyType = null;
                string tableName;

                // Find if there's a "with" clause
                var withIndex = Array.IndexOf(parts, "with");
                var hasWithClause = withIndex != -1;

                if (hasWithClause)
                {
                    // Syntax: model [Type] Name with Mixin
                    if (withIndex == 2)
                    {
                        // model Type Name with Mixin
                        primaryKeyType = parts[0];

                        // Check if explicitly specifying 'int' (which is not allowed)
                        if (primaryKeyType.Equals("int", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception("Primary key type 'int' is the default and should not be explicitly specified. Use 'model Name with Mixin' instead of 'model int Name with Mixin'.");
                        }

                        tableName = parts[1];
                        withMixin = parts[3];
                    }
                    else if (withIndex == 1)
                    {
                        // model Name with Mixin
                        tableName = parts[0];
                        withMixin = parts[2];
                    }
                    else
                    {
                        throw new Exception($"Invalid model syntax: {line}");
                    }
                }
                else
                {
                    // Syntax: model [Type] Name
                    if (parts.Length == 2)
                    {
                        // Check if explicitly specifying 'int' (which is not allowed)
                        if (parts[0].Equals("int", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception("Primary key type 'int' is the default and should not be explicitly specified. Use 'model Name' instead of 'model int Name'.");
                        }

                        // Check if parts[0] is a valid simple type (types without precision)
                        if (IsValidPrimaryKeyType(parts[0]))
                        {
                            primaryKeyType = parts[0];
                            tableName = parts[1];
                        }
                        else
                        {
                            throw new Exception($"Unknown primary key type '{parts[0]}' or invalid syntax in: {line}");
                        }
                    }
                    else if (parts.Length == 1)
                    {
                        // model Name
                        tableName = parts[0];
                    }
                    else
                    {
                        throw new Exception($"Invalid model syntax: {line}");
                    }
                }

                table.Name = tableName;

                var fieldModel = new FieldModel
                {
                    Name = $"{table.Name}ID",
                    Type = "int",  // Default type
                    IsNullable = false,
                    IsPrimaryKey = true,
                    IsIdentity = true
                };

                // Override type if specified
                if (primaryKeyType != null)
                {
                    fieldModel.Type = primaryKeyType;

                    // Normalize DSL type to SQL type for primary key
                    fieldModel.Type =
                        Vnum.TryFromCode<DmdFieldType>(fieldModel.Type, ignoreCase: true, out var dmdFieldType)
                            ? dmdFieldType.SqlFieldType.Code
                            : fieldModel.Type;

                    // Special handling for guid: disable identity
                    if (fieldModel.Type.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase))
                    {
                        fieldModel.IsIdentity = false;
                    }
                }

                table.Fields.Add(fieldModel);

                model.Tables.Add(table.Name, table);
            }
            else if (line.StartsWith("extends ") && string.IsNullOrEmpty(table.Name))
            {
                var tableName = line.Substring(8).Split('{')[0].Trim();

                if (!model.Tables.TryGetValue(tableName, out table))
                {
                    throw new Exception($"Cannot extend {tableName}");
                }
            }
            else
            {
                if (line == "}")
                    break;

                // Handle ! prefix for model/foreign key lines
                var isOptionalModel = false;
                if (line.StartsWith("!model "))
                {
                    isOptionalModel = true;
                    line = line.Substring(1); // Remove the !
                }

                if (line.StartsWith("model ") || line.StartsWith("models "))
                {
                    var isMany = line.StartsWith("models ");
                    var prefix = isMany ? "models " : "model ";
                    var rest = line.Substring(prefix.Length).Split('{')[0].Trim();

                    var aliasParts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    bool isNullable = aliasParts[0].EndsWith("?");
                    string nestedModelName = aliasParts[0].TrimEnd('?');
                    string? aliasName = null;

                    if (aliasParts.Length == 3 && aliasParts[1] == "as")
                    {
                        isNullable = isNullable || aliasParts[2].EndsWith("?");
                        aliasName = aliasParts[2].TrimEnd('?');
                    }

                    var relationshipType = isMany ? RelationshipType.OneToMany : RelationshipType.OneToOne;

                    // Store the ! in the name for mixin matching logic, but not for table FKs
                    var mixinName = isOptionalModel ? "!" + nestedModelName : nestedModelName;

                    // If an alias is provided and ends with "ID", use it directly as the column name
                    // If an alias is provided but doesn't end with "ID", use {alias}{modelName}ID
                    // Otherwise, use the standard format: {ModelName}ID
                    var columnName = aliasName != null
                        ? (aliasName.EndsWith("ID", StringComparison.OrdinalIgnoreCase)
                            ? aliasName
                            : $"{aliasName}{nestedModelName}ID")
                        : $"{nestedModelName}ID";

                    var fkField = new FieldModel
                    {
                        Name = columnName,
                        Type = "int",
                        IsNullable = isNullable
                    };

                    var fk = new ForeignKeyModel
                    {
                        ColumnName = fkField.Name,
                        TargetTable = nestedModelName,
                        TargetColumnName = $"{nestedModelName}ID",
                        IsNullable = isNullable,
                        RelationshipType = relationshipType
                    };

                    table.ForeignKeys.Add(fk);

                    table.Fields.Add(fkField);
                }
                else if (line.StartsWith("key "))
                {
                    var indexModel = ParseKey(line);
                    if (indexModel != null)
                        table.Indexes.Add(indexModel);
                }

                else if (line.StartsWith("index "))
                {
                    var indexModel = ParseIndex(line);
                    if (indexModel != null)
                        table.Indexes.Add(indexModel);
                }
                else if (line.StartsWith("@"))
                {
                    var attribute = line.Substring(1).Trim();
                    table.Attributes[attribute] = true;
                }
                else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("//") && !line.StartsWith("#"))
                {
                    ParseField(line, table);
                }
            }
        }

        // Apply mixin if specified
        if (!string.IsNullOrEmpty(withMixin) && model.Mixins.TryGetValue(withMixin, out var mixin))
        {
            ApplyMixin(table, mixin);
        }

        // Check for @NoIdentity attribute and set IsIdentity = false on all primary key fields
        if (table.Attributes.ContainsKey("NoIdentity"))
        {
            foreach (var field in table.Fields.Where(f => f.IsPrimaryKey))
            {
                field.IsIdentity = false;
            }
        }
    }

    private FieldModel? ParseField(string line, IModel targetModel)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var type = "";
        var name = "";
        var model = "";
        var isOptional = false;
        var alias = "";
        var isNullable = false;
        int? precision = null;
        int? scale = null;

        if (parts[0].EndsWith("model") || parts[0].EndsWith("models"))
        {
            model = parts[0];
            type = parts[1];
            name = parts[1];

            isNullable = name.EndsWith("?");
            if (isNullable)
            {
                name = name.Substring(0, name.Length - 1);
            }

            if (model.StartsWith("!"))
            {
                model = model[1..];
                isOptional = true;
            }

            if (parts.Length == 4 && parts[2] == "as")
            {
                alias = parts[3];
                // Also check if alias has nullable marker
                if (alias.EndsWith("?"))
                {
                    isNullable = true;
                    alias = alias.Substring(0, alias.Length - 1);
                }
            }

            // If an alias is provided and ends with "ID", use it directly as the column name
            // If an alias is provided but doesn't end with "ID", use {alias}{modelName}ID
            // Otherwise, use the standard format: {ModelName}ID
            var columnName = !string.IsNullOrEmpty(alias)
                ? (alias.EndsWith("ID", StringComparison.OrdinalIgnoreCase)
                    ? alias
                    : $"{alias}{name}ID")
                : $"{name}ID";

            targetModel.ForeignKeys.Add(new ForeignKeyModel()
            {
                ColumnName = columnName,
                TargetTable = name,
                TargetColumnName = $"{name}ID",
                IsNullable = isNullable,
                RelationshipType = RelationshipType.OneToOne
            });

            name = columnName;
            type = "int";
        }
        else
        {
            type = parts[0];
            name = parts[1];
        }

        // Check for nullable type marker (if not already set in model relationship handling above)
        if (type.EndsWith("?"))
        {
            isNullable = true;
            type = type.Substring(0, type.Length - 1);
        }

        if (type.Contains("("))
        {
            var precisionScale = type.Substring(type.IndexOf("(") + 1);
            precisionScale = precisionScale.Substring(0, precisionScale.Length - 1);

            var psParts = precisionScale.Split(',');
            if (psParts.Length == 2)
            {
                precision = int.Parse(psParts[0]);
                scale = int.Parse(psParts[1]);
            }
            else if (psParts.Length == 1 && psParts[0] == "max")
            {
                precision = -1;
            }
            else if (psParts.Length == 1)
            {
                precision = int.Parse(psParts[0]);
            }

            type = type.Substring(0, type.IndexOf("("));
        }

        var field = new FieldModel
        {
            Name = name,
            Type = type,
            IsNullable = isNullable,
            IsOptional = isOptional,
            Precision = precision,
            Scale = scale
        };

        field.Type =
            Vnum.TryFromCode<DmdFieldType>(field.Type, ignoreCase: true, out var dmdFieldType)
                ? dmdFieldType.SqlFieldType.Code
                : field.Type;

        targetModel.Fields.Add(field);

        return field;
    }

    private IndexModel? ParseKey(string line)
    {
        var match = Regex.Match(line, @"key\s*\(([^)]+)\)");
        if (!match.Success) return null;

        var fields = match.Groups[1].Value.Split(',').Select(f => f.Trim()).ToList();

        return new IndexModel
        {
            Fields = fields,
            IsUnique = true,
            IsAlternateKey = true,
            Kind = IndexKind.NonClustered
        };
    }

    private IndexModel? ParseIndex(string line)
    {
        var match = Regex.Match(line, @"index\s*\(([^)]+)\)");
        if (!match.Success) return null;

        var fields = match.Groups[1].Value.Split(',').Select(f => f.Trim()).ToList();
        var isUnique = line.Contains("@unique");

        return new IndexModel
        {
            Fields = fields,
            IsUnique = isUnique,
            IsAlternateKey = false,
            Kind = IndexKind.NonClustered
        };
    }

    private void ApplyMixin(TableModel table, MixinModel mixin)
    {
        table.Mixins.Add(mixin.Name);

        // Add mixin fields
        foreach (var field in mixin.Fields)
        {
            table.Fields.Add(new FieldModel
            {
                Name = field.Name,
                Type = field.Type,
                IsNullable = field.IsNullable,
                IsOptional = field.IsOptional,
                Precision = field.Precision,
                Scale = field.Scale,
                IsPrimaryKey = field.IsPrimaryKey,
                IsIdentity = field.IsIdentity
            });
        }

        foreach (var fk in mixin.ForeignKeys)
        {
            table.ForeignKeys.Add(new ForeignKeyModel
            {
                TargetTable = fk.TargetTable,
                ColumnName = fk.ColumnName,
                TargetColumnName = fk.TargetColumnName,
                IsNullable = fk.IsNullable,
                RelationshipType = fk.RelationshipType
            });
        }
    }
}