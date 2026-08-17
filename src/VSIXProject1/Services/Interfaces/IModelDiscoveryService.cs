#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for discovering models and validating connections for LLM providers.
    /// </summary>
    public interface IModelDiscoveryService
    {
        /// <summary>
        /// Discovers available models for a provider via API or returns default list on failure.
        /// </summary>
        Task<List<string>> DiscoverModelsAsync(ModelProvider provider, string? apiKey = null);

        /// <summary>
        /// Validates connection to a model provider (tests API key and model availability).
        /// </summary>
        Task<bool> ValidateConnectionAsync(ModelInfo model);

        /// <summary>
        /// Gets metadata for a provider (download URL, supported models, etc).
        /// </summary>
        ProviderMetadata? GetProviderMetadata(ModelProvider provider);
    }
}
