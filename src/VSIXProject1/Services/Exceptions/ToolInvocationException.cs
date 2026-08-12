using System;

namespace ContinueVS.Services.Exceptions
{
    /// <summary>
    /// Exception thrown when a tool invocation fails (built-in, MCP, or HTTP tools).
    /// </summary>
    public class ToolInvocationException : Exception
    {
        /// <summary>
        /// Gets the name of the tool that failed to invoke.
        /// </summary>
        public string? ToolName { get; }

        /// <summary>
        /// Gets the result code from the failed tool invocation, if available.
        /// </summary>
        public string? ResultCode { get; }

        /// <summary>
        /// Initializes a new instance of the ToolInvocationException class.
        /// </summary>
        public ToolInvocationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ToolInvocationException class with a tool name.
        /// </summary>
        public ToolInvocationException(string message, string? toolName)
            : base(message)
        {
            ToolName = toolName;
        }

        /// <summary>
        /// Initializes a new instance of the ToolInvocationException class with tool name and result code.
        /// </summary>
        public ToolInvocationException(string message, string? toolName, string? resultCode)
            : base(message)
        {
            ToolName = toolName;
            ResultCode = resultCode;
        }

        /// <summary>
        /// Initializes a new instance of the ToolInvocationException class with an inner exception.
        /// </summary>
        public ToolInvocationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ToolInvocationException class with tool name and inner exception.
        /// </summary>
        public ToolInvocationException(string message, string? toolName, Exception innerException)
            : base(message, innerException)
        {
            ToolName = toolName;
        }

        /// <summary>
        /// Initializes a new instance of the ToolInvocationException class with tool name, result code, and inner exception.
        /// </summary>
        public ToolInvocationException(string message, string? toolName, string? resultCode, Exception innerException)
            : base(message, innerException)
        {
            ToolName = toolName;
            ResultCode = resultCode;
        }
    }
}
