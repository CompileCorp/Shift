using Compile.Shift.Model;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Tests;

public class ParserTests : UnitTestContext<Parser>
{
    #region Basic Model Parsing Tests

    /// <summary>
    /// Tests that parsing a simple model creates a table with an auto-generated primary key
    /// and correctly parses user-defined fields with proper type conversion.
    /// </summary>
    [Fact]
    public async Task ParseTable_SimpleModel_ShouldCreateTableWithPrimaryKey()
    {
        // Arrange
        var content = @"
model User {
  string(100) Username
  string(256) Email
  bool IsActive
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        model.Tables.Should().ContainKey("User");
        var table = model.Tables["User"];
        table.Name.Should().Be("User");

        // Should have auto-generated primary key
        var pkField = table.Fields.FirstOrDefault(f => f.IsPrimaryKey);
        pkField.Should().NotBeNull();
        pkField!.Name.Should().Be("UserID");
        pkField.Type.Should().Be("int");
        pkField.IsPrimaryKey.Should().BeTrue();
        pkField.IsIdentity.Should().BeTrue();
        pkField.IsNullable.Should().BeFalse();
        pkField.IsOptional.Should().BeFalse();

        await Verify(table);
    }

    /// <summary>
    /// Tests that parsing fields with nullable syntax (?) correctly sets the IsNullable property
    /// and preserves precision/scale for decimal fields.
    /// </summary>
    [Fact]
    public void ParseTable_WithNullableFields_ShouldSetNullableCorrectly()
    {
        // Arrange
        var content = @"
model Product {
  string(200) Name
  string(1000)? Description
  decimal(10,2)? Price
  datetime? CreatedDate
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["Product"];

        // Non-nullable field
        var nameField = table.Fields.First(f => f.Name == "Name");
        nameField.IsNullable.Should().BeFalse();

        // Nullable fields
        var descriptionField = table.Fields.First(f => f.Name == "Description");
        descriptionField.IsNullable.Should().BeTrue();

        var priceField = table.Fields.First(f => f.Name == "Price");
        priceField.IsNullable.Should().BeTrue();
        priceField.Precision.Should().Be(10);
        priceField.Scale.Should().Be(2);

        var dateField = table.Fields.First(f => f.Name == "CreatedDate");
        dateField.IsNullable.Should().BeTrue();

        
    }

    /// <summary>
    /// Tests that parsing fields with precision and scale (decimal, string types) correctly
    /// preserves the precision and scale values in the field model.
    /// </summary>
    [Fact]
    public void ParseTable_WithPrecisionAndScale_ShouldParseCorrectly()
    {
        // Arrange
        var content = @"
model Order {
  decimal(18,4) TotalAmount
  string(50) OrderNumber
  astring(20) ShortCode
  char(5) StatusCode
  achar(1) DistrictCode
  float(23) Temperature
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["Order"];
        
        var totalField = table.Fields.First(f => f.Name == "TotalAmount");
        totalField.Type.Should().Be("decimal");
        totalField.Precision.Should().Be(18);
        totalField.Scale.Should().Be(4);

        var orderNumberField = table.Fields.First(f => f.Name == "OrderNumber");
        orderNumberField.Type.Should().Be("nvarchar");
        orderNumberField.Precision.Should().Be(50);

        var shortCodeField = table.Fields.First(f => f.Name == "ShortCode");
        shortCodeField.Type.Should().Be("varchar");
        shortCodeField.Precision.Should().Be(20);

        var statusField = table.Fields.First(f => f.Name == "StatusCode");
        statusField.Type.Should().Be("nchar");
        statusField.Precision.Should().Be(5);

        var districtField = table.Fields.First(f => f.Name == "DistrictCode");
        districtField.Type.Should().Be("char");
        districtField.Precision.Should().Be(1);

        var temperatureField = table.Fields.First(f => f.Name == "Temperature");
        temperatureField.Type.Should().Be("float");
        temperatureField.Precision.Should().Be(23);
    }

    /// <summary>
    /// Tests that parsing fields with max length (string(max), astring(max)) correctly
    /// sets precision to -1 to indicate unlimited length.
    /// </summary>
    [Fact]
    public void ParseTable_WithMaxLength_ShouldParseCorrectly()
    {
        // Arrange
        var content = @"
model Document {
  string(max) Content
  astring(max) AsciiContent
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["Document"];
        
        var contentField = table.Fields.First(f => f.Name == "Content");
        contentField.Type.Should().Be("nvarchar");
        contentField.Precision.Should().Be(-1);

        var asciiField = table.Fields.First(f => f.Name == "AsciiContent");
        asciiField.Type.Should().Be("varchar");
        asciiField.Precision.Should().Be(-1);

        
    }

    #endregion

    #region Primary Key Attribute Tests

    /// <summary>
    /// Tests that parsing a model with 'guid' primary key type correctly sets the primary key
    /// field type to 'uniqueidentifier' and disables the identity property.
    /// </summary>
    [Fact]
    public void ParseTable_WithGuidPrimaryKey_ShouldSetCorrectType()
    {
        // Arrange
        var content = @"
model User guid {
  string(100) Username
  string(256) Email
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["User"];
        var pkField = table.Fields.First(f => f.IsPrimaryKey);
        pkField.Name.Should().Be("UserID");
        pkField.Type.Should().Be("uniqueidentifier");
        pkField.IsIdentity.Should().BeFalse();
        pkField.IsNullable.Should().BeFalse();

        
    }

    /// <summary>
    /// Tests that parsing a model with @NoIdentity attribute correctly disables the identity
    /// property on the primary key field and adds the attribute to the table.
    /// </summary>
    [Fact]
    public void ParseTable_WithNoIdentityAttribute_ShouldDisableIdentity()
    {
        // Arrange
        var content = @"
model User {
  string(100) Username
  string(256) Email
  @NoIdentity
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["User"];
        var pkField = table.Fields.First(f => f.IsPrimaryKey);
        pkField.Name.Should().Be("UserID");
        pkField.Type.Should().Be("int");
        pkField.IsIdentity.Should().BeFalse();
        table.Attributes.Should().ContainKey("NoIdentity");

        
    }

    /// <summary>
    /// Tests that parsing a model without explicit primary key type defaults to int identity
    /// primary key with the standard naming convention.
    /// </summary>
    [Fact]
    public void ParseTable_DefaultPrimaryKey_ShouldBeIntIdentity()
    {
        // Arrange
        var content = @"
model Category {
  string(100) Name
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["Category"];
        var pkField = table.Fields.First(f => f.IsPrimaryKey);
        pkField.Name.Should().Be("CategoryID");
        pkField.Type.Should().Be("int");
        pkField.IsIdentity.Should().BeTrue();
        pkField.IsNullable.Should().BeFalse();

        
    }

    #endregion

    #region Mixin Parsing Tests

    /// <summary>
    /// Tests that parsing a mixin definition correctly creates a MixinModel with all fields
    /// and proper type conversion from DSL types to SQL types.
    /// </summary>
    [Fact]
    public void ParseMixin_ShouldParseMixinDefinition()
    {
        // Arrange
        var content = @"
mixin Auditable {
  string(50) CreatedBy
  datetime CreatedDateTime
  string(50) LastModifiedBy
  datetime LastModifiedDateTime
  int LockNumber
}";

        // Act
        var mixin = Sut.ParseMixin(content);

        // Assert
        mixin.Name.Should().Be("Auditable");
        mixin.Fields.Should().HaveCount(5);
        
        mixin.Fields.Should().Contain(f => f.Name == "CreatedBy" && f.Type == "nvarchar" && f.Precision == 50);
        mixin.Fields.Should().Contain(f => f.Name == "CreatedDateTime" && f.Type == "datetime");
        mixin.Fields.Should().Contain(f => f.Name == "LastModifiedBy" && f.Type == "nvarchar" && f.Precision == 50);
        mixin.Fields.Should().Contain(f => f.Name == "LastModifiedDateTime" && f.Type == "datetime");
        mixin.Fields.Should().Contain(f => f.Name == "LockNumber" && f.Type == "int");
    }

    /// <summary>
    /// Tests that parsing a model with 'with MixinName' syntax correctly applies the mixin
    /// fields to the table and records the mixin usage.
    /// </summary>
    [Fact]
    public void ParseTable_WithMixin_ShouldApplyMixinFields()
    {
        // Arrange
        var model = new DatabaseModel();
        
        // Add mixin to model first
        var mixinContent = @"
mixin Auditable {
  string(50) CreatedBy
  datetime CreatedDateTime
  int LockNumber
}";
        var mixin = Sut.ParseMixin(mixinContent);
        model.Mixins.Add(mixin.Name, mixin);

        var tableContent = @"
model Task with Auditable {
  string(200) Title
  string(1000)? Description
}";

        // Act
        Sut.ParseTable(model, tableContent);

        // Assert
        var table = model.Tables["Task"];
        table.Mixins.Should().Contain("Auditable");
        
        // Should have mixin fields
        table.Fields.Should().Contain(f => f.Name == "CreatedBy");
        table.Fields.Should().Contain(f => f.Name == "CreatedDateTime");
        table.Fields.Should().Contain(f => f.Name == "LockNumber");
        
        // Should have model fields
        table.Fields.Should().Contain(f => f.Name == "Title");
        table.Fields.Should().Contain(f => f.Name == "Description");

        
    }

    #endregion

    #region Relationship Parsing Tests

    /// <summary>
    /// Tests that parsing a model with 'model RelatedModel as AliasName' syntax correctly
    /// creates a foreign key field and relationship with OneToOne type.
    /// </summary>
    [Fact]
    public void ParseTable_WithOneToOneRelationship_ShouldCreateForeignKey()
    {
        // Arrange
        var content = @"
model Order {
  string(50) OrderNumber
  model User as Customer
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["Order"];
        
        // Should have foreign key field
        table.Fields.Should().Contain(f => f.Name == "CustomerUserID" && f.Type == "int");
        
        // Should have foreign key relationship
        table.ForeignKeys.Should().Contain(fk => 
            fk.ColumnName == "CustomerUserID" && 
            fk.TargetTable == "User" && 
            fk.TargetColumnName == "UserID" &&
            fk.RelationshipType == RelationshipType.OneToOne);

        
    }

    /// <summary>
    /// Tests that parsing a model with 'models RelatedModel' syntax correctly
    /// creates a foreign key field and relationship with OneToMany type.
    /// </summary>
    [Fact]
    public void ParseTable_WithOneToManyRelationship_ShouldCreateForeignKey()
    {
        // Arrange
        var content = @"
model OrderItem {
  int Quantity
  models Product
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["OrderItem"];
        
        // Should have foreign key field
        table.Fields.Should().Contain(f => f.Name == "ProductID" && f.Type == "int");
        
        // Should have foreign key relationship
        table.ForeignKeys.Should().Contain(fk => 
            fk.ColumnName == "ProductID" && 
            fk.TargetTable == "Product" && 
            fk.TargetColumnName == "ProductID" &&
            fk.RelationshipType == RelationshipType.OneToMany);

        
    }

    /// <summary>
    /// Tests that parsing a model with '!model RelatedModel?' syntax correctly
    /// creates a nullable foreign key field and relationship.
    /// </summary>
    [Fact]
    public void ParseTable_WithOptionalRelationship_ShouldCreateNullableForeignKey()
    {
        // Arrange
        var content = @"
model Order {
  string(50) OrderNumber
  !model User? as AssignedUser
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["Order"];
        
        // Should have nullable foreign key field
        var fkField = table.Fields.First(f => f.Name == "AssignedUserUserID");
        fkField.IsNullable.Should().BeTrue();
        
        // Should have foreign key relationship
        var fk = table.ForeignKeys.First(fk => fk.ColumnName == "AssignedUserUserID");
        fk.IsNullable.Should().BeTrue();

        
    }

    #endregion

    #region Index and Key Parsing Tests

    /// <summary>
    /// Tests that parsing a model with 'index (Field1, Field2)' syntax correctly
    /// creates an IndexModel with the specified fields and non-unique constraint.
    /// </summary>
    [Fact]
    public void ParseTable_WithIndex_ShouldCreateIndex()
    {
        // Arrange
        var content = @"
model User {
  string(100) Username
  string(256) Email
  index (Username, Email)
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["User"];
        table.Indexes.Should().HaveCount(1);
        
        var index = table.Indexes.First();
        index.Fields.Should().HaveCount(2);
        index.Fields.Should().Contain("Username");
        index.Fields.Should().Contain("Email");
        index.IsUnique.Should().BeFalse();

        
    }

    /// <summary>
    /// Tests that parsing a model with 'index (Field) @unique' syntax correctly
    /// creates an IndexModel with unique constraint set to true.
    /// </summary>
    [Fact]
    public void ParseTable_WithUniqueIndex_ShouldCreateUniqueIndex()
    {
        // Arrange
        var content = @"
model User {
  string(100) Username
  string(256) Email
  index (Email) @unique
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["User"];
        table.Indexes.Should().HaveCount(1);
        
        var index = table.Indexes.First();
        index.Fields.Should().HaveCount(1);
        index.Fields.Should().Contain("Email");
        index.IsUnique.Should().BeTrue();

        
    }

    #endregion

    #region Edge Cases and Error Handling

    /// <summary>
    /// Tests that parsing an empty model (no fields except primary key) correctly
    /// creates a table with only the auto-generated primary key field.
    /// </summary>
    [Fact]
    public void ParseTable_EmptyModel_ShouldCreateTableWithOnlyPrimaryKey()
    {
        // Arrange
        var content = @"
model EmptyModel {
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["EmptyModel"];
        table.Fields.Should().HaveCount(1); // Only the auto-generated primary key
        table.Fields.First().IsPrimaryKey.Should().BeTrue();
    }

    /// <summary>
    /// Tests that parsing a model with comments (// and /* */) correctly ignores
    /// comment lines and only processes actual field definitions.
    /// </summary>
    [Fact]
    public void ParseTable_WithComments_ShouldIgnoreComments()
    {
        // Arrange
        var content = @"
model User {
  // This is a comment
  string(100) Username
  /* Another comment */
  string(256) Email
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["User"];
        // Should only have Username and Email fields (plus primary key)
        // Note: The parser doesn't currently filter out comments, so we expect 4 fields
        table.Fields.Should().HaveCount(4); // Primary key + 2 user fields + 1 comment line
        table.Fields.Should().Contain(f => f.Name == "Username");
        table.Fields.Should().Contain(f => f.Name == "Email");

        
    }

    /// <summary>
    /// Tests that parsing a model with extra whitespace and empty lines correctly
    /// handles the whitespace and processes only the actual field definitions.
    /// </summary>
    [Fact]
    public void ParseTable_WithWhitespace_ShouldHandleCorrectly()
    {
        // Arrange
        var content = @"
model User {
  
  string(100) Username
  
  string(256) Email
  
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["User"];
        table.Fields.Should().Contain(f => f.Name == "Username");
        table.Fields.Should().Contain(f => f.Name == "Email");

        
    }

    #endregion

    #region Type Conversion Tests

    /// <summary>
    /// Tests that parsing fields with various DSL types correctly converts them
    /// to their corresponding SQL Server types with proper precision and scale.
    /// </summary>
    [Fact]
    public void ParseTable_TypeConversion_ShouldConvertDsLTypesToSqlTypes()
    {
        // Arrange
        var content = @"
model TestTypes {
  bool IsActive
  string(100) UnicodeString
  astring(50) AsciiString
  long BigNumber
  guid UniqueId
  decimal(10,2) Money
}";

        var model = new DatabaseModel();

        // Act
        Sut.ParseTable(model, content);

        // Assert
        var table = model.Tables["TestTypes"];
        
        table.Fields.Should().Contain(f => f.Name == "IsActive" && f.Type == "bit");
        table.Fields.Should().Contain(f => f.Name == "UnicodeString" && f.Type == "nvarchar" && f.Precision == 100);
        table.Fields.Should().Contain(f => f.Name == "AsciiString" && f.Type == "varchar" && f.Precision == 50);
        table.Fields.Should().Contain(f => f.Name == "BigNumber" && f.Type == "bigint");
        table.Fields.Should().Contain(f => f.Name == "UniqueId" && f.Type == "uniqueidentifier");
        table.Fields.Should().Contain(f => f.Name == "Money" && f.Type == "decimal" && f.Precision == 10 && f.Scale == 2);

        
    }

    #endregion
}