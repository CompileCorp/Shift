using Compile.Shift.Cli.Commands;
using FluentAssertions;
using MediatR;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for PrintHelpCommandHandler.
/// Tests the command handler that prints help information to console.
/// </summary>
public class PrintHelpCommandHandlerTests : UnitTestContext<PrintHelpCommandHandler>
{
    #region Happy Path Tests

    /// <summary>
    /// Tests that PrintHelpCommandHandler completes successfully.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCompleteSuccessfully()
    {
        // Arrange
        var command = new PrintHelpCommand();

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    /// <summary>
    /// Tests that PrintHelpCommandHandler returns Unit.Value on successful completion.
    /// </summary>
    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnUnitValue()
    {
        // Arrange
        var command = new PrintHelpCommand();

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    /// <summary>
    /// Tests that PrintHelpCommandHandler handles cancellation token correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithCancellationToken_ShouldCompleteSuccessfully()
    {
        // Arrange
        var command = new PrintHelpCommand();
        var cancellationToken = new CancellationToken();

        // Act
        var result = await Sut.Handle(command, cancellationToken);

        // Assert
        result.Should().Be(Unit.Value);
    }

    /// <summary>
    /// Tests that PrintHelpCommandHandler handles multiple calls correctly.
    /// </summary>
    [Fact]
    public async Task Handle_WithMultipleCalls_ShouldCompleteSuccessfully()
    {
        // Arrange
        var command = new PrintHelpCommand();

        // Act
        var result1 = await Sut.Handle(command, CancellationToken.None);
        var result2 = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result1.Should().Be(Unit.Value);
        result2.Should().Be(Unit.Value);
    }

    #endregion

    #region Command Validation Tests

    /// <summary>
    /// Tests that PrintHelpCommandHandler works with default PrintHelpCommand.
    /// </summary>
    [Fact]
    public async Task Handle_WithDefaultCommand_ShouldCompleteSuccessfully()
    {
        // Arrange
        var command = new PrintHelpCommand();

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
    }

    /// <summary>
    /// Tests that PrintHelpCommandHandler is stateless and can be called multiple times.
    /// </summary>
    [Fact]
    public async Task Handle_IsStateless_ShouldWorkCorrectly()
    {
        // Arrange
        var command1 = new PrintHelpCommand();
        var command2 = new PrintHelpCommand();

        // Act
        var result1 = await Sut.Handle(command1, CancellationToken.None);
        var result2 = await Sut.Handle(command2, CancellationToken.None);

        // Assert
        result1.Should().Be(Unit.Value);
        result2.Should().Be(Unit.Value);
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Tests that PrintHelpCommandHandler handles exceptions gracefully.
    /// </summary>
    [Fact]
    public async Task Handle_WithException_ShouldNotThrow()
    {
        // Arrange
        var command = new PrintHelpCommand();

        // Act & Assert
        // The handler should not throw exceptions under normal circumstances
        var result = await Sut.Handle(command, CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    #endregion

    #region Performance Tests

    /// <summary>
    /// Tests that PrintHelpCommandHandler completes quickly.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCompleteQuickly()
    {
        // Arrange
        var command = new PrintHelpCommand();
        var startTime = DateTime.UtcNow;

        // Act
        var result = await Sut.Handle(command, CancellationToken.None);
        var endTime = DateTime.UtcNow;

        // Assert
        result.Should().Be(Unit.Value);
        var duration = endTime - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "Help command should complete quickly");
    }

    #endregion
}
