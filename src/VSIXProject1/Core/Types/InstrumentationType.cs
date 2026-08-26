namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration of instrumentation strategies that the LLM can apply.
    /// </summary>
    public enum InstrumentationType
    {
        /// <summary>Console.WriteLine for quick debugging output.</summary>
        ConsoleLog,

        /// <summary>Debug.Assert for invariant checking.</summary>
        DebugAssert,

        /// <summary>Null guard clause (if (x == null) throw/return).</summary>
        NullCheck,

        /// <summary>Try-catch wrapper for exception handling and logging.</summary>
        TryCatchWrapper,

        /// <summary>Structured logging statement via ILogger or similar.</summary>
        LoggingStatement
    }
}
