using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Helper for running xUnit tests on STA (Single-Threaded Apartment) thread with WPF Dispatcher pump.
    /// 
    /// WPF and WebView2 components require both STA context AND a running message dispatcher.
    /// xUnit runs tests on MTA by default, which causes InvalidOperationException on Window/WebView2 instantiation.
    /// Even with STA thread, WebView2.EnsureCoreWebView2Async() requires the Dispatcher to pump messages.
    /// 
    /// Usage (async test with WebView2):
    ///   StaTestHelper.RunAsync(async () => {
    ///       var window = new Window();
    ///       await webView.EnsureCoreWebView2Async(env);
    ///   });
    /// 
    /// Usage (sync test):
    ///   StaTestHelper.Run(() => {
    ///       var window = new Window();
    ///   });
    /// </summary>
    public static class StaTestHelper
    {
        /// <summary>
        /// Runs synchronous test code on STA thread.
        /// </summary>
        /// <param name="action">Test code to execute on STA thread</param>
        public static void Run(Action action)
        {
            Exception caughtException = null;
            var done = new ManualResetEventSlim(false);

            var t = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
                finally
                {
                    done.Set();
                }
            })
            {
                IsBackground = true
            };

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            done.Wait();

            if (caughtException != null)
            {
                throw caughtException;
            }
        }

        /// <summary>
        /// Runs synchronous test code on STA thread and returns a value.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="func">Test code to execute on STA thread</param>
        /// <returns>Result from func</returns>
        public static T Run<T>(Func<T> func)
        {
            T result = default;
            Exception caughtException = null;
            var done = new ManualResetEventSlim(false);

            var t = new Thread(() =>
            {
                try
                {
                    result = func();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
                finally
                {
                    done.Set();
                }
            })
            {
                IsBackground = true
            };

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            done.Wait();

            if (caughtException != null)
            {
                throw caughtException;
            }

            return result;
        }

        /// <summary>
        /// Runs asynchronous test code on STA thread with WPF Dispatcher message pump.
        /// Enables async/await inside STA context for WebView2 integration tests.
        /// The Dispatcher message pump is necessary for WebView2.EnsureCoreWebView2Async() to work.
        /// </summary>
        /// <param name="func">Async test code to execute on STA thread</param>
        public static Task RunAsync(Func<Task> func)
        {
            Exception caughtException = null;
            var tcs = new TaskCompletionSource<bool>();

            var t = new Thread(() =>
            {
                try
                {
                    // Create and run the async function on the Dispatcher
#pragma warning disable VSTHRD001 // Avoid async void - we're manually managing the dispatcher
#pragma warning disable VSTHRD110 // Not awaiting is intentional; we use Dispatcher.Run() instead
                    Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
                    {
                        try
                        {
                            await func();
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            caughtException = ex;
                            tcs.TrySetException(ex);
                        }
                        finally
                        {
                            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                        }
                    });
#pragma warning restore VSTHRD110
#pragma warning restore VSTHRD001

                    Dispatcher.Run();

                    // If the task hasn't been completed yet, complete it now
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true
            };

            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            return tcs.Task;
        }

        /// <summary>
        /// Runs asynchronous test code on STA thread with WPF Dispatcher message pump and returns a value.
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="func">Async test code to execute on STA thread</param>
        /// <returns>Result from func</returns>
        public static Task<T> RunAsync<T>(Func<Task<T>> func)
        {
            T result = default;
            Exception caughtException = null;
            var tcs = new TaskCompletionSource<T>();

            var t = new Thread(() =>
            {
                try
                {
                    // Create and run the async function on the Dispatcher
#pragma warning disable VSTHRD001 // Avoid async void - we're manually managing the dispatcher
#pragma warning disable VSTHRD110 // Not awaiting is intentional; we use Dispatcher.Run() instead
                    Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
                    {
                        try
                        {
                            result = await func();
                            tcs.SetResult(result);
                        }
                        catch (Exception ex)
                        {
                            caughtException = ex;
                            tcs.TrySetException(ex);
                        }
                        finally
                        {
                            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                        }
                    });
#pragma warning restore VSTHRD110
#pragma warning restore VSTHRD001

                    Dispatcher.Run();

                    // If the task hasn't been completed yet, complete it now
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetResult(result);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true
            };

            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            return tcs.Task;
        }
    }
}
