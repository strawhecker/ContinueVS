# Profiler Integration Handler (Step 96)

## Overview

The Profiler Integration Handler aggregates real-time metrics from the bridge infrastructure to provide performance diagnostics and health monitoring. This optional handler enables compliance baseline validation for testing phases (Steps 97–99) and future metrics dashboard integration (Step 101).

**Status**: Optional (does not block subsequent steps)  
**Dependencies**: Step 64 (TimeoutManager), Step 72 (MessageLogger), Step 74 (ErrorRecoveryMetrics), Step 66 (SymbolExtractor)  
**Message Type**: `bridge:getProfilerData`  
**Timeout Policy**: Fast (2000ms)  
**Stability Tier**: Core  

## Architecture

### Non-Invasive Aggregation Model

The profiler **reads only** from existing metric collectors and does not modify handler code or inject tracing:

```
Bridge Infrastructure                Profiler (Step 96)
─────────────────────────           ──────────────────

TimeoutManager (Step 64)    ────┐
  ├─ latencies[]                 │
  ├─ completedRequests           │    Aggregate Metrics
  ├─ totalTimeouts               ├──→ ├─ Per-handler latency
  └─ p99LatencyMs                │    ├─ Error rates
                                │    └─ Cache hit rates
MessageLogger (Step 72)     ────┤
  ├─ totalMessages                │    Build Report
  ├─ requestCount                 ├──→ ├─ JSON envelope
  └─ averageLatency               │    ├─ Summary stats
                                │    └─ Timestamp
ErrorRecoveryMetrics (74)   ────┤
  ├─ errorCount                  │    Return Response
  ├─ successCount                 ├──→ bridge:getProfilerData
  └─ timeoutCount                │    response (JSON-RPC)

SymbolExtractor (Step 66)   ────┐
  ├─ hitCount
  ├─ missCount
  └─ cacheSize
```

### Metrics Collection Strategy

1. **TimeoutManager** → Latency percentiles (p50/p95/p99), timeout counts
2. **MessageLogger** → Message volume, average latency
3. **ErrorRecoveryMetrics** → Error rates, success counts
4. **SymbolExtractor** (optional) → Cache hit rates
5. **Summary** → Slowest handler, highest error rate, total requests

## Message Contract

### Request

```javascript
{
  messageId: "uuid-string",
  messageType: "bridge:getProfilerData",
  data: {}  // No payload required
}
```

### Response (Success)

```javascript
{
  success: true,
  timestamp: "2024-01-15T10:30:45.123Z",
  data: {
    handlers: [
      {
        name: "aggregate",  // Handler identifier
        latency: {
          p50: 25.5,        // Median latency (ms)
          p95: 85.2,        // 95th percentile (ms)
          p99: 98.7         // 99th percentile (ms)
        },
        errorRate: 0.05,    // 5% error rate (0.0-1.0)
        requestCount: 1000, // Total requests processed
        timeoutCount: 50,   // Number of timeouts
        cacheHitRate: 0.75  // Optional: 75% cache hits
      }
    ],
    summary: {
      slowestHandler: "aggregate",
      maxP99: 98.7,
      highestErrorRate: "aggregate",
      maxErrorRate: 0.05,
      totalRequests: 1000,
      totalTimeouts: 50,
      totalErrors: 50,
      generationTimeMs: 3  // Report generation time
    }
  }
}
```

### Response (Error)

```javascript
{
  success: false,
  timestamp: "2024-01-15T10:30:45.123Z",
  error: {
    code: -32603,  // JSON-RPC internal error
    message: "Failed to generate profiler report",
    data: {
      details: "Original error message",
      code: "ERROR_CODE"
    }
  }
}
```

## Handler Metrics Schema

Each handler in the profiler report includes:

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Handler message type or identifier |
| `latency.p50` | number | 50th percentile latency (milliseconds) |
| `latency.p95` | number | 95th percentile latency (milliseconds) |
| `latency.p99` | number | 99th percentile latency (milliseconds) |
| `errorRate` | number | Error rate (0.0–1.0, where 1.0 = 100%) |
| `requestCount` | number | Total requests handled |
| `timeoutCount` | number | Number of timeout events |
| `cacheHitRate` | number | Optional; cache hit rate (0.0–1.0) |

## Integration Points

### Step 64: TimeoutManager
- Provides latency metrics and timeout tracking
- Profiler calls `getMetrics()` to extract `latencies[]`, `completedRequests`, `totalTimeouts`
- Percentiles calculated from latency array

### Step 72: MessageLogger
- Provides message volume tracking
- Profiler calls `getStats()` to extract message counts
- Used for secondary validation of request volumes

### Step 74: ErrorRecoveryMetrics
- Provides error rate and recovery statistics
- Profiler calls `getErrorRate()` to extract error/success/timeout counts
- Calculates error rate as: `errors / (errors + success)`

### Step 66: SymbolExtractor
- Optional integration for cache hit rate
- Profiler calls `getCacheStats()` if available
- Gracefully omitted if unavailable

### Step 71: Handler Registration
- Profiler handler registered as `bridge:getProfilerData`
- Core stability tier, fast timeout (2000ms)
- Invoked directly by handler dispatcher

### Step 97: Handler Compliance Tests
- **Primary consumer** of profiler data
- Uses p50/p95/p99 latencies as compliance baselines
- Validates error rates against thresholds
- Tracks performance across test scenarios

### Step 98: Handler Performance Tests
- Consumes profiler data for performance benchmarking
- Compares test results against profiler baseline
- Identifies regressions

### Step 99: Handler Stress Tests
- Monitors error rates via profiler
- Validates graceful degradation under load
- Tracks timeout rate changes

### Step 101: Metrics Dashboard
- **Future integration** for visualization
- Profiler provides real-time snapshots
- Dashboard can implement persistence/trending

## Usage Examples

### Basic Request (JavaScript)

```javascript
import { createProfilerHandler } from './profiler-integration.mjs';

// Create handler with dependencies (from Steps 64, 72, 74, 66)
const profilerHandler = createProfilerHandler(
  timeoutManager,        // Step 64
  messageLogger,         // Step 72
  errorRecoveryMetrics,  // Step 74
  symbolExtractor,       // Step 66 (optional)
  logger,                // Optional
  metrics                // Optional
);

// Invoke handler
const message = {
  messageId: 'profile-req-001',
  messageType: 'bridge:getProfilerData',
  data: {}
};

const context = { correlationId: 'trace-123' };
const response = await profilerHandler(message, context);

if (response.success) {
  console.log(`Max latency (p99): ${response.data.summary.maxP99}ms`);
  console.log(`Error rate: ${response.data.summary.maxErrorRate * 100}%`);
  console.log(`Total requests: ${response.data.summary.totalRequests}`);
} else {
  console.error(`Profiler error: ${response.error.message}`);
}
```

### Compliance Test Integration (Step 97)

```javascript
// Step 97 compliance test retrieving baseline
async function validateHandlerCompliance() {
  const profileReport = await profilerHandler(
    { messageId: 'compliance-baseline-001', messageType: 'bridge:getProfilerData' },
    context
  );

  if (!profileReport.success) {
    throw new Error('Failed to retrieve profiler baseline');
  }

  // Use p99 as compliance baseline
  const p99Baseline = profileReport.data.summary.maxP99;
  assert(p99Baseline < 100, `Handler p99 ${p99Baseline}ms exceeds baseline 100ms`);

  // Validate error rate
  const errorRateBaseline = profileReport.data.summary.maxErrorRate;
  assert(errorRateBaseline < 0.05, `Error rate ${errorRateBaseline * 100}% exceeds 5%`);
}
```

### Performance Monitoring (Step 98)

```javascript
// Step 98 performance test using profiler as baseline
async function benchmarkHandlerPerformance() {
  // Get baseline
  const baseline = await profilerHandler(...);
  const baselineP95 = baseline.data.summary.maxP99; // Use p99 as upper bound

  // Run benchmark
  const results = await runPerformanceBenchmark();
  const actualP95 = calculatePercentile(results, 95);

  // Compare against baseline
  const regression = (actualP95 - baselineP95) / baselineP95;
  console.log(`Performance regression: ${(regression * 100).toFixed(2)}%`);

  if (regression > 0.1) {  // 10% threshold
    throw new Error('Performance regression detected');
  }
}
```

## Error Handling

### Exception Classes

**ProfilerError**: Base exception for profiler operations
- `code`: Operation type or error category
- `details`: Additional context

**JSON-RPC Error Codes**:
- `-32603` (Internal error): Aggregation failure, partial metrics
- `-32602` (Invalid params): Malformed request message

### Graceful Degradation

If any metric source is unavailable or throws an exception:

1. **Single metric source fails** → Report includes available data, warns in logs
2. **Multiple sources fail** → Return JSON-RPC error response with best-effort data in details
3. **No valid metrics** → Return error response with empty handler array
4. **Optional dependencies missing** → Omit fields (e.g., cache hit rate)

### Common Error Scenarios

| Scenario | Behavior |
|----------|----------|
| TimeoutManager unavailable | Latency percentiles = 0, continue with other metrics |
| MessageLogger unavailable | Message volume = 0, continue with timeout metrics |
| SymbolExtractor unavailable | Cache hit rate omitted from response |
| All metrics unavailable | Return error response (-32603) |
| Invalid message structure | Return error response (-32602) |
| Report generation > 20ms | Log warning, still return data |

## Performance Characteristics

### Latency Gates

- **Report generation**: < 20ms for 10 handlers ✅
- **Percentile calculation**: < 5ms for 10,000 latencies
- **Aggregation**: < 10ms for all metric sources

### Memory Usage

- **Fixed overhead**: ~5KB per report
- **Variable overhead**: ~10 bytes per latency entry
- **Bounded growth**: Metric sources handle their own memory management

### Throughput

- **Single request**: No queuing required (fast execution)
- **Concurrent requests**: Handled by message dispatcher (Step 14)
- **Typical load**: < 1% CPU overhead for profiler operations

## Testing Strategy

### Test Execution

```bash
# Run profiler integration tests
cd src/versions/v2.0.0
npx mocha tests/profiler-integration.test.mjs --timeout 10000

# Expected: 35/35 tests passing (~500ms)
```

### Test Coverage

**35 Comprehensive Test Cases**:

| Suite | Tests | Focus |
|-------|-------|-------|
| Initialization | 4 | Dependency injection, validation |
| Metrics Aggregation | 6 | Per-source aggregation, graceful degradation |
| Percentile Calculation | 5 | p50/p95/p99 computation, edge cases |
| Report Generation | 4 | Structure, summary stats, timestamps |
| Message Handling | 5 | Request parsing, response envelope, validation |
| Error Handling | 4 | Exception recovery, best-effort data |
| Performance Gates | 3 | Latency targets, memory efficiency |
| Data Freshness | 2 | Timestamp accuracy, state reflection |
| Integration Patterns | 2 | Mock compatibility, real-world scenarios |

### Coverage Validation

- ✅ All metric sources aggregated correctly
- ✅ Percentile calculations accurate (p50, p95, p99)
- ✅ Error handling recovers gracefully
- ✅ Performance gates met (<20ms)
- ✅ Timestamp freshness within ±100ms
- ✅ Graceful degradation for missing dependencies

## Troubleshooting

### Problem: Report Generation > 20ms

**Cause**: Large latency arrays or slow metric sources  
**Solution**:
1. Check TimeoutManager.getMetrics() performance
2. Limit latency array size (cap at 10,000 entries)
3. Profile metric source implementations

### Problem: Cache Hit Rate Missing

**Cause**: SymbolExtractor not available or not configured  
**Solution**:
1. Verify SymbolExtractor is passed to profiler factory
2. Check getCacheStats() method exists
3. This is graceful — handler still works without cache metrics

### Problem: Error Rate Inaccurate

**Cause**: ErrorRecoveryMetrics not synchronized with actual errors  
**Solution**:
1. Verify ErrorRecoveryMetrics is updated on handler errors
2. Check getErrorRate() returns fresh data (not cached)
3. Ensure success count and error count sum correctly

### Problem: Timestamp Inaccurate

**Cause**: System clock drift or async delays  
**Solution**:
1. Verify system time is synchronized (NTP)
2. Check report timestamp is generated at end of aggregation (not start)
3. Within ±100ms is acceptable for compliance

## FAQ

**Q: Does the profiler affect handler performance?**  
A: No. The profiler reads only from existing metrics; it doesn't inject tracing or modify handler code. Overhead is <1% CPU.

**Q: Can I persist profiler reports for trending?**  
A: Step 96 provides real-time snapshots only. Step 101 (metrics dashboard) will implement persistence and trending.

**Q: What if metrics are unavailable?**  
A: Profiler gracefully degrades. If all sources fail, it returns a JSON-RPC error. Partial data is returned when possible.

**Q: Should I query profiler frequently?**  
A: Yes, profiler is designed for frequent polling (e.g., every 1–5 seconds). Performance gate is 20ms, so overhead is minimal.

**Q: How do I integrate profiler with Step 97 compliance tests?**  
A: Use p50/p95/p99 latencies as compliance baselines. See "Compliance Test Integration" example above.

**Q: Can I extend the profiler to include custom metrics?**  
A: Not in Step 96. Custom metrics are planned for Step 101 (metrics dashboard). For now, profiler aggregates standard bridge metrics only.

## Related Steps

- **Step 64**: TimeoutManager (latency source)
- **Step 72**: MessageLogger (volume source)
- **Step 74**: ErrorRecoveryMetrics (error source)
- **Step 66**: SymbolExtractor (cache source)
- **Step 71**: Handler Registration (deployment)
- **Step 97**: Handler Compliance Tests (primary consumer)
- **Step 98**: Handler Performance Tests (secondary consumer)
- **Step 99**: Handler Stress Tests (error rate monitoring)
- **Step 101**: Metrics Dashboard (future visualization)

## Next Steps

After Step 96 completion:
1. **Step 97**: Use profiler baseline for handler compliance validation
2. **Step 98**: Benchmark handler performance against profiler p99
3. **Step 99**: Monitor error rates under stress via profiler
4. **Step 101**: Visualize profiler reports in metrics dashboard
