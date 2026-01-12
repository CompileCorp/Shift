using Compile.Shift.Model;
using Compile.Shift.Helpers;

namespace Compile.Shift;

public class MigrationPlanner
{
    public MigrationPlan GeneratePlan(DatabaseModel targetModel, DatabaseModel actualModel)
    {
        var plan = new MigrationPlan();

        // 1. Create missing tables
        var missingTables = targetModel.Tables.Values
            .Where(t => !actualModel.Tables.Values.Any(at => at.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var table in missingTables)
        {
            plan.Steps.Add(new MigrationStep
            {
                Action = MigrationAction.CreateTable,
                TableName = table.Name,
                Fields = table.Fields
            });

            foreach (var foreignKey in table.ForeignKeys.Where(fk => targetModel.Tables.ContainsKey(fk.TargetTable)))
            {
                plan.Steps.Add(new MigrationStep
                {
                    Action = MigrationAction.AddForeignKey,
                    TableName = table.Name,
                    ForeignKey = foreignKey
                });
            }

            // Add indexes for new tables
            foreach (var index in table.Indexes)
            {
                plan.Steps.Add(new MigrationStep
                {
                    Action = MigrationAction.AddIndex,
                    TableName = table.Name,
                    Index = index,
                    Table = table
                });
            }
        }

        // 2. Add missing columns to existing tables
        foreach (var targetTable in targetModel.Tables.Values)
        {
            var actualTable = actualModel.Tables.Values
                .FirstOrDefault(at => at.Name.Equals(targetTable.Name, StringComparison.OrdinalIgnoreCase));

            if (actualTable != null)
            {
                var missingFields = targetTable.Fields
                    .Where(tf => !actualTable.Fields.Any(af => af.Name.Equals(tf.Name, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var field in missingFields)
                {
                    plan.Steps.Add(new MigrationStep
                    {
                        Action = MigrationAction.AddColumn,
                        TableName = targetTable.Name,
                        Fields = new List<FieldModel> { field }
                    });

                    //var missingFks = targetTable.ForeignKeys
                    //	.Where(x => x.ColumnName == field.Name)
                    //	.ToList();

                    //foreach (var foreignKey in missingFks)
                    //{
                    //	plan.Steps.Add(new MigrationStep
                    //	{
                    //		Action = MigrationAction.AddForeignKey,
                    //		TableName = targetTable.Name,
                    //		ForeignKey = foreignKey
                    //	});
                    //}
                }

                // Detect alter operations for size/precision changes (strings/binaries/decimals)
                foreach (var targetField in targetTable.Fields)
                {
                    var actualField = actualTable.Fields
                        .FirstOrDefault(af => af.Name.Equals(targetField.Name, StringComparison.OrdinalIgnoreCase));

                    if (actualField == null) continue;

                    var targetType = targetField.Type.ToLowerInvariant();
                    var actualType = actualField.Type.ToLowerInvariant();

                    // Handle string/binary size-bearing types only when base type matches
                    var isSizeType = targetType is "varchar" or "nvarchar" or "char" or "nchar" or "binary" or "varbinary";
                    if (isSizeType && string.Equals(targetType, actualType, StringComparison.OrdinalIgnoreCase))
                    {
                        int? targetPrecision = targetField.Precision;
                        int? actualPrecision = actualField.Precision;

                        bool sizeChanged = false;
                        // Any change including MAX <-> fixed and different fixed lengths
                        if (targetPrecision != actualPrecision)
                        {
                            sizeChanged = true;
                        }

                        if (sizeChanged)
                        {
                            plan.Steps.Add(new MigrationStep
                            {
                                Action = MigrationAction.AlterColumn,
                                TableName = targetTable.Name,
                                Fields = new List<FieldModel> { targetField }
                            });
                        }
                    }

                    // Handle decimal/numeric precision/scale changes (treat decimal and numeric as compatible)
                    bool targetIsDecimal = targetType is "decimal" or "numeric";
                    bool actualIsDecimal = actualType is "decimal" or "numeric";
                    if (targetIsDecimal && actualIsDecimal)
                    {
                        var precisionChanged = (targetField.Precision ?? 0) != (actualField.Precision ?? 0);
                        var scaleChanged = (targetField.Scale ?? 0) != (actualField.Scale ?? 0);
                        if (precisionChanged || scaleChanged)
                        {
                            plan.Steps.Add(new MigrationStep
                            {
                                Action = MigrationAction.AlterColumn,
                                TableName = targetTable.Name,
                                Fields = new List<FieldModel> { targetField }
                            });
                        }
                    }
                }
            }
        }

        // 3. Add missing foreign keys
        foreach (var targetTable in targetModel.Tables.Values)
        {
            var actualTable = actualModel.Tables.Values
                .FirstOrDefault(at => at.Name.Equals(targetTable.Name, StringComparison.OrdinalIgnoreCase));

            if (actualTable != null)
            {
                var missingForeignKeys = targetTable.ForeignKeys
                    .Where(tfk => targetModel.Tables.ContainsKey(tfk.TargetTable))
                    .Where(tfk => !actualTable.ForeignKeys.Any(afk =>
                        afk.TargetTable.Equals(tfk.TargetTable, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var foreignKey in missingForeignKeys)
                {
                    plan.Steps.Add(new MigrationStep
                    {
                        Action = MigrationAction.AddForeignKey,
                        TableName = targetTable.Name,
                        ForeignKey = foreignKey
                    });
                }
            }
        }

        // 4. Add missing indexes for existing tables (+ report extras)
        foreach (var targetTable in targetModel.Tables.Values)
        {
            var actualTable = actualModel.Tables.Values
                .FirstOrDefault(at => at.Name.Equals(targetTable.Name, StringComparison.OrdinalIgnoreCase));

            if (actualTable != null)
            {
                // Normalize target index fields to actual column names before comparing
                var normalizedTargetIndexes = targetTable.Indexes
                    .Select(ti => new
                    {
                        ResolvedFields = IndexFieldResolver.ResolveIndexFieldNames(ti.Fields, targetTable),
                        ti.IsUnique,
                        Index = ti
                    })
                    .ToList();

                // Add missing indexes (compare against actual using resolved field names)
                var missingIndexes = normalizedTargetIndexes
                    .Where(nt => !actualTable.Indexes.Any(ai =>
                        ai.IsUnique == nt.IsUnique &&
                        ai.Fields.SequenceEqual(nt.ResolvedFields, StringComparer.OrdinalIgnoreCase)))
                    .Select(nt => nt.Index)
                    .ToList();

                foreach (var index in missingIndexes)
                {
                    plan.Steps.Add(new MigrationStep
                    {
                        Action = MigrationAction.AddIndex,
                        TableName = targetTable.Name,
                        Index = index,
                        Table = targetTable
                    });
                }

                // Report extra indexes (indexes in actual but not in normalized target)
                var extraIndexes = actualTable.Indexes
                    .Where(ai => !normalizedTargetIndexes.Any(nt =>
                        nt.IsUnique == ai.IsUnique &&
                        ai.Fields.SequenceEqual(nt.ResolvedFields, StringComparer.OrdinalIgnoreCase)))
                    .Select(f => new ExtraIndexReport
                    {
                        TableName = actualTable.Name,
                        IsUnique = f.IsUnique,
                        Fields = f.Fields,
                    })
                    .ToList();

                plan.ExtrasInSqlServer.ExtraIndexes.AddRange(extraIndexes);
            }
        }

        /*

				// Report extras in SQL Server (not included in migration plan)
				plan.ExtrasInSqlServer = new ExtrasReport
				{
					ExtraTables = actualModel.Tables
						.Where(at => !targetModel.Tables.Any(tt => tt.Name.Equals(at.Name, StringComparison.OrdinalIgnoreCase)))
						.Select(t => t.Name)
						.ToList(),

					ExtraColumns = new List<ExtraColumnReport>()
				};

				foreach (var actualTable in actualModel.Tables)
				{
					var targetTable = targetModel.Tables
						.FirstOrDefault(tt => tt.Name.Equals(actualTable.Name, StringComparison.OrdinalIgnoreCase));

					if (targetTable != null)
					{
						var extraColumns = actualTable.Fields
							.Where(af => !targetTable.Fields.Any(tf => tf.Name.Equals(af.Name, StringComparison.OrdinalIgnoreCase)))
							.Select(f => new ExtraColumnReport
							{
								TableName = actualTable.Name,
								ColumnName = f.Name,
								DataType = f.Type
							})
							.ToList();

						plan.ExtrasInSqlServer.ExtraColumns.AddRange(extraColumns);
					}
				}
		*/
        return plan;
    }
}