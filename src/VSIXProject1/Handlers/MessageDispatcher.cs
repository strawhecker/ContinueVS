using ContinueVS.Exceptions;
using ContinueVS.IPC;
using ContinueVS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers
{
    /// <summary>
    /// Routes incoming WebView messages to registered <see cref="IMessageHandler"/> implementations
    /// and manages the request/response lifecycle.
    /// 
    /// Responsibilities:
    /// - Register handlers by message type (case-insensitive)
    /// - Validate message envelopes (non-null, non-empty type, well-formed id)
    /// - Dispatch messages to appropriate handlers
    /// - Handle errors and exceptions during dispatch
    /// - Record metrics and lifecycle events
    /// - Support timeout enforcement via CancellationToken
    /// 
    /// Error Handling:
    /// - Handler not found (-32003): Log warning, raise BridgeMessageDispatcherException
    /// - Handler execution throws (-32603): Wrap with context, log error, raise exception
    /// - Message validation fails (-32004): Log error, raise exception
    /// 
    /// Dependencies (all optional; gracefully degrade if null):
    /// - IBridgeLogger: Logs registration, dispatch success/errors
    /// - IBridgeTelemetryCollector: Records handler execution timing and errors
    /// </summary>
    internal sealed class MessageDispatcher
    {
        private readonly Dictionary<string, IMessageHandler> _handlers =
            new Dictionary<string, IMessageHandler>(StringComparer.OrdinalIgnoreCase);

        private readonly IBridgeLogger? _logger;
        private readonly IBridgeTelemetryCollector? _telemetry;

        /// <summary>
        /// Initializes a new instance of MessageDispatcher with optional dependency injection.
        /// </summary>
        /// <param name="logger">Optional logger. If null, logging is silently skipped.</param>
        /// <param name="telemetry">Optional telemetry collector. If null, metrics are not recorded.</param>
        public MessageDispatcher(
            IBridgeLogger? logger = null,
            IBridgeTelemetryCollector? telemetry = null)
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        /// <summary>
        /// Registers a handler for the given message type.
        /// Case-insensitive message type matching.
        /// </summary>
        /// <param name="messageType">The message type (e.g., "bridge:getEditorState").</param>
        /// <param name="handler">The handler implementation.</param>
        /// <exception cref="ArgumentNullException">Thrown if messageType or handler is null.</exception>
        /// <exception cref="ArgumentException">Thrown if a handler is already registered for messageType.</exception>
        public void Register(string messageType, IMessageHandler handler)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_handlers.ContainsKey(messageType))
                throw new ArgumentException(
                    $"A handler is already registered for message type '{messageType}'.",
                    nameof(messageType));

            _handlers[messageType] = handler;

            // Log registration (fire-and-forget to avoid blocking)
            if (_logger != null)
            {
                _ = _logger.WriteDebugAsync(
                    $"Handler registered for message type: {messageType}",
                    new Dictionary<string, object> { { "messageType", messageType } });
            }
        }

        /// <summary>
        /// Dispatches a message to the handler registered for its message type.
        /// 
        /// Flow:
        /// 1. Validate message envelope (non-null, non-empty type, well-formed id)
        /// 2. Find handler in registry (case-insensitive)
        /// 3. If handler found:
        ///    - Invoke handler with cancellation token
        ///    - Record success metrics
        ///    - On handler exception: wrap, log, and raise BridgeMessageDispatcherException
        /// 4. If handler not found: log warning and raise BridgeMessageDispatcherException
        /// 
        /// Handler execution exceptions are wrapped in BridgeMessageDispatcherException
        /// with context (messageType, handlerName) for error recovery routing.
        /// </summary>
        /// <param name="message">The message to dispatch. Must not be null and must have non-empty MessageType.</param>
        /// <param name="cancellationToken">Cancellation token for the dispatch operation.</param>
        /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
        /// <exception cref="BridgeMessageDispatcherException">Thrown if validation fails, handler not found, or handler throws.</exception>
        public async Task DispatchAsync(Message message, CancellationToken cancellationToken)
        {
            // Validate message envelope
            ValidateMessage(message);

            // Find handler
            if (!_handlers.TryGetValue(message.MessageType, out var handler))
            {
                var context = new Dictionary<string, string>
                {
                    { "messageType", message.MessageType },
                    { "messageId", message.MessageId ?? "null" }
                };

                await LogWarningAsync(
                    $"No handler registered for message type: {message.MessageType}",
                    context);

                throw new BridgeMessageDispatcherException(
                    $"No handler registered for message type '{message.MessageType}'.",
                    BridgeMessageDispatcherException.OperationType.HandlerNotFound,
                    BridgeMessageDispatcherException.ErrorCodes.HandlerNotFound,
                    message.MessageType,
                    context);
            }

            // Invoke handler with error wrapping
            var sw = Stopwatch.StartNew();
            try
            {
                await handler.HandleAsync(message, cancellationToken);
                sw.Stop();

                // Record success metrics
                await RecordHandlerExecutionAsync(
                    message.MessageType,
                    sw.ElapsedMilliseconds,
                    success: true);
            }
            catch (OperationCanceledException)
            {
                // Timeout or explicit cancellation
                sw.Stop();

                await RecordHandlerExecutionAsync(
                    message.MessageType,
                    sw.ElapsedMilliseconds,
                    success: false,
                    errorType: "OperationCanceledException");

                throw new BridgeMessageDispatcherException(
                    $"Handler '{message.MessageType}' was cancelled (timeout or explicit cancellation).",
                    BridgeMessageDispatcherException.OperationType.TimeoutExceeded,
                    BridgeMessageDispatcherException.ErrorCodes.TimeoutExceeded,
                    message.MessageType,
                    handler.GetType().Name,
                    new Dictionary<string, string>
                    {
                        { "messageId", message.MessageId ?? "null" },
                        { "elapsedMs", sw.ElapsedMilliseconds.ToString() }
                    },
                    null);
            }
            catch (Exception ex)
            {
                // Handler execution error
                sw.Stop();

                await RecordHandlerExecutionAsync(
                    message.MessageType,
                    sw.ElapsedMilliseconds,
                    success: false,
                    errorType: ex.GetType().Name);

                var context = new Dictionary<string, string>
                {
                    { "messageType", message.MessageType },
                    { "messageId", message.MessageId ?? "null" },
                    { "handlerName", handler.GetType().Name },
                    { "elapsedMs", sw.ElapsedMilliseconds.ToString() }
                };

                await LogErrorAsync(
                    $"Handler '{message.MessageType}' threw exception: {ex.Message}",
                    ex,
                    context);

                throw new BridgeMessageDispatcherException(
                    $"Handler '{message.MessageType}' threw an exception: {ex.Message}",
                    BridgeMessageDispatcherException.OperationType.DispatchError,
                    BridgeMessageDispatcherException.ErrorCodes.DispatchError,
                    message.MessageType,
                    handler.GetType().Name,
                    context,
                    ex);
            }
        }

        /// <summary>
        /// Dispatches a message with a timeout constraint.
        /// 
        /// Creates a linked CancellationTokenSource with the timeout and passes it
        /// to DispatchAsync. If timeout expires, OperationCanceledException is raised
        /// and wrapped in BridgeMessageDispatcherException with TimeoutExceeded operation type.
        /// </summary>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="timeoutMs">Timeout in milliseconds. If ≤ 0, uses Timeout.Infinite (no timeout).</param>
        /// <param name="cancellationToken">External cancellation token to compose with timeout token.</param>
        /// <exception cref="BridgeMessageDispatcherException">Thrown on validation error, handler not found, handler error, or timeout.</exception>
        public async Task DispatchWithTimeoutAsync(
            Message message,
            long timeoutMs,
            CancellationToken cancellationToken = default)
        {
            if (timeoutMs <= 0)
            {
                await DispatchAsync(message, cancellationToken);
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            await DispatchAsync(message, cts.Token);
        }

        /// <summary>
        /// Validates message envelope for null, empty type, and well-formed id.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
        /// <exception cref="BridgeMessageDispatcherException">Thrown if type is empty or id is malformed.</exception>
        private void ValidateMessage(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(message.MessageType))
            {
                throw new BridgeMessageDispatcherException(
                    "Message type is null or empty.",
                    BridgeMessageDispatcherException.OperationType.ValidationFailed,
                    BridgeMessageDispatcherException.ErrorCodes.ValidationFailed,
                    null,
                    new Dictionary<string, string> { { "messageId", message.MessageId ?? "null" } });
            }

            // MessageId can be null for notifications, but must be valid string if present
            if (!string.IsNullOrEmpty(message.MessageId) && message.MessageId.Length > 256)
            {
                throw new BridgeMessageDispatcherException(
                    "Message id exceeds maximum length of 256 characters.",
                    BridgeMessageDispatcherException.OperationType.ValidationFailed,
                    BridgeMessageDispatcherException.ErrorCodes.ValidationFailed,
                    message.MessageType,
                    new Dictionary<string, string> { { "idLength", message.MessageId.Length.ToString() } });
            }
        }

        /// <summary>
        /// Records handler execution timing and status via telemetry collector.
        /// Fire-and-forget to avoid blocking dispatcher.
        /// </summary>
        private async Task RecordHandlerExecutionAsync(
            string messageType,
            long elapsedMs,
            bool success,
            string? errorType = null)
        {
            if (_telemetry == null)
                return;

            try
            {
                var metadata = new Dictionary<string, object>
                {
                    { "messageType", messageType },
                    { "success", success }
                };

                if (errorType != null)
                    metadata["errorType"] = errorType;

                await _telemetry.RecordHandlerExecutionAsync(messageType, elapsedMs, metadata);
            }
            catch
            {
                // Telemetry failures do not affect dispatch
            }
        }

        /// <summary>
        /// Logs a warning message via the logger (fire-and-forget).
        /// </summary>
        private async Task LogWarningAsync(string message, Dictionary<string, string> context)
        {
            if (_logger == null)
                return;

            try
            {
                var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in context)
                    metadata[kvp.Key] = kvp.Value;

                await _logger.WriteWarningAsync(message, metadata);
            }
            catch
            {
                // Logger failures do not affect dispatch
            }
        }

        /// <summary>
        /// Logs an error message via the logger (fire-and-forget).
        /// </summary>
        private async Task LogErrorAsync(
            string message,
            Exception ex,
            Dictionary<string, string> context)
        {
            if (_logger == null)
                return;

            try
            {
                var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in context)
                    metadata[kvp.Key] = kvp.Value;

                await _logger.WriteErrorAsync(message, ex, metadata);
            }
            catch
            {
                // Logger failures do not affect dispatch
            }
        }
    }
}
