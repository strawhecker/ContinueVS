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
            var mockControl = new Mock<ContinueToolWindowControl>();
            var handler = new GetWorkspaceDirsHandler(mockControl.Object);

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
            mockControl.Verify(
                c => c.SendReplyToGui(
                    It.IsAny<string>(),
                    "test-id-123",
                    It.IsAny<object>()),
                Times.Once);

            System.Diagnostics.Debug.WriteLine("[TEST] Assertion passed: SendReplyToGui was called");
        }
    }
}
