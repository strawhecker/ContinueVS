using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Dumps context information for debugging LLM requests.
    /// Provides visibility into what's being sent to the LLM before tokenization.
    /// </summary>
    public interface IContextDumpService
    {
        /// <summary>
        /// Dumps the complete context (system message, context items, user message) 
        /// that will be sent to the LLM to Debug Output.
        /// Shows raw text before tokenization, with estimated token counts.
        /// </summary>
        /// <param name="messages">The list of chat messages being sent to the LLM.</param>
        /// <param name="selectedContext">Optional context items included in the request.</param>
        Task DumpContextBeforeSendAsync(List<ChatMessage> messages, List<ContextItem>? selectedContext = null);

        /// <summary>
        /// Dumps the response received from the LLM to Debug Output.
        /// Includes content, token count, and timing information.
        /// </summary>
        /// <param name="responseContent">The response text from the LLM.</param>
        Task DumpResponseAfterReceiveAsync(string responseContent);
    }
}
