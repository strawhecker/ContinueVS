using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using Moq;

namespace ContinueVS.Tests.Services
{
    public class LlmQuestionServiceTests
    {
        [Fact]
        public void Constructor_ValidatesDependencies()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LlmQuestionService(null, null));
        }

        [Fact]
        public async Task HandleLLMQuestionAsync_InAutonomousMode_AppliesPolicy()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var service = new LlmQuestionService(mockPromptService.Object);

            var question = new LLMQuestionPrompt
            {
                QuestionText = "Should I proceed?",
                QuestionType = LLMQuestionType.Confirmation
            };

            var answer = await service.HandleLLMQuestionAsync(question, isAutonomous: true);

            Assert.NotEmpty(answer);
            mockPromptService.Verify(m => m.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task HandleLLMQuestionAsync_InInteractiveMode_PromptsUser()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            mockPromptService
                .Setup(m => m.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), true))
                .ReturnsAsync("User's answer");

            var service = new LlmQuestionService(mockPromptService.Object);

            var question = new LLMQuestionPrompt
            {
                QuestionText = "Should I proceed?",
                QuestionType = LLMQuestionType.Confirmation
            };

            var answer = await service.HandleLLMQuestionAsync(question, isAutonomous: false);

            Assert.Equal("User's answer", answer);
            mockPromptService.Verify(
                m => m.PromptOnLLMQuestionAsync(It.Is<LLMQuestionPrompt>(q => q.QuestionText == question.QuestionText), true),
                Times.Once);
        }

        [Fact]
        public async Task HandleLLMQuestionAsync_WithClarificationQuestion_ReturnsDefaultAnswer()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var service = new LlmQuestionService(mockPromptService.Object);

            var question = new LLMQuestionPrompt
            {
                QuestionText = "What approach should I use?",
                QuestionType = LLMQuestionType.Clarification
            };

            var answer = await service.HandleLLMQuestionAsync(question, isAutonomous: true, AutoAnswerResponse.Default);

            Assert.NotEmpty(answer);
        }

        [Fact]
        public async Task HandleLLMQuestionAsync_WithConfirmationQuestion_ReturnsYes()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var service = new LlmQuestionService(mockPromptService.Object);

            var question = new LLMQuestionPrompt
            {
                QuestionText = "Should I apply this optimization?",
                QuestionType = LLMQuestionType.Confirmation
            };

            var answer = await service.HandleLLMQuestionAsync(question, isAutonomous: true, AutoAnswerResponse.Aggressive);

            Assert.Contains("proceed", answer.ToLowerInvariant());
        }

        [Fact]
        public async Task DetectLLMQuestionAsync_ParsesLLMResponseForQuestion()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var service = new LlmQuestionService(mockPromptService.Object);

            var llmResponse = "I've analyzed the error. Which approach should I use: (1) Retry or (2) Fallback?";

            var detected = await service.DetectLLMQuestionAsync(llmResponse);

            Assert.NotNull(detected);
            Assert.Contains("?", detected.QuestionText);
            Assert.Equal(LLMQuestionType.Selection, detected.QuestionType);
        }

        [Fact]
        public async Task HandleLLMQuestionAsync_InvalidQuestion_ThrowsArgumentException()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var service = new LlmQuestionService(mockPromptService.Object);

            var question = new LLMQuestionPrompt
            {
                QuestionText = "" // Empty question text
            };

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.HandleLLMQuestionAsync(question, isAutonomous: true));
        }

        [Fact]
        public async Task DetectLLMQuestionAsync_NoQuestion_ReturnsNull()
        {
            var mockPromptService = new Mock<IInteractivePromptService>();
            var service = new LlmQuestionService(mockPromptService.Object);

            var llmResponse = "I've completed the analysis. No further questions.";

            var detected = await service.DetectLLMQuestionAsync(llmResponse);

            Assert.Null(detected);
        }
    }
}
