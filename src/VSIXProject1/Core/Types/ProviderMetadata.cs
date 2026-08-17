#nullable enable

using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Metadata about a supported LLM provider.
    /// </summary>
    public class ProviderMetadata
    {
        public string Name { get; set; } = string.Empty;
        public ModelProvider Provider { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public bool SupportsAutodetect { get; set; }
        public List<string> DefaultModels { get; set; } = new List<string>();
    }
}
