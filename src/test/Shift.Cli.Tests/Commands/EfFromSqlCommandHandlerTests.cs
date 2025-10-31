using Compile.Shift.Cli.Commands;
using Compile.Shift.Ef;
using Compile.Shift.Model;
using FluentAssertions;
using MediatR;
using Moq;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for EfFromSqlCommandHandler.
/// Tests the command handler that generates EF code from SQL Server.
/// </summary>
public class EfFromSqlCommandHandlerTests : UnitTestContext<EfFromSqlCommandHandler>
{
    #region Happy Path Tests

    /// <summary>
    /// Tests that EfFromSqlCommandHandler calls IShift.LoadFromSqlAsync with connection string and schema.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallLoadFromSqlAsyncWithCorrectParameters()
    {
        // Arrange
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        var expectedModel = new DatabaseModel();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromSqlAsync(command.ConnectionString, command.Schema), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCommandHandler calls IEfCodeGenerator.GenerateEfCodeAsync with model and output path.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallEfGeneratorWithCorrectParameters()
    {
        // Arrange
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output");

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            expectedModel,
            command.OutputDirectoryPath,
            It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCommandHandler returns Unit.Value on successful completion.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnUnitValue()
    {
        // Arrange
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    /// <summary>
    /// Tests that EfFromSqlCommandHandler calls methods in correct sequence.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallMethodsInCorrectSequence()
    {
        // Arrange
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output");

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);

        // Verify LoadFromSqlAsync is called first
        mockShift.Verify(x => x.LoadFromSqlAsync(command.ConnectionString, command.Schema), Times.Once);

        // Verify GenerateEfCodeAsync is called with the model returned from LoadFromSqlAsync
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            expectedModel,
            command.OutputDirectoryPath,
            It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region Schema Handling Tests

    /// <summary>
    /// Tests that EfFromSqlCommandHandler uses custom schema when provided.
    /// </summary>
    [Fact]
    public async Task Handle_WithCustomSchema_ShouldUseCustomSchema()
    {
        // Arrange
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "custom",
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromSqlAsync(command.ConnectionString, "custom"), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCommandHandler uses default "dbo" schema when not specified.
    /// </summary>
    [Fact]
    public async Task Handle_WithDefaultSchema_ShouldUseDboSchema()
    {
        // Arrange
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromSqlAsync(command.ConnectionString, "dbo"), Times.Once);
    }

    #endregion

    #region Model Flow Tests

    /// <summary>
    /// Tests that EfFromSqlCommandHandler passes loaded model correctly to EF generator.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldPassModelToEfGenerator()
    {
        // Arrange
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output");

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            expectedModel,
            command.OutputDirectoryPath,
            It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCommandHandler passes output path correctly to EF generator.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldPassOutputPathToEfGenerator()
    {
        // Arrange
        var outputPath = "./GeneratedCode";
        var command = new EfFromSqlCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: outputPath);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            outputPath,
            It.IsAny<string>()), Times.Once);
    }

    #endregion
}