#nullable enable

using System;
using System.Threading;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Default deterministic tool-call ID allocator (gap80).
    ///
    /// Base = time-of-day to 100 ns precision; +1 per additional tool in a parallel batch.
    /// A monotonic reset-counter is included so a Reset() immediately followed by Next() is
    /// guaranteed to yield a different base even within the same 100 ns tick.
    /// Wrapped in a lock for thread safety (concurrent streaming batches).
    /// </summary>
    public class ToolCallIdAllocator : IToolCallIdAllocator
    {
        private readonly object _gate = new object();
        private long _baseTicks;
        private long _increment;
        private long _resetCounter;

        /// <inheritdoc />
        public string Next()
        {
            lock (_gate)
            {
                if (_baseTicks == 0)
                {
                    _baseTicks = DateTime.UtcNow.Ticks;
                    _increment = 0;
                }

                // tc-{base 100ns}-{reset generation}-{batch increment}
                string id = $"tc-{_baseTicks:X16}-{_resetCounter:X2}-{_increment:X4}";
                _increment++;
                return id;
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            lock (_gate)
            {
                // Advance the generation so the next base is distinct even if the
                // re-sampled time-of-day tick is identical (guaranteed-unique IDs across resets).
                _resetCounter++;
                _baseTicks = 0;
                _increment = 0;
            }
        }
    }
}
