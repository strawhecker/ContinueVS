#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for the ask_user human-in-the-loop tool (gap86).
    /// Verifies multiple-choice (with answers) and open-ended (without answers) behavior,
    /// plus sanitization, required-question validation, and mode availability.
    /// </summary>
    public class ToolServiceAskUserTests
    {
        private readonly Mock<IIdeService> _ideServiceMock;
        private readonly Mock<IConfigService> _configServiceMock;
        private readonly Mock<IInteractivePromptService> _promptServiceMock;

        public ToolServiceAskUserTests()
        {
            _ideServiceMock = new Mock<IIdeService>();
            _configServiceMock = new Mock<IConfigService>();
            _configServiceMock.Setup(s => s.GetEnabledTools())
                .Returns((IEnumerable<ToolDefinition>)BuiltInToolsRegistry.GetAllBuiltInTools());
            _promptServiceMock = new Mock<IInteractivePromptService>();
        }

        private ToolService CreateService()
        {
            return new ToolService(
                _ideServiceMock.Object,
                _configServiceMock.Object,
                interactivePromptService: _promptServiceMock.Object);
        }

        [Fact]
        public async Task InvokeAskUser_WithMultipleChoiceAnswers_ReturnsChosenOption()
        {
            _promptServiceMock
                .Setup(s => s.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()))
                .ReturnsAsync("Option B");

            var service = CreateService();
            var args = new Dictionary<string, object>
            {
                { "question", "Which strategy should I use?" },
                { "answers", new List<string> { "Option A", "Option B", "Option C" } }
            };

            var result = await service.InvokeAsync("ask_user", args);

            Assert.True(result.IsSuccess);
            Assert.Equal("Option B", result.Output);

            // Verify the prompt was a Selection question with options in the hint/context
            _promptServiceMock.Verify(
                s => s.PromptOnLLMQuestionAsync(
                    It.Is<LLMQuestionPrompt>(p =>
                        p.QuestionText == "Which strategy should I use?" &&
                        p.QuestionType == LLMQuestionType.Selection &&
                        p.AutoAnswerHint != null &&
                        p.AutoAnswerHint.Contains("Option A")),
                    It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAskUser_WithOpenEndedQuestion_PromptsClarification()
        {
            _promptServiceMock
                .Setup(s => s.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()))
                .ReturnsAsync("User prose answer");

            var service = CreateService();
            var args = new Dictionary<string, object>
            {
                { "question", "Can you describe the expected behavior?" }
            };

            var result = await service.InvokeAsync("ask_user", args);

            Assert.True(result.IsSuccess);
            Assert.Equal("User prose answer", result.Output);

            _promptServiceMock.Verify(
                s => s.PromptOnLLMQuestionAsync(
                    It.Is<LLMQuestionPrompt>(p =>
                        p.QuestionText == "Can you describe the expected behavior?" &&
                        p.QuestionType == LLMQuestionType.Clarification &&
                        p.AutoAnswerHint == null),
                    It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAskUser_WithEmptyQuestion_ReturnsError()
        {
            var service = CreateService();
            var args = new Dictionary<string, object> { { "question", "  " } };

            var result = await service.InvokeAsync("ask_user", args);

            Assert.False(result.IsSuccess);
            Assert.Contains("question", result.Output);
            _promptServiceMock.Verify(
                s => s.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task InvokeAskUser_WithNullPromptService_ReturnsError()
        {
            var service = new ToolService(_ideServiceMock.Object, _configServiceMock.Object);

            var result = await service.InvokeAsync("ask_user", new Dictionary<string, object>
            {
                { "question", "Proceed?" }
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("prompt service", result.Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InvokeAskUser_SanitizesControlCharactersFromAnswer()
        {
            _promptServiceMock
                .Setup(s => s.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()))
                .ReturnsAsync("  answer\twith\ncontrol\0chars  ");

            var service = CreateService();
            var result = await service.InvokeAsync("ask_user", new Dictionary<string, object>
            {
                { "question", "Anything to add?" }
            });

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain("\0", result.Output);
            Assert.DoesNotContain("\n", result.Output);
            Assert.Equal("answerwithcontrolchars", result.Output);
        }

        [Fact]
        public async Task InvokeAskUser_WhenPromptServiceThrows_ReturnsError()
        {
            _promptServiceMock
                .Setup(s => s.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("User cancelled"));

            var service = CreateService();
            var result = await service.InvokeAsync("ask_user", new Dictionary<string, object>
            {
                { "question", "Proceed?" }
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("User cancelled", result.Output);
        }

        [Fact]
        public async Task InvokeAskUser_WithRequireHumanDecision_ThreadsFlagIntoPrompt()
        {
            _promptServiceMock
                .Setup(s => s.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()))
                .ReturnsAsync("Option A");

            var service = CreateService();
            var args = new Dictionary<string, object>
            {
                { "question", "Which irreversible approach should we take?" },
                { "answers", new List<string> { "Option A", "Option B" } },
                { "requireHumanDecision", true }
            };

            var result = await service.InvokeAsync("ask_user", args);

            Assert.True(result.IsSuccess);
            _promptServiceMock.Verify(
                s => s.PromptOnLLMQuestionAsync(
                    It.Is<LLMQuestionPrompt>(p => p.RequireHumanDecision),
                    It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAskUser_WithoutRequireHumanDecision_DefaultsFalse()
        {
            _promptServiceMock
                .Setup(s => s.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()))
                .ReturnsAsync("Option A");

            var service = CreateService();
            var args = new Dictionary<string, object>
            {
                { "question", "Which routine strategy should I use?" },
                { "answers", new List<string> { "Option A", "Option B" } }
            };

            var result = await service.InvokeAsync("ask_user", args);

            Assert.True(result.IsSuccess);
            _promptServiceMock.Verify(
                s => s.PromptOnLLMQuestionAsync(
                    It.Is<LLMQuestionPrompt>(p => !p.RequireHumanDecision),
                    It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public void GetAvailableTools_IncludesAskUserInAgentAndDebugModes()
        {
            var service = CreateService();

            var agentTools = service.GetAvailableTools(ChatMode.Agent).ToList();
            var debugTools = service.GetAvailableTools(ChatMode.Debug).ToList();
            var planTools = service.GetAvailableTools(ChatMode.Plan).ToList();

            Assert.Contains(agentTools, t => t.Name == "ask_user");
            Assert.Contains(debugTools, t => t.Name == "ask_user");
            // ask_user is a loop-mode (interactive) tool; not in read-only Plan mode
            Assert.DoesNotContain(planTools, t => t.Name == "ask_user");
        }
    }
}
