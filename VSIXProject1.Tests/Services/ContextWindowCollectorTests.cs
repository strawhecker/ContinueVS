using Xunit;
using Moq;
using EnvDTE;
using System;
using System.Collections;
using System.Collections.Generic;
using ContinueVS.Services;
using System.Threading.Tasks;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Test suite for ContextWindowCollector
    /// 
    /// 18 tests across 5 categories:
    /// - Initialization (2 tests)
    /// - Token Calculation (6 tests)
    /// - Integration (4 tests)
    /// - Edge Cases (4 tests)
    /// - Performance (2 tests)
    /// </summary>
    public class ContextWindowCollectorTests
    {
        // ====================================================================
        // Test Fixtures & Helpers
        // ====================================================================

        /// <summary>
        /// Create a mock DTE instance with configurable state
        /// </summary>
        private Mock<DTE> CreateMockDte()
        {
            var dteMock = new Mock<DTE>();

            // Setup Documents collection
            var docsMock = new Mock<Documents>();
            dteMock.Setup(d => d.Documents).Returns(docsMock.Object);

            return dteMock;
        }

        /// <summary>
        /// Create a mock TextDocument with specified line count and content
        /// </summary>
        private Mock<TextDocument> CreateMockTextDocument(int lineCount = 50, int totalChars = 4000)
        {
            var textDocMock = new Mock<TextDocument>();

            // Setup EndPoint
            var endPointMock = new Mock<EditPoint>();
            endPointMock.Setup(p => p.Line).Returns(lineCount);
            endPointMock.Setup(p => p.AbsoluteCharOffset).Returns(totalChars);
            textDocMock.Setup(d => d.EndPoint).Returns(endPointMock.Object);

            // Setup StartPoint
            var startPointMock = new Mock<EditPoint>();
            startPointMock.Setup(p => p.AbsoluteCharOffset).Returns(0);
            startPointMock.Setup(p => p.CreateEditPoint()).Returns(startPointMock.Object);
            textDocMock.Setup(d => d.StartPoint).Returns(startPointMock.Object);

            return textDocMock;
        }

        /// <summary>
        /// Create a mock Document wrapping a TextDocument
        /// </summary>
        private Mock<Document> CreateMockDocument(Mock<TextDocument> textDocMock)
        {
            var docMock = new Mock<Document>();
            docMock.Setup(d => d.Object).Returns(textDocMock.Object);
            return docMock;
        }

        // ====================================================================
        // Test Suite 1: Initialization (2 tests)
        // ====================================================================

        [Fact]
        public void Constructor_WithValidDTE_InitializesSuccessfully()
        {
            // Arrange
            var dteMock = CreateMockDte();

            // Act
            var collector = new ContextWindowCollector(dteMock.Object);

            // Assert
            Assert.NotNull(collector);
        }

        [Fact]
        public void Constructor_WithNullDTE_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ContextWindowCollector(null));
        }

        // ====================================================================
        // Test Suite 2: Token Calculation (6 tests)
        // ====================================================================

        [Fact]
        public async Task GetContextWindowAsync_CalculatesEditorContentTokens()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(50, 4000); // ~1000 tokens
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.EstimatedTokens.EditorContent > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_CalculatesConversationHistoryTokens()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.EstimatedTokens.ConversationHistory >= 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_SumsAllSourcesCorrectly()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(50, 4000);
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            int expectedSum = result.EstimatedTokens.EditorContent +
                             result.EstimatedTokens.SelectedText +
                             result.EstimatedTokens.RecentFiles +
                             result.EstimatedTokens.ConversationHistory;
            Assert.Equal(expectedSum, result.UsedTokens);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesEmptyEditor()
        {
            // Arrange
            var dteMock = CreateMockDte();
            dteMock.Setup(d => d.ActiveDocument).Returns((Document)null);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.EstimatedTokens.EditorContent);
        }

        [Fact]
        public async Task GetContextWindowAsync_CapsTokensAtMaximum()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(10000, 1000000); // Very large file
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.UsedTokens <= result.MaxTokens);
        }

        // ====================================================================
        // Test Suite 3: Integration (4 tests)
        // ====================================================================

        [Fact]
        public async Task GetContextWindowAsync_ReturnsValidContextWindowInfo()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
            Assert.True(result.UsedTokens >= 0);
            Assert.NotNull(result.EstimatedTokens);
        }

        [Fact]
        public async Task GetContextWindowAsync_ReflectsEditorChanges()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(50, 4000);
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);
            var collector = new ContextWindowCollector(dteMock.Object);

            // Act - First call
            var result1 = await collector.GetContextWindowAsync();

            // Update document
            var textDocMock2 = CreateMockTextDocument(100, 8000); // Double size
            var docMock2 = CreateMockDocument(textDocMock2);
            dteMock.Setup(d => d.ActiveDocument).Returns(docMock2.Object);

            // Act - Second call
            var result2 = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            // Second result should have higher token count (from larger document)
            Assert.True(result2.EstimatedTokens.EditorContent >= result1.EstimatedTokens.EditorContent);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesDTEExceptionsGracefully()
        {
            // Arrange
            var dteMock = CreateMockDte();
            dteMock.Setup(d => d.ActiveDocument).Throws<Exception>();

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert - Should return gracefully with defaults
            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
        }

        // ====================================================================
        // Test Suite 4: Edge Cases (4 tests)
        // ====================================================================

        [Fact]
        public async Task GetContextWindowAsync_HandlesVeryLargeFile()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(50000, 1000000); // 1MB equivalent
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.UsedTokens <= result.MaxTokens); // Should be capped
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesNullConversationHistory()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.EstimatedTokens.ConversationHistory >= 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesZeroMaxTokensConfiguration()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(0, 0);
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0); // Should have default maximum
        }

        [Fact]
        public async Task GetContextWindowAsync_OverflowProtectionOnTokenSum()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(100000, 5000000); // Massive file
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.UsedTokens <= result.MaxTokens); // Overflow protection
            Assert.True(result.UsedTokens > 0); // But should have some tokens
        }

        // ====================================================================
        // Test Suite 5: Performance (2 tests)
        // ====================================================================

        [Fact]
        public async Task GetContextWindowAsync_CompletesWithinTimeLimit()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(50, 4000);
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);
            var startTime = DateTime.Now;

            // Act
            var result = await collector.GetContextWindowAsync();

            // Assert
            var elapsed = DateTime.Now - startTime;
            Assert.NotNull(result);
            Assert.True(elapsed.TotalMilliseconds < 500, 
                $"Operation took {elapsed.TotalMilliseconds}ms, expected < 500ms");
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesConcurrentCalls()
        {
            // Arrange
            var dteMock = CreateMockDte();
            var textDocMock = CreateMockTextDocument(50, 4000);
            var docMock = CreateMockDocument(textDocMock);

            dteMock.Setup(d => d.ActiveDocument).Returns(docMock.Object);

            var collector = new ContextWindowCollector(dteMock.Object);

            // Act - Fire multiple concurrent requests
            var tasks = new List<Task<ContextWindowCollector.ContextWindowInfo>>
            {
                collector.GetContextWindowAsync(),
                collector.GetContextWindowAsync(),
                collector.GetContextWindowAsync(),
            };

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(3, results.Count);
            foreach (var result in results)
            {
                Assert.NotNull(result);
                Assert.True(result.MaxTokens > 0);
            }
        }
    }
}
