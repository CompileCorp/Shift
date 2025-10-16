using Compile.Shift.Cli.Commands;
using Compile.Shift.Model;
using FluentAssertions;
using MediatR;
using Moq;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for ApplyCommandHandler.
/// Tests the command handler that applies DMD files from paths to database.
/// </summary>
public class ApplyCommandHandlerTests : UnitTestContext<ApplyCommandHandler>
{
    #region Happy Path Tests

    /// <summary>
    /// Tests that ApplyCommandHandler calls IShift.LoadFromPathAsync with correct paths.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallLoadFromPathAsyncWithCorrectPaths()
    {
        // Arrange
        var command = new ApplyCommand(
            ConnectionString: "Server=.;Database=Test;",
            ModelLocationPaths: new[] { "./Models", "./Models2" });

        var mockShift = GetMockFor<IShift>();
        var expectedModel = new DatabaseModel();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(expectedModel);
        mockShift.Setup(x => x.ApplyToSqlAsync(It.IsAny<DatabaseModel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromPathAsync(command.ModelLocationPaths), Times.Once);
    }

    /// <summary>
    /// Tests that ApplyCommandHandler calls IShift.ApplyToSqlAsync with loaded model and connection string.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallApplyToSqlAsyncWithCorrectParameters()
    {
        // Arrange
        var command = new ApplyCommand(
            ConnectionString: "Server=.;Database=Test;",
            ModelLocationPaths: new[] { "./Models" });

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(expectedModel);
        mockShift.Setup(x => x.ApplyToSqlAsync(It.IsAny<DatabaseModel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.ApplyToSqlAsync(
            expectedModel, 
            command.ConnectionString, 
            "dbo"), Times.Once);
    }

    /// <summary>
    /// Tests that ApplyCommandHandler returns Unit.Value on successful completion.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnUnitValue()
    {
        // Arrange
        var command = new ApplyCommand(
            ConnectionString: "Server=.;Database=Test;",
            ModelLocationPaths: new[] { "./Models" });

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new DatabaseModel());
        mockShift.Setup(x => x.ApplyToSqlAsync(It.IsAny<DatabaseModel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    /// <summary>
    /// Tests that ApplyCommandHandler calls both methods in correct sequence.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCallMethodsInCorrectSequence()
    {
        // Arrange
        var command = new ApplyCommand(
            ConnectionString: "Server=.;Database=Test;",
            ModelLocationPaths: new[] { "./Models" });

        var expectedModel = new DatabaseModel();
        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(expectedModel);
        mockShift.Setup(x => x.ApplyToSqlAsync(It.IsAny<DatabaseModel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        
        // Verify LoadFromPathAsync is called first
        mockShift.Verify(x => x.LoadFromPathAsync(command.ModelLocationPaths), Times.Once);
        
        // Verify ApplyToSqlAsync is called with the model returned from LoadFromPathAsync
        mockShift.Verify(x => x.ApplyToSqlAsync(
            expectedModel, 
            command.ConnectionString, 
            "dbo"), Times.Once);
    }

    #endregion

    #region Parameter Validation Tests

    /// <summary>
    /// Tests that ApplyCommandHandler passes multiple model paths correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithMultiplePaths_ShouldPassAllPathsToLoadFromPathAsync()
    {
        // Arrange
        var paths = new[] { "./Models/User.dmd", "./Models/Product.dmd", "./Models/Order.dmd" };
        var command = new ApplyCommand(
            ConnectionString: "Server=.;Database=Test;",
            ModelLocationPaths: paths);

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new DatabaseModel());
        mockShift.Setup(x => x.ApplyToSqlAsync(It.IsAny<DatabaseModel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.LoadFromPathAsync(paths), Times.Once);
    }

    /// <summary>
    /// Tests that ApplyCommandHandler uses default schema "dbo" when applying to SQL.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldUseDefaultSchemaDbo()
    {
        // Arrange
        var command = new ApplyCommand(
            ConnectionString: "Server=.;Database=Test;",
            ModelLocationPaths: new[] { "./Models" });

        var mockShift = GetMockFor<IShift>();
        mockShift.Setup(x => x.LoadFromPathAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new DatabaseModel());
        mockShift.Setup(x => x.ApplyToSqlAsync(It.IsAny<DatabaseModel>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        mockShift.Verify(x => x.ApplyToSqlAsync(
            It.IsAny<DatabaseModel>(), 
            command.ConnectionString, 
            "dbo"), Times.Once);
    }

    #endregion
}
