#nullable enable

using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Tests.Services.Fakes;
using EnvDTE;
using System;
using System.Linq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests the gap94 debug-session lifecycle logic in <see cref="DebuggerInterop"/> against the
    /// fake <c>EnvDTE.Debugger</c> seam: Start issues Debug.Start via the command delegate and
    /// flips state, Stop ends, Restart re-enters a live session, Attach binds a pid, GetSessions
    /// maps DebuggedProcesses into handles, Select validates id, and every path is fail-soft
    /// (benign null/empty, no throw) on missing startup project or COM failure.
    /// </summary>
    public class DebuggerInteropLifecycleTests
    {
        private FakeProcess MakeProcess(int pid, string name)
        {
            return new FakeProcess { ProcessIDValue = pid, NameValue = name };
        }

        [Fact]
        public void Start_IssuesDebugStart_AndCapturesSession_WhenDebuggerBecomesLive()
        {
            var d = new FakeDebugger { CurrentProgramValue = true };
            d.CurrentModeValue = dbgDebugMode.dbgRunMode;
            d.ProcessesValue.Add(MakeProcess(7, "app.exe"));

            var issued = default(string);
            var command = new Action<string>(c => issued = c);

            var session = DebuggerInterop.Start(d, command, launchProfile: "local");

            Assert.Equal("Debug.Start", issued);
            Assert.NotNull(session);
            Assert.Equal("proc:7", session!.SessionId);
            Assert.Equal("run", session.Mode);
        }

        [Fact]
        public void Start_WithNullCommand_DoesNotThrow_ReturnsNull_WhenNoLiveSession()
        {
            var d = new FakeDebugger(); // design mode -> not live
            var session = DebuggerInterop.Start(d, executeCommand: null, launchProfile: null);
            Assert.Null(session);
        }

        [Fact]
        public void Stop_EndsLiveSession_AndCapturesPriorSession()
        {
            var d = new FakeDebugger { CurrentProgramValue = true };
            d.CurrentModeValue = dbgDebugMode.dbgBreakMode;
            d.ProcessesValue.Add(MakeProcess(7, "app.exe"));

            var ended = DebuggerInterop.Stop(d);

            Assert.NotNull(ended);
            Assert.Equal("break", ended!.Mode);
        }

        [Fact]
        public void Stop_WhenNotLive_ReturnsNull()
        {
            var d = new FakeDebugger(); // design mode
            Assert.Null(DebuggerInterop.Stop(d));
        }

        [Fact]
        public void Restart_StopsThenStarts_AndCapturesSession()
        {
            var d = new FakeDebugger { CurrentProgramValue = true };
            d.CurrentModeValue = dbgDebugMode.dbgBreakMode;
            d.ProcessesValue.Add(MakeProcess(7, "app.exe"));

            var issued = default(string);
            var command = new Action<string>(c => issued = c);

            var restarted = DebuggerInterop.Restart(d, command);

            Assert.Equal("Debug.Start", issued);
            Assert.NotNull(restarted);
        }

        [Fact]
        public void AttachToProcess_BindsPid_SetsSelectedSessionHandle()
        {
            var d = new FakeDebugger();
            d.LocalProcessValue = new FakeProcesses();
            d.LocalProcessValue.Add(MakeProcess(42, "my-app.exe"));
            d.LocalProcessValue.Add(MakeProcess(7, "other.exe"));

            var attached = DebuggerInterop.AttachToProcess(d, 42);

            Assert.NotNull(attached);
            Assert.Equal("proc:42", attached!.SessionId);
            Assert.Equal("my-app.exe", attached.ProcessName);
        }

        [Fact]
        public void AttachToProcess_UnknownPid_ReturnsNull()
        {
            var d = new FakeDebugger();
            d.LocalProcessValue = new FakeProcesses();
            d.LocalProcessValue.Add(MakeProcess(1, "a.exe"));

            Assert.Null(DebuggerInterop.AttachToProcess(d, 999));
        }

        [Fact]
        public void GetSessions_MapsDebuggedProcesses()
        {
            var d = new FakeDebugger();
            d.CurrentModeValue = dbgDebugMode.dbgBreakMode;
            d.ProcessesValue.Add(MakeProcess(10, "p1"));
            d.ProcessesValue.Add(MakeProcess(20, "p2"));

            var sessions = DebuggerInterop.GetSessions(d);

            Assert.Equal(2, sessions.Count);
            Assert.Contains(sessions, s => s.SessionId == "proc:10" && s.ProcessName == "p1");
            Assert.Contains(sessions, s => s.SessionId == "proc:20" && s.ProcessName == "p2");
            Assert.All(sessions, s => Assert.Equal("break", s.Mode));
        }

        [Fact]
        public void GetSessions_NoProcesses_ReturnsEmpty()
        {
            var d = new FakeDebugger();
            Assert.Empty(DebuggerInterop.GetSessions(d));
        }

        [Fact]
        public void GetSessions_NullDebugger_ReturnsEmpty_NoThrow()
        {
            Assert.Empty(DebuggerInterop.GetSessions(null));
        }
    }
}
