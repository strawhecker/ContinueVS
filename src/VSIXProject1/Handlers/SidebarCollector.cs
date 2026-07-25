using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.IPC;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.Handlers
{
    /// <summary>
    /// Sidebar State DTO returned by SidebarCollector
    /// </summary>
    public class SidebarState
    {
        public List<SidebarMessage> Messages { get; set; } = new();
        public List<SidebarDocument> Documents { get; set; } = new();
        public List<SidebarSymbol> Symbols { get; set; } = new();
        public Dictionary<string, SidebarDiagnostics> Diagnostics { get; set; } = new();
        public List<SidebarAction> Actions { get; set; } = new();
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Message DTO (placeholder for Step 87)
    /// </summary>
    public class SidebarMessage
    {
        public string? Id { get; set; }
        public string? Content { get; set; }
        public string? Author { get; set; }
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// Document DTO for open files
    /// </summary>
    public class SidebarDocument
    {
        public string? Filepath { get; set; }
        public string? Language { get; set; }
        public bool IsModified { get; set; }
        public int LineCount { get; set; }
    }

    /// <summary>
    /// Symbol DTO for bookmarks and references
    /// </summary>
    public class SidebarSymbol
    {
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public bool IsBookmarked { get; set; }
    }

    /// <summary>
    /// Diagnostics DTO for errors and warnings
    /// </summary>
    public class SidebarDiagnostics
    {
        public List<SidebarDiagnosticItem> Errors { get; set; } = new();
        public List<SidebarDiagnosticItem> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Individual diagnostic item
    /// </summary>
    public class SidebarDiagnosticItem
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string? Message { get; set; }
        public string? Code { get; set; }
    }

    /// <summary>
    /// Action DTO for quick actions and suggestions
    /// </summary>
    public class SidebarAction
    {
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Sidebar UI Exception
    /// </summary>
    public class SidebarException : Exception
    {
        public string Code { get; set; }

        public SidebarException(string message, string code = "SIDEBAR_ERROR") : base(message)
        {
            Code = code;
        }

        public SidebarException(string message, Exception innerException, string code = "SIDEBAR_ERROR")
            : base(message, innerException)
        {
            Code = code;
        }
    }

    /// <summary>
    /// Sidebar Collector — Provides DTE-based sidebar state for bridge handler
    ///
    /// Enumerates open documents, diagnostics, symbols, and workspace structure
    /// to populate the sidebar UI tree visible in the Continue WebView.
    ///
    /// **Factory Pattern**: Instantiated once per bridge session; state collected
    /// on-demand per GetSidebarStateAsync() call.
    ///
    /// **Dependencies**:
    /// - IServiceProvider: DTE access (EnvDTE)
    /// - IDiagnosticsProvider: Errors/warnings aggregation
    ///
    /// **Step 86 deliverable**
    /// </summary>
    internal sealed class SidebarCollector
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly object? _dte;

        /// <summary>
        /// Initialize SidebarCollector with service provider and DTE
        /// </summary>
        /// <param name="serviceProvider">VS service provider (required)</param>
        /// <param name="dte">DTE instance for document enumeration (nullable for testing)</param>
        /// <exception cref="SidebarException">If serviceProvider is null</exception>
        public SidebarCollector(IServiceProvider serviceProvider, object? dte = null)
        {
            _serviceProvider = serviceProvider ?? throw new SidebarException("ServiceProvider required", "MISSING_SERVICE_PROVIDER");
            _dte = dte;
        }

        /// <summary>
        /// Get sidebar state asynchronously
        /// </summary>
        /// <param name="filterFilepath">Optional: return only this file's diagnostics and symbols</param>
        /// <returns>SidebarState DTO</returns>
        /// <exception cref="SidebarException">If DTE is unavailable or operation fails</exception>
        public async Task<SidebarState> GetSidebarStateAsync(string? filterFilepath = null)
        {
            return await Task.Run(() =>
            {
                return GetSidebarStateInternal(filterFilepath);
            });
        }

        /// <summary>
        /// Internal synchronous implementation (runs on thread pool)
        /// </summary>
        private SidebarState GetSidebarStateInternal(string? filterFilepath)
        {
            var state = new SidebarState();

            try
            {
                // Enumerate open documents
                state.Documents = GetOpenDocuments(filterFilepath);

                // Query diagnostics
                state.Diagnostics = GetDiagnostics(filterFilepath);

                // Populate symbols from active editor
                state.Symbols = GetSymbols(filterFilepath);

                // Collect workspace tree
                if (string.IsNullOrEmpty(filterFilepath))
                {
                    // Full workspace tree (deferred; placeholder for now)
                    // Step 87 (context-window) may expand this
                }

                return state;
            }
            catch (Exception ex)
            {
                throw new SidebarException(
                    $"Failed to get sidebar state: {ex.Message}",
                    ex,
                    "GET_STATE_FAILED"
                );
            }
        }

        /// <summary>
        /// Enumerate open documents from DTE
        /// </summary>
        private List<SidebarDocument> GetOpenDocuments(string? filterFilepath)
        {
            var documents = new List<SidebarDocument>();

            try
            {
                if (_dte == null)
                {
                    return documents;
                }

                var documentsProperty = _dte.GetType().GetProperty("Documents");
                var documentsCollection = documentsProperty?.GetValue(_dte);

                if (documentsCollection == null)
                {
                    return documents;
                }

                foreach (var doc in (System.Collections.IEnumerable)documentsCollection)
                {
                    try
                    {
                        var fullNameProperty = doc.GetType().GetProperty("FullName");
                        var filepath = (string?)fullNameProperty?.GetValue(doc);

                        if (string.IsNullOrEmpty(filepath))
                        {
                            continue;
                        }

                        // Apply filter if specified
                        if (!string.IsNullOrEmpty(filterFilepath) && filepath != filterFilepath)
                        {
                            continue;
                        }

                        var language = GetLanguageFromFilepath(filepath ?? string.Empty);
                        var lineCount = GetLineCount(doc);

                        var savedProperty = doc.GetType().GetProperty("Saved");
                        var isModified = savedProperty != null && !(bool)(savedProperty.GetValue(doc) ?? true);

                        documents.Add(new SidebarDocument
                        {
                            Filepath = filepath,
                            Language = language,
                            IsModified = isModified,
                            LineCount = lineCount,
                        });
                    }
                    catch (Exception ex)
                    {
                        // Log and continue; don't fail entire operation
                        System.Diagnostics.Debug.WriteLine($"[SidebarCollector] Error processing document: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SidebarCollector] Error enumerating documents: {ex.Message}");
            }

            return documents;
        }

        /// <summary>
        /// Aggregate diagnostics (placeholder for future implementation)
        /// </summary>
        private Dictionary<string, SidebarDiagnostics> GetDiagnostics(string? filterFilepath)
        {
            var diagnostics = new Dictionary<string, SidebarDiagnostics>();

            try
            {
                // TODO: Implement diagnostics aggregation from VS error list
                // For now, return empty to avoid blocking
                return diagnostics;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SidebarCollector] Error getting diagnostics: {ex.Message}");
                return diagnostics;
            }
        }

        /// <summary>
        /// Extract symbols from active editor context
        /// </summary>
        private List<SidebarSymbol> GetSymbols(string? filterFilepath)
        {
            var symbols = new List<SidebarSymbol>();

            try
            {
                if (_dte == null)
                {
                    return symbols;
                }

                var activeDocProperty = _dte.GetType().GetProperty("ActiveDocument");
                var activeDocument = activeDocProperty?.GetValue(_dte);

                if (activeDocument != null)
                {
                    var fullNameProperty = activeDocument.GetType().GetProperty("FullName");
                    var activeFilepath = (string?)fullNameProperty?.GetValue(activeDocument);

                    if (string.IsNullOrEmpty(filterFilepath) || activeFilepath == filterFilepath)
                    {
                        // TODO: Integrate with Step 53 (SymbolExtractor) cache
                        // For now, return empty array
                    }
                }

                return symbols;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SidebarCollector] Error getting symbols: {ex.Message}");
                return symbols;
            }
        }

        /// <summary>
        /// Determine language from file extension
        /// </summary>
        private static string GetLanguageFromFilepath(string filepath)
        {
            var ext = Path.GetExtension(filepath)?.ToLowerInvariant() ?? "";
            return ext switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".json" => "json",
                ".md" => "markdown",
                ".xml" => "xml",
                ".html" => "html",
                ".css" => "css",
                ".py" => "python",
                ".cpp" or ".c" or ".h" => "cpp",
                _ => "plaintext",
            };
        }

        /// <summary>
        /// Get line count from document
        /// </summary>
        private static int GetLineCount(object doc)
        {
            try
            {
                // COM interop: Document.Object can be tricky to access
                // Use reflection to safely get the TextDocument
                var objProp = doc.GetType().GetProperty("Object");
                if (objProp != null)
                {
                    var obj = objProp.GetValue(doc);
                    // Safely access EndPoint.Line via reflection
                    var textDocType = obj?.GetType();
                    if (textDocType != null)
                    {
                        var endPointProp = textDocType.GetProperty("EndPoint");
                        if (endPointProp != null)
                        {
                            var endPoint = endPointProp.GetValue(obj);
                            var lineProp = endPoint?.GetType().GetProperty("Line");
                            if (lineProp != null)
                            {
                                return (int)lineProp.GetValue(endPoint);
                            }
                        }
                    }
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
