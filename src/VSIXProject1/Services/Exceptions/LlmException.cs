using System;

namespace ContinueVS.Services.Exceptions
{
    /// <summary>
    /// Exception thrown when LLM operations fail (streaming, token counting, model detection).
    /// </summary>
    public class LlmException : Exception
    {
        /// <summary>
        /// Gets the model ID associated with the failed operation, if available.
        /// </summary>
        public string? ModelId { get; }

        /// <summary>
        /// Gets an optional error code for diagnostic purposes.
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance of the LlmException class.
        /// </summary>
        public LlmException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LlmException class with a model ID.
        /// </summary>
        public LlmException(string message, string? modelId)
            : base(message)
        {
            ModelId = modelId;
        }

        /// <summary>
        /// Initializes a new instance of the LlmException class with model ID and error code.
        /// </summary>
        public LlmException(string message, string? modelId, string? errorCode)
            : base(message)
        {
            ModelId = modelId;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the LlmException class with an inner exception.
        /// </summary>
        public LlmException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the LlmException class with model ID and inner exception.
        /// </summary>
        public LlmException(string message, string? modelId, Exception innerException)
            : base(message, innerException)
        {
            ModelId = modelId;
        }

        /// <summary>
        /// Initializes a new instance of the LlmException class with model ID, error code, and inner exception.
        /// </summary>
        public LlmException(string message, string? modelId, string? errorCode, Exception innerException)
            : base(message, innerException)
        {
            ModelId = modelId;
            ErrorCode = errorCode;
        }
    }
}
