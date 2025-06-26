namespace Compile.Shift.Tests;

public class ModelExporterTests : IDisposable
{
    private readonly ModelExporter _exporter;
    private readonly string _testOutputDir;

    public ModelExporterTests()
    {
        _exporter = new ModelExporter();
        _testOutputDir = Path.Combine(Path.GetTempPath(), "DmdSystemTests");
        Directory.CreateDirectory(_testOutputDir);
    }

    /*
    [Fact]
    public void ExportToDmd_ShouldCreateValidDmdFiles()
    {
        // Arrange
        var model = CreateTestModel();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        Assert.True(Directory.Exists(outputPath));
        var files = Directory.GetFiles(outputPath, "*.dmd");
        Assert.Single(files);

        var customerFile = Path.Combine(outputPath, "customer.dmd");
        Assert.True(File.Exists(customerFile));

        var content = File.ReadAllText(customerFile);
        Assert.Contains("model Customer {", content);
        Assert.Contains("string name", content);
    }

    [Fact]
    public void ExportToDmd_WithMultipleModels_ShouldCreateMultipleFiles()
    {
        // Arrange
        var model = CreateComplexModel();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        Assert.True(Directory.Exists(outputPath));
        var files = Directory.GetFiles(outputPath, "*.dmd");
        Assert.Equal(2, files.Length);

        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var orderFile = Path.Combine(outputPath, "order.dmd");

        Assert.True(File.Exists(customerFile));
        Assert.True(File.Exists(orderFile));

        var customerContent = File.ReadAllText(customerFile);
        var orderContent = File.ReadAllText(orderFile);

        Assert.Contains("model Customer {", customerContent);
        Assert.Contains("model Order {", orderContent);
    }

    [Fact]
    public void ExportToDmd_WithForeignKeys_ShouldIncludeRelationships()
    {
        // Arrange
        var model = CreateModelWithForeignKeys();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        Assert.Contains("model Order", content);
    }

    [Fact]
    public void ExportToDmd_WithIndexes_ShouldIncludeIndexes()
    {
        // Arrange
        var model = CreateModelWithIndexes();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        Assert.Contains("key (name)", content);
    }

    [Fact]
    public void ExportToDmd_WithAttributes_ShouldIncludeAttributes()
    {
        // Arrange
        var model = CreateModelWithAttributes();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        Assert.Contains("@transactional", content);
    }

    [Fact]
    public void ExportToDmd_WithNullableFields_ShouldIncludeQuestionMark()
    {
        // Arrange
        var model = CreateModelWithNullableFields();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        Assert.Contains("string? email", content);
        Assert.Contains("string name", content); // Not nullable
    }

    [Fact]
    public void ExportToDmd_WithOneToManyRelationships_ShouldUseModelsKeyword()
    {
        // Arrange
        var model = CreateModelWithOneToManyRelationships();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        Assert.Contains("models Order", content);
        Assert.Contains("model Profile", content); // One-to-one
    }

    [Fact]
    public void ExportToDmd_WithNullableRelationships_ShouldIncludeQuestionMark()
    {
        // Arrange
        var model = CreateModelWithNullableRelationships();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        Assert.Contains("model Profile?", content);
        Assert.Contains("model Order", content); // Not nullable
    }

    [Fact]
    public void ExportToDmd_ShouldExcludeAutoGeneratedIdFields()
    {
        // Arrange
        var model = CreateTestModel();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        Assert.DoesNotContain("CustomerID", content);
        Assert.Contains("string name", content);
    }

    [Fact]
    public void ExportToDmd_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var model = CreateTestModel();
        var outputPath = Path.Combine(_testOutputDir, "newdirectory");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        Assert.True(Directory.Exists(outputPath));
        var files = Directory.GetFiles(outputPath, "*.dmd");
        Assert.Single(files);
    }

    [Fact]
    public void ExportToDmd_WithEmptyModel_ShouldCreateDirectoryButNoFiles()
    {
        // Arrange
        var model = new DatabaseModel(); // Empty model
        var outputPath = Path.Combine(_testOutputDir, "empty-export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        Assert.True(Directory.Exists(outputPath));
        var files = Directory.GetFiles(outputPath, "*.dmd");
        Assert.Empty(files);
    }

    [Fact]
    public void ExportToDmd_WithComplexModel_ShouldGenerateValidDmdContent()
    {
        // Arrange
        var model = CreateComplexModel();
        var outputPath = Path.Combine(_testOutputDir, "export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var content = File.ReadAllText(customerFile);

        // Check structure
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("model Customer {", lines[0].Trim());
        Assert.Equal("}", lines[^1].Trim());

        // Check content
        Assert.Contains("string name", content);
        Assert.Contains("string? email", content);
        Assert.Contains("model Order", content);
        Assert.Contains("key (email)", content);
        Assert.Contains("@transactional", content);
    }

    [Fact]
    public void ExportToDmd_WithMockDatabase_ShouldCreateValidFiles()
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
                        new FieldModel { Name = "name", Type = "string", IsNullable = false },
                        new FieldModel { Name = "email", Type = "string", IsNullable = true }
                    },
                    ForeignKeys = new List<ForeignKeyModel>
                    {
                        new ForeignKeyModel
                        {
                            TargetTable = "Profile",
                            IsNullable = false,
                            RelationshipType = RelationshipType.OneToOne
                        }
                    },
                    Indexes = new List<IndexModel>
                    {
                        new IndexModel
                        {
                            Fields = new List<string> { "email" },
                            IsUnique = true
                        }
                    },
                    Attributes = new Dictionary<string, bool>
                    {
                        { "transactional", true }
                    }
                },
                new TableModel
                {
                    Name = "Product",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "ProductID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false },
                        new FieldModel { Name = "price", Type = "decimal", IsNullable = false }
                    },
                    Indexes = new List<IndexModel>
                    {
                        new IndexModel
                        {
                            Fields = new List<string> { "name" },
                            IsUnique = true
                        }
                    }
                }
            }
        };
        var outputPath = Path.Combine(_testOutputDir, "mock-export");

        // Act
        _exporter.ExportToDmd(model, outputPath);

        // Assert
        Assert.True(Directory.Exists(outputPath));
        var files = Directory.GetFiles(outputPath, "*.dmd");
        Assert.Equal(2, files.Length);

        var customerFile = Path.Combine(outputPath, "customer.dmd");
        var productFile = Path.Combine(outputPath, "product.dmd");

        Assert.True(File.Exists(customerFile));
        Assert.True(File.Exists(productFile));

        var customerContent = File.ReadAllText(customerFile);
        var productContent = File.ReadAllText(productFile);

        // Verify Customer file content
        Assert.Contains("model Customer {", customerContent);
        Assert.Contains("string name", customerContent);
        Assert.Contains("string? email", customerContent);
        Assert.Contains("model Profile", customerContent);
        Assert.Contains("key (email)", customerContent);
        Assert.Contains("@transactional", customerContent);

        // Verify Product file content
        Assert.Contains("model Product {", productContent);
        Assert.Contains("string name", productContent);
        Assert.Contains("decimal price", productContent);
        Assert.Contains("key (name)", productContent);
    }

    private static DatabaseModel CreateTestModel()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false }
                    }
                }
            }
        };
    }

    private static DatabaseModel CreateModelWithForeignKeys()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false }
                    },
                    ForeignKeys = new List<ForeignKeyModel>
                    {
                        new ForeignKeyModel
                        {
                            TargetTable = "Order",
                            IsNullable = false,
                            RelationshipType = RelationshipType.OneToOne
                        }
                    }
                }
            }
        };
    }

    private static DatabaseModel CreateModelWithIndexes()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false }
                    },
                    Indexes = new List<IndexModel>
                    {
                        new IndexModel
                        {
                            Fields = new List<string> { "name" },
                            IsUnique = true
                        }
                    }
                }
            }
        };
    }

    private static DatabaseModel CreateModelWithAttributes()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false }
                    },
                    Attributes = new Dictionary<string, bool>
                    {
                        { "transactional", true }
                    }
                }
            }
        };
    }

    private static DatabaseModel CreateModelWithNullableFields()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false },
                        new FieldModel { Name = "email", Type = "string", IsNullable = true }
                    }
                }
            }
        };
    }

    private static DatabaseModel CreateModelWithOneToManyRelationships()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false }
                    },
                    ForeignKeys = new List<ForeignKeyModel>
                    {
                        new ForeignKeyModel
                        {
                            TargetTable = "Order",
                            IsNullable = false,
                            RelationshipType = RelationshipType.OneToMany
                        },
                        new ForeignKeyModel
                        {
                            TargetTable = "Profile",
                            IsNullable = false,
                            RelationshipType = RelationshipType.OneToOne
                        }
                    }
                }
            }
        };
    }

    private static DatabaseModel CreateModelWithNullableRelationships()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false }
                    },
                    ForeignKeys = new List<ForeignKeyModel>
                    {
                        new ForeignKeyModel
                        {
                            TargetTable = "Profile",
                            IsNullable = true,
                            RelationshipType = RelationshipType.OneToOne
                        },
                        new ForeignKeyModel
                        {
                            TargetTable = "Order",
                            IsNullable = false,
                            RelationshipType = RelationshipType.OneToOne
                        }
                    }
                }
            }
        };
    }

    private static DatabaseModel CreateComplexModel()
    {
        return new DatabaseModel
        {
            Tables = new List<TableModel>
            {
                new TableModel
                {
                    Name = "Customer",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "CustomerID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "name", Type = "string", IsNullable = false },
                        new FieldModel { Name = "email", Type = "string", IsNullable = true }
                    },
                    ForeignKeys = new List<ForeignKeyModel>
                    {
                        new ForeignKeyModel
                        {
                            TargetTable = "Order",
                            IsNullable = false,
                            RelationshipType = RelationshipType.OneToOne
                        }
                    },
                    Indexes = new List<IndexModel>
                    {
                        new IndexModel
                        {
                            Fields = new List<string> { "email" },
                            IsUnique = true
                        }
                    },
                    Attributes = new Dictionary<string, bool>
                    {
                        { "transactional", true }
                    }
                },
                new TableModel
                {
                    Name = "Order",
                    Fields = new List<FieldModel>
                    {
                        new FieldModel { Name = "OrderID", Type = "int", IsNullable = false },
                        new FieldModel { Name = "orderDate", Type = "datetime", IsNullable = false }
                    }
                }
            }
        };
    }
    */

    public void Dispose()
    {
        if (Directory.Exists(_testOutputDir))
        {
            Directory.Delete(_testOutputDir, true);
        }
    }
}