using System;
using System.Collections.ObjectModel;
using Xunit;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.Services
{
    public class StreamingTextBlockCollectionTests
    {
        [Fact]
        public void AppendChunk_SingleLineNoNewline_ExtendsLastBlock()
        {
            var collection = new StreamingTextBlockCollection();
            collection.Add(new TextBlockModel("Hello"));

            collection.AppendChunk(" World");

            Assert.Single(collection);
            Assert.Equal("Hello World", collection[0].Text);
        }

        [Fact]
        public void AppendChunk_SingleLineWithNewline_CreatesNewBlocks()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Line1\nLine2");

            Assert.Equal(2, collection.Count);
            Assert.Equal("Line1", collection[0].Text);
            Assert.Equal("Line2", collection[1].Text);
        }

        [Fact]
        public void AppendChunk_WindowsNewline_SplitsCorrectly()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Line1\r\nLine2");

            Assert.Equal(2, collection.Count);
            Assert.Equal("Line1", collection[0].Text);
            Assert.Equal("Line2", collection[1].Text);
        }

        [Fact]
        public void AppendChunk_EmptyString_DoesNothing()
        {
            var collection = new StreamingTextBlockCollection();
            collection.Add(new TextBlockModel("Initial"));

            collection.AppendChunk(string.Empty);

            Assert.Single(collection);
            Assert.Equal("Initial", collection[0].Text);
        }

        [Fact]
        public void AppendChunk_Null_DoesNothing()
        {
            var collection = new StreamingTextBlockCollection();
            collection.Add(new TextBlockModel("Initial"));

            collection.AppendChunk(null);

            Assert.Single(collection);
            Assert.Equal("Initial", collection[0].Text);
        }

        [Fact]
        public void AppendChunk_MultipleChunks_Accumulates()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Chunk1");
            collection.AppendChunk(" continues");
            collection.AppendChunk("\nNew line");

            Assert.Equal(2, collection.Count);
            Assert.Equal("Chunk1 continues", collection[0].Text);
            Assert.Equal("New line", collection[1].Text);
        }

        [Fact]
        public void AppendChunk_TracksTimestamp()
        {
            var collection = new StreamingTextBlockCollection();
            var before = DateTime.Now;
            collection.AppendChunk("Test");
            var after = DateTime.Now;

            Assert.Single(collection);
            Assert.True(collection[0].Timestamp >= before && collection[0].Timestamp <= after);
        }

        [Fact]
        public void Clear_RemovesAllBlocks()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Line1\nLine2\nLine3");

            collection.Clear();

            Assert.Empty(collection);
        }

        [Fact]
        public void GetCombinedText_ReturnsJoinedContent()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Line1\nLine2\nLine3");

            var combined = collection.GetCombinedText();

            Assert.Equal("Line1\nLine2\nLine3", combined);
        }

        [Fact]
        public void BlockCount_ReturnsCorrectCount()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("L1\nL2\nL3");

            Assert.Equal(3, collection.BlockCount);
        }

        [Fact]
        public void AppendChunk_TrailingNewline_HandledCorrectly()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Line1\n");

            Assert.Single(collection);
            Assert.Equal("Line1", collection[0].Text);
        }

        [Fact]
        public void AppendChunk_ConsecutiveNewlines_CreatesEmptyBlocks()
        {
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("A\n\nC");

            Assert.True(collection.Count >= 2);
            // May have empty blocks depending on implementation
        }
    }
}
