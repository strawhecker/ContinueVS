#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests for workflow policy behavior integration (gap27_14).
    /// Verifies that ExecuteToolAsync correctly enforces Auto, Interactive, and Bypass policies.
    /// </summary>
    public class PolicyBehaviorTests
    {
        private static WorkflowService CreateWorkflowService(
            IToolService? toolService = null,
            INotificationService? notificationService = null)
        {
            var tool = toolService ?? CreateMockToolService().Object;
            var notif = notificationService ?? new Mock<INotificationService>().Object;
            return new WorkflowService(tool, notif);
        }

        private static Mock<IToolService> CreateMockToolService()
        {
            var mock = new Mock<IToolService>();
            mock.Setup(x => x.InvokeAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
                .ReturnsAsync(new ToolResult
                {
                    ToolName = "test_tool",
                    Output = "Test output",
                    IsSuccess = true
                });
            return mock;
        }

        [Fact]
        public async Task ExecuteToolAsync_Auto_Policy_Executes_Immediately_And_Returns_Result()
        {
            // Arrange
            var toolServiceMock = CreateMockToolService();
            var notificationServiceMock = new Mock<INotificationService>();
            var workflowService = new WorkflowService(toolServiceMock.Object, notificationServiceMock.Object);

            await workflowService.SetContinuationPolicyAsync(ContinuationPolicy.Auto);

            var toolCall = new ToolCall
            {
                Id = "call-1",
                Name = "test_tool",
                Arguments = new Dictionary<string, object> { { "param", "value" } }
            };

            // Act
            var result = await workflowService.ExecuteToolAsync(toolCall);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal("test_tool", result.ToolName);
            toolServiceMock.Verify(x => x.InvokeAsync("test_tool", It.IsAny<IDictionary<string, object>>()), Times.Once);
            notificationServiceMock.Verify(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteToolAsync_Interactive_Policy_Shows_Confirmation_And_Executes_When_Approved()
        {
            // Arrange
            var toolServiceMock = CreateMockToolService();
            var notificationServiceMock = new Mock<INotificationService>();
            notificationServiceMock
                .Setup(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var workflowService = new WorkflowService(toolServiceMock.Object, notificationServiceMock.Object);
            await workflowService.SetContinuationPolicyAsync(ContinuationPolicy.Interactive);

            var toolCall = new ToolCall
            {
                Id = "call-2",
                Name = "test_tool",
                Arguments = new Dictionary<string, object> { { "param", "value" } }
            };

            // Act
            var result = await workflowService.ExecuteToolAsync(toolCall);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            notificationServiceMock.Verify(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            toolServiceMock.Verify(x => x.InvokeAsync("test_tool", It.IsAny<IDictionary<string, object>>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteToolAsync_Interactive_Policy_Skips_Execution_When_User_Declines()
        {
            // Arrange
            var toolServiceMock = CreateMockToolService();
            var notificationServiceMock = new Mock<INotificationService>();
            notificationServiceMock
                .Setup(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            var workflowService = new WorkflowService(toolServiceMock.Object, notificationServiceMock.Object);
            await workflowService.SetContinuationPolicyAsync(ContinuationPolicy.Interactive);

            var toolCall = new ToolCall
            {
                Id = "call-3",
                Name = "test_tool",
                Arguments = new Dictionary<string, object> { { "param", "value" } }
            };

            // Act
            var result = await workflowService.ExecuteToolAsync(toolCall);

            // Assert
            Assert.Null(result);
            notificationServiceMock.Verify(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            toolServiceMock.Verify(x => x.InvokeAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteToolAsync_Bypass_Policy_Executes_Without_Confirmation()
        {
            // Arrange
            var toolServiceMock = CreateMockToolService();
            var notificationServiceMock = new Mock<INotificationService>();
            var workflowService = new WorkflowService(toolServiceMock.Object, notificationServiceMock.Object);

            await workflowService.SetContinuationPolicyAsync(ContinuationPolicy.Bypass);

            var toolCall = new ToolCall
            {
                Id = "call-4",
                Name = "test_tool",
                Arguments = new Dictionary<string, object> { { "param", "value" } }
            };

            // Act
            var result = await workflowService.ExecuteToolAsync(toolCall);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            toolServiceMock.Verify(x => x.InvokeAsync("test_tool", It.IsAny<IDictionary<string, object>>()), Times.Once);
            notificationServiceMock.Verify(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteToolAsync_Policy_Override_Takes_Precedence_Over_Current_Policy()
        {
            // Arrange
            var toolServiceMock = CreateMockToolService();
            var notificationServiceMock = new Mock<INotificationService>();
            notificationServiceMock
                .Setup(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var workflowService = new WorkflowService(toolServiceMock.Object, notificationServiceMock.Object);

            // Set current policy to Auto, but override with Interactive
            await workflowService.SetContinuationPolicyAsync(ContinuationPolicy.Auto);

            var toolCall = new ToolCall
            {
                Id = "call-5",
                Name = "test_tool",
                Arguments = new Dictionary<string, object> { { "param", "value" } }
            };

            // Act
            var result = await workflowService.ExecuteToolAsync(toolCall, ContinuationPolicy.Interactive);

            // Assert
            Assert.NotNull(result);
            // Confirmation should be shown because we explicitly passed Interactive policy
            notificationServiceMock.Verify(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            toolServiceMock.Verify(x => x.InvokeAsync("test_tool", It.IsAny<IDictionary<string, object>>()), Times.Once);
        }
    }
}
