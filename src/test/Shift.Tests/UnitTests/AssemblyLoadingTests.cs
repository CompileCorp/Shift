using Compile.Shift.Model;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Compile.Shift.UnitTests;

public class AssemblyLoadingTests
{
    private readonly ILogger<Shift> _logger;
    private readonly Shift _shift;

    public AssemblyLoadingTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<Shift>();
        _shift = new Shift { Logger = _logger };
    }

    [Fact]
    public async Task LoadFromAssembly_ShouldLoadEmbeddedResources()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var model = await _shift.LoadFromAssembly(assembly);

        // Assert
        Assert.NotNull(model);

        // Should have loaded the Auditable mixin
        Assert.True(model.Mixins.ContainsKey("Auditable"));
        var auditableMixin = model.Mixins["Auditable"];
        Assert.Equal("Auditable", auditableMixin.Name);
        Assert.Contains(auditableMixin.Fields, f => f.Name == "CreatedDateTime");
        Assert.Contains(auditableMixin.Fields, f => f.Name == "LastModifiedDateTime");

        // Should have loaded the User model
        Assert.True(model.Tables.ContainsKey("User"));
        var userTable = model.Tables["User"];
        Assert.Equal("User", userTable.Name);
        Assert.Contains(userTable.Fields, f => f.Name == "Username");
        Assert.Contains(userTable.Fields, f => f.Name == "Email");
        Assert.True(userTable.Fields.Where(x => x.IsPrimaryKey).All(x => x.Type == "uniqueidentifier"));

        // Should have loaded the Task model with Auditable mixin applied
        Assert.True(model.Tables.ContainsKey("Task"));
        var taskTable = model.Tables["Task"];
        Assert.Equal("Task", taskTable.Name);
        Assert.Contains(taskTable.Fields, f => f.Name == "Title");
        Assert.Contains(taskTable.Fields, f => f.Name == "Description");
        Assert.True(taskTable.Fields.Where(x => x.IsPrimaryKey).All(x => x.Type == "int"));

        // Should have mixin fields applied
        Assert.Contains(taskTable.Fields, f => f.Name == "CreatedDateTime");
        Assert.Contains(taskTable.Fields, f => f.Name == "LastModifiedDateTime");
        Assert.Contains(taskTable.Mixins, m => m == "Auditable");

        // Should also have loaded resources from OtherNamespace (when no filter is applied)
        Assert.True(model.Mixins.ContainsKey("SoftDelete"));
        Assert.True(model.Tables.ContainsKey("Product"));
    }

    [Fact]
    public async Task LoadFromAssembliesAsync_ShouldRespectOrderAndPriority()
    {
        // Arrange
        var assembly1 = Assembly.GetExecutingAssembly();
        var assembly2 = Assembly.GetExecutingAssembly(); // Same assembly for simplicity
        var assemblies = new[] { assembly1, assembly2 };

        // Act
        var model = await _shift.LoadFromAssembliesAsync(assemblies);

        // Assert
        Assert.NotNull(model);

        // Mixins and tables should be loaded only once (first assembly wins)
        Assert.Single(model.Mixins, m => m.Key == "Auditable");
        Assert.Single(model.Tables, t => t.Key == "User");
        Assert.Single(model.Tables, t => t.Key == "Task");
    }

    [Fact]
    public async Task LoadFromAssembly_ShouldHandleEmptyAssembly()
    {
        // Arrange - create a mock assembly with no embedded resources
        var emptyAssembly = typeof(string).Assembly; // System.Private.CoreLib has no DMD/DMDX files

        // Act
        var model = await _shift.LoadFromAssembly(emptyAssembly);

        // Assert
        Assert.NotNull(model);
        Assert.Empty(model.Mixins);
        Assert.Empty(model.Tables);
    }

    [Fact]
    public void GetManifestResourceNames_ShouldReturnEmbeddedResources()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var resourceNames = assembly.GetManifestResourceNames();

        // Assert
        Assert.Contains(resourceNames, name => name.EndsWith("Auditable.dmdx"));
        Assert.Contains(resourceNames, name => name.EndsWith("User.dmd"));
        Assert.Contains(resourceNames, name => name.EndsWith("Task.dmd"));
    }

    [Fact]
    public async Task LoadFromAssembly_ShouldLoadMixinsBeforeModels()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var model = await _shift.LoadFromAssembly(assembly);

        // Assert
        // The Task model should have the Auditable mixin properly applied
        Assert.True(model.Tables.ContainsKey("Task"));
        var taskTable = model.Tables["Task"];

        // Verify mixin was applied (mixin fields should be present)
        Assert.Contains(taskTable.Fields, f => f.Name == "CreatedDateTime");
        Assert.Contains(taskTable.Fields, f => f.Name == "LastModifiedDateTime");
        Assert.Contains(taskTable.Fields, f => f.Name == "LockNumber");
        Assert.Contains(taskTable.Mixins, m => m == "Auditable");
    }

    [Fact]
    public async Task LoadFromAssembly_WithNamespaceFilter_ShouldFilterOutOtherNamespace()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // First verify both namespaces exist in the assembly
        var allResourceNames = assembly.GetManifestResourceNames();
        Assert.Contains(allResourceNames, name => name.Contains("TestResources") && name.EndsWith(".dmd"));
        Assert.Contains(allResourceNames, name => name.Contains("OtherNamespace") && name.EndsWith(".dmd"));

        // Act - Load only from TestResources namespace
        var model = await _shift.LoadFromAssembly(
            assembly,
            new[] { "Compile.Shift.TestResources" });

        // Assert
        Assert.NotNull(model);

        // Should have loaded resources from TestResources namespace
        Assert.True(model.Mixins.ContainsKey("Auditable"));
        Assert.True(model.Tables.ContainsKey("User"));
        Assert.True(model.Tables.ContainsKey("Task"));

        // Should NOT have loaded resources from OtherNamespace
        Assert.False(model.Mixins.ContainsKey("SoftDelete"), "SoftDelete mixin from OtherNamespace should be filtered out");
        Assert.False(model.Tables.ContainsKey("Product"), "Product table from OtherNamespace should be filtered out");
    }

    [Fact]
    public async Task LoadFromAssembly_WithMultipleNamespaces_ShouldLoadFromBoth()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act - Load from both namespaces
        var model = await _shift.LoadFromAssembly(
            assembly,
            new[] { "Compile.Shift.TestResources", "Compile.Shift.OtherNamespace" });

        // Assert
        Assert.NotNull(model);

        // Should have loaded resources from TestResources namespace
        Assert.True(model.Mixins.ContainsKey("Auditable"));
        Assert.True(model.Tables.ContainsKey("User"));
        Assert.True(model.Tables.ContainsKey("Task"));

        // Should also have loaded resources from OtherNamespace
        Assert.True(model.Mixins.ContainsKey("SoftDelete"));
        Assert.True(model.Tables.ContainsKey("Product"));
    }
}