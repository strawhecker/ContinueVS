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
    /// Interface for the Debug Session Service that orchestrates instruction execution.
    /// </summary>
    public interface IDebugSessionService
    {
        /// <summary>
        /// Loads a debug instruction from a file path.
        /// </summary>
        /// <param name="instructionPath">File path to the instruction (JSON).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The loaded DebugInstruction.</returns>
        Task<DebugInstruction> LoadInstructionAsync(string instructionPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a debug instruction by generating phases and executing them sequentially.
        /// </summary>
        /// <param name="instruction">The instruction to execute.</param>
        /// <param name="changeStackId">The change stack ID to apply changes to.</param>
        /// <param name="targetDir">Target directory for applying changes.</param>
        /// <param name="mode">Debug execution mode: Autonomous (auto-answers) or Interactive (prompts user). (gap29_8_8)</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The TestPlan with phases and their execution annotations.</returns>
        Task<TestPlan> ExecuteInstructionAsync(
            DebugInstruction instruction,
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
        /// Gets the current pause state of the session (gap31_1).
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Sets the pause state asynchronously (gap31_1).
        /// Consumed by phase executors via CancellationToken polling.
        /// </summary>
        Task SetPausedAsync(bool paused);

        /// <summary>
        /// Stores a pause checkpoint captured during streaming (gap31_3).
        /// Checkpoint contains buffered streamed text, chunk count, and session context snapshot.
        /// </summary>
        Task SetPauseCheckpointAsync(PauseCheckpoint checkpoint);

        /// <summary>
        /// Retrieves the current pause checkpoint if one exists (gap31_3).
        /// </summary>
        Task<PauseCheckpoint?> GetPauseCheckpointAsync();

        /// <summary>
        /// Clears the pause checkpoint.
        /// Called when starting a new stream session to ensure fresh state.
        /// </summary>
        void ClearPauseCheckpoint();
    }

    /// <summary>
    /// Implementation of IDebugSessionService.
    /// Orchestrates the full workflow: load instruction → generate phases → execute phases sequentially.
    /// </summary>
    public class DebugSessionService : IDebugSessionService
    {
        private readonly IInstructionProcessorService _instructionProcessor;
        private readonly IChangeStackService _changeStackService;
        private readonly PhaseExecutorFactory _executorFactory;
        private readonly IBridgeLogger? _logger;
        private TestPlan? _currentSessionState;
        private bool _isPaused = false;

        public DebugSessionService(
            IInstructionProcessorService instructionProcessor,
            IChangeStackService changeStackService,
            PhaseExecutorFactory executorFactory,
            IBridgeLogger? logger = null)
        {
            if (instructionProcessor == null)
                throw new ArgumentNullException(nameof(instructionProcessor));
            if (changeStackService == null)
                throw new ArgumentNullException(nameof(changeStackService));
            if (executorFactory == null)
                throw new ArgumentNullException(nameof(executorFactory));

            _instructionProcessor = instructionProcessor;
            _changeStackService = changeStackService;
            _executorFactory = executorFactory;
            _logger = logger;
        }

        public async Task<DebugInstruction> LoadInstructionAsync(
            string instructionPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(instructionPath))
                throw new ArgumentException("Instruction path cannot be empty.", nameof(instructionPath));

            if (!File.Exists(instructionPath))
                throw new FileNotFoundException($"Instruction file not found: {instructionPath}");

            try
            {
                var json = await Task.Run(() => File.ReadAllText(instructionPath), cancellationToken);
                var instruction = Newtonsoft.Json.JsonConvert.DeserializeObject<DebugInstruction>(json);

                if (instruction == null)
                    throw new InvalidOperationException("Failed to deserialize instruction from file.");

                if (_logger != null)
                    await _logger.WriteDebugAsync($"DebugSessionService.LoadInstructionAsync: loaded instruction '{instruction.Text}'");

                return instruction;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"DebugSessionService.LoadInstructionAsync: error - {ex.Message}");
                throw;
            }
        }

        public async Task<TestPlan> ExecuteInstructionAsync(
            DebugInstruction instruction,
            string changeStackId,
            string targetDir,
            DebugExecutionMode mode = DebugExecutionMode.Autonomous,
            CancellationToken cancellationToken = default)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));
            if (string.IsNullOrWhiteSpace(changeStackId))
                throw new ArgumentException("Change stack ID cannot be empty.", nameof(changeStackId));
            if (string.IsNullOrWhiteSpace(targetDir))
                throw new ArgumentException("Target directory cannot be empty.", nameof(targetDir));

            if (_logger != null)
                await _logger.WriteDebugAsync($"DebugSessionService.ExecuteInstructionAsync: starting execution for '{instruction.Text}' (mode={mode})");

            try
            {
                // Generate phases from instruction
                var testPlan = await _instructionProcessor.GenerateInternalPhasesAsync(instruction, cancellationToken);

                if (_logger != null)
                    await _logger.WriteDebugAsync($"DebugSessionService.ExecuteInstructionAsync: generated {testPlan.Phases.Count} phases");

                // Get the change stack
                var changeStack = _changeStackService.GetChangeStack(changeStackId);
                if (changeStack == null)
                    throw new InvalidOperationException($"Change stack '{changeStackId}' not found.");

                // Execute phases sequentially
                foreach (var phase in testPlan.Phases)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Update phase status to InProgress
                    phase.Status = InternalPhaseStatus.InProgress;

                    // Get executor for this phase type
                    var executor = _executorFactory.GetExecutor(phase.Type);
                    if (executor == null)
                    {
                        if (_logger != null)
                            await _logger.WriteDebugAsync($"DebugSessionService: no executor found for phase type {phase.Type}");

                        phase.Execution = new InternalPhaseExecution
                        {
                            Strategy = phase.Type.ToString(),
                            Result = "Skipped",
                            ChangesAppliedCount = 0,
                            ExecutedAt = DateTime.UtcNow,
                            ErrorMessage = $"No executor registered for phase type {phase.Type}"
                        };
                        phase.Status = InternalPhaseStatus.Failed;
                        break;
                    }

                    // Execute the phase
                    try
                    {
                        bool isInteractiveMode = (mode == DebugExecutionMode.Interactive);
                        phase.Execution = await executor.ExecuteAsync(phase, changeStack, targetDir, isInteractiveMode, cancellationToken);

                        // Update phase status based on execution result
                        if (phase.Execution.Result == "Completed" || phase.Execution.Result == "Skipped")
                        {
                            phase.Status = InternalPhaseStatus.Completed;
                        }
                        else
                        {
                            phase.Status = InternalPhaseStatus.Failed;

                            if (_logger != null)
                                await _logger.WriteDebugAsync($"DebugSessionService: phase '{phase.Id}' failed: {phase.Execution.ErrorMessage}");

                            // Stop execution on first failure
                            break;
                        }

                        if (_logger != null)
                            await _logger.WriteDebugAsync($"DebugSessionService: phase '{phase.Id}' completed with {phase.Execution.ChangesAppliedCount} changes");
                    }
                    catch (Exception ex)
                    {
                        phase.Status = InternalPhaseStatus.Failed;
                        phase.Execution = new InternalPhaseExecution
                        {
                            Strategy = phase.Type.ToString(),
                            Result = "Failed",
                            ChangesAppliedCount = 0,
                            ExecutedAt = DateTime.UtcNow,
                            ErrorMessage = ex.Message
                        };

                        if (_logger != null)
                            await _logger.WriteDebugAsync($"DebugSessionService: phase exception - {ex.Message}");

                        // Stop execution on exception
                        break;
                    }
                }

                // Store session state
                _currentSessionState = testPlan;

                if (_logger != null)
                    await _logger.WriteDebugAsync($"DebugSessionService.ExecuteInstructionAsync: execution completed");

                return testPlan;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"DebugSessionService.ExecuteInstructionAsync: fatal error - {ex.Message}");
                throw;
            }
        }

        public TestPlan? GetSessionState()
        {
            return _currentSessionState;
        }

        /// <summary>
        /// Gets the current pause state of the session (gap31_1).
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Sets the pause state asynchronously (gap31_1).
        /// Consumed by phase executors via CancellationToken polling.
        /// </summary>
        public async Task SetPausedAsync(bool paused)
        {
            _isPaused = paused;
            if (_logger != null)
                await _logger.WriteDebugAsync($"DebugSessionService.SetPausedAsync: pause state set to {paused}");
        }

        /// <summary>
        /// Stores a pause checkpoint captured during streaming (gap31_3).
        /// Checkpoint contains buffered streamed text, chunk count, and session context snapshot.
        /// </summary>
        public async Task SetPauseCheckpointAsync(PauseCheckpoint checkpoint)
        {
            _currentPauseCheckpoint = checkpoint;
            if (_logger != null)
                await _logger.WriteDebugAsync($"DebugSessionService.SetPauseCheckpointAsync: checkpoint stored with {checkpoint.ChunkCount} chunks");
        }

        /// <summary>
        /// Retrieves the current pause checkpoint if one exists (gap31_3).
        /// </summary>
        public async Task<PauseCheckpoint?> GetPauseCheckpointAsync()
        {
            await Task.CompletedTask;
            return _currentPauseCheckpoint;
        }

        /// <summary>
        /// Clears the pause checkpoint.
        /// Called when starting a new stream session to ensure fresh state.
        /// </summary>
        public void ClearPauseCheckpoint()
        {
            _currentPauseCheckpoint = null;
        }

        private PauseCheckpoint? _currentPauseCheckpoint;
    }
}
