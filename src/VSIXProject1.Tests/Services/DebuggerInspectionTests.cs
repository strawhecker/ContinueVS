using System;
using System.Linq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Tests.Services.Fakes;
using EnvDTE;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class DebuggerInspectionTests
    {
        private static DebugSessionState BreakState()
            => new DebugSessionState { Mode = "break", IsDebuggerActive = true, ThreadId = 5 };

        private static FakeThread ThreadWithFrames(params string[] functionNames)
        {
            var thread = new FakeThread { IDValue = 5, NameValue = "worker" };
            foreach (var fn in functionNames)
                thread.StackFramesValue.Add(new FakeStackFrame { FunctionNameValue = fn });
            return thread;
        }

        // ---------- Call stack ----------

        [Fact]
        public void GetCallStack_ReturnsFramesForThread()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentThreadValue = ThreadWithFrames("Outer", "Inner")
            };

            var result = DebuggerInterop.GetCallStack(d, BreakState(), 5, 50);

            Assert.True(result.Ok);
            Assert.Equal(2, result.Data.Count);
            Assert.Equal("Outer", result.Data[0].MethodName);
            Assert.Equal("Inner", result.Data[1].MethodName);
            Assert.Equal(0, result.Data[0].FrameIndex);
            Assert.Equal(5, result.Data[0].ThreadId);
        }

        [Fact]
        public void GetCallStack_InvalidThreadId_Rejected()
        {
            var result = DebuggerInterop.GetCallStack(null, BreakState(), 0, 50);
            Assert.False(result.Ok);
            Assert.Equal("invalid-thread-id", result.Reason);
        }

        // ---------- Frame select ----------

        [Fact]
        public void SelectFrame_ValidFrame_ReturnsOkAndEchoesCursor()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentThreadValue = ThreadWithFrames("Outer", "Inner")
            };

            var result = DebuggerInterop.SelectFrame(d, BreakState(), 5, 1);

            Assert.True(result.Ok);
            Assert.True(result.Data);
            Assert.Equal(5, result.State.ThreadId);
            Assert.Equal("frame:1", result.State.Frame);
        }

        [Fact]
        public void SelectFrame_OutOfRange_Rejected()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentThreadValue = ThreadWithFrames("Only")
            };

            var result = DebuggerInterop.SelectFrame(d, BreakState(), 5, 9);

            Assert.False(result.Ok);
            Assert.Equal("frame-out-of-range", result.Reason);
        }

        // ---------- Statement ----------

        [Fact]
        public void GetStatement_CurrentFrame_UsesBreakpointLastHit()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                BreakpointLastHitValue = new FakeBreakpoint { FileValue = @"C:\p.cs", FileLineValue = 42 },
                CurrentStackFrameValue = new FakeStackFrame { FunctionNameValue = "M" }
            };

            var result = DebuggerInterop.GetStatement(d, BreakState(), 5, 0, 3);

            Assert.True(result.Ok);
            Assert.True(result.Data.IsCurrent);
            Assert.Equal(@"C:\p.cs", result.Data.FilePath);
            Assert.Equal(42, result.Data.LineNumber);
        }

        // ---------- Locals ----------

        [Fact]
        public void GetLocals_ReturnsStringifiedVariables()
        {
            var frame = new FakeStackFrame();
            frame.LocalsValue.Add(new FakeExpression { NameValue = "x", TypeValue = "int", ValueValue = "42" });
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = frame
            };

            var result = DebuggerInterop.GetLocals(d, BreakState(), 5, 0);

            Assert.True(result.Ok);
            var v = Assert.Single(result.Data);
            Assert.Equal("x", v.Name);
            Assert.Equal("int = 42", v.Value);
        }

        // ---------- Arguments ----------

        [Fact]
        public void GetArguments_ReturnsArgumentVariables()
        {
            var frame = new FakeStackFrame();
            frame.ArgumentsValue.Add(new FakeExpression { NameValue = "a", TypeValue = "string", ValueValue = "\"hi\"" });
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = frame
            };

            var result = DebuggerInterop.GetArguments(d, BreakState(), 5, 0);

            Assert.True(result.Ok);
            var v = Assert.Single(result.Data);
            Assert.Equal("a", v.Name);
            Assert.Equal("string = \"hi\"", v.Value);
        }

        // ---------- This ----------

        [Fact]
        public void GetThis_ResolvesViaExpression()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                ThisValue = new FakeExpression { TypeValue = "MyClass", ValueValue = "{MyClass}" },
                CurrentStackFrameValue = new FakeStackFrame()
            };

            var result = DebuggerInterop.GetThis(d, BreakState(), 5, 0);

            Assert.True(result.Ok);
            Assert.Equal("this", result.Data.Name);
            Assert.True(result.Data.IsReadOnly);
        }

        [Fact]
        public void GetThis_NoExpression_Rejected()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                ThisValue = null,
                CurrentStackFrameValue = new FakeStackFrame()
            };

            var result = DebuggerInterop.GetThis(d, BreakState(), 5, 0);

            Assert.False(result.Ok);
            Assert.Equal("this-unavailable", result.Reason);
        }

        // ---------- Threads ----------

        [Fact]
        public void GetThreads_EnumeratesAcrossProcessesAndDeduplicates()
        {
            var program = new FakeProgram();
            var t1 = new FakeThread { IDValue = 1, NameValue = "main", PriorityValue = "Normal", SuspendCountValue = 2, IsFrozenValue = true };
            var t2 = new FakeThread { IDValue = 2, NameValue = "worker" };
            var t3 = new FakeThread { IDValue = 1, NameValue = "dup" }; // duplicate id -> skipped
            program.ThreadsValue.Add(t1);
            program.ThreadsValue.Add(t2);
            program.ThreadsValue.Add(t3);

            var proc = new FakeProcess();
            proc.ProgramsValue.Add(program);

            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                ProcessesValue = FakeProcessesWith(proc)
            };

            var result = DebuggerInterop.GetThreads(d, BreakState());

            Assert.True(result.Ok);
            Assert.Equal(2, result.Data.Count);
            Assert.Contains(result.Data, th => th.Id == 1 && th.Name == "main" && th.IsFrozen && th.SuspendCount == 2);
            Assert.Contains(result.Data, th => th.Id == 2 && th.Name == "worker");
        }

        // ---------- Process ----------

        [Fact]
        public void GetProcessInfo_ReadsCurrentProcess()
        {
            var proc = new FakeProcess { ProcessIDValue = 99, NameValue = "myapp.exe" };
            proc.ProgramsValue.Add(new FakeProgram());
            proc.ProgramsValue.Add(new FakeProgram());

            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgRunMode,
                CurrentProcessValue = proc
            };

            var result = DebuggerInterop.GetProcessInfo(d, BreakState());

            Assert.True(result.Ok);
            Assert.Equal(99, result.Data.Id);
            Assert.Equal("myapp.exe", result.Data.Name);
            Assert.Equal(2, result.Data.ProgramCount);
        }

        [Fact]
        public void GetProcessInfo_NoProcess_Rejected()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgRunMode, CurrentProcessValue = null };
            var result = DebuggerInterop.GetProcessInfo(d, BreakState());
            Assert.False(result.Ok);
            Assert.Equal("no-process", result.Reason);
        }

        // ---------- Not-exposed-by-DTE ----------

        [Theory]
        [InlineData("modules")]
        [InlineData("exception-settings")]
        [InlineData("output")]
        public void EnvDtoUnsupported_ReturnsBenignRejection(string kind)
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };

            switch (kind)
            {
                case "modules":
                    var m = DebuggerInterop.GetModules(d, BreakState());
                    Assert.False(m.Ok);
                    Assert.Equal("not-exposed-by-dte", m.Reason);
                    break;
                case "exception-settings":
                    var e = DebuggerInterop.ListExceptionSettings(d, BreakState());
                    Assert.False(e.Ok);
                    Assert.Equal("not-exposed-by-dte", e.Reason);
                    break;
                case "output":
                    var o = DebuggerInterop.GetOutput(d, BreakState(), 100);
                    Assert.False(o.Ok);
                    Assert.Equal("not-exposed-by-dte", o.Reason);
                    break;
            }
        }

        // ---------- Current exception ----------

        [Fact]
        public void GetCurrentException_UsesBreakpointLastHit()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                BreakpointLastHitValue = new FakeBreakpoint { FunctionNameValue = "System.NullReferenceException", FileValue = @"C:\p.cs", FileLineValue = 3 }
            };

            var result = DebuggerInterop.GetCurrentException(d, BreakState());

            Assert.True(result.Ok);
            Assert.Equal("System.NullReferenceException", result.Data.Name);
        }

        [Fact]
        public void GetCurrentException_NoHit_Rejected()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var result = DebuggerInterop.GetCurrentException(d, BreakState());
            Assert.False(result.Ok);
            Assert.Equal("no-exception", result.Reason);
        }

        // ---------- Breakpoint lifecycle ----------

        [Fact]
        public void EnableDisableConditionClear_ById()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            DebuggerInterop.SetBreakpoint(d, @"C:\p.cs", 10, "x > 1");

            var bp = d.BreakpointsValue.Items[0];
            var id = bp.NameValue;

            var dis = DebuggerInterop.DisableBreakpoint(d, BreakState(), id);
            Assert.True(dis.Ok && dis.Data);
            Assert.False(bp.EnabledValue);

            var enable = DebuggerInterop.EnableBreakpoint(d, BreakState(), id);
            Assert.True(enable.Ok && enable.Data);
            Assert.True(bp.EnabledValue);

            // ConditionBreakpoint deletes + recreates the breakpoint (EnvDTE Condition is read-only).
            var cond = DebuggerInterop.ConditionBreakpoint(d, BreakState(), id, "x > 5");
            Assert.True(cond.Ok && cond.Data);
            var recreated = d.BreakpointsValue.Items[0];
            Assert.Equal("x > 5", recreated.ConditionValue);
            Assert.True(bp.Deleted); // original was deleted during recreate

            var clear = DebuggerInterop.ClearBreakpointById(d, BreakState(), recreated.NameValue);
            Assert.True(clear.Ok && clear.Data);
            Assert.True(recreated.Deleted);
        }

        [Fact]
        public void EnableBreakpoint_NotFound_Rejected()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var result = DebuggerInterop.EnableBreakpoint(d, BreakState(), "nope");
            Assert.False(result.Ok);
            Assert.Equal("breakpoint-not-found", result.Reason);
        }

        // helpers
        private static FakeProcesses FakeProcessesWith(params FakeProcess[] processes)
        {
            var ps = new FakeProcesses();
            foreach (var p in processes) ps.Add(p);
            return ps;
        }
    }
}
