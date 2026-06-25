using Newtonsoft.Json;
using System;

namespace ContinueVS.Handlers.Llm
{
    internal static class ContinueConfigReader
    {
        private static readonly string ConfigPath = global::System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".continue",
            "config.json");

        /// <summary>
        /// Reads ~/.continue/config.json and returns the model matching <paramref name="title"/>,
        /// or the first model if no match is found. Returns null if the config cannot be loaded.
        /// </summary>
        internal static LlmModelConfig? FindModel(string title)
        {
            try
            {
                if (!global::System.IO.File.Exists(ConfigPath))
                    return null;

                var json = global::System.IO.File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<ContinueConfig>(json);

                if (config == null || config.Models.Count == 0)
                    return null;

                if (!string.IsNullOrEmpty(title))
                {
                    var match = config.Models.Find(m =>
                        string.Equals(m.Title, title, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        return match;
                }

                return config.Models[0];
            }
            catch
            {
                return null;
            }
        }
    }
}
