# Bridge State Persistence Guide

**Step 105**: Bridge runtime state persistence for recovery and diagnostics.

## Overview

Bridge State Persistence provides optional, non-breaking runtime state snapshots for the Continue bridge. On graceful shutdown, handler statuses, subscription counts, pending work, and initialization progress are saved to `~/.continue/bridge-state.json` for potential recovery on restart.

### Key Features

- **Optional**: Bridge operates without persisted state (graceful degradation)
- **Non-breaking**: Hooks into existing Step 45 lifecycle without requiring changes
- **Transparent**: Persisted state is best-effort only—not critical for correctness
- **Recoverable**: State validated and selectively used on startup; clean restart if state corrupted
- **Observable**: Integration with IBridgeLogger and IBridgeTelemetryCollector for diagnostics

### Performance Targets

- Checkpoint write: <500ms
- State recovery: <200ms
- Memory overhead: <5MB

---

## Architecture

### Persistence Model

Bridge state is persisted as a single JSON file at `~/.continue/bridge-state.json`.

#### Checkpoint Schema

```json
{
  "timestamp": "2024-01-15T10:30:45.123Z",
  "phase": "ready",
  "handlers": {
    "refactor": { "status": "active", "errorCount": 0, "timeoutCount": 0 },
    "search": { "status": "active", "errorCount": 1, "timeoutCount": 0 },
    "hover": { "status": "idle", "errorCount": 0, "timeoutCount": 0 }
  },
  "subscriptions": { "count": 25, "types": ["onEdit", "onSave", "onClose"] },
  "pendingRequests": { "count": 3 },
  "uptime": 1234,
  "bridgeVersion": "2.0.0"
}
```

#### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `timestamp` | ISO 8601 | UTC time checkpoint was created |
| `phase` | enum | Bridge phase: `bootstrap`, `connected`, `subscribed`, `ready`, `degraded` |
| `handlers` | object | Map of handler name → `{ status, errorCount, timeoutCount }` |
| `status` | enum | Handler status: `active`, `idle`, `error` |
| `errorCount` | number | Cumulative errors from this handler |
| `timeoutCount` | number | Cumulative timeouts from this handler |
| `subscriptions` | object | `{ count: number, types: string[] }` |
| `pendingRequests` | object | `{ count: number }` of RPC messages awaiting response |
| `uptime` | number | Seconds the bridge has been running |
| `bridgeVersion` | string | Semantic version (e.g., "2.0.0") |

### Lifecycle

1. **Bootstrap** (Step 46 + bootstrap-state-recovery.mjs)
   - Early in `core-server.js`: call `attemptStateRecovery()`
   - If valid checkpoint found, validate against current handlers
   - Pass recovered state to handler initialization for subscription replay
   - If checkpoint missing, corrupted, or stale (>7 days), start fresh

2. **Runtime** (Step 105)
   - Optional periodic snapshots (default 30s, configurable)
   - Can be manually triggered for diagnostics

3. **Graceful Shutdown** (Step 45 + bridge-lifecycle-integration.mjs)
   - On `SIGTERM` or `process.exit()`: call `persistence.saveAsync()`
   - Captures current bridge state (handlers, subscriptions, pending work)
   - Writes atomically to `~/.continue/bridge-state.json`
   - Non-blocking: shutdown proceeds regardless of persistence success

---

## Integration

### Step 45 (Bridge Lifecycle Manager)

Import and call during lifecycle manager initialization:

```javascript
import { setupBridgeStatePersistenceHooks } from './bridge-lifecycle-integration.mjs';

const lifeCycleManager = createBridgeLifecycleManager(...);
const persistence = setupBridgeStatePersistenceHooks(
  lifeCycleManager,
  logger,
  metrics
);

// Step 45 should expose:
//  - onGracefulShutdown(handler) → registers handler to run before shutdown
//  - onStartup(handler) → registers handler to run after initialization
```

### Step 46 (WebView Bootstrap Handler) + core-server.js

Early in server startup, before handler registration:

```javascript
import { attemptStateRecovery, validateRecoveredState, replaySubscriptionsFromCheckpoint } from './bootstrap-state-recovery.mjs';

// During bootstrap
const recoveredState = await attemptStateRecovery(logger, metrics);

if (recoveredState) {
  // Validate against current handler registry
  if (validateRecoveredState(recoveredState, handlerNames, logger)) {
    // Replay subscriptions from checkpoint
    await replaySubscriptionsFromCheckpoint(recoveredState, handlerRegistry, logger);
  }
}
```

### Step 101 (Metrics Dashboard)

Dashboard can consume state snapshots for health display:

```javascript
import { BridgeStatePersistence } from './bridge-state-persistence.mjs';

const persistence = new BridgeStatePersistence({ logger, metrics });
const checkpoint = await persistence.loadAsync();

if (checkpoint) {
  // Display checkpoint info in dashboard
  dashboard.display({
    phase: checkpoint.phase,
    handlerCount: Object.keys(checkpoint.handlers).length,
    subscriptionCount: checkpoint.subscriptions.count,
    uptime: checkpoint.uptime
  });
}
```

### Step 110+ (E2E & Regression Testing)

Multi-restart scenarios can validate state recovery:

```javascript
import { BridgeStatePersistence } from './bridge-state-persistence.mjs';
import { createValidCheckpoint } from './tests/mocks/bridge-state-fixtures.mjs';

// Test: verify subscriptions restored after restart
const checkpoint = createValidCheckpoint();
const persistence = new BridgeStatePersistence({ stateDir: tempDir });

// Save state
await persistence.saveAsync(checkpoint);

// Simulate restart: load state
const recovered = await persistence.loadAsync();

// Verify subscriptions count matches
assert(recovered.subscriptions.count === checkpoint.subscriptions.count);
```

---

## API Reference

### Node.js

#### `BridgeStatePersistence` Class

```javascript
import { BridgeStatePersistence } from './bridge-state-persistence.mjs';

const persistence = new BridgeStatePersistence({
  stateDir: path.join(os.homedir(), '.continue'),
  stateFile: path.join(os.homedir(), '.continue', 'bridge-state.json'),
  logger: IBridgeLogger,          // optional
  metrics: IBridgeTelemetryCollector // optional
});

// Save checkpoint to disk
const success = await persistence.saveAsync(checkpoint);

// Load checkpoint from disk
const checkpoint = await persistence.loadAsync(); // or null

// Delete checkpoint file
const deleted = await persistence.deleteAsync();
```

#### `BridgeStateCheckpoint` Class

```javascript
import { BridgeStateCheckpoint } from './bridge-state-persistence.mjs';

const checkpoint = new BridgeStateCheckpoint({
  timestamp: new Date().toISOString(),
  phase: 'ready',
  handlers: { /* ... */ },
  subscriptions: { count: 25, types: [] },
  pendingRequests: { count: 3 },
  uptime: 1234,
  bridgeVersion: '2.0.0'
});

// Validate checkpoint structure
if (checkpoint.validate()) { /* ... */ }

// Check if stale
if (checkpoint.isStale(7 * 24 * 60 * 60 * 1000)) { /* ... */ }

// Serialize
const json = checkpoint.toJSON();

// Deserialize
const restored = BridgeStateCheckpoint.fromJSON(json);
```

#### Factory Functions

```javascript
import { createBridgeStatePersistence } from './bridge-state-persistence.mjs';

const persistence = createBridgeStatePersistence({
  logger,
  metrics
});
```

### C#

#### `BridgeStateSnapshot` Class

```csharp
using VSIXProject1.Services;

var snapshot = new BridgeStateSnapshot
{
  CapturedAt = DateTime.UtcNow,
  CurrentPhase = "ready",
  HandlerCount = 5,
  ActiveHandlers = new List<string> { "refactor", "search" },
  SubscriptionCount = 20,
  PendingRequestCount = 2,
  UptimeSeconds = 1234,
  BridgeVersion = "2.0.0"
};

// Validate snapshot
if (snapshot.Validate()) { /* ... */ }

// Serialize to JSON
var json = snapshot.ToJson();
```

#### `BridgeStateCollector` Class

```csharp
using VSIXProject1.Services;

var collector = new BridgeStateCollector(logger: null);
var snapshot = await collector.CreateSnapshotAsync();

if (snapshot != null && snapshot.Validate())
{
  // Snapshot is valid
}
```

#### `BridgeContextHolder` Static Class

```csharp
// Set during bridge initialization (Step 45)
BridgeContextHolder.SetCurrentPhase("ready");
BridgeContextHolder.SetHandlerRegistry(registry);
BridgeContextHolder.SetBridgeContext(context);
BridgeContextHolder.SetBridgeVersion("2.0.0");

// Reset (for testing)
BridgeContextHolder.Reset();
```

---

## Error Handling

### Checkpoint Corruption

If the state file is corrupted (invalid JSON, missing fields):

- `loadAsync()` returns `null`
- Bridge starts with clean initialization
- Warning logged via logger

### State Validation Failure

If recovered checkpoint fails validation:

- Checkpoint rejected and discarded
- Bridge proceeds with clean state
- Diagnostic message logged

### Stale State

If checkpoint is older than 7 days:

- Checkpoint automatically discarded during recovery
- Bridge starts fresh
- Info message logged

### File Permission Errors

If state directory cannot be created or file cannot be written:

- `saveAsync()` returns `false`
- Shutdown proceeds regardless (best-effort)
- Error logged via logger

### Handler Registry Mismatch

If checkpoint references handlers that no longer exist:

- Checkpoint rejected during validation
- Bridge starts fresh (handlers may have changed)
- Warning logged

---

## Configuration

### Optional Parameters

```javascript
const persistence = new BridgeStatePersistence({
  // Directory for state files (default: ~/.continue)
  stateDir: '/custom/path',

  // State file path (default: ~/.continue/bridge-state.json)
  stateFile: '/custom/path/state.json',

  // Optional logger implementing IBridgeLogger
  logger: myLogger,

  // Optional metrics collector implementing IBridgeTelemetryCollector
  metrics: myMetrics,

  // Maximum age before checkpoint is considered stale (default: 7 days)
  maxAgeMs: 7 * 24 * 60 * 60 * 1000
});
```

### Environment Variables (Future)

- `BRIDGE_STATE_DIR` - Override state directory
- `BRIDGE_STATE_DISABLED` - Disable persistence (set to "true")
- `BRIDGE_STATE_SNAPSHOT_INTERVAL_MS` - Periodic snapshot interval

---

## Testing

### Fixtures

Bridge state fixtures available in `tests/mocks/bridge-state-fixtures.mjs`:

```javascript
import {
  createValidCheckpoint,
  createMinimalCheckpoint,
  createDegradedCheckpoint,
  createLargeCheckpoint,
  createCheckpointByPhase,
  mockLogger,
  mockMetrics
} from './tests/mocks/bridge-state-fixtures.mjs';

// Use in tests
const checkpoint = createValidCheckpoint();
const logger = mockLogger();
const metrics = mockMetrics();
```

### Test Coverage

**Node.js** (30 tests in `bridge-state-persistence.test.mjs`):
- Initialization & configuration (3)
- Checkpoint creation (4)
- Persistence (6)
- Recovery (6)
- Schema validation (4)
- Performance gates (2)
- Integration patterns (2)
- Edge cases (3)

**C#** (18+ tests in `BridgeStateCollectorTests.cs`):
- Snapshot creation (5)
- Handler state capture (4)
- Graceful degradation (4)
- Error handling (3)
- Performance (2)
- Snapshot validation
- JSON serialization

### Performance Verification

```bash
# Run all tests with timing
npx mocha src/versions/v2.0.0/tests/bridge-state-persistence.test.mjs --reporter json

# C# tests
dotnet test VSIXProject1.Tests --logger "console;verbosity=detailed"
```

---

## Troubleshooting

### Checkpoint not being saved

- Check permissions on `~/.continue` directory
- Verify `IBridgeLogger` is configured
- Check logs for `[BridgeStatePersistence]` messages
- Verify bridge is calling `saveAsync()` during shutdown

### State not being recovered

- Check if `~/.continue/bridge-state.json` exists
- Verify checkpoint timestamp is not older than 7 days
- Check logs for validation errors
- Ensure `attemptStateRecovery()` is called during bootstrap

### Performance degradation

- Monitor `bridge.state.save.duration_ms` and `bridge.state.load.duration_ms` metrics
- If save/load exceeds gates (500ms/200ms), investigate file I/O
- Consider disabling periodic snapshots if not needed

---

## Related Steps

- **Step 45**: Bridge Lifecycle Manager (graceful shutdown trigger)
- **Step 46**: WebView Bootstrap Handler (recovery on startup)
- **Step 101**: Metrics Dashboard (displays state snapshots)
- **Step 103**: Crash Recovery (distinct from runtime state)
- **Step 104**: Configuration Persistence (different concern)
- **Step 110**: E2E Scenarios (validates multi-restart flows)
- **Step 112**: Regression Suite (baseline state recovery performance)
- **Step 115**: Part III Gate (infrastructure completeness)
