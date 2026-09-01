#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.Services;
using ContinueVS.UI.Pages;

namespace ContinueVS.UI.Navigation
{
    public class PageNavigator : IPageNavigator
    {
        private static readonly Dictionary<string, Type> RouteMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "chat", typeof(ChatPage) },
            { "history", typeof(HistoryPage) },
            { "config", typeof(ConfigPage) }
        };

        public async Task NavigateAsync(string? route, Frame? frame)
        {
            try
            {
                _ = LoggerService.Current.WriteDebugAsync($"[g7-nav-b6] PageNavigator.NavigateAsync called with route: {route}");

                if (frame == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[g7-nav-b7] PageNavigator: Frame is null, cannot navigate");
                    return;
                }

                if (string.IsNullOrWhiteSpace(route))
                {
                    _ = LoggerService.Current.WriteDebugAsync("[g7-nav-b8] PageNavigator: Route is null or empty, ignoring navigation");
                    return;
                }

                if (!RouteMap.TryGetValue(route!, out var pageType))
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[g7-nav-b9] PageNavigator: Unknown route '{route}'");
                    return;
                }

                var instance = Activator.CreateInstance(pageType);
                if (instance == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[g7-nav-b11] PageNavigator: Failed to create instance of {pageType.Name}");
                    return;
                }

                _ = LoggerService.Current.WriteDebugAsync($"[g7-nav-b10] PageNavigator: Navigating to {pageType.Name}");

                // Support both Page and UserControl as page content
                if (instance is Page page)
                {
                    frame.Navigate(page);
                }
                else if (instance is UIElement element)
                {
                    frame.Navigate(element);
                }
                else
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[g7-nav-b11b] PageNavigator: {pageType.Name} is not a navigable UIElement");
                }
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[g7-nav-b12] PageNavigator: Navigation error for route '{route}': {ex.Message}", ex);
            }

            await Task.CompletedTask;
        }
    }
}
