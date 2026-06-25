using Newtonsoft.Json;
using System.Collections.Generic;

namespace ContinueVS.Handlers.Llm
{
    internal sealed class LlmModelConfig
    {
        [JsonProperty("title")]    public string  Title    { get; set; } = "";
        [JsonProperty("provider")] public string  Provider { get; set; } = "";
        [JsonProperty("model")]    public string  Model    { get; set; } = "";
        [JsonProperty("apiKey")]   public string? ApiKey   { get; set; }
        [JsonProperty("apiBase")]  public string? ApiBase  { get; set; }
    }

    internal sealed class ContinueConfig
    {
        [JsonProperty("models")] public List<LlmModelConfig> Models { get; set; } = new List<LlmModelConfig>();
    }
}
