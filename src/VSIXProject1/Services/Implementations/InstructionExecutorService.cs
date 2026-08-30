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
    /// Implementation of IInstructionExecutorService.
    /// Orchestrates the full workflow: load instruction → generate phases → execute phases sequentially.
    /// Shared by Agent and Debug modes; Debug-exclusive behaviour (debugger context) is injected upstream
    /// via ModeConfig.RequiresDebuggerContext before this service is invoked.
    /// </summary>
    public class InstructionExecutorService : IInstructionExecutorService
    {
        private readonly IInstructionProcessorService _instructionProcessor;
        private readonly IChangeStackService _changeStackService;
        private readonly PhaseExecutorFactory _executorFactory;
        private readonly IBridgeLogger? _logger;
        private TestPlan? _currentSessionState;
        private bool _isPaused = false;
        private PauseCheckpoint? _currentPauseCheckpoint;

        public InstructionExecutorService(
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

        public async Task<ExecutionInstruction> LoadInstructionAsync(
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
                var instruction = Newtonsoft.Json.JsonConvert.DeserializeObject<ExecutionInstruction>(json);

                if (instruction == null)
                    throw new InvalidOperationException("Failed to deserialize instruction from file.");

                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstructionExecutorService.LoadInstructionAsync: loaded instruction '{instruction.Text}'");

                return instruction;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstructionExecutorService.LoadInstructionAsync: error - {ex.Message}");
                throw;
            }
        }

        public async Task<TestPlan> ExecuteInstructionAsync(
            ExecutionInstruction instruction,
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
                await _logger.WriteDebugAsync($"InstructionExecutorService.ExecuteInstructionAsync: starting execution for '{instruction.Text}' (mode={mode})");

            try
            {
                // Generate phases from instruction
                var testPlan = await _instructionProcessor.GenerateInternalPhasesAsync(instruction, cancellationToken);

                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstructionExecutorService.ExecuteInstructionAsync: generated {testPlan.Phases.Count} phases");

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
                            await _logger.WriteDebugAsync($"InstructionExecutorService: no executor found for phase type {phase.Type}");

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
                                await _logger.WriteDebugAsync($"InstructionExecutorService: phase '{phase.Id}' failed: {phase.Execution.ErrorMessage}");

                            // Stop execution on first failure
                            break;
                        }

                        if (_logger != null)
                            await _logger.WriteDebugAsync($"InstructionExecutorService: phase '{phase.Id}' completed with {phase.Execution.ChangesAppliedCount} changes");
                    }
                    catch (Exception ex)
                    {
                        phase.Execution = new InternalPhaseExecution
                        {
                            Strategy = phase.Type.ToString(),
                            Result = "Failed",
                            ChangesAppliedCount = 0,
                            ExecutedAt = DateTime.UtcNow,
                            ErrorMessage = ex.Message
                        };
                        phase.Status = InternalPhaseStatus.Failed;

                        if (_logger != null)
                            await _logger.WriteDebugAsync($"InstructionExecutorService: phase '{phase.Id}' threw exception: {ex.Message}");

                        break;
                    }
                }

                // Store final session state
                _currentSessionState = testPlan;

                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstructionExecutorService.ExecuteInstructionAsync: execution complete. Phases: {testPlan.Phases.Count}");

                return testPlan;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstructionExecutorService.ExecuteInstructionAsync: fatal error - {ex.Message}");
                throw;
            }
        }

        public TestPlan? GetSessionState() => _currentSessionState;

        public bool IsPaused => _isPaused;

        public async Task SetPausedAsync(bool paused)
        {
            _isPaused = paused;
            if (_logger != null)
                await _logger.WriteDebugAsync($"InstructionExecutorService.SetPausedAsync: pause state set to {paused}");
        }

        public async Task SetPauseCheckpointAsync(PauseCheckpoint checkpoint)
        {
            _currentPauseCheckpoint = checkpoint;
            if (_logger != null)
                await _logger.WriteDebugAsync($"InstructionExecutorService.SetPauseCheckpointAsync: checkpoint stored with {checkpoint.ChunkCount} chunks");
        }

        public async Task<PauseCheckpoint?> GetPauseCheckpointAsync()
        {
            return _currentPauseCheckpoint;
        }

        public void ClearPauseCheckpoint()
        {
            _currentPauseCheckpoint = null;
        }
    }
}
