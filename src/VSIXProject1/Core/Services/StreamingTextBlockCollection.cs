using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Append-only ObservableCollection of TextBlockModel with O(1) newline-based reentrancy.
    /// Efficiently handles streaming text by splitting on newlines and managing TextBlocks.
    /// </summary>
    public class StreamingTextBlockCollection : ObservableCollection<TextBlockModel>
    {
        /// <summary>
        /// Appends streaming text chunk. If chunk contains no newlines, extends last TextBlock.
        /// If chunk contains newlines, closes last block and creates new blocks for each line.
        /// </summary>
        public void AppendChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return;
            }

            var lines = chunk.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0)
                {
                    // First line: append to last TextBlock if exists, else create new
                    if (Count > 0)
                    {
                        Items[Count - 1].Text += lines[i];
                    }
                    else if (!string.IsNullOrEmpty(lines[i]))
                    {
                        Add(new TextBlockModel(lines[i]));
                    }
                }
                else
                {
                    // Subsequent lines: create new TextBlock (unless empty and last line)
                    if (!string.IsNullOrEmpty(lines[i]) || i < lines.Length - 1)
                    {
                        Add(new TextBlockModel(lines[i]));
                    }
                }
            }
        }

        /// <summary>
        /// Clears all TextBlocks and resets collection.
        /// </summary>
        public new void Clear()
        {
            base.Clear();
        }

        /// <summary>
        /// Returns the concatenated text of all TextBlocks joined by newlines.
        /// </summary>
        public string GetCombinedText()
        {
            if (Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", this.Select(t => t.Text));
        }

        /// <summary>
        /// Returns the count of TextBlocks in the collection.
        /// </summary>
        public int BlockCount => Count;
    }
}
