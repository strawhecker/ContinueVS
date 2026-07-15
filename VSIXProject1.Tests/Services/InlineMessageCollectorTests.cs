using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Bridge.IPC;
using ContinueVS.Bridge.Services;
using Xunit;
using Moq;
using EnvDTE;

namespace ContinueVS.Bridge.Tests.Services
{
    /// <summary>
    /// Unit tests for InlineMessageCollector.
    /// 
    /// Covers:
    /// - Initialization and null checks
    /// - GetInlineMessages (valid position, invalid filepath, out-of-bounds)
    /// - PostInlineMessage (valid/invalid messages, error handling)
    /// - ClearMessages (all messages, specific position, non-existent)
    /// - Edge cases (special characters, large numbers)
    /// 
    /// Test Count: 15 tests
    /// </summary>
    public class InlineMessageCollectorTests
    {
        private static DTE CreateMockDte()
        {
            var mockDte = new Mock<DTE>();
            return mockDte.Object;
        }

        // ====================================================================
        // INITIALIZATION TESTS (2 tests)
        // ====================================================================

        [Fact]
        public void Constructor_WithValidDte_Succeeds()
        {
            // Arrange
            var mockDte = CreateMockDte();

            // Act
            var collector = new InlineMessageCollector(mockDte);

            // Assert
            Assert.NotNull(collector);
            Assert.Equal(0, collector.GetStoredMessageCount());
        }

        [Fact]
        public void Constructor_WithNullDte_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new InlineMessageCollector(null));
            Assert.Contains("DTE", ex.Message);
        }

        // ====================================================================
        // GET_INLINE_MESSAGES TESTS (4 tests)
        // ====================================================================

        [Fact]
        public async Task GetInlineMessagesAsync_WithValidPosition_ReturnsArray()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);
            var message = new InlineMessage
            {
                Filepath = "/test.cs",
                Line = 10,
                Column = 5,
                Title = "Test message",
            };
            await collector.PostInlineMessageAsync(message);

            // Act
            var result = await collector.GetInlineMessagesAsync("/test.cs", 10, 5);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<InlineMessage[]>(result);
            Assert.Equal(1, result.Length);
            Assert.Equal("Test message", result[0].Title);
        }

        [Fact]
        public async Task GetInlineMessagesAsync_WithInvalidFilepath_ReturnsEmptyArray()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);

            // Act
            var result = await collector.GetInlineMessagesAsync("", 0, 0);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetInlineMessagesAsync_WithOutOfBoundsPosition_ReturnsEmptyArray()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);

            // Act
            var result1 = await collector.GetInlineMessagesAsync("/test.cs", -1, 0);
            var result2 = await collector.GetInlineMessagesAsync("/test.cs", 0, -5);

            // Assert
            Assert.Empty(result1);
            Assert.Empty(result2);
        }

        [Fact]
        public async Task GetInlineMessagesAsync_WhenNoMessagesAtPosition_ReturnsEmptyArray()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);
            var message = new InlineMessage
            {
                Filepath = "/test.cs",
                Line = 10,
                Column = 5,
                Title = "Test",
            };
            await collector.PostInlineMessageAsync(message);

            // Act
            var result = await collector.GetInlineMessagesAsync("/test.cs", 15, 5);

            // Assert
            Assert.Empty(result);
        }

        // ====================================================================
        // POST_INLINE_MESSAGE TESTS (4 tests)
        // ====================================================================

        [Fact]
        public async Task PostInlineMessageAsync_WithValidMessage_ReturnsTrue()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);
            var message = new InlineMessage
            {
                Filepath = "/test.cs",
                Line = 5,
                Column = 10,
                Title = "Fix this",
            };

            // Act
            var result = await collector.PostInlineMessageAsync(message);

            // Assert
            Assert.True(result);
            Assert.Equal(1, collector.GetStoredMessageCount());
        }

        [Fact]
        public async Task PostInlineMessageAsync_WithNullTitle_ReturnsFalse()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);
            var message = new InlineMessage
            {
                Filepath = "/test.cs",
                Line = 5,
                Column = 10,
                Title = null,
            };

            // Act
            var result = await collector.PostInlineMessageAsync(message);

            // Assert
            Assert.False(result);
            Assert.Equal(0, collector.GetStoredMessageCount());
        }

        [Fact]
        public async Task PostInlineMessageAsync_WithInvalidPosition_ReturnsFalse()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);
            var message = new InlineMessage
            {
                Filepath = "/test.cs",
                Line = -1,
                Column = 10,
                Title = "Fix",
            };

            // Act
            var result = await collector.PostInlineMessageAsync(message);

            // Assert
            Assert.False(result);
            Assert.Equal(0, collector.GetStoredMessageCount());
        }

        [Fact]
        public async Task PostInlineMessageAsync_WithNullMessage_ReturnsFalse()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);

            // Act
            var result = await collector.PostInlineMessageAsync(null);

            // Assert
            Assert.False(result);
            Assert.Equal(0, collector.GetStoredMessageCount());
        }

        // ====================================================================
        // CLEAR_MESSAGES TESTS (3 tests)
        // ====================================================================

        [Fact]
        public async Task ClearMessagesAsync_WithAllMessages_ReturnsCount()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);

            var msg1 = new InlineMessage { Filepath = "/test.cs", Line = 1, Column = 0, Title = "M1" };
            var msg2 = new InlineMessage { Filepath = "/test.cs", Line = 2, Column = 0, Title = "M2" };
            var msg3 = new InlineMessage { Filepath = "/test.cs", Line = 3, Column = 0, Title = "M3" };

            await collector.PostInlineMessageAsync(msg1);
            await collector.PostInlineMessageAsync(msg2);
            await collector.PostInlineMessageAsync(msg3);

            // Act
            var cleared = await collector.ClearMessagesAsync("/test.cs");

            // Assert
            Assert.Equal(3, cleared);
            Assert.Equal(0, collector.GetStoredMessageCount());
        }

        [Fact]
        public async Task ClearMessagesAsync_WithSpecificPosition_ReturnsClearedCount()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);

            var msg1 = new InlineMessage { Filepath = "/test.cs", Line = 1, Column = 0, Title = "M1" };
            var msg2 = new InlineMessage { Filepath = "/test.cs", Line = 1, Column = 5, Title = "M2" };
            var msg3 = new InlineMessage { Filepath = "/test.cs", Line = 2, Column = 0, Title = "M3" };

            await collector.PostInlineMessageAsync(msg1);
            await collector.PostInlineMessageAsync(msg2);
            await collector.PostInlineMessageAsync(msg3);

            // Act
            var cleared = await collector.ClearMessagesAsync("/test.cs", line: 1);

            // Assert
            Assert.Equal(2, cleared);
            Assert.Equal(1, collector.GetStoredMessageCount());

            // Verify remaining message
            var remaining = await collector.GetInlineMessagesAsync("/test.cs", 2, 0);
            Assert.Single(remaining);
        }

        [Fact]
        public async Task ClearMessagesAsync_WithNonExistentFile_ReturnsZero()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);

            // Act
            var cleared = await collector.ClearMessagesAsync("/nonexistent.cs");

            // Assert
            Assert.Equal(0, cleared);
        }

        // ====================================================================
        // EDGE CASE TESTS (2 tests)
        // ====================================================================

        [Fact]
        public async Task PostAndGetMessages_WithLargeLineColumn_Succeeds()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);
            var message = new InlineMessage
            {
                Filepath = "/test.cs",
                Line = 999999,
                Column = 999999,
                Title = "Large position",
            };

            // Act
            var posted = await collector.PostInlineMessageAsync(message);
            var retrieved = await collector.GetInlineMessagesAsync("/test.cs", 999999, 999999);

            // Assert
            Assert.True(posted);
            Assert.Single(retrieved);
            Assert.Equal("Large position", retrieved[0].Title);
        }

        [Fact]
        public async Task PostMessages_WithSpecialCharactersInFilepath_Succeeds()
        {
            // Arrange
            var mockDte = CreateMockDte();
            var collector = new InlineMessageCollector(mockDte);
            var message = new InlineMessage
            {
                Filepath = @"C:\Projects\MyProject\src\My-File_v2.0.cs",
                Line = 5,
                Column = 10,
                Title = "Special path",
            };

            // Act
            var posted = await collector.PostInlineMessageAsync(message);
            var retrieved = await collector.GetInlineMessagesAsync(
                @"C:\Projects\MyProject\src\My-File_v2.0.cs", 5, 10);

            // Assert
            Assert.True(posted);
            Assert.Single(retrieved);
        }
    }
}
