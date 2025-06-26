using Compile.Shift.Model;

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

            foreach (var foreignKey in table.ForeignKeys)
            {
                plan.Steps.Add(new MigrationStep
                {
                    Action = MigrationAction.AddForeignKey,
                    TableName = table.Name,
                    ForeignKey = foreignKey
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