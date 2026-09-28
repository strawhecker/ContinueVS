using ContinueVS.Tests.Helpers;
using Xunit;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// gap86_1: Tests for the HasInputText property on ChatPageViewModel, which drives the
    /// "Use text below as answer" claim button. The claim copies the composer's draft into the
    /// card's own independent answer string and never mutates the composer, so the property only
    /// reflects whether the composer currently has non-whitespace text.
    /// </summary>
    public class ChatPageViewModelHasInputTextTests
    {
        [Fact]
        public void HasInputText_EmptyInput_IsFalse()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            vm.InputText = string.Empty;

            Assert.False(vm.HasInputText);
        }

        [Fact]
        public void HasInputText_WhitespaceInput_IsFalse()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            vm.InputText = "   \t  ";

            Assert.False(vm.HasInputText);
        }

        [Fact]
        public void HasInputText_TextInput_IsTrue()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            vm.InputText = "Use this as my answer";

            Assert.True(vm.HasInputText);
        }

        [Fact]
        public void HasInputText_SettingBackToEmpty_UpdatesToFalse()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            vm.InputText = "some draft";
            Assert.True(vm.HasInputText);

            vm.InputText = string.Empty;
            Assert.False(vm.HasInputText);
        }
    }
}
