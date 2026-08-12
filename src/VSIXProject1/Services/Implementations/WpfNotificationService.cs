#nullable enable

using System;
using System.Threading.Tasks;
using System.Windows;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// WPF implementation of INotificationService for displaying notifications and dialogs to the user.
    /// Uses System.Windows.MessageBox for simple notifications and custom WPF windows for progress and input dialogs.
    /// </summary>
    public class WpfNotificationService : INotificationService
    {
        public event EventHandler<NotificationEventArgs>? NotificationShown;

        /// <summary>
        /// Shows a notification to the user using a MessageBox.
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

            var icon = type switch
            {
                NotificationType.Information => MessageBoxImage.Information,
                NotificationType.Warning => MessageBoxImage.Warning,
                NotificationType.Error => MessageBoxImage.Error,
                NotificationType.Success => MessageBoxImage.Information,
                _ => MessageBoxImage.None
            };

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);

            // Fire the NotificationShown event
            RaiseNotificationShown(title, message, type);
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

            var progressWindow = new UI.ProgressWindow { Title = title };
            var progress = new Progress<int>(value =>
            {
                progressWindow.ReportProgress(value);
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
                        progressWindow.Close();
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

            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
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

            var inputWindow = new UI.InputWindow
            {
                Title = title,
                Prompt = prompt,
                Input = defaultValue
            };

            var result = inputWindow.ShowDialog();
            return result == true ? inputWindow.Input : null;
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
    }
}
