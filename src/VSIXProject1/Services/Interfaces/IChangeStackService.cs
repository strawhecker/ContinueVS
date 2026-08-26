using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for managing per-change transaction stacks during debug sessions.
    /// Enables creation, modification, and rollback of changes with baseline preservation.
    /// </summary>
    public interface IChangeStackService
    {
        /// <summary>
        /// Creates a new change stack instance.
        /// </summary>
        /// <returns>The stack ID for future reference.</returns>
        string CreateChangeStack();

        /// <summary>
        /// Retrieves an existing change stack by ID.
        /// </summary>
        /// <param name="stackId">The stack ID to retrieve.</param>
        /// <returns>The ChangeStack if found, otherwise null.</returns>
        ChangeStack? GetChangeStack(string stackId);

        /// <summary>
        /// Applies a change to a file and records it in the change stack.
        /// Creates a baseline before the change is applied.
        /// </summary>
        /// <param name="stackId">The stack ID where this change will be recorded.</param>
        /// <param name="change">The CodeChange to apply.</param>
        /// <param name="filePath">The file path where the change will be written.</param>
        /// <returns>A task that completes when the change is applied and recorded.</returns>
        Task ApplyChangeAsync(string stackId, CodeChange change, string filePath);

        /// <summary>
        /// Rolls back a single change, restoring the file to its state before that change.
        /// Earlier changes remain applied (per-change rollback).
        /// </summary>
        /// <param name="stackId">The stack ID containing the change.</param>
        /// <param name="changeId">The ID of the change to roll back.</param>
        /// <returns>A task that completes when the rollback is done.</returns>
        Task RollbackChangeAsync(string stackId, string changeId);

        /// <summary>
        /// Rolls back all changes that come after a specified change ID (cascade rollback).
        /// The specified change remains applied; all subsequent changes are reverted.
        /// </summary>
        /// <param name="stackId">The stack ID containing the changes.</param>
        /// <param name="changeId">The ID of the change to roll back to (inclusive).</param>
        /// <returns>A task that completes when all cascade rollbacks are done.</returns>
        Task RollbackToChangeAsync(string stackId, string changeId);

        /// <summary>
        /// Removes a change stack from memory (cleanup after session/phase ends).
        /// </summary>
        /// <param name="stackId">The stack ID to remove.</param>
        void RemoveChangeStack(string stackId);
    }
}
