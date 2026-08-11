using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Enumeration of notification types.
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// Information notification.
        /// </summary>
        Information,

        /// <summary>
        /// Warning notification.
        /// </summary>
        Warning,

        /// <summary>
        /// Error notification.
        /// </summary>
        Error,

        /// <summary>
        /// Success notification.
        /// </summary>
        Success
    }

    /// <summary>
    /// Event arguments for notifications.
    /// </summary>
    public class NotificationEventArgs : EventArgs
    {
        /// <summary>
        /// Title of the notification.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Message content of the notification.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Type of notification.
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// Timestamp when the notification was shown.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
