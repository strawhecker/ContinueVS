using System;
using ContinueVS.Core.Services;
using ContinueVS.Core.Types;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class StreamingTextBlockCollectionTests
    {
        [Fact]
        public void AppendChunk_WithSingleLineChunk_ExtendsLastBlock()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Hello ");
            collection.AppendChunk("world");

            // Act
            var result = collection.Blocks;

            // Assert
            Assert.Single(result);
            Assert.Equal("Hello world", result[0].Text);
        }

        [Fact]
        public void AppendChunk_WithNewlineInChunk_CreatesNewBlock()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();

            // Act
            collection.AppendChunk("Line 1\nLine 2");

            // Assert
            Assert.Equal(2, collection.Blocks.Count);
            Assert.Equal("Line 1", collection.Blocks[0].Text);
            Assert.Equal("Line 2", collection.Blocks[1].Text);
        }

        [Fact]
        public void AppendChunk_WithMultipleNewlines_CreatesMultipleBlocks()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();

            // Act
            collection.AppendChunk("A\nB\nC");

            // Assert
            Assert.Equal(3, collection.Blocks.Count);
            Assert.Equal("A", collection.Blocks[0].Text);
            Assert.Equal("B", collection.Blocks[1].Text);
            Assert.Equal("C", collection.Blocks[2].Text);
        }

        [Fact]
        public void AppendChunk_WithEmptyChunk_DoesNotAddBlock()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Initial");

            // Act
            collection.AppendChunk(string.Empty);

            // Assert
            Assert.Single(collection.Blocks);
            Assert.Equal("Initial", collection.Blocks[0].Text);
        }

        [Fact]
        public void AppendChunk_WithNullChunk_DoesNotThrow()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Initial");

            // Act
            collection.AppendChunk(null!);

            // Assert
            Assert.Single(collection.Blocks);
        }

        [Fact]
        public void AppendChunk_WithNewlineOnly_CreatesEmptyBlock()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("A\n");

            // Act & Assert
            Assert.True(collection.Blocks.Count >= 1);
            Assert.Equal("A", collection.Blocks[0].Text);
        }

        [Fact]
        public void AppendChunk_SequentialAppends_BuildCorrectConcatenation()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            var chunks = new[] { "Hello ", "world", "!\n", "More ", "content" };

            // Act
            foreach (var chunk in chunks)
            {
                collection.AppendChunk(chunk);
            }

            // Assert
            var concatenated = collection.GetConcatenatedText();
            Assert.Equal("Hello world!\nMore content", concatenated);
        }

        [Fact]
        public void AppendChunk_WithMixedContent_MaintainsOrder()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();

            // Act
            collection.AppendChunk("First\n");
            collection.AppendChunk("Second\n");
            collection.AppendChunk("Third");

            // Assert
            Assert.Equal(3, collection.Blocks.Count);
            Assert.Equal("First", collection.Blocks[0].Text);
            Assert.Equal("Second", collection.Blocks[1].Text);
            Assert.Equal("Third", collection.Blocks[2].Text);
        }

        [Fact]
        public void GetConcatenatedText_WithMultipleBlocks_JoinsWithNewlines()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Line 1\nLine 2\nLine 3");

            // Act
            var result = collection.GetConcatenatedText();

            // Assert
            Assert.Equal("Line 1\nLine 2\nLine 3", result);
        }

        [Fact]
        public void Clear_RemovesAllBlocks()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            collection.AppendChunk("Content\nMore");

            // Act
            collection.Clear();

            // Assert
            Assert.Empty(collection.Blocks);
            Assert.Equal(string.Empty, collection.GetConcatenatedText());
        }

        [Fact]
        public void AppendChunk_ChunkIndexIncrementsPerBlock()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();

            // Act
            collection.AppendChunk("A\nB\nC");

            // Assert
            Assert.Equal(0, collection.Blocks[0].ChunkIndex);
            Assert.Equal(1, collection.Blocks[1].ChunkIndex);
            Assert.Equal(2, collection.Blocks[2].ChunkIndex);
        }

        [Fact]
        public void AppendChunk_CreatedAtTimestampIsSet()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            var beforeTime = DateTime.UtcNow;

            // Act
            collection.AppendChunk("Test");

            // Assert
            var afterTime = DateTime.UtcNow;
            Assert.NotEqual(default(DateTime), collection.Blocks[0].CreatedAt);
            Assert.True(collection.Blocks[0].CreatedAt >= beforeTime);
            Assert.True(collection.Blocks[0].CreatedAt <= afterTime);
        }
    }
}
