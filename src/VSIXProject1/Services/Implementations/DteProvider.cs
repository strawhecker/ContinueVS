using EnvDTE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Real implementation of IDteProvider that wraps the Visual Studio DTE object.
    /// </summary>
    public class DteProvider : IDteProvider
    {
        private readonly DTE _dte;

        public DteProvider(DTE dte)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
        }

        public string GetSelectedText()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var activeDoc = _dte.ActiveDocument;
                if (activeDoc == null)
                    return string.Empty;

                var selection = activeDoc.Selection;
                if (selection is TextSelection textSelection)
                {
                    return textSelection.Text ?? string.Empty;
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetActiveDocumentContent()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var activeDoc = _dte.ActiveDocument;
                if (activeDoc == null)
                    return string.Empty;

#pragma warning disable CS8974
                object? docObjValue = activeDoc.Object;
#pragma warning restore CS8974
                var textDoc = docObjValue as TextDocument;
                if (textDoc != null)
                {
                    return textDoc.StartPoint.CreateEditPoint().GetText(textDoc.EndPoint) ?? string.Empty;
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public List<string> GetRecentFiles(int maxCount)
        {
            var recentFiles = new List<string>();
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                foreach (Document doc in _dte.Documents)
                {
                    if (recentFiles.Count >= maxCount)
                        break;
                    if (!string.IsNullOrEmpty(doc.FullName))
                    {
                        recentFiles.Add(doc.FullName);
                    }
                }
            }
            catch
            {
                // Silently fail and return empty list
            }
            return recentFiles;
        }

        public List<string> GetOpenDocumentPaths()
        {
            var openDocs = new List<string>();
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                foreach (Document doc in _dte.Documents)
                {
                    if (!string.IsNullOrEmpty(doc.FullName))
                    {
                        openDocs.Add(doc.FullName);
                    }
                }
            }
            catch
            {
                // Best-effort: on failure return what we have (empty). Callers treat an empty
                // result as "binding status unknown" rather than silently clearing.
            }
            return openDocs;
        }

        public string GetActiveFilepath()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var activeDoc = _dte.ActiveDocument;
                if (activeDoc != null)
                    return activeDoc.FullName ?? string.Empty;

                // Tool window has focus — ActiveDocument is null. Try the active window's document first.
                var winDoc = _dte.ActiveWindow?.Document;
                if (winDoc != null && !string.IsNullOrWhiteSpace(winDoc.FullName))
                    return winDoc.FullName;

                // Fall back to the first open document in the Documents collection.
                var docs = _dte.Documents;
                if (docs != null && docs.Count > 0)
                {
                    var first = docs.Item(1);
                    if (first != null && !string.IsNullOrWhiteSpace(first.FullName))
                        return first.FullName;
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetSolutionDirectory()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var solutionPath = _dte.Solution?.FullName;
                if (string.IsNullOrWhiteSpace(solutionPath))
                    return string.Empty;
                return Path.GetDirectoryName(solutionPath) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public EnvDTE.Debugger? GetDebugger()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _dte.Debugger;
            }
            catch
            {
                return null;
            }
        }

        public string? GetStartupProjectName()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var startupProjects = _dte?.Solution?.SolutionBuild?.StartupProjects;
                if (startupProjects == null)
                    return null;

                // StartupProjects is an object that is typically a string (single startup project)
                // or an object[] (multiple). Normalize to the first project name.
                switch (startupProjects)
                {
                    case string single:
                        return string.IsNullOrWhiteSpace(single) ? null : single;
                    case object[] many when many.Length > 0:
                        return many[0]?.ToString();
                    case System.Collections.IEnumerable seq:
                        foreach (var item in seq)
                        {
                            var s = item?.ToString();
                            if (!string.IsNullOrWhiteSpace(s))
                                return s;
                        }
                        return null;
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public Selection? GetCursorSelection()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var activeDoc = _dte.ActiveDocument;
                if (activeDoc == null)
                {
                    LoggerService.Current.WriteDebug("[gap33-dte-cursor-nodoc] No active document");
                    return null;
                }

                var selection = activeDoc.Selection as TextSelection;
                if (selection == null)
                {
                    LoggerService.Current.WriteDebug("[gap33-dte-cursor-nosel] Active document has no TextSelection");
                    return null;
                }

                var filePath = activeDoc.FullName ?? string.Empty;
                var startLine = selection.AnchorPoint.Line;
                var startCol = selection.AnchorPoint.DisplayColumn;
                var endLine = selection.ActivePoint.Line;
                var endCol = selection.ActivePoint.DisplayColumn;

                LoggerService.Current.WriteDebug($"[gap33-dte-cursor] file={filePath} start={startLine}:{startCol} end={endLine}:{endCol}");

                return new Selection
                {
                    Start = new Location { FilePath = filePath, Line = startLine, Column = startCol },
                    End = new Location { FilePath = filePath, Line = endLine, Column = endCol }
                };
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteDebug($"[gap33-dte-cursor-error] {ex.Message}");
                return null;
            }
        }

        public ContinueVS.Core.Types.ActiveDocumentInfo? GetActiveDocumentInfo()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var activeDoc = _dte.ActiveDocument;
                if (activeDoc == null)
                    return null;

                var filePath = activeDoc.FullName ?? string.Empty;
                var selection = GetCursorSelection();
                return new ContinueVS.Core.Types.ActiveDocumentInfo
                {
                    FilePath = filePath,
                    Selection = selection
                };
            }
            catch
            {
                return null;
            }
        }

        public string? OpenFileInIde(string filePath)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (string.IsNullOrWhiteSpace(filePath))
                    return null;

                _dte.ItemOperations.OpenFile(filePath, Constants.vsViewKindTextView);
                // OpenFile returns a Window (no FullName); the active document now carries the path.
                return _dte.ActiveDocument?.FullName ?? filePath;
            }
            catch
            {
                return null;
            }
        }

        public bool NavigateTo(string filePath, int line)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (string.IsNullOrWhiteSpace(filePath) || line < 1)
                    return false;

                // Open the target file so Edit.GoTo applies to the right document.
                _dte.ItemOperations.OpenFile(filePath, Constants.vsViewKindTextView);
                _dte.ExecuteCommand("Edit.GoTo", line.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ContinueVS.Core.Types.ActiveDocumentInfo? GotoDefinition()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _dte.ExecuteCommand("Edit.GoToDefinition");
                return GetActiveDocumentInfo();
            }
            catch
            {
                return null;
            }
        }

        public bool BuildSolution(string? projectName)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var solutionBuild = _dte.Solution?.SolutionBuild;
                if (solutionBuild == null)
                    return false;

                if (string.IsNullOrWhiteSpace(projectName))
                {
                    solutionBuild.Build(true);
                }
                else
                {
                    var configName = solutionBuild.ActiveConfiguration?.Name ?? "Debug";
                    solutionBuild.BuildProject(configName, projectName, true);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ContinueVS.Core.Types.BuildConfigInfo? GetActiveBuildConfiguration()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var configs = _dte.Solution?.SolutionBuild?.ActiveConfiguration;
                if (configs == null)
                    return null;

                return new ContinueVS.Core.Types.BuildConfigInfo
                {
                    Name = configs.Name,
                    Platform = null,
                    IsActive = true
                };
            }
            catch
            {
                return null;
            }
        }

        public ContinueVS.Core.Types.LaunchProfileInfo? GetLaunchProfile()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var startupProject = GetStartupProjectName();
                var config = GetActiveBuildConfiguration();
                return new ContinueVS.Core.Types.LaunchProfileInfo
                {
                    StartupProject = startupProject,
                    LaunchProfile = config?.Name
                };
            }
            catch
            {
                return null;
            }
        }

        public ContinueVS.Core.Types.OutputPaneInfo? GetOutputPane(string paneName)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (string.IsNullOrWhiteSpace(paneName))
                    return null;

                // The Output window is reached via the Windows collection, then cast to
                // OutputWindow; DTE has no ToolWindows property in this interop surface.
                var outputWindow = _dte.Windows?.Item(Constants.vsWindowKindOutput)?.Object as OutputWindow;
                if (outputWindow == null)
                    return null;

                var panes = outputWindow.OutputWindowPanes;
                if (panes == null || panes.Count == 0)
                    return null;

                // Find the pane by name; OutputWindowPanes is not indexable by name directly,
                // so scan the collection.
                foreach (OutputWindowPane pane in panes)
                {
                    if (string.Equals(pane.Name, paneName, StringComparison.OrdinalIgnoreCase))
                    {
                        var doc = pane.TextDocument;
                        if (doc == null)
                            return new ContinueVS.Core.Types.OutputPaneInfo { Name = paneName, Content = string.Empty };
                        var text = doc.StartPoint.CreateEditPoint().GetText(doc.EndPoint) ?? string.Empty;
                        return new ContinueVS.Core.Types.OutputPaneInfo { Name = paneName, Content = text };
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}

