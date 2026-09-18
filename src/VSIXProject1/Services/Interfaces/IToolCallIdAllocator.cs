#nullable enable

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Allocates deterministic, own tool-call IDs (gap80).
    ///
    /// LLM-provided tool IDs are random and don't correlate to results. This allocator
    /// produces an ID sequence owned by ContinueVS: the base is time-of-day to 100 ns
    /// precision, incremented by 1 for each additional tool in a parallel batch. Replacing
    /// the LLM's random ID with our own makes correlation and pruning deterministic.
    /// </summary>
    public interface IToolCallIdAllocator
    {
        /// <summary>
        /// Allocates a fresh deterministic tool-call ID.
        /// The first call returns the time-of-day base; subsequent calls increment by 1.
        /// </summary>
        string Next();

        /// <summary>
        /// Resets the allocator's increment base so the next call begins a new base.
        /// Call once at the start of a send/action to align with the per-action budget.
        /// </summary>
        void Reset();
    }
}
