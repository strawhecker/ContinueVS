using EnvDTE;
using System;
using System.Collections.Generic;
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
    }
}

