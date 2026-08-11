using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the definition/schema of a tool parameter for validation and documentation.
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>
        /// Name of the parameter.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// JSON Schema type of the parameter (e.g., "string", "number", "array", "object", "boolean").
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "string";

        /// <summary>
        /// Human-readable description of the parameter and its purpose.
        /// </summary>
        [JsonProperty("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Whether this parameter is required for tool invocation.
        /// </summary>
        [JsonProperty("required")]
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Default value for this parameter if not provided by the caller.
        /// </summary>
        [JsonProperty("default")]
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Enum values allowed for this parameter (if constrained).
        /// </summary>
        [JsonProperty("enum")]
        public IList<object>? AllowedValues { get; set; }

        /// <summary>
        /// Additional JSON Schema properties for complex type validation (e.g., items for array, properties for object).
        /// </summary>
        [JsonProperty("schema")]
        public IDictionary<string, object>? Schema { get; set; }
    }

    /// <summary>
    /// Represents the definition and metadata of a tool that can be invoked.
    /// Used by IToolService.GetAvailableTools() and tool registry.
    /// Supports built-in, MCP, and HTTP-based tools.
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// Unique name/identifier for this tool (e.g., "read_file", "edit_file", "search_codebase").
        /// Must be alphanumeric with underscores; used for routing tool calls.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what the tool does.
        /// Displayed in UI and provided to LLMs for understanding tool purpose.
        /// </summary>
        [JsonProperty("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Category/grouping for organizing tools (e.g., "Built-In", "MCP:server-name", "HTTP", "File Operations").
        /// Helps UI organize tool lists and assists in classification for routing.
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; } = "Built-In";

        /// <summary>
        /// List of parameter definitions for this tool.
        /// Describes expected arguments, their types, and requirements for invocation.
        /// </summary>
        [JsonProperty("parameters")]
        public IList<ParameterDefinition> Parameters { get; set; } = new List<ParameterDefinition>();

        /// <summary>
        /// Human-readable description of the tool's return value/output format.
        /// Helps LLMs understand what to expect after tool execution.
        /// </summary>
        [JsonProperty("returnsDescription")]
        public string? ReturnsDescription { get; set; }

        /// <summary>
        /// Indicates whether this tool is available for invocation.
        /// Disabled tools appear in registry but cannot be called.
        /// </summary>
        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Indicates whether this tool requires async execution (long-running operations).
        /// Useful for UI to show progress indicators or streaming results.
        /// </summary>
        [JsonProperty("isAsync")]
        public bool IsAsync { get; set; } = true;

        /// <summary>
        /// Type of tool: "builtin" (internal), "mcp" (Model Context Protocol), or "http" (custom endpoint).
        /// Determines routing behavior in tool execution layer.
        /// </summary>
        [JsonProperty("toolType")]
        public string ToolType { get; set; } = "builtin";

        /// <summary>
        /// Server identifier for MCP tools (e.g., "my-mcp-server").
        /// Only populated for ToolType="mcp"; null for other types.
        /// </summary>
        [JsonProperty("mcpServerId")]
        public string? McpServerId { get; set; }

        /// <summary>
        /// HTTP endpoint URL for HTTP-based tools.
        /// Only populated for ToolType="http"; null for other types.
        /// </summary>
        [JsonProperty("httpEndpoint")]
        public string? HttpEndpoint { get; set; }

        /// <summary>
        /// Timestamp when this tool definition was created or last updated.
        /// Useful for cache invalidation and change tracking.
        /// </summary>
        [JsonProperty("lastModified")]
        public DateTime? LastModified { get; set; }
    }
}
