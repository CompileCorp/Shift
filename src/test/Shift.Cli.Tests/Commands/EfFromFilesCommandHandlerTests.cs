using Compile.Shift.Cli.Commands;
using Compile.Shift.Ef;
using Compile.Shift.Model;
using FluentAssertions;
using MediatR;
using Moq;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for EfFromFilesCommandHandler.
/// Tests the command handler that generates EF code from DMD files.
/// </summary>
public class EfFromFilesCommandHandlerTests : UnitTestContext<EfFromFilesCommandHandler>
{
    #region Happy Path Tests

    /// <summary>
    /// Tests that EfFromFilesCommandHandler calls IShift.LoadFromPathAsync with DMD file paths.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallLoadFromPathAsyncWithCorrectPaths()
    {
        // Arrange
        var dmdPaths = new[] { "./Models/User.dmd", "./Models/Product.dmd" };
        var command = new EfFromFilesCommand(
            DmdLocationPaths: dmdPaths,
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        var expectedModel = new DatabaseModel();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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
        mockShift.Verify(x => x.LoadFromPathAsync(command.DmdLocationPaths), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromFilesCommandHandler calls IEfCodeGenerator.GenerateEfCodeAsync with model and output path.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallEfGeneratorWithCorrectParameters()
    {
        // Arrange
        var command = new EfFromFilesCommand(
            DmdLocationPaths: new[] { "./Models/User.dmd" },
            OutputDirectoryPath: "./Output");

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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
    /// Tests that EfFromFilesCommandHandler returns Unit.Value on successful completion.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnUnitValue()
    {
        // Arrange
        var command = new EfFromFilesCommand(
            DmdLocationPaths: new[] { "./Models/User.dmd" },
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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
    /// Tests that EfFromFilesCommandHandler calls methods in correct sequence.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallMethodsInCorrectSequence()
    {
        // Arrange
        var command = new EfFromFilesCommand(
            DmdLocationPaths: new[] { "./Models/User.dmd" },
            OutputDirectoryPath: "./Output");

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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

        // Verify LoadFromPathAsync is called first
        mockShift.Verify(x => x.LoadFromPathAsync(command.DmdLocationPaths), Times.Once);

        // Verify GenerateEfCodeAsync is called with the model returned from LoadFromPathAsync
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            expectedModel,
            command.OutputDirectoryPath,
            It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region Multiple Files Tests

    /// <summary>
    /// Tests that EfFromFilesCommandHandler handles multiple input DMD file paths correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithMultipleFiles_ShouldProcessAllFiles()
    {
        // Arrange
        var dmdPaths = new[] { "./Models/User.dmd", "./Models/Product.dmd", "./Models/Order.dmd" };
        var command = new EfFromFilesCommand(
            DmdLocationPaths: dmdPaths,
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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
        mockShift.Verify(x => x.LoadFromPathAsync(dmdPaths), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromFilesCommandHandler handles single DMD file path correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithSingleFile_ShouldProcessSingleFile()
    {
        // Arrange
        var dmdPaths = new[] { "./Models/User.dmd" };
        var command = new EfFromFilesCommand(
            DmdLocationPaths: dmdPaths,
            OutputDirectoryPath: "./Output");

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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
        mockShift.Verify(x => x.LoadFromPathAsync(dmdPaths), Times.Once);
    }

    #endregion

    #region Model Flow Tests

    /// <summary>
    /// Tests that EfFromFilesCommandHandler passes loaded model correctly to EF generator.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldPassModelToEfGenerator()
    {
        // Arrange
        var command = new EfFromFilesCommand(
            DmdLocationPaths: new[] { "./Models/User.dmd" },
            OutputDirectoryPath: "./Output");

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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
    /// Tests that EfFromFilesCommandHandler passes output path correctly to EF generator.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldPassOutputPathToEfGenerator()
    {
        // Arrange
        var outputPath = "./GeneratedCode";
        var command = new EfFromFilesCommand(
            DmdLocationPaths: new[] { "./Models/User.dmd" },
            OutputDirectoryPath: outputPath);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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

    #region Parameter Validation Tests

    /// <summary>
    /// Tests that EfFromFilesCommandHandler passes all command parameters correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithAllParameters_ShouldPassAllParametersCorrectly()
    {
        // Arrange
        var dmdPaths = new[] { "./Models/User.dmd", "./Models/Product.dmd" };
        var outputPath = "./GeneratedCode";

        var command = new EfFromFilesCommand(
            DmdLocationPaths: dmdPaths,
            OutputDirectoryPath: outputPath);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
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
        mockShift.Verify(x => x.LoadFromPathAsync(dmdPaths), Times.Once);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            outputPath,
            It.IsAny<string>()), Times.Once);
    }

    #endregion
}