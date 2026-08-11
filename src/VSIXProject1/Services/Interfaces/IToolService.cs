using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for tool execution and management.
    /// Handles routing tool calls to built-in tools, MCP servers, and HTTP endpoints.
    /// </summary>
    public interface IToolService
    {
        /// <summary>
        /// Gets all available tools.
        /// </summary>
        /// <returns>An enumerable of available ToolDefinition instances.</returns>
        IEnumerable<ToolDefinition> GetAvailableTools();

        /// <summary>
        /// Gets a specific tool by name.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <returns>The ToolDefinition instance, or null if not found.</returns>
        ToolDefinition? GetTool(string toolName);

        /// <summary>
        /// Invokes a tool with the given arguments.
        /// </summary>
        /// <param name="toolName">The name of the tool to invoke.</param>
        /// <param name="args">Arguments to pass to the tool.</param>
        /// <param name="ct">Cancellation token to stop the tool execution.</param>
        /// <returns>The result of the tool execution.</returns>
        Task<ToolResult> InvokeAsync(
            string toolName,
            IDictionary<string, object> args,
            CancellationToken ct = default);

        /// <summary>
        /// Reads the contents of a file.
        /// </summary>
        /// <param name="filepath">The path to the file to read.</param>
        /// <returns>The file contents as a string.</returns>
        Task<string> ReadFileAsync(string filepath);

        /// <summary>
        /// Writes contents to a file.
        /// </summary>
        /// <param name="filepath">The path to the file to write.</param>
        /// <param name="contents">The contents to write to the file.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WriteFileAsync(string filepath, string contents);

        /// <summary>
        /// Searches the codebase for matches to a query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>An enumerable of code search results.</returns>
        Task<IEnumerable<CodeSearchResult>> SearchCodebaseAsync(string query, int maxResults);

        /// <summary>
        /// Runs a subprocess command.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="cwd">The working directory for the subprocess.</param>
        /// <returns>A tuple of (stdout, stderr).</returns>
        Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd);

        /// <summary>
        /// Loads available MCP tools from a server.
        /// </summary>
        /// <param name="serverId">The ID of the MCP server.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LoadMcpToolsAsync(string serverId);

        /// <summary>
        /// Invokes a tool provided by an MCP server.
        /// </summary>
        /// <param name="serverId">The ID of the MCP server.</param>
        /// <param name="toolName">The name of the tool to invoke.</param>
        /// <param name="args">Arguments to pass to the tool.</param>
        /// <returns>The result of the tool execution.</returns>
        Task<ToolResult> InvokeMcpToolAsync(
            string serverId,
            string toolName,
            IDictionary<string, object> args);

        /// <summary>
        /// Event raised when a tool execution error occurs.
        /// </summary>
        event EventHandler<ToolErrorEventArgs>? Error;
    }

    /// <summary>
    /// Represents the result of a code search operation.
    /// </summary>
    public class CodeSearchResult
    {
        /// <summary>
        /// Path to the file containing the match.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Line number of the match (1-based).
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The line of code containing the match.
        /// </summary>
        public string? LineContent { get; set; }

        /// <summary>
        /// Relevance score (0.0 to 1.0).
        /// </summary>
        public double Relevance { get; set; }
    }
}
