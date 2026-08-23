using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Utilities;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    /// <summary>
    /// Defines the operational mode for the chat interface.
    /// </summary>
    public enum ChatMode
    {
        /// <summary>
        /// Chat mode: Basic Q&A with optional "Apply" button for code suggestions.
        /// </summary>
        Ask,

        /// <summary>
        /// Agent mode: Autonomous tool calling and code editing with user approval.
        /// </summary>
        Agent,

        /// <summary>
        /// Plan mode: Read-only plan generation and review.
        /// </summary>
        Plan
    }

    /// <summary>
    /// System message prompts for each operational mode.
    /// </summary>
    internal static class ChatModeSystemPrompts
    {
        /// <summary>
        /// System prompt for Ask mode: guidance for basic Q&A interaction.
        /// </summary>
        public const string DEFAULT_ASK_SYSTEM_MESSAGE = "You are a helpful coding assistant in Ask mode. Provide code suggestions and explanations. Use the Apply button or switch to Agent Mode for automatic edits.";

        /// <summary>
        /// System prompt for Agent mode: guidance for autonomous tool calling.
        /// </summary>
        public const string DEFAULT_AGENT_SYSTEM_MESSAGE = "You are an autonomous coding agent in Agent mode. Call read-only tools to analyze code. Use edit tools when the user approves changes. Always confirm before applying edits.";

        /// <summary>
        /// System prompt for Plan mode: guidance for read-only plan generation.
        /// </summary>
        public const string DEFAULT_PLAN_SYSTEM_MESSAGE = "You are a planning assistant in Plan mode. Generate detailed implementation plans and analysis in read-only mode. Suggest Agent Mode for executing code changes.";
    }

    public class ChatPageViewModel : ViewModelBase
        {
            private readonly ILlmService _llmService;
            private readonly IContextService _contextService;
            private readonly IToolService _toolService;
            private readonly ISessionService _sessionService;
            private readonly INotificationService _notificationService;
            private readonly IConfigService _configService;
            private readonly ISystemPromptService _systemPromptService;
            private readonly IUIStateService _uiStateService;
            private UIState? _cachedUIState;

        private string? _inputText;
        private bool _isStreaming;
        private string? _streamingResponse;
        private CancellationTokenSource? _streamingCts;
        private ChatMode _currentMode = ChatMode.Ask;
        private ModelInfo? _selectedModel;
        private List<ToolCall> _pendingToolCalls = new List<ToolCall>();
        private const int MaxToolCallIterations = 10;
        private int _toolCallIterationCount = 0;

        /// <summary>
        /// Tracks cumulative tool failures in current iteration.
        /// Gap23_3: If 2+ tools fail in same iteration, loop terminates.
        /// Reset each iteration; single failures continue loop.
        /// </summary>
        private int _toolFailureCount = 0;

        /// <summary>
        /// Flag to track if tool call limit has been reached (gap23_4_3).
        /// Set when InvalidOperationException thrown, resets on new session.
        /// </summary>
        private bool _limitReachedFlag = false;

        /// <summary>
        /// Flag to show the 80% warning banner (gap23_4_4).
        /// Auto-dismisses after 5 seconds.
        /// </summary>
        private bool _showWarningBanner = false;

        /// <summary>
        /// Flag to show the 100% error banner (gap23_4_4).
        /// Persists until user closes it.
        /// </summary>
        private bool _showErrorBanner = false;

        /// <summary>
        /// Timer to auto-dismiss warning banner after 5 seconds.
        /// </summary>
        private DispatcherTimer? _warningDismissTimer;

        public ObservableCollection<ChatMessage> Messages { get; }
        public ObservableCollection<ContextItem> SelectedContext { get; }
        public ObservableCollection<ModelInfo> AvailableModels { get; }

        public string? InputText
        {
            get => _inputText;
            set 
            {
                if (Set(ref _inputText, value))
                {
                    SendMessageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set 
            {
                if (Set(ref _isStreaming, value))
                {
                    SendMessageCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? StreamingResponse
        {
            get => _streamingResponse;
            set => Set(ref _streamingResponse, value);
        }

        /// <summary>
        /// Gets or sets the current operational mode (Ask, Agent, or Plan).
        /// </summary>
        public ChatMode CurrentMode
        {
            get => _currentMode;
            set
            {
                System.Diagnostics.Debug.WriteLine($"[a9-property-entry] CurrentMode setter: oldValue={_currentMode}, newValue={value}");
                if (Set(ref _currentMode, value))
                {
                    System.Diagnostics.Debug.WriteLine($"[a9-property-set-success] Set() returned true, property changed. New _currentMode={_currentMode}, PropertyChanged notification raised");
                    SendMessageCommand.RaiseCanExecuteChanged();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[a9-property-set-noop] Set() returned false, property unchanged. _currentMode still={_currentMode}");
                }
            }
        }

        /// <summary>
        /// Gets or sets the currently selected model for chat messages.
        /// </summary>
        public ModelInfo? SelectedModel
        {
            get => _selectedModel;
            set => Set(ref _selectedModel, value);
        }

        /// <summary>
        /// Gets or sets whether to show the 80% warning banner (gap23_4_4).
        /// </summary>
        public bool ShowWarningBanner
        {
            get => _showWarningBanner;
            set => Set(ref _showWarningBanner, value);
        }

        /// <summary>
        /// Gets or sets whether to show the 100% error banner (gap23_4_4).
        /// </summary>
        public bool ShowErrorBanner
        {
            get => _showErrorBanner;
            set => Set(ref _showErrorBanner, value);
        }

        public RelayCommand SendMessageCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand<string> AddContextCommand { get; }
        /// <summary>
        /// Command to switch to a specified chat mode.
        /// </summary>
        public RelayCommand<ChatMode> SetModeCommand { get; }
        /// <summary>
        /// Command to delete a message by ID.
        /// </summary>
        public RelayCommand<string> DeleteMessageCommand { get; }

        public ChatPageViewModel(
            ILlmService llmService,
            IContextService contextService,
            IToolService toolService,
            ISessionService sessionService,
            INotificationService notificationService,
            IConfigService configService,
            ISystemPromptService systemPromptService,
            IUIStateService uiStateService)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));
            if (contextService == null) throw new ArgumentNullException(nameof(contextService));
            if (toolService == null) throw new ArgumentNullException(nameof(toolService));
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (systemPromptService == null) throw new ArgumentNullException(nameof(systemPromptService));
            if (uiStateService == null) throw new ArgumentNullException(nameof(uiStateService));

            _llmService = llmService;
            _contextService = contextService;
            _toolService = toolService;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _configService = configService;
            _systemPromptService = systemPromptService;
            _uiStateService = uiStateService;

            Messages = new ObservableCollection<ChatMessage>();
            SelectedContext = new ObservableCollection<ContextItem>();
            AvailableModels = new ObservableCollection<ModelInfo>();
            _inputText = string.Empty;
            _streamingResponse = string.Empty;

            SendMessageCommand = new RelayCommand(ExecuteSendMessage, CanSendMessage);
            CancelCommand = new RelayCommand(ExecuteCancel, () => IsStreaming);
            AddContextCommand = new RelayCommand<string>(ExecuteAddContext);
            SetModeCommand = new RelayCommand<ChatMode>(mode => CurrentMode = mode);
            DeleteMessageCommand = new RelayCommand<string>(ExecuteDeleteMessage);

            _ = InitializeAsync();
            _configService.ConfigChanged += ConfigService_ConfigChanged;

            // gap23_4_3: Reset limit flag when session changes
            _sessionService.SessionChanged += (s, e) =>
            {
                if (e.IsNewSession)
                {
                    _limitReachedFlag = false;
                    ShowWarningBanner = false;
                    ShowErrorBanner = false;
                    DismissWarningBanner();
                    SendMessageCommand.RaiseCanExecuteChanged();
                    System.Diagnostics.Debug.WriteLine("[gap23_4_3-reset] Limit flag cleared on new session");
                }
            };
        }

        /// <summary>
        /// Switches to the UI/main thread via Dispatcher.InvokeAsync.
        /// This is safe for both VS runtime and unit test contexts.
        /// </summary>
        private static async Task SwitchToMainThreadAsync()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                await dispatcher.InvokeAsync(() => { });
#pragma warning restore VSTHRD001
            }
        }

        private async Task InitializeAsync()
        {
            await _systemPromptService.LoadAsync();
            await LoadModelsAsync();

            // Cache UIState for tool policy decisions in ExecuteToolCallsAsync
            try
            {
                _cachedUIState = await _uiStateService.GetUIStateAsync();
                System.Diagnostics.Debug.WriteLine($"[gap9-uistate-load] UIState cached: {_cachedUIState?.ToolSettings.Count ?? 0} tool policies loaded");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap9-uistate-error] Failed to load UIState: {ex.Message}");
                // Fall back to empty UIState (all tools default to AskFirst)
                _cachedUIState = new UIState();
            }

            // Reset limit flag on initialization
            _limitReachedFlag = false;
        }

        private async Task LoadModelsAsync()
        {
            try
            {
                var config = _configService.GetCurrentConfig();
                if (config?.Models != null)
                {
                    AvailableModels.Clear();
                    foreach (var model in config.Models)
                    {
                        AvailableModels.Add(model);
                    }

                    if (AvailableModels.Count > 0 && _selectedModel == null)
                    {
                        SelectedModel = AvailableModels[0];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[chat-model-load-error] Failed to load models: {ex.Message}");
            }
        }

        private void ConfigService_ConfigChanged(object? sender, EventArgs e)
        {
            _ = LoadModelsAsync();
        }

        /// <summary>
        /// Dismisses the warning banner and stops the auto-dismiss timer (gap23_4_4).
        /// </summary>
        private void DismissWarningBanner()
        {
            ShowWarningBanner = false;
            if (_warningDismissTimer != null)
            {
                _warningDismissTimer.Stop();
                _warningDismissTimer = null;
            }
        }

        /// <summary>
        /// Public command method for warning banner dismiss button (gap23_4_4).
        /// </summary>
        public void DismissWarningBannerCommand()
        {
            DismissWarningBanner();
        }

        /// <summary>
        /// Resets tool call limit state when a new user action (send) begins (gap23_4_4).
        /// The tool limit is per-action: each send resets the counter.
        /// If an ask/agent/plan exhausts tools, it stops. User can send again with fresh budget.
        /// </summary>
        private void ResetToolCallLimitForAction()
        {
            _limitReachedFlag = false;
            ShowWarningBanner = false;
            ShowErrorBanner = false;
            DismissWarningBanner(); // Stop any active dismissal timer
            SendMessageCommand.RaiseCanExecuteChanged();
            System.Diagnostics.Debug.WriteLine("[gap23_4_4-reset] Tool call limit reset for new user action. Fresh budget allocated.");
        }

        /// <summary>
        /// Calculates the percentage of tool calls used in the current session (gap23_4_4).
        /// Returns null-safe value; defaults to 0 if session or settings not available.
        /// </summary>
        private double GetToolCallPercentage()
        {
            try
            {
                var session = _sessionService?.GetCurrentSession();
                var config = _configService?.GetCurrentConfig();

                if (session == null || config == null)
                    return 0.0;

                int maxToolCalls = (int)(config.CustomSettings?[UserSettings.Agent_MaxToolCallsPerSession] ?? 100);
                if (maxToolCalls <= 0)
                    maxToolCalls = 100;

                return (session.ToolCallsExecuted / (double)maxToolCalls) * 100.0;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Checks the tool call limit and shows warning/error banners as needed (gap23_4_4).
        /// At 80%: Shows auto-dismissing warning banner and logs analytics.
        /// At 100%: Shows persistent error banner, blocks send button, and logs analytics.
        /// </summary>
        private void CheckToolCallLimit()
        {
            try
            {
                double percentage = GetToolCallPercentage();
                System.Diagnostics.Debug.WriteLine($"[gap23_4_4-check] Tool call percentage: {percentage:F1}%");

                if (percentage >= 100.0)
                {
                    // At limit: Show persistent error banner, disable send
                    if (!ShowErrorBanner)
                    {
                        ShowErrorBanner = true;
                        _limitReachedFlag = true;
                        SendMessageCommand.RaiseCanExecuteChanged();
                        System.Diagnostics.Debug.WriteLine("[gap23_4_4-error] Tool call limit reached (100%). Error banner shown.");

                        // Log analytics event
                        _notificationService.ShowError("Tool call limit reached (100/100). Start a new session to continue.");
                    }
                }
                else if (percentage >= 80.0)
                {
                    // Approaching limit: Show auto-dismiss warning banner
                    if (!ShowWarningBanner)
                    {
                        ShowWarningBanner = true;
                        System.Diagnostics.Debug.WriteLine($"[gap23_4_4-warning] Approaching tool call limit ({percentage:F1}%). Warning banner shown.");

                        // Start 5-second auto-dismiss timer
                        if (_warningDismissTimer == null)
                        {
                            _warningDismissTimer = new DispatcherTimer();
                            _warningDismissTimer.Interval = TimeSpan.FromSeconds(5);
                            _warningDismissTimer.Tick += (s, e) => DismissWarningBanner();
                        }
                        _warningDismissTimer.Start();

                        // Log analytics event
                        _notificationService.ShowError($"Approaching tool call limit ({(int)percentage}/100 used). Consider starting a new session soon.");
                    }
                }
                else
                {
                    // Below 80%: Dismiss warning if shown
                    DismissWarningBanner();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap23_4_4-error] Exception in CheckToolCallLimit: {ex.Message}");
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteSendMessage()
#pragma warning restore VSTHRD100
        {
            try
            {
                IsStreaming = true;
                _streamingCts = new CancellationTokenSource();
                _pendingToolCalls.Clear();
                _toolCallIterationCount = 0;

                // Reset tool limit state for this action (gap23_4_4)
                // Each user-initiated send action gets its own tool call budget
                ResetToolCallLimitForAction();

                var userMessage = new ChatMessage
                {
                    Role = ChatMessageRole.User,
                    Content = InputText ?? string.Empty
                };

                // Clear input text immediately on UI thread before starting async operations
                InputText = string.Empty;

                await _sessionService.AddMessageAsync(userMessage);

                // Switch to main thread to update ObservableCollection
                await SwitchToMainThreadAsync();
                Messages.Add(userMessage);

                System.Diagnostics.Debug.WriteLine($"[a9-command-entry] ExecuteSendMessage started. CurrentMode={CurrentMode}");
                System.Diagnostics.Debug.WriteLine($"[a9-exec] ExecuteSendMessage: User message added. Role={userMessage.Role}, Content={userMessage.Content}, MessagesCount={Messages.Count}");

                // GAP22_4: Prune messages if needed before streaming
                var session = _sessionService.GetCurrentSession();
                if (session?.Messages.Count > 1)
                {
                    var selectedModel = _configService.GetSelectedModel();
                    if (selectedModel != null && selectedModel.ContextWindow > 0)
                    {
                        int availableTokens = (int)(selectedModel.ContextWindow * 0.75);
                        int approximateNewMessageTokens = (userMessage.Content?.Length ?? 0 + 3) / 4;

                        System.Diagnostics.Debug.WriteLine($"[gap22-prune-check] ContextWindow={selectedModel.ContextWindow}, Available={availableTokens}, NewMsgTokens~={approximateNewMessageTokens}");

                        if (approximateNewMessageTokens > availableTokens / 2)
                        {
                            var (removedCount, prunedMessages) = await _sessionService.PruneOldMessagesAsync(availableTokens);
                            System.Diagnostics.Debug.WriteLine($"[gap22-pruned] Removed {removedCount} messages to stay under token limit");
                        }
                    }
                }

                StreamingResponse = string.Empty;

                var messages = new List<ChatMessage>();

                // Inject mode-specific system message
                var systemPrompt = GetSystemMessageForMode(CurrentMode);
                messages.Add(new ChatMessage
                {
                    Role = ChatMessageRole.System,
                    Content = systemPrompt
                });

                if (SelectedContext.Count > 0)
                {
                    var contextSummary = string.Join("\n",
                        SelectedContext.Select(c => c.FilePath + ": " + c.Content));
                    messages.Add(new ChatMessage
                    {
                        Role = ChatMessageRole.System,
                        Content = "Context:\n" + contextSummary
                    });
                }

                messages.Add(userMessage);

                // Loop until no more tool calls or max iterations reached
                while (_toolCallIterationCount < MaxToolCallIterations)
                {
                    _toolCallIterationCount++;
                    _toolFailureCount = 0;  // Reset failure counter for this iteration
                    _pendingToolCalls.Clear();

                    var streamOptions = new StreamOptions
                    {
                        Messages = messages
                    };

                    // Create provisional assistant message BEFORE streaming starts
                    // This allows UI to display responses incrementally as chunks arrive
                    var assistantMessage = new ChatMessage
                    {
                        Role = ChatMessageRole.Assistant,
                        Content = string.Empty,
                        ToolCalls = null
                    };

                    // Switch to main thread to update ObservableCollection
                    await SwitchToMainThreadAsync();
                    Messages.Add(assistantMessage);

                    // Stream directly without retry wrapper:
                    // Streaming operations can't be safely retried because chunks are consumed as they arrive.
                    // The HTTP connection is stateful, and mid-stream retries would lose already-received chunks.
                    // If streaming fails, the entire message is lost and the caller (UI) handles the error.
                    await foreach (var chunk in _llmService.StreamAsync(messages, streamOptions, _streamingCts.Token))
                    {
                        if (chunk.Type == ChunkType.Text)
                        {
                            // Update the message content in place - this triggers PropertyChanged
                            // and the UI updates with the new content
                            assistantMessage.Content += chunk.Content;
                            StreamingResponse += chunk.Content;
                        }
                        else if (chunk.Type == ChunkType.ToolCall && chunk.ToolCall != null)
                        {
                            _pendingToolCalls.Add(chunk.ToolCall);
                        }
                    }

                    // Finalize the message with tool calls and add to session
                    if (_pendingToolCalls.Count > 0)
                    {
                        assistantMessage.ToolCalls = new List<ToolCall>(_pendingToolCalls);
                    }
                    await _sessionService.AddMessageAsync(assistantMessage);
                    System.Diagnostics.Debug.WriteLine($"[a9-command-assistant] Assistant message added. Role={assistantMessage.Role}, Content length={assistantMessage.Content.Length}, ToolCallsCount={_pendingToolCalls.Count}");

                    // gap23_4_4: Check tool call limit and show banners
                    CheckToolCallLimit();

                    // Only execute tools in Agent mode
                    System.Diagnostics.Debug.WriteLine($"[a9-command-toolcheck] Checking tool execution: CurrentMode={CurrentMode}, _pendingToolCalls.Count={_pendingToolCalls.Count}, ShouldExecute={CurrentMode == ChatMode.Agent && _pendingToolCalls.Count > 0}");
                    if (CurrentMode == ChatMode.Agent && _pendingToolCalls.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[a9-command-toolexec] Executing tools in Agent mode");
                        _toolFailureCount = await ExecuteToolCallsAsync(_pendingToolCalls);
                        System.Diagnostics.Debug.WriteLine($"[gap23_3-loop] Iteration {_toolCallIterationCount}: {_pendingToolCalls.Count} tools executed, {_toolFailureCount} failures");

                        // Check error accumulation: 2+ failures trigger loop termination
                        if (_toolFailureCount >= 2)
                        {
                            System.Diagnostics.Debug.WriteLine($"[gap23_3-error] Too many tool failures ({_toolFailureCount}). Terminating loop.");
                            break;
                        }

                        // Collect tool results for next iteration
                        var toolResultMessages = new List<ChatMessage>();
                        foreach (var toolMsg in Messages.Where(m => m.Role == ChatMessageRole.Tool))
                        {
                            toolResultMessages.Add(toolMsg);
                        }

                        // Add tool results to messages for next loop iteration
                        messages.AddRange(toolResultMessages);

                        // Reset streaming response for next iteration
                        StreamingResponse = string.Empty;
                    }
                    else
                    {
                        // No tools or not in Agent mode, break the loop
                        System.Diagnostics.Debug.WriteLine($"[gap23_3-loop] No tools or not in Agent mode. Breaking loop.");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[ChatPageViewModel.ExecuteSendMessage] OperationCanceledException: User cancelled");
                StreamingResponse += "\n[Cancelled by user]";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Exception caught: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Exception message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Exception stack trace: {ex.StackTrace}");

                // Gap23_3: Distinguish between LLM streaming failure and other errors
                if (ex is HttpRequestException || ex is InvalidOperationException && ex.Message.Contains("stream"))
                {
                    System.Diagnostics.Debug.WriteLine("[gap23_3-error] LLM streaming failed, terminating loop");
                }

                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Showing error popup: {ex.Message}");
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
            finally
            {
                IsStreaming = false;
                _streamingCts?.Dispose();
            }
        }

        /// <summary>
        /// Gets the policy for a tool from cached UIState.
        /// Defaults to AskFirst (safe) if:
        ///   - Tool not found in UIState
        ///   - UIState is null
        /// </summary>
        private ToolPolicy GetToolPolicy(string toolName)
        {
            if (_cachedUIState == null || _cachedUIState.ToolSettings == null)
            {
                System.Diagnostics.Debug.WriteLine($"[gap9-policy-default] No UIState cached; defaulting {toolName} to AskFirst");
                return ToolPolicy.AskFirst;
            }

            if (_cachedUIState.ToolSettings.TryGetValue(toolName, out var policy))
            {
                System.Diagnostics.Debug.WriteLine($"[gap9-policy-lookup] Tool {toolName} policy: {policy}");
                return policy;
            }

            System.Diagnostics.Debug.WriteLine($"[gap9-policy-missing] Tool {toolName} not in UIState; defaulting to AskFirst");
            return ToolPolicy.AskFirst;
        }

        /// <summary>
        /// Executes pending tool calls and creates Tool role messages with results.
        /// Gap23_3: Returns the number of failures in this batch for error accumulation.
        /// Tracks cumulative failures for loop termination logic (2+ failures = stop).
        /// Gap9: Respects tool policies (AutoApprove, AskFirst, Disabled) from UIState.
        /// </summary>
        private async Task<int> ExecuteToolCallsAsync(List<ToolCall> toolCalls)
        {
            int failureCount = 0;
            foreach (var toolCall in toolCalls)
            {
                // Get tool policy from cached UIState
                var policy = GetToolPolicy(toolCall.Name);

                // Apply policy logic
                if (policy == ToolPolicy.Disabled)
                {
                    System.Diagnostics.Debug.WriteLine($"[gap9-policy-apply] Tool {toolCall.Name} is DISABLED; skipping execution");

                    await SwitchToMainThreadAsync();
                    var disabledMessage = new ChatMessage
                    {
                        Role = ChatMessageRole.Tool,
                        Content = $"[Policy: Disabled] Tool '{toolCall.Name}' is disabled and cannot be executed.",
                        InvocationStatus = ToolInvocationStatus.Skipped,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    Messages.Add(disabledMessage);
                    await _sessionService.AddMessageAsync(disabledMessage);
                    continue;
                }

                if (policy == ToolPolicy.AskFirst)
                {
                    System.Diagnostics.Debug.WriteLine($"[gap9-policy-apply] Tool {toolCall.Name} requires approval (AskFirst); skipping for now");

                    // TODO: Show approval dialog for AskFirst tools
                    // For now, stub it with a message
                    await SwitchToMainThreadAsync();
                    var askFirstMessage = new ChatMessage
                    {
                        Role = ChatMessageRole.Tool,
                        Content = $"[Policy: AskFirst] Tool '{toolCall.Name}' requires your approval. Use Agent Mode with tool approval dialog to execute.",
                        InvocationStatus = ToolInvocationStatus.Skipped,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    Messages.Add(askFirstMessage);
                    await _sessionService.AddMessageAsync(askFirstMessage);
                    continue;
                }

                // AutoApprove: Execute immediately
                System.Diagnostics.Debug.WriteLine($"[gap9-policy-apply] Tool {toolCall.Name} is AutoApprove; executing immediately");

                var toolMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Content = $"[Executing: {toolCall.Name}]",
                    InvocationStatus = ToolInvocationStatus.Running,
                    ExecutionStartTime = DateTime.Now
                };
                await _sessionService.AddMessageAsync(toolMessage);

                // Switch to main thread to update ObservableCollection
                await SwitchToMainThreadAsync();
                Messages.Add(toolMessage);
                try
                {
                    var result = await _toolService.InvokeAsync(
                        toolCall.Name,
                        toolCall.Arguments ?? new Dictionary<string, object>(),
                        _streamingCts?.Token ?? CancellationToken.None);

                    toolMessage.Content = $"Tool '{toolCall.Name}' result: {result.Output}";
                    toolMessage.InvocationStatus = ToolInvocationStatus.Complete;
                    toolMessage.ExecutionEndTime = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"[gap9-exec] Tool '{toolCall.Name}' executed successfully. Result: {result.Output}");

                }
                catch (InvalidOperationException ex)
                {
                    // gap23_4_3: Tool call limit reached (gap23_4_3)
                    System.Diagnostics.Debug.WriteLine($"[gap23_4_3-limit-caught] {ex.Message}");
                    _limitReachedFlag = true;
                    await SwitchToMainThreadAsync();
                    _notificationService?.ShowError(ex.Message);
                    SendMessageCommand.RaiseCanExecuteChanged();
                    throw; // Stop tool execution loop when limit is hit
                }
                catch (Exception ex)
                {
                    toolMessage.Content = $"Tool '{toolCall.Name}' failed: {ex.Message}";
                    toolMessage.InvocationStatus = ToolInvocationStatus.Failed;
                    toolMessage.ExecutionEndTime = DateTime.Now;
                    failureCount++;

                    System.Diagnostics.Debug.WriteLine($"[gap9-exec] Tool '{toolCall.Name}' execution failed: {ex.Message}");
                }
            }
            return failureCount;
        }

        private bool CanSendMessage()
        {
            // gap23_4_3: Disable send while limit is active
            // gap23_4_4: Also disable when error banner is shown
            return !IsStreaming && !string.IsNullOrWhiteSpace(InputText) && !_limitReachedFlag && !ShowErrorBanner;
        }

        private void ExecuteCancel()
        {
            _streamingCts?.Cancel();
        }

#pragma warning disable VSTHRD100
        private async void ExecuteAddContext(string query)
#pragma warning restore VSTHRD100
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var items = await _contextService.GetContextItemsAsync(query, maxItems: 5);
                SelectedContext.Clear();
                foreach (var item in items)
                {
                    SelectedContext.Add(item);
                }
            }
            catch (Exception ex)
            {
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
        }

        /// <summary>
        /// Gets the system message prompt for the specified chat mode.
        /// Uses the SystemPromptService to load from config file, with fallback to defaults.
        /// </summary>
        private string GetSystemMessageForMode(ChatMode mode)
        {
            var modeKey = mode.ToString().ToLowerInvariant();
            return _systemPromptService.GetPromptForMode(modeKey);
        }

        /// <summary>
        /// Executes the delete message command.
        /// Removes the message from the collection and persists the deletion to the session service.
        /// </summary>
        private void ExecuteDeleteMessage(string messageId)
        {
            System.Diagnostics.Debug.WriteLine($"[delete-cmd] ExecuteDeleteMessage called with ID: {messageId}");

            if (string.IsNullOrWhiteSpace(messageId))
            {
                System.Diagnostics.Debug.WriteLine($"[delete-cmd] messageId is null/empty, aborting");
                return;
            }

            // Find and remove message from collection
            var messageToDelete = Messages.FirstOrDefault(m => m.Id == messageId);
            if (messageToDelete == null)
            {
                System.Diagnostics.Debug.WriteLine($"[delete-cmd] Message with ID {messageId} not found in collection. Available: {string.Join(",", Messages.Select(m => m.Id))}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[delete-cmd] Found message, removing from collection. Current count: {Messages.Count}");
            Messages.Remove(messageToDelete);
            System.Diagnostics.Debug.WriteLine($"[delete-cmd] Message removed. New count: {Messages.Count}");

            // Persist deletion asynchronously (fire-and-forget with error handling)
            _ = ExecuteDeleteMessageAsync(messageId, messageToDelete);
        }

        /// <summary>
        /// Asynchronously persists message deletion to the service.
        /// If deletion fails, restores the message to the collection and notifies the user.
        /// </summary>
        private async Task ExecuteDeleteMessageAsync(string messageId, ChatMessage messageToRestore)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[delete-service] Calling DeleteMessageAsync for ID: {messageId}");
                await _sessionService.DeleteMessageAsync(messageId);
                System.Diagnostics.Debug.WriteLine($"[delete-service] Successfully deleted message ID: {messageId}");
            }
            catch (Exception ex)
            {
                // If service deletion fails, add message back and notify user
                System.Diagnostics.Debug.WriteLine($"[delete-service] Delete failed, restoring message: {ex.Message}");
                Messages.Add(messageToRestore);
                await _notificationService.ShowNotificationAsync("Delete Failed", 
                    $"Could not delete message: {ex.Message}", NotificationType.Error);
                System.Diagnostics.Debug.WriteLine($"[delete-error] Service deletion failed: {ex.Message}");
            }
        }
    }
}
