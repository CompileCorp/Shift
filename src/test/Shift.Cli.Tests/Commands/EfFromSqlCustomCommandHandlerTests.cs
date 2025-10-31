using Compile.Shift.Cli.Commands;
using Compile.Shift.Ef;
using Compile.Shift.Model;
using FluentAssertions;
using MediatR;
using Moq;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for EfFromSqlCustomCommandHandler.
/// Tests the command handler that generates EF code from SQL Server with custom options.
/// </summary>
public class EfFromSqlCustomCommandHandlerTests : UnitTestContext<EfFromSqlCustomCommandHandler>
{
    #region Happy Path Tests

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler calls IShift.LoadFromSqlAsync with connection string and schema.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallLoadFromSqlAsyncWithCorrectParameters()
    {
        // Arrange
        var options = new EfCodeGenerationOptions();
        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        var expectedModel = new DatabaseModel();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromSqlAsync(command.ConnectionString, command.Schema), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler calls IEfCodeGenerator.GenerateEfCodeAsync with custom options.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallEfGeneratorWithCorrectParameters()
    {
        // Arrange
        var options = new EfCodeGenerationOptions();
        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            expectedModel,
            command.OutputDirectoryPath,
            options), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler returns Unit.Value on successful completion.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnUnitValue()
    {
        // Arrange
        var options = new EfCodeGenerationOptions();
        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler calls methods in correct sequence.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallMethodsInCorrectSequence()
    {
        // Arrange
        var options = new EfCodeGenerationOptions();
        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
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
            options), Times.Once);
    }

    #endregion

    #region Options Handling Tests

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler passes EfCodeGenerationOptions correctly to EF generator.
    /// </summary>
    [Fact]
    public async Task Handle_WithCustomOptions_ShouldPassOptionsToEfGenerator()
    {
        // Arrange
        var options = new EfCodeGenerationOptions
        {
            NamespaceName = "MyApp.Data",
            ContextClassName = "MyDbContext",
            InterfaceName = "IMyDbContext",
            BaseClassName = "BaseDbContext"
        };

        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            "./Output",
            options), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler handles various namespace options correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithNamespaceOptions_ShouldPassNamespaceToEfGenerator()
    {
        // Arrange
        var options = new EfCodeGenerationOptions
        {
            NamespaceName = "CustomNamespace.Data"
        };

        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            "./Output",
            options), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler handles context class name options correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithContextOptions_ShouldPassContextToEfGenerator()
    {
        // Arrange
        var options = new EfCodeGenerationOptions
        {
            ContextClassName = "CustomDbContext"
        };

        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            "./Output",
            options), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler handles interface name options correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithInterfaceOptions_ShouldPassInterfaceToEfGenerator()
    {
        // Arrange
        var options = new EfCodeGenerationOptions
        {
            InterfaceName = "ICustomDbContext"
        };

        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            "./Output",
            options), Times.Once);
    }

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler handles base class name options correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithBaseClassOptions_ShouldPassBaseClassToEfGenerator()
    {
        // Arrange
        var options = new EfCodeGenerationOptions
        {
            BaseClassName = "CustomBaseDbContext"
        };

        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            "./Output",
            options), Times.Once);
    }

    #endregion

    #region Schema Handling Tests

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler uses custom schema when provided.
    /// </summary>
    [Fact]
    public async Task Handle_WithCustomSchema_ShouldUseCustomSchema()
    {
        // Arrange
        var options = new EfCodeGenerationOptions();
        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "custom",
            OutputDirectoryPath: "./Output",
            Options: options);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DatabaseModel());

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromSqlAsync(command.ConnectionString, "custom"), Times.Once);
    }

    #endregion

    #region Model Flow Tests

    /// <summary>
    /// Tests that EfFromSqlCustomCommandHandler passes loaded model correctly to EF generator.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldPassModelToEfGenerator()
    {
        // Arrange
        var options = new EfCodeGenerationOptions();
        var command = new EfFromSqlCustomCommand(
            ConnectionString: "Server=.;Database=Test;",
            Schema: "dbo",
            OutputDirectoryPath: "./Output",
            Options: options);

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedModel);

        var mockEfGenerator = GetMockFor<IEfCodeGenerator>();
        mockEfGenerator.Setup(x => x.GenerateEfCodeAsync(
            It.IsAny<DatabaseModel>(),
            It.IsAny<string>(),
            It.IsAny<EfCodeGenerationOptions>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockEfGenerator.Verify(x => x.GenerateEfCodeAsync(
            expectedModel,
            command.OutputDirectoryPath,
            options), Times.Once);
    }

    #endregion
}