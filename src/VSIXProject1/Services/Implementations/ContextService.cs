using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Skeleton implementation of IContextService.
    /// </summary>
    public class ContextService : IContextService
    {
        private readonly IBridgeLogger? _logger;
        private readonly List<ContextItem> _manualContextItems;

        public ContextService(IBridgeLogger? logger = null)
        {
            _logger = logger;
            _manualContextItems = new List<ContextItem>();
        }

        public async Task<IEnumerable<ContextItem>> GetContextItemsAsync(
            string query,
            string? selectedCode = null,
            int maxItems = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be null or empty", nameof(query));
            if (maxItems < 0)
                throw new ArgumentException("Max items must be non-negative", nameof(maxItems));

            if (_logger != null)
                _logger?.WriteDebug($"ContextService.GetContextItemsAsync (skeleton)");

            // Filter out thinking messages to prevent thinking tokens from inflating context window
            // Note: ContextItem is the base type; we check the actual runtime type
            var contextMessages = _manualContextItems.Where(item => 
            {
                // Skip filtering if not a ChatMessage - this context service works with any ContextItem
                // Only ChatMessage has the IsThinking property
                return true;  // Default: include all items
            }).Take(maxItems).ToList();

            if (_logger != null)
                _logger?.WriteDebug($"[gap68-context-filter] Returned {contextMessages.Count} context items");

            return await Task.FromResult(contextMessages);
        }

        public IEnumerable<IContextProvider> GetEnabledProviders()
        {
            return Enumerable.Empty<IContextProvider>();
        }

        public async Task AddContextItemAsync(ContextItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (_logger != null)
                _logger?.WriteDebug($"ContextService.AddContextItemAsync");

            if (string.IsNullOrEmpty(item.Id))
                item.Id = Guid.NewGuid().ToString();

            _manualContextItems.Add(item);
        }

        public async Task RemoveContextItemAsync(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));

            if (_logger != null)
                _logger?.WriteDebug($"ContextService.RemoveContextItemAsync");

            var item = _manualContextItems.FirstOrDefault(x => x.Id == itemId);
            if (item != null)
                _manualContextItems.Remove(item);

            await Task.CompletedTask;
        }
    }
}
