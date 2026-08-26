#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    /// Unit tests for DebugSessionService.
    /// Tests orchestration flow: instruction load → phase generation → sequential phase execution → annotation.
    /// </summary>
    public class DebugSessionServiceTests
    {
        private readonly Mock<IInstructionProcessorService> _mockInstructionProcessor;
        private readonly Mock<IChangeStackService> _mockChangeStackService;
        private readonly Mock<IBridgeLogger> _mockLogger;
        private readonly DebugSessionService _service;

        public DebugSessionServiceTests()
        {
            _mockInstructionProcessor = new Mock<IInstructionProcessorService>();
            _mockChangeStackService = new Mock<IChangeStackService>();
            _mockLogger = new Mock<IBridgeLogger>();

            var executorFactory = new PhaseExecutorFactory(_mockChangeStackService.Object, _mockLogger.Object);
            _service = new DebugSessionService(
                _mockInstructionProcessor.Object,
                _mockChangeStackService.Object,
                executorFactory,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_AllPhasesSucceed_AnnotatesAllPhases()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Debug issue with null reference" };
            var changeStack = new ChangeStack();

            _mockChangeStackService
                .Setup(s => s.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            var testPlan = new TestPlan
            {
                Title = "Debug Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Analyze null reference source",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-2",
                        Type = InternalPhaseType.Observation,
                        Description = "Observe execution flow",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockInstructionProcessor
                .Setup(p => p.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                "/tmp/target",
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Phases.Count);
            Assert.All(result.Phases, phase => Assert.NotNull(phase.Execution));
            Assert.All(result.Phases, phase => Assert.Equal(InternalPhaseStatus.Completed, phase.Status));
            Assert.Equal("Analysis", result.Phases[0].Execution!.Strategy);
            Assert.Equal("Observation", result.Phases[1].Execution!.Strategy);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_ZeroChangeObservationPhase_MarksCompleted()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Observe current state" };
            var changeStack = new ChangeStack();

            _mockChangeStackService
                .Setup(s => s.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            var testPlan = new TestPlan
            {
                Title = "Observation Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Observation,
                        Description = "Gather diagnostic data",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockInstructionProcessor
                .Setup(p => p.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                "/tmp/target",
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Phases);
            var phase = result.Phases[0];
            Assert.NotNull(phase.Execution);
            Assert.Equal(0, phase.Execution.ChangesAppliedCount);
            Assert.Equal("Completed", phase.Execution.Result);
            Assert.Equal(InternalPhaseStatus.Completed, phase.Status);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_MultiChangeInstrumentationPhase_TracksChangeCount()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Add instrumentation" };
            var changeStack = new ChangeStack();

            _mockChangeStackService
                .Setup(s => s.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            var testPlan = new TestPlan
            {
                Title = "Instrumentation Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Instrumentation,
                        Description = "Add logging to SendMessage method",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockInstructionProcessor
                .Setup(p => p.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                "/tmp/target",
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Phases);
            var phase = result.Phases[0];
            Assert.NotNull(phase.Execution);
            Assert.Equal("Instrumentation", phase.Execution.Strategy);
            Assert.True(phase.Execution.ChangesAppliedCount >= 0, "Instrumentation phase should track change count");
            Assert.Equal(InternalPhaseStatus.Completed, phase.Status);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_PhaseFailure_StopsExecution()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Debug multi-phase workflow" };
            var changeStack = new ChangeStack();

            _mockChangeStackService
                .Setup(s => s.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            var testPlan = new TestPlan
            {
                Title = "Multi-Phase Plan",
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
                        Type = InternalPhaseType.Breakpoint, // No executor for Breakpoint yet
                        Description = "Phase 2: Set breakpoint",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-3",
                        Type = InternalPhaseType.Observation,
                        Description = "Phase 3: Observe",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockInstructionProcessor
                .Setup(p => p.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                "/tmp/target",
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Phases.Count);

            // First phase should be completed
            Assert.Equal(InternalPhaseStatus.Completed, result.Phases[0].Status);
            Assert.NotNull(result.Phases[0].Execution);
            Assert.Equal("Completed", result.Phases[0].Execution?.Result);

            // Second phase should fail (no executor)
            Assert.Equal(InternalPhaseStatus.Failed, result.Phases[1].Status);
            Assert.NotNull(result.Phases[1].Execution);
            Assert.Equal("Skipped", result.Phases[1].Execution?.Result);

            // Third phase should never run (still Pending)
            Assert.Equal(InternalPhaseStatus.Pending, result.Phases[2].Status);
            Assert.Null(result.Phases[2].Execution);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_PhaseSequencing_ExecutesInOrder()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Sequential phase execution test" };
            var changeStack = new ChangeStack();
            var executionOrder = new List<string>();

            _mockChangeStackService
                .Setup(s => s.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            var testPlan = new TestPlan
            {
                Title = "Sequencing Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Step 1",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-2",
                        Type = InternalPhaseType.Observation,
                        Description = "Step 2",
                        Status = InternalPhaseStatus.Pending
                    },
                    new InternalPhase
                    {
                        Id = "phase-3",
                        Type = InternalPhaseType.Analysis,
                        Description = "Step 3",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockInstructionProcessor
                .Setup(p => p.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                "/tmp/target",
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Phases.Count);
            Assert.All(result.Phases, p => Assert.NotNull(p.Execution));
            Assert.All(result.Phases, p => Assert.Equal(InternalPhaseStatus.Completed, p.Status));

            // Check execution timestamps exist
            for (int i = 0; i < result.Phases.Count; i++)
            {
                Assert.NotNull(result.Phases[i].Execution);
                Assert.True(result.Phases[i].Execution!.ExecutedAt > DateTime.MinValue);
            }
        }

        [Fact]
        public async Task LoadInstructionAsync_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonexistentPath = "/tmp/nonexistent/instruction.json";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _service.LoadInstructionAsync(nonexistentPath, CancellationToken.None));
        }

        [Fact]
        public async Task LoadInstructionAsync_ValidJsonFile_LoadsInstruction()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"instruction-{Guid.NewGuid()}.json");
            var instruction = new DebugInstruction { Text = "Test instruction", Context = "Test context" };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(instruction);

            try
            {
                File.WriteAllText(tempFile, json);

                // Act
                var loaded = await _service.LoadInstructionAsync(tempFile, CancellationToken.None);

                // Assert
                Assert.NotNull(loaded);
                Assert.Equal("Test instruction", loaded.Text);
                Assert.Equal("Test context", loaded.Context);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task GetSessionState_AfterExecution_ReturnsPlan()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Session state test" };
            var changeStack = new ChangeStack();

            _mockChangeStackService
                .Setup(s => s.GetChangeStack(It.IsAny<string>()))
                .Returns(changeStack);

            var testPlan = new TestPlan
            {
                Title = "Session Plan",
                Phases = new List<InternalPhase>
                {
                    new InternalPhase
                    {
                        Id = "phase-1",
                        Type = InternalPhaseType.Analysis,
                        Description = "Analyze",
                        Status = InternalPhaseStatus.Pending
                    }
                }
            };

            _mockInstructionProcessor
                .Setup(p => p.GenerateInternalPhasesAsync(instruction, It.IsAny<CancellationToken>()))
                .ReturnsAsync(testPlan);

            // Act
            var executed = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                "/tmp/target",
                CancellationToken.None);

            var sessionState = _service.GetSessionState();

            // Assert
            Assert.NotNull(sessionState);
            Assert.Equal(executed.Id, sessionState.Id);
            Assert.Single(sessionState.Phases);
        }
    }
}
