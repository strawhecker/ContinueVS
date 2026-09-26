using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Tests.Services.Fakes;
using EnvDTE;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests the gap92_3 evaluate/mutate interop surface against the fake EnvDTE debugger.
    /// </summary>
    public class DebuggerInteropEvaluateMutateTests
    {
        private static DebugSessionState BreakState()
            => new DebugSessionState { Mode = "break", IsDebuggerActive = true, ThreadId = 5 };

        // ---------- Evaluate ----------

        [Fact]
        public void EvaluateExpression_ReturnsEvaluatedVariable()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = new FakeStackFrame()
            };

            var result = DebuggerInterop.EvaluateExpression(d, BreakState(), 5, 0, "x");

            Assert.True(result.Ok);
            Assert.Equal("x", result.Data?.Name);
            Assert.Equal("ok", result.Data?.Value);
            Assert.True(result.Data!.IsReadOnly);
        }

        [Fact]
        public void EvaluateExpression_EmptyExpression_Rejected()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var result = DebuggerInterop.EvaluateExpression(d, BreakState(), 5, 0, "  ");
            Assert.False(result.Ok);
            Assert.Equal("invalid-expression", result.Reason);
        }

        [Fact]
        public void EvaluateExpression_InvalidExpression_Rejected()
        {
            // FakeDebugger.GetExpression returns a valid expression for any non-"this" text,
            // so to simulate an invalid expression we force the frame to be unavailable.
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentThreadValue = new FakeThread { IDValue = 5 }
            };
            var result = DebuggerInterop.EvaluateExpression(d, BreakState(), 5, 99, "x");
            Assert.False(result.Ok);
            Assert.Equal("frame-unavailable", result.Reason);
        }

        // ---------- SetValue ----------

        [Fact]
        public void SetValue_WritesToMatchingLocal()
        {
            var frame = new FakeStackFrame();
            frame.LocalsValue.Add(new FakeExpression { NameValue = "x", TypeValue = "int", ValueValue = "1" });
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = frame
            };

            var result = DebuggerInterop.SetValue(d, BreakState(), 5, 0, "x", "42");

            Assert.True(result.Ok);
            Assert.True(result.Data);
            var writtenLocal = Assert.IsType<FakeExpression>(frame.LocalsValue.Items[0]);
            Assert.Equal("42", writtenLocal.ValueValue);
        }

        [Fact]
        public void SetValue_WritesToMatchingArgument()
        {
            var frame = new FakeStackFrame();
            frame.ArgumentsValue.Add(new FakeExpression { NameValue = "a", TypeValue = "string", ValueValue = "\"old\"" });
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = frame
            };

            var result = DebuggerInterop.SetValue(d, BreakState(), 5, 0, "a", "\"new\"");

            Assert.True(result.Ok);
            var writtenArg = Assert.IsType<FakeExpression>(frame.ArgumentsValue.Items[0]);
            Assert.Equal("\"new\"", writtenArg.ValueValue);
        }

        [Fact]
        public void SetValue_MissingVariable_Rejected()
        {
            var frame = new FakeStackFrame();
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = frame
            };

            var result = DebuggerInterop.SetValue(d, BreakState(), 5, 0, "nope", "1");

            Assert.False(result.Ok);
            Assert.Equal("variable-not-found", result.Reason);
        }

        [Fact]
        public void SetValue_EmptyName_Rejected()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var result = DebuggerInterop.SetValue(d, BreakState(), 5, 0, "  ", "1");
            Assert.False(result.Ok);
            Assert.Equal("invalid-name", result.Reason);
        }

        // ---------- Memory (benign not-exposed-by-dte) ----------

        [Fact]
        public void MemoryRead_ReportsBenignNotExposedByDte()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var result = DebuggerInterop.MemoryRead(d, BreakState(), "0x1234", 16);
            Assert.False(result.Ok);
            Assert.Equal("not-exposed-by-dte", result.Reason);
            Assert.Equal("break", result.State.Mode);
        }

        [Fact]
        public void MemoryWrite_ReportsBenignNotExposedByDte()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var result = DebuggerInterop.MemoryWrite(d, BreakState(), "0x1234", "AA");
            Assert.False(result.Ok);
            Assert.Equal("not-exposed-by-dte", result.Reason);
        }

        // ---------- Freeze / Thaw ----------

        [Fact]
        public void FreezeThread_ThawsThread_FlipsIsFrozen()
        {
            var thread = new FakeThread { IDValue = 5, IsFrozenValue = false };
            var program = new FakeProgram();
            program.ThreadsValue.Add(thread);
            var proc = new FakeProcess();
            proc.ProgramsValue.Add(program);
            var ps = new FakeProcesses();
            ps.Add(proc);

            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                ProcessesValue = ps
            };

            var freezed = DebuggerInterop.FreezeThread(d, BreakState(), 5);
            Assert.True(freezed.Ok && freezed.Data);
            Assert.True(thread.IsFrozenValue);

            var thawed = DebuggerInterop.ThawThread(d, BreakState(), 5);
            Assert.True(thawed.Ok && thawed.Data);
            Assert.False(thread.IsFrozenValue);
        }

        [Fact]
        public void FreezeThread_UnknownThread_Rejected()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode, ProcessesValue = new FakeProcesses() };
            var result = DebuggerInterop.FreezeThread(d, BreakState(), 999);
            Assert.False(result.Ok);
            Assert.Equal("thread-not-found", result.Reason);
        }

        [Fact]
        public void FreezeThread_InvalidThreadId_Rejected()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var result = DebuggerInterop.FreezeThread(d, BreakState(), 0);
            Assert.False(result.Ok);
            Assert.Equal("invalid-thread-id", result.Reason);
        }

        // ---------- RunToCursor ----------

        [Fact]
        public void RunToCursor_IssuesCommandAndEchoesRunMode()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };

            var result = DebuggerInterop.RunToCursor(d, BreakState());

            Assert.True(result.Ok);
            Assert.Equal(1, d.RunToCursorCalls);
            Assert.Equal("run", result.State.Mode);
        }
    }
}
