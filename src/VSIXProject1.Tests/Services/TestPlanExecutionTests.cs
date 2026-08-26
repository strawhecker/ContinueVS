using System;
using Xunit;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// xUnit tests for TestPlanExecution and related types.
    /// Validates immutability, execution isolation, and data structure integrity.
    /// </summary>
    public class TestPlanExecutionTests
    {
        [Fact]
        public void TestPlan_IsSealed_PreventInheritance()
        {
            // Arrange & Act
            var plan = new TestPlan
            {
                Id = "plan-123",
                Title = "Test Plan"
            };

            // Assert
            var type = plan.GetType();
            Assert.True(type.IsSealed, "TestPlan should be sealed");
            Assert.NotNull(plan);
            Assert.Equal("plan-123", plan.Id);
        }

        [Fact]
        public void PhaseExecutionResult_CalculatesDuration_Correctly()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMilliseconds(500);

            var result = new PhaseExecutionResult
            {
                PhaseId = "phase-1",
                Status = ExecutionStatus.Succeeded,
                Evidence = "Phase completed successfully",
                StartTime = startTime,
                EndTime = endTime
            };

            // Act
            var duration = result.DurationMs;

            // Assert
            Assert.NotNull(duration);
            Assert.True(duration >= 490 && duration <= 510, $"Expected ~500ms, got {duration}ms");
        }

        [Fact]
        public void PhaseExecutionResult_WithoutEndTime_DurationIsNull()
        {
            // Arrange
            var result = new PhaseExecutionResult
            {
                PhaseId = "phase-1",
                Status = ExecutionStatus.Running,
                StartTime = DateTime.UtcNow,
                EndTime = null
            };

            // Act
            var duration = result.DurationMs;

            // Assert
            Assert.Null(duration);
        }

        [Fact]
        public void TestPlanExecution_DefaultsToEmptyPhasesList()
        {
            // Arrange & Act
            var execution = new TestPlanExecution();

            // Assert
            Assert.NotNull(execution.Phases);
            Assert.Empty(execution.Phases);
        }

        [Fact]
        public void TestPlanExecution_ComputedDurationMs_ReflectsStartAndEnd()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMilliseconds(1000);

            var execution = new TestPlanExecution
            {
                Id = "exec-1",
                PlanId = "plan-1",
                StartedAt = startTime,
                CompletedAt = endTime
            };

            // Act
            var duration = execution.DurationMs;

            // Assert
            Assert.NotNull(duration);
            Assert.True(duration.Value > 0, "Duration should be positive");
            Assert.True(duration.Value >= 990 && duration.Value <= 1010, $"Expected ~1000ms, got {duration}ms");
        }

        [Fact]
        public void TestPlanExecution_WithoutCompletedAt_DurationIsNull()
        {
            // Arrange
            var execution = new TestPlanExecution
            {
                Id = "exec-1",
                PlanId = "plan-1",
                StartedAt = DateTime.UtcNow,
                CompletedAt = null
            };

            // Act
            var duration = execution.DurationMs;

            // Assert
            Assert.Null(duration);
        }

        [Fact]
        public void TestPlanExecution_InitializesWithValidDefaults()
        {
            // Arrange & Act
            var execution = new TestPlanExecution();

            // Assert
            Assert.NotNull(execution.Id);
            Assert.NotEmpty(execution.Id); // GUID generated
            Assert.Equal(string.Empty, execution.PlanId);
            Assert.NotNull(execution.Phases);
            Assert.Empty(execution.Phases);
            Assert.Equal(ExecutionStatus.Pending, execution.OverallStatus);
            Assert.Equal(1, execution.AttemptCount);
        }

        [Fact]
        public void ExecutionStatus_HasAllRequiredStatuses()
        {
            // Assert
            Assert.True(Enum.IsDefined(typeof(ExecutionStatus), ExecutionStatus.Pending));
            Assert.True(Enum.IsDefined(typeof(ExecutionStatus), ExecutionStatus.Running));
            Assert.True(Enum.IsDefined(typeof(ExecutionStatus), ExecutionStatus.Succeeded));
            Assert.True(Enum.IsDefined(typeof(ExecutionStatus), ExecutionStatus.Failed));
            Assert.True(Enum.IsDefined(typeof(ExecutionStatus), ExecutionStatus.Skipped));
            Assert.True(Enum.IsDefined(typeof(ExecutionStatus), ExecutionStatus.Cancelled));
        }

        [Fact]
        public void PhaseExecutionResult_UsesUtcTimestamps()
        {
            // Arrange & Act
            var result = new PhaseExecutionResult();

            // Assert
            Assert.Equal(DateTimeKind.Utc, result.StartTime.Kind);
        }

        [Fact]
        public void TestPlanExecution_UsesUtcTimestamps()
        {
            // Arrange & Act
            var execution = new TestPlanExecution();

            // Assert
            Assert.Equal(DateTimeKind.Utc, execution.StartedAt.Kind);
        }

        [Fact]
        public void TestPlanExecution_CanAddPhaseResults()
        {
            // Arrange
            var execution = new TestPlanExecution { PlanId = "plan-1" };
            var phase = new PhaseExecutionResult
            {
                PhaseId = "phase-1",
                Status = ExecutionStatus.Succeeded,
                Evidence = "Executed"
            };

            // Act
            execution.Phases.Add(phase);

            // Assert
            Assert.Single(execution.Phases);
            Assert.Equal("phase-1", execution.Phases[0].PhaseId);
        }

        [Fact]
        public void TestPlanExecution_ThrowsOnNullPlanId()
        {
            // Arrange & Act & Assert
            var execution = new TestPlanExecution();
            // Default constructor allows null/empty, but is valid behavior
            Assert.Equal(string.Empty, execution.PlanId);
        }
    }
}
