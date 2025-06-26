namespace Compile.Shift.Tests;

public class ParserTests
{
    private readonly Parser _parser;

    public ParserTests()
    {
        _parser = new Parser();
    }

    /*
    [Fact]
    public void ParseSimpleModel_ShouldCreateTableWithPrimaryKey()
    {
        // Arrange
        var dmdContent = @"
model Customer {
  string name
  int age?
}";
        var tempFile = CreateTempDmdFile(dmdContent);

        // Act
        var result = _parser.ParseFiles(new[] { tempFile });

        // Assert
        Assert.Single(result.Tables);
        var table = result.Tables.First();
        Assert.Equal("Customer", table.Name);
        Assert.Equal(3, table.Fields.Count); // name, age, CustomerID (auto-generated)

        var primaryKey = table.Fields.First(f => f.Name == "CustomerID");
        Assert.Equal("int", primaryKey.Type);
        Assert.False(primaryKey.IsNullable);

        var nameField = table.Fields.First(f => f.Name == "name");
        Assert.Equal("string", nameField.Type);
        Assert.False(nameField.IsNullable);

        var ageField = table.Fields.First(f => f.Name == "age");
        Assert.Equal("int", ageField.Type);
        Assert.True(ageField.IsNullable);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void ParseModelWithMixin_ShouldApplyMixinFields()
    {
        // Arrange
        var dmdContent = @"
mixin Audited {
  datetime createdAt
  datetime updatedAt
}

model Customer with Audited {
  string name
}";
        var tempFile = CreateTempDmdFile(dmdContent);

        // Act
        var result = _parser.ParseFiles(new[] { tempFile });

        // Assert
        Assert.Single(result.Tables);
        var table = result.Tables.First();
        Assert.Equal("Customer", table.Name);
        Assert.Equal(4, table.Fields.Count); // name, createdAt, updatedAt, CustomerID

        Assert.Contains(table.Fields, f => f.Name == "createdAt" && f.Type == "datetime");
        Assert.Contains(table.Fields, f => f.Name == "updatedAt" && f.Type == "datetime");

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void ParseModelWithRelationships_ShouldCreateForeignKeys()
    {
        // Arrange
        var dmdContent = @"
model Customer {
  string name
  model Profile
  models Order
}";
        var tempFile = CreateTempDmdFile(dmdContent);

        // Act
        var result = _parser.ParseFiles(new[] { tempFile });

        // Assert
        Assert.Single(result.Tables);
        var table = result.Tables.First();
        Assert.Equal(2, table.ForeignKeys.Count);

        var profileFk = table.ForeignKeys.First(fk => fk.TargetTable == "Profile");
        Assert.Equal(RelationshipType.OneToOne, profileFk.RelationshipType);
        Assert.False(profileFk.IsNullable);

        var orderFk = table.ForeignKeys.First(fk => fk.TargetTable == "Order");
        Assert.Equal(RelationshipType.OneToMany, orderFk.RelationshipType);
        Assert.False(orderFk.IsNullable);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void ParseModelWithIndexes_ShouldCreateIndexes()
    {
        // Arrange
        var dmdContent = @"
model Product {
  string name
  decimal price
  index (name) @unique
  index (price, name)
}";
        var tempFile = CreateTempDmdFile(dmdContent);

        // Act
        var result = _parser.ParseFiles(new[] { tempFile });

        // Assert
        Assert.Single(result.Tables);
        var table = result.Tables.First();
        Assert.Equal(2, table.Indexes.Count);

        var uniqueIndex = table.Indexes.First(i => i.Fields.Contains("name") && i.IsUnique);
        Assert.Single(uniqueIndex.Fields);
        Assert.True(uniqueIndex.IsUnique);

        var compositeIndex = table.Indexes.First(i => i.Fields.Count == 2);
        Assert.Equal(2, compositeIndex.Fields.Count);
        Assert.Contains("price", compositeIndex.Fields);
        Assert.Contains("name", compositeIndex.Fields);
        Assert.False(compositeIndex.IsUnique);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void ParseModelWithAttributes_ShouldSetAttributes()
    {
        // Arrange
        var dmdContent = @"
model Customer {
  string name
  @transactional
}";
        var tempFile = CreateTempDmdFile(dmdContent);

        // Act
        var result = _parser.ParseFiles(new[] { tempFile });

        // Assert
        Assert.Single(result.Tables);
        var table = result.Tables.First();
        Assert.Single(table.Attributes);
        Assert.True(table.Attributes["transactional"]);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void ParseMultipleFiles_ShouldCombineResults()
    {
        // Arrange
        var file1Content = @"
model Customer {
  string name
}";
        var file2Content = @"
model Product {
  string name
}";
        var tempFile1 = CreateTempDmdFile(file1Content);
        var tempFile2 = CreateTempDmdFile(file2Content);

        // Act
        var result = _parser.ParseFiles(new[] { tempFile1, tempFile2 });

        // Assert
        Assert.Equal(2, result.Tables.Count);
        Assert.Contains(result.Tables, t => t.Name == "Customer");
        Assert.Contains(result.Tables, t => t.Name == "Product");

        // Cleanup
        File.Delete(tempFile1);
        File.Delete(tempFile2);
    }

    [Fact]
    public void ParseMultipleModelsInOneFile_ShouldCreateAllModels()
    {
        // Arrange
        var dmdContent = @"
model Customer {
  string name
  int age?
}

model Product {
  string name
  decimal price
  index (name) @unique
}";
        var tempFile = CreateTempDmdFile(dmdContent);

        // Act
        var result = _parser.ParseFiles(new[] { tempFile });

        // Assert
        Assert.Equal(2, result.Tables.Count);
        Assert.Contains(result.Tables, t => t.Name == "Customer");
        Assert.Contains(result.Tables, t => t.Name == "Product");

        var customer = result.Tables.First(t => t.Name == "Customer");
        Assert.Equal(3, customer.Fields.Count); // name, age, CustomerID

        var product = result.Tables.First(t => t.Name == "Product");
        Assert.Equal(3, product.Fields.Count); // name, price, ProductID
        Assert.Single(product.Indexes);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void ParseEmptyFile_ShouldReturnEmptyModel()
    {
        // Arrange
        var tempFile = CreateTempDmdFile("");

        // Act
        var result = _parser.ParseFiles(new[] { tempFile });

        // Assert
        Assert.Empty(result.Tables);

        // Cleanup
        File.Delete(tempFile);
    }

    private static string CreateTempDmdFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        var dmdFile = Path.ChangeExtension(tempFile, ".dmd");
        File.Move(tempFile, dmdFile);
        File.WriteAllText(dmdFile, content);
        return dmdFile;
    }
    */
}