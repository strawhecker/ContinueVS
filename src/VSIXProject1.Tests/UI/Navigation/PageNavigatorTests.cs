#nullable enable

using System;
using Xunit;
using System.Threading;
using ContinueVS.UI.Navigation;

namespace ContinueVS.Tests.UI.Navigation
{
    public class PageNavigatorTests
    {
        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithValidChatRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("chat", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithValidConfigRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("config", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithSettingsAlias_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("settings", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithValidHistoryRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("history", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithValidStatsRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("stats", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithValidEditModeRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("editmode", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithNullFrame_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("chat", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithNullRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync(null, null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithEmptyRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync(string.Empty, null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithUnknownRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("unknown-route", null);
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task NavigateAsync_WithCaseInsensitiveRoute_DoesNotThrow()
        {
            await RunOnSTAThread(async () =>
            {
                // Arrange
                var navigator = new PageNavigator();

                // Act & Assert (should not throw)
                await navigator.NavigateAsync("CHAT", null);
            });
        }

        private static async System.Threading.Tasks.Task RunOnSTAThread(Func<System.Threading.Tasks.Task> testAction)
        {
            System.Threading.Tasks.TaskCompletionSource<bool> tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    testAction().Wait();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            await tcs.Task;
        }
    }
}
