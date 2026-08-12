using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Utilities;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    public class ChatPageViewModel : ViewModelBase
    {
        private readonly ILlmService _llmService;
        private readonly IContextService _contextService;
        private readonly IToolService _toolService;
        private readonly ISessionService _sessionService;
        private readonly INotificationService _notificationService;

        private string? _inputText;
        private bool _isStreaming;
        private string? _streamingResponse;
        private CancellationTokenSource? _streamingCts;

        public ObservableCollection<ChatMessage> Messages { get; }
        public ObservableCollection<ContextItem> SelectedContext { get; }

        public string? InputText
        {
            get => _inputText;
            set => Set(ref _inputText, value);
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set => Set(ref _isStreaming, value);
        }

        public string? StreamingResponse
        {
            get => _streamingResponse;
            set => Set(ref _streamingResponse, value);
        }

        public RelayCommand SendMessageCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand<string> AddContextCommand { get; }

        public ChatPageViewModel(
            ILlmService llmService,
            IContextService contextService,
            IToolService toolService,
            ISessionService sessionService,
            INotificationService notificationService)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));
            if (contextService == null) throw new ArgumentNullException(nameof(contextService));
            if (toolService == null) throw new ArgumentNullException(nameof(toolService));
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));

            _llmService = llmService;
            _contextService = contextService;
            _toolService = toolService;
            _sessionService = sessionService;
            _notificationService = notificationService;

            Messages = new ObservableCollection<ChatMessage>();
            SelectedContext = new ObservableCollection<ContextItem>();
            _inputText = string.Empty;
            _streamingResponse = string.Empty;

            SendMessageCommand = new RelayCommand(ExecuteSendMessage, CanSendMessage);
            CancelCommand = new RelayCommand(ExecuteCancel, () => IsStreaming);
            AddContextCommand = new RelayCommand<string>(ExecuteAddContext);
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

                StreamingResponse = string.Empty;

                var messages = new List<ChatMessage> { userMessage };
                if (SelectedContext.Count > 0)
                {
                    var contextSummary = string.Join("\n", 
                        SelectedContext.Select(c => c.FilePath + ": " + c.Content));
                    messages.Insert(0, new ChatMessage
                    {
                        Role = ChatMessageRole.System,
                        Content = "Context:\n" + contextSummary
                    });
                }

                await RetryPolicyHelper.ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await foreach (var chunk in _llmService.StreamAsync(messages, ct: ct))
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

                InputText = string.Empty;
            }
            catch (OperationCanceledException)
            {
                StreamingResponse += "\n[Cancelled by user]";
            }
            catch (Exception ex)
            {
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
    }
}
