using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a debug instruction provided by the user in Debug mode.
    /// The instruction is free-text and may be vague (e.g., "Debug why SendMessage fails with null").
    /// </summary>
    public class DebugInstruction
    {
        /// <summary>
        /// Unique identifier for this instruction.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The user's debug request text.
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Optional context information (e.g., file path, line number, error message, callstack).
        /// </summary>
        [JsonProperty("context")]
        public string? Context { get; set; }

        /// <summary>
        /// Timestamp when this instruction was created.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
