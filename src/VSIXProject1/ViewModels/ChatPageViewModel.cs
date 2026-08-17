using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private string? _inputText;
        private bool _isStreaming;
        private string? _streamingResponse;
        private CancellationTokenSource? _streamingCts;
        private ChatMode _currentMode = ChatMode.Ask;
        private ModelInfo? _selectedModel;

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
                if (Set(ref _currentMode, value))
                {
                    SendMessageCommand.RaiseCanExecuteChanged();
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

        public ChatPageViewModel(
            ILlmService llmService,
            IContextService contextService,
            IToolService toolService,
            ISessionService sessionService,
            INotificationService notificationService,
            IConfigService configService)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));
            if (contextService == null) throw new ArgumentNullException(nameof(contextService));
            if (toolService == null) throw new ArgumentNullException(nameof(toolService));
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));
            if (configService == null) throw new ArgumentNullException(nameof(configService));

            _llmService = llmService;
            _contextService = contextService;
            _toolService = toolService;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _configService = configService;

            Messages = new ObservableCollection<ChatMessage>();
            SelectedContext = new ObservableCollection<ContextItem>();
            AvailableModels = new ObservableCollection<ModelInfo>();
            _inputText = string.Empty;
            _streamingResponse = string.Empty;

            SendMessageCommand = new RelayCommand(ExecuteSendMessage, CanSendMessage);
            CancelCommand = new RelayCommand(ExecuteCancel, () => IsStreaming);
            AddContextCommand = new RelayCommand<string>(ExecuteAddContext);
            SetModeCommand = new RelayCommand<ChatMode>(mode => CurrentMode = mode);

            _ = LoadModelsAsync();
            _configService.ConfigChanged += ConfigService_ConfigChanged;
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

        var userMessage = new ChatMessage
        {
            Role = ChatMessageRole.User,
            Content = InputText ?? string.Empty
        };
        await _sessionService.AddMessageAsync(userMessage);
        Messages.Add(userMessage);
        System.Diagnostics.Debug.WriteLine($"[a6-exec] ExecuteSendMessage: User message added. Role={userMessage.Role}, Content={userMessage.Content}, MessagesCount={Messages.Count}");

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

        var streamOptions = new StreamOptions
        {
            Messages = messages
        };

        await RetryPolicyHelper.ExecuteWithRetryAsync(
            async ct =>
            {
                await foreach (var chunk in _llmService.StreamAsync(messages, streamOptions, ct))
                {
                    if (chunk.Type == ChunkType.Text)
                            {
                                StreamingResponse += chunk.Content;
                            }
                        }
                    },
                    _streamingCts.Token,
                    maxRetries: 3);

                var assistantMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = StreamingResponse
                };
                await _sessionService.AddMessageAsync(assistantMessage);
                Messages.Add(assistantMessage);
                System.Diagnostics.Debug.WriteLine($"[a6-exec] ExecuteSendMessage: Assistant message added. Role={assistantMessage.Role}, Content length={assistantMessage.Content.Length}, MessagesCount={Messages.Count}");

                InputText = string.Empty;
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

                // Log inner exception if present
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
        /// </summary>
        private string GetSystemMessageForMode(ChatMode mode)
        {
            return mode switch
            {
                ChatMode.Ask => ChatModeSystemPrompts.DEFAULT_ASK_SYSTEM_MESSAGE,
                ChatMode.Agent => ChatModeSystemPrompts.DEFAULT_AGENT_SYSTEM_MESSAGE,
                ChatMode.Plan => ChatModeSystemPrompts.DEFAULT_PLAN_SYSTEM_MESSAGE,
                _ => ChatModeSystemPrompts.DEFAULT_ASK_SYSTEM_MESSAGE
            };
        }
    }
}
