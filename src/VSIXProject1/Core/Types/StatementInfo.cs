using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// The current source statement at the selected frame, plus optional surrounding source lines
    /// (gap92_2 inspection surface).
    /// </summary>
    public sealed class StatementInfo
    {
        /// <summary>
        /// Source file path of the current statement.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// 1-based line number of the current statement.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Text of the current statement (best effort, may be empty).
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// True when this statement is the paused execution point in the current frame.
        /// </summary>
        public bool IsCurrent { get; set; }

        /// <summary>
        /// Surrounding source lines (index 0 = the current line), when requested. Each entry is the
        /// raw source text for its corresponding line.
        /// </summary>
        public List<SurroundingLine> SurroundingLines { get; set; } = new List<SurroundingLine>();
    }

    /// <summary>
    /// One entry in a <see cref="StatementInfo.SurroundingLines"/> span.
    /// </summary>
    public sealed class SurroundingLine
    {
        /// <summary>
        /// 1-based source line number.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Raw source text for this line.
        /// </summary>
        public string? Text { get; set; }
    }
}
