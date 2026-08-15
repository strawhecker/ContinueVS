#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using ContinueVS.UI.Pages;

namespace ContinueVS.UI.Navigation
{
    public class PageNavigator : IPageNavigator
    {
        private static readonly Dictionary<string, Type> RouteMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "chat", typeof(ChatPage) },
            { "history", typeof(HistoryPage) }
        };

        public async Task NavigateAsync(string? route, Frame? frame)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[g7-nav-b6] PageNavigator.NavigateAsync called with route: {route}");

                if (frame == null)
                {
                    System.Diagnostics.Debug.WriteLine("[g7-nav-b7] PageNavigator: Frame is null, cannot navigate");
                    return;
                }

                if (string.IsNullOrWhiteSpace(route))
                {
                    System.Diagnostics.Debug.WriteLine("[g7-nav-b8] PageNavigator: Route is null or empty, ignoring navigation");
                    return;
                }

                if (!RouteMap.TryGetValue(route!, out var pageType))
                {
                    System.Diagnostics.Debug.WriteLine($"[g7-nav-b9] PageNavigator: Unknown route '{route}'");
                    return;
                }

                var page = Activator.CreateInstance(pageType) as Page;
                if (page != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[g7-nav-b10] PageNavigator: Navigating to {pageType.Name}");
                    frame.Navigate(page);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[g7-nav-b11] PageNavigator: Failed to create instance of {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[g7-nav-b12] PageNavigator: Navigation error for route '{route}': {ex.Message}");
            }

            await Task.CompletedTask;
        }
    }
}
