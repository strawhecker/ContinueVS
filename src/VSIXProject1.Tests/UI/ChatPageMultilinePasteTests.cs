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
    /// gap42_3: Unit tests confirming InputText preserves multiline content as required
    /// for Ctrl+V paste support. All tests are headless (no STA / WPF required).
    /// </summary>
    public class ChatPageMultilinePasteTests
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
                new Mock<IInstructionExecutorService>().Object,
                null,
                null
            );
        }

        [Fact]
        public void WhenInputTextContainsNewlines_PropertyPreservesAllLines()
        {
            // Arrange
            var vm = CreateViewModel();
            const string multiline = "line1\nline2\nline3";

            // Act
            vm.InputText = multiline;

            // Assert
            Assert.Equal(multiline, vm.InputText);
        }

        [Fact]
        public void WhenInputTextContainsCRLF_PropertyPreservesCRLF()
        {
            // Arrange
            var vm = CreateViewModel();
            const string multiline = "line1\r\nline2\r\nline3";

            // Act
            vm.InputText = multiline;

            // Assert
            Assert.Equal(multiline, vm.InputText);
        }

        [Fact]
        public void WhenInputTextIsMultiline_SendMessageCommand_CanExecuteReturnsTrue()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = "line1\nline2\nline3";

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void WhenInputTextIsMultilineWhitespaceOnly_SendMessageCommand_CanExecuteReturnsFalse()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.InputText = "   \n   \n   ";

            // Act
            var canExecute = vm.SendMessageCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void WhenInputTextContainsMixedNewlines_PropertyPreservesContent()
        {
            // Arrange
            var vm = CreateViewModel();
            const string multiline = "a\nb\r\nc";

            // Act
            vm.InputText = multiline;

            // Assert
            Assert.Equal(multiline, vm.InputText);
        }
    }
}
