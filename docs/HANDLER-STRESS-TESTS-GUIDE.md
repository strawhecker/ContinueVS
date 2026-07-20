# Handler Stress Tests Guide (Step 99)

**Status**: Step 99 Implementation  
**Last Updated**: 2024-01-15  
**Related Steps**: 97 (Compliance), 98 (Performance), 110 (E2E), 112 (Regression), 115 (Part III Gate)

---

## Overview

Step 99 implements a comprehensive stress testing framework for all 20 bridge handlers (Steps 76–95). It validates handler stability, isolation, and predictability under adversity: concurrent load, error injection, sustained throughput, and cascading failures.

**Key Goals**:
- ✅ No handler crashes under 100 concurrent requests
- ✅ p99 latency <500ms (vs baseline p99 <100ms)
- ✅ Memory stable over 30s sustained load (no >50MB growth)
- ✅ Error rate <5% under error injection
- ✅ Handler isolation (one failure doesn't poison others)

---

## Architecture

### Components

#### 1. **stress-test-engine.mjs** (~600 lines)
Orchestrates all stress scenarios and collects metrics.

**Core Classes**:
- `StressTestEngine` — Main coordinator
- `ErrorInjector` — Fault simulation (timeouts, protocol errors, missing deps)

**Scenario Runners**:
```javascript
await engine.runConcurrencyScenario(config)
await engine.runErrorInjectionScenario(config)
await engine.runSustainedLoadScenario(config)
await engine.runCascadingFailureScenario(config)
```

**Metrics Collected**:
- Latency percentiles (p50, p95, p99, max)
- Success/error rates
- Memory profiling (delta, peak)
- Per-handler results
- Error breakdown by type

#### 2. **handler-stress-tests.test.mjs** (~900 lines)
80+ test cases organized in 4 suites.

**Test Organization**:
- **Suite 1**: High Concurrency (20+ tests)
- **Suite 2**: Error Injection (20+ tests)
- **Suite 3**: Sustained Load (20+ tests)
- **Suite 4**: Cascading Failures (20+ tests)
- **Cross-Scenario**: Validation tests

**Framework**: Mocha + Chai (ESM)

#### 3. **stress-test-fixtures.mjs** (~500 lines)
Message payloads and error scenarios.

**Fixtures**:
```javascript
getConcurrencyFixtures()        // High-volume payload templates
getErrorInjectionFixtures()     // Error scenarios
getSustainedLoadFixtures(rate)  // Realistic load patterns
getCascadingFailureFixtures()   // Multi-phase failure scenarios
```

**Helpers**:
- `generateMessagePayload(messageType)`
- `validateMessagePayload(message)`
- `createMessageBatch(count, types)`

---

## Stress Scenarios

### Scenario 1: High Concurrency

**Goal**: Validate handler performance and isolation under parallel requests.

**Configuration**:
```javascript
{
  concurrencyLevel: 50,      // Parallel requests per batch
  requestsPerHandler: 500,   // Total requests per handler
  measureMemory: true,
}
```

**Execution**:
1. Batch handlers into groups of `concurrencyLevel`
2. Dispatch all batches concurrently via `Promise.all()`
3. Collect latency, memory, and error metrics
4. Calculate percentiles (p50, p95, p99)

**Success Criteria**:
- p99 latency < 500ms (baseline < 100ms, 5x margin)
- Error rate < 5%
- Throughput > 50 req/s
- Memory stable (avg delta < 100KB/req)

**Sample Results**:
```
[Concurrency] Complete: 9500/10000 success
  p50=8ms, p95=42ms, p99=120ms, max=350ms
  error_rate=5.0%, throughput=320.5 req/s
  memory_avg_delta=8.3 KB
```

### Scenario 2: Error Injection

**Goal**: Test handler robustness under systematic failures.

**Error Types**:
1. **Timeout** — Handler exceeds time limit
2. **Protocol Error** — Malformed response
3. **Missing Dependency** — Service unavailable
4. **Validation Error** — Invalid input
5. **Permission Error** — Access denied

**Configuration**:
```javascript
{
  concurrencyLevel: 20,
  requestsPerHandler: 100,
  errorInjection: {
    enabled: true,
    scenarios: ['timeout', 'protocol_error', 'missing_dependency'],
    injectionRate: 0.5,  // Inject 50% of requests
  },
}
```

**Execution**:
1. For each request, determine if injection should occur
2. If yes, wrap handler to inject specific error
3. Measure handler response (success, error type, latency)
4. Track error breakdown by type

**Success Criteria**:
- Error rate ≈ injection rate (±20% tolerance)
- Handler isolation maintained (errors don't cascade)
- p99 latency < 1000ms (relaxed vs baseline)
- Error types properly categorized

**Sample Results**:
```
[Error Injection] Complete: 1500/3000 success
  error_rate=50.2% (target=50%)
  Breakdown: timeout=600, protocol_error=500, missing_dep=400
  p99=280ms (higher than baseline due to errors)
```

### Scenario 3: Sustained Load

**Goal**: Detect memory leaks and performance degradation over time.

**Configuration**:
```javascript
{
  durationSeconds: 30,
  messagesPerSecond: 1000,
  measureMemory: true,
  captureRawMetrics: true,
}
```

**Execution**:
1. Divide scenario into 10s phases
2. Each phase: dispatch ~10,000 messages (50 msg/handler)
3. Track memory usage, latency, and success rate per phase
4. Detect unbounded growth trends

**Success Criteria**:
- Error rate < 5%
- Memory stable (no >50MB growth over 30s)
- No unbounded growth trend (last third vs first third)
- Latency consistent (p95/p99 variance < 50%)
- Throughput near target (±30% tolerance)

**Sample Results**:
```
[Sustained Load] Complete: 29800/30000 success
  duration=31.2s, throughput=955.8 req/s
  error_rate=0.7%, memory_avg_delta=6.2KB

  Phase Breakdown:
    Phase 0: 1000 msgs, success_rate=100%, mem_avg_delta=7.5KB
    Phase 1: 1000 msgs, success_rate=99.8%, mem_avg_delta=6.1KB
    Phase 2: 1000 msgs, success_rate=99.5%, mem_avg_delta=5.8KB

  Growth Analysis: First third avg=7.1KB, Last third avg=5.9KB
    Growth: -16.9% (healthy, no leak)
```

### Scenario 4: Cascading Failures

**Goal**: Verify handler isolation and graceful degradation.

**Configuration**:
```javascript
{
  concurrencyLevel: 20,
  requestsPerHandler: 50,
  // Two phases: baseline, then inject failure
}
```

**Execution**:

**Phase 1 — Baseline**:
1. Run all 20 handlers normally
2. Record success rate per handler (should be ~100%)

**Phase 2 — Failure Injection**:
1. Select handler to fail (e.g., `bridge:gitIntegration`)
2. Wrap its handler to always return failure
3. Run all 20 handlers again
4. Measure:
   - Failing handler: success rate ~0%
   - Other handlers: success rate unchanged (~100%)
   - System error rate: (1 failure out of 20) ≈ 5%

**Success Criteria**:
- Isolation rate > 80% (19+ of 20 handlers unaffected)
- Failing handler error rate = 100%
- Other handlers maintain baseline success rates
- System error rate matches expected (1/20 ≈ 5%)

**Sample Results**:
```
[Cascading Failure] Complete: 1800/2000 success
  isolation_rate=95% (19/20 isolated)
  error_rate=5.2% (1 handler failing)

  Handler Breakdown:
    bridge:refactor         — baseline=100%, failure_phase=100% ✓ ISOLATED
    bridge:fixSuggestion    — baseline=100%, failure_phase=100% ✓ ISOLATED
    ...
    bridge:gitIntegration   — baseline=100%, failure_phase=0%   ✓ FAILED (intended)
    ...
    bridge:profiler         — baseline=100%, failure_phase=100% ✓ ISOLATED
```

---

## Running the Tests

### Full Suite
```bash
# Run all stress test scenarios (5–10 minutes)
npm test -- handler-stress-tests.test.mjs

# With verbose output
npm test -- handler-stress-tests.test.mjs --reporter json > stress-results.json
```

### Individual Scenarios
```bash
# Concurrency only
npm test -- handler-stress-tests.test.mjs --grep "High Concurrency"

# Error injection
npm test -- handler-stress-tests.test.mjs --grep "Error Injection"

# Sustained load
npm test -- handler-stress-tests.test.mjs --grep "Sustained Load"

# Cascading failures
npm test -- handler-stress-tests.test.mjs --grep "Cascading Failures"
```

### Per-Handler Tests
```bash
# Test individual handler (refactor)
npm test -- handler-stress-tests.test.mjs --grep "bridge:refactor"
```

---

## Interpreting Results

### Success Gates

All gates must pass for Step 99 to complete:

```
✓ Concurrency p99 <500ms      — latency remains bounded under load
✓ Error rate <5%              — system resilience (baseline)
✓ Memory stable               — no leaks or unbounded growth
✓ Isolation >80%              — handler independence
✓ All 20 handlers tested      — complete coverage
✓ Build: 0 warnings/errors    — compilation success
✓ Tests: 100% passing         — functional correctness
```

### Latency Interpretation

| Metric | Good | Acceptable | Concerning |
|--------|------|-----------|------------|
| p50    | <50ms | <100ms | >100ms |
| p95    | <150ms | <250ms | >250ms |
| p99    | <500ms (stress) | <200ms (baseline) | >500ms |
| max    | <2000ms | <5000ms | >5000ms |

### Memory Interpretation

| Metric | Good | Acceptable | Concerning |
|--------|------|-----------|------------|
| avg_delta | <10KB | <50KB | >50KB |
| max_delta | <50MB | <100MB | >100MB |
| trend (30s) | -5% to +10% | +10% to +30% | >+30% |

### Error Rate Interpretation

| Scenario | Good | Acceptable | Failing |
|----------|------|-----------|--------|
| Concurrency | <1% | <5% | >5% |
| Error Injection | ≈ injection rate | ±20% tolerance | >±30% |
| Sustained Load | <1% | <5% | >5% |
| Cascading | 1 handler failing | ±10% | >±20% |

---

## Troubleshooting

### Problem: Tests timeout (>10 minutes)

**Cause**: Slow handlers or high concurrency level
**Solution**:
```javascript
// In handler-stress-tests.test.mjs
// Reduce concurrency or request count
await engine.runConcurrencyScenario({
  concurrencyLevel: 25,  // was 50
  requestsPerHandler: 250,  // was 500
});
```

### Problem: High memory usage (>500MB)

**Cause**: Memory leak in handler or metrics collection
**Solution**:
1. Disable raw metrics capture
```javascript
scenarioDefaults: {
  captureRawMetrics: false,  // Disable for large runs
}
```
2. Profile with `--inspect` flag
```bash
node --inspect node_modules/.bin/mocha handler-stress-tests.test.mjs
```

### Problem: High error rate (>10%)

**Cause**: Handler bugs or infrastructure issues
**Solution**:
1. Check error types in results
2. Run individual handler tests
3. Verify handler implementation (Step 76–95)

### Problem: Isolation test fails (isolation rate <80%)

**Cause**: Error cascading between handlers
**Solution**:
1. Review error propagation in handler-dispatcher.mjs (Step 14)
2. Ensure error recovery middleware (Step 74) is active
3. Check for shared state between handlers

### Problem: Cascading failure phase shows <80% success for other handlers

**Cause**: Failing handler is affecting others via shared resources
**Solution**:
1. Verify handler isolation (no shared global state)
2. Check middleware error recovery (Step 74)
3. Verify message routing isolation (Step 47)

---

## Integration with Other Steps

### Step 97: Compliance Baseline
- Step 97 establishes happy-path p99 <100ms
- Step 99 validates p99 <500ms under stress (5x margin)
- If Step 99 p99 > 500ms, debug Step 97 baseline first

### Step 98: Performance Tests
- Step 98 establishes baseline throughput
- Step 99 validates throughput under sustained load
- Compare throughput degradation between steps

### Step 110: End-to-End Scenarios
- Step 110 uses stress fixtures for realistic load
- Step 99 validates individual handler robustness
- Together: handler + system-level validation

### Step 112: Regression Suite
- Step 112 uses Step 99 results as baseline
- Future runs compared against Step 99 metrics
- Detect performance regressions early

### Step 115: Part III Gate
- Step 99 stress test report required for Part III approval
- Success gates: all ✅ before proceeding to Phase IV (Steps 116+)

---

## Continuous Monitoring

### Metrics Export
After tests complete, export results for dashboarding:

```bash
# Generate JSON report
npm test -- handler-stress-tests.test.mjs --reporter json > reports/stress-2024-01-15.json

# Append to history
cat reports/stress-2024-01-15.json >> reports/stress-history.ndjson
```

### Dashboard Integration (Optional)
```javascript
// Parse NDJSON and load into monitoring system
const history = fs.readFileSync('reports/stress-history.ndjson', 'utf-8')
  .split('\n')
  .filter(l => l)
  .map(l => JSON.parse(l));

// Plot latency trend
plotTrend('p99_latency', history.map(r => r.results.concurrency.latencyPercentiles.p99));
```

---

## Performance Tuning Guide

### If p99 latency is high (>500ms):

1. **Check concurrent request handling**
   - Reduce `concurrencyLevel` in test config
   - Profile with `--inspect`

2. **Check handler performance**
   - Profile individual handlers (Step 97 baseline)
   - Look for synchronous I/O, blocking operations

3. **Check middleware overhead**
   - Measure Step 47 routing, Step 72 logging, Step 73 validation
   - Disable in test if overhead is high

### If error rate is high (>5%):

1. **Identify failing handlers**
   - Review `errorBreakdown` in results
   - Check Step 74 error recovery middleware

2. **Check error injection configuration**
   - Verify `injectionRate` < actual error rate
   - Review `scenarios` being tested

### If memory grows unbounded:

1. **Enable memory profiling**
   - Capture heap snapshots before/after test
   - Use Chrome DevTools to analyze

2. **Check for resource leaks**
   - Handlers not cleaning up: file handles, promises, timers
   - Middleware not releasing context

3. **Reduce message volume**
   - Lower `messagesPerSecond` in sustained load test
   - Reduce `requestsPerHandler` count

---

## Related Files

- **Step 99**: `src/versions/v2.0.0/lib/stress-test-engine.mjs`
- **Step 99**: `src/versions/v2.0.0/tests/handler-stress-tests.test.mjs`
- **Step 99**: `src/versions/v2.0.0/tests/mocks/stress-test-fixtures.mjs`
- **Step 97**: `docs/HANDLER-COMPLIANCE-TESTS-GUIDE.md` (baseline)
- **Step 98**: `docs/PERFORMANCE-BASELINE.md` (throughput expectations)
- **Step 110**: `docs/E2E-SCENARIOS-GUIDE.md` (system-level tests)
- **Step 112**: `docs/REGRESSION-SUITE-GUIDE.md` (comparison)

---

## Summary

Step 99 provides comprehensive stress testing for all bridge handlers. It validates:

✅ Handler stability under concurrent load  
✅ Graceful error handling and recovery  
✅ Memory stability and leak detection  
✅ Handler isolation and independence  
✅ System resilience under cascading failures  

All results feed into Step 110 (E2E scenarios), Step 112 (regression suite), and Step 115 (Part III gate).
