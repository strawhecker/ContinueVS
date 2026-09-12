using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ContinueVS.Core.Parsers
{
    /// <summary>
    /// Minimal markdown parser for inline formatting only.
    /// Parses a single line of text and returns WPF Runs with styling applied.
    /// Supports: **bold**, *italic*, `code`
    /// Does NOT parse block-level markdown (headers, lists, code fences, etc.)
    /// </summary>
    public static class SelectiveMarkdownParser
    {
        /// <summary>
        /// Regex pattern for bold text: **text**
        /// Matches non-greedy content between double asterisks.
        /// </summary>
        private static readonly Regex BoldPattern = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

        /// <summary>
        /// Regex pattern for italic text: *text* (but not **)
        /// Matches single asterisks with non-greedy content.
        /// Excludes cases where asterisks are part of **.
        /// </summary>
        private static readonly Regex ItalicPattern = new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);

        /// <summary>
        /// Regex pattern for code text: `text`
        /// Matches backtick-enclosed content (not triple backticks).
        /// </summary>
        private static readonly Regex CodePattern = new Regex(@"`([^`]+)`", RegexOptions.Compiled);

        /// <summary>
        /// Parse a single line of text and return WPF Runs with inline markdown styling.
        /// Processes bold (**text**), italic (*text*), and code (`text`) in a single pass.
        /// </summary>
        /// <param name="text">Input text line to parse</param>
        /// <returns>List of Run objects with styling applied; never empty (at least one plain run)</returns>
        public static List<Run> ParseLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<Run> { new Run { Text = string.Empty } };
            }

            var runs = new List<Run>();
            var segments = new List<(int start, int end, RunStyle style)>();

            // Find all bold matches
            foreach (Match match in BoldPattern.Matches(text))
            {
                segments.Add((match.Index, match.Index + match.Length, RunStyle.Bold));
            }

            // Find all code matches first (highest priority to avoid regex conflicts)
            foreach (Match match in CodePattern.Matches(text))
            {
                bool overlapsWithBold = false;
                foreach (var (bStart, bEnd, _) in segments)
                {
                    if (!(match.Index + match.Length <= bStart || match.Index >= bEnd))
                    {
                        overlapsWithBold = true;
                        break;
                    }
                }
                if (!overlapsWithBold)
                {
                    segments.Add((match.Index, match.Index + match.Length, RunStyle.Code));
                }
            }

            // Find all italic matches (excluding matches within bold or code)
            foreach (Match match in ItalicPattern.Matches(text))
            {
                bool overlapsWithOther = false;
                foreach (var (oStart, oEnd, _) in segments)
                {
                    if (!(match.Index + match.Length <= oStart || match.Index >= oEnd))
                    {
                        overlapsWithOther = true;
                        break;
                    }
                }
                if (!overlapsWithOther)
                {
                    segments.Add((match.Index, match.Index + match.Length, RunStyle.Italic));
                }
            }

            // If no formatting found, return single plain run
            if (segments.Count == 0)
            {
                return new List<Run> { new Run { Text = text } };
            }

            // Sort segments by start position
            segments.Sort((a, b) => a.start.CompareTo(b.start));

            // Build runs by merging plain text and styled segments
            int currentPos = 0;
            foreach (var (start, end, style) in segments)
            {
                // Add plain text before this segment
                if (currentPos < start)
                {
                    runs.Add(new Run { Text = text.Substring(currentPos, start - currentPos) });
                }

                // Extract styled content and remove delimiters
                string styledContent = ExtractStyledContent(text, start, end, style);
                Run styledRun = CreateStyledRun(styledContent, style);
                runs.Add(styledRun);

                currentPos = end;
            }

            // Add remaining plain text
            if (currentPos < text.Length)
            {
                runs.Add(new Run { Text = text.Substring(currentPos) });
            }

            return runs.Count > 0 ? runs : new List<Run> { new Run { Text = text } };
        }

        /// <summary>
        /// Extract the content within formatting delimiters.
        /// </summary>
        private static string ExtractStyledContent(string text, int start, int end, RunStyle style)
        {
            return style switch
            {
                RunStyle.Bold => text.Substring(start + 2, end - start - 4),   // Remove **...**
                RunStyle.Italic => text.Substring(start + 1, end - start - 2), // Remove *...*
                RunStyle.Code => text.Substring(start + 1, end - start - 2),   // Remove `...`
                _ => text.Substring(start, end - start)
            };
        }

        /// <summary>
        /// Create a Run with appropriate styling based on the RunStyle enum.
        /// </summary>
        private static Run CreateStyledRun(string content, RunStyle style)
        {
            var run = new Run { Text = content };

            return style switch
            {
                RunStyle.Bold => new Run
                {
                    Text = content,
                    FontWeight = FontWeights.Bold
                },
                RunStyle.Italic => new Run
                {
                    Text = content,
                    FontStyle = FontStyles.Italic
                },
                RunStyle.Code => new Run
                {
                    Text = content,
                    FontFamily = new FontFamily("Courier New"),
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 60))
                },
                _ => run
            };
        }

        /// <summary>
        /// Enumeration of run styles produced by selective markdown parser.
        /// </summary>
        private enum RunStyle
        {
            Plain,
            Bold,
            Italic,
            Code
        }
    }
}
