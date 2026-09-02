using System;
#nullable enable

using Moq;
using ContinueVS.ViewModels;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating ChatPageViewModel instances in tests with default mock dependencies.
    /// </summary>
    public static class ChatPageViewModelTestHelper
    {
        /// <summary>
        /// Creates a ChatPageViewModel with mock services for testing.
        /// </summary>
        public static ChatPageViewModel CreateTestViewModel(
            ILlmService? llmService = null,
            IContextService? contextService = null,
            IToolService? toolService = null,
            ISessionService? sessionService = null,
            INotificationService? notificationService = null,
            IConfigService? configService = null,
            ISystemPromptService? systemPromptService = null,
            IUIStateService? uiStateService = null,
            IInstructionExecutorService? instructionExecutorService = null,
            IChangeStackService? changeStackService = null,
            IMarkdownService? markdownService = null,
            ILlmQuestionService? llmQuestionService = null,
            IModeService? modeService = null,
            IWorkflowService? workflowService = null,
            IIdeService? ideService = null,
            IModeConfigRegistry? modeConfigRegistry = null,
            IPlanOutputService? planOutputService = null)
        {
            return new ChatPageViewModel(
                llmService ?? new Mock<ILlmService>().Object,
                contextService ?? new Mock<IContextService>().Object,
                toolService ?? new Mock<IToolService>().Object,
                sessionService ?? new Mock<ISessionService>().Object,
                notificationService ?? new Mock<INotificationService>().Object,
                configService ?? new Mock<IConfigService>().Object,
                systemPromptService ?? new Mock<ISystemPromptService>().Object,
                uiStateService ?? new Mock<IUIStateService>().Object,
                instructionExecutorService ?? new Mock<IInstructionExecutorService>().Object,
                changeStackService ?? new Mock<IChangeStackService>().Object,
                markdownService ?? new Mock<IMarkdownService>().Object,
                llmQuestionService ?? new Mock<ILlmQuestionService>().Object,
                modeService,
                workflowService,
                ideService,
                modeConfigRegistry,
                planOutputService);
        }
    }
}
