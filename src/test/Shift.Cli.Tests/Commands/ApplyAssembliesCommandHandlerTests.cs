using Compile.Shift.Cli.Commands;
using FluentAssertions;
using Shift.Test.Framework.Infrastructure;

namespace Compile.Shift.Cli.Tests.Commands;

/// <summary>
/// Unit tests for ApplyAssembliesCommandHandler.
/// Tests the command handler that applies DMD files from assembly resources to database.
/// </summary>
public class ApplyAssembliesCommandHandlerTests : UnitTestContext<ApplyAssembliesCommandHandler>
{

    /// <summary>
    /// Tests that ApplyAssembliesCommandHandler throws FileNotFoundException when DLL file doesn't exist.
    /// </summary>
    [Fact]
    public async Task Handle_WithNonExistentDllFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var command = new ApplyAssembliesCommand(
            ConnectionString: "Server=.;Database=Test;",
            DllPaths: new[] { "./NonExistent.dll" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => Sut.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("Assembly file not found");
    }
}