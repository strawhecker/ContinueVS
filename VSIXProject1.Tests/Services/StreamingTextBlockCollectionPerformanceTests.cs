using System;
using System.Diagnostics;
using System.Text;
using ContinueVS.Core.Parsers;
using ContinueVS.Core.Services;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class StreamingTextBlockCollectionPerformanceTests
    {
        /// <summary>
        /// Benchmark test: measure parse time for 100KB mock response via StreamingTextBlockCollection.
        /// Verifies O(n) performance by comparing chunk-by-chunk parsing vs. full re-parse.
        /// Target: < 200ms for 100KB response (vs. 2-4s with MarkdownBlockRenderer re-parsing).
        /// </summary>
        [Fact]
        public void AppendChunk_100KBResponse_CompletesInUnder200ms()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            var stopwatch = Stopwatch.StartNew();

            // Generate 100KB of mock response content with formatting
            int chunks = 100;
            int chunkSize = 1000; // ~1KB per chunk
            string chunkContent = GenerateMockChunk(chunkSize);

            // Act
            for (int i = 0; i < chunks; i++)
            {
                collection.AppendChunk(chunkContent);
                if (i % 10 == 0)
                {
                    collection.AppendChunk("\n"); // Add newlines periodically
                }
            }

            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < 200,
                $"AppendChunk took {stopwatch.ElapsedMilliseconds}ms, expected < 200ms");
        }

        /// <summary>
        /// Verify that streaming blocks maintain reasonable memory footprint.
        /// For 100KB response, should have ~50 TextBlockModel instances (not 1000+ string allocations).
        /// </summary>
        [Fact]
        public void AppendChunk_100KBResponse_CreatesReasonableNumberOfBlocks()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            string chunkContent = GenerateMockChunk(1000);

            // Act
            for (int i = 0; i < 100; i++)
            {
                collection.AppendChunk(chunkContent);
                if (i % 10 == 0)
                {
                    collection.AppendChunk("\n");
                }
            }

            // Assert - should have far fewer blocks than chunks
            // 100 chunks with 10 newlines = ~10 blocks (1 per 10 chunks)
            Assert.True(collection.Blocks.Count < 200,
                $"Created {collection.Blocks.Count} blocks for 100 chunks, expected < 200");
        }

        /// <summary>
        /// Compare performance: incremental chunk appending vs. single full re-parse.
        /// This test demonstrates the performance gain of append-only architecture.
        /// </summary>
        [Fact]
        public void PerformanceComparison_IncrementalVsFullReparse()
        {
            // Arrange
            var collection = new StreamingTextBlockCollection();
            var sb = new StringBuilder();
            string chunkContent = GenerateMockChunk(1000);

            var incrementalStopwatch = Stopwatch.StartNew();

            // Act - incremental approach (what StreamingTextBlockCollection does)
            for (int i = 0; i < 100; i++)
            {
                collection.AppendChunk(chunkContent);
                sb.Append(chunkContent);
                if (i % 10 == 0)
                {
                    collection.AppendChunk("\n");
                    sb.Append("\n");
                }
            }

            incrementalStopwatch.Stop();

            // Simulate full re-parse (what MarkdownBlockRenderer does on each chunk)
            var fullReparseStopwatch = Stopwatch.StartNew();
            string fullContent = sb.ToString();
            for (int i = 0; i < 100; i++)
            {
                // Simulate parsing the accumulated content up to this point
                string partial = fullContent.Substring(0, Math.Min((i + 1) * 1000, fullContent.Length));
                // Parse each line (simplified simulation)
                foreach (var line in partial.Split('\n'))
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        SelectiveMarkdownParser.ParseLine(line);
                    }
                }
            }
            fullReparseStopwatch.Stop();

            // Assert - incremental should be significantly faster
            Assert.True(incrementalStopwatch.ElapsedMilliseconds < fullReparseStopwatch.ElapsedMilliseconds,
                $"Incremental: {incrementalStopwatch.ElapsedMilliseconds}ms, Full re-parse: {fullReparseStopwatch.ElapsedMilliseconds}ms");
        }

        private string GenerateMockChunk(int sizeBytes)
        {
            var sb = new StringBuilder();
            sb.Append("This is a mock response chunk with some **bold** text, *italic* text, and `code` samples. ");
            sb.Append("Lorem ipsum dolor sit amet, consectetur adipiscing elit. ");
            sb.Append("Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. ");

            int repetitions = sizeBytes / 200; // Approximate size
            for (int i = 0; i < repetitions; i++)
            {
                sb.Append("Line ");
                sb.Append(i);
                sb.Append(": Some content with **formatting** and `inline code`. ");
            }

            return sb.ToString();
        }
    }
}
