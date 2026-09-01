#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelGap32_1Tests
    {
        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            return new Mock<ILlmService>();
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
            return new Mock<IToolService>();
        }

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);
            mock.Setup(x => x.DeleteMessageAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            mock.Setup(x => x.GetCurrentSession()).Returns(default(Session)!);
            mock.SetupAdd(x => x.SessionChanged += It.IsAny<EventHandler<SessionChangedEventArgs>>());
            return mock;
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            var mock = new Mock<INotificationService>();
            mock.Setup(x => x.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);
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
                        Name = "TestModel",
                        Provider = "ollama",
                        BaseUrl = "http://localhost:11434"
                    }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            mock.Setup(x => x.GetSelectedModel()).Returns((ModelInfo?)null);
            mock.SetupAdd(x => x.ConfigChanged += It.IsAny<EventHandler<ConfigChangedEventArgs>>());
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
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(new UIState { ToolSettings = new Dictionary<string, ToolPolicy>() });
            return mock;
        }

        private static Mock<IInstructionExecutorService> CreateDebugSessionServiceMock()
        {
            var mock = new Mock<IInstructionExecutorService>();
            mock.Setup(x => x.ClearPauseCheckpoint());
            return mock;
        }

        private ChatPageViewModel CreateViewModel(IIdeService? ideService = null)
        {
            return new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateDebugSessionServiceMock().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                null,
                null,
                ideService);
        }

        [Fact]
        public void Constructor_WithIdeService_DoesNotThrow()
        {
            // Arrange
            var ideService = new Mock<IIdeService>();
            ideService.Setup(x => x.GetActiveFilepath()).Returns("C:\\project\\Main.cs");
            ideService.Setup(x => x.ReadFileAsync("C:\\project\\Main.cs"))
                .ReturnsAsync("class Main {}");

            // Act & Assert
            var ex = Record.Exception(() => CreateViewModel(ideService.Object));
            Assert.Null(ex);
        }

        [Fact]
        public void Constructor_WithNullIdeService_DoesNotThrow()
        {
            // Act & Assert
            var ex = Record.Exception(() => CreateViewModel(null));
            Assert.Null(ex);
        }

        [Fact]
        public void SelectedContext_InitiallyEmpty()
        {
            // Arrange
            var viewModel = CreateViewModel();

            // Act & Assert
            Assert.Empty(viewModel.SelectedContext);
        }

        [Fact]
        public void SelectedContext_CanAddManualItem()
        {
            // Arrange
            var viewModel = CreateViewModel();
            var item = new ContextItem
            {
                Type = ContextItemType.File,
                FilePath = "C:\\project\\Foo.cs",
                Content = "class Foo {}",
                Source = "manual"
            };

            // Act
            viewModel.SelectedContext.Add(item);

            // Assert
            Assert.Single(viewModel.SelectedContext);
            Assert.Equal("C:\\project\\Foo.cs", viewModel.SelectedContext[0].FilePath);
        }

        [Fact]
        public void Constructor_IdeServiceInjected_GetActiveFilepathNotCalledAtConstruction()
        {
            // Arrange
            var ideService = new Mock<IIdeService>();
            ideService.Setup(x => x.GetActiveFilepath()).Returns("C:\\project\\Main.cs");

            // Act
            _ = CreateViewModel(ideService.Object);

            // Assert — active file is only queried at send time, not at construction
            ideService.Verify(x => x.GetActiveFilepath(), Times.Never);
        }

        [Fact]
        public void SelectedContext_DeduplicationByFilePath_ManualItemNotDuplicated()
        {
            // Arrange
            const string path = "C:\\project\\Main.cs";
            var viewModel = CreateViewModel();
            var manualItem = new ContextItem
            {
                Type = ContextItemType.File,
                FilePath = path,
                Content = "class Main {}",
                Source = "manual"
            };
            viewModel.SelectedContext.Add(manualItem);

            // Assert — collection has exactly the one manually added item; no implicit duplication at rest
            Assert.Single(viewModel.SelectedContext);
            Assert.Equal(path, viewModel.SelectedContext[0].FilePath);
            Assert.Equal("manual", viewModel.SelectedContext[0].Source);
        }
    }
}
