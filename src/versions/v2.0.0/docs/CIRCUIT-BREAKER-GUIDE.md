# Circuit Breaker Guide (Step 108)

## Overview

The **Circuit Breaker** is a fault tolerance pattern that prevents cascading failures across the bridge handler pipeline. It complements the **Rate Limiter** (Step 107) and **Error Recovery** (Step 74) to provide defense-in-depth resilience.

**Three-State Machine**:
- **CLOSED**: Normal operation, requests flow through
- **OPEN**: Handler failing; requests rejected immediately (fast-fail)
- **HALF_OPEN**: Probing recovery; single "probe" request allowed to test recovery

---

## Architecture

### State Machine Diagram

```
┌─────────────────────────────────────────────────────┐
│                   CLOSED                             │
│         (Normal Operation)                           │
│    Requests: Accepted                               │
│    Errors tracked but don't block                   │
└──────────┬──────────────────────────────────────────┘
           │
           │ errorCount ≥ 5
           │ OR errorRate > 5%
           ↓
┌─────────────────────────────────────────────────────┐
│                    OPEN                              │
│         (Handler Failing)                            │
│    Requests: Rejected immediately (-32000)          │
│    Metrics recorded for monitoring                  │
└──────────┬──────────────────────────────────────────┘
           │
           │ Cooldown expired (30s)
           ↓
┌─────────────────────────────────────────────────────┐
│                 HALF_OPEN                            │
│         (Probing Recovery)                           │
│    Requests: Single probe allowed                   │
│    Success → CLOSED (recovery successful)           │
│    Failure → OPEN (recovery failed)                 │
└─────────────────────────────────────────────────────┘
```

### Message Flow

```
Request
  ↓
┌──────────────────────────────────────┐
│ PRE-DISPATCH HOOK                     │
│ canAcceptRequest(handlerType)         │
└──────────────────────────────────────┘
  │
  ├─→ [OPEN] → Reject (-32000 error)
  │
  ├─→ [CLOSED/HALF_OPEN] → Continue
  │
  ↓
┌──────────────────────────────────────┐
│ HANDLER EXECUTION                     │
│ (handler processes request)           │
└──────────────────────────────────────┘
  │
  │ (success or failure)
  ↓
┌──────────────────────────────────────┐
│ POST-DISPATCH HOOK                    │
│ recordResult(handlerType, result)     │
├──────────────────────────────────────┤
│ - Update metrics (success/failure)    │
│ - Record latency                      │
│ - Evaluate state transitions          │
│ - Emit events (stateChange, alert)    │
└──────────────────────────────────────┘
  ↓
Response
```

### Per-Handler Isolation

Each handler maintains **independent** circuit state:

```
Handler A OPEN ─┐
                │─→ No impact
Handler B CLOSED┘

Handler B failure doesn't auto-trip Handler A circuit
(19/20 handlers remain unaffected when peer fails)
```

---

## Configuration

### Default Thresholds

| Setting | Default | Range | Purpose |
|---------|---------|-------|---------|
| `failureThreshold` | 5 | 1-100 | Error count to trigger OPEN |
| `successThreshold` | 2 | 1-10 | Consecutive successes to trigger CLOSED |
| `errorRateThreshold` | 0.05 | 0.01-1.0 | Error rate % to trigger OPEN (5%) |
| `timeoutMs` | 30000 | 1000-600000 | Cooldown before HALF_OPEN probe (30s) |
| `cooldownMs` | 5000 | 100-60000 | Initial cooldown duration (5s) |
| `maxRetries` | 3 | 1-10 | Max probe attempts before staying OPEN |
| `windowSizeMs` | 60000 | 10000-600000 | Metrics window for error rate (60s) |
| `p99LatencyThreshold` | 500 | 100-10000 | p99 latency alert threshold (ms) |

### Custom Configuration

```javascript
import { createCircuitBreakerManager } from './circuit-breaker-manager.mjs';

const customConfig = {
  failureThreshold: 10,           // More tolerant
  successThreshold: 3,            // Stricter recovery
  errorRateThreshold: 0.1,        // 10% error rate
  timeoutMs: 60000,               // 1 minute cooldown
  cooldownMs: 10000,              // 10s initial cooldown
  maxRetries: 5,                  // More retry attempts
  windowSizeMs: 120000,           // 2 minute metrics window
  p99LatencyThreshold: 1000,      // 1s latency alert
};

const manager = createCircuitBreakerManager(customConfig, {
  logger: myLogger,
  metrics: myMetricsCollector,
  timeoutManager: step64TimeoutManager,
  rateLimiter: step107RateLimiter,
});

manager.start();
```

---

## Per-Handler Lifecycle

### CLOSED → OPEN Transition

**Triggers** (any one):
- Error count ≥ `failureThreshold` (default 5)
- Error rate > `errorRateThreshold` (default 5%)
- Consecutive failures ≥ `failureThreshold`

**Example**:
```javascript
// After 5 consecutive failures
manager.recordFailure('bridge:refactor');  // x5
manager._evaluateCircuit(circuit);
// circuit now OPEN → requests rejected (-32000)
```

### OPEN → HALF_OPEN Transition

**Trigger**:
- `timeoutMs` cooldown expired (default 30 seconds)

**Example**:
```javascript
// 30+ seconds pass since OPEN
manager._evaluateCircuit(circuit);
// circuit now HALF_OPEN → next request is probe
```

### HALF_OPEN → CLOSED Transition

**Trigger**:
- `successThreshold` consecutive successes (default 2)

**Example**:
```javascript
// In HALF_OPEN state
manager.recordSuccess('bridge:refactor');
manager.recordSuccess('bridge:refactor');
manager._evaluateCircuit(circuit);
// circuit now CLOSED → recovery successful
```

### HALF_OPEN → OPEN Transition

**Triggers** (any one):
- Any failure in probe phase
- `maxRetries` probe attempts exceeded

**Example**:
```javascript
// In HALF_OPEN state
manager.recordFailure('bridge:refactor');
manager._evaluateCircuit(circuit);
// circuit back OPEN → recovery failed, restart cooldown
```

---

## Metrics & Observability

### Circuit Metrics

```javascript
const state = manager.getCircuitState('bridge:refactor');
{
  handlerType: 'bridge:refactor',
  state: 'CLOSED',
  metrics: {
    errorCount: 2,
    successCount: 18,
    consecutiveFailures: 0,
    totalRequests: 20,
    errorRate: 0.1,
    p99Latency: 245,
    probeAttempts: 0,
  },
  timeSinceStateChange: 5432,
  canAttemptRecovery: false,
}
```

### Aggregate Metrics

```javascript
const agg = manager.getAggregateMetrics();
{
  totalCircuits: 20,
  closedCircuits: 18,
  openCircuits: 1,
  halfOpenCircuits: 1,
  totalStateChanges: 42,
  totalAlerts: 3,
}
```

### Events

**State Change Event**:
```javascript
manager.on('stateChange', (event) => {
  console.log(event);
  // {
  //   handler: 'bridge:refactor',
  //   from: 'CLOSED',
  //   to: 'OPEN',
  //   reason: 'Error threshold exceeded (count=5, rate=25%)',
  //   metrics: { ... },
  //   timestamp: 1705…
  // }
});
```

**Alert Event**:
```javascript
manager.on('alert', (event) => {
  console.log(event);
  // {
  //   handler: 'bridge:refactor',
  //   state: 'OPEN',
  //   alertType: 'CIRCUIT_OPEN',
  //   details: { errorCount: 5, errorRate: '25%' },
  //   severity: 'CRITICAL',
  //   timestamp: 1705…
  // }
});
```

---

## Integration with Bridge Components

### Step 47: MiddlewareChain

Circuit-breaker middleware hooks into the message chain:

```javascript
import { createCircuitBreakerMiddleware } from './circuit-breaker-middleware.mjs';

const cbMiddleware = createCircuitBreakerMiddleware(manager, {
  logger,
  metrics,
  enableBlockingOnOpen: true,
  recordMetrics: true,
});

middlewareChain.registerHook('circuitBreaker', cbMiddleware);
```

### Step 64: TimeoutManager

Circuit-breaker consumes error rates and p99 latency:

```javascript
// Automatically fed during evaluation
const errorRate = timeoutManager.getErrorRate('bridge:refactor');
const p99Latency = timeoutManager.getP99Latency('bridge:refactor');
// Used to decide state transitions
```

### Step 74: ErrorRecoveryMetrics

Circuit-breaker coordinates recovery:

```javascript
// Error classification informs HALF_OPEN probe decisions
const isTransient = errorRecoveryMetrics.isTransientError(error);
const retrySuccessRate = errorRecoveryMetrics.getRetrySuccessRate('bridge:refactor');
```

### Step 107: RateLimiter

Circuit-breaker checks token availability for probes:

```javascript
// Before allowing HALF_OPEN probe request
if (!rateLimiter.canAcceptRequest(handlerType)) {
  // Block probe, wait for tokens
  return false;
}
```

---

## Common Scenarios

### Scenario 1: Handler Timeout Spike

```
Time  | Handler Events                | Circuit State
------|-------------------------------|---------------
0s    | Request succeeds (50ms)       | CLOSED
1s    | Request times out (5000ms)    | CLOSED
2s    | Request times out (5000ms)    | CLOSED
3s    | Request times out (5000ms)    | CLOSED
4s    | Request times out (5000ms)    | CLOSED
5s    | Request times out (5000ms)    | OPEN (threshold hit)
6s    | Request rejected (-32000)     | OPEN
...
35s   | [Auto-transition after 30s]   | HALF_OPEN
36s   | Probe request succeeds        | CLOSED (recovery!)
```

### Scenario 2: Cascading Failure Prevention

```
Handler A (database)    → Fails
  ↓
Handler B (uses A data) → Doesn't fail (circuit OPEN protects B)
  ↓
Handler C (uses B)      → Doesn't fail (cascade stopped)
  ↓
Result: Only A is OPEN, B and C remain healthy
```

### Scenario 3: Recovery After Network Partition

```
Network partition occurs (20 seconds)
  ↓
Handler requests fail consistently
  ↓
Circuit transitions: CLOSED → OPEN (error rate > 5%)
  ↓
Partition heals (25 seconds later)
  ↓
Cooldown expires (30 seconds)
  ↓
Circuit transitions: OPEN → HALF_OPEN (probe allowed)
  ↓
Probe request succeeds
  ↓
Circuit transitions: HALF_OPEN → CLOSED (recovery successful)
  ↓
Normal operation resumes
```

---

## Troubleshooting

### Circuit Stuck OPEN

**Symptom**: Handler requests always rejected with -32000 error

**Causes**:
1. Handler still failing (probe attempts exhausted)
2. Cooldown period not elapsed
3. Manual override active

**Solutions**:
```javascript
// Check circuit state
const state = manager.getCircuitState('handler:name');
console.log(state.metrics);  // Check error rate

// Manual reset (for ops/debugging)
manager.resetCircuit('handler:name');

// Force transition (if absolutely necessary)
manager.forceCircuitState('handler:name', 'CLOSED', 'Manual recovery');
```

### Circuit Oscillating (OPEN ↔ HALF_OPEN ↔ OPEN)

**Symptom**: Circuit repeatedly opens and closes

**Causes**:
1. Probe succeeds inconsistently (intermittent failures)
2. Metrics thresholds too low for handler's error profile
3. RateLimiter blocking probes (no tokens available)

**Solutions**:
```javascript
// Increase thresholds for handlers with higher error rates
const config = {
  failureThreshold: 10,      // Tolerate more errors
  errorRateThreshold: 0.1,   // 10% error rate OK
  timeoutMs: 60000,          // Longer cooldown before probing
};

// Check if RateLimiter is blocking probes
const limiterMetrics = rateLimiter.getMetrics();
console.log(limiterMetrics.rejected);  // Count of rejected requests
```

### High Alert Volume

**Symptom**: Too many CIRCUIT_OPEN or HIGH_LATENCY alerts

**Causes**:
1. Handlers experiencing legitimate stress (cascading failures elsewhere)
2. Alert thresholds too sensitive
3. External service degradation

**Solutions**:
```javascript
// Increase latency threshold
config.p99LatencyThreshold = 1000;  // From 500ms to 1s

// Check external service health
const externalHealth = checkExternalServices();

// Silence alerts temporarily (if needed)
manager.AlertTriggered.disable('HIGH_LATENCY', 300000);  // 5 minutes
```

### Probe Never Transitions to CLOSED

**Symptom**: Circuit stuck in HALF_OPEN, probe requests fail

**Causes**:
1. Underlying issue not resolved
2. Success threshold requires consecutive successes (not total)
3. Handler code has race conditions

**Solutions**:
```javascript
// Check detailed probe metrics
const circuit = manager.getCircuit('handler:name');
console.log(circuit.metrics.probeAttempts);    // How many probes?
console.log(circuit.metrics.consecutiveFailures);  // Still failing?

// Verify handler code is sound
const handlerTests = runHandlerDiagnostics('handler:name');
console.log(handlerTests);  // Check for race conditions, resource leaks

// Increase successThreshold if spurious failures expected
config.successThreshold = 5;  // Require 5 consecutive successes
```

---

## Performance Characteristics

| Operation | Latency | Notes |
|-----------|---------|-------|
| `canAcceptRequest()` | <1ms | O(1) hash lookup |
| `recordSuccess/Failure()` | <1ms | O(1) metrics update |
| State transition | <10ms | O(1) state machine check |
| `getCircuitState()` | <1ms | Snapshot creation |
| `getAggregateMetrics()` | <50ms | Aggregation across 100 circuits |
| State evaluation loop | 1s interval | Background task, configurable |

---

## Feature Flags & Environment Variables

```bash
# Enable/disable circuit-breaker (optional for v2.0.0)
CIRCUIT_BREAKER_ENABLED=true

# Override default config via env (for ops tuning)
CIRCUIT_BREAKER_FAILURE_THRESHOLD=10
CIRCUIT_BREAKER_ERROR_RATE_THRESHOLD=0.15
CIRCUIT_BREAKER_TIMEOUT_MS=60000
```

---

## Best Practices

1. **Monitor Alerts**: Subscribe to `alert` events and relay to ops dashboards
2. **Tune Thresholds**: Adjust based on Step 99 stress test baselines
3. **Coordinate with RateLimiter**: Circuit-breaker + RateLimiter provide layered defense
4. **Test Recovery**: Verify HALF_OPEN → CLOSED transitions in integration tests
5. **Log State Changes**: Emit correlationIds for debugging cascading failures
6. **Document Per-Handler Policies**: Different handlers need different thresholds (e.g., fast handlers tolerate different error rates)

---

## Related Steps

| Step | Description | Integration |
|------|-------------|-------------|
| 47 | MiddlewareChain | Pre/post-dispatch hooks |
| 64 | TimeoutManager | Error rate metrics |
| 74 | ErrorRecoveryMetrics | Recovery coordination |
| 99 | Stress Tests | Isolation validation |
| 107 | RateLimiter | Token availability checks |
| 108 | CircuitBreaker | **THIS MODULE** |
| 109 | MetricsAggregator | Consumes circuit state |
| 110 | E2E Tests | Cascading failure scenarios |
| 115 | Part III Gate | Resilience baseline required |

---

## Example: Full Integration

```javascript
import { getCircuitBreakerManager } from './handler-registry.mjs';
import { createIntegrationContext } from './circuit-breaker-integration.mjs';

// Initialize manager with dependencies
const cbManager = getCircuitBreakerManager(null, {
  logger,
  metrics,
  timeoutManager: step64Manager,
  rateLimiter: step107Limiter,
});

// Subscribe to events
cbManager.on('stateChange', (event) => {
  console.log(`[CircuitBreaker] ${event.handler} ${event.from} → ${event.to}`);
  dashboardService.updateCircuitState(event);
});

cbManager.on('alert', (event) => {
  console.warn(`[CircuitBreakerAlert] ${event.alertType} for ${event.handler}`);
  alertingService.emit(event);
});

// Pre-dispatch check (in middleware)
if (!cbManager.canAcceptRequest('bridge:refactor')) {
  return { error: { code: -32000, message: 'Circuit breaker OPEN' } };
}

// Post-dispatch record (in middleware)
manager.recordSuccess('bridge:refactor', latencyMs);

// Get state for monitoring
const agg = cbManager.getAggregateMetrics();
console.log(`Healthy: ${agg.closedCircuits}/${agg.totalCircuits}`);
```

---

## References

- **Release It!** by Michael T. Nygard (Circuit Breaker pattern)
- **AWS SDK CircuitBreaker**: https://aws.amazon.com/blogs/architecture/circuit-breaker-pattern/
- **Hystrix (Netflix)**: https://github.com/Netflix/Hystrix (inspiration)
- **Step 108 Code**: `src/versions/v2.0.0/lib/circuit-breaker-*.mjs`

---

**Last Updated**: 2024-01-15  
**Version**: 1.0.0  
**Status**: Complete  
**Part III Resilience Layer**: Timeout (64) + Rate Limit (107) + Error Recovery (74) + Circuit-Breaker (108)
