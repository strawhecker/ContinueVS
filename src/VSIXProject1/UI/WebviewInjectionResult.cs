using System;

namespace ContinueVS.UI
{
    /// <summary>
    /// Result of attempting to inject the webview bridge.
    /// 
    /// This type captures success/failure information, error context, and timing
    /// to assist in debugging injection issues and monitoring bridge initialization.
    /// </summary>
    internal sealed class WebviewInjectionResult
    {
        /// <summary>
        /// Gets whether the injection completed successfully.
        /// </summary>
        /// <remarks>
        /// This property should be checked before assuming the bridge is available.
        /// Even if false, the bridge object may be partially initialized depending
        /// on when the error occurred.
        /// </remarks>
        public bool Success { get; }

        /// <summary>
        /// Gets the error message if injection failed, or null if successful.
        /// </summary>
        /// <remarks>
        /// This message is suitable for logging but should not be displayed
        /// directly to users (may contain internal implementation details).
        /// </remarks>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the timestamp when injection was attempted.
        /// </summary>
        /// <remarks>
        /// Useful for correlating with other system events and measuring
        /// time between initialization steps.
        /// </remarks>
        public DateTime InjectionTime { get; }

        /// <summary>
        /// Gets the JavaScript injection script that was executed.
        /// </summary>
        /// <remarks>
        /// Provided for debugging and logging purposes. Populated regardless of
        /// success or failure to help troubleshoot script issues.
        /// </remarks>
        public string InjectionScript { get; }

        /// <summary>
        /// Gets the exception that occurred during injection, if any.
        /// </summary>
        /// <remarks>
        /// Null if the operation was successful or failed gracefully without
        /// throwing. Useful for detailed error analysis and support.
        /// </remarks>
        public Exception? Exception { get; }

        public WebviewInjectionResult(
            bool success,
            string? errorMessage = null,
            string injectionScript = "",
            Exception? exception = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            InjectionTime = DateTime.UtcNow;
            InjectionScript = injectionScript;
            Exception = exception;
        }

        /// <summary>
        /// Creates a successful injection result.
        /// </summary>
        public static WebviewInjectionResult CreateSuccess(string injectionScript) =>
            new(success: true, injectionScript: injectionScript);

        /// <summary>
        /// Creates a failed injection result.
        /// </summary>
        public static WebviewInjectionResult CreateFailure(
            string errorMessage,
            string injectionScript = "",
            Exception? exception = null) =>
            new(success: false, errorMessage, injectionScript, exception);
    }
}
