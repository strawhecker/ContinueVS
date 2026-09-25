using System;
using System.Linq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Tests.Services.Fakes;
using EnvDTE;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class DebuggerInteropTests
    {
        // ---------- State building ----------

        [Fact]
        public void BuildState_WhenDesignMode_ReturnsBenignEmptyState()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgDesignMode };

            var state = DebuggerInterop.BuildState(d);

            Assert.NotNull(state);
            Assert.False(state.IsRunning);
            Assert.Null(state.CurrentFile);
            Assert.Equal(0, state.CurrentLine);
            Assert.Empty(state.CallStack);
        }

        [Fact]
        public void BuildState_WhenRunMode_IsRunningTrue_NoCallStack()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgRunMode };

            var state = DebuggerInterop.BuildState(d);

            Assert.NotNull(state);
            Assert.True(state.IsRunning);
        }

        [Fact]
        public void BuildState_BreakMode_CollectsCallStackFromCurrentThread()
        {
            var frame0 = new FakeStackFrame { FunctionNameValue = "Outer" };
            var frame1 = new FakeStackFrame { FunctionNameValue = "Inner" };
            var thread = new FakeThread { IDValue = 5 };
            thread.StackFramesValue.Add(frame0);
            thread.StackFramesValue.Add(frame1);

            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentThreadValue = thread
            };

            var state = DebuggerInterop.BuildState(d);

            Assert.NotNull(state);
            Assert.False(state.IsRunning);
            Assert.Equal(2, state.CallStack.Count);
            Assert.Equal("Outer", state.CallStack[0].MethodName);
            Assert.Equal("Inner", state.CallStack[1].MethodName);
            Assert.Equal(0, state.CallStack[0].FrameIndex);
            Assert.Equal(1, state.CallStack[1].FrameIndex);
        }

        [Fact]
        public void BuildState_BreakMode_ReadsCurrentLineFromBreakpointLastHit()
        {
            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                BreakpointLastHitValue = new FakeBreakpoint { FileLineValue = 42 }
            };

            var state = DebuggerInterop.BuildState(d);

            Assert.NotNull(state);
            Assert.Equal(42, state.CurrentLine);
        }

        // ---------- Locals ----------

        [Fact]
        public void CollectLocals_StringifiesLocalValues()
        {
            var frame = new FakeStackFrame();
            frame.LocalsValue.Add(new FakeExpression { NameValue = "x", TypeValue = "int", ValueValue = "42" });
            frame.LocalsValue.Add(new FakeExpression { NameValue = "msg", TypeValue = "string", ValueValue = "\"hi\"" });

            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = frame
            };

            var locals = DebuggerInterop.CollectLocals(d);

            Assert.Equal(2, locals.Count);
            Assert.Equal("int = 42", locals["x"]);
            Assert.Equal("string = \"hi\"", locals["msg"]);
        }

        [Fact]
        public void CollectLocals_WhenExpressionThrows_DoesNotAbortScan()
        {
            var frame = new FakeStackFrame();
            frame.LocalsValue.Add(new FakeExpression { NameValue = "ok", TypeValue = "", ValueValue = "1" });
            frame.LocalsValue.Add(new FakeExpression { NameValue = "bad", ThrowOnValue = true });
            frame.LocalsValue.Add(new FakeExpression { NameValue = "after", TypeValue = "", ValueValue = "2" });

            var d = new FakeDebugger
            {
                CurrentModeValue = dbgDebugMode.dbgBreakMode,
                CurrentStackFrameValue = frame
            };

            var locals = DebuggerInterop.CollectLocals(d);

            Assert.Contains("ok", locals.Keys);
            Assert.Contains("after", locals.Keys);
            Assert.DoesNotContain("bad", locals.Keys);
        }

        [Fact]
        public void CollectLocals_NotPaused_ReturnsEmpty()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgRunMode };
            Assert.Empty(DebuggerInterop.CollectLocals(d));
        }

        // ---------- Breakpoints ----------

        [Fact]
        public void SetBreakpoint_AddsToBreakpoints_ReturnsInfo()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };

            var info = DebuggerInterop.SetBreakpoint(d, @"C:\src\Program.cs", 10, "x > 3");

            Assert.NotNull(info);
            Assert.Equal(@"C:\src\Program.cs", info.FilePath);
            Assert.Equal(10, info.LineNumber);
            Assert.True(info.IsEnabled);
            Assert.Single(d.BreakpointsValue.Items);
            Assert.True(d.BreakpointsValue.Items[0].FileLineValue == 10);
        }

        [Fact]
        public void ClearBreakpoint_DeletesMatchingByFileAndLine()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            DebuggerInterop.SetBreakpoint(d, @"C:\src\Program.cs", 10, null);
            DebuggerInterop.SetBreakpoint(d, @"C:\src\Other.cs", 20, null);

            var cleared = DebuggerInterop.ClearBreakpoint(d, @"C:\src\Program.cs", 10);

            Assert.True(cleared);
            var remaining = d.BreakpointsValue.Items;
            Assert.Single(remaining);
            Assert.Equal("Other.cs", System.IO.Path.GetFileName(remaining[0].FileValue));
        }

        [Fact]
        public void ClearBreakpoint_NoMatch_ReturnsFalse()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            var cleared = DebuggerInterop.ClearBreakpoint(d, @"C:\src\Nope.cs", 99);
            Assert.False(cleared);
        }

        // ---------- Stepping ----------

        [Theory]
        [InlineData(DebugStepAction.StepOver)]
        [InlineData(DebugStepAction.StepInto)]
        [InlineData(DebugStepAction.StepOut)]
        public void ExecuteStep_MapsEnumToCorrectMethod(DebugStepAction action)
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };

            DebuggerInterop.ExecuteStep(d, action);

            Assert.Equal(1, action switch
            {
                DebugStepAction.StepOver => d.StepOverCalls,
                DebugStepAction.StepInto => d.StepIntoCalls,
                DebugStepAction.StepOut => d.StepOutCalls,
                _ => -1
            });
        }

        [Fact]
        public void ExecuteStep_NotPaused_ReturnsEmptyState_NoStepExecuted()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgRunMode };

            var state = DebuggerInterop.ExecuteStep(d, DebugStepAction.StepOver);

            Assert.False(state.IsRunning);
            Assert.Equal(0, d.StepOverCalls);
        }

        [Fact]
        public void ExecuteStep_Continue_IssuesGo()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };

            DebuggerInterop.ExecuteStep(d, DebugStepAction.Continue);

            Assert.Equal(1, d.GoCalls);
        }

        // ---------- Resume ----------

        [Fact]
        public void ResumeExecution_WhenPaused_IssuesGo()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };

            var issued = DebuggerInterop.ResumeExecution(d);

            Assert.True(issued);
            Assert.Equal(1, d.GoCalls);
        }

        [Fact]
        public void ResumeExecution_WhenNotPaused_ReturnsFalse()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgDesignMode };

            var issued = DebuggerInterop.ResumeExecution(d);

            Assert.False(issued);
            Assert.Equal(0, d.GoCalls);
        }

        [Fact]
        public void IsActive_DesignMode_ReturnsFalse()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgDesignMode };
            Assert.False(DebuggerInterop.IsActive(d));
        }

        [Fact]
        public void IsActive_BreakMode_ReturnsTrue()
        {
            var d = new FakeDebugger { CurrentModeValue = dbgDebugMode.dbgBreakMode };
            Assert.True(DebuggerInterop.IsActive(d));
        }
    }
}
