using Compile.Shift.Model;
using Compile.Shift.Helpers;
using Microsoft.Extensions.Logging;

namespace Compile.Shift;

public class MigrationPlanner
{
    public ILogger? Logger { get; init; }

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
                // and for the narrow set of base-type changes that can be applied in place.
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
                        // Normalize: treat null and -1 as equivalent (both mean MAX)
                        int? targetPrecision = targetField.Precision == -1 ? null : targetField.Precision;
                        int? actualPrecision = actualField.Precision == -1 ? null : actualField.Precision;

                        bool sizeChanged = targetPrecision != actualPrecision;

                        if (sizeChanged)
                        {
                            Logger?.LogWarning(
                                "AlterColumn {Table}.{Column}: target precision {TargetPrecision} != actual precision {ActualPrecision}",
                                targetTable.Name, targetField.Name, targetPrecision, actualPrecision);

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

                    // Handle base-type changes. Only conversions on the SqlTypeConversion allow-list
                    // are migrated; every other change is reported so the drift stops being silent.
                    if (!string.Equals(targetType, actualType, StringComparison.OrdinalIgnoreCase)
                        && !(targetIsDecimal && actualIsDecimal))
                    {
                        if (!SqlTypeConversion.IsSupportedInPlaceConversion(actualType, targetType, out var maxRenderedWidth))
                        {
                            // A target that is exactly Shift's own round-trip of the actual type
                            // (text -> varchar(max), money -> decimal(19,4)) is not drift, so it is
                            // not worth reporting. Precision and scale are part of that comparison:
                            // text -> varchar(50) is a real change of intent and is still reported.
                            if (!SqlTypeConversion.IsRoundTripEquivalent(actualField, targetField))
                            {
                                Report(plan, new MigrationDiagnostic
                                {
                                    Kind = MigrationDiagnosticKind.UnsupportedTypeChange,
                                    TableName = targetTable.Name,
                                    ColumnName = targetField.Name,
                                    ActualType = actualType,
                                    TargetType = targetType,
                                    Reason = $"{actualType} cannot be converted to {targetType} in place. The column is left unchanged."
                                });
                            }
                        }
                        // A target too narrow to hold every value the source type can represent is
                        // refused here rather than planned and left to the runner's live-data probe.
                        // Deferring would make migratability depend on what happens to be stored, so
                        // the same model would apply on one database and be skipped on another; and
                        // the probe cannot be trusted to carry that weight, because SQL Server does
                        // not raise on a too-narrow integer conversion - it stores '*' - so any row
                        // the probe misses is destroyed silently. Precision -1 means MAX, which fits.
                        else if (targetField.Precision is int targetWidth && targetWidth != -1 && targetWidth < maxRenderedWidth)
                        {
                            Report(plan, new MigrationDiagnostic
                            {
                                Kind = MigrationDiagnosticKind.TargetTooNarrow,
                                TableName = targetTable.Name,
                                ColumnName = targetField.Name,
                                ActualType = actualType,
                                TargetType = targetType,
                                Reason = $"target {targetType}({targetWidth}) cannot hold every {actualType} value, which needs up to {maxRenderedWidth} characters. Widen the field to {targetType}({maxRenderedWidth}) to migrate it."
                            });
                        }
                        else
                        {
                            Logger?.LogWarning(
                                "AlterColumn {Table}.{Column}: converting {ActualType} to {TargetType} with precision {TargetPrecision}",
                                targetTable.Name, targetField.Name, actualType, targetType, targetField.Precision);

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

    /// <summary>
    /// Records a refusal on the plan and logs it. Both matter: the log is what an operator watching
    /// an apply sees, and the plan is what a caller or a test can act on without matching on prose.
    /// </summary>
    private void Report(MigrationPlan plan, MigrationDiagnostic diagnostic)
    {
        plan.Diagnostics.Add(diagnostic);

        Logger?.LogWarning(
            "Unmigrated type change {Table}.{Column} ({Kind}): {Reason}",
            diagnostic.TableName, diagnostic.ColumnName, diagnostic.Kind, diagnostic.Reason);
    }
}