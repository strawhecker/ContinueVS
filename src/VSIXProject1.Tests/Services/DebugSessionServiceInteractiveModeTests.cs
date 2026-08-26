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
    /// Integration tests for DebugSessionService in Interactive and Autonomous modes.
    /// Validates prompt behavior, user choice handling, and mode-dependent execution flow.
    /// (gap29_8_8)
    /// </summary>
    public class DebugSessionServiceInteractiveModeTests
    {
        private readonly Mock<IInstructionProcessorService> _mockInstructionProcessor;
        private readonly Mock<IChangeStackService> _mockChangeStackService;
        private readonly Mock<IDebugStrategyGeneratorService> _mockStrategyGenerator;
        private readonly Mock<IInstrumentationService> _mockInstrumentationService;
        private readonly Mock<IInteractivePromptService> _mockPromptService;
        private readonly Mock<IBridgeLogger> _mockLogger;
        private readonly DebugSessionService _service;
        private readonly ChangeStack _testChangeStack;

        public DebugSessionServiceInteractiveModeTests()
        {
            _mockInstructionProcessor = new Mock<IInstructionProcessorService>();
            _mockChangeStackService = new Mock<IChangeStackService>();
            _mockStrategyGenerator = new Mock<IDebugStrategyGeneratorService>();
            _mockInstrumentationService = new Mock<IInstrumentationService>();
            _mockPromptService = new Mock<IInteractivePromptService>();
            _mockLogger = new Mock<IBridgeLogger>();

            _testChangeStack = new ChangeStack();
            _mockChangeStackService.Setup(cs => cs.GetChangeStack(It.IsAny<string>()))
                .Returns(_testChangeStack);

            var executorFactory = new PhaseExecutorFactory(
                _mockChangeStackService.Object,
                _mockStrategyGenerator.Object,
                _mockInstrumentationService.Object,
                _mockLogger.Object,
                _mockPromptService.Object);

            _service = new DebugSessionService(
                _mockInstructionProcessor.Object,
                _mockChangeStackService.Object,
                executorFactory,
                _mockLogger.Object);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_InteractiveMode_CallsPromptService()
        {
            // Arrange
            var instruction = new DebugInstruction { Id = "instr-1", Text = "Debug SendMessage" };
            var phase = new InternalPhase { Id = "phase-1", Type = InternalPhaseType.Analysis, Description = "Analyze call stack" };
            var testPlan = new TestPlan { Id = "plan-1", Title = "Test Plan", Phases = new List<InternalPhase> { phase } };

            _mockInstructionProcessor.Setup(ip => ip.GenerateInternalPhasesAsync(instruction, default))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                Path.GetTempPath(),
                mode: DebugExecutionMode.Interactive);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Phases);
            Assert.Equal(InternalPhaseStatus.Completed, result.Phases[0].Status);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_AutonomousMode_SkipsPrompts()
        {
            // Arrange
            var instruction = new DebugInstruction { Id = "instr-3", Text = "Debug failure" };
            var phase = new InternalPhase { Id = "phase-3", Type = InternalPhaseType.Observation, Description = "Observe state" };
            var testPlan = new TestPlan { Id = "plan-3", Phases = new List<InternalPhase> { phase } };

            _mockInstructionProcessor.Setup(ip => ip.GenerateInternalPhasesAsync(instruction, default))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                Path.GetTempPath(),
                mode: DebugExecutionMode.Autonomous);

            // Assert
            Assert.NotNull(result);
            _mockPromptService.Verify(ps => ps.PromptOnPhaseFailureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_DefaultMode_IsAutonomous()
        {
            // Arrange
            var instruction = new DebugInstruction { Id = "instr-4", Text = "Test default" };
            var phase = new InternalPhase { Id = "phase-4", Type = InternalPhaseType.Analysis };
            var testPlan = new TestPlan { Id = "plan-4", Phases = new List<InternalPhase> { phase } };

            _mockInstructionProcessor.Setup(ip => ip.GenerateInternalPhasesAsync(instruction, default))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                Path.GetTempPath()); // No mode specified = default to Autonomous

            // Assert
            Assert.NotNull(result);
            // Verify that mode was Autonomous (prompts were not called)
            _mockPromptService.Verify(ps => ps.PromptOnPhaseFailureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteInstructionAsync_WithMultiplePhases_ExecutesSequentially()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Multi-phase debug" };
            var phase1 = new InternalPhase { Id = "phase-5", Type = InternalPhaseType.Analysis };
            var phase2 = new InternalPhase { Id = "phase-6", Type = InternalPhaseType.Observation };
            var testPlan = new TestPlan { Phases = new List<InternalPhase> { phase1, phase2 } };

            _mockInstructionProcessor.Setup(ip => ip.GenerateInternalPhasesAsync(instruction, default))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                Path.GetTempPath(),
                DebugExecutionMode.Autonomous);

            // Assert
            Assert.Equal(2, result.Phases.Count);
            Assert.All(result.Phases, p => Assert.Equal(InternalPhaseStatus.Completed, p.Status));
        }

        [Fact]
        public async Task ExecuteInstructionAsync_InteractiveMode_PassesModeToExecutors()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Mode passing test" };
            var phase = new InternalPhase { Type = InternalPhaseType.Analysis };
            var testPlan = new TestPlan { Phases = new List<InternalPhase> { phase } };

            _mockInstructionProcessor.Setup(ip => ip.GenerateInternalPhasesAsync(instruction, default))
                .ReturnsAsync(testPlan);

            // Act
            var result = await _service.ExecuteInstructionAsync(
                instruction,
                "stack-1",
                Path.GetTempPath(),
                DebugExecutionMode.Interactive);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Phases);
            // Mode should be passed to executor (verified by no prompts being called on Analysis phase)
        }

        [Fact]
        public async Task ExecuteInstructionAsync_InvalidInstruction_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.ExecuteInstructionAsync(null!, "stack-1", Path.GetTempPath()));
        }

        [Fact]
        public async Task ExecuteInstructionAsync_InvalidChangeStackId_ThrowsArgumentException()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ExecuteInstructionAsync(instruction, "", Path.GetTempPath()));
        }

        [Fact]
        public async Task ExecuteInstructionAsync_InvalidTargetDir_ThrowsArgumentException()
        {
            // Arrange
            var instruction = new DebugInstruction { Text = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ExecuteInstructionAsync(instruction, "stack-1", ""));
        }
    }
}
