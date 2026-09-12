using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using ContinueVS.Core.Parsers;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a single text block in a streaming response.
    /// Contains the raw text and parsed inline runs for WPF TextBlock rendering.
    /// Automatically parses inline markdown (bold, italic, code) when Text is set.
    /// </summary>
    public class TextBlockModel : INotifyPropertyChanged
    {
        private string _text = string.Empty;
        private List<Run> _inlineRuns = new List<Run>();
        private bool _isCodeBlock;
        private DateTime _createdAt;
        private int _chunkIndex;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raw text content of this block.
        /// Setting this property automatically triggers markdown parsing via SelectiveMarkdownParser.
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    RefreshInlineRuns();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Parsed inline runs with styling (bold, italic, code).
        /// Populated automatically when Text is set.
        /// </summary>
        public List<Run> InlineRuns
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
        /// Flag indicating whether this block contains code (triple backticks or indented block).
        /// Used for selection highlighting or future syntax highlighting.
        /// </summary>
        public bool IsCodeBlock
        {
            get => _isCodeBlock;
            set
            {
                if (_isCodeBlock != value)
                {
                    _isCodeBlock = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Timestamp when this block was created during streaming.
        /// Used for performance metrics and debugging.
        /// </summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (_createdAt != value)
                {
                    _createdAt = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Sequential index of the chunk that created this block.
        /// Used for debugging and maintaining chunk order.
        /// </summary>
        public int ChunkIndex
        {
            get => _chunkIndex;
            set
            {
                if (_chunkIndex != value)
                {
                    _chunkIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public TextBlockModel()
        {
            _createdAt = DateTime.UtcNow;
            _inlineRuns = new List<Run>();
        }

        /// <summary>
        /// Parse the text content using selective markdown parser and regenerate InlineRuns.
        /// Called automatically when Text property is set.
        /// </summary>
        private void RefreshInlineRuns()
        {
            try
            {
                _inlineRuns = SelectiveMarkdownParser.ParseLine(_text);
            }
            catch (Exception ex)
            {
                // Fallback: render as plain text if parsing fails
                System.Diagnostics.Debug.WriteLine($"[TextBlockModel] Failed to parse inline markdown: {ex.Message}");
                _inlineRuns = new List<Run> { new Run { Text = _text } };
            }
            OnPropertyChanged(nameof(InlineRuns));
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
