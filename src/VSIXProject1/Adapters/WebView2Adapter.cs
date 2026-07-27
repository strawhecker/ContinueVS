#nullable enable

using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ContinueVS.Adapters
{
    /// <summary>
    /// Mockable interface for CoreWebView2 to support unit testing.
    /// </summary>
    public interface IWebView2Adapter : IAsyncDisposable
    {
        /// <summary>
        /// Event fired when navigation completes.
        /// </summary>
        event EventHandler<INavigationCompletedEventArgs>? NavigationCompleted;

        /// <summary>
        /// Sets virtual host name to folder mapping for the WebView2.
        /// </summary>
        void SetVirtualHostNameToFolderMapping(
            string hostName,
            string folderPath,
            CoreWebView2HostResourceAccessKind accessKind);

        /// <summary>
        /// Executes script in the WebView2 context.
        /// </summary>
        Task<string> ExecuteScriptAsync(string script);
    }

    /// <summary>
    /// Mockable interface for navigation completed event arguments.
    /// </summary>
    public interface INavigationCompletedEventArgs
    {
        /// <summary>
        /// Gets whether the navigation was successful.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets the web error status if navigation failed.
        /// </summary>
        CoreWebView2WebErrorStatus WebErrorStatus { get; }
    }

    /// <summary>
    /// Production implementation of IWebView2Adapter that wraps CoreWebView2.
    /// </summary>
    public sealed class WebView2Adapter : IWebView2Adapter
    {
        private CoreWebView2? _coreWebView2;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of WebView2Adapter.
        /// </summary>
        /// <param name="coreWebView2">The underlying CoreWebView2 object.</param>
        public WebView2Adapter(CoreWebView2 coreWebView2)
        {
            if (coreWebView2 == null) throw new ArgumentNullException(nameof(coreWebView2));
            _coreWebView2 = coreWebView2;

            // Wire up the real NavigationCompleted event to our mockable interface
            _coreWebView2.NavigationCompleted += OnNavigationCompleted;
        }

        public event EventHandler<INavigationCompletedEventArgs>? NavigationCompleted;

        public void SetVirtualHostNameToFolderMapping(
            string hostName,
            string folderPath,
            CoreWebView2HostResourceAccessKind accessKind)
        {
            if (_disposed)
                throw new ObjectDisposedException("WebView2Adapter has been disposed");

            _coreWebView2?.SetVirtualHostNameToFolderMapping(hostName, folderPath, accessKind);
        }

        public async Task<string> ExecuteScriptAsync(string script)
        {
            if (_disposed)
                throw new ObjectDisposedException("WebView2Adapter has been disposed");

            if (_coreWebView2 == null)
                throw new ObjectDisposedException("CoreWebView2 is null");

            return await _coreWebView2.ExecuteScriptAsync(script);
        }

        private void OnNavigationCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs args)
        {
            var adaptedArgs = new NavigationCompletedEventArgsAdapter(args);
            NavigationCompleted?.Invoke(this, adaptedArgs);
        }

        /// <summary>
        /// Disposes the adapter and unsubscribes from all events.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                Debug.WriteLine("[b15-ADAPTER-DISPOSE] WebView2Adapter disposal starting");

                if (_coreWebView2 != null)
                {
                    Debug.WriteLine("[b15-UNSUBSCRIBE-NAV] Unsubscribing from NavigationCompleted event");
                    _coreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    _coreWebView2 = null;
                    Debug.WriteLine("[b15-ADAPTER-DISPOSE] CoreWebView2 reference cleared");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[b15-ADAPTER-DISPOSE-ERROR] Error during disposal: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Adapter for CoreWebView2NavigationCompletedEventArgs to support mocking.
    /// </summary>
    internal sealed class NavigationCompletedEventArgsAdapter : INavigationCompletedEventArgs
    {
        private readonly CoreWebView2NavigationCompletedEventArgs _args;

        public NavigationCompletedEventArgsAdapter(CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            _args = args;
        }

        public bool IsSuccess => _args.IsSuccess;

        public CoreWebView2WebErrorStatus WebErrorStatus => _args.WebErrorStatus;
    }
}
