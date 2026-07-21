# Performance Tuning Guide

A practical reference for operators, QA teams, and engineers to interpret performance baselines, detect regressions, and optimize the bridge for production workloads.

---

## Baseline Reference (from Step 112)

### Regression Severity Thresholds

The bridge establishes performance baselines through Step 98 performance testing and Step 99 stress testing. Step 112 regression testing compares current metrics against these baselines using the following severity tiers:

| Metric | Critical (Blocks Release) | High (Escalate) | Medium (Monitor) | Low (Track) |
|--------|---------------------------|-----------------|-----------------|-----------|
| **Latency p99** | >50% regression | >25% regression | >15% regression | >10% regression |
| **Throughput** | >40% drop | >20% drop | >10% drop | >5% drop |
| **Memory** | >50MB peak | >20MB growth | >10MB growth | N/A |
| **Error Rate** | >10% abs | >5% abs | >2% abs | >1% abs |

**Interpretation Guide**:
- **Critical**: Release is blocked; fix required before shipping
  - Example: Fast tier baseline p99=1500ms → current p99=2250ms (50% increase) = CRITICAL
- **High**: Escalate to engineering; review before release
  - Example: Medium tier baseline p99=7000ms → current p99=8750ms (25% increase) = HIGH
- **Medium**: Log for tracking; monitor in production
  - Example: Slow tier baseline p99=25000ms → current p99=28750ms (15% increase) = MEDIUM
- **Low**: Document for historical trend analysis
  - Example: Memory baseline 30MB → current 33MB (10% increase) = LOW

---

### Handler Timeout Policies (from Step 64)

Handlers are categorized by complexity tier, each with a timeout policy. The timeout defines the maximum time the bridge waits for a handler to complete before returning a timeout error.

| Tier | Default (ms) | Complexity | Use Case | Handler Examples |
|------|---|---|---|---|
| **Fast** | 2,000 | Simple queries, state reads | Read-only operations, no I/O | `bridge:getEditorState`, `bridge:hoverInfo`, `bridge:search` |
| **Medium** | 10,000 | Complex analysis, local I/O | Transformation, parsing, local analysis | `bridge:refactor`, `bridge:codeCompletion`, `bridge:formatDocument` |
| **Slow** | 30,000 | External integration | Network I/O, external services | `bridge:debugSession`, `bridge:git`, `bridge:terminal` |

**Timeout Tuning Guidance**:
- **Increase timeout if**: Timeout errors appear in logs >5% of invocations (check Step 96 profiler)
- **Decrease timeout if**: Handler completes consistently <50% of timeout (reduces user wait)
- **Profile before tuning**: Use Step 96 profiler to measure actual p50/p95/p99 latency first

---

## Tuning Procedures

### 1. Latency Regression (p99 >baseline)

**When to Apply**:
- User reports slow handler response
- Step 112 regression test flags p99 >threshold
- Step 96 profiler shows p99 increasing over time

**Diagnosis**:

Step 1: Run the Step 96 profiler to measure current p99 latency per handler
```bash
# Invoke profiler endpoint
curl http://localhost:3000/bridge/profiler | jq '.handlers[] | {name, p50, p95, p99}'

# Example output:
# {
#   "name": "refactor",
#   "p50": 3200,
#   "p95": 7850,
#   "p99": 8950
# }
```

Step 2: Compare to Step 112 baseline for handler tier
```bash
# Fast tier baseline: p99 <2000ms
# Medium tier baseline: p99 <10000ms
# Slow tier baseline: p99 <30000ms

# If refactor (medium tier) shows p99=8950ms: OK (< 10000ms)
# If refactor shows p99=11500ms: HIGH regression (>25%)
```

Step 3: Check Step 98 performance test results
```bash
npm test -- --grep "performance-suite" --prefix src/versions/v2.0.0
# Compare current results to Step 98 baseline snapshot
```

**Common Causes**:
1. **Timeout policy too short**: Handler completes >95% of timeout → increase timeout
2. **Symbol cache thrashing**: Large workspace causing repeated cache regeneration → clear cache
3. **Concurrent load spike**: Message queue saturation (Step 65) → check concurrency test results
4. **Middleware overhead**: Steps 72–74 (logging, validation, error recovery) adding latency → profile middleware

**Remediation Steps**:

**Option A: Handler slow (adjust timeout)**
```javascript
// Step 71: handler-registry.mjs
// Find handler entry and adjust timeoutPolicy

{
  name: "refactor",
  timeoutPolicy: "medium",  // Change from "fast" to "medium" if needed
  // OR increase custom timeout:
  customTimeout: 15000  // Increase from 10000ms to 15000ms
}

// Restart bridge for changes to take effect
npm start --prefix src/versions/v2.0.0
```

**Option B: Symbol cache thrashing (clear cache)**
```bash
# Invoke Step 94 reload handler to clear symbol cache
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{
    "messageId": "1",
    "messageType": "bridge:reload",
    "data": { "scope": "symbols" }
  }'

# Then re-run profiler to verify latency improvement
curl http://localhost:3000/bridge/profiler | jq '.handlers[] | select(.name=="refactor") | .p99'
```

**Option C: Concurrent load issue (check queue saturation)**
```bash
# Run Step 99 concurrent stress test
npm test -- --grep "concurrent-load" --prefix src/versions/v2.0.0

# If p99 >500ms @100 parallel: Step 65 queue size too small
# Increase queue size in Step 65 configuration:
# maxQueueSize: 500  # Increase from 300 to 500
```

**Option D: Middleware overhead (profile middleware)**
```bash
# Increase logging to trace middleware execution time
// Step 72: message-logging-middleware.mjs
// Add performance.mark() around middleware execution

performance.mark("middleware-start");
// ... middleware code ...
performance.mark("middleware-end");
performance.measure("middleware", "middleware-start", "middleware-end");
```

**Verification**:
- Re-run Step 96 profiler; verify p99 decreased
- Run Step 98 performance test; compare to baseline
- Run Step 99 concurrent test; verify isolated improvement

---

### 2. Memory Leak or Unbounded Growth

**When to Apply**:
- Bridge memory usage grows over time without plateau
- Step 99 sustained load test shows >10KB avg delta per 30s
- Step 112 regression test flags memory >threshold
- Bridge eventually crashes with OutOfMemory error

**Baseline Reference** (from Step 99):
- **Peak memory**: <50 MB (critical if peak >60 MB)
- **Average delta**: <10 KB per 30 seconds during sustained load
- **Sustained load**: 100 concurrent requests for 5 minutes should not trigger OOM

**Diagnosis**:

Step 1: Run the Step 99 sustained load test to reproduce memory growth
```bash
npm test -- --grep "sustained-load" --prefix src/versions/v2.0.0

# Output includes memory readings:
# Initial: 45 MB
# After 30s: 48 MB (delta: +3 MB, acceptable)
# After 60s: 52 MB (delta: +4 MB, high)
# After 90s: 57 MB (delta: +5 MB, CRITICAL)
```

Step 2: Attach Node.js memory profiler to identify leak
```bash
# Start bridge with --inspect flag
node --inspect=9229 src/versions/v2.0.0/core-server.js

# Open Chrome DevTools: chrome://inspect
# Take heap snapshot at start
# Run load test or user workflow
# Take heap snapshot at end
# Compare snapshots to find retained objects
```

Step 3: Check handler lifecycle cleanup
```bash
# Review handler dispose() methods in src/versions/v2.0.0/lib/handlers/
# Each handler should implement dispose() or cleanup

// Example handler cleanup:
export class RefactorHandler {
  dispose() {
    // Clean up event listeners
    this.editorChangeSubscription?.dispose();

    // Clear caches
    this.symbolCache.clear();

    // Cancel pending operations
    this.pendingRequests.clear();
  }
}
```

**Common Causes**:
1. **Circular references**: Object graph not garbage collected
   - Fix: Break circular refs; use weak references
2. **Uncleaned event listeners**: Step 51 subscriptions not disposed
   - Fix: Call dispose() on subscription; unsubscribe from events
3. **Unbounded cache**: Step 52 document cache or Step 53 symbol cache growing
   - Fix: Add cache eviction policy (LRU, size limit, TTL)
4. **Message queue retention**: Step 65 queue holding stale messages
   - Fix: Verify queue dequeue on handler completion
5. **Diagnostics accumulation**: Step 103 crash recovery files not rotated
   - Fix: Implement log rotation; delete old files

**Remediation Steps**:

**Option A: Force garbage collection**
```bash
# Manual garbage collection trigger (temporary, diagnostic only)
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{
    "messageId": "1",
    "messageType": "bridge:gc",
    "data": {}
  }'

# Note: This is a diagnostic tool; does NOT fix memory leak
```

**Option B: Clear document cache**
```bash
# Invoke Step 94 reload handler to clear document cache
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{
    "messageId": "1",
    "messageType": "bridge:reload",
    "data": { "scope": "documents" }
  }'
```

**Option C: Review handler lifecycle (code fix required)**
```javascript
// src/versions/v2.0.0/lib/handlers/refactor-handler.mjs

export class RefactorHandler {
  constructor() {
    this.subscriptions = [];
    this.cache = new Map();
  }

  async invoke(request) {
    // ... handler logic ...
  }

  dispose() {
    // CRITICAL: Clean up all resources
    this.subscriptions.forEach(sub => sub.dispose());
    this.subscriptions = [];

    this.cache.clear();
  }
}

// Step 71: Ensure handler.dispose() called on shutdown
bridgeLifecycleManager.onShutdown(() => {
  allHandlers.forEach(handler => handler.dispose?.());
});
```

**Option D: Add cache eviction policy**
```javascript
// Step 52: document-provider.mjs
// Add LRU cache with size limit

export class DocumentCache {
  constructor(maxSize = 100) {
    this.cache = new Map();
    this.maxSize = maxSize;
  }

  set(key, value) {
    if (this.cache.size >= this.maxSize && !this.cache.has(key)) {
      // Evict oldest (first) entry
      const firstKey = this.cache.keys().next().value;
      this.cache.delete(firstKey);
    }
    this.cache.set(key, value);
  }

  get(key) {
    return this.cache.get(key);
  }

  clear() {
    this.cache.clear();
  }
}
```

**Option E: Rotate diagnostics files**
```javascript
// Step 103: crash-recovery-manager.mjs
// Add log rotation to prevent unlimited growth

export function rotateDiagnostics() {
  const diagnosticsDir = path.join(os.homedir(), '.continue', 'crash-diagnostics');
  const files = fs.readdirSync(diagnosticsDir);
  const sortedByTime = files
    .map(f => ({ name: f, time: fs.statSync(path.join(diagnosticsDir, f)).mtime }))
    .sort((a, b) => b.time - a.time);

  // Keep only last 50 files; delete older
  const toDelete = sortedByTime.slice(50);
  toDelete.forEach(f => {
    fs.unlinkSync(path.join(diagnosticsDir, f.name));
  });
}
```

**Verification**:
- Re-run Step 99 sustained load test; verify memory delta <10KB/30s
- Attach memory profiler; verify no retained objects growing
- Monitor in production; verify memory plateaus after initial climb

---

### 3. Throughput Drop (requests/sec below baseline)

**When to Apply**:
- Bridge processes fewer requests per second than baseline
- User reports laggy response to multiple concurrent requests
- Step 98 performance test shows throughput regression
- Message queue backs up or requests timeout

**Baseline Reference** (from Step 98):
- **Target throughput**: ~320 requests/sec (varies by handler mix)
- **Acceptable minimum**: >300 requests/sec
- **Critical threshold**: <150 requests/sec (>50% drop)

**Diagnosis**:

Step 1: Measure current throughput
```bash
# Run Step 98 performance test
npm test -- --grep "throughput" --prefix src/versions/v2.0.0

# Example output:
# Throughput: 312 msg/sec (vs baseline 320 = -2.5%, acceptable)
# Throughput: 245 msg/sec (vs baseline 320 = -23%, HIGH regression)
# Throughput: 125 msg/sec (vs baseline 320 = -61%, CRITICAL regression)
```

Step 2: Check priority queue saturation
```bash
# Run Step 99 concurrent stress test
npm test -- --grep "concurrent-load" --prefix src/versions/v2.0.0

# Monitor queue depth during test:
# If queue reaches maxSize frequently → queue too small
# If queue completes <100ms → queue size adequate
```

Step 3: Check middleware performance
```bash
# Profile middleware execution time (Steps 72–74)
// Step 72: message-logging-middleware.mjs - add timing

const start = performance.now();
// ... middleware code ...
const duration = performance.now() - start;
if (duration > 10) {
  console.warn(`Middleware took ${duration}ms`);
}
```

Step 4: Check handler registry performance
```bash
// Step 71: handler-registry.mjs
// Verify handler lookups are cached, not O(n)

// Good: O(1) lookup
const handlerMap = new Map();
handlerMap.set("refactor", refactorHandler);
const handler = handlerMap.get(name);  // Fast

// Bad: O(n) lookup
const handler = allHandlers.find(h => h.name === name);  // Slow
```

**Common Causes**:
1. **Message queue saturation**: Step 65 queue too small for concurrent load
   - Fix: Increase maxQueueSize in Step 65 config
2. **Middleware overhead**: Steps 72–74 consuming CPU
   - Fix: Reduce logging verbosity; profile middleware hotspots
3. **Handler registry inefficiency**: Step 71 lookups slow
   - Fix: Verify caching; use Map not array find()
4. **Validation overhead**: Step 73 validator slow on large payloads
   - Fix: Optimize validator; cache validation results
5. **Concurrent handler contention**: Shared resource locks blocking handlers
   - Fix: Review Step 105 state persistence; reduce lock contention

**Remediation Steps**:

**Option A: Increase priority queue size (Step 65)**
```javascript
// src/versions/v2.0.0/lib/priority-queue.mjs

const queueConfig = {
  maxQueueSize: 500,  // Increase from 300 to 500
  maxWaitTime: 30000  // Keep timeout same
};

const queue = new PriorityQueue(queueConfig);
```

**Option B: Reduce logging verbosity (Step 72)**
```javascript
// src/versions/v2.0.0/lib/message-logging-middleware.mjs

// Reduce log level from 'debug' to 'warn'
export const logLevel = 'warn';  // was 'debug'

// Or disable logging in high-throughput scenarios
if (messageCount % 100 === 0) {
  logger.debug(`Processed ${messageCount} messages`);  // Log every 100th only
}
```

**Option C: Profile middleware performance**
```bash
# Add performance.mark() to measure middleware
performance.mark("middleware-validation-start");
// ... validation code ...
performance.mark("middleware-validation-end");
performance.measure("validation", "middleware-validation-start", "middleware-validation-end");

# Review PerformanceObserver to find bottleneck
const observer = new PerformanceObserver((list) => {
  for (const entry of list.getEntries()) {
    if (entry.duration > 5) {  // Warn if >5ms
      console.warn(`${entry.name}: ${entry.duration}ms`);
    }
  }
});
observer.observe({ entryTypes: ['measure'] });
```

**Option D: Optimize handler registry (Step 71)**
```javascript
// src/versions/v2.0.0/lib/handler-registry.mjs

// Ensure Map is used for O(1) lookup, not array.find()
export class HandlerRegistry {
  constructor() {
    this.handlers = new Map();  // Use Map, not array
  }

  register(name, handler) {
    this.handlers.set(name, handler);
  }

  get(name) {
    return this.handlers.get(name);  // O(1) lookup
  }
}
```

**Verification**:
- Re-run Step 98 performance test; verify throughput increased
- Run Step 99 concurrent test; verify p99 latency stable
- Monitor message queue depth; verify not saturating

---

### 4. Error Rate Spike (unintended errors >1%)

**When to Apply**:
- Unintended errors appearing during normal operation
- Step 99 error injection test shows error rate >1% above baseline
- Step 112 regression test flags error rate regression
- User reports intermittent handler failures

**Baseline Reference** (from Step 99):
- **Target error rate**: <1% unintended errors
- **Acceptable range**: <2% (includes both intended and unintended)
- **Critical**: >10% error rate

**Diagnosis**:

Step 1: Measure current error rate
```bash
# Run Step 96 profiler to get current metrics
curl http://localhost:3000/bridge/profiler | jq '.metrics | {totalRequests, totalErrors, errorRate}'

# Example output:
# {
#   "totalRequests": 10000,
#   "totalErrors": 85,
#   "errorRate": 0.0085  # 0.85% = OK
# }

# If errorRate > 0.01 (1%): Investigate
# If errorRate > 0.05 (5%): CRITICAL
```

Step 2: Run error injection baseline test
```bash
# Run Step 99 error injection test to compare
npm test -- --grep "error-injection" --prefix src/versions/v2.0.0

# Output shows:
# Baseline: 95 intended + 5 unintended = 100 errors = 5% total, 0.5% unintended
# Current: 95 intended + 15 unintended = 110 errors = 11% total, 1.5% unintended
# If current unintended >0.1% above baseline: Investigate
```

Step 3: Categorize errors
```bash
# Review core-server.js logs to identify error types
# Group by error code:
# - -32600 InvalidRequest: Message validation failure
# - -32602 InvalidParams: Parameter validation failure
# - -32603 InternalError: Handler execution failure

# High -32603 rate → handler crashes
# High -32600/-32602 → validation too strict
```

**Common Causes**:
1. **Validation failures** (Step 73): Validation rules too strict
   - Fix: Relax validation; verify schema matches actual messages
2. **Timeout enforcement**: Handler timeouts too short
   - Fix: Increase timeout policy (Step 64); verify with profiler
3. **Handler crashes**: Logic errors causing exceptions
   - Fix: Review handler code; add error handling; increase unit test coverage
4. **Missing dependencies**: Handler missing config or state
   - Fix: Verify Step 104 config file; verify Step 105 state persistence
5. **Resource exhaustion**: Queue full, memory low, disk full
   - Fix: Increase queue size; free resources; restart bridge

**Remediation Steps**:

**Option A: Validate request structure (Step 73)**
```bash
# Review Step 73 validation rules to ensure not too strict

# Example: Validate message envelope
{
  messageId: "1",           # Required
  messageType: "bridge:refactor",  # Required
  data: { ... }             # Required (can be empty object)
}

# Ensure validation matches actual message format
```

**Option B: Increase timeout (Step 64)**
```javascript
// Step 71: handler-registry.mjs
// Find handlers with high timeout error rate

{
  name: "refactor",
  timeoutPolicy: "medium",  // Default 10000ms
  // If timeout errors >5%: increase to "slow"
  timeoutPolicy: "slow"     // 30000ms
}
```

**Option C: Review handler code (Step 76–87)**
```javascript
// Example: Add error handling to prevent crashes

export class RefactorHandler {
  async invoke(request) {
    try {
      // Validate input
      if (!request.data.filePath) {
        throw new Error("Missing filePath");
      }

      // Execute handler
      const result = await performRefactor(request.data);

      return {
        success: true,
        result: result
      };
    } catch (error) {
      // Catch and report error properly
      return {
        success: false,
        error: error.message,
        code: -32603
      };
    }
  }
}
```

**Option D: Check config and state (Steps 104–105)**
```bash
# Verify config file exists
ls -la ~/.continue/config.json

# Verify state file accessible
ls -la ~/.continue/bridge-state.json

# If missing, recreate
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{
    "messageId": "1",
    "messageType": "bridge:applySettings",
    "data": { "models": [] }
  }'
```

**Verification**:
- Re-run Step 96 profiler; verify error rate <1%
- Run Step 99 error injection test; compare to baseline
- Monitor error logs; verify specific error types decreased

---

## Profiling Guide

### Using bridge:getProfilerData (Step 96)

The bridge exposes a profiler endpoint for real-time performance metrics.

**Invoke Profiler**:
```bash
curl -X POST http://localhost:3000/bridge/profiler \
  -H "Content-Type: application/json" \
  -d '{"messageId":"1","messageType":"bridge:getProfilerData","data":{}}'
```

**Response Structure**:
```json
{
  "timestamp": "2024-01-15T10:30:45Z",
  "metrics": {
    "totalRequests": 10000,
    "totalErrors": 85,
    "errorRate": 0.0085,
    "timeoutCount": 3
  },
  "handlers": [
    {
      "name": "refactor",
      "p50": 3200,
      "p95": 7850,
      "p99": 8950,
      "errorCount": 5,
      "timeoutCount": 1
    },
    ...
  ]
}
```

**Interpretation**:
- **p50**: 50th percentile (median latency); represents typical performance
- **p95**: 95th percentile; represents acceptable latency for most users
- **p99**: 99th percentile; represents worst-case latency; used for SLA targets
- **errorCount**: Total errors for this handler; used to identify problematic handlers
- **timeoutCount**: Total timeouts; indicator of timeout policy mismatch

**Example Analysis**:
```bash
# Handler "refactor" has p99=8950ms
# Timeout policy = "medium" (10000ms)
# Usage: p99 is 89.5% of timeout → within bounds

# Handler "debug" has p99=31200ms
# Timeout policy = "slow" (30000ms)
# Usage: p99 exceeds timeout (30000ms) → CRITICAL
# Action: Increase timeout to 35000ms or optimize handler
```

---

### Node.js Built-in Profiler

For deep performance analysis, use Node.js --inspect flag.

**Start Bridge with Profiler**:
```bash
node --inspect=9229 src/versions/v2.0.0/core-server.js
```

**Open Chrome DevTools**:
1. Open Chrome browser
2. Navigate to chrome://inspect
3. Click "inspect" on the bridge process
4. Go to "Performance" tab
5. Click "Record" button
6. Run user workflow (e.g., send refactor request)
7. Click "Stop" to end recording

**Analyze Profile**:
- **CPU flame chart**: Shows which functions consumed CPU time
- **Call tree**: Hierarchical view of function execution
- **Bottom-up**: Shows which functions were called most
- **Timeline**: Visualizes execution over time

**Memory Profiling**:
1. Open Chrome DevTools (see above)
2. Go to "Memory" tab
3. Click "Take snapshot" to capture heap
4. Run user workflow
5. Click "Take snapshot" again
6. Compare snapshots to find retained objects

---

### Custom Instrumentation

Add performance.mark() and performance.measure() to measure specific handler operations.

**Example: Profile Handler Invocation**:
```javascript
// src/versions/v2.0.0/lib/message-routing-middleware.mjs

export async function routeMessage(message) {
  const handlerName = message.messageType;

  // Mark start of handler invocation
  performance.mark(`${handlerName}-start`);

  try {
    const handler = registry.get(handlerName);
    const result = await handler.invoke(message.data);

    // Mark end of handler invocation
    performance.mark(`${handlerName}-end`);

    // Measure time between marks
    performance.measure(
      `${handlerName}-duration`,
      `${handlerName}-start`,
      `${handlerName}-end`
    );

    return result;
  } catch (error) {
    performance.mark(`${handlerName}-error`);
    throw error;
  }
}

// Collect metrics
const observer = new PerformanceObserver((list) => {
  for (const entry of list.getEntries()) {
    if (entry.duration > 100) {  // Log slow operations
      console.warn(`${entry.name}: ${entry.duration.toFixed(2)}ms`);
    }
  }
});

observer.observe({ entryTypes: ['measure'] });
```

**Compare to Step 98 Baseline**:
```bash
# Step 98 performance test captures baseline metrics
npm test -- --grep "performance-suite" --prefix src/versions/v2.0.0

# Review test output for baseline p50/p95/p99 per handler
# Compare current measurements to baseline
# If current >baseline: Performance regression detected
```

---

## Optimization Checklist

Use this checklist when investigating performance issues or preparing for release.

- [ ] **Baseline Comparison**: Compare current latency to Step 112 baseline per handler tier
  - Run: `curl http://localhost:3000/bridge/profiler | jq '.handlers[] | {name, p99}'`
  - Expected: All p99 <tier threshold (fast <2s, medium <10s, slow <30s)

- [ ] **Handler Profiling**: Identify slowest handlers using Step 96 profiler
  - Run: `curl http://localhost:3000/bridge/profiler | jq '.handlers | sort_by(.p99) | reverse | .[0:5]'`
  - Action: Profile top 5 slowest; optimize or increase timeout

- [ ] **Memory Check**: Run sustained load test; verify memory delta
  - Run: `npm test -- --grep "sustained-load" --prefix src/versions/v2.0.0`
  - Expected: Avg delta <10KB/30s; peak <50MB

- [ ] **Error Rate Review**: Check current error rate vs baseline
  - Run: `curl http://localhost:3000/bridge/profiler | jq '.metrics | {totalRequests, totalErrors, errorRate}'`
  - Expected: Error rate <1% (unintended errors)

- [ ] **Concurrent Load Test**: Run concurrent stress test
  - Run: `npm test -- --grep "concurrent-load" --prefix src/versions/v2.0.0`
  - Expected: p99 <500ms @100 parallel requests

- [ ] **Node.js Profiler**: If needed, attach --inspect for deep analysis
  - Run: `node --inspect=9229 src/versions/v2.0.0/core-server.js`
  - Action: Use Chrome DevTools to profile CPU/memory

- [ ] **Timeout Review**: Adjust timeouts based on profiler results
  - Action: Update Step 64/71 timeout policies; verify with profiler
  - Expected: Handler p99 <75% of timeout (safety margin)

- [ ] **Cache Optimization**: Clear caches if memory/latency high
  - Run: `curl -X POST http://localhost:3000/bridge -d '{"messageId":"1","messageType":"bridge:reload","data":{"scope":"symbols"}}'`
  - Action: Verify latency improvement after clear

- [ ] **Middleware Tuning**: Reduce logging verbosity if throughput low
  - Action: Update Step 72 log level from 'debug' to 'warn'
  - Expected: Throughput increase by 10-15%

- [ ] **Final Validation**: Re-run Step 98–99 tests after optimization
  - Run: `npm test -- --grep "performance-suite|concurrent-load|sustained-load" --prefix src/versions/v2.0.0`
  - Expected: All tests pass; metrics back to/above baseline

---

## Performance Budgets

A "performance budget" is a threshold for acceptable performance metrics. Use these budgets when evaluating performance:

| Metric | Budget | Scope | Check Frequency |
|--------|--------|-------|-----------------|
| **Handler p99 latency** | Tier threshold | Per handler | Per release (Step 112) |
| **Overall throughput** | >300 msg/sec | Aggregate | Per release (Step 98) |
| **Memory peak** | <50 MB | Total process | Per release (Step 99) |
| **Error rate** | <1% unintended | Aggregate | Per release (Step 99) |
| **Concurrent p99** | <500ms @100 parallel | Aggregate | Per release (Step 99) |

**Enforcement**:
- Release gates in Step 112 block release if budgets exceeded
- QA must sign-off on any intentional budget overages
- Document budget changes in release notes

---

## Additional Resources

- **TROUBLESHOOTING-GUIDE.md**: Symptom-based diagnosis and remediation (~/docs/TROUBLESHOOTING-GUIDE.md)
- **HANDLER-ERROR-CATALOG.mjs**: Programmatic error index (~/src/versions/v2.0.0/tests/mocks/handler-error-catalog.mjs)
- **HANDLER-REGRESSION-GUIDE.md**: Regression testing and severity interpretation (~/docs/HANDLER-REGRESSION-GUIDE.md)
- **Step 96 Profiler**: Real-time performance metrics (http://localhost:3000/bridge/profiler)
- **Step 98 Performance Tests**: Baseline throughput/latency tests (~/src/versions/v2.0.0/tests/handler-performance.test.mjs)
- **Step 99 Stress Tests**: Concurrent load, sustained load, error injection (~/src/versions/v2.0.0/tests/handler-stress.test.mjs)
