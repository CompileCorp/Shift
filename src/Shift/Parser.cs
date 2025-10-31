using Compile.Shift.Model;
using Compile.Shift.Model.Vnums;
using Compile.VnumEnumeration;
using System.Text.RegularExpressions;

namespace Compile.Shift;

public class Parser
{
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

                table.Name = parts[0].Trim();

                var containsWith = line.Contains(" with ");

                // model name type with model 4
                // model name type 2

                // Check for mixin usage
                if (containsWith)
                {
                    var withIndex = line.IndexOf(" with ", StringComparison.Ordinal);
                    withMixin = line.Substring(withIndex + 6).Split('{')[0].Trim();
                    table.Name = line.Substring(6, withIndex - 6).Trim();
                }

                var fieldModel = new FieldModel
                {
                    Name = $"{table.Name}ID",
                    Type = "int",
                    IsNullable = false,
                    IsPrimaryKey = true,
                    IsIdentity = true
                };

                if ((containsWith && parts.Length == 4) || (!containsWith && parts.Length == 2))
                {
                    fieldModel.Type = parts[1].Trim();
                }

                // Normalize DSL type to SQL type for primary key and adjust identity if needed
                fieldModel.Type =
                    Vnum.TryFromCode<DmdFieldType>(fieldModel.Type, ignoreCase: true, out var dmdFieldType)
                        ? dmdFieldType.SqlFieldType.Code
                        : fieldModel.Type;

                if (fieldModel.Type.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase))
                {
                    fieldModel.IsIdentity = false;
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

                    var fkField = new FieldModel
                    {
                        Name = $"{aliasName ?? ""}{nestedModelName}ID",
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
        int? precision = null;
        int? scale = null;

        if (parts[0].EndsWith("model") || parts[0].EndsWith("models"))
        {
            model = parts[0];
            type = parts[1];
            name = parts[1];

            if (name.EndsWith("?"))
            {
                name = name.Substring(0, name.Length - 1);
            }

            if (model.StartsWith("!"))
            {
                model = model[1..];
                isOptional = true;
            }

            if (parts.Length == 4)
            {
                alias = parts[3];
            }

            targetModel.ForeignKeys.Add(new ForeignKeyModel()
            {
                ColumnName = alias + name + "ID",
                TargetTable = name,
                TargetColumnName = name + "ID",
                RelationshipType = RelationshipType.OneToOne
            });

            name = alias + name + "ID";
            type = "int";
        }
        else
        {
            type = parts[0];
            name = parts[1];
        }

        var isNullable = false;
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