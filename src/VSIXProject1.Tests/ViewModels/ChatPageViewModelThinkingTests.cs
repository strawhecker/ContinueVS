using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using Moq;
using Xunit;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// gap68: Tests for ChatPageViewModel thinking parsing and setting integration.
    /// </summary>
    public class ChatPageViewModelThinkingTests
    {
        private readonly Mock<ILlmService> _mockLlmService;
        private readonly Mock<IContextService> _mockContextService;
        private readonly Mock<IToolService> _mockToolService;
        private readonly Mock<ISessionService> _mockSessionService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IConfigService> _mockConfigService;
        private readonly Mock<ISystemPromptService> _mockSystemPromptService;
        private readonly Mock<IUIStateService> _mockUIStateService;
        private readonly Mock<IInstructionExecutorService> _mockInstructionExecutorService;
        private readonly Mock<IChangeStackService> _mockChangeStackService;
        private readonly Mock<IMarkdownService> _mockMarkdownService;
        private readonly Mock<IModeConfigRegistry> _mockModeConfigRegistry;
        private readonly Mock<IAgentCommandDispatcher> _mockAgentCommandDispatcher;

        public ChatPageViewModelThinkingTests()
        {
            _mockLlmService = new Mock<ILlmService>();
            _mockContextService = new Mock<IContextService>();
            _mockToolService = new Mock<IToolService>();
            _mockSessionService = new Mock<ISessionService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockConfigService = new Mock<IConfigService>();
            _mockSystemPromptService = new Mock<ISystemPromptService>();
            _mockUIStateService = new Mock<IUIStateService>();
            _mockInstructionExecutorService = new Mock<IInstructionExecutorService>();
            _mockChangeStackService = new Mock<IChangeStackService>();
            _mockMarkdownService = new Mock<IMarkdownService>();
            _mockModeConfigRegistry = new Mock<IModeConfigRegistry>();
            _mockAgentCommandDispatcher = new Mock<IAgentCommandDispatcher>();

            // Setup defaults
            _mockModeConfigRegistry
                .Setup(r => r.GetConfig(It.IsAny<ChatMode>()))
                .Returns(new ModeConfig { AllowToolLoop = false, ExportsPlanFile = false });

            _mockSessionService
                .Setup(s => s.GetCurrentSession())
                .Returns(new Session { Messages = new List<ChatMessage>() });

            _mockConfigService
                .Setup(c => c.GetCurrentConfig())
                .Returns(new ContinueConfig { CustomSettings = new Dictionary<string, object>() });

            _mockConfigService
                .Setup(c => c.GetSelectedModel())
                .Returns(new ModelInfo { ContextWindow = 4096 });
        }

        private ChatPageViewModel CreateViewModel()
        {
            return new ChatPageViewModel(
                _mockLlmService.Object,
                _mockContextService.Object,
                _mockToolService.Object,
                _mockSessionService.Object,
                _mockNotificationService.Object,
                _mockConfigService.Object,
                _mockSystemPromptService.Object,
                _mockUIStateService.Object,
                _mockInstructionExecutorService.Object,
                _mockChangeStackService.Object,
                _mockMarkdownService.Object,
                modeService: null,
                workflowService: null,
                ideService: null,
                planOutputService: null,
                agentCommandDispatcher: _mockAgentCommandDispatcher.Object);
        }

        [Fact]
        public async Task ChatMessage_IsThinking_PropertyBindsCorrectly()
        {
            // Arrange
            var msg = new ChatMessage
            {
                Role = ChatMessageRole.Thinking,
                Content = "Thinking content",
                IsThinking = true,
                IsExpanded = false
            };

            // Act
            msg.IsExpanded = true;

            // Assert
            Assert.True(msg.IsExpanded);
        }

        [Fact]
        public void ChatMessage_IsThinking_MultipleTogglesBetweenStates()
        {
            // Arrange
            var msg = new ChatMessage { IsExpanded = false };
            var stateChanges = new List<bool>();
            msg.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "IsExpanded")
                    stateChanges.Add(msg.IsExpanded);
            };

            // Act
            msg.IsExpanded = true;
            msg.IsExpanded = false;
            msg.IsExpanded = true;

            // Assert
            Assert.Equal(new[] { true, false, true }, stateChanges);
        }

        [Fact]
        public void UserSettings_Contains_Chat_ShowThinkingAfterStreaming()
        {
            // Assert
            Assert.NotNull(UserSettings.Chat_ShowThinkingAfterStreaming);
            Assert.Equal("chat.showThinkingAfterStreaming", UserSettings.Chat_ShowThinkingAfterStreaming);
        }

        [Fact]
        public void UserSettings_ShowThinkingAfterStreaming_DefaultTrue()
        {
            // Act
            var defaults = UserSettings.GetDefaults();

            // Assert
            Assert.True((bool)defaults[UserSettings.Chat_ShowThinkingAfterStreaming]);
        }

        [Fact]
        public void UserSettings_GetDefault_ShowThinkingAfterStreaming()
        {
            // Act
            var value = UserSettings.GetDefault(UserSettings.Chat_ShowThinkingAfterStreaming);

            // Assert
            Assert.NotNull(value);
            Assert.True((bool)value);
        }

        [Fact]
        public void ChatMessageRole_Includes_Thinking()
        {
            // Assert
            Assert.True(Enum.IsDefined(typeof(ChatMessageRole), ChatMessageRole.Thinking));
        }

        [Fact]
        public void ChatMessage_ThinkingRole_Message_HasCorrectProperties()
        {
            // Arrange & Act
            var msg = new ChatMessage
            {
                Role = ChatMessageRole.Thinking,
                Content = "Model is thinking about the problem...",
                IsThinking = true,
                IsExpanded = false
            };

            // Assert
            Assert.Equal(ChatMessageRole.Thinking, msg.Role);
            Assert.Equal("Model is thinking about the problem...", msg.Content);
            Assert.True(msg.IsThinking);
            Assert.False(msg.IsExpanded);
        }

        [Fact]
        public void ChatMessage_CanToggleExpandedState_ForUIBinding()
        {
            // Arrange
            var messages = new ObservableCollection<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.Thinking,
                    Content = "Thinking",
                    IsThinking = true,
                    IsExpanded = false
                }
            };

            var msg = messages[0];
            var expandedBeforeToggle = msg.IsExpanded;

            // Act
            msg.IsExpanded = !msg.IsExpanded;
            var expandedAfterToggle = msg.IsExpanded;

            // Assert
            Assert.False(expandedBeforeToggle);
            Assert.True(expandedAfterToggle);
            Assert.Same(msg, messages[0]);  // Same instance in collection
        }

        [Fact]
        public void ChatMessage_IsThinking_FlagCanBePersisted()
        {
            // Arrange
            var original = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = ChatMessageRole.Thinking,
                Content = "Reasoning",
                IsThinking = true
            };

            // Act
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(original);
            var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatMessage>(json);

            // Assert
            Assert.NotNull(restored);
            Assert.True(restored.IsThinking);
        }

        [Fact]
        public void ChatMessage_IsExpanded_UIState_NotPersisted()
        {
            // Arrange
            var original = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = ChatMessageRole.Thinking,
                Content = "Thinking",
                IsExpanded = true
            };

            // Act
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(original);
            var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatMessage>(json);

            // Assert - IsExpanded should default to false when deserialized
            Assert.NotNull(restored);
            Assert.False(restored.IsExpanded);
        }

        [Fact]
        public void UserSettings_All_GetDefaults_IncludesThinkingSetting()
        {
            // Act
            var all = UserSettings.GetDefaults();

            // Assert
            Assert.Contains(UserSettings.Chat_ShowThinkingAfterStreaming, all.Keys);
            Assert.True((bool)all[UserSettings.Chat_ShowThinkingAfterStreaming]);
        }
    }
}
