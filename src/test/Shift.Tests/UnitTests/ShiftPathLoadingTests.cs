using Compile.Shift.Model;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace Compile.Shift.UnitTests;

/// <summary>
/// Unit tests for <see cref="Shift"/> file-loading behaviour: handling of missing directories,
/// foreign-key type normalization, and the unimplemented SaveToPathAsync stub.
/// </summary>
public class ShiftPathLoadingTests : IDisposable
{
    private readonly Shift _shift = new() { Logger = NullLogger.Instance };
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ShiftPathTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task LoadFromPathAsync_MissingDirectory_ReturnsEmptyModel()
    {
        var missing = Path.Combine(_dir, "does-not-exist");

        var model = await _shift.LoadFromPathAsync(new[] { missing });

        model.Tables.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadFromPathAsync_NormalizesForeignKeyTypeToTargetPrimaryKeyType()
    {
        // User has a GUID primary key; Order references User. After loading, the Order FK column
        // (which the parser defaults to int) must be realigned to the target PK type.
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "User.dmd"),
            "model User guid {\n  string(50) Name\n}");
        await File.WriteAllTextAsync(Path.Combine(_dir, "Order.dmd"),
            "model Order {\n  model User\n}");

        var model = await _shift.LoadFromPathAsync(new[] { _dir });

        var order = model.Tables["Order"];
        var fkField = order.Fields.Single(f => f.Name == "UserID");
        fkField.Type.Should().Be("uniqueidentifier");
        fkField.Precision.Should().BeNull();
        fkField.Scale.Should().BeNull();
    }

    [Fact]
    public void SaveToPathAsync_IsNotImplemented_Throws()
    {
        var act = () => _shift.SaveToPathAsync();

        act.Should().Throw<NotImplementedException>();
    }
}