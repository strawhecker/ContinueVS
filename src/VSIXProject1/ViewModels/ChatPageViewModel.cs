using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        private string? _inputText;
        private bool _isStreaming;
        private string? _streamingResponse;
        private CancellationTokenSource? _streamingCts;
        private ChatMode _currentMode = ChatMode.Ask;
        private ModelInfo? _selectedModel;
        private List<ToolCall> _pendingToolCalls = new List<ToolCall>();
        private const int MaxToolCallIterations = 10;
        private int _toolCallIterationCount = 0;

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
            ISystemPromptService systemPromptService)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));
            if (contextService == null) throw new ArgumentNullException(nameof(contextService));
            if (toolService == null) throw new ArgumentNullException(nameof(toolService));
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (systemPromptService == null) throw new ArgumentNullException(nameof(systemPromptService));

            _llmService = llmService;
            _contextService = contextService;
            _toolService = toolService;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _configService = configService;
            _systemPromptService = systemPromptService;

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

                    // Only execute tools in Agent mode
                    System.Diagnostics.Debug.WriteLine($"[a9-command-toolcheck] Checking tool execution: CurrentMode={CurrentMode}, _pendingToolCalls.Count={_pendingToolCalls.Count}, ShouldExecute={CurrentMode == ChatMode.Agent && _pendingToolCalls.Count > 0}");
                    if (CurrentMode == ChatMode.Agent && _pendingToolCalls.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[a9-command-toolexec] Executing tools in Agent mode");
                        await ExecuteToolCallsAsync(_pendingToolCalls);

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
        /// Executes pending tool calls and creates Tool role messages with results.
        /// </summary>
        private async Task ExecuteToolCallsAsync(List<ToolCall> toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
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
                catch (Exception ex)
                {
                    toolMessage.Content = $"Tool '{toolCall.Name}' failed: {ex.Message}";
                    toolMessage.InvocationStatus = ToolInvocationStatus.Failed;
                    toolMessage.ExecutionEndTime = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"[gap9-exec] Tool '{toolCall.Name}' execution failed: {ex.Message}");
                }
            }
        }

        private bool CanSendMessage()
        {
            return !IsStreaming && !string.IsNullOrWhiteSpace(InputText);
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
