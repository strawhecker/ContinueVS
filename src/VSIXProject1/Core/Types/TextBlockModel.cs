using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a single line of streaming text with auto-parsed inline runs.
    /// Supports inline markdown: **bold**, *italic*, `code`.
    /// </summary>
    public class TextBlockModel : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private List<InlineRun> _inlineRuns = new();
        private DateTime _timestamp = DateTime.Now;

        /// <summary>
        /// The raw text content of this line.
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                    // Re-parse inline runs when text changes
                    ParseInlineRuns();
                }
            }
        }

        /// <summary>
        /// Collection of inline runs parsed from Text (bold, italic, code).
        /// </summary>
        public List<InlineRun> InlineRuns
        {
            get => _inlineRuns;
            private set
            {
                if (_inlineRuns != value)
                {
                    _inlineRuns = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Timestamp when this line was added to the collection.
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    OnPropertyChanged();
                }
            }
        }

        public TextBlockModel()
        {
        }

        public TextBlockModel(string text)
        {
            Text = text;
        }

        /// <summary>
        /// Parse inline markdown from Text and populate InlineRuns.
        /// </summary>
        private void ParseInlineRuns()
        {
            if (string.IsNullOrEmpty(_text))
            {
                InlineRuns = new();
                return;
            }

            var runs = new List<InlineRun>();
            var pos = 0;
            var text = _text;

            while (pos < text.Length)
            {
                // Look for **bold**
                var boldStart = text.IndexOf("**", pos);
                if (boldStart >= 0)
                {
                    // Add plain text before bold
                    if (boldStart > pos)
                    {
                        runs.Add(new InlineRun { Text = text.Substring(pos, boldStart - pos), Style = InlineStyle.Plain });
                    }

                    var boldEnd = text.IndexOf("**", boldStart + 2);
                    if (boldEnd >= 0)
                    {
                        runs.Add(new InlineRun { Text = text.Substring(boldStart + 2, boldEnd - boldStart - 2), Style = InlineStyle.Bold });
                        pos = boldEnd + 2;
                        continue;
                    }
                }

                // Look for *italic* (but not **)
                var italicStart = text.IndexOf('*', pos);
                if (italicStart >= 0 && (italicStart + 1 >= text.Length || text[italicStart + 1] != '*'))
                {
                    // Add plain text before italic
                    if (italicStart > pos)
                    {
                        runs.Add(new InlineRun { Text = text.Substring(pos, italicStart - pos), Style = InlineStyle.Plain });
                    }

                    var italicEnd = text.IndexOf('*', italicStart + 1);
                    if (italicEnd >= 0)
                    {
                        runs.Add(new InlineRun { Text = text.Substring(italicStart + 1, italicEnd - italicStart - 1), Style = InlineStyle.Italic });
                        pos = italicEnd + 1;
                        continue;
                    }
                }

                // Look for `code`
                var codeStart = text.IndexOf('`', pos);
                if (codeStart >= 0)
                {
                    // Add plain text before code
                    if (codeStart > pos)
                    {
                        runs.Add(new InlineRun { Text = text.Substring(pos, codeStart - pos), Style = InlineStyle.Plain });
                    }

                    var codeEnd = text.IndexOf('`', codeStart + 1);
                    if (codeEnd >= 0)
                    {
                        runs.Add(new InlineRun { Text = text.Substring(codeStart + 1, codeEnd - codeStart - 1), Style = InlineStyle.Code });
                        pos = codeEnd + 1;
                        continue;
                    }
                }

                // No more special markers, add remaining text as plain
                runs.Add(new InlineRun { Text = text.Substring(pos), Style = InlineStyle.Plain });
                break;
            }

            // If no runs were created, add entire text as plain
            if (runs.Count == 0)
            {
                runs.Add(new InlineRun { Text = text, Style = InlineStyle.Plain });
            }

            InlineRuns = runs;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a single inline run with a style (plain, bold, italic, code).
    /// </summary>
    public class InlineRun
    {
        public string Text { get; set; } = string.Empty;
        public InlineStyle Style { get; set; } = InlineStyle.Plain;
    }

    /// <summary>
    /// Enumeration of inline markdown styles.
    /// </summary>
    public enum InlineStyle
    {
        Plain,
        Bold,
        Italic,
        Code
    }
}
