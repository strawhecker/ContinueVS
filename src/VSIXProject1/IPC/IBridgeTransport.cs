using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Event args for message received from the Continue bridge process.
    /// </summary>
    internal sealed class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The message received from the Continue process stdout.
        /// </summary>
        public Message Message { get; }

        public MessageReceivedEventArgs(Message message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }
    }

    /// <summary>
    /// Event args for errors occurring in the bridge transport.
    /// </summary>
    internal sealed class BridgeErrorEventArgs : EventArgs
    {
        /// <summary>
        /// The exception that occurred in the transport.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Indicates whether the error is fatal (transport cannot recover).
        /// </summary>
        public bool IsFatal { get; }

        public BridgeErrorEventArgs(Exception exception, bool isFatal = true)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            IsFatal = isFatal;
        }
    }

    /// <summary>
    /// Provides bidirectional communication with the Continue npm bridge process.
    /// 
    /// Handles:
    /// - Process lifecycle (start, stop, health checks)
    /// - Message serialization/deserialization (JSON-RPC framing via stdout/stdin)
    /// - Event-driven notifications (message received, errors, process closed)
    /// - Graceful shutdown with cancellation support
    /// 
    /// Implementations (e.g., StdioTransport) manage the Continue process subprocess,
    /// handle stdio buffering, and ensure JSON message integrity.
    /// </summary>
    internal interface IBridgeTransport : IAsyncDisposable
    {
        /// <summary>
        /// Gets a value indicating whether the bridge transport is currently connected to an active process.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Starts the Continue process and prepares the transport for communication.
        /// 
        /// This method:
        /// - Launches the Continue subprocess with the appropriate npm package version
        /// - Initializes stdio streams (stdin for sending, stdout for receiving)
        /// - Subscribes to process events (exit, error)
        /// - Validates that the process is accepting connections
        /// 
        /// Calling this method when <see cref="IsRunning"/> is true is idempotent and has no effect.
        /// </summary>
        /// <param name="cancellationToken">Allows cancellation of the startup operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the process fails to start or connection validation fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gracefully shuts down the bridge transport and terminates the Continue process.
        /// 
        /// This method:
        /// - Closes stdout/stdin streams
        /// - Sends a SIGTERM signal to the Continue process (if supported)
        /// - Waits for process termination (with timeout)
        /// - Unsubscribes from process events
        /// 
        /// Calling this method when <see cref="IsRunning"/> is false is idempotent and has no effect.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopAsync();

        /// <summary>
        /// Sends a message to the Continue process via stdin.
        /// 
        /// The message is serialized to JSON-RPC format and delimited with \r\n.
        /// Multiple concurrent sends are serialized to ensure message ordering.
        /// </summary>
        /// <param name="message">The message to send. Must not be null.</param>
        /// <param name="cancellationToken">Allows cancellation of the send operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if transport is not running.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        Task SendMessageAsync(Message message, CancellationToken cancellationToken);

        /// <summary>
        /// Receives the next message from the Continue process via stdout.
        /// 
        /// This method blocks until a complete JSON-RPC message is received and parsed.
        /// Messages are buffered and delivered in FIFO order.
        /// If the process closes, this method raises <see cref="OnClosed"/> and returns null.
        /// </summary>
        /// <param name="cancellationToken">Allows cancellation of the receive operation.</param>
        /// <returns>The next Message from the Continue process, or null if process closed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if transport is not running.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        Task<Message?> ReceiveMessageAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Raised when a complete message is received from the Continue process.
        /// 
        /// This event is fired on a background thread and should be handled quickly
        /// to avoid blocking message reception. Subscribers should delegate heavy work
        /// to background tasks or thread-safe queues.
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> OnMessageReceived;

        /// <summary>
        /// Raised when a non-fatal or fatal error occurs in the bridge transport.
        /// 
        /// Check <see cref="BridgeErrorEventArgs.IsFatal"/> to determine if the transport
        /// is still operational. Fatal errors typically result in automatic shutdown.
        /// 
        /// This event is fired on a background thread. Subscribers should handle errors
        /// quickly and avoid blocking I/O operations.
        /// </summary>
        event EventHandler<BridgeErrorEventArgs> OnError;

        /// <summary>
        /// Raised when the Continue process has terminated (whether gracefully or due to error).
        /// 
        /// After this event fires, <see cref="IsRunning"/> will return false and further
        /// message operations will fail. Callers should invoke <see cref="StartAsync"/>
        /// to reconnect if desired.
        /// 
        /// This event is fired on a background thread.
        /// </summary>
        event EventHandler OnClosed;
    }
}
