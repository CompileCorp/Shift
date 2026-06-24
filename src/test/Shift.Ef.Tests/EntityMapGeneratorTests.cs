using Compile.Shift.Ef;
using FluentAssertions;
using static Compile.Shift.Ef.Tests.TestModels;

namespace Compile.Shift.Ef.Tests;

/// <summary>
/// Tests for <see cref="EntityMapGenerator"/> (IEntityTypeConfiguration generation).
///
/// Intent: produce a fluent configuration per table covering ToTable, per-property
/// HasColumnName/HasColumnType/IsRequired, primary keys (+ identity), foreign keys
/// (HasOne/WithMany/HasForeignKey/HasConstraintName) and indexes (single/composite, unique).
/// </summary>
public class EntityMapGeneratorTests
{
    private readonly EntityMapGenerator _sut = new();

    [Fact]
    public void GenerateEntityMap_EmitsClassConfigureAndToTable()
    {
        var table = Table("User", new[] { Field("Id", "int", primaryKey: true) });

        var code = _sut.GenerateEntityMap(table, "My.Namespace");

        code.Should().Contain("namespace My.Namespace;");
        code.Should().Contain("public partial class UserEntityMap : IEntityTypeConfiguration<UserEntity>");
        code.Should().Contain("public void Configure(EntityTypeBuilder<UserEntity> builder)");
        code.Should().Contain("builder.ToTable(\"User\");");
    }

    [Fact]
    public void GenerateEntityMap_Property_HasColumnNameAndType()
    {
        var table = Table("User", new[] { Field("Username", "nvarchar", precision: 100) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain("builder.Property(e => e.Username)");
        code.Should().Contain(".HasColumnName(\"Username\")");
        code.Should().Contain(".HasColumnType(\"nvarchar(100)\")");
    }

    [Fact]
    public void GenerateEntityMap_NonNullableProperty_IsRequired()
    {
        var table = Table("User", new[] { Field("Username", "nvarchar", precision: 100) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain(".IsRequired();");
    }

    [Fact]
    public void GenerateEntityMap_NullableProperty_IsNotRequired()
    {
        var table = Table("User", new[] { Field("Nickname", "nvarchar", precision: 100, nullable: true) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().NotContain(".IsRequired()");
    }

    [Fact]
    public void GenerateEntityMap_PrimaryKey_AddsHasKey()
    {
        var table = Table("User", new[] { Field("Id", "int", primaryKey: true) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain("builder.HasKey(e => e.Id);");
    }

    [Fact]
    public void GenerateEntityMap_IdentityPrimaryKey_AddsValueGeneratedOnAdd()
    {
        var table = Table("User", new[] { Field("Id", "int", primaryKey: true, identity: true) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain("builder.Property(e => e.Id).ValueGeneratedOnAdd();");
    }

    [Fact]
    public void GenerateEntityMap_NonIdentityPrimaryKey_DoesNotAddValueGenerated()
    {
        var table = Table("User", new[] { Field("Id", "uniqueidentifier", primaryKey: true) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().NotContain("ValueGeneratedOnAdd");
    }

    [Fact]
    public void GenerateEntityMap_ForeignKey_ConfiguresRelationshipAndConstraintName()
    {
        var table = Table(
            "Order",
            fields: new[] { Field("CustomerId", "int") },
            foreignKeys: new[] { ForeignKey("CustomerId", "Customer") });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain("builder.HasOne(e => e.Customer)");
        code.Should().Contain(".WithMany()");
        code.Should().Contain(".HasForeignKey(e => e.CustomerId)");
        code.Should().Contain(".IsRequired()");
        code.Should().Contain(".HasConstraintName(\"FK_Order_Customer_CustomerId\");");
    }

    [Fact]
    public void GenerateEntityMap_NullableForeignKey_IsRequiredFalse()
    {
        var table = Table(
            "Order",
            fields: new[] { Field("CustomerId", "int", nullable: true) },
            foreignKeys: new[] { ForeignKey("CustomerId", "Customer", nullable: true) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain(".IsRequired(false)");
    }

    [Fact]
    public void GenerateEntityMap_SingleColumnIndex_EmitsHasIndex()
    {
        var table = Table(
            "User",
            fields: new[] { Field("Email", "nvarchar", precision: 256) },
            indexes: new[] { Index(unique: false, "Email") });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain("builder.HasIndex(e => e.Email)");
    }

    [Fact]
    public void GenerateEntityMap_UniqueCompositeIndex_EmitsAnonymousTypeAndIsUnique()
    {
        var table = Table(
            "User",
            fields: new[] { Field("FirstName", "nvarchar", precision: 50), Field("LastName", "nvarchar", precision: 50) },
            indexes: new[] { Index(unique: true, "FirstName", "LastName") });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain("builder.HasIndex(e => new { e.FirstName, e.LastName })");
        code.Should().Contain(".IsUnique()");
    }

    [Theory]
    [InlineData("decimal", 18, 2, "decimal(18,2)")]
    [InlineData("numeric", 10, 4, "decimal(10,4)")]
    public void GenerateEntityMap_DecimalWithScale_UsesDecimalColumnType(string type, int p, int s, string expected)
    {
        var table = Table("Product", new[] { Field("Price", type, precision: p, scale: s) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain($".HasColumnType(\"{expected}\")");
    }

    [Fact]
    public void GenerateEntityMap_DecimalPrecisionOnly_OmitsScale()
    {
        var table = Table("Product", new[] { Field("Weight", "decimal", precision: 9) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain(".HasColumnType(\"decimal(9)\")");
    }

    [Fact]
    public void GenerateEntityMap_VarcharMax_RendersMaxKeyword()
    {
        // Intent: a max-length varchar is represented in the model with Precision == -1
        // (the same convention used by the SQL loader and parser). The generated EF column
        // type must be "nvarchar(max)" — never "nvarchar(-1)", which is not valid SQL.
        var table = Table("Doc", new[] { Field("Body", "nvarchar", precision: -1) });

        var code = _sut.GenerateEntityMap(table, "N");

        code.Should().Contain(".HasColumnType(\"nvarchar(max)\")");
        code.Should().NotContain("nvarchar(-1)");
    }
}