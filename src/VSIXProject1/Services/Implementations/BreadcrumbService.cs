using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using System.Reflection;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Service for recording and querying breadcrumb trail of application events.
    /// Maintains an in-memory circular buffer of the last 20 events per session.
    /// Automatically masks sensitive data (API keys, passwords, tokens, etc.).
    /// </summary>
    public class BreadcrumbService : IBreadcrumbService, IDisposable
    {
        private readonly INotificationService _notificationService;
        private readonly ConcurrentQueue<BreadcrumbRecord> _breadcrumbs;
        private readonly string _sessionId;
        private readonly object _limitLock = new object();
        private const int MaxBreadcrumbs = 20;

        // Regex patterns for masking sensitive data (case-insensitive)
        private static readonly Regex ApiKeyPattern = new Regex(@"(?:api[_-]?key|apikey)\s*[:=]\s*[""']?[a-zA-Z0-9_\-]{10,}[""']?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PasswordPattern = new Regex(@"(?:password|passwd|pwd)\s*[:=]\s*[""']?[^""'\s]{3,}[""']?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TokenPattern = new Regex(@"(?:token|auth[_-]?token|bearer)\s*[:=]\s*[""']?[a-zA-Z0-9\-._~+/]+[""']?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SecretPattern = new Regex(@"(?:secret|private[_-]?key)\s*[:=]\s*[""']?[a-zA-Z0-9_\-./+=]{10,}[""']?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the BreadcrumbService class.
        /// </summary>
        /// <param name="notificationService">The notification service to hook into.</param>
        public BreadcrumbService(INotificationService notificationService)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _breadcrumbs = new ConcurrentQueue<BreadcrumbRecord>();
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);

            // Subscribe to notification events
            _notificationService.NotificationShown += OnNotificationShown;
        }

        /// <summary>
        /// Records a breadcrumb event with timestamp, level, and message.
        /// Automatically masks sensitive data.
        /// </summary>
        public async Task RecordBreadcrumbAsync(string message, BreadcrumbLevel level)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var maskedMessage = MaskSensitiveData(message);
            var breadcrumb = new BreadcrumbRecord(DateTime.UtcNow, level, maskedMessage, _sessionId);

            _breadcrumbs.Enqueue(breadcrumb);

            // Enforce 20-record limit
            lock (_limitLock)
            {
                while (_breadcrumbs.Count > MaxBreadcrumbs)
                {
                    _breadcrumbs.TryDequeue(out _);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves all recorded breadcrumbs up to the specified limit.
        /// </summary>
        public async Task<IReadOnlyList<BreadcrumbRecord>> GetBreadcrumbsAsync(int limit = 20)
        {
            if (limit <= 0)
                limit = 20;

            var records = _breadcrumbs.ToList();
            if (records.Count > limit)
            {
                records = records.Skip(records.Count - limit).ToList();
            }

            return await Task.FromResult(records.AsReadOnly());
        }

        /// <summary>
        /// Retrieves breadcrumbs filtered by severity level.
        /// </summary>
        public async Task<IReadOnlyList<BreadcrumbRecord>> GetBreadcrumbsByLevelAsync(BreadcrumbLevel level, int limit = 20)
        {
            if (limit <= 0)
                limit = 20;

            var records = _breadcrumbs
                .Where(b => b.Level == level)
                .ToList();

            if (records.Count > limit)
            {
                records = records.Skip(records.Count - limit).ToList();
            }

            return await Task.FromResult(records.AsReadOnly());
        }

        /// <summary>
        /// Clears all recorded breadcrumbs for the current session.
        /// </summary>
        public async Task ClearBreadcrumbsAsync()
        {
            lock (_limitLock)
            {
                while (_breadcrumbs.TryDequeue(out _)) { }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Event handler for INotificationService.NotificationShown.
        /// Automatically records breadcrumbs when notifications are displayed.
        /// </summary>
        private void OnNotificationShown(object? sender, NotificationEventArgs e)
        {
            if (e == null)
                return;

            var level = MapNotificationTypeToBreadcrumbLevel(e.Type);
            var message = string.IsNullOrEmpty(e.Title)
                ? (e.Message ?? "")
                : $"{e.Title}: {e.Message}";

            // Fire and forget with exception handling
            try
            {
                _ = RecordBreadcrumbAsync(message, level);
            }
            catch
            {
                // Silently ignore recording failures to avoid event handler crashes
            }
        }

        /// <summary>
        /// Maps NotificationType to BreadcrumbLevel.
        /// </summary>
        private static BreadcrumbLevel MapNotificationTypeToBreadcrumbLevel(NotificationType type)
        {
            return type switch
            {
                NotificationType.Information => BreadcrumbLevel.Info,
                NotificationType.Success => BreadcrumbLevel.Info,
                NotificationType.Warning => BreadcrumbLevel.Warning,
                NotificationType.Error => BreadcrumbLevel.Error,
                _ => BreadcrumbLevel.Info
            };
        }

        /// <summary>
        /// Masks sensitive data in the message using regex patterns.
        /// Replaces sensitive values with asterisks while preserving message structure.
        /// </summary>
        private static string MaskSensitiveData(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var masked = message;

            // Apply masking patterns
            masked = ApiKeyPattern.Replace(masked, m =>
            {
                var match = m.Value;
                var separator = match.IndexOf(':') >= 0 ? ':' : '=';
                var beforeSeparator = match.Substring(0, match.IndexOf(separator) + 1);
                return beforeSeparator + " ***MASKED***";
            });

            masked = PasswordPattern.Replace(masked, m =>
            {
                var match = m.Value;
                var separator = match.IndexOf(':') >= 0 ? ':' : '=';
                var beforeSeparator = match.Substring(0, match.IndexOf(separator) + 1);
                return beforeSeparator + " ***MASKED***";
            });

            masked = TokenPattern.Replace(masked, m =>
            {
                var match = m.Value;
                var separator = match.IndexOf(':') >= 0 ? ':' : '=';
                var beforeSeparator = match.Substring(0, match.IndexOf(separator) + 1);
                return beforeSeparator + " ***MASKED***";
            });

            masked = SecretPattern.Replace(masked, m =>
            {
                var match = m.Value;
                var separator = match.IndexOf(':') >= 0 ? ':' : '=';
                var beforeSeparator = match.Substring(0, match.IndexOf(separator) + 1);
                return beforeSeparator + " ***MASKED***";
            });

            return masked;
        }

        /// <summary>
        /// Disposes the breadcrumb service and unsubscribes from notification events.
        /// </summary>
        public void Dispose()
        {
            if (_notificationService != null)
            {
                _notificationService.NotificationShown -= OnNotificationShown;
            }
        }
    }
}
