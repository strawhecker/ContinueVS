using System;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Uniform, state-echo wrapper returned by every <see cref="Services.Interfaces.IDebuggerService"/>
    /// inspection method (gap92_2). Combines the result data with the current
    /// <see cref="DebugSessionState"/> and a benign failure signal, so the LLM tool-loop gets one
    /// predictable shape: <c>{ ok, state, data, reason }</c> — and no inspection call ever throws.
    /// </summary>
    /// <typeparam name="T">The payload type carried on success (<c>Ok == true</c>).</typeparam>
    public sealed class DebugInspectionResult<T>
    {
        /// <summary>
        /// True when the inspection succeeded and <see cref="Data"/> is populated.
        /// </summary>
        public bool Ok { get; }

        /// <summary>
        /// Echo of the debugger <see cref="DebugSessionState"/> captured when the call ran.
        /// Always present, even on failure, so callers stay grounded in the live debugger truth.
        /// </summary>
        public DebugSessionState State { get; }

        /// <summary>
        /// The inspection payload. Meaningful only when <see cref="Ok"/> is true.
        /// </summary>
        public T? Data { get; }

        /// <summary>
        /// Human-readable reason when <see cref="Ok"/> is false (e.g. "requires-break-mode",
        /// "debugger-not-active", "no-data"). Null on success.
        /// </summary>
        public string? Reason { get; }

        private DebugInspectionResult(bool ok, DebugSessionState state, T? data, string? reason)
        {
            Ok = ok;
            State = state ?? throw new ArgumentNullException(nameof(state));
            Data = data;
            Reason = reason;
        }

        /// <summary>
        /// Creates a successful result carrying <paramref name="data"/>.
        /// </summary>
        public static DebugInspectionResult<T> Success(DebugSessionState state, T data)
            => new DebugInspectionResult<T>(ok: true, state, data, reason: null);

        /// <summary>
        /// Creates a benign failure result with the given <paramref name="reason"/>.
        /// </summary>
        public static DebugInspectionResult<T> Rejected(DebugSessionState state, string reason)
            => new DebugInspectionResult<T>(ok: false, state, default, reason);
    }
}
