# Debug-Session Handler (Step 61)

**Status**: ✅ COMPLETE  
**Module**: `src/versions/v2.0.0/lib/debug-session-handler.mjs`  
**C# Bridge**: `src/VSIXProject1/Editor/DebugSessionCollector.cs`  
**Test**: `src/versions/v2.0.0/tests/debug-session-handler.test.mjs`  
**Mocks**: `src/versions/v2.0.0/tests/mocks/debugger-mock.mjs`

## Purpose

Surfaces active debugger state (paused, running, stopped) along with stack frames and local variables to the Continue WebView. Enables context-aware code suggestions while stepping through code. Integrates with VS IDE debugger via DTE2.DebuggerEvents.

## Message Types

### Query: `bridge:getDebugSession`

Retrieves current debug state and optional stack frames/locals.

**Request**:
```javascript
{
  data: {
    includeStack: boolean,     // Include call stack (default: true)
    includeLocals: boolean,    // Include variables (default: true)
    maxFrames: number          // Max frames 1-50 (default: 20)
  }
}
```

**Response**:
```javascript
{
  success: true,
  data: {
    state: 'paused' | 'running' | 'stopped',
    frame: {
      file: string,
      line: number,
      column: number,
      functionName: string,
      locals: [
        { name: string, value: string, type: string },
        ...
      ]
    } | null,
    stack: [
      { file: string, line: number, functionName: string },
      ...
    ],
    sessionId: string,
    queryTime: number
  }
}
```

### Subscribe: `bridge:onDebugStateChange`

Emitted when debugger enters Run, Break, or Design mode via DTE2.DebuggerEvents.

**Event**:
```javascript
{
  state: 'paused' | 'running' | 'stopped',
  frame: { file, line, functionName, locals } | null,
  sessionId: string,
  timestamp: number
}
```

## Architecture

### C# Bridge: DebugSessionCollector

**File**: `src/VSIXProject1/Editor/DebugSessionCollector.cs` (350 lines)

Hooks DTE2 debugger events in the UI thread:

```csharp
_debuggerEvents.OnEnterRunMode += OnEnterRunMode;       // Execution started
_debuggerEvents.OnEnterBreakMode += OnEnterBreakMode;   // Paused at breakpoint
_debuggerEvents.OnEnterDesignMode += OnEnterDesignMode; // Stopped, no session

// Extract & normalize:
// - Current StackFrame (line, column, function name, locals)
// - Full call stack (up to 20 frames)
// - Local variables & parameters (up to 50)

// Send to bridge:
_control.SendToGui("debugStateChange", {
  state = "paused|running|stopped",
  frame = { file, line, column, functionName, locals },
  stack = [ { file, line, functionName }, ... ],
  sessionId = "uuid"  // Changes on run/stop
});
```

**Integration**:
- Registered in `EditorContextProvider.RegisterAsync()` (Step 48)
- Disposed in `EditorContextProvider.Dispose()`
- Debounced: max 10 events/sec (100ms intervals)
- Gracefully degrades if DTE unavailable

### JavaScript Handler: DebugSessionHandler

**File**: `src/versions/v2.0.0/lib/debug-session-handler.mjs` (~400 lines)

**Responsibilities**:
1. Cache debug state & frames (LRU, 5-min TTL)
2. Normalize states (Design→stopped, Run→running, Break→paused)
3. Process queries from WebView
4. Emit subscriptions on state changes

**Public API**:
```javascript
class DebugSessionHandler {
  // Query handler (called by WebView)
  async handle(message) → {
    success: boolean,
    data: { state, frame, stack, sessionId, queryTime }
  }

  // Receive push from C# bridge
  async onDebugStateChangeMessage(message) → void

  // Subscribe to state changes
  onDebugStateChange(callback) → unsubscribe()

  // Register with message dispatcher
  async registerMessageHandlers(server) → void

  // Cleanup
  dispose() → void

  // Diagnostics
  getCacheStats() → { hits, misses, evictions, size }
}
```

## Cache Strategy

**LRU Cache** with Time-To-Live:
- **Capacity**: Max 100 entries
- **Key**: `sessionId:frameIndex`
- **TTL**: 5 minutes (auto-evict after inactivity)
- **Eviction**: LRU entry removed when full
- **Clear**: Full clear on session ID change (new debug session)

**Rationale**: Stack frame extraction is expensive (DTE API calls), cache prevents repeated queries during rapid stepping cycles.

## State Normalization

Mapping VS debugger states to Continue states:

| VS State (DTE) | Continue State | Meaning |
|---|---|---|
| Design Mode | stopped | Debugger not running, no session |
| Run Mode | running | Execution in progress, no breakpoint |
| Break Mode | paused | Paused at breakpoint or step |

## Error Handling

| Condition | Handler Response | Robustness |
|---|---|---|
| Invalid state value (e.g., "invalid") | Throw StateValidationError | Fail fast |
| Missing frame (paused but no stack) | Return null frame | Graceful |
| Corrupted locals (malformed entries) | Skip invalid, return valid | Skip corrupt |
| Large local value (>10KB) | Include as-is | No truncation |
| DTE unavailable (Step 48 failed) | Return stopped state | Graceful fallback |
| Cache TTL expired | Re-query on next request | Auto-refresh |
| Listener throws during emit | Catch & log, continue | Robustness |

## Performance Targets

| Metric | Target | Notes |
|---|---|---|
| Query latency (cache hit) | <5ms | LRU lookup + serialization |
| Query latency (cache miss) | <50ms | Frame extraction + caching |
| Subscription emit rate | ≤10/sec | Debounced on IDE side (100ms) |
| Memory per frame | ~1KB | file path, line, locals array |
| Max cache entries | 100 | LRU eviction beyond limit |
| Cache hit rate | >85% | Typical stepping patterns |

## Integration Points

### Upstream (C# → JavaScript)

- Receives `debugStateChange` messages from DebugSessionCollector
- Updates internal state: `currentState`, `currentFrame`, `currentStack`, `currentSessionId`
- Caches frame data for repeated queries during stepping
- Detects session changes (session ID differs) → clears cache

### Downstream (JavaScript → WebView)

- Responds to `bridge:getDebugSession` queries with current state
- Emits `bridge:onDebugStateChange` subscription events on state transitions
- WebView displays "Debugging file.cs:42" context info
- WebView uses stack/locals for context-aware code suggestions

### Related Steps

- **Step 45** (BridgeLifecycleManager): Initializes bridge startup
- **Step 48** (EditorContextProvider): Hosts DebugSessionCollector (C# side)
- **Step 60** (TestExplorerHandler): Similar stateful handler with caching
- **Step 71** (Handler Registration): Registers handler with dispatcher
- **Step 75** (E2E Tests): Validates debug-session in WebView integration

## Testing

### Test Coverage

30+ comprehensive tests across 10 suites:

1. **Initialization** (4 tests)
   - Default options
   - Custom logger/metrics
   - Cache options
   - No-op logger fallback

2. **Cache Management** (3 tests)
   - Frame caching on pause
   - Cache clearing on session change
   - TTL expiry behavior

3. **State Normalization** (4 tests)
   - Design → stopped
   - Run → running
   - Break → paused with frame
   - Invalid state rejection

4. **Frame Extraction** (3 tests)
   - Locals extraction
   - Many locals (50+)
   - Null locals handling

5. **Query Handler** (6 tests)
   - State query response
   - Stack inclusion/exclusion
   - maxFrames limit enforcement
   - Local stripping (includeLocals=false)
   - Invalid maxFrames rejection
   - Metric recording

6. **Subscriptions** (3 tests)
   - Listener registration
   - Multiple listener support
   - Unsubscribe mechanism

7. **Error Handling** (2 tests)
   - Missing message data
   - Large value handling

8. **State Transitions** (2 tests)
   - Transition sequence handling
   - Session change detection

9. **Edge Cases** (2 tests)
   - Empty stack
   - Frame without locals

10. **Integration** (2 tests)
    - All fixture states
    - State validation

### Test Fixtures

13 mock generator functions in `debugger-mock.mjs`:

- `getDesignModeState()` → stopped, no frame
- `getRunModeState()` → running, no frame
- `getBreakModeState()` → paused with 3 locals + 3-frame stack
- `getBreakModeWithManyLocalsState()` → 50 locals, 20-frame stack
- `getBreakModeWithDeepStackState()` → 100 stack frames, 1 local
- `getCorruptedFrameState()` → malformed locals (null name, undefined value)
- `getFrameWithNullLocalsState()` → null locals array
- `getFrameWithSpecialCharsState()` → paths/names with `()`, `<T>` characters
- `getFrameWithLargeValueState()` → 10KB string local value
- `getTransitionSequence()` → [design → run → break → run → design]
- `getSessionChangeSequence()` → transitions with sessionId changes
- `getAllStates()` → all 9 fixture states as array
- `validateState(state)` → validation utility (checks structure)

### Running Tests

```bash
cd src/versions/v2.0.0
node tests/debug-session-handler.test.mjs
```

Expected output: 30+ tests passing, <100ms execution.

## Usage Example

```javascript
// 1. Create handler with dependencies
const handler = new DebugSessionHandler({
  logger: bridgeLogger,        // Optional
  metrics: bridgeMetrics,      // Optional
  cacheSize: 100,              // Optional
  cacheTtlMs: 5 * 60 * 1000    // Optional
});

// 2. Register with dispatcher
await handler.registerMessageHandlers(server);

// 3. Subscribe to changes
const unsubscribe = handler.onDebugStateChange((event) => {
  console.log(`State: ${event.state}, Line: ${event.frame?.line}`);
  // Update WebView UI with debug context
});

// 4. Handle WebView queries
const response = await handler.handle({
  data: {
    includeStack: true,
    includeLocals: true,
    maxFrames: 10
  }
});

if (response.success) {
  console.log(`Currently ${response.data.state} at ${response.data.frame?.file}:${response.data.frame?.line}`);
}

// 5. Cleanup on shutdown
unsubscribe();
handler.dispose();
```

## Troubleshooting

### Handler not receiving debugStateChange messages

**Symptoms**: `currentState` always 'stopped', frame always null

**Checks**:
1. DebugSessionCollector hooked in EditorContextProvider.RegisterAsync()?
2. DTE2.DebuggerEvents available (not null)?
3. Debugger actually running (set breakpoint, hit it)?
4. Bridge message transport working (SendToGui succeeds)?

**Fix**: Enable debug logging in DebugSessionCollector, check Output window.

### Locals array empty or null

**Symptoms**: frame.locals is [] or null even when paused

**Checks**:
1. Debugger actually paused (state === 'paused')?
2. Frame.Locals collection accessible (may require IDE permissions)?
3. Target code in debug build (debug symbols available)?

**Fix**: Verify breakpoint is hit, inspect DTE.Debugger.CurrentThread.StackFrames in debugger.

### Cache hit rate low (<50%)

**Symptoms**: Query latency consistently >50ms

**Checks**:
1. Increase cacheTtlMs? (default 5 min)
2. Verify session ID stability (should only change on run/stop)?
3. Cache size sufficient for typical debugging?

**Fix**: Increase cacheSize or cacheTtlMs during handler initialization.

### Query response slow (>200ms)

**Symptoms**: WebView feels laggy during stepping

**Checks**:
1. Is this a cache hit or miss? (check queryTime in response)
2. Stack too deep (>50 frames)?
3. Too many locals (>200)?

**Fix**: Reduce maxFrames or filter locals server-side in DebugSessionCollector.

---

**Document Version**: 2.1  
**Last Updated**: 2024-01-15  
**Related**: Step 61 (this handler), Step 71 (registration), Step 75 (E2E tests)
