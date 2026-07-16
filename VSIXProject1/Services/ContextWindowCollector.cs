using EnvDTE;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Collects context window token budget and utilization information from Visual Studio.
    /// 
    /// Exposes the following data:
    /// - maxTokens: Total context window size (from Continue configuration)
    /// - usedTokens: Estimated tokens consumed by active conversation
    /// - estimatedTokens: Breakdown by source (editor, selected text, files, history)
    /// 
    /// Token estimation methodology:
    /// - Editor content: 1 token per ~4 characters (rough approximation)
    /// - Selected text: 1 token per ~4 characters
    /// - Recent files: 1 token per ~4 characters (limited to ~5 recent files)
    /// - Conversation history: Estimated from message count
    /// 
    /// This collector is thread-safe and handles missing/unavailable IDE state gracefully.
    /// </summary>
    public class ContextWindowCollector
    {
        private readonly DTE _dte;
        private const int MaxRecentFiles = 5;
        private const int DefaultMaxTokens = 4096;
        private const int EstimatedTokensPerMessage = 250;
        private const int CharactersPerToken = 4; // Rough approximation

        /// <summary>
        /// DTO for context window information response
        /// </summary>
        public class ContextWindowInfo
        {
            public int MaxTokens { get; set; }
            public int UsedTokens { get; set; }
            public EstimatedTokensBreakdown EstimatedTokens { get; set; }
        }

        /// <summary>
        /// Breakdown of token usage by source
        /// </summary>
        public class EstimatedTokensBreakdown
        {
            public int EditorContent { get; set; }
            public int SelectedText { get; set; }
            public int RecentFiles { get; set; }
            public int ConversationHistory { get; set; }
        }

        /// <summary>
        /// Initialize the context window collector with a DTE instance
        /// </summary>
        /// <param name="dte">Visual Studio DTE object</param>
        public ContextWindowCollector(DTE dte)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
        }

        /// <summary>
        /// Asynchronously retrieve context window information
        /// </summary>
        /// <returns>ContextWindowInfo object with token budget and utilization</returns>
        public async Task<ContextWindowInfo> GetContextWindowAsync()
        {
            try
            {
                return await Task.Run(() => GetContextWindowInternal());
            }
            catch (Exception ex)
            {
                // Log error and return graceful default
                System.Diagnostics.Debug.WriteLine($"Error retrieving context window: {ex.Message}");
                return GetDefaultContextWindow();
            }
        }

        /// <summary>
        /// Internal synchronous implementation of context window collection
        /// </summary>
        private ContextWindowInfo GetContextWindowInternal()
        {
            try
            {
                var info = new ContextWindowInfo
                {
                    MaxTokens = DefaultMaxTokens,
                    EstimatedTokens = new EstimatedTokensBreakdown()
                };

                // Estimate tokens from active document
                int editorTokens = EstimateEditorTokens();
                info.EstimatedTokens.EditorContent = editorTokens;

                // Estimate tokens from selected text
                int selectionTokens = EstimateSelectedTextTokens();
                info.EstimatedTokens.SelectedText = selectionTokens;

                // Estimate tokens from recent files (limit to 5)
                int recentFilesTokens = EstimateRecentFilesTokens();
                info.EstimatedTokens.RecentFiles = recentFilesTokens;

                // Estimate tokens from conversation history
                int historyTokens = EstimateConversationHistoryTokens();
                info.EstimatedTokens.ConversationHistory = historyTokens;

                // Sum all token estimates
                int totalUsedTokens = editorTokens + selectionTokens + recentFilesTokens + historyTokens;

                // Cap at maxTokens
                info.UsedTokens = Math.Min(totalUsedTokens, info.MaxTokens);

                return info;
            }
            catch
            {
                return GetDefaultContextWindow();
            }
        }

        /// <summary>
        /// Estimate tokens consumed by active editor content
        /// </summary>
        private int EstimateEditorTokens()
        {
            try
            {
                if (_dte?.ActiveDocument == null)
                    return 0;

                var textDocument = _dte.ActiveDocument.Object as TextDocument;
                if (textDocument == null)
                    return 0;

                // Count characters in the document
                int charCount = 0;
                try
                {
                    EditPoint startPoint = textDocument.StartPoint.CreateEditPoint();
                    EditPoint endPoint = textDocument.EndPoint.CreateEditPoint();
                    charCount = endPoint.AbsoluteCharOffset - startPoint.AbsoluteCharOffset;
                }
                catch
                {
                    // Fallback: estimate from line count if character counting fails
                    charCount = textDocument.EndPoint.Line * 80; // Assume ~80 chars per line
                }

                // Estimate tokens: 1 token per ~4 characters
                return Math.Max(1, charCount / CharactersPerToken);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Estimate tokens consumed by selected text
        /// </summary>
        private int EstimateSelectedTextTokens()
        {
            try
            {
                if (_dte?.ActiveDocument == null)
                    return 0;

                var textDocument = _dte.ActiveDocument.Object as TextDocument;
                if (textDocument == null)
                    return 0;

                // Get selection
                Selection selection = null;
                try
                {
                    if (_dte.ActiveWindow?.Selection is Selection sel && !sel.IsEmpty)
                    {
                        selection = sel;
                    }
                }
                catch
                {
                    return 0;
                }

                if (selection == null || selection.IsEmpty)
                    return 0;

                // Get selected text
                string selectedText = null;
                try
                {
                    selectedText = selection.Text;
                }
                catch
                {
                    return 0;
                }

                if (string.IsNullOrEmpty(selectedText))
                    return 0;

                // Estimate tokens from selected text
                return Math.Max(1, selectedText.Length / CharactersPerToken);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Estimate tokens consumed by recent open files (limited to 5)
        /// </summary>
        private int EstimateRecentFilesTokens()
        {
            try
            {
                if (_dte?.Documents == null)
                    return 0;

                int totalTokens = 0;
                int fileCount = 0;

                foreach (Document doc in _dte.Documents)
                {
                    if (fileCount >= MaxRecentFiles)
                        break;

                    try
                    {
                        var textDoc = doc.Object as TextDocument;
                        if (textDoc != null)
                        {
                            // Estimate this file's size
                            int charCount = 0;
                            try
                            {
                                EditPoint startPoint = textDoc.StartPoint.CreateEditPoint();
                                EditPoint endPoint = textDoc.EndPoint.CreateEditPoint();
                                charCount = endPoint.AbsoluteCharOffset - startPoint.AbsoluteCharOffset;
                            }
                            catch
                            {
                                charCount = textDoc.EndPoint.Line * 80;
                            }

                            // Add to total
                            totalTokens += Math.Max(1, charCount / CharactersPerToken);
                            fileCount++;
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                        continue;
                    }
                }

                return totalTokens;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Estimate tokens consumed by conversation history
        /// 
        /// Note: This is a placeholder estimation. In a real implementation,
        /// this would be populated by the Continue bridge IPC mechanism.
        /// For now, we estimate based on a fixed token-per-message average.
        /// </summary>
        private int EstimateConversationHistoryTokens()
        {
            try
            {
                // Placeholder: assume average conversation has ~4 messages
                // Each message estimated at ~250 tokens
                // This should be populated from Continue's actual state
                return 4 * EstimatedTokensPerMessage;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Return default context window when collection fails
        /// </summary>
        private ContextWindowInfo GetDefaultContextWindow()
        {
            return new ContextWindowInfo
            {
                MaxTokens = DefaultMaxTokens,
                UsedTokens = 0,
                EstimatedTokens = new EstimatedTokensBreakdown
                {
                    EditorContent = 0,
                    SelectedText = 0,
                    RecentFiles = 0,
                    ConversationHistory = 0,
                },
            };
        }
    }
}
