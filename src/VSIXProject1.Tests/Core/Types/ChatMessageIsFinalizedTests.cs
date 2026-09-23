using System;
using ContinueVS.Core.Types;
using Xunit;

namespace ContinueVS.Tests.Core.Types
{
    public class ChatMessageIsFinalizedTests
    {
        [Fact]
        public void IsFinalized_Default_False()
        {
            var message = new ChatMessage();
            Assert.False(message.IsFinalized);
        }

        [Fact]
        public void IsFinalized_FinalizeStreaming_SetsTrue()
        {
            var message = new ChatMessage();
            message.AppendChunk("hello");
            message.FinalizeStreaming();
            Assert.True(message.IsFinalized);
        }

        [Fact]
        public void Content_Setter_SetsIsFinalized()
        {
            var message = new ChatMessage { Content = "full text" };
            Assert.True(message.IsFinalized);
        }

        [Fact]
        public void FinalizeStreaming_FiresIsFinalizedPropertyChanged()
        {
            var message = new ChatMessage();
            bool fired = false;
            message.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatMessage.IsFinalized))
                    fired = true;
            };

            message.AppendChunk("abc");
            message.FinalizeStreaming();

            Assert.True(fired);
        }

        [Fact]
        public void AppendChunk_DoesNotSetIsFinalized()
        {
            var message = new ChatMessage();
            message.AppendChunk("incremental");
            Assert.False(message.IsFinalized);
        }

        [Fact]
        public void FinalizeStreaming_FiresTokenAppended_WithPendingContent()
        {
            var message = new ChatMessage();
            int tokenEvents = 0;
            message.TokenAppended += t => tokenEvents++;

            message.AppendChunk("abc");
            message.FinalizeStreaming();

            Assert.Equal("abc", message.Content);
            Assert.True(tokenEvents >= 1, "TokenAppended should have fired during append/finalize");
        }

        [Fact]
        public void Content_MatchesBuffer_BeforeFinalize()
        {
            var message = new ChatMessage();
            message.AppendChunk("line one\n");
            Assert.Equal("line one\n", message.Content);
        }

        [Fact]
        public void Content_IsCachedString_AfterFinalize()
        {
            var message = new ChatMessage();
            message.AppendChunk("cached");
            message.FinalizeStreaming();
            string first = message.Content;
            Assert.Equal("cached", first);
        }

        [Fact]
        public void IsMinimized_IndependentOfIsFinalized()
        {
            var message = new ChatMessage();
            message.AppendChunk("x");
            message.IsMinimized = true;
            message.FinalizeStreaming();
            Assert.True(message.IsMinimized);
            Assert.True(message.IsFinalized);
        }
    }
}
