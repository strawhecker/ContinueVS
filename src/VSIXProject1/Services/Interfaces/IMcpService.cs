using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for MCP server integration.
    /// Handles lifecycle and communication with Model Context Protocol servers.
    /// </summary>
    public interface IMcpService
    {
        /// <summary>
        /// Initializes and connects to an MCP server.
        /// </summary>
        /// <param name="config">The MCP server configuration.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeServerAsync(McpServerConfig config);

        /// <summary>
        /// Shuts down a connected MCP server.
        /// </summary>
        /// <param name="serverId">The ID of the server to shut down.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShutdownServerAsync(string serverId);

        /// <summary>
        /// Restarts an MCP server.
        /// </summary>
        /// <param name="serverId">The ID of the server to restart.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RestartServerAsync(string serverId);

        /// <summary>
        /// Gets the status of an MCP server.
        /// </summary>
        /// <param name="serverId">The ID of the server.</param>
        /// <returns>The current status of the server.</returns>
        McpServerStatus? GetServerStatus(string serverId);

        /// <summary>
        /// Gets the status of all connected MCP servers.
        /// </summary>
        /// <returns>An enumerable of server status objects.</returns>
        IEnumerable<McpServerStatus> GetAllServers();

        /// <summary>
        /// Gets tools available from an MCP server.
        /// </summary>
        /// <param name="serverId">The ID of the server.</param>
        /// <returns>An enumerable of tool definitions.</returns>
        IEnumerable<ToolDefinition> GetServerTools(string serverId);

        /// <summary>
        /// Gets a resource from an MCP server.
        /// </summary>
        /// <param name="serverId">The ID of the server.</param>
        /// <param name="resourceUri">The URI of the resource to retrieve.</param>
        /// <returns>The resource content as a string.</returns>
        Task<string> GetResourceAsync(string serverId, string resourceUri);

        /// <summary>
        /// Gets a prompt template from an MCP server.
        /// </summary>
        /// <param name="serverId">The ID of the server.</param>
        /// <param name="promptName">The name of the prompt template.</param>
        /// <param name="args">Optional arguments to fill in the template.</param>
        /// <returns>The rendered prompt template.</returns>
        Task<PromptTemplate> GetPromptAsync(
            string serverId,
            string promptName,
            IDictionary<string, object>? args = null);

        /// <summary>
        /// Event raised when an MCP server connects.
        /// </summary>
        event EventHandler<McpServerEventArgs>? ServerConnected;

        /// <summary>
        /// Event raised when an MCP server disconnects.
        /// </summary>
        event EventHandler<McpServerEventArgs>? ServerDisconnected;
    }

    /// <summary>
    /// Configuration for an MCP server.
    /// </summary>
    public class McpServerConfig
    {
        /// <summary>
        /// Unique identifier for the server.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Display name for the server.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Type of server (stdio, SSE, etc.).
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Command to start the server (for stdio type).
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Arguments for the server command.
        /// </summary>
        public List<string> Args { get; set; } = new List<string>();

        /// <summary>
        /// Environment variables for the server process.
        /// </summary>
        public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Status of an MCP server.
    /// </summary>
    public class McpServerStatus
    {
        /// <summary>
        /// Unique identifier for the server.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Display name of the server.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Current connection status.
        /// </summary>
        public McpServerStatusType Status { get; set; }

        /// <summary>
        /// Optional status message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Number of tools provided by this server.
        /// </summary>
        public int ToolCount { get; set; }

        /// <summary>
        /// Timestamp of the last status update.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Enumeration of MCP server status values.
    /// </summary>
    public enum McpServerStatusType
    {
        /// <summary>
        /// Server is not connected.
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Server is connecting.
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// Server is connected and operational.
        /// </summary>
        Connected = 2,

        /// <summary>
        /// Server encountered an error.
        /// </summary>
        Error = 3
    }

    /// <summary>
    /// Represents a prompt template from an MCP server.
    /// </summary>
    public class PromptTemplate
    {
        /// <summary>
        /// Name of the prompt template.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Description of what the prompt does.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The rendered prompt content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Arguments the prompt accepts.
        /// </summary>
        public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();
    }
}
