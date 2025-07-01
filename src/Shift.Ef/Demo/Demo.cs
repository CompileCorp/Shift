using Compile.Shift.Model;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Ef.Demo;

/// <summary>
/// Demonstration of Shift.Ef code generation with a sample model
/// </summary>
public class Demo
{
    public static async Task RunDemoAsync()
    {
        using var loggerFactory = LoggerFactory.Create(builder => 
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<Demo>();
        
        // Create a sample database model
        var model = CreateSampleDatabaseModel();
        
        // Generate Entity Framework code with custom options
        var options = new EfCodeGenerationOptions
        {
            NamespaceName = "Demo.Generated",
            ContextClassName = "DemoDbContext",
            InterfaceName = "IDemoDbContext",
            BaseClassName = "DbContext" // Could be custom base class
        };

        var generator = new EfCodeGenerator { Logger = logger };
        await generator.GenerateEfCodeAsync(model, "./Demo/Generated", options);
        
        logger.LogInformation("Demo completed! Check the ./Demo/Generated folder for generated files.");
    }

    private static DatabaseModel CreateSampleDatabaseModel()
    {
        var model = new DatabaseModel();

        // Create Client table
        var clientTable = new TableModel
        {
            Name = "Client",
            Fields = new List<FieldModel>
            {
                new FieldModel
                {
                    Name = "ClientId",
                    Type = "int",
                    IsPrimaryKey = true,
                    IsIdentity = true,
                    IsNullable = false
                },
                new FieldModel
                {
                    Name = "Name",
                    Type = "nvarchar",
                    Precision = 100,
                    IsNullable = false
                },
                new FieldModel
                {
                    Name = "Email",
                    Type = "nvarchar",
                    Precision = 255,
                    IsNullable = true
                },
                new FieldModel
                {
                    Name = "CreatedDate",
                    Type = "datetime2",
                    IsNullable = false
                },
                new FieldModel
                {
                    Name = "IsActive",
                    Type = "bit",
                    IsNullable = false
                }
            },
            Indexes = new List<IndexModel>
            {
                new IndexModel
                {
                    Fields = new List<string> { "Email" },
                    IsUnique = true
                }
            }
        };

        // Create Order table
        var orderTable = new TableModel
        {
            Name = "Order",
            Fields = new List<FieldModel>
            {
                new FieldModel
                {
                    Name = "OrderId",
                    Type = "int",
                    IsPrimaryKey = true,
                    IsIdentity = true,
                    IsNullable = false
                },
                new FieldModel
                {
                    Name = "ClientId",
                    Type = "int",
                    IsNullable = false
                },
                new FieldModel
                {
                    Name = "OrderNumber",
                    Type = "nvarchar",
                    Precision = 50,
                    IsNullable = false
                },
                new FieldModel
                {
                    Name = "Total",
                    Type = "decimal",
                    Precision = 18,
                    Scale = 2,
                    IsNullable = false
                },
                new FieldModel
                {
                    Name = "OrderDate",
                    Type = "datetime2",
                    IsNullable = false
                }
            },
            ForeignKeys = new List<ForeignKeyModel>
            {
                new ForeignKeyModel
                {
                    ColumnName = "ClientId",
                    TargetTable = "Client",
                    TargetColumnName = "ClientId",
                    IsNullable = false,
                    RelationshipType = RelationshipType.OneToMany
                }
            },
            Indexes = new List<IndexModel>
            {
                new IndexModel
                {
                    Fields = new List<string> { "OrderNumber" },
                    IsUnique = true
                },
                new IndexModel
                {
                    Fields = new List<string> { "ClientId" },
                    IsUnique = false
                }
            }
        };

        model.Tables.Add("Client", clientTable);
        model.Tables.Add("Order", orderTable);

        return model;
    }
}