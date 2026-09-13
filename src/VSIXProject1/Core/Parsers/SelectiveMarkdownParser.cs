using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ContinueVS.Core.Types;

namespace ContinueVS.Core.Parsers
{
    /// <summary>
    /// Regex-based parser for inline markdown (bold, italic, code).
    /// Stateless per-line parsing; no document context required.
    /// Prioritizes: code > bold > italic to avoid conflicts.
    /// </summary>
    public class SelectiveMarkdownParser
    {
        // Patterns for inline markdown (non-greedy)
        private static readonly Regex CodePattern = new Regex(@"`[^`]+`", RegexOptions.Compiled);
        private static readonly Regex BoldPattern = new Regex(@"\*\*[^\*]+\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicPattern = new Regex(@"(?<!\*)\*[^\*]+\*(?!\*)", RegexOptions.Compiled);

        /// <summary>
        /// Parse a single line and extract inline markdown runs.
        /// Processes in priority order: code, bold, italic.
        /// </summary>
        public static List<InlineRun> ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return new List<InlineRun> { new InlineRun { Text = line ?? string.Empty, Style = InlineStyle.Plain } };
            }

            // Track which positions are already consumed by higher-priority patterns
            var consumed = new bool[line.Length];

            var runs = new List<(int Start, int End, InlineStyle Style, string Text)>();

            // Priority 1: Code (backticks)
            foreach (Match match in CodePattern.Matches(line))
            {
                if (!IsConsumed(consumed, match.Index, match.Length))
                {
                    runs.Add((match.Index, match.Index + match.Length, InlineStyle.Code, match.Groups[0].Value.Trim('`')));
                    MarkConsumed(consumed, match.Index, match.Length);
                }
            }

            // Priority 2: Bold (double asterisks)
            foreach (Match match in BoldPattern.Matches(line))
            {
                if (!IsConsumed(consumed, match.Index, match.Length))
                {
                    runs.Add((match.Index, match.Index + match.Length, InlineStyle.Bold, match.Groups[0].Value.Trim('*')));
                    MarkConsumed(consumed, match.Index, match.Length);
                }
            }

            // Priority 3: Italic (single asterisks, not preceded/followed by asterisk)
            foreach (Match match in ItalicPattern.Matches(line))
            {
                if (!IsConsumed(consumed, match.Index, match.Length))
                {
                    runs.Add((match.Index, match.Index + match.Length, InlineStyle.Italic, match.Groups[0].Value.Trim('*')));
                    MarkConsumed(consumed, match.Index, match.Length);
                }
            }

            // Sort runs by start position
            runs.Sort((a, b) => a.Start.CompareTo(b.Start));

            // Build final result with plain text segments
            var result = new List<InlineRun>();
            int lastPos = 0;

            foreach (var (start, end, style, text) in runs)
            {
                if (start > lastPos)
                {
                    result.Add(new InlineRun { Text = line.Substring(lastPos, start - lastPos), Style = InlineStyle.Plain });
                }
                result.Add(new InlineRun { Text = text, Style = style });
                lastPos = end;
            }

            // Add remaining plain text
            if (lastPos < line.Length)
            {
                result.Add(new InlineRun { Text = line.Substring(lastPos), Style = InlineStyle.Plain });
            }

            return result.Count > 0 ? result : new List<InlineRun> { new InlineRun { Text = line, Style = InlineStyle.Plain } };
        }

        private static bool IsConsumed(bool[] consumed, int start, int length)
        {
            for (int i = start; i < start + length && i < consumed.Length; i++)
            {
                if (consumed[i])
                {
                    return true;
                }
            }
            return false;
        }

        private static void MarkConsumed(bool[] consumed, int start, int length)
        {
            for (int i = start; i < start + length && i < consumed.Length; i++)
            {
                consumed[i] = true;
            }
        }
    }
}
