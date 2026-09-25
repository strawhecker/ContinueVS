using System;
using System.Collections;
using System.Collections.Generic;
using EnvDTE;

namespace ContinueVS.Tests.Services.Fakes
{
    // Hand-rolled fakes for the EnvDTE debugger COM interfaces so DebuggerInterop / DebugStateGuard
    // can be unit tested without a live Visual Studio process. EnvDTE uses COM-style method-based
    // indexers (Item(object)) and covariant Parent/Collection returns; explicit per-member
    // implementation is required. Only the members the interop touches are backed by real state;
    // the rest throw NotImplementedException to surface accidental coupling.

    internal sealed class FakeExpression : Expression
    {
        public string NameValue { get; set; } = string.Empty;
        public string ValueValue { get; set; } = "?";
        public string TypeValue { get; set; } = string.Empty;
        public bool ThrowOnValue { get; set; }

        string Expression.Name => NameValue;
        string Expression.Type => TypeValue;
        string Expression.Value { get { if (ThrowOnValue) throw new System.Runtime.InteropServices.COMException("boom"); return ValueValue; } set { ValueValue = value; } }
        bool Expression.IsValidValue => true;
        Expressions Expression.DataMembers => throw new NotImplementedException();
        Expressions Expression.Collection => throw new NotImplementedException();
        DTE Expression.DTE => throw new NotImplementedException();
        Debugger Expression.Parent => throw new NotImplementedException();
    }

    internal sealed class FakeExpressions : Expressions
    {
        private readonly List<Expression> _items = new List<Expression>();
        public void Add(Expression e) => _items.Add(e);
        public IReadOnlyList<Expression> Items => _items;

        int Expressions.Count => _items.Count;
        Expression Expressions.Item(object index) => _items[(int)index - 1];
        IEnumerator Expressions.GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
        DTE Expressions.DTE => throw new NotImplementedException();
        Debugger Expressions.Parent => throw new NotImplementedException();
    }

    internal sealed class FakeStackFrame : StackFrame
    {
        public string FunctionNameValue { get; set; } = "method";
        public FakeExpressions LocalsValue { get; set; } = new FakeExpressions();

        string StackFrame.FunctionName => FunctionNameValue;
        Expressions StackFrame.Locals => LocalsValue;
        Expressions StackFrame.Arguments => throw new NotImplementedException();
        StackFrames StackFrame.Collection => throw new NotImplementedException();
        DTE StackFrame.DTE => throw new NotImplementedException();
        string StackFrame.Language => "CSharp";
        string StackFrame.Module => throw new NotImplementedException();
        EnvDTE.Thread StackFrame.Parent => throw new NotImplementedException();
        string StackFrame.ReturnType => throw new NotImplementedException();
    }

    internal sealed class FakeStackFrames : StackFrames
    {
        private readonly List<StackFrame> _items = new List<StackFrame>();
        public void Add(StackFrame f) => _items.Add(f);
        public IReadOnlyList<StackFrame> Items => _items;

        int StackFrames.Count => _items.Count;
        StackFrame StackFrames.Item(object index) => _items[(int)index - 1];
        IEnumerator StackFrames.GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
        DTE StackFrames.DTE => throw new NotImplementedException();
        Debugger StackFrames.Parent => throw new NotImplementedException();
    }

    internal sealed class FakeThread : EnvDTE.Thread
    {
        public int IDValue { get; set; }
        public FakeStackFrames StackFramesValue { get; set; } = new FakeStackFrames();

        int EnvDTE.Thread.ID => IDValue;
        StackFrames EnvDTE.Thread.StackFrames => StackFramesValue;
        Threads EnvDTE.Thread.Collection => throw new NotImplementedException();
        DTE EnvDTE.Thread.DTE => throw new NotImplementedException();
        bool EnvDTE.Thread.IsAlive => true;
        bool EnvDTE.Thread.IsFrozen => false;
        string EnvDTE.Thread.Location => throw new NotImplementedException();
        string EnvDTE.Thread.Name => throw new NotImplementedException();
        Debugger EnvDTE.Thread.Parent => throw new NotImplementedException();
        string EnvDTE.Thread.Priority => throw new NotImplementedException();
        Program EnvDTE.Thread.Program => throw new NotImplementedException();
        int EnvDTE.Thread.SuspendCount => 0;
        void EnvDTE.Thread.Freeze() { }
        void EnvDTE.Thread.Thaw() { }
    }

    internal sealed class FakeBreakpoint : Breakpoint
    {
        public string NameValue { get; set; } = string.Empty;
        public string FileValue { get; set; } = string.Empty;
        public int FileLineValue { get; set; }
        public bool EnabledValue { get; set; } = true;
        public int CurrentHitsValue { get; set; }
        public string ConditionValue { get; set; } = string.Empty;
        public bool Deleted { get; private set; }

        public FakeBreakpoints Owner { get; set; }

        string Breakpoint.Name { get => NameValue; set => NameValue = value; }
        string Breakpoint.File => FileValue;
        int Breakpoint.FileLine => FileLineValue;
        int Breakpoint.FileColumn => 1;
        bool Breakpoint.Enabled { get => EnabledValue; set => EnabledValue = value; }
        int Breakpoint.CurrentHits => CurrentHitsValue;
        string Breakpoint.Condition => ConditionValue;
        dbgBreakpointConditionType Breakpoint.ConditionType => dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue;
        void Breakpoint.Delete()
        {
            Deleted = true;
            Owner?.Items.Remove(this);
        }
        void Breakpoint.ResetHitCount() { }
        string Breakpoint.Tag { get => string.Empty; set { } }
        string Breakpoint.FunctionName => throw new NotImplementedException();
        int Breakpoint.FunctionColumnOffset => throw new NotImplementedException();
        int Breakpoint.FunctionLineOffset => throw new NotImplementedException();
        int Breakpoint.HitCountTarget => throw new NotImplementedException();
        dbgHitCountType Breakpoint.HitCountType => throw new NotImplementedException();
        string Breakpoint.Language => throw new NotImplementedException();
        dbgBreakpointLocationType Breakpoint.LocationType => throw new NotImplementedException();
        Breakpoint Breakpoint.Parent => throw new NotImplementedException();
        Program Breakpoint.Program => throw new NotImplementedException();
        dbgBreakpointType Breakpoint.Type => throw new NotImplementedException();
        Breakpoints Breakpoint.Collection => throw new NotImplementedException();
        Breakpoints Breakpoint.Children => throw new NotImplementedException();
        DTE Breakpoint.DTE => throw new NotImplementedException();
    }

    internal sealed class FakeBreakpoints : Breakpoints
    {
        public readonly List<FakeBreakpoint> Items = new List<FakeBreakpoint>();

        int Breakpoints.Count => Items.Count;
        Breakpoint Breakpoints.Item(object index) => Items[(int)index - 1];
        IEnumerator Breakpoints.GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
        DTE Breakpoints.DTE => throw new NotImplementedException();
        Debugger Breakpoints.Parent => throw new NotImplementedException();

        Breakpoints Breakpoints.Add(string Function, string File, int Line, int Column, string Condition, dbgBreakpointConditionType ConditionType, string Language, string Data, int DataCount, string Address, int HitCount, dbgHitCountType HitCountType)
        {
            var bp = new FakeBreakpoint
            {
                NameValue = $"{File}:{Line}",
                FileValue = File,
                FileLineValue = Line,
                ConditionValue = Condition,
                EnabledValue = true,
                Owner = this
            };
            Items.Add(bp);
            return this;
        }
    }

    internal sealed class FakeProgram : Program
    {
        public bool IsBeingDebuggedValue { get; set; } = true;

        bool Program.IsBeingDebugged => IsBeingDebuggedValue;
        Programs Program.Collection => throw new NotImplementedException();
        DTE Program.DTE => throw new NotImplementedException();
        string Program.Name => "test-program";
        Debugger Program.Parent => throw new NotImplementedException();
        Process Program.Process => throw new NotImplementedException();
        Threads Program.Threads => throw new NotImplementedException();
    }

    internal sealed class FakeProcess : Process
    {
        public int ProcessIDValue { get; set; } = 7;

        int Process.ProcessID => ProcessIDValue;
        Processes Process.Collection => throw new NotImplementedException();
        DTE Process.DTE => throw new NotImplementedException();
        string Process.Name => "test-process";
        Debugger Process.Parent => throw new NotImplementedException();
        Programs Process.Programs => throw new NotImplementedException();
        void Process.Attach() { }
        void Process.Break(bool WaitForBreakMode) { }
        void Process.Detach(bool WaitForBreakMode) { }
        void Process.Terminate(bool WaitForBreakMode) { }
    }

    /// <summary>
    /// Fake EnvDTE.Debugger used to exercise the debugger state/inspection logic without a live debugger.
    /// </summary>
    internal sealed class FakeDebugger : Debugger
    {
        public dbgDebugMode CurrentModeValue { get; set; } = dbgDebugMode.dbgDesignMode;
        public FakeThread CurrentThreadValue { get; set; } = new FakeThread();
        public FakeStackFrame CurrentStackFrameValue { get; set; } = new FakeStackFrame();
        public FakeBreakpoints BreakpointsValue { get; set; } = new FakeBreakpoints();
        public FakeBreakpoint BreakpointLastHitValue { get; set; }
        public bool CurrentProgramValue { get; set; } = false;
        public int GoCalls { get; private set; }
        public int StepOverCalls { get; private set; }
        public int StepIntoCalls { get; private set; }
        public int StepOutCalls { get; private set; }
        public bool ThrowOnGo { get; set; }

        dbgDebugMode Debugger.CurrentMode => CurrentModeValue;
        EnvDTE.Thread Debugger.CurrentThread { get => CurrentThreadValue; set { } }
        StackFrame Debugger.CurrentStackFrame { get => CurrentStackFrameValue; set { } }
        Program Debugger.CurrentProgram { get => CurrentProgramValue ? new FakeProgram() : null; set { } }
        Breakpoints Debugger.Breakpoints => BreakpointsValue;
        Breakpoint Debugger.BreakpointLastHit => BreakpointLastHitValue;

        void Debugger.Go(bool WaitForBreakOrEnd)
        {
            GoCalls++;
            if (ThrowOnGo) throw new System.Runtime.InteropServices.COMException("go boom");
            CurrentModeValue = dbgDebugMode.dbgRunMode;
        }
        void Debugger.StepOver(bool WaitForBreakOrEnd) { StepOverCalls++; CurrentModeValue = dbgDebugMode.dbgBreakMode; }
        void Debugger.StepInto(bool WaitForBreakOrEnd) { StepIntoCalls++; CurrentModeValue = dbgDebugMode.dbgBreakMode; }
        void Debugger.StepOut(bool WaitForBreakOrEnd) { StepOutCalls++; CurrentModeValue = dbgDebugMode.dbgBreakMode; }
        void Debugger.Break(bool WaitForBreakMode) { CurrentModeValue = dbgDebugMode.dbgBreakMode; }

        Expression Debugger.GetExpression(string ExpressionText, bool UseAutoExpressions, int Timeout)
            => new FakeExpression { NameValue = ExpressionText, ValueValue = "ok" };

        // Unused members throw.
        Breakpoints Debugger.AllBreakpointsLastHit => throw new NotImplementedException();
        Processes Debugger.DebuggedProcesses => throw new NotImplementedException();
        DTE Debugger.DTE => throw new NotImplementedException();
        bool Debugger.HexDisplayMode { get => throw new NotImplementedException(); set { } }
        bool Debugger.HexInputMode { get => throw new NotImplementedException(); set { } }
        Languages Debugger.Languages => throw new NotImplementedException();
        dbgEventReason Debugger.LastBreakReason => throw new NotImplementedException();
        Processes Debugger.LocalProcesses => throw new NotImplementedException();
        DTE Debugger.Parent => throw new NotImplementedException();
        Process Debugger.CurrentProcess { get => throw new NotImplementedException(); set { } }
        void Debugger.DetachAll() => throw new NotImplementedException();
        void Debugger.ExecuteStatement(string Statement, int Timeout, bool IsATestCall) => throw new NotImplementedException();
        void Debugger.RunToCursor(bool WaitForBreakOrEnd) => throw new NotImplementedException();
        void Debugger.SetNextStatement() => throw new NotImplementedException();
        void Debugger.Stop(bool WaitForBreakOrEnd) => throw new NotImplementedException();
        void Debugger.TerminateAll() => throw new NotImplementedException();
    }
}
