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
            { "config", typeof(ConfigPage) }
        };

        public async Task NavigateAsync(string? route, Frame? frame)
        {
            try
            {
                LoggerService.Current.WriteDebug($"[g7-nav-b6] PageNavigator.NavigateAsync called with route: {route}");

                if (frame == null)
                {
                    LoggerService.Current.WriteDebug("[g7-nav-b7] PageNavigator: Frame is null, cannot navigate");
                    return;
                }

                if (string.IsNullOrWhiteSpace(route))
                {
                    LoggerService.Current.WriteDebug("[g7-nav-b8] PageNavigator: Route is null or empty, ignoring navigation");
                    return;
                }

                if (!RouteMap.TryGetValue(route!, out var pageType))
                {
                    LoggerService.Current.WriteDebug($"[g7-nav-b9] PageNavigator: Unknown route '{route}'");
                    return;
                }

                // If the requested route's page is already hosted, do NOT recreate it.
                // Re-creating on every tab-return discards the fully-rendered visual tree
                // (hundreds of markdown/code-block messages) and forces a full, costly redraw
                // for zero benefit. The existing page + ViewModel retains all its state.
                if (IsCurrentContent(frame, pageType))
                {
                    LoggerService.Current.WriteDebug($"[g7-nav-skip] PageNavigator: '{route}' already hosted ({pageType.Name}) — skipping recreation (no redraw).");
                    return;
                }

                var instance = Activator.CreateInstance(pageType);
                if (instance == null)
                {
                    LoggerService.Current.WriteDebug($"[g7-nav-b11] PageNavigator: Failed to create instance of {pageType.Name}");
                    return;
                }

                LoggerService.Current.WriteDebug($"[g7-nav-b10] PageNavigator: Navigating to {pageType.Name}");

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
                    LoggerService.Current.WriteDebug($"[g7-nav-b11b] PageNavigator: {pageType.Name} is not a navigable UIElement");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[g7-nav-b12] PageNavigator: Navigation error for route '{route}': {ex.Message}", ex);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Determines whether the given page type is already the current content of the frame.
        /// Used to avoid re-creating (and fully re-rendering) a page that is already hosted,
        /// which otherwise happens on every tab-return and discards all rendered state.
        /// </summary>
        private static bool IsCurrentContent(Frame? frame, Type pageType)
        {
            var content = frame?.Content;
            if (content == null)
                return false;

            // Frame.Content holds the exact page/control instance we created.
            if (content.GetType() == pageType)
                return true;

            // A navigated Page may be wrapped in a journal entry host; fall back to type name.
            return content.GetType().Name == pageType.Name;
        }
    }
}
