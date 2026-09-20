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

        // =====================================================================================
        // DO NOT REMOVE OR REPLACE THIS CACHE WITH A REMOVE-ON-UNLOAD / SINGLE-PAGE DESIGN.
        //
        // WHY THIS FIELD MUST EXIST (regression protection — read before "optimizing"):
        // -------------------------------------------------------------------------------------
        // Gap82's original fix (skip only when the requested page is *currently* mounted in the
        // Frame) was NOT sufficient. That guard only helped for VS tool-window tab switches
        // where the Frame content did not change. It silently BROKE for real in-tool-window
        // route changes (Chat -> Config -> Chat): the Frame content flips to ConfigPage, so when
        // the user returns to Chat the "IsCurrentContent" guard saw ConfigPage and decided to
        // recreate ChatPage from scratch. Recreating ChatPage also recreates its
        // ChatPageViewModel, which starts with an EMPTY Messages/DisplayMessages collection —
        // so the user's conversation vanished and had to be reloaded on every tab flip.
        //
        // THE RULE: The Frame is a *view* that only shows ONE page at a time. It cannot by
        // itself remember the pages it is NOT currently showing. To preserve live UI state
        // (messages, input text, scroll position) across route flips, we must RETAIN each
        // created page instance here, in this dictionary, for the lifetime of the navigator
        // (which is a singleton). Returning to a route reuses the retained instance — it is
        // then RE-MOUNTED into the Frame, not recreated, so the ViewModel/messages survive.
        //
        // DO NOT "SIMPLIFY" THIS INTO ONLY THE IsCurrentContent GUARD BELOW. Doing so makes
        // navigating Chat -> Config -> Chat reload/lose the conversation again — the exact bug
        // this was written to fix. If you ever think the cache is "unnecessary memory", cache
        // size is bounded by RouteMap (one instance per real route) and replacing it recreates
        // ChatPageViewModel with no Messages. That is strictly worse.
        // =====================================================================================
        private readonly Dictionary<string, object> _retainedPages = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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

                // CRITICAL (gap82 regression fix): Reuse a previously-created instance for this
                // route if one exists, instead of always `Activator.CreateInstance`.
                // Without this, returning to Chat after visiting Config created a brand-new
                // ChatPage + ChatPageViewModel (empty Messages) — losing the conversation.
                // The retained instance keeps its live ViewModel/messages; we just re-mount it.
                object? instance;
                if (_retainedPages.TryGetValue(route!, out var retained) && retained != null)
                {
                    instance = retained;
                    LoggerService.Current.WriteDebug($"[g7-nav-reuse] PageNavigator: reusing retained {pageType.Name} for route '{route}' (preserves ViewModel state, no redraw).");
                }
                else
                {
                    instance = Activator.CreateInstance(pageType);
                    if (instance == null)
                    {
                        LoggerService.Current.WriteDebug($"[g7-nav-b11] PageNavigator: Failed to create instance of {pageType.Name}");
                        return;
                    }
                    // Retain the first-created instance for this route so future navigations
                    // reuse it (see the large comment atop the class — do not remove).
                    _retainedPages[route!] = instance;
                    LoggerService.Current.WriteDebug($"[g7-nav-b10] PageNavigator: Navigating to {pageType.Name} (creating + retaining instance).");
                }

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
        ///
        /// NOTE: This alone is NOT sufficient to preserve state across route changes
        /// (Chat -> Config -> Chat). See the retained-pages cache above — the cache is the
        /// real fix; this guard is only a fast-path no-op for the currently-hosted page.
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
