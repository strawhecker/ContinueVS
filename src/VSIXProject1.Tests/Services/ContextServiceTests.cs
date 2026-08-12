using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Tests
{
    public class ContextServiceTests
    {
        [Fact]
        public async Task GetContextItemsAsync_ReturnsItems()
        {
            var service = new ContextService(null);
            var results = await service.GetContextItemsAsync("test query");
            Assert.NotNull(results);
        }

        [Fact]
        public async Task AddContextItemAsync_StoresItem()
        {
            var service = new ContextService(null);
            var item = new ContinueVS.Core.Types.ContextItem { Content = "test" };
            await service.AddContextItemAsync(item);
            var results = await service.GetContextItemsAsync("test", null, 10);
            Assert.Single(results);
        }

        [Fact]
        public async Task RemoveContextItemAsync_DeletesItem()
        {
            var service = new ContextService(null);
            var item = new ContinueVS.Core.Types.ContextItem { Content = "test" };
            await service.AddContextItemAsync(item);
            var itemId = item.Id;
            await service.RemoveContextItemAsync(itemId);
            var results = await service.GetContextItemsAsync("test", null, 10);
            Assert.Empty(results);
        }

        [Fact]
        public void GetEnabledProviders_ReturnsEmpty()
        {
            var service = new ContextService(null);
            var providers = service.GetEnabledProviders();
            Assert.Empty(providers);
        }
    }
}
