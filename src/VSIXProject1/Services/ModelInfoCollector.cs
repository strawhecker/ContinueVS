#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Model info collector stub. Legacy configuration system has been removed.
    /// This stub provides no-op implementations for backward compatibility.
    /// </summary>
    internal sealed class ModelInfoCollector
    {
        public ModelInfoCollector()
        {
        }

        public async Task<List<ModelInfoDto>> GetAvailableModelsAsync(List<object>? modelsOverride = null)
        {
            //No-op model collection
            await Task.CompletedTask;
            return new List<ModelInfoDto>();
        }
    }

    /// <summary>
    /// Model info DTO (stub).
    /// </summary>
    public sealed class ModelInfoDto
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
    }
}
