using System;
using System.Collections.ObjectModel;
using ContinueVS.Core.Types;

namespace ContinueVS.Core.Services
{
    /// <summary>
    /// Append-only collection wrapper around ObservableCollection<TextBlockModel>.
    /// Implements O(n) streaming performance by accumulating content into TextBlock models
    /// with reentrancy: if a chunk contains no newline, extend the last TextBlock;
    /// if it contains newlines, close the last block and create new ones.
    /// </summary>
    public class StreamingTextBlockCollection
    {
        private readonly ObservableCollection<TextBlockModel> _blocks;
        private int _chunkIndex = 0;

        /// <summary>
        /// Underlying ObservableCollection of TextBlockModel items.
        /// Bind UI ItemsControl or ItemsSource to this collection.
        /// </summary>
        public ObservableCollection<TextBlockModel> Blocks
        {
            get => _blocks;
        }

        public StreamingTextBlockCollection()
        {
            _blocks = new ObservableCollection<TextBlockModel>();
        }

        /// <summary>
        /// Append a chunk of text to the collection.
        /// If chunk contains no newline, extends the last TextBlock.
        /// If chunk contains newlines, closes the last block and creates new blocks for each line.
        /// Cost: O(c) where c = chunk size (only newline detection and split, no re-parsing).
        /// </summary>
        /// <param name="chunk">Text chunk to append</param>
        public void AppendChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return;
            }

            // Check if chunk contains newlines
            int newlineIndex = chunk.IndexOf('\n');

            if (newlineIndex == -1)
            {
                // No newline in chunk: append to last block or create new one
                if (_blocks.Count > 0)
                {
                    var lastBlock = _blocks[_blocks.Count - 1];
                    lastBlock.Text += chunk;
                }
                else
                {
                    // First chunk with no newline: create initial block
                    var newBlock = new TextBlockModel
                    {
                        Text = chunk,
                        ChunkIndex = _chunkIndex++,
                        CreatedAt = DateTime.UtcNow
                    };
                    _blocks.Add(newBlock);
                }
            }
            else
            {
                // Chunk contains newlines: split and create new blocks for each line
                int currentStart = 0;
                int currentNewline = newlineIndex;

                while (currentNewline >= 0)
                {
                    // Extract text up to (but not including) the newline
                    string linePart = chunk.Substring(currentStart, currentNewline - currentStart);

                    // Append line part to last block or create new block
                    if (_blocks.Count > 0)
                    {
                        var lastBlock = _blocks[_blocks.Count - 1];
                        lastBlock.Text += linePart;
                    }
                    else
                    {
                        // Create new block for first line
                        var newBlock = new TextBlockModel
                        {
                            Text = linePart,
                            ChunkIndex = _chunkIndex++,
                            CreatedAt = DateTime.UtcNow
                        };
                        _blocks.Add(newBlock);
                    }

                    // Move past the newline and start a new block
                    currentStart = currentNewline + 1;
                    currentNewline = chunk.IndexOf('\n', currentStart);

                    // If there's more content after this newline, create a new block
                    if (currentStart < chunk.Length)
                    {
                        if (currentNewline == -1)
                        {
                            // No more newlines: remainder goes into new block
                            string remainder = chunk.Substring(currentStart);
                            var newBlock = new TextBlockModel
                            {
                                Text = remainder,
                                ChunkIndex = _chunkIndex++,
                                CreatedAt = DateTime.UtcNow
                            };
                            _blocks.Add(newBlock);
                            break;
                        }
                        else
                        {
                            // More newlines: create block for text between newlines
                            var newBlock = new TextBlockModel
                            {
                                Text = string.Empty,
                                ChunkIndex = _chunkIndex++,
                                CreatedAt = DateTime.UtcNow
                            };
                            _blocks.Add(newBlock);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clear all blocks from the collection.
        /// Used when starting a new streaming response.
        /// </summary>
        public void Clear()
        {
            _blocks.Clear();
            _chunkIndex = 0;
        }

        /// <summary>
        /// Get the concatenated text of all blocks as a single string.
        /// Blocks are joined with newlines.
        /// </summary>
        public string GetConcatenatedText()
        {
            if (_blocks.Count == 0)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _blocks.Count; i++)
            {
                sb.Append(_blocks[i].Text);
                if (i < _blocks.Count - 1)
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }
    }
}
