using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an immutable test plan generated from a debug instruction.
    /// Contains an ordered list of internal phases (strategy attempts).
    /// </summary>
    public class TestPlan
    {
        /// <summary>
        /// Unique identifier for this test plan.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display title or summary of the test plan.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of internal phases that comprise this test plan.
        /// Phases are strategy attempts generated from the debug instruction.
        /// </summary>
        [JsonProperty("phases")]
        public List<InternalPhase> Phases { get; set; } = new List<InternalPhase>();

        /// <summary>
        /// Timestamp when this test plan was created.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
