using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using Moq;

namespace ContinueVS.Tests.Services
{
    public class LlmQuestionIntegrationTests
    {
        [Fact]
        public async Task LlmQuestionService_IntegratesWithInteractivePromptService_Interactive()
        {
            var mockNotificationService = new Mock<INotificationService>();
            mockNotificationService
                .Setup(m => m.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var promptService = new InteractivePromptService(mockNotificationService.Object);
            var questionService = new LlmQuestionService(promptService);

            var question = new LLMQuestionPrompt
            {
                QuestionText = "Should I proceed?",
                QuestionType = LLMQuestionType.Confirmation
            };

            var answer = await questionService.HandleLLMQuestionAsync(question, isAutonomous: false);

            Assert.NotEmpty(answer);
            mockNotificationService.Verify(
                m => m.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task LlmQuestionService_BypassesPromptService_Autonomous()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var questionService = new LlmQuestionService(mockPromptService.Object);

            var question = new LLMQuestionPrompt
            {
                QuestionText = "Should I proceed?",
                QuestionType = LLMQuestionType.Confirmation
            };

            var answer = await questionService.HandleLLMQuestionAsync(question, isAutonomous: true);

            Assert.NotEmpty(answer);
            mockPromptService.Verify(
                m => m.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public void AutoAnswerPolicyRegistry_MatchesQuestionTypeToResponse()
        {
            // Test Default strategy for Confirmation
            var defaultConfirmation = AutoAnswerPolicyRegistry.GetDefaultAnswer(
                LLMQuestionType.Confirmation,
                AutoAnswerResponse.Default);
            Assert.NotEmpty(defaultConfirmation);

            // Test Conservative strategy
            var conservativeConfirmation = AutoAnswerPolicyRegistry.GetDefaultAnswer(
                LLMQuestionType.Confirmation,
                AutoAnswerResponse.Conservative);
            Assert.NotEmpty(conservativeConfirmation);

            // Test Aggressive strategy
            var aggressiveConfirmation = AutoAnswerPolicyRegistry.GetDefaultAnswer(
                LLMQuestionType.Confirmation,
                AutoAnswerResponse.Aggressive);
            Assert.NotEmpty(aggressiveConfirmation);

            // Different strategies should produce different answers
            Assert.NotEqual(conservativeConfirmation, aggressiveConfirmation);
        }

        [Fact]
        public async Task LlmQuestionService_DetectsAndClassifiesQuestionTypes()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var questionService = new LlmQuestionService(mockPromptService.Object);

            // Test Confirmation detection
            var confirmationResponse = "Should I apply this optimization?";
            var confirmationQuestion = await questionService.DetectLLMQuestionAsync(confirmationResponse);
            Assert.NotNull(confirmationQuestion);
            Assert.Equal(LLMQuestionType.Confirmation, confirmationQuestion.QuestionType);

            // Test Selection detection
            var selectionResponse = "Which strategy should I use: (1) Fast or (2) Safe?";
            var selectionQuestion = await questionService.DetectLLMQuestionAsync(selectionResponse);
            Assert.NotNull(selectionQuestion);
            Assert.Equal(LLMQuestionType.Selection, selectionQuestion.QuestionType);

            // Test ThresholdWarning detection
            var thresholdResponse = "Retry attempts have exceeded the threshold. Should I continue?";
            var thresholdQuestion = await questionService.DetectLLMQuestionAsync(thresholdResponse);
            Assert.NotNull(thresholdQuestion);
            Assert.Equal(LLMQuestionType.ThresholdWarning, thresholdQuestion.QuestionType);
        }

        [Fact]
        public async Task LlmQuestionService_HandlesFullQuestionAnswerFlow()
        {
            var mockNotificationService = new Mock<INotificationService>();
            mockNotificationService
                .Setup(m => m.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var promptService = new InteractivePromptService(mockNotificationService.Object);
            var questionService = new LlmQuestionService(promptService);

            // Simulate LLM response with embedded question
            var llmResponse = "The error indicates a timeout. Should I increase the timeout value?";

            // Step 1: Detect question
            var detectedQuestion = await questionService.DetectLLMQuestionAsync(llmResponse);
            Assert.NotNull(detectedQuestion);
            Assert.NotEmpty(detectedQuestion.QuestionText);

            // Step 2: Handle question in interactive mode
            var answer = await questionService.HandleLLMQuestionAsync(
                detectedQuestion,
                isAutonomous: false);

            Assert.NotEmpty(answer);

            // Step 3: Handle same question in autonomous mode
            var autoAnswer = await questionService.HandleLLMQuestionAsync(
                detectedQuestion,
                isAutonomous: true);

            Assert.NotEmpty(autoAnswer);
            // Autonomous should bypass prompt service
            mockNotificationService.Verify(
                m => m.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Once); // Only called for interactive mode
        }
    }
}
