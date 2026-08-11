using System;
using System.Threading.Tasks;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for displaying notifications to the user.
    /// Supports toasts, dialogs, and progress indicators.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows a notification to the user.
        /// </summary>
        /// <param name="title">The title of the notification.</param>
        /// <param name="message">The message content.</param>
        /// <param name="type">The type of notification (Info, Warning, Error, Success).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowNotificationAsync(string title, string message, NotificationType type);

        /// <summary>
        /// Shows a progress dialog or indicator.
        /// </summary>
        /// <param name="title">The title of the progress dialog.</param>
        /// <param name="workAction">The action that reports progress.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ShowProgressAsync(string title, Action<IProgress<int>> workAction);

        /// <summary>
        /// Shows a confirmation dialog.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The confirmation message.</param>
        /// <returns>True if the user confirmed, false otherwise.</returns>
        Task<bool> ShowConfirmationAsync(string title, string message);

        /// <summary>
        /// Shows an input dialog for user text entry.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="prompt">The prompt text.</param>
        /// <param name="defaultValue">The default value for the input.</param>
        /// <returns>The user's input, or null if cancelled.</returns>
        Task<string?> ShowInputAsync(string title, string prompt, string defaultValue = "");

        /// <summary>
        /// Event raised when a notification is shown.
        /// </summary>
        event EventHandler<NotificationEventArgs>? NotificationShown;
    }
}
