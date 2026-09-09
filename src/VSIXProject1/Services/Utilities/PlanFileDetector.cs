using System;
using System.Collections.Generic;
using System.Text;

namespace ContinueVS.Services.Utilities
{
    /// <summary>
    /// Detects and buffers plan file output marked with the hardcoded marker filename.
    /// Accumulates content between triple-backtick fences when the marker is detected.
    /// gap70: Provides stateful streaming support for plan file capture.
    /// </summary>
    public class PlanFileDetector
    {
        /// <summary>
        /// The hardcoded marker filename that signals plan file output.
        /// Used as the opening fence identifier: start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\r\n```A485254C_7481_47BB_A8CF_45B8DEED2DD8
        /// </summary>
        private const string MarkerFileNameStart = "start_A485254C_7481_47BB_A8CF_45B8DEED2DD8";
        private const string MarkerFileNameStop = "stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8";

        private enum DetectorState
        {
            /// <summary>Waiting for opening fence with marker on same line.</summary>
            Idle,

            /// <summary>Marker detected on fence line; now accumulating content until closing fence.</summary>
            BufferingContent,

            /// <summary>Closing fence received; buffer complete.</summary>
            Complete
        }

        private DetectorState _state = DetectorState.Idle;
        private StringBuilder _buffer = new StringBuilder();
        private string? _lineBuffer = string.Empty;

        /// <summary>
        /// Tracks the opening and closing fence lines for plan content removal from response.
        /// Allows cleaning up plan markers and fenced content from the displayed response.
        /// </summary>
        private List<(int StartLine, int EndLine)> _detectionRanges = new List<(int, int)>();
        private int _lineCount = 0;
        private int? _detectionStartLine = null;

        /// <summary>
        /// Gets a value indicating whether the detector has detected and completed buffering a plan file.
        /// </summary>
        public bool IsComplete => _state == DetectorState.Complete;

        /// <summary>
        /// Gets the buffered content if <see cref="IsComplete"/> is true.
        /// </summary>
        public string GetBufferedContent()
        {
            if (_state != DetectorState.Complete)
                throw new InvalidOperationException("Detector is not complete. Call IsComplete first.");

            return _buffer.ToString();
        }

        /// <summary>
        /// Gets the plan file marker start pattern.
        /// Used to identify where plan file blocks begin.
        /// </summary>
        public string GetMarkerStart()
        {
            return MarkerFileNameStart;
        }

        /// <summary>
        /// Gets the plan file marker stop pattern.
        /// Used to identify where plan file blocks end.
        /// </summary>
        public string GetMarkerStop()
        {
            return MarkerFileNameStop;
        }

        /// <summary>
        /// Processes a chunk of text from the LLM stream.
        /// Detects marker filename on the opening fence (same line as ```) and accumulates content between fences.
        /// </summary>
        /// <param name="chunk">The text chunk to process (may be null or empty).</param>
        public void ProcessChunk(string? chunk)
        {
            if (chunk == null || chunk.Length == 0)
                return;

            if (_state == DetectorState.Complete)
                return; // Already complete, ignore further chunks

            // Append chunk character by character to line buffer
            foreach (char c in chunk)
            {
                _lineBuffer += c;

                // Check if we've completed a line (ended with newline)
                if (c == '\n')
                {
                    ProcessLine(_lineBuffer);
                    _lineBuffer = string.Empty;
                }
            }
        }

        /// <summary>
        /// Completes detection and processes any remaining partial line.
        /// Called at end of stream to finalize buffering state.
        /// </summary>
        public void CompleteDetection()
        {
            if (!string.IsNullOrEmpty(_lineBuffer))
            {
                ProcessLine(_lineBuffer);
                _lineBuffer = string.Empty;
            }

            // If we're still buffering, mark complete (fence never closed, but stream ended)
            if (_state == DetectorState.BufferingContent)
            {
                _state = DetectorState.Complete;
            }
        }

        /// <summary>
        /// Processes a single complete line of text.
        /// Supports the format (marker directly on same line as opening fence):
        /// start_A485254C_7481_47BB_A8CF_45B8DEED2DD8
        /// ## Sections
        /// Content...
        /// stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8
        /// </summary>
        private void ProcessLine(string? line)
        {
            if (line == null || line.Length == 0)
                return;

            string trimmedLine = line.TrimStart();

            switch (_state)
            {
                case DetectorState.Idle:
                    // Look for opening fence with marker on same line (e.g., .md)
                    if (trimmedLine.StartsWith(MarkerFileNameStart))
                    {
                        _state = DetectorState.BufferingContent;
                        _detectionStartLine = _lineCount;
                    }
                    break;

                case DetectorState.BufferingContent:
                    // Check for closing fence (line only contains 3+ backticks, nothing else important)
                    if (trimmedLine.StartsWith(MarkerFileNameStop))
                    {
                        // Closing fence marks end; don't include it in the buffer
                        _state = DetectorState.Complete;
                        if (_detectionStartLine.HasValue)
                        {
                            _detectionRanges.Add((_detectionStartLine.Value, _lineCount));
                            _detectionStartLine = null;
                        }
                    }
                    else
                    {
                        // Accumulate content, preserving line endings
                        _buffer.Append(line);
                    }
                    break;
            }

            _lineCount++;
        }

        /// <summary>
        /// Resets the detector to its initial state for reuse in a new session.
        /// </summary>
        public void Reset()
        {
            _state = DetectorState.Idle;
            _buffer.Clear();
            _lineBuffer = string.Empty;
            _detectionRanges.Clear();
            _lineCount = 0;
            _detectionStartLine = null;
        }
    }
}
