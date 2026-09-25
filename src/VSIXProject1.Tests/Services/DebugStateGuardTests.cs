using ContinueVS.Core.Types;
using ContinueVS.Tests.Services.Fakes;
using EnvDTE;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class DebugStateGuardTests
    {
        [Fact]
        public void IsDebuggerLive_DesignMode_ReturnsFalse()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgDesignMode };
            Assert.False(DebugStateGuard.IsDebuggerLive(d));
        }

        [Fact]
        public void IsDebuggerLive_BreakAndRun_ReturnsTrue()
        {
            Assert.True(DebugStateGuard.IsDebuggerLive(new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode }));
            Assert.True(DebugStateGuard.IsDebuggerLive(new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgRunMode }));
        }

        [Fact]
        public void IsDebuggerLive_Null_ReturnsFalse()
        {
            Assert.False(DebugStateGuard.IsDebuggerLive(null));
        }

        [Fact]
        public void IsDebuggerPaused_OnlyBreakMode_ReturnsTrue()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            Assert.True(DebugStateGuard.IsDebuggerPaused(d));
        }

        [Fact]
        public void IsDebuggerPaused_RunMode_ReturnsFalse()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgRunMode };
            Assert.False(DebugStateGuard.IsDebuggerPaused(d));
        }

        [Fact]
        public void NotActiveState_ReturnsBenignEcho()
        {
            var state = DebugStateGuard.NotActiveState("debugger-not-active");
            Assert.Equal("none", state.Mode);
            Assert.False(state.IsDebuggerActive);
            Assert.Equal("debugger-not-active", state.BreakReason);
        }

        [Fact]
        public void CaptureState_BreakMode_ReflectsModeAndProgram()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentProgramValue = true,
                CurrentThreadValue = new FakeThread { IDValue = 7 }
            };

            var state = DebugStateGuard.CaptureState(d);

            Assert.Equal("break", state.Mode);
            Assert.True(state.IsDebuggerActive);
            Assert.True(state.IsActiveProgram);
            Assert.True(state.IsBreakMode);
            Assert.Equal(7, state.ThreadId);
        }

        [Fact]
        public void CaptureState_RunMode_IsRunning_NotBreak()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgRunMode,
                CurrentProgramValue = true
            };

            var state = DebugStateGuard.CaptureState(d);

            Assert.Equal("run", state.Mode);
            Assert.False(state.IsBreakMode);
            Assert.True(state.IsDebuggerActive);
        }

        [Fact]
        public void CaptureState_Null_ReturnsBenignEcho()
        {
            var state = DebugStateGuard.CaptureState(null);
            Assert.Equal("none", state.Mode);
            Assert.False(state.IsDebuggerActive);
        }
    }
}
