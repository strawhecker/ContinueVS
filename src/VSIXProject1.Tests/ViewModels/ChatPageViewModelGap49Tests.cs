#nullable enable

using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for gap49: File path detection in responses and Copy/Apply dropdown behavior.
    /// </summary>
    public class ChatPageViewModelGap49Tests
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

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            return mock;
        }

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Llama 3.1", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            return mock;
        }

        private static Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns("Test system prompt");
            mock.Setup(x => x.LoadAsync())
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            return mock;
        }

        private static Mock<IToolService> CreateToolServiceMock()
        {
            return new Mock<IToolService>();
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            var mock = new Mock<INotificationService>();
            mock.Setup(x => x.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            return mock;
        }

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(new UIState());
            return mock;
        }

        private static Mock<IInstructionExecutorService> CreateInstructionExecutorServiceMock()
        {
            return new Mock<IInstructionExecutorService>();
        }

        private static Mock<IChangeStackService> CreateChangeStackServiceMock()
        {
            var mock = new Mock<IChangeStackService>();
            mock.Setup(x => x.CreateChangeStack()).Returns(System.Guid.NewGuid().ToString());
            return mock;
        }

        private static Mock<IMarkdownService> CreateMarkdownServiceMock()
        {
            return new Mock<IMarkdownService>();
        }

        /// <summary>
        /// Test: DetectFilePathInResponse returns true when response contains JSON path field

        [Fact]
        public void DetectFilePathInResponse_ReturnsTrue_WhenResponseContainsJsonPath()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            var responseWithJsonPath = @"{ ""path"": ""/home/user/myfile.txt"", ""content"": ""code here"" }";

            // Act
            // Access private method via reflection for testing
            var method = typeof(ChatPageViewModel).GetMethod("DetectFilePathInResponse", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool?)method?.Invoke(viewModel, new object[] { responseWithJsonPath });

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Test: DetectFilePathInResponse returns true when response contains file: pattern
        /// </summary>
        [Fact]
        public void DetectFilePathInResponse_ReturnsTrue_WhenResponseContainsFilePattern()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            var responseWithFilePath = "The changes should be applied to file: /src/myfile.cs";

            // Act
            var method = typeof(ChatPageViewModel).GetMethod("DetectFilePathInResponse",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool?)method?.Invoke(viewModel, new object[] { responseWithFilePath });

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Test: DetectFilePathInResponse returns true when response contains Unix path
        /// </summary>
        [Fact]
        public void DetectFilePathInResponse_ReturnsTrue_WhenResponseContainsUnixPath()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            var responseWithUnixPath = "Modify /home/user/project/src/main.cpp";

            // Act
            var method = typeof(ChatPageViewModel).GetMethod("DetectFilePathInResponse",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool?)method?.Invoke(viewModel, new object[] { responseWithUnixPath });

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Test: DetectFilePathInResponse returns false when response contains no path
        /// </summary>
        [Fact]
        public void DetectFilePathInResponse_ReturnsFalse_WhenNoFilePath()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            var responseWithoutPath = "This is just a generic response with no file paths mentioned.";

            // Act
            var method = typeof(ChatPageViewModel).GetMethod("DetectFilePathInResponse",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool?)method?.Invoke(viewModel, new object[] { responseWithoutPath });

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Test: DetectFilePathInResponse returns false when response is null/empty
        /// </summary>
        [Fact]
        public void DetectFilePathInResponse_ReturnsFalse_WhenResponseIsEmpty()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            // Act
            var method = typeof(ChatPageViewModel).GetMethod("DetectFilePathInResponse",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool?)method?.Invoke(viewModel, new object[] { string.Empty });

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Test: CurrentResponseHasFilePath property can be set and retrieved
        /// </summary>
        [Fact]
        public void CurrentResponseHasFilePath_CanBeSetAndRetrieved()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            // Act
            viewModel.CurrentResponseHasFilePath = true;

            // Assert
            Assert.True(viewModel.CurrentResponseHasFilePath);
        }

        /// <summary>
        /// Test: Code action selection can be set (0=Copy, 1=Apply)
        /// </summary>
        [Fact]
        public void SelectedCodeAction_CanBeSetAndRetrieved()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            // Act
            viewModel.SelectedCodeAction = 1; // Apply

            // Assert
            Assert.Equal(1, viewModel.SelectedCodeAction);
        }

        /// <summary>
        /// Test: Code action defaults to Copy (0)
        /// </summary>
        [Fact]
        public void SelectedCodeAction_DefaultsToCopy()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                CreateInstructionExecutorServiceMock().Object,
                CreateChangeStackServiceMock().Object,
                CreateMarkdownServiceMock().Object);

            // Assert
            Assert.Equal(0, viewModel.SelectedCodeAction); // 0 = Copy
        }
    }
}
