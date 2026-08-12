using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services.Tests
{
    public class IndexingServiceTests
    {
        [Fact]
        public async Task StartIndexingAsync_SetsIndexing()
        {
            var service = new IndexingService(null);
            await service.StartIndexingAsync();
            var status = service.GetCurrentStatus();
            Assert.Equal(ContinueVS.Core.Types.IndexingStatus.Indexing, status.Status);
        }

        [Fact]
        public async Task PauseIndexingAsync_PausesIndexing()
        {
            var service = new IndexingService(null);
            await service.StartIndexingAsync();
            await service.PauseIndexingAsync();
            var status = service.GetCurrentStatus();
            Assert.Equal(ContinueVS.Core.Types.IndexingStatus.Paused, status.Status);
        }

        [Fact]
        public async Task CancelIndexingAsync_CancelsIndexing()
        {
            var service = new IndexingService(null);
            await service.StartIndexingAsync();
            await service.CancelIndexingAsync();
            var status = service.GetCurrentStatus();
            Assert.Equal(ContinueVS.Core.Types.IndexingStatus.Cancelled, status.Status);
        }

        [Fact]
        public async Task IsIndexedAsync_ReturnsFalse()
        {
            var service = new IndexingService(null);
            var result = await service.IsIndexedAsync("/path/to/file.cs");
            Assert.False(result);
        }

        [Fact]
        public async Task SearchIndexAsync_ReturnsEmpty()
        {
            var service = new IndexingService(null);
            var results = await service.SearchIndexAsync("test", 10);
            Assert.Empty(results);
        }

        [Fact]
        public void GetCurrentStatus_ReturnsStatus()
        {
            var service = new IndexingService(null);
            var status = service.GetCurrentStatus();
            Assert.NotNull(status);
            Assert.Equal(ContinueVS.Core.Types.IndexingStatus.Idle, status.Status);
        }

        [Fact]
        public async Task RebuildIndexAsync_Completes()
        {
            var service = new IndexingService(null);
            await service.RebuildIndexAsync();
        }
    }
}
