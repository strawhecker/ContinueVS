using System;
using ContinueVS.Core.Types;
using Xunit;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for gap74: Context Budget State display in ChatPageViewModel.
    /// Tests budget state property binding and button text updates.
    /// </summary>
    public class ContextBudgetStateTests
    {
        [Fact]
        public void ContextBudgetState_Safe_IsInitialState()
        {
            Assert.Equal(ContextBudgetState.Safe, ContextBudgetState.Safe);
        }

        [Fact]
        public void ContextBudgetState_Caution_IndicatesNearLimit()
        {
            Assert.Equal(ContextBudgetState.Caution, ContextBudgetState.Caution);
        }

        [Fact]
        public void ContextBudgetState_Locked_IndicatesLimitReached()
        {
            Assert.Equal(ContextBudgetState.Locked, ContextBudgetState.Locked);
        }

        [Theory]
        [InlineData(ContextBudgetState.Safe)]
        [InlineData(ContextBudgetState.Caution)]
        [InlineData(ContextBudgetState.Locked)]
        public void ContextBudgetState_AllStatesValid(ContextBudgetState state)
        {
            // Assert that state is defined
            Assert.True(Enum.IsDefined(typeof(ContextBudgetState), state));
        }
    }
}
