using EnvDTE;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.Services
{
    /// <summary>
    /// Collects context window token budget and utilization information from Visual Studio.
    /// 
    /// Exposes the following data:
    /// - maxTokens: Total context window size (from TokenLimitSettings)
    /// - usedTokens: Estimated tokens consumed by active conversation
    /// - estimatedTokens: Breakdown by source (editor, selected text, files, history)
    /// 
    /// Token estimation methodology:
    /// - Editor content: 1 token per ~N characters (N from settings, default 4)
    /// - Selected text: 1 token per ~N characters
    /// - Recent files: 1 token per ~N characters (limited to ~5 recent files)
    /// - Conversation history: Estimated from message count
    /// 
    /// Token limits are read from ~/.continue/vsx-settings.json at startup.
    /// All DTE access must occur on the UI thread.
    /// </summary>
    public class ContextWindowCollector
    {
        private readonly DTE _dte;
        private const int MaxRecentFiles = 5;
        private const int EstimatedTokensPerMessage = 250;

        /// <summary>
        /// DTO for context window information response
        /// </summary>
        public class ContextWindowInfo
        {
            public int MaxTokens { get; set; }
            public int UsedTokens { get; set; }
            public EstimatedTokensBreakdown EstimatedTokens { get; set; } = new EstimatedTokensBreakdown();
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
        /// Asynchronously retrieve context window information.
        /// Reads token limits from ~/.continue/vsx-settings.json.
        /// Falls back to hardcoded defaults if settings unavailable.
        /// Must be called from the UI thread or after switching to it.
        /// </summary>
        /// <returns>ContextWindowInfo object with token budget and utilization</returns>
        public async Task<ContextWindowInfo> GetContextWindowAsync()
        {
            try
            {
                // Load token limit settings from ~/.continue/vsx-settings.json
                var settings = await TokenLimitSettings.ReadSettingsAsync();

                // Switch to UI thread for DTE access
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                return GetContextWindowInternal(settings);
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
        /// Must be called on the UI thread
        /// </summary>
        private ContextWindowInfo GetContextWindowInternal(TokenLimitSettings.TokenLimitConfig settings)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var info = new ContextWindowInfo
                {
                    MaxTokens = settings.MaxContextTokens,
                    EstimatedTokens = new EstimatedTokensBreakdown()
                };

                // Estimate tokens from active document
                int editorTokens = EstimateEditorTokens(settings);
                info.EstimatedTokens.EditorContent = editorTokens;

                // Estimate tokens from selected text
                int selectionTokens = EstimateSelectedTextTokens(settings);
                info.EstimatedTokens.SelectedText = selectionTokens;

                // Estimate tokens from recent files (limit to 5)
                int recentFilesTokens = EstimateRecentFilesTokens(settings);
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
        /// Uses the charsPerToken ratio from token limit settings
        /// Must be called on the UI thread
        /// </summary>
        private int EstimateEditorTokens(TokenLimitSettings.TokenLimitConfig settings)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (_dte?.ActiveDocument == null)
                    return 0;

                // Access the Object property and check for TextDocument
                #pragma warning disable CS8974
                object? docObjValue = _dte.ActiveDocument.Object;
                #pragma warning restore CS8974
                if (!(docObjValue is TextDocument textDocument))
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

                // Estimate tokens using configured ratio
                return Math.Max(1, charCount / settings.CharsPerToken);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Estimate tokens consumed by selected text
        /// Uses the charsPerToken ratio from token limit settings
        /// Must be called on the UI thread
        /// </summary>
        private int EstimateSelectedTextTokens(TokenLimitSettings.TokenLimitConfig settings)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (_dte?.ActiveDocument == null)
                    return 0;

                // Access the Object property and check for TextDocument
                #pragma warning disable CS8974
                object? docObjValue = _dte.ActiveDocument.Object;
                #pragma warning restore CS8974
                if (!(docObjValue is TextDocument textDocument))
                    return 0;

                // Get selection - safely check type first  
                object? selection = null;
                try
                {
                    selection = _dte.ActiveWindow?.Selection;
                }
                catch
                {
                    return 0;
                }

                // Type-check for TextSelection before accessing properties
                if (selection is not TextSelection textSelection)
                    return 0;

                if (textSelection.IsEmpty)
                    return 0;

                // Get selected text
                string? selectedText = null;
                try
                {
                    selectedText = textSelection.Text;
                }
                catch
                {
                    return 0;
                }

                if (string.IsNullOrEmpty(selectedText))
                    return 0;

                // Estimate tokens from selected text using configured ratio
                return Math.Max(1, selectedText.Length / settings.CharsPerToken);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Estimate tokens consumed by recent open files (limited to 5)
        /// Uses the charsPerToken ratio from token limit settings
        /// Must be called on the UI thread
        /// </summary>
        private int EstimateRecentFilesTokens(TokenLimitSettings.TokenLimitConfig settings)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

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
                        // Access the Object property and check for TextDocument
                        #pragma warning disable CS8974
                        object? docObjValue = doc.Object;
                        #pragma warning restore CS8974
                        if (!(docObjValue is TextDocument textDoc))
                            continue;

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
                            // Fallback
                            charCount = textDoc.EndPoint.Line * 80;
                        }

                        int fileTokens = Math.Max(1, charCount / settings.CharsPerToken);
                        totalTokens += fileTokens;
                        fileCount++;
                    }
                    catch
                    {
                        // Skip this file and continue
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
        /// Uses defaults from TokenLimitSettings
        /// </summary>
        private ContextWindowInfo GetDefaultContextWindow()
        {
            // Create default settings (131072 max, 8192 reserve)
            var defaultSettings = new TokenLimitSettings.TokenLimitConfig();
            return new ContextWindowInfo
            {
                MaxTokens = defaultSettings.MaxContextTokens,
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
