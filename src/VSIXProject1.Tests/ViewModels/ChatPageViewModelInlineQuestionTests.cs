using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Tests.Helpers;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for the inline LLM question answer/cancel flow (gap96/97).
    /// Verifies that cancelling an inline question returns a literal "no answer" sentinel rather
    /// than fabricating a policy default (which would be auto-answering in a no-automation context).
    /// </summary>
    public class ChatPageViewModelInlineQuestionTests
    {
        [Fact]
        public async Task AddInlineQuestionAsync_Cancel_ReturnsNoAnswerSentinel()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();

            var question = new LLMQuestionMessage(
                "Should I proceed?",
                LLMQuestionType.Confirmation,
                requireHumanDecision: false);

            // Start adding the question; it will wait for an answer/cancel.
            var addTask = vm.AddInlineQuestionAsync(question);

            // Simulate the user dismissing the prompt (Cancel button in ChatPage.xaml.cs).
            Assert.NotNull(question.OnCancelAsync);
            await question.OnCancelAsync!.Invoke();

            var answer = await addTask;

            // Cancelling must NOT fabricate a policy default (e.g. "Yes, proceed with caution.").
            // It returns a literal no-answer sentinel so the caller can decide how to proceed.
            Assert.Equal("[no answer]", answer);
        }

        [Fact]
        public async Task AddInlineQuestionAsync_RequireHumanDecisionCancel_ReturnsNoAnswerSentinel()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();

            var question = new LLMQuestionMessage(
                "Which irreversible approach?",
                LLMQuestionType.Selection,
                requireHumanDecision: true);

            var addTask = vm.AddInlineQuestionAsync(question);

            Assert.NotNull(question.OnCancelAsync);
            await question.OnCancelAsync!.Invoke();

            var answer = await addTask;

            // Flagged questions must never be auto-answered; cancel yields the no-answer sentinel.
            Assert.Equal("[no answer]", answer);
        }
    }
}
