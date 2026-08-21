using Xunit;
using System;
using System.Collections.Generic;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using ContinueVS.Core.Types;
using System.Threading.Tasks;

#nullable enable

namespace ContinueVS.Tests.Services
{
    public class ContextWindowCollectorTests
    {
        /// <summary>
        /// Simple stub implementation of IDteProvider without Moq to avoid interop dependency
        /// </summary>
        private class StubDteProvider : IDteProvider
        {
            public string ActiveDocumentContent { get; set; } = string.Empty;
            public string SelectedText { get; set; } = string.Empty;
            public List<string> RecentFilesData { get; set; } = new();

            public string GetSelectedText()
            {
                return SelectedText;
            }

            public string GetActiveDocumentContent()
            {
                return ActiveDocumentContent;
            }

            public List<string> GetRecentFiles(int maxCount)
            {
                return new List<string>(RecentFilesData.Take(maxCount));
            }
        }

        [Fact]
        public void Constructor_WithValidDteProvider_InitializesSuccessfully()
        {
            var dte = new StubDteProvider();
            var collector = new ContextWindowCollector(dte);
            Assert.NotNull(collector);
        }

        [Fact]
        public void Constructor_WithNullDteProvider_ThrowsArgumentNullException()
        {
            IDteProvider? nullProvider = null;
            Assert.Throws<ArgumentNullException>(() => new ContextWindowCollector(nullProvider!));
        }

        [Fact]
        public async Task GetContextWindowAsync_ReturnsValidContextWindowInfo()
        {
            var dte = new StubDteProvider
            {
                ActiveDocumentContent = "sample content"
            };

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
            Assert.True(result.UsedTokens >= 0);
            Assert.NotNull(result.EstimatedTokens);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesNullOrEmptyContent()
        {
            var dte = new StubDteProvider();

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
            Assert.Equal(0, result.EstimatedTokens.EditorContent);
            Assert.Equal(0, result.EstimatedTokens.SelectedText);
        }

        [Fact]
        public async Task GetContextWindowAsync_ReservedForNewContext_IsCalculated()
        {
            var dte = new StubDteProvider
            {
                ActiveDocumentContent = "test content"
            };

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.ReservedForNewContext > 0);
            // ReservedForNewContext = MaxTokens - UsedTokens - safetyMargin (5%)
            Assert.True(result.ReservedForNewContext <= result.MaxTokens - result.UsedTokens);
            Assert.True(result.ReservedForNewContext > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesConcurrentCalls()
        {
            var dte = new StubDteProvider
            {
                ActiveDocumentContent = "test"
            };

            var collector = new ContextWindowCollector(dte);

            var tasks = new List<Task<ContextWindowCollector.ContextWindowInfo>>
            {
                collector.GetContextWindowAsync(),
                collector.GetContextWindowAsync(),
                collector.GetContextWindowAsync(),
            };

            var results = await Task.WhenAll(tasks);

            Assert.Equal(3, results.Length);
            foreach (var result in results)
            {
                Assert.NotNull(result);
                Assert.True(result.MaxTokens > 0);
            }
        }

        [Fact]
        public async Task GetContextWindowAsync_EstimatesEditorTokensCorrectly()
        {
            var editorContent = "This is a sample document with some content.";
            var dte = new StubDteProvider
            {
                ActiveDocumentContent = editorContent
            };

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.EstimatedTokens.EditorContent > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_EstimatesSelectedTextTokens()
        {
            var dte = new StubDteProvider
            {
                ActiveDocumentContent = "full content",
                SelectedText = "selected text portion"
            };

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.EstimatedTokens.SelectedText > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_EstimatesRecentFilesTokens()
        {
            var dte = new StubDteProvider
            {
                RecentFilesData = new List<string> { "file1.cs", "file2.cs", "file3.cs" }
            };

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.EstimatedTokens.RecentFiles > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_EstimatesConversationHistoryTokens()
        {
            var dte = new StubDteProvider();

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.EstimatedTokens.ConversationHistory >= 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_CalculatesUsedTokensSum()
        {
            var dte = new StubDteProvider
            {
                ActiveDocumentContent = "editor",
                SelectedText = "selected",
                RecentFilesData = new List<string> { "file.cs" }
            };

            var collector = new ContextWindowCollector(dte);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            var expectedUsed = result.EstimatedTokens.EditorContent
                + result.EstimatedTokens.SelectedText
                + result.EstimatedTokens.RecentFiles
                + result.EstimatedTokens.ConversationHistory;
            Assert.Equal(expectedUsed, result.UsedTokens);
        }
    }
}
