using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using Moq;

namespace ContinueVS.Tests.Services
{
    public class UIStateServiceTests
    {
        private Mock<IConfigService> CreateMockConfigService(UIState initialState = null)
        {
            var mock = new Mock<IConfigService>();
            var uiState = initialState ?? new UIState();
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(uiState);
            mock.Setup(x => x.SaveUIStateAsync(It.IsAny<UIState>()))
                .Callback<UIState>(state => uiState = new UIState
                {
                    ToolSettings = new Dictionary<string, ToolPolicy>(state.ToolSettings),
                    ToolGroupSettings = new Dictionary<string, bool>(state.ToolGroupSettings),
                    RuleSettings = new Dictionary<string, bool>(state.RuleSettings),
                    ReasoningSettings = new Dictionary<string, ReasoningSettings>(state.ReasoningSettings),
                    OnboardingCardVisible = state.OnboardingCardVisible,
                    ExploreDialogOpen = state.ExploreDialogOpen,
                    TTSActive = state.TTSActive,
                    FileEditingMode = state.FileEditingMode,
                    LastModified = state.LastModified,
                    Version = state.Version
                })
                .Returns(Task.CompletedTask);
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(() => uiState);
            return mock;
        }

        [Fact]
        public async Task GetToolPolicyAsync_ReturnsAskFirstByDefault_WhenToolNotFound()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            var policy = await service.GetToolPolicyAsync("unknown_tool");

            Assert.Equal(ToolPolicy.AskFirst, policy);
        }

        [Fact]
        public async Task SaveToolPolicyAsync_PersistsPolicyToConfig()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            await service.SaveToolPolicyAsync("read_file", ToolPolicy.AutoApprove);
            var policy = await service.GetToolPolicyAsync("read_file");

            Assert.Equal(ToolPolicy.AutoApprove, policy);
            mockConfigService.Verify(x => x.SaveUIStateAsync(It.IsAny<UIState>()), Times.Once);
        }

        [Fact]
        public async Task SaveToolPolicyAsync_UpdatesExistingPolicy()
        {
            var initialState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy> { { "read_file", ToolPolicy.AskFirst } }
            };
            var mockConfigService = CreateMockConfigService(initialState);
            var service = new UIStateService(mockConfigService.Object);

            await service.SaveToolPolicyAsync("read_file", ToolPolicy.Disabled);
            var policy = await service.GetToolPolicyAsync("read_file");

            Assert.Equal(ToolPolicy.Disabled, policy);
        }

        [Fact]
        public async Task GetAllToolPoliciesAsync_ReturnsCompletePolicy()
        {
            var initialState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "read_file", ToolPolicy.AutoApprove },
                    { "edit_file", ToolPolicy.AskFirst },
                    { "search_codebase", ToolPolicy.Disabled }
                }
            };
            var mockConfigService = CreateMockConfigService(initialState);
            var service = new UIStateService(mockConfigService.Object);

            var policies = await service.GetAllToolPoliciesAsync();

            Assert.Equal(3, policies.Count);
            Assert.Equal(ToolPolicy.AutoApprove, policies["read_file"]);
            Assert.Equal(ToolPolicy.AskFirst, policies["edit_file"]);
            Assert.Equal(ToolPolicy.Disabled, policies["search_codebase"]);
        }

        [Fact]
        public async Task GetToolGroupPolicyAsync_ReturnsEnabledByDefault_WhenGroupNotFound()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            var enabled = await service.GetToolGroupPolicyAsync("unknown_group");

            Assert.True(enabled);
        }

        [Fact]
        public async Task SaveToolGroupPolicyAsync_PersistsGroupSetting()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            await service.SaveToolGroupPolicyAsync("file_operations", false);
            var enabled = await service.GetToolGroupPolicyAsync("file_operations");

            Assert.False(enabled);
            mockConfigService.Verify(x => x.SaveUIStateAsync(It.IsAny<UIState>()), Times.Once);
        }

        [Fact]
        public async Task IsRuleEnabledAsync_ReturnsEnabledByDefault_WhenRuleNotFound()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            var enabled = await service.IsRuleEnabledAsync("unknown_rule");

            Assert.True(enabled);
        }

        [Fact]
        public async Task SaveRuleSettingAsync_PersistsRuleSetting()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            await service.SaveRuleSettingAsync("auto_continue", false);
            var enabled = await service.IsRuleEnabledAsync("auto_continue");

            Assert.False(enabled);
            mockConfigService.Verify(x => x.SaveUIStateAsync(It.IsAny<UIState>()), Times.Once);
        }

        [Fact]
        public async Task GetUIStateAsync_ReturnsCompleteState()
        {
            var initialState = new UIState
            {
                OnboardingCardVisible = false,
                ExploreDialogOpen = true,
                TTSActive = true
            };
            var mockConfigService = CreateMockConfigService(initialState);
            var service = new UIStateService(mockConfigService.Object);

            var state = await service.GetUIStateAsync();

            Assert.False(state.OnboardingCardVisible);
            Assert.True(state.ExploreDialogOpen);
            Assert.True(state.TTSActive);
        }

        [Fact]
        public async Task SaveUIStateAsync_OverwritesAllSettings()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            var newState = new UIState
            {
                OnboardingCardVisible = false,
                TTSActive = true,
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "read_file", ToolPolicy.AutoApprove }
                }
            };
            await service.SaveUIStateAsync(newState);
            var savedState = await service.GetUIStateAsync();

            Assert.False(savedState.OnboardingCardVisible);
            Assert.True(savedState.TTSActive);
            Assert.Single(savedState.ToolSettings);
            mockConfigService.Verify(x => x.SaveUIStateAsync(It.IsAny<UIState>()), Times.Once);
        }

        [Fact]
        public async Task SaveToolPolicyAsync_UpdatesLastModified()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            var beforeSave = DateTime.UtcNow.AddSeconds(-1);
            await service.SaveToolPolicyAsync("test_tool", ToolPolicy.AutoApprove);
            var afterSave = DateTime.UtcNow.AddSeconds(1);

            var state = await service.GetUIStateAsync();
            Assert.InRange(state.LastModified, beforeSave, afterSave);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenConfigServiceNull()
        {
            Assert.Throws<ArgumentNullException>(() => new UIStateService(null!));
        }

        [Fact]
        public async Task GetToolPolicyAsync_ThrowsArgumentNullException_WhenToolNameNull()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetToolPolicyAsync(null!));
        }

        [Fact]
        public async Task SaveToolPolicyAsync_ThrowsArgumentNullException_WhenToolNameNull()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.SaveToolPolicyAsync(null!, ToolPolicy.AskFirst));
        }

        [Fact]
        public async Task SaveUIStateAsync_ThrowsArgumentNullException_WhenStateNull()
        {
            var mockConfigService = CreateMockConfigService();
            var service = new UIStateService(mockConfigService.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveUIStateAsync(null!));
        }
    }
}
