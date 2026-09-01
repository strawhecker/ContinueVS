#nullable enable

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using ContinueVS.ViewModels.Models;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for continuation policy ViewModel support (gap27_12).
    /// Verifies that ContinuationPolicies collection and SelectedPolicy property work correctly.
    /// </summary>
    public class PolicyViewModelTests
    {
        private static ChatPageViewModel CreateViewModel(IWorkflowService? workflowService = null)
        {
            var llmMock = new Mock<ILlmService>();

            var contextMock = new Mock<IContextService>();
            contextMock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());

            var toolMock = new Mock<IToolService>();

            var sessionMock = new Mock<ISessionService>();
            sessionMock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();

            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Test Model", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            var configMock = new Mock<IConfigService>();
            configMock.Setup(x => x.GetCurrentConfig()).Returns(config);
            // gap27_16: Mock GetDefaultPolicyAsync to return Interactive (default)
            configMock.Setup(x => x.GetDefaultPolicyAsync())
                .ReturnsAsync(ContinuationPolicy.Interactive);
            // gap27_16: Mock SaveDefaultPolicyAsync (fire-and-forget, returns completed task)
            configMock.Setup(x => x.SaveDefaultPolicyAsync(It.IsAny<ContinuationPolicy>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            // gap27_5: Mock GetDefaultModeAsync to return Ask (0)
            configMock.Setup(x => x.GetDefaultModeAsync())
                .ReturnsAsync(0);

            var promptMock = new Mock<ISystemPromptService>();
            promptMock.Setup(x => x.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            promptMock.Setup(x => x.GetPromptForMode(It.IsAny<string>())).Returns("Test prompt");

            var uiStateMock = new Mock<IUIStateService>();

            return new ChatPageViewModel(
                llmMock.Object,
                contextMock.Object,
                toolMock.Object,
                sessionMock.Object,
                notifMock.Object,
                configMock.Object,
                promptMock.Object,
                uiStateMock.Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                null,
                workflowService
            );
        }

        [Fact]
        public void ContinuationPolicies_Should_Load_On_First_Access()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var policies = vm.ContinuationPolicies;

            // Assert
            Assert.NotNull(policies);
            Assert.Equal(3, policies.Count);
        }

        [Fact]
        public void SelectedPolicy_Should_Default_To_Interactive()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var selectedPolicy = vm.SelectedPolicy;

            // Assert
            Assert.Equal(ContinuationPolicy.Interactive, selectedPolicy);
        }

        [Fact]
        public void SelectedPolicy_Change_Should_Fire_PropertyChanged()
        {
            // Arrange
            var vm = CreateViewModel();
            bool propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatPageViewModel.SelectedPolicy))
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            vm.SelectedPolicy = ContinuationPolicy.Auto;

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal(ContinuationPolicy.Auto, vm.SelectedPolicy);
        }

        [Fact]
        public void SelectedPolicy_Setter_Should_Call_Service()
        {
            // Arrange
            var workflowServiceMock = new Mock<IWorkflowService>();
            var vm = CreateViewModel(workflowServiceMock.Object);

            // Act
            vm.SelectedPolicy = ContinuationPolicy.Deferred;

            // Assert
            workflowServiceMock.Verify(x => x.SetContinuationPolicyAsync(ContinuationPolicy.Deferred), Times.Once);
        }
    }
}
