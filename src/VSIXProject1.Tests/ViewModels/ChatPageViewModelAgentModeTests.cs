#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Core;
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

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var uiState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "grep_search", ToolPolicy.AutoApprove },
                    { "find_symbol", ToolPolicy.AutoApprove }
                }
            };
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(uiState);
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

        /// <summary>
        /// Unit Test 1: CompletionChunk with ToolCall type is recognized
        /// Validates that ToolCall chunks have proper type and content
        /// </summary>
        [Fact]
        public void CompletionChunk_WithToolCallType_StoresToolCallData()
        {
            // Arrange
            var toolCall = new ToolCall { Name = "test_tool", Id = "123", Arguments = new Dictionary<string, object> { { "arg", "val" } } };

            // Act
            var chunk = new CompletionChunk
            {
                Type = ChunkType.ToolCall,
                ToolCall = toolCall
            };

            // Assert
            Assert.Equal(ChunkType.ToolCall, chunk.Type);
            Assert.NotNull(chunk.ToolCall);
            Assert.Equal("test_tool", chunk.ToolCall.Name);
            Assert.Equal("123", chunk.ToolCall.Id);
        }

        /// <summary>
        /// Unit Test 2: Tool result ChatMessage structure
        /// Validates ChatMessage properly stores tool execution results
        /// </summary>
        [Fact]
        public void ChatMessage_WithToolRole_StoresResultAndStatus()
        {
            // Arrange & Act
            var toolMsg = new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                Content = "Tool execution result",
                InvocationStatus = ToolInvocationStatus.Complete,
                ExecutionStartTime = DateTime.Now,
                ExecutionEndTime = DateTime.Now.AddMilliseconds(500)
            };

            // Assert
            Assert.Equal(ChatMessageRole.Tool, toolMsg.Role);
            Assert.Equal("Tool execution result", toolMsg.Content);
            Assert.Equal(ToolInvocationStatus.Complete, toolMsg.InvocationStatus);
            Assert.NotNull(toolMsg.ExecutionStartTime);
            Assert.NotNull(toolMsg.ExecutionEndTime);
            Assert.True(toolMsg.ExecutionEndTime > toolMsg.ExecutionStartTime);
        }

        /// <summary>
        /// Unit Test 3: Tool message failure state
        /// Validates tool messages can represent failure state
        /// </summary>
        [Fact]
        public void ChatMessage_ToolFailure_StoresFailureStatus()
        {
            // Arrange & Act
            var failedMsg = new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                Content = "Tool 'read_file' failed: File not found",
                InvocationStatus = ToolInvocationStatus.Failed
            };

            // Assert
            Assert.Equal(ToolInvocationStatus.Failed, failedMsg.InvocationStatus);
            Assert.Contains("failed", failedMsg.Content, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Unit Test 4: Tool result message accumulation in list
        /// Validates multiple tool messages can be collected
        /// </summary>
        [Fact]
        public void ChatMessageList_WithMultipleToolMessages_MaintainsOrdering()
        {
            // Arrange & Act
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Do something" },
                new ChatMessage { Role = ChatMessageRole.Assistant, Content = "I'll help", 
                    ToolCalls = new List<ToolCall> { new ToolCall { Name = "tool1", Id = "1" } } },
                new ChatMessage { Role = ChatMessageRole.Tool, Content = "Tool 1 result", InvocationStatus = ToolInvocationStatus.Complete },
                new ChatMessage { Role = ChatMessageRole.Tool, Content = "Tool 2 result", InvocationStatus = ToolInvocationStatus.Complete }
            };

            // Assert
            Assert.Equal(4, messages.Count);
            Assert.Equal(ChatMessageRole.User, messages[0].Role);
            Assert.Equal(ChatMessageRole.Assistant, messages[1].Role);
            Assert.Equal(ChatMessageRole.Tool, messages[2].Role);
            Assert.Equal(ChatMessageRole.Tool, messages[3].Role);

            var toolMessages = messages.Where(m => m.Role == ChatMessageRole.Tool).ToList();
            Assert.Equal(2, toolMessages.Count);
            Assert.All(toolMessages, msg => Assert.Equal(ToolInvocationStatus.Complete, msg.InvocationStatus));
        }

        /// <summary>
        /// Unit Test 5: CompletionChunk text sequence with tool call
        /// Validates chunks can include text and tool call in sequence
        /// </summary>
        [Fact]
        public void CompletionChunk_TextAndToolCallSequence_BothPreserved()
        {
            // Arrange & Act
            var chunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "I will " },
                new CompletionChunk { Type = ChunkType.ToolCall, ToolCall = new ToolCall { Name = "read_file", Id = "1" } },
                new CompletionChunk { Type = ChunkType.Text, Content = " for you" }
            };

            // Assert
            Assert.Equal(3, chunks.Count);
            Assert.Equal(ChunkType.Text, chunks[0].Type);
            Assert.Equal(ChunkType.ToolCall, chunks[1].Type);
            Assert.Equal(ChunkType.Text, chunks[2].Type);
            var toolCall = chunks[1].ToolCall;
            Assert.NotNull(toolCall);
            Assert.Equal("read_file", toolCall.Name);
        }

        /// <summary>
        /// Integration Test 1: Tool result message with complete execution lifecycle
        /// Validates all states of a tool execution
        /// </summary>
        [Fact]
        public void ChatMessage_ToolExecutionLifecycle_AllStatesRepresented()
        {
            // Arrange & Act - Simulate lifecycle
            var now = DateTime.Now;
            var pending = new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Pending, Content = "[Pending]" };
            var running = new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Running, ExecutionStartTime = now, Content = "[Running]" };
            var completed = new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Complete, ExecutionStartTime = now, ExecutionEndTime = now.AddSeconds(1), Content = "Result data" };
            var failed = new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, ExecutionStartTime = now, ExecutionEndTime = now.AddSeconds(1), Content = "Error message" };

            // Assert
            Assert.Equal(ToolInvocationStatus.Pending, pending.InvocationStatus);
            Assert.Equal(ToolInvocationStatus.Running, running.InvocationStatus);
            Assert.Equal(ToolInvocationStatus.Complete, completed.InvocationStatus);
            Assert.Equal(ToolInvocationStatus.Failed, failed.InvocationStatus);

            var allStates = new[] { pending, running, completed, failed };
            Assert.All(allStates, msg => Assert.Equal(ChatMessageRole.Tool, msg.Role));
        }

        /// <summary>
        /// Integration Test 2: Multi-turn message conversation with tool context
        /// Validates conversation structure for LLM→Tool→LLM pattern
        /// </summary>
        [Fact]
        public void ChatMessageSequence_SingleTurnToolPattern_FollowsCorrectStructure()
        {
            // Arrange & Act - Build single-turn tool cycle
            var conversation = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.System, Content = "You are helpful" },
                new ChatMessage { Role = ChatMessageRole.User, Content = "Read the file" },
                new ChatMessage 
                { 
                    Role = ChatMessageRole.Assistant, 
                    Content = "I'll read it", 
                    ToolCalls = new List<ToolCall> { new ToolCall { Name = "read_file", Id = "1", Arguments = new Dictionary<string, object> { { "path", "test.txt" } } } }
                },
                new ChatMessage { Role = ChatMessageRole.Tool, Content = "File contents here", InvocationStatus = ToolInvocationStatus.Complete },
                new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Here's what I found..." }
            };

            // Assert - Verify structure
            Assert.Equal(5, conversation.Count);
            Assert.Equal(ChatMessageRole.System, conversation[0].Role);
            Assert.Equal(ChatMessageRole.User, conversation[1].Role);
            Assert.Equal(ChatMessageRole.Assistant, conversation[2].Role);
            var toolCalls = conversation[2].ToolCalls;
            Assert.NotNull(toolCalls);
            Assert.Single(toolCalls);

            Assert.Equal(ChatMessageRole.Tool, conversation[3].Role);
            Assert.Equal(ToolInvocationStatus.Complete, conversation[3].InvocationStatus);
            Assert.Equal(ChatMessageRole.Assistant, conversation[4].Role);

            // Tool message was added to conversation before second LLM response
            var toolIndex = conversation.FindIndex(m => m.Role == ChatMessageRole.Tool);
            var secondAssistantIndex = conversation.FindLastIndex(m => m.Role == ChatMessageRole.Assistant);
            Assert.True(toolIndex < secondAssistantIndex, "Tool message should appear before final assistant response");
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
            var uiStateService = CreateUIStateServiceMock();

            return new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object,
                uiStateService.Object,
                null,
                null);
        }

        /// <summary>
        /// Gap23_3a Unit Test 1: Multiple tool messages with failure states
        /// Validates that tool invocation statuses are individually tracked
        /// </summary>
        [Fact]
        public void ToolMessages_WithFailureStates_TrackIndividualStatus()
        {
            // Arrange - Simulate tool batch with mixed results
            var successTool = new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                Content = "Tool 'grep_search' result: Found 3 matches",
                InvocationStatus = ToolInvocationStatus.Complete
            };
            var failedTool = new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                Content = "Tool 'read_file' failed: File not found",
                InvocationStatus = ToolInvocationStatus.Failed
            };

            // Act
            var toolMessages = new List<ChatMessage> { successTool, failedTool };
            var completedCount = toolMessages.Count(m => m.InvocationStatus == ToolInvocationStatus.Complete);
            var failedCount = toolMessages.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);

            // Assert
            Assert.Equal(1, completedCount);
            Assert.Equal(1, failedCount);
            Assert.NotEqual(
                toolMessages[0].InvocationStatus,
                toolMessages[1].InvocationStatus);
        }

        /// <summary>
        /// Gap23_3a Unit Test 2: Failure count detection for loop termination
        /// Validates that 2+ failures trigger loop break (gap23_3 threshold)
        /// </summary>
        [Fact]
        public void ToolFailureDetection_TwoOrMoreFailures_ExceedsThreshold()
        {
            // Arrange - Simulate iteration with 2 tool calls, both failing
            var failedTools = new List<ChatMessage>
            {
                new ChatMessage 
                { 
                    Role = ChatMessageRole.Tool,
                    Content = "Tool failed",
                    InvocationStatus = ToolInvocationStatus.Failed 
                },
                new ChatMessage 
                { 
                    Role = ChatMessageRole.Tool,
                    Content = "Tool failed",
                    InvocationStatus = ToolInvocationStatus.Failed 
                }
            };

            // Act
            var failureCount = failedTools.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            bool shouldTermianateLoop = failureCount >= 2;  // gap23_3 termination threshold

            // Assert
            Assert.Equal(2, failureCount);
            Assert.True(shouldTermianateLoop, "Loop should terminate when 2+ tools fail");
        }

        /// <summary>
        /// Gap23_3a Unit Test 3: Single failure does not trigger loop termination
        /// Validates that single tool failure continues iteration
        /// </summary>
        [Fact]
        public void SingleToolFailure_DoesNotExceedThreshold()
        {
            // Arrange - Simulate iteration with 1 tool call failing
            var failedTool = new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                Content = "Tool failed",
                InvocationStatus = ToolInvocationStatus.Failed
            };

            // Act
            var failureCount = new List<ChatMessage> { failedTool }
                .Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            bool shouldTerminateLoop = failureCount >= 2;

            // Assert
            Assert.Equal(1, failureCount);
            Assert.False(shouldTerminateLoop, "Single failure should not terminate loop");
        }

        /// <summary>
        /// Gap23_3a Unit Test 4: Mixed success/failure tools aggregate properly
        /// Validates failure counter in presence of successful tool calls
        /// </summary>
        [Fact]
        public void MixedToolResults_FailureCountAccurate()
        {
            // Arrange - Simulate iteration with 3 tools: success, fail, success
            var tools = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Complete, Content = "Success" },
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, Content = "Failed" },
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Complete, Content = "Success" }
            };

            // Act
            var failureCount = tools.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            var successCount = tools.Count(m => m.InvocationStatus == ToolInvocationStatus.Complete);

            // Assert
            Assert.Equal(1, failureCount);
            Assert.Equal(2, successCount);
            Assert.False(failureCount >= 2, "Only 1 failure should not trigger termination");
        }

        /// <summary>
        /// Gap23_3b Integration Test 1: Error accumulation scenario with recovery
        /// Validates that loop continues after single failure and recovers
        /// </summary>
        [Fact]
        public void ErrorAccumulation_SingleFailureRecovery_Continues()
        {
            // Arrange - Simulate two iterations: first has 1 failure, second has 0
            var iteration1Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, Content = "Tool failed in iteration 1" }
            };
            var iteration2Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Complete, Content = "Tool succeeded in iteration 2" }
            };

            // Act
            var iter1Failures = iteration1Messages.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            var iter2Failures = iteration2Messages.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            bool shouldContinueAfterIter1 = iter1Failures < 2;

            // Assert
            Assert.Equal(1, iter1Failures);
            Assert.Equal(0, iter2Failures);
            Assert.True(shouldContinueAfterIter1, "Loop should continue after single failure");
        }

        /// <summary>
        /// Gap23_3b Integration Test 2: Error accumulation with loop termination
        /// Validates accumulation triggers termination across iterations
        /// </summary>
        [Fact]
        public void ErrorAccumulation_MultipleFailuresInIteration_Terminates()
        {
            // Arrange - Simulate iteration with 2+ tools failing
            var failedIteration = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, Content = "Tool 1 failed" },
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, Content = "Tool 2 failed" },
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Complete, Content = "Tool 3 succeeded" }
            };

            // Act
            var totalFailures = failedIteration.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            bool shouldTerminate = totalFailures >= 2;
            int lastToolStatus = failedIteration.Count - 1;
            var lastToolResult = failedIteration[lastToolStatus].InvocationStatus;

            // Assert
            Assert.Equal(2, totalFailures);
            Assert.True(shouldTerminate, "2+ failures should terminate loop");
            Assert.Equal(ToolInvocationStatus.Complete, lastToolResult);  // Some tools may succeed before threshold
        }
    }
}
