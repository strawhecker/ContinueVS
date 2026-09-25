using ContinueVS.Core.Types;
using ContinueVS.Tests.Services.Fakes;
using EnvDTE;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests the guard-gate / state-echo contract that every IDebuggerService inspection method
    /// delegates to (gap92_2). The service layer itself marshals via ThreadHelper and cannot be
    /// constructed in a unit test without a VS host, so the gating composition it depends on —
    /// <see cref="DebugStateGuard.RequireInspection"/> — is what is exercised here.
    /// </summary>
    public class DebuggerServiceInspectionTests
    {
        // ---------- Paused-gated reads (stack/frame/statement/locals/args/this/exception) ----------

        [Fact]
        public void RequireInspection_PausedGate_WhenPaused_ReturnsOkWithLiveState()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentProgramValue = true,
                CurrentThreadValue = new FakeThread { IDValue = 3 }
            };

            var (ok, state, reason) = DebugStateGuard.RequireInspection(d, InspectionGate.Paused);

            Assert.True(ok);
            Assert.Null(reason);
            Assert.NotNull(state);
            Assert.True(state.IsDebuggerActive);
            Assert.Equal("break", state.Mode);
            Assert.Equal(3, state.ThreadId);
        }

        [Fact]
        public void RequireInspection_PausedGate_WhenRunning_RejectsWithBreakRequirement()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgRunMode };

            var (ok, state, reason) = DebugStateGuard.RequireInspection(d, InspectionGate.Paused);

            Assert.False(ok);
            Assert.Equal("requires-break-mode", reason);
            Assert.NotNull(state);
            Assert.True(state.IsDebuggerActive);
            Assert.Equal("run", state.Mode);
        }

        // ---------- Any-gated reads (threads/modules/process/settings/output) ----------

        [Theory]
        [InlineData(dbgDebugMode.dbgRunMode)]
        [InlineData(dbgDebugMode.dbgBreakMode)]
        public void RequireInspection_AnyGate_RunOrBreak_ReturnsOk(dbgDebugMode mode)
        {
            var d = new FakeDebugger { CurrentModeValue = mode };

            var (ok, state, reason) = DebugStateGuard.RequireInspection(d, InspectionGate.Any);

            Assert.True(ok);
            Assert.Null(reason);
            Assert.True(state.IsDebuggerActive);
        }

        // ---------- Not-active ----------

        [Fact]
        public void RequireInspection_DesignMode_RejectsNotActive()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgDesignMode };

            var (ok, state, reason) = DebugStateGuard.RequireInspection(d, InspectionGate.Any);

            Assert.False(ok);
            Assert.Equal("debugger-not-active", reason);
            Assert.False(state.IsDebuggerActive);
            Assert.Equal("none", state.Mode);
        }

        [Fact]
        public void RequireInspection_Null_RejectsNotActive()
        {
            var (ok, state, reason) = DebugStateGuard.RequireInspection(null, InspectionGate.Paused);

            Assert.False(ok);
            Assert.Equal("debugger-not-active", reason);
            Assert.NotNull(state);
        }

        // ---------- Inspection result shape ----------

        [Fact]
        public void InspectionResult_Success_ExposesOkStateAndData()
        {
            var state = new DebugSessionState { Mode = "break", IsDebuggerActive = true };
            var result = DebugInspectionResult<int>.Success(state, 42);

            Assert.True(result.Ok);
            Assert.Equal(42, result.Data);
            Assert.Null(result.Reason);
            Assert.Same(state, result.State);
        }

        [Fact]
        public void InspectionResult_Rejected_ExposesReasonAndEchoState()
        {
            var state = new DebugSessionState { Mode = "none" };
            var result = DebugInspectionResult<int>.Rejected(state, "nope");

            Assert.False(result.Ok);
            Assert.Equal(0, result.Data);
            Assert.Equal("nope", result.Reason);
            Assert.Same(state, result.State);
        }
    }
}
