using System;

namespace ContinueVS.Services.Exceptions
{
    /// <summary>
    /// Exception thrown when indexing operations fail (start, pause, resume, cancel).
    /// </summary>
    public class IndexingException : Exception
    {
        /// <summary>
        /// Gets the name or identifier of the index that failed, if available.
        /// </summary>
        public string? IndexName { get; }

        /// <summary>
        /// Gets the status code indicating the failure reason, if available.
        /// </summary>
        public string? StatusCode { get; }

        /// <summary>
        /// Initializes a new instance of the IndexingException class.
        /// </summary>
        public IndexingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the IndexingException class with an index name.
        /// </summary>
        public IndexingException(string message, string? indexName)
            : base(message)
        {
            IndexName = indexName;
        }

        /// <summary>
        /// Initializes a new instance of the IndexingException class with index name and status code.
        /// </summary>
        public IndexingException(string message, string? indexName, string? statusCode)
            : base(message)
        {
            IndexName = indexName;
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initializes a new instance of the IndexingException class with an inner exception.
        /// </summary>
        public IndexingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the IndexingException class with index name and inner exception.
        /// </summary>
        public IndexingException(string message, string? indexName, Exception innerException)
            : base(message, innerException)
        {
            IndexName = indexName;
        }

        /// <summary>
        /// Initializes a new instance of the IndexingException class with index name, status code, and inner exception.
        /// </summary>
        public IndexingException(string message, string? indexName, string? statusCode, Exception innerException)
            : base(message, innerException)
        {
            IndexName = indexName;
            StatusCode = statusCode;
        }
    }
}
