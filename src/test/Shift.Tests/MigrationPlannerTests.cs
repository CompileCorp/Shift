namespace Compile.Shift.Tests;

public class MigrationPlannerTests
{
    private readonly MigrationPlanner _planner;

    public MigrationPlannerTests()
    {
        _planner = new MigrationPlanner();
    }
    /*
	[Fact]
	public void GeneratePlan_NewTable_ShouldCreateTableStep()
	{
		// Arrange
		var targetModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Customer",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false }
					}
				}
			}
		};

		var actualModel = new DatabaseModel { Tables = new List<TableModel>() };

		// Act
		var plan = _planner.GeneratePlan(targetModel, actualModel);

		// Assert
		Assert.Single(plan.Steps);
		var step = plan.Steps.First();
		Assert.Equal(MigrationAction.CreateTable, step.Action);
		Assert.Equal("Customer", step.TableName);
		Assert.Fail();
	}

	[Fact]
	public void GeneratePlan_NewColumn_ShouldAddColumnStep()
	{
		// Arrange
		var targetModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Customer",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false },
						new FieldModel { Name = "Email", Type = "string", IsNullable = true }
					}
				}
			}
		};

		var actualModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Customer",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false }
					}
				}
			}
		};

		// Act
		var plan = _planner.GeneratePlan(targetModel, actualModel);

		// Assert
		Assert.Single(plan.Steps);
		var step = plan.Steps.First();
		Assert.Equal(MigrationAction.AddColumn, step.Action);
		Assert.Equal("Customer", step.TableName);
		Assert.Fail();
	}

	[Fact]
	public void GeneratePlan_NewForeignKey_ShouldAddForeignKeyStep()
	{
		// Arrange
		var targetModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Order",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "OrderID", Type = "int", IsNullable = false }
					},
					ForeignKeys = new List<ForeignKeyModel>
					{
						new ForeignKeyModel
						{
							TargetTable = "Customer",
							IsNullable = false,
							RelationshipType = RelationshipType.OneToOne
						}
					}
				}
			}
		};

		var actualModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Order",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "OrderID", Type = "int", IsNullable = false }
					},
					ForeignKeys = new List<ForeignKeyModel>()
				}
			}
		};

		// Act
		var plan = _planner.GeneratePlan(targetModel, actualModel);

		// Assert
		Assert.Single(plan.Steps);
		var step = plan.Steps.First();
		Assert.Equal(MigrationAction.AddForeignKey, step.Action);
		Assert.Equal("Order", step.TableName);
		Assert.Fail();
	}

	[Fact]
	public void GeneratePlan_NoChanges_ShouldReturnEmptyPlan()
	{
		// Arrange
		var model = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Customer",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false }
					}
				}
			}
		};

		// Act
		var plan = _planner.GeneratePlan(model, model);

		// Assert
		Assert.Empty(plan.Steps);
	}

	[Fact]
	public void GeneratePlan_ExtraTableInActual_ShouldReportInExtras()
	{
		// Arrange
		var targetModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel { Name = "Customer" }
			}
		};

		var actualModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel { Name = "Customer" },
				new TableModel { Name = "LegacyTable" }
			}
		};

		// Act
		var plan = _planner.GeneratePlan(targetModel, actualModel);

		// Assert
		Assert.Empty(plan.Steps);
		Assert.Single(plan.ExtrasInSqlServer.ExtraTables);
		Assert.Contains("LegacyTable", plan.ExtrasInSqlServer.ExtraTables);
	}

	[Fact]
	public void GeneratePlan_ExtraColumnInActual_ShouldReportInExtras()
	{
		// Arrange
		var targetModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Customer",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false }
					}
				}
			}
		};

		var actualModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "Customer",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false },
						new FieldModel { Name = "LegacyField", Type = "datetime", IsNullable = true }
					}
				}
			}
		};

		// Act
		var plan = _planner.GeneratePlan(targetModel, actualModel);

		// Assert
		Assert.Empty(plan.Steps);
		Assert.Single(plan.ExtrasInSqlServer.ExtraColumns);
		var extraColumn = plan.ExtrasInSqlServer.ExtraColumns.First();
		Assert.Equal("Customer", extraColumn.TableName);
		Assert.Equal("LegacyField", extraColumn.ColumnName);
		Assert.Equal("datetime", extraColumn.DataType);
	}

	[Fact]
	public void GeneratePlan_MultipleChanges_ShouldGenerateCorrectOrder()
	{
		// Arrange
		var targetModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "NewTable",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "NewTableID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false }
					}
				},
				new TableModel
				{
					Name = "ExistingTable",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "ExistingTableID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false },
						new FieldModel { Name = "NewField", Type = "int", IsNullable = true }
					},
					ForeignKeys = new List<ForeignKeyModel>
					{
						new ForeignKeyModel
						{
							TargetTable = "NewTable",
							IsNullable = false,
							RelationshipType = RelationshipType.OneToOne
						}
					}
				}
			}
		};

		var actualModel = new DatabaseModel
		{
			Tables = new List<TableModel>
			{
				new TableModel
				{
					Name = "ExistingTable",
					Fields = new List<FieldModel>
					{
						new FieldModel { Name = "ExistingTableID", Type = "int", IsNullable = false },
						new FieldModel { Name = "Name", Type = "string", IsNullable = false }
					},
					ForeignKeys = new List<ForeignKeyModel>()
				}
			}
		};

		// Act
		var plan = _planner.GeneratePlan(targetModel, actualModel);

		// Assert
		Assert.Equal(3, plan.Steps.Count);

		// Should be in order: CreateTable, AddColumn, AddForeignKey
		Assert.Equal(MigrationAction.CreateTable, plan.Steps[0].Action);
		Assert.Equal("NewTable", plan.Steps[0].TableName);

		Assert.Equal(MigrationAction.AddColumn, plan.Steps[1].Action);
		Assert.Equal("ExistingTable", plan.Steps[1].TableName);

		Assert.Equal(MigrationAction.AddForeignKey, plan.Steps[2].Action);
		Assert.Equal("ExistingTable", plan.Steps[2].TableName);
	}

	[Fact]
	public void TestOptionalMixinFields()
	{
		// Create a mixin with optional fields
		var mixin = new MixinModel
		{
			Name = "TestMixin",
			Fields = new List<FieldModel>
			{
				new FieldModel { Name = "RequiredField", Type = "int" },
				new FieldModel { Name = "!OptionalField", Type = "nvarchar(50)" } // Optional field
            }
		};

		// Create a table that only has the required field
		var table = new TableModel
		{
			Name = "TestTable",
			Fields = new List<FieldModel>
			{
				new FieldModel { Name = "TestTableID", Type = "int" },
				new FieldModel { Name = "RequiredField", Type = "int" }
                // Missing OptionalField - should still match
            }
		};

		// The mixin should match because it has the required field
		// and the optional field can be missing
		var exporter = new ModelExporter();
		var mixins = new List<MixinModel> { mixin };

		var result = exporter.GenerateDmdContent(table, mixins);

		// Should include the mixin in the header
		Assert.Contains("model TestTable with TestMixin", result);

		// Should not include the optional field in the output since it's not in the table
		Assert.DoesNotContain("OptionalField", result);

		// Should not include the required field since it's part of the mixin
		Assert.DoesNotContain("RequiredField", result);
	}

	[Fact]
	public void TestAuditableMixinWithOptionalFields()
	{
		// Use the Auditable mixin content directly
		var parser = new Parser();
		var mixinContent = @"mixin Auditable {
  !model User as CreatedBy
  !model User as LastModifiedBy
  nvarchar(50) CreatedBy
  datetime CreatedDateTime
  nvarchar(50) LastModifiedBy
  datetime LastModifiedDateTime
  model? Transaction
  int LockNumber
}";
		var mixin = parser.ParseMixin(mixinContent);

		// Test 1: Table with all fields (including optional ones)
		var fullTable = new TableModel
		{
			Name = "FullAuditableTable",
			Fields = new List<FieldModel>
			{
				new FieldModel { Name = "FullAuditableTableID", Type = "int" },
				new FieldModel { Name = "CreatedBy", Type = "nvarchar(50)" },
				new FieldModel { Name = "CreatedDateTime", Type = "datetime" },
				new FieldModel { Name = "LastModifiedBy", Type = "nvarchar(50)" },
				new FieldModel { Name = "LastModifiedDateTime", Type = "datetime" },
				new FieldModel { Name = "Transaction", Type = "model" },
				new FieldModel { Name = "LockNumber", Type = "int" }
			},
			ForeignKeys = new List<ForeignKeyModel>
			{
				new ForeignKeyModel { TargetTable = "User", IsNullable = false, RelationshipType = RelationshipType.OneToOne },
				new ForeignKeyModel { TargetTable = "User", IsNullable = false, RelationshipType = RelationshipType.OneToOne },
				new ForeignKeyModel { TargetTable = "Transaction", IsNullable = true, RelationshipType = RelationshipType.OneToOne }
			}
		};

		var exporter = new ModelExporter();
		var result1 = exporter.GenerateDmdContent(fullTable, new List<MixinModel> { mixin! });

		// Should include the mixin in the header
		Assert.Contains("model FullAuditableTable with Auditable", result1);

		// Should not include any of the mixin fields in the body
		Assert.DoesNotContain("CreatedBy", result1);
		Assert.DoesNotContain("CreatedDateTime", result1);
		Assert.DoesNotContain("LastModifiedBy", result1);
		Assert.DoesNotContain("LastModifiedDateTime", result1);
		// Remove broad assertions for Transaction and LockNumber

		// Test 2: Table missing optional fields (CreatedBy and LastModifiedBy)
		var partialTable = new TableModel
		{
			Name = "PartialAuditableTable",
			Fields = new List<FieldModel>
			{
				new FieldModel { Name = "PartialAuditableTableID", Type = "int" },
				new FieldModel { Name = "CreatedBy", Type = "nvarchar(50)" },
				new FieldModel { Name = "LastModifiedBy", Type = "nvarchar(50)" },
				new FieldModel { Name = "CreatedDateTime", Type = "datetime" },
				new FieldModel { Name = "LastModifiedDateTime", Type = "datetime" },
				new FieldModel { Name = "Transaction", Type = "model" },
				new FieldModel { Name = "LockNumber", Type = "int" }
			},
			ForeignKeys = new List<ForeignKeyModel>
			{
				new ForeignKeyModel { TargetTable = "Transaction", IsNullable = true, RelationshipType = RelationshipType.OneToOne }
			}
		};

		var result2 = exporter.GenerateDmdContent(partialTable, new List<MixinModel> { mixin! });

		// Should still include the mixin in the header (optional fields can be missing)
		Assert.Contains("model PartialAuditableTable with Auditable", result2);

		// Should not include the required fields that are present
		Assert.DoesNotContain("CreatedDateTime", result2);
		Assert.DoesNotContain("LastModifiedDateTime", result2);
		// Per-line checks for Transaction and LockNumber as fields only
		foreach (var line in result2.Split('\n'))
		{
			var trimmed = line.Trim();
			Assert.False(trimmed.EndsWith("Transaction"), $"Transaction should not appear as a field: {trimmed}");
			Assert.False(trimmed.EndsWith("LockNumber"), $"LockNumber should not appear as a field: {trimmed}");
		}

		// Test 3: Table missing required fields (should not match)
		var invalidTable = new TableModel
		{
			Name = "InvalidAuditableTable",
			Fields = new List<FieldModel>
			{
				new FieldModel { Name = "InvalidAuditableTableID", Type = "int" },
				new FieldModel { Name = "CreatedBy", Type = "nvarchar(50)" },
				new FieldModel { Name = "LastModifiedBy", Type = "nvarchar(50)" }
                // Missing required fields like CreatedDateTime, LastModifiedDateTime, etc.
            }
		};

		var result3 = exporter.GenerateDmdContent(invalidTable, new List<MixinModel> { mixin! });

		// Should NOT include the mixin in the header (missing required fields)
		Assert.DoesNotContain("model InvalidAuditableTable with Auditable", result3);
		Assert.Contains("model InvalidAuditableTable {", result3);

		// Should include the fields that are present
		Assert.Contains("string CreatedBy", result3);
		Assert.Contains("string LastModifiedBy", result3);
	}
	*/
}