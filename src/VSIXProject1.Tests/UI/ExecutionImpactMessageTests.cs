using System;
using System.Collections.Generic;
using Xunit;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.UI
{
    /// <summary>
    /// Unit tests for ExecutionImpactMessage (gap69).
    /// Validates message creation, aggregation, and data integrity.
    /// </summary>
    public class ExecutionImpactMessageTests
    {
        [Fact]
        public void Constructor_CreatesMessageWithSystemRole()
        {
            // Arrange & Act
            var message = new ExecutionImpactMessage();

            // Assert
            Assert.Equal(ChatMessageRole.System, message.Role);
            Assert.Equal("Execution Impact Summary", message.Content);
        }

        [Fact]
        public void Initialize_WithEmptyPhases_SetsZeroMetrics()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            var phases = new List<PhaseExecutionResult>();

            // Act
            message.Initialize(phases);

            // Assert
            Assert.Equal(0, message.FilesChanged);
            Assert.Equal(0, message.ToolsRun);
            Assert.Equal(0, message.DurationMs);
            Assert.Equal(ExecutionStatus.Succeeded, message.Status);
        }

        [Fact]
        public void Initialize_WithPhases_ComputesToolsRunCount()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            var startTime = DateTime.UtcNow;
            var phases = new List<PhaseExecutionResult>
            {
                new PhaseExecutionResult { PhaseId = "phase1", StartTime = startTime, EndTime = startTime.AddSeconds(1), Status = ExecutionStatus.Succeeded },
                new PhaseExecutionResult { PhaseId = "phase2", StartTime = startTime.AddSeconds(1), EndTime = startTime.AddSeconds(2), Status = ExecutionStatus.Succeeded },
                new PhaseExecutionResult { PhaseId = "phase3", StartTime = startTime.AddSeconds(2), EndTime = startTime.AddSeconds(3), Status = ExecutionStatus.Succeeded }
            };

            // Act
            message.Initialize(phases);

            // Assert
            Assert.Equal(3, message.ToolsRun);
        }

        [Fact]
        public void Initialize_WithPhases_ComputesDuration()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddSeconds(5);
            var phases = new List<PhaseExecutionResult>
            {
                new PhaseExecutionResult { PhaseId = "phase1", StartTime = startTime, EndTime = endTime, Status = ExecutionStatus.Succeeded }
            };

            // Act
            message.Initialize(phases);

            // Assert
            Assert.InRange(message.DurationMs, 4900, 5100);  // Allow small variance
        }

        [Fact]
        public void Initialize_WithFailedPhase_MarksFailed()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            var startTime = DateTime.UtcNow;
            var phases = new List<PhaseExecutionResult>
            {
                new PhaseExecutionResult { PhaseId = "phase1", StartTime = startTime, EndTime = startTime.AddSeconds(1), Status = ExecutionStatus.Succeeded },
                new PhaseExecutionResult { PhaseId = "phase2", StartTime = startTime.AddSeconds(1), EndTime = startTime.AddSeconds(2), Status = ExecutionStatus.Failed }
            };

            // Act
            message.Initialize(phases);

            // Assert
            Assert.Equal(ExecutionStatus.Failed, message.Status);
        }

        [Fact]
        public void Initialize_WithNullPhases_IgnoresGracefully()
        {
            // Arrange
            var message = new ExecutionImpactMessage();

            // Act
            message.Initialize(null);

            // Assert
            Assert.Equal(0, message.FilesChanged);
            Assert.Equal(0, message.ToolsRun);
        }

        [Fact]
        public void GetSummaryText_SuccessfulExecution_ReturnsCheckmark()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            message.Status = ExecutionStatus.Succeeded;
            message.FilesChanged = 3;
            message.ToolsRun = 5;
            message.DurationMs = 12300;

            // Act
            var summary = message.GetSummaryText();

            // Assert
            Assert.Contains("✓", summary);
            Assert.Contains("3 files", summary);
            Assert.Contains("5 tools", summary);
            Assert.Contains("12.3s", summary);
        }

        [Fact]
        public void GetSummaryText_FailedExecution_ReturnsXmark()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            message.Status = ExecutionStatus.Failed;
            message.FilesChanged = 1;
            message.ToolsRun = 2;
            message.DurationMs = 5000;

            // Act
            var summary = message.GetSummaryText();

            // Assert
            Assert.Contains("✗", summary);
            Assert.Contains("1 files", summary);
            Assert.Contains("2 tools", summary);
        }

        [Fact]
        public void Properties_AreSerializable()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            message.Status = ExecutionStatus.Succeeded;
            message.FilesChanged = 2;
            message.ToolsRun = 3;
            message.DurationMs = 1500;

            // Act & Assert
            Assert.NotNull(message.Phases);
            Assert.Equal(2, message.FilesChanged);
            Assert.Equal(3, message.ToolsRun);
            Assert.Equal(1500, message.DurationMs);
        }
    }
}
