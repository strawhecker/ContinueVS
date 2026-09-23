#nullable enable

using ContinueVS.Core.Types;
using Newtonsoft.Json;
using Xunit;

namespace ContinueVS.Tests.Core
{
    public class MessageViewModeTests
    {
        [Fact]
        public void ViewMode_Default_IsPretty()
        {
            // Every card rests in Pretty: arguably the resting default for reading.
            var message = new ChatMessage();
            Assert.Equal(MessageViewMode.Pretty, message.ViewMode);
        }

        [Fact]
        public void ViewMode_CanBeToggled()
        {
            var message = new ChatMessage { ViewMode = MessageViewMode.Raw };
            Assert.Equal(MessageViewMode.Raw, message.ViewMode);
            message.ViewMode = MessageViewMode.Pretty;
            Assert.Equal(MessageViewMode.Pretty, message.ViewMode);
        }

        [Fact]
        public void ViewMode_IsJsonIgnored_NotSerialized()
        {
            // ViewMode is session-only (non-sticky); it must never be persisted or reach the LLM.
            var message = new ChatMessage { ViewMode = MessageViewMode.Raw, Content = "hello" };
            var json = JsonConvert.SerializeObject(message);
            Assert.DoesNotContain("viewMode", json, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ViewMode", json);
        }

        [Fact]
        public void ViewMode_FiresPropertyChanged()
        {
            var message = new ChatMessage();
            var fired = false;
            message.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatMessage.ViewMode))
                    fired = true;
            };

            message.ViewMode = MessageViewMode.Raw;
            Assert.True(fired);
        }
    }
}
