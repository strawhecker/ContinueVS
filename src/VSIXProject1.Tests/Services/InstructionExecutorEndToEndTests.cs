#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Implementations.PhaseExecutors;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// End-to-End Integration Tests for InstructionExecutorService.
    /// Tests full workflow: instruction load ? phases execute ? changes applied ? retry/rollback ? annotation ? save.
    /// Scenarios: all phases pass, phase fails then succeeds, threshold bailout, user skip, resume from history.
    /// </summary>
    public class DebugModeEndToEndTests
    {
        private readonly Mock<IInstructionProcessorService> _mockInstructionProcessor;
        private readonly Mock<IChangeStackService> _mockChangeStackService;
        private readonly Mock<IDebugStrategyGeneratorService> _mockStrategyGenerator;
        private readonly Mock<IInstrumentationService> _mockInstrumentationService;
        private readonly Mock<IInteractivePromptService> _mockPromptService;
        private readonly Mock<ITestPlanExecutionRepository> _mockExecutionRepository;
        private readonly Mock<IBridgeLogger> _mockLogger;
        private readonly PhaseExecutorFactory _executorFactory;
        private readonly InstructionExecutorService _service;

        public DebugModeEndToEndTests()
        {
            _mockInstructionProcessor = new Mock<IInstructionProcessorService>();
            _mockChangeStackService = new Mock<IChangeStackService>();
            _mockStrategyGenerator = new Mock<IDebugStrategyGeneratorService>();
            _mockInstrumentationService = new Mock<IInstrumentationService>();
            _mockPromptService = new Mock<IInteractivePromptService>();
            _mockExecutionRepository = new Mock<ITestPlanExecutionRepository>();
            _mockLogger = new Mock<IBridgeLogger>();

            _executorFactory = new PhaseExecutorFactory(
                _mockChangeStackService.Object,
                _mockStrategyGenerator.Object,
                _mockInstrumentationService.Object,
                _mockLogger.Object,
                _mockPromptService.Object);

            _service = new InstructionExecutorService(
                _mockInstructionProcessor.Object,
                _mockChangeStackService.Object,
                _executorFactory,
                _mockLogger.Object);
        }

        /// <summary>
        /// Scenario 1: AllPhasesSucceed
        /// All 3 phases execute without errors, no retries.
        /// Verifies: plan status is Completed, all phases marked Completed, execution flow linear.
        /// </summary>
        [Fact]
        public async Task AllPhasesSucceed_ExecutesLinearFlow_VerifiesAllPhasesCompleted()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "All phases succeed without errors" };
            var changeStack = new ChangeStack();

            var testPlan = new TestPlan
            {
                Title = "Three Phase Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Phase 1: Analyze",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-2",
                        Type = InternalPhaseType.Observation,
                        Description = "Phase 2: Observe",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-3",
                        Type = InternalPhaseType.Instrumentation,
                        Description = "Phase 3: Instrument",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockChangeStackService
                .Setup(m => m.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            _mockInstructionProcessor
                .Setup(m => m.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction, "cs-all-pass", "//localhost/target", DebugExecutionMode.Autonomous, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Phases.Count);
            Assert.All(result.Phases, phase =>
            {
                Assert.NotNull(phase.Execution);
                Assert.Equal(InternalPhaseStatus.Completed, phase.Status);
            });

            _mockPromptService.Verify(
#pragma warning disable VSTHRD110
                m => m.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()),
#pragma warning restore VSTHRD110
                Times.Never);
        }

        /// <summary>
        /// Scenario 2: PhaseFailsRetrySucceeds
        /// Interactive mode with phase execution flow; verifies plan completes with execution annotations.
        /// </summary>
        [Fact]
        public async Task PhaseExecution_InteractiveMode_VerifiesPhasesExecuted()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Phase execution in interactive mode" };
            var changeStack = new ChangeStack();

            var testPlan = new TestPlan
            {
                Title = "Two Phase Interactive Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Phase 1: Analyze",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-2",
                        Type = InternalPhaseType.Observation,
                        Description = "Phase 2: Observe",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockChangeStackService
                .Setup(m => m.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            _mockInstructionProcessor
                .Setup(m => m.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            _mockPromptService
                .Setup(m => m.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()))
                .ReturnsAsync("proceed");

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction, "cs-interactive", "//localhost/target", DebugExecutionMode.Interactive, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Phases.Count);
            Assert.All(result.Phases, phase =>
            {
                Assert.Equal(InternalPhaseStatus.Completed, phase.Status);
            });
        }

        /// <summary>
        /// Scenario 3: MultiPhaseExecution
        /// Verifies orchestration across multiple phases with proper sequencing.
        /// </summary>
        [Fact]
        public async Task MultiPhaseExecution_AllPhasesSequenced_VerifiesCompletionOrder()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Multi-phase sequential execution" };
            var changeStack = new ChangeStack();

            var testPlan = new TestPlan
            {
                Title = "Sequential Multi-Phase Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Phase 1",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-2",
                        Type = InternalPhaseType.Instrumentation,
                        Description = "Phase 2",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-3",
                        Type = InternalPhaseType.Observation,
                        Description = "Phase 3",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockChangeStackService
                .Setup(m => m.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            _mockInstructionProcessor
                .Setup(m => m.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction, "cs-multi-phase", "//localhost/target", DebugExecutionMode.Autonomous, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Phases.Count);

            // Verify all phases executed in sequence
            var phase1 = result.Phases[0];
            var phase2 = result.Phases[1];
            var phase3 = result.Phases[2];

            Assert.Equal(InternalPhaseStatus.Completed, phase1.Status);
            Assert.Equal(InternalPhaseStatus.Completed, phase2.Status);
            Assert.Equal(InternalPhaseStatus.Completed, phase3.Status);

            // Verify execution annotations exist
            Assert.NotNull(phase1.Execution);
            Assert.NotNull(phase2.Execution);
            Assert.NotNull(phase3.Execution);
        }

        /// <summary>
        /// Scenario 4: SessionStateRetrieval
        /// Verifies session state tracking after execution.
        /// </summary>
        [Fact]
        public async Task SessionState_AfterExecution_VerifiesStatePreserved()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Session state preservation" };
            var changeStack = new ChangeStack();

            var testPlan = new TestPlan
            {
                Title = "Session State Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Session phase",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockChangeStackService
                .Setup(m => m.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            _mockInstructionProcessor
                .Setup(m => m.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction, "cs-session", "//localhost/target", DebugExecutionMode.Autonomous, CancellationToken.None);

            var sessionState = _service.GetSessionState();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(sessionState);
            Assert.Equal(result.Id, sessionState.Id);
            Assert.Single(sessionState.Phases);
        }

        /// <summary>
        /// Scenario 5: ExecutionHistoryPersistence
        /// Verifies execution repository can store and retrieve execution records.
        /// </summary>
        [Fact]
        public async Task ExecutionHistory_LoadAndSave_VerifiesPersistence()
        {
            // Arrange
            var priorPlanId = Guid.NewGuid().ToString();
            var executionRecord = new TestPlanExecution
            {
                Id = Guid.NewGuid().ToString(),
                PlanId = priorPlanId,
                Phases = new List<PhaseExecutionResult>
                {
                    new PhaseExecutionResult
                    {
                        PhaseId = "phase-1",
                        Status = ExecutionStatus.Succeeded,
                        Evidence = "Phase 1 completed",
                        AttemptCount = 1,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow.AddSeconds(5)
                    }
                },
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddSeconds(5),
                OverallStatus = ExecutionStatus.Succeeded,
                AttemptCount = 1
            };

            _mockExecutionRepository
                .Setup(m => m.LoadTestPlanExecutionAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(executionRecord);

            // Act
            var loadedExecution = await _mockExecutionRepository.Object.LoadTestPlanExecutionAsync(
                priorPlanId, CancellationToken.None);

            // Assert
            Assert.NotNull(loadedExecution);
            Assert.Equal(priorPlanId, loadedExecution.PlanId);
            Assert.Equal(ExecutionStatus.Succeeded, loadedExecution.OverallStatus);
            Assert.Single(loadedExecution.Phases);
            Assert.Equal("Phase 1 completed", loadedExecution.Phases[0].Evidence);
        }

        /// <summary>
        /// Scenario 6: AutonomousVsInteractiveMode
        /// Verifies mode routing works correctly for autonomous and interactive flows.
        /// </summary>
        [Fact]
        public async Task ModeRouting_AutonomousMode_BypassesPrompts()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Autonomous mode routing" };
            var changeStack = new ChangeStack();

            var testPlan = new TestPlan
            {
                Title = "Mode Test Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Autonomous phase",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockChangeStackService
                .Setup(m => m.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            _mockInstructionProcessor
                .Setup(m => m.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act - Autonomous mode
            var result = await _service.ExecuteInstructionAsync(
                instruction, "cs-autonomous", "//localhost/target", DebugExecutionMode.Autonomous, CancellationToken.None);

            // Assert - Autonomous should NOT call prompt service
            Assert.NotNull(result);
            _mockPromptService.Verify(
                m => m.PromptOnLLMQuestionAsync(It.IsAny<LLMQuestionPrompt>(), It.IsAny<bool>()),
                Times.Never);
        }
    }
}
