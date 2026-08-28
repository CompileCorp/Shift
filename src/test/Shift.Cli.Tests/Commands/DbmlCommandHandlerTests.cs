using Compile.Shift.Cli.Commands;
using Compile.Shift.Dbml;
using Compile.Shift.Model;
using FluentAssertions;
using MediatR;
using Moq;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for DbmlCommandHandler.
/// Tests the command handler that exports DMD/DMDX files to a DBML diagram.
/// </summary>
public class DbmlCommandHandlerTests : UnitTestContext<DbmlCommandHandler>
{
    private static DbmlCommand Command() =>
        new(DmdLocationPaths: ["./Models", "./Mixins"], OutputPath: "./Diagrams");

    [Fact]
    public async Task Handle_WithValidCommand_ShouldLoadTheModelFromTheGivenPaths()
    {
        var command = Command();
        var model = new DatabaseModel();

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(model);

        var result = await Sut.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromPathAsync(command.DmdLocationPaths), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPassTheLoadedModelAndOutputPathToTheExporter()
    {
        var command = Command();
        var model = new DatabaseModel();

        GetMockFor<IShift>()
            .Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(model);

        var mockExporter = GetMockFor<IDbmlExporter>();
        mockExporter
            .Setup(x => x.ExportAsync(It.IsAny<DatabaseModel>(), It.IsAny<string>()))
            .ReturnsAsync("./Diagrams/model.dbml");

        var result = await Sut.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        mockExporter.Verify(x => x.ExportAsync(model, command.OutputPath), Times.Once);
    }
}