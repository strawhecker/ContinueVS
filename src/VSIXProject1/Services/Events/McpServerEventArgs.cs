using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Enumeration of MCP server status values.
    /// </summary>
    public enum McpServerStatusType
    {
        /// <summary>
        /// Server is not running.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Server is attempting to connect.
        /// </summary>
        Connecting,

        /// <summary>
        /// Server is connected and ready.
        /// </summary>
        Connected,

        /// <summary>
        /// Server encountered an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Event arguments for MCP server events.
    /// </summary>
    public class McpServerEventArgs : EventArgs
    {
        /// <summary>
        /// ID of the MCP server.
        /// </summary>
        public string? ServerId { get; set; }

        /// <summary>
        /// Status of the server.
        /// </summary>
        public McpServerStatusType Status { get; set; }

        /// <summary>
        /// Optional status message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Timestamp when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
