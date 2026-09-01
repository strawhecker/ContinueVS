using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IChangeStackService.
    /// Manages multiple change stack instances with per-change rollback support.
    /// </summary>
    public class ChangeStackService : IChangeStackService
    {
        private readonly ConcurrentDictionary<string, ChangeStack> _stacks;

        public ChangeStackService()
        {
            _stacks = new ConcurrentDictionary<string, ChangeStack>();
        }

        public string CreateChangeStack()
        {
            var stack = new ChangeStack();
            _stacks.TryAdd(stack.Id, stack);
            return stack.Id;
        }

        public ChangeStack? GetChangeStack(string stackId)
        {
            if (string.IsNullOrEmpty(stackId))
            {
                return null;
            }

            _stacks.TryGetValue(stackId, out var stack);
            return stack;
        }

        public async Task ApplyChangeAsync(string stackId, CodeChange change, string filePath)
        {
            if (string.IsNullOrEmpty(stackId))
            {
                throw new ArgumentException("Stack ID cannot be null or empty.", nameof(stackId));
            }

            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            var stack = GetChangeStack(stackId);
            if (stack == null)
            {
                throw new InvalidOperationException($"Change stack with ID '{stackId}' not found.");
            }

            // Create baseline before applying the change
            string baselineContent = string.Empty;
            try
            {
                if (File.Exists(filePath))
                {
                    baselineContent = await Task.Run(() => File.ReadAllText(filePath));
                }
            }
            catch (Exception ex)
            {
                // Log error but continue; baseline may be empty if file doesn't exist yet
                _ = LoggerService.Current.WriteErrorAsync($"Error reading file for baseline: {ex.Message}", ex);
            }

            // Create and attach baseline to the change
            var baseline = new ChangeBaseline
            {
                FilePath = filePath,
                BaselineContent = baselineContent,
                CreatedAt = DateTime.UtcNow
            };
            change.Baseline = baseline;

            // Write the new content to disk
            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");
                    File.WriteAllText(filePath, change.NewContent);
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply change to file '{filePath}': {ex.Message}", ex);
            }

            // Record the change in the stack
            stack.RecordChange(change);
            stack.MarkAsApplied(change.ChangeId);
        }

        public async Task RollbackChangeAsync(string stackId, string changeId)
        {
            if (string.IsNullOrEmpty(stackId))
            {
                throw new ArgumentException("Stack ID cannot be null or empty.", nameof(stackId));
            }

            if (string.IsNullOrEmpty(changeId))
            {
                throw new ArgumentException("Change ID cannot be null or empty.", nameof(changeId));
            }

            var stack = GetChangeStack(stackId);
            if (stack == null)
            {
                throw new InvalidOperationException($"Change stack with ID '{stackId}' not found.");
            }

            var change = stack.FindChangeById(changeId);
            if (change == null)
            {
                throw new InvalidOperationException($"Change with ID '{changeId}' not found in stack.");
            }

            if (change.Baseline == null)
            {
                throw new InvalidOperationException($"Change '{changeId}' has no baseline; cannot rollback.");
            }

            // Restore the file to its baseline state
            try
            {
                await Task.Run(() =>
                {
                    var filePath = change.Baseline.FilePath;
                    var baselineContent = change.Baseline.BaselineContent;

                    if (string.IsNullOrEmpty(baselineContent))
                    {
                        // Baseline was empty; delete the file
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    else
                    {
                        // Restore baseline content
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");
                        File.WriteAllText(filePath, baselineContent);
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to rollback change '{changeId}': {ex.Message}", ex);
            }

            stack.UnmarkAsApplied(changeId);
        }

        public async Task RollbackToChangeAsync(string stackId, string changeId)
        {
            if (string.IsNullOrEmpty(stackId))
            {
                throw new ArgumentException("Stack ID cannot be null or empty.", nameof(stackId));
            }

            if (string.IsNullOrEmpty(changeId))
            {
                throw new ArgumentException("Change ID cannot be null or empty.", nameof(changeId));
            }

            var stack = GetChangeStack(stackId);
            if (stack == null)
            {
                throw new InvalidOperationException($"Change stack with ID '{stackId}' not found.");
            }

            var changesToRollback = stack.GetChangesAfter(changeId);

            // Rollback in reverse order (most recent first)
            for (int i = changesToRollback.Count - 1; i >= 0; i--)
            {
                var change = changesToRollback[i];
                await RollbackChangeAsync(stackId, change.ChangeId);
            }
        }

        public void RemoveChangeStack(string stackId)
        {
            if (!string.IsNullOrEmpty(stackId))
            {
                _stacks.TryRemove(stackId, out _);
            }
        }
    }
}
