using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for markdown parsing and rendering.
    /// Handles markdown-to-AST conversion, code block language detection, and async parsing.
    /// </summary>
    public interface IMarkdownService
    {
        /// <summary>
        /// Parses markdown content asynchronously into a structured MarkdownNode tree.
        /// </summary>
        /// <param name="content">Raw markdown string (may contain code blocks, bold, italic, links, etc.)</param>
        /// <returns>Root MarkdownNode representing the parsed markdown structure</returns>
        /// <exception cref="MarkdownParsingException">Thrown if parsing fails catastrophically</exception>
        Task<MarkdownNode> ParseMarkdownAsync(string content);

        /// <summary>
        /// Extracts the language identifier from a markdown code fence.
        /// </summary>
        /// <param name="fence">Code fence line (e.g., "```csharp" or "```javascript")</param>
        /// <returns>Language identifier (e.g., "csharp", "javascript"), or empty string if not detected</returns>
        string ExtractLanguageFromFence(string fence);
    }
}
