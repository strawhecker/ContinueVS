using System;
using System.Diagnostics;
using Xunit;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.Services
{
    public class StreamingTextBlockCollectionPerformanceTests
    {
        [Fact]
        public void AppendChunk_100KbResponse_CompletesUnder200ms()
        {
            var collection = new StreamingTextBlockCollection();
            var stopwatch = Stopwatch.StartNew();

            // Generate 100KB of text
            const int targetSize = 100 * 1024; // 100KB
            var chunkSize = 1024; // 1KB chunks
            var content = new string('x', chunkSize);

            for (int i = 0; i < targetSize / chunkSize; i++)
            {
                collection.AppendChunk(content);
                if (i % 10 == 0)
                {
                    collection.AppendChunk("\n"); // Add newline every 10 chunks
                }
            }

            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < 200, 
                $"Appending 100KB took {stopwatch.ElapsedMilliseconds}ms (should be < 200ms)");
        }

        [Fact]
        public void BlockCount_100KbResponse_ReasonableCount()
        {
            var collection = new StreamingTextBlockCollection();

            // Generate 100KB of text
            const int targetSize = 100 * 1024;
            var chunkSize = 1024;
            var content = new string('x', chunkSize);

            for (int i = 0; i < targetSize / chunkSize; i++)
            {
                collection.AppendChunk(content);
                if (i % 10 == 0)
                {
                    collection.AppendChunk("\n");
                }
            }

            // With 1KB chunks and newlines every 10 chunks, expect ~100 blocks
            Assert.True(collection.BlockCount < 200, 
                $"Block count is {collection.BlockCount} (should be < 200 for reasonable memory)");
        }

        [Fact]
        public void GetCombinedText_100KbResponse_Efficient()
        {
            var collection = new StreamingTextBlockCollection();
            const int targetSize = 100 * 1024;
            var chunkSize = 1024;
            var content = new string('x', chunkSize);

            for (int i = 0; i < targetSize / chunkSize; i++)
            {
                collection.AppendChunk(content);
                if (i % 10 == 0)
                {
                    collection.AppendChunk("\n");
                }
            }

            var stopwatch = Stopwatch.StartNew();
            var combined = collection.GetCombinedText();
            stopwatch.Stop();

            Assert.NotEmpty(combined);
            Assert.True(stopwatch.ElapsedMilliseconds < 50, 
                $"GetCombinedText took {stopwatch.ElapsedMilliseconds}ms (should be < 50ms)");
        }
    }
}
