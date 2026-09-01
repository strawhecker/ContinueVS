using System;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services
{
    /// <summary>
    /// Collects context window token budget and utilization information from Visual Studio.
    /// 
    /// Exposes the following data:
    /// - maxTokens: Total context window size (from active model, or TokenLimitSettings as fallback)
    /// - usedTokens: Estimated tokens consumed by active conversation
    /// - estimatedTokens: Breakdown by source (editor, selected text, files, history)
    /// 
    /// Token estimation methodology:
    /// - Editor content: 1 token per ~N characters (N from settings, default 4)
    /// - Selected text: 1 token per ~N characters
    /// - Recent files: 1 token per ~N characters (limited to ~5 recent files)
    /// - Conversation history: Estimated from message count
    /// 
    /// Token limits resolve in precedence order:
    /// 1. Active model's ContextWindow (if selected and non-zero)
    /// 2. TokenLimitSettings from ~/.continue/vsx-settings.json
    /// 3. Hardcoded default 131072
    /// 
    /// All DTE access must occur on the UI thread.
    /// </summary>
    public class ContextWindowCollector
    {
        private readonly IDteProvider _dteProvider;
        private readonly IConfigService? _configService;
        private const int MaxRecentFiles = 5;
        private const int EstimatedTokensPerMessage = 250;

        /// <summary>
        /// DTO for context window information response
        /// </summary>
        public class ContextWindowInfo
        {
            public int MaxTokens { get; set; }
            public int UsedTokens { get; set; }
            public int ReservedForNewContext { get; set; }
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
        /// Initialize the context window collector with a DTE provider abstraction.
        /// </summary>
        /// <param name="dteProvider">Abstraction over DTE for testing</param>
        /// <param name="configService">Optional config service for resolving active model's context window</param>
        public ContextWindowCollector(IDteProvider dteProvider, IConfigService? configService = null)
        {
            _dteProvider = dteProvider ?? throw new ArgumentNullException(nameof(dteProvider));
            _configService = configService;
        }

        /// <summary>
        /// Asynchronously retrieve context window information.
        /// Resolves max tokens from active model's ContextWindow, TokenLimitSettings, or hardcoded default.
        /// Must be called from the UI thread or after switching to it.
        /// </summary>
        /// <returns>ContextWindowInfo object with token budget and utilization</returns>
        public async Task<ContextWindowInfo> GetContextWindowAsync()
        {
            try
            {
                // Load token limit settings from ~/.continue/vsx-settings.json
                var settings = await TokenLimitSettings.ReadSettingsAsync();

                // Determine MaxContextTokens with precedence: model > settings > hardcoded default
                int maxContextTokens = ResolveMaxContextTokens(settings);
                _ = LoggerService.Current.WriteDebugAsync($"[gap19-collector-resolve-tokens] ResolvedMaxContextTokens={maxContextTokens}");

                // DTE access is handled inside IDteProvider implementations;
                // those implementations are responsible for their own thread marshalling.
                return GetContextWindowInternal(settings, maxContextTokens);
            }
            catch (Exception ex)
            {
                // Log error and return graceful default
                _ = LoggerService.Current.WriteErrorAsync($"Error retrieving context window: {ex.Message}", ex);
                return GetDefaultContextWindow();
            }
        }

        /// <summary>
        /// Resolve MaxContextTokens from active model (if available) with fallback to settings.
        /// Precedence: model.ContextWindow > settings.MaxContextTokens > 131072
        /// </summary>
        private int ResolveMaxContextTokens(TokenLimitSettings.TokenLimitConfig settings)
        {
            try
            {
                // Try to get active model's context window from config service
                if (_configService != null)
                {
                    var activeModel = _configService.GetSelectedModel();
                    if (activeModel != null && activeModel.ContextWindow > 0)
                    {
                        _ = LoggerService.Current.WriteDebugAsync($"[gap19-collector-active-model] Using active model context window: {activeModel.ContextWindow} (model: {activeModel.Name})");
                        return activeModel.ContextWindow;
                    }
                    else if (activeModel != null)
                    {
                        _ = LoggerService.Current.WriteDebugAsync($"[gap19-collector-no-context-window] Active model selected but ContextWindow is 0 or null: {activeModel.Name}");
                    }
                    else
                    {
                        _ = LoggerService.Current.WriteDebugAsync($"[gap19-collector-no-active-model] No active model selected; using settings file");
                    }
                }
                else
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[gap19-collector-no-config-service] ConfigService not available; using settings file");
                }

                // Fall back to settings file value
                _ = LoggerService.Current.WriteDebugAsync($"[gap19-collector-settings-fallback] Using TokenLimitSettings.MaxContextTokens: {settings.MaxContextTokens}");
                return settings.MaxContextTokens;
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[gap19-collector-resolve-error] Error resolving max context tokens: {ex.Message}; falling back to settings", ex);
                return settings.MaxContextTokens;
            }
        }

        /// <summary>
        /// Internal synchronous implementation of context window collection
        /// Must be called on the UI thread
        /// </summary>
        private ContextWindowInfo GetContextWindowInternal(TokenLimitSettings.TokenLimitConfig settings, int maxContextTokens)
        {
            try
            {
                var info = new ContextWindowInfo
                {
                    MaxTokens = maxContextTokens,
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

                // Calculate reserved space for new context (remaining available tokens minus 5% safety margin)
                int safetyMargin = Math.Max(1, info.MaxTokens / 20); // 5% safety margin
                info.ReservedForNewContext = Math.Max(0, info.MaxTokens - info.UsedTokens - safetyMargin);

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
                var content = _dteProvider.GetActiveDocumentContent();
                if (string.IsNullOrEmpty(content))
                    return 0;

                return Math.Max(1, content.Length / settings.CharsPerToken);
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
                var selectedText = _dteProvider.GetSelectedText();
                if (string.IsNullOrEmpty(selectedText))
                    return 0;

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
                var recentFiles = _dteProvider.GetRecentFiles(MaxRecentFiles);
                if (recentFiles == null || recentFiles.Count == 0)
                    return 0;

                // Estimate: assume each file is about 500 bytes on average
                int totalTokens = 0;
                foreach (var filePath in recentFiles)
                {
                    // Rough estimate: file path + assumed content
                    int estimatedChars = filePath.Length + 500;
                    totalTokens += Math.Max(1, estimatedChars / settings.CharsPerToken);
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
