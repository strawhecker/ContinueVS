using Microsoft.Web.WebView2.Core;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.UI
{
    /// <summary>
    /// Handles injection of the JavaScript bridge into WebView2 to enable
    /// bidirectional communication between C# handlers and the React GUI.
    /// </summary>
    internal interface IWebviewInjector
    {
        /// <summary>
        /// Injects the continueVS bridge JavaScript object into the WebView.
        /// 
        /// This must be called after CoreWebView2 is initialized and the virtual
        /// host mapping is configured, but before navigating to the HTML page.
        /// 
        /// The injected script creates:
        /// - window.continueVS.onMessage() for C# → React messaging
        /// - window.continueVS.sendMessage() for React → C# messaging
        /// - 'continueVSBridgeReady' event when bridge initialization completes
        /// </summary>
        /// <param name="coreWebView2">The CoreWebView2 instance to inject into (must not be null)</param>
        /// <param name="cancellationToken">Token to cancel the injection operation</param>
        /// <returns>
        /// Result containing injection status, error details, and timing information.
        /// Even if Success is false, the method completes without throwing.
        /// </returns>
        /// <remarks>
        /// This method is designed to be tolerant of transient issues and logs
        /// errors without propagating exceptions. Callers should check the
        /// WebviewInjectionResult.Success property to determine if the bridge
        /// is functional.
        /// </remarks>
        Task<WebviewInjectionResult> InjectBridgeAsync(
            CoreWebView2 coreWebView2,
            CancellationToken cancellationToken);
    }
}
