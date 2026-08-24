#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContinueVS.Services.Interfaces.Parsers
{
    /// <summary>
    /// Represents a parser strategy with confidence score.
    /// </summary>
    public class ParserStrategy
    {
        /// <summary>
        /// The parser implementation.
        /// </summary>
        public IStackTraceParser? Parser { get; set; }

        /// <summary>
        /// Confidence score (0.0 to 1.0), higher = more likely to be correct format.
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Reason for this confidence level.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Service for detecting stack trace format heuristically.
    /// Examines input content and returns parsers in priority order.
    /// </summary>
    public interface IFormatDetector
    {
        /// <summary>
        /// Detects the format of the input stack trace and returns applicable parsers in priority order.
        /// </summary>
        /// <param name="input">The stack trace input to analyze.</param>
        /// <returns>List of ParserStrategy objects, ordered by confidence (highest first).</returns>
        Task<List<ParserStrategy>> DetectFormatsAsync(string? input);
    }
}
