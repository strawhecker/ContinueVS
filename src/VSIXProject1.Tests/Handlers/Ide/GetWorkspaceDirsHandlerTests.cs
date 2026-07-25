using ContinueVS.Handlers.Ide;
using ContinueVS.IPC;
using ContinueVS.UI;
using Moq;
using Xunit;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Tests.Handlers.Ide
{
    public class GetWorkspaceDirsHandlerTests
    {
        [Fact]
        public async Task HandleAsync_WithValidMessage_SendsReplyWithWorkspaceDirs()
        {
            // Arrange
            var mockGuiReply = new Mock<IGuiReplyProvider>();
            var mockWorkspaceProvider = new Mock<IWorkspacePathProvider>();
            mockWorkspaceProvider
                .Setup(w => w.GetWorkspaceDirectoriesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "/home/user/project" });

            var handler = new GetWorkspaceDirsHandler(mockGuiReply.Object, mockWorkspaceProvider.Object);

            var message = new Message
            {
                MessageType = "getWorkspaceDirs",
                MessageId = "test-id-123",
                Data = JToken.FromObject("")
            };

            // Act
            System.Diagnostics.Debug.WriteLine("[TEST] GetWorkspaceDirsHandler test starting");
            await handler.HandleAsync(message, CancellationToken.None);
            System.Diagnostics.Debug.WriteLine("[TEST] GetWorkspaceDirsHandler test completed");

            // Assert
            // Verify SendReplyToGui was called
            mockGuiReply.Verify(
                c => c.SendReplyToGui(
                    It.IsAny<string>(),
                    "test-id-123",
                    It.IsAny<object>()),
                Times.Once);

            System.Diagnostics.Debug.WriteLine("[TEST] Assertion passed: SendReplyToGui was called");
        }
    }
}
