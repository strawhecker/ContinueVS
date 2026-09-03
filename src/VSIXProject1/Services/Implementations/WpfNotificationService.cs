#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using ContinueVS.UI.Views;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// WPF implementation of INotificationService for displaying notifications and dialogs to the user.
    /// Displays notifications as System-role messages in the chat interface with auto-dismiss.
    /// </summary>
    public class WpfNotificationService : INotificationService
    {
        private readonly IBridgeLogger? _logger;
        private readonly Func<MainViewModel?>? _getViewModel;
        private readonly Func<ChatPageViewModel?>? _getChatPageViewModel;
        private readonly int _notificationDurationMs;

        public event EventHandler<NotificationEventArgs>? NotificationShown;

        /// <summary>
        /// Initializes a new instance of WpfNotificationService.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="viewModel">Deprecated: kept for backward compatibility. Use getViewModel parameter instead.</param>
        /// <param name="getViewModel">Factory function to lazily retrieve the MainViewModel.</param>
        /// <param name="getChatPageViewModel">Factory function to lazily retrieve the ChatPageViewModel for chat-based notifications.</param>
        /// <param name="notificationDurationMs">Duration in milliseconds before notification auto-dismisses. Default: 7000 (7 seconds).</param>
        public WpfNotificationService(IBridgeLogger? logger = null, MainViewModel? viewModel = null, Func<MainViewModel?>? getViewModel = null, Func<ChatPageViewModel?>? getChatPageViewModel = null, int notificationDurationMs = 7000)
        {
            _logger = logger;
            _getViewModel = getViewModel;
            _getChatPageViewModel = getChatPageViewModel;
            _notificationDurationMs = notificationDurationMs;

            // If no getViewModel factory is provided but a viewModel instance is, create a simple factory
            if (_getViewModel == null && viewModel != null)
            {
                _getViewModel = () => viewModel;
            }
        }

        /// <summary>
        /// Shows a notification to the user as a System-role message in the chat interface.
        /// The notification appears as a dismissable system message and auto-dismisses after the configured duration.
        /// Falls back to chat dialog or logging if ChatPageViewModel is unavailable.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content.</param>
        /// <param name="type">The type of notification (Info, Warning, Error, Success).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ShowNotificationAsync(string title, string message, NotificationType type)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentNullException(nameof(title), "Notification title cannot be null or empty.");
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message), "Notification message cannot be null or empty.");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var chatViewModel = _getChatPageViewModel?.Invoke();
            if (chatViewModel != null)
            {
                // Primary path: Add as System message to chat and schedule auto-dismiss
                await AddNotificationToChatAsync(chatViewModel, title, message, type);
            }
            else
            {
                // Fallback: Try TextDialog via MainViewModel
                var mainViewModel = _getViewModel?.Invoke();
                if (mainViewModel != null)
                {
                    try
                    {
                        var dialog = new TextDialog();
                        dialog.Initialize(TextDialog.DialogType.Text, $"{title}: {message}");
                        mainViewModel.ShowDialog(dialog);
                        await dialog.GetResultAsync();
                        mainViewModel.HideDialog();
                    }
                    catch (Exception fallbackEx)
                    {
                        _ = LoggerService.Current.WriteDebugAsync($"[WpfNotificationService] Fallback TextDialog failed: {fallbackEx.GetType().Name}: {fallbackEx.Message}");
                    }
                }
                else
                {
                    // Final fallback: Log only
                    _ = LoggerService.Current.WriteDebugAsync($"[WpfNotificationService] No ViewModel available. Notification: [{type}] {title}: {message}");
                }
            }

            // Fire the NotificationShown event for telemetry
            RaiseNotificationShown(title, message, type);
        }

        /// <summary>
        /// Adds a notification to the chat as a System-role message with auto-dismiss timer.
        /// </summary>
        private async Task AddNotificationToChatAsync(ChatPageViewModel viewModel, string title, string message, NotificationType type)
        {
            try
            {
                // Format: "[Type] Title: Message" (e.g., "[Error] Delete Failed: Could not delete message: ...")
                var formattedContent = $"[{type}] {title}: {message}";

                var notificationMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = ChatMessageRole.System,
                    Content = formattedContent,
                    Timestamp = DateTime.UtcNow
                };

                // Add to messages collection
                viewModel.Messages.Add(notificationMessage);

                // Schedule auto-dismiss after configured duration
                _ = Task.Delay(_notificationDurationMs).ContinueWith(_ =>
                {
                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        try
                        {
                            viewModel.Messages.Remove(notificationMessage);
                        }
                        catch
                        {
                            // Silently ignore if already removed or collection disposed
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[WpfNotificationService] Failed to add notification to chat: {ex.GetType().Name}: {ex.Message}");
            }
        }


        /// <summary>
        /// Shows a progress dialog or indicator.
        /// </summary>
        /// <param name="title">The title of the progress dialog.</param>
        /// <param name="workAction">The action that reports progress.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ShowProgressAsync(string title, Action<IProgress<int>> workAction)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentNullException(nameof(title), "Progress title cannot be null or empty.");
            if (workAction == null)
                throw new ArgumentNullException(nameof(workAction), "Work action cannot be null.");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var viewModel = _getViewModel?.Invoke();
            if (viewModel == null)
                throw new InvalidOperationException("MainViewModel must be provided to display progress.");

            var dialog = new TextDialog();
            dialog.Initialize(TextDialog.DialogType.Progress, title);
            viewModel.ShowDialog(dialog);

            var progress = new Progress<int>(value =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    dialog.ReportProgress(value);
                });
            });

            await Task.Run(() =>
            {
                try
                {
                    workAction(progress);
                }
                finally
                {
                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        dialog.CompleteProgress();
                        viewModel.HideDialog();
                    });
                }
            });
        }

        /// <summary>
        /// Shows a confirmation dialog.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The confirmation message.</param>
        /// <returns>True if the user confirmed, false otherwise.</returns>
        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentNullException(nameof(title), "Confirmation title cannot be null or empty.");
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message), "Confirmation message cannot be null or empty.");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // If ViewModel is available, use TextDialog overlay; otherwise fall back to MessageBox
            var viewModel = _getViewModel?.Invoke();
            if (viewModel != null)
            {
                var dialog = new TextDialog();
                dialog.Initialize(TextDialog.DialogType.Confirmation, message);
                viewModel.ShowDialog(dialog);

                var result = await dialog.GetResultAsync();
                viewModel.HideDialog();

                return result?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? false;
            }
            else
            {
                var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                return result == MessageBoxResult.Yes;
            }
        }

        /// <summary>
        /// Shows an input dialog for user text entry.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="prompt">The prompt text.</param>
        /// <param name="defaultValue">The default value for the input.</param>
        /// <returns>The user's input, or null if cancelled.</returns>
        public async Task<string?> ShowInputAsync(string title, string prompt, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(title))
                throw new ArgumentNullException(nameof(title), "Input dialog title cannot be null or empty.");
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentNullException(nameof(prompt), "Input prompt cannot be null or empty.");

            defaultValue ??= "";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Use TextDialog overlay (no fallback to InputWindow)
            var viewModel = _getViewModel?.Invoke();
            if (viewModel == null)
                throw new InvalidOperationException("MainViewModel must be provided to display input dialogs.");

            var dialog = new TextDialog();
            dialog.Initialize(TextDialog.DialogType.Text, prompt, defaultValue);
            viewModel.ShowDialog(dialog);

            var result = await dialog.GetResultAsync();
            viewModel.HideDialog();

            return result;
        }

        /// <summary>
        /// Raises the NotificationShown event.
        /// </summary>
        private void RaiseNotificationShown(string title, string message, NotificationType type)
        {
            NotificationShown?.Invoke(this, new NotificationEventArgs
            {
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Shows an error notification to the user (gap23_4_3).
        /// </summary>
        /// <param name="message">The error message to display.</param>
        public async Task ShowErrorAsync(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message), "Error message cannot be null or empty.");

            await ShowNotificationAsync("Error", message, NotificationType.Error);
        }

        /// <summary>
        /// Shows an error notification synchronously (for backward compatibility).
        /// Uses fire-and-forget pattern with logging.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        public void ShowError(string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message), "Error message cannot be null or empty.");

            // Fire-and-forget with explicit task tracking
            _ = LoggerService.Current.WriteDebugAsync("[WpfNotificationService] ShowError called; async ShowErrorAsync dispatched");
            _ = ShowErrorAsync(message);
        }
    }
}
