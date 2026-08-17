#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelAgentModeTests
    {
        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            var mock = new Mock<ILlmService>();
            return mock;
        }

        private static Mock<IContextService> CreateContextServiceMock()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private static Mock<IToolService> CreateToolServiceMock()
        {
            var mock = new Mock<IToolService>();
            return mock;
        }

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            var mock = new Mock<INotificationService>();
            return mock;
        }

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo 
                    { 
                        Name = "Llama 3.1",
                        Provider = "ollama",
                        BaseUrl = "http://localhost:11434"
                    }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            return mock;
        }

        private static Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns("Test system prompt");
            return mock;
        }

        [Fact]
        public void AgentMode_IsAvailable()
        {
            // Arrange & Act
            var statuses = Enum.GetNames(typeof(ChatMode));

            // Assert
            Assert.Contains("Agent", statuses);
        }

        [Fact]
        public void CurrentMode_CanBeSetToAgent()
        {
            // Arrange
            var viewModel = CreateViewModel();

            // Act
            viewModel.CurrentMode = ChatMode.Agent;

            // Assert
            Assert.Equal(ChatMode.Agent, viewModel.CurrentMode);
        }

        [Fact]
        public void ToolInvocationStatus_EnumHasAllRequiredValues()
        {
            // Arrange & Act
            var statuses = Enum.GetNames(typeof(ToolInvocationStatus));

            // Assert
            Assert.Contains("Pending", statuses);
            Assert.Contains("Running", statuses);
            Assert.Contains("Complete", statuses);
            Assert.Contains("Failed", statuses);
        }

        [Fact]
        public void ChatMessage_WithRoleTool_StoresToolInvocationStatus()
        {
            // Arrange
            var msg = new ChatMessage 
            { 
                Role = ChatMessageRole.Tool,
                Content = "Tool result"
            };

            // Act
            msg.InvocationStatus = ToolInvocationStatus.Complete;
            msg.ExecutionStartTime = DateTime.Now;
            msg.ExecutionEndTime = DateTime.Now.AddSeconds(1);

            // Assert
            Assert.Equal(ToolInvocationStatus.Complete, msg.InvocationStatus);
            Assert.NotNull(msg.ExecutionStartTime);
            Assert.NotNull(msg.ExecutionEndTime);
        }

        [Fact]
        public void ChatMessage_CanHoldToolCalls()
        {
            // Arrange
            var toolCall = new ToolCall { Name = "read_file", Id = "1" };
            var msg = new ChatMessage 
            { 
                Role = ChatMessageRole.Assistant,
                Content = "I'll read the file",
                ToolCalls = new List<ToolCall> { toolCall }
            };

            // Act & Assert
            Assert.NotNull(msg.ToolCalls);
            Assert.Single(msg.ToolCalls);
            Assert.Equal("read_file", msg.ToolCalls[0].Name);
        }

        private ChatPageViewModel CreateViewModel()
        {
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();

            return new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object);
        }
    }
}
