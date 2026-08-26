using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Applies instrumentation strategies to source files.
    /// Each snippet insertion becomes a separate CodeChange in the ChangeStack.
    /// Insertions done in reverse line order to prevent line number drift.
    /// </summary>
    public class InstrumentationService : IInstrumentationService
    {
        private readonly IBridgeLogger? _logger;

        public InstrumentationService(IBridgeLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<List<string>> ApplyStrategyAsync(
            InstrumentationStrategy? strategy,
            ChangeStack changeStack,
            string targetDir,
            CancellationToken cancellationToken = default)
        {
            var appliedChangeIds = new List<string>();

            if (strategy == null)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync("InstrumentationService: strategy is null");
                return appliedChangeIds;
            }

            try
            {
                // Resolve target file path
                var targetFilePath = ResolveFilePath(strategy.TargetFile, targetDir);
                if (string.IsNullOrEmpty(targetFilePath))
                {
                    if (_logger != null)
                        await _logger.WriteDebugAsync($"InstrumentationService: could not resolve target file {strategy.TargetFile}");
                    return appliedChangeIds;
                }

                // targetFilePath is guaranteed non-null after the check above
                var resolvedPath = targetFilePath!;

                // Read current file content
                if (!File.Exists(resolvedPath))
                {
                    if (_logger != null)
                        await _logger.WriteDebugAsync($"InstrumentationService: target file not found - {resolvedPath}");
                    return appliedChangeIds;
                }

                var lines = File.ReadAllLines(resolvedPath).ToList();

                // Sort snippets by line number in descending order (insert from bottom to top)
                var sortedSnippets = strategy.CodeSnippets
                    .OrderByDescending(s => s.LineNumber)
                    .ToList();

                foreach (var snippet in sortedSnippets)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // Insert snippet at the specified line
                        int insertIndex = Math.Min(snippet.LineNumber, lines.Count);
                        insertIndex = Math.Max(0, insertIndex - 1); // Convert to 0-based index

                        lines.Insert(insertIndex, snippet.Code);

                        // Create CodeChange object
                        var change = new CodeChange
                        {
                            ChangeId = Guid.NewGuid().ToString(),
                            FilePath = resolvedPath,
                            NewContent = string.Join(Environment.NewLine, lines),
                            Description = $"Instrumentation: {snippet.Reason}",
                            Timestamp = DateTime.UtcNow
                        };

                        // Record change in stack (ChangeStack is passed by reference and tracks locally)
                        changeStack.RecordChange(change);
                        File.WriteAllText(resolvedPath, change.NewContent);
                        changeStack.MarkAsApplied(change.ChangeId);
                        appliedChangeIds.Add(change.ChangeId);

                        if (_logger != null)
                            await _logger.WriteDebugAsync($"InstrumentationService: applied snippet at line {snippet.LineNumber}, ChangeId={change.ChangeId}");
                    }
                    catch (Exception ex)
                    {
                        if (_logger != null)
                            await _logger.WriteDebugAsync($"InstrumentationService: error applying snippet at line {snippet.LineNumber} - {ex.Message}");
                        // Continue with next snippet; don't fail entire strategy
                    }
                }

                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstrumentationService: applied {appliedChangeIds.Count} changes total");
                return appliedChangeIds;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstrumentationService: exception during strategy application - {ex.Message}");
                return appliedChangeIds;
            }
        }

        private string? ResolveFilePath(string filePath, string targetDir)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            // If already absolute, use as-is
            if (Path.IsPathRooted(filePath))
            {
                return File.Exists(filePath) ? filePath : null;
            }

            // Try relative to targetDir
            var resolved = Path.Combine(targetDir, filePath);
            return File.Exists(resolved) ? resolved : null;
        }
    }
}
