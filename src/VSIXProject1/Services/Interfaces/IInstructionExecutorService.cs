using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Orchestrates execution of an instruction: loads it, generates phases, and executes them sequentially.
    /// Shared by Agent and Debug modes. The only Debug-exclusive concern is debugger_context injection,
    /// which is handled upstream via ModeConfig.RequiresDebuggerContext.
    /// </summary>
    public interface IInstructionExecutorService
    {
        /// <summary>
        /// Loads an execution instruction from a file path.
        /// </summary>
        /// <param name="instructionPath">File path to the instruction (JSON).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The loaded ExecutionInstruction.</returns>
        Task<ExecutionInstruction> LoadInstructionAsync(string instructionPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an instruction by generating phases and executing them sequentially.
        /// </summary>
        /// <param name="instruction">The instruction to execute.</param>
        /// <param name="changeStackId">The change stack ID to apply changes to.</param>
        /// <param name="targetDir">Target directory for applying changes.</param>
        /// <param name="mode">Execution mode: Autonomous (auto-answers) or Interactive (prompts user).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The TestPlan with phases and their execution annotations.</returns>
        Task<TestPlan> ExecuteInstructionAsync(
            ExecutionInstruction instruction,
            string changeStackId,
            string targetDir,
            DebugExecutionMode mode = DebugExecutionMode.Autonomous,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current session state (last executed test plan with annotations).
        /// </summary>
        /// <returns>The current TestPlan or null if no execution has occurred.</returns>
        TestPlan? GetSessionState();

        /// <summary>
        /// Gets the current pause state of the session.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Sets the pause state asynchronously.
        /// Consumed by phase executors via CancellationToken polling.
        /// </summary>
        Task SetPausedAsync(bool paused);

        /// <summary>
        /// Stores a pause checkpoint captured during streaming.
        /// Checkpoint contains buffered streamed text, chunk count, and session context snapshot.
        /// </summary>
        Task SetPauseCheckpointAsync(PauseCheckpoint checkpoint);

        /// <summary>
        /// Retrieves the current pause checkpoint if one exists.
        /// </summary>
        Task<PauseCheckpoint?> GetPauseCheckpointAsync();

        /// <summary>
        /// Clears the pause checkpoint.
        /// Called when starting a new stream session to ensure fresh state.
        /// </summary>
        void ClearPauseCheckpoint();
    }
}
