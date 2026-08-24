using System;
using System.Collections.Generic;
using System.Linq;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Immutable result record for distributed trace ID parsing operations.
    /// 
    /// Contains success flag, parsed trace context (if successful), and any error messages
    /// from parsing attempts.
    /// </summary>
    public sealed class TraceParseResult
    {
        /// <summary>Gets a value indicating whether the parse operation succeeded.</summary>
        public bool Success { get; }

        /// <summary>Gets the parsed trace context, if parsing succeeded; otherwise null.</summary>
        public TraceContext? TraceContext { get; }

        /// <summary>Gets the list of error messages from parsing attempts.</summary>
        public IReadOnlyList<string> ErrorMessages { get; }

        /// <summary>
        /// Creates a new trace parse result with the specified outcome.
        /// </summary>
        /// <param name="success">Whether parsing succeeded.</param>
        /// <param name="traceContext">The parsed trace context, if successful.</param>
        /// <param name="errorMessages">Any error messages from parsing.</param>
        public TraceParseResult(bool success, TraceContext? traceContext, IReadOnlyList<string> errorMessages)
        {
            Success = success;
            TraceContext = traceContext;
            ErrorMessages = errorMessages ?? new List<string>();
        }

        /// <summary>
        /// Creates a successful parse result with the given trace context.
        /// </summary>
        public static TraceParseResult CreateSuccess(TraceContext traceContext)
        {
            if (traceContext == null)
                throw new ArgumentNullException(nameof(traceContext));

            return new TraceParseResult(
                success: true,
                traceContext: traceContext,
                errorMessages: new List<string>()
            );
        }

        /// <summary>
        /// Creates a failed parse result with the given error messages.
        /// </summary>
        public static TraceParseResult CreateFailure(params string[] errors)
        {
            var errorList = new List<string>(errors ?? Array.Empty<string>());
            return new TraceParseResult(
                success: false,
                traceContext: null,
                errorMessages: errorList
            );
        }

        /// <summary>
        /// Returns a string representation of this parse result for debugging.
        /// </summary>
        public override string ToString() =>
            Success
                ? $"TraceParseResult(Success=true, {TraceContext})"
                : $"TraceParseResult(Success=false, Errors=[{string.Join("; ", ErrorMessages)}])";
    }
}
