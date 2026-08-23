#nullable enable

using System;
using System.Collections.ObjectModel;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for gap25_6: Onboarding Dismissal State.
    /// Tests verify that the onboarding card visibility is automatically synced with Messages collection state.
    /// </summary>
    public class ChatPageViewModelOnboardingTests
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
                .ReturnsAsync(new System.Collections.Generic.List<ContextItem>());
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
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            mock.Setup(x => x.GetCurrentSession())
                .Returns(new Session { Id = "test-session" });
            return mock;
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            var mock = new Mock<INotificationService>();
            return mock;
        }

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var mock = new Mock<IConfigService>();
            var config = new ContinueConfig
            {
                Models = new System.Collections.Generic.List<ModelInfo>
                {
                    new ModelInfo { Name = "Llama 3.1 8B Instruct", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            return mock;
        }

        private static Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            return mock;
        }

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(new UIState());
            return mock;
        }

        private ChatPageViewModel CreateViewModel()
        {
            return new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object
            );
        }

        /// <summary>
        /// gap25_6 test 1: OnboardingCardVisible_InitiallyTrue
        /// Verifies that when ChatPageViewModel is constructed, OnboardingCardVisible is true (chat is empty).
        /// </summary>
        [Fact]
        public void OnboardingCardVisible_InitiallyTrue()
        {
            // Arrange
            var viewModel = CreateViewModel();

            // Act
            // Property is checked immediately after construction

            // Assert
            Assert.True(viewModel.OnboardingCardVisible, "Onboarding card should be visible when chat is empty (initial state)");
            Assert.Empty(viewModel.Messages);
        }

        /// <summary>
        /// gap25_6 test 2: OnboardingCardVisible_BecomesFalseWhenMessageAdded
        /// Verifies that when a message is added to the Messages collection, OnboardingCardVisible becomes false.
        /// </summary>
        [Fact]
        public void OnboardingCardVisible_BecomesFalseWhenMessageAdded()
        {
            // Arrange
            var viewModel = CreateViewModel();
            Assert.True(viewModel.OnboardingCardVisible, "Card should initially be visible");

            var message = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "Hello, how can you help?"
            };

            // Act
            viewModel.Messages.Add(message);

            // Assert
            Assert.False(viewModel.OnboardingCardVisible, "Onboarding card should be hidden when first message is added");
            Assert.Single(viewModel.Messages);
        }

        /// <summary>
        /// gap25_6 test 3: OnboardingCardVisible_BecomesTrueWhenMessagesCleared
        /// Verifies that when the Messages collection is cleared, OnboardingCardVisible becomes true again.
        /// This simulates a user starting a new conversation after clearing the chat.
        /// </summary>
        [Fact]
        public void OnboardingCardVisible_BecomesTrueWhenMessagesCleared()
        {
            // Arrange
            var viewModel = CreateViewModel();

            var message1 = new ChatMessage { Role = ChatMessageRole.User, Content = "First message" };
            var message2 = new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Response" };
            viewModel.Messages.Add(message1);
            viewModel.Messages.Add(message2);

            Assert.False(viewModel.OnboardingCardVisible, "Card should be hidden when messages exist");
            Assert.Equal(2, viewModel.Messages.Count);

            // Act
            viewModel.Messages.Clear();

            // Assert
            Assert.True(viewModel.OnboardingCardVisible, "Onboarding card should reappear when chat is cleared");
            Assert.Empty(viewModel.Messages);
        }
    }
}
