using System;

namespace ContinueVS.UI
{
    /// <summary>
    /// Exception thrown when webview bridge injection fails in an unrecoverable way.
    /// </summary>
    /// <remarks>
    /// This exception is typically thrown by WebviewInjector when the CoreWebView2
    /// is in an invalid state or when the injection script is malformed. Callers
    /// may catch this exception to implement custom recovery logic.
    /// </remarks>
    internal sealed class WebviewInjectionException : Exception
    {
        /// <summary>
        /// Gets the JavaScript code that failed to execute.
        /// </summary>
        public string? FailedScript { get; }

        public WebviewInjectionException(string message)
            : base(message)
        {
            FailedScript = null;
        }

        public WebviewInjectionException(string message, Exception innerException)
            : base(message, innerException)
        {
            FailedScript = null;
        }

        public WebviewInjectionException(string message, string? failedScript, Exception? innerException = null)
            : base(message, innerException)
        {
            FailedScript = failedScript;
        }
    }
}
