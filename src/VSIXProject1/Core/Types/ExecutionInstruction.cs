using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an instruction provided by the user to drive phase execution.
    /// The instruction is free-text and may be vague (e.g., "Fix why SendMessage fails with null").
    /// Shared by Agent and Debug modes via IInstructionExecutorService.
    /// </summary>
    public class ExecutionInstruction
    {
        /// <summary>
        /// Unique identifier for this instruction.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The user's request text.
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
