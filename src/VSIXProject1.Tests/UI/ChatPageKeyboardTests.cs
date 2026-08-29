#nullable enable

using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.UI
{
    /// <summary>
    /// gap35_1 / gap35_3: Unit tests verifying the SendMessageCommand CanExecute guard used by
    /// the InputTextBox_KeyDown handler. Tests are headless (no STA / WPF required).
    /// gap35_1 covers: text present/not-streaming → true, empty text → false, streaming → false.
    /// gap35_3 covers: IsPaused → false, ShowErrorBanner → false, whitespace-only input → false.
    /// </summary>
    public class ChatPageKeyboardTests
    {
        private static Mock<IContextService> CreateContextServiceMock()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
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

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var mock = new Mock<IConfigService>();
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "test-model", Provider = "ollama", BaseUrl = "http://localhost:11434" }
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
                new Mock<ILlmService>().Object,
                CreateContextServiceMock().Object,
                new Mock<IToolService>().Object,
                CreateSessionServiceMock().Object,
                new Mock<INotificationService>().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IDebugSessionService>().Object,
                null,
                null
            );
        }

        [Fact]
        public void WhenInputTextIsSet_AndNotStreaming_SendMessageCommand_CanExecuteReturnsTrue()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = "hello world";

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void WhenInputTextIsEmpty_SendMessageCommand_CanExecuteReturnsFalse()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = string.Empty;

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void WhenIsStreamingIsTrue_SendMessageCommand_CanExecuteReturnsFalse()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = "hello world";
            vm.IsStreaming = true;

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        /// <summary>
        /// gap35_3: Additional CanExecute guard tests — IsPaused, ShowErrorBanner, whitespace-only input.
        /// Covers the composite predicate in CanSendMessage() that the InputTextBox_KeyDown handler
        /// respects before calling SendMessageCommand.Execute(null).
        /// </summary>
        [Fact]
        public void WhenIsPausedIsTrue_SendMessageCommand_CanExecuteReturnsFalse()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = "hello world";
            vm.IsPaused = true;

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void WhenShowErrorBannerIsTrue_SendMessageCommand_CanExecuteReturnsFalse()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = "hello world";
            vm.ShowErrorBanner = true;

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void WhenInputTextIsWhitespaceOnly_SendMessageCommand_CanExecuteReturnsFalse()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = "   ";

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }
    }
}
