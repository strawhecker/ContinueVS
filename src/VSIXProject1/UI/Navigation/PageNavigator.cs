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
                if (frame == null)
                {
                    System.Diagnostics.Debug.WriteLine("PageNavigator: Frame is null, cannot navigate");
                    return;
                }

                if (string.IsNullOrWhiteSpace(route))
                {
                    System.Diagnostics.Debug.WriteLine("PageNavigator: Route is null or empty, ignoring navigation");
                    return;
                }

                if (!RouteMap.TryGetValue(route!, out var pageType))
                {
                    System.Diagnostics.Debug.WriteLine($"PageNavigator: Unknown route '{route}'");
                    return;
                }

                var page = Activator.CreateInstance(pageType) as Page;
                if (page != null)
                {
                    frame.Navigate(page);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"PageNavigator: Failed to create instance of {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PageNavigator: Navigation error for route '{route}': {ex.Message}");
            }

            await Task.CompletedTask;
        }
    }
}
