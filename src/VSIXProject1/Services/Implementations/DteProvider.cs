using EnvDTE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public string GetActiveFilepath()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var activeDoc = _dte.ActiveDocument;
                if (activeDoc == null)
                    return string.Empty;

                return activeDoc.FullName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
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
                    Debug.WriteLine("[gap33-dte-cursor-nodoc] No active document");
                    return null;
                }

                var selection = activeDoc.Selection as TextSelection;
                if (selection == null)
                {
                    Debug.WriteLine("[gap33-dte-cursor-nosel] Active document has no TextSelection");
                    return null;
                }

                var filePath = activeDoc.FullName ?? string.Empty;
                var startLine = selection.AnchorPoint.Line;
                var startCol = selection.AnchorPoint.DisplayColumn;
                var endLine = selection.ActivePoint.Line;
                var endCol = selection.ActivePoint.DisplayColumn;

                Debug.WriteLine($"[gap33-dte-cursor] file={filePath} start={startLine}:{startCol} end={endLine}:{endCol}");

                return new Selection
                {
                    Start = new Location { FilePath = filePath, Line = startLine, Column = startCol },
                    End = new Location { FilePath = filePath, Line = endLine, Column = endCol }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap33-dte-cursor-error] {ex.Message}");
                return null;
            }
        }
    }
}

