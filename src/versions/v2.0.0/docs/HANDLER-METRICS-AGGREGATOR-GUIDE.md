# Handler Metrics Aggregator Guide (Step 109)

**Version**: 2.0.0  
**Status**: Complete  
**Last Updated**: 2024-01-15  
**Location**: `src/versions/v2.0.0/lib/`

## Overview & Design

The Handler Metrics Aggregator is the persistent metrics collection layer for the ContinueVS Bridge. It bridges real-time profiling (Step 96) with historical regression analysis (Steps 98, 110, 112).

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ ProfilerHandler (Step 96)                                       │
│ └─ Real-time snapshots: latency, errors, request counts        │
│    (5 handlers: bridge:search, bridge:completion, ...)         │
└──────────────────────┬──────────────────────────────────────────┘
                       │ (every 5 seconds)
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│ HandlerMetricsAggregator (THIS - Step 109)                      │
│ ├─ SnapshotCollector: polls ProfilerHandler                    │
│ ├─ MetricsStore: persists to ~/.continue/metrics/              │
│ └─ TrendAnalyzer: rolling averages, variance, anomalies        │
└──────────────────────┬──────────────────────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
   ┌────────────┐ ┌─────────┐ ┌──────────────┐
   │ Step 98    │ │Step 110 │ │ Step 112     │
   │Regression │ │E2E Base │ │Regression    │
   │Detector   │ │Scenarios│ │ Suite        │
   └────────────┘ └─────────┘ └──────────────┘
```

### Key Components

**HandlerMetricsAggregator** (handler-metrics-aggregator.mjs)
- Main orchestrator class
- Manages collection, storage, and querying
- Subscribes to ProfilerHandler via RPC interface
- Provides query API for historical metrics

**SnapshotCollector**
- Polls ProfilerHandler every N seconds (default 5s)
- Buffers snapshots in memory (configurable, default 100)
- Triggers persistence on timer or buffer overflow

**MetricsStore** (metrics-historical-store.mjs)
- Disk persistence layer
- Format: JSON lines (line-delimited) with daily rotation
- File naming: `metrics-YYYY-MM-DD.jsonl`
- Auto-creates `~/.continue/metrics/` directory
- Atomic writes via temp file + rename

**TrendAnalyzer** (metrics-trend-analyzer.mjs)
- Statistical analysis: rolling averages, variance, anomalies
- Calculates trends (direction: INCREASING, STABLE, DECREASING)
- Generates confidence scores
- Supports multiple analysis windows (5s, 1m, 5m)

### Time-Series Model

**Snapshot Structure**
```javascript
{
  timestamp: 1705315200000,  // Milliseconds since epoch
  handlers: [
    {
      name: 'bridge:search',          // Handler message type
      latency: {
        p50: 15,                       // 50th percentile (median) in ms
        p95: 35,                       // 95th percentile in ms
        p99: 80                        // 99th percentile in ms
      },
      errorRate: 0.01,                // Decimal 0.0-1.0
      requestCount: 250,              // Total requests in window
      timeoutCount: 2,                // Timeout requests
      cacheHitRate: 0.78              // Optional
    },
    ...
  ],
  metadata: {
    collectionInterval: 5000,
    bufferSize: 100
  }
}
```

**File Format: JSON Lines**
```
{"timestamp":1705315200000,"handlers":[...],"metadata":{...}}
{"timestamp":1705315205000,"handlers":[...],"metadata":{...}}
{"timestamp":1705315210000,"handlers":[...],"metadata":{...}}
```

### Collection Interval

- **Default**: 5 seconds
- **Rationale**: Captures real-time variations without excessive I/O
- **Configurable**: Pass `collectionInterval` in config
- **Persistence**: Buffer persists every 100 snapshots (configurable)

### Storage Location & Cleanup

- **Directory**: `~/.continue/metrics/` (platform-aware: Windows, Linux, macOS)
- **File Retention**: 7 days by default (configurable)
- **Cleanup**: Run `aggregator.cleanup(retentionDays)` to purge old files
- **Size**: ~500B per snapshot → ~7MB per day (typical)

---

## API Reference

### HandlerMetricsAggregator

#### Constructor

```javascript
const aggregator = new HandlerMetricsAggregator({
  logger,                    // Optional logger
  metricsCollector,         // Optional host-side collector (C#)
  collectionInterval: 5000, // Milliseconds between snapshots
  bufferSize: 100,          // Snapshots to buffer before persist
  retentionDays: 7,         // Days to retain historical data
  baseDir: null,            // Override storage directory
  profilerRpc: rpc          // RPC interface to ProfilerHandler
});
```

#### Methods

**initialize()**
```javascript
await aggregator.initialize();
// Creates storage directory, initializes stores
// Must be called before start()
```

**start()**
```javascript
await aggregator.start();
// Begin collection: subscribe to ProfilerHandler
// Polls every collectionInterval milliseconds
// Returns immediately; collection runs in background
```

**stop()**
```javascript
await aggregator.stop();
// Stop collection gracefully
// Persists any remaining buffered snapshots
// Safe to call multiple times
```

**queryMetrics(handlerName, options)**
```javascript
const metrics = await aggregator.queryMetrics('bridge:search', {
  since: Date.now() - 60 * 60 * 1000,  // 1 hour ago
  until: Date.now(),                    // Now
  limit: 100,                           // Max results
  aggregation: 'average'                // 'latest' or 'average'
});
// Returns: HandlerMetricData[] | throws QueryError
```

**getLatestSnapshot()**
```javascript
const snapshot = await aggregator.getLatestSnapshot();
// Returns: MetricsSnapshot | null
```

**getTrend(handlerName, window)**
```javascript
const trend = await aggregator.getTrend('bridge:search', 5);
// Returns trend report:
// {
//   handler: 'bridge:search',
//   direction: 'INCREASING' | 'DECREASING' | 'STABLE',
//   slope: -0.05,                  // Rate of change
//   confidence: 0.92,              // 0-1
//   currentP99: 85,                // Latest p99
//   rollingAverage: 82,            // 5-sample rolling avg
//   mean: 80,
//   variance: 15,
//   sampleCount: 25,
//   recommendation: 'INVESTIGATE_REGRESSION'
// }
```

**detectAnomalies(handlerName, threshold)**
```javascript
const anomalies = await aggregator.detectAnomalies('bridge:search', 2);
// threshold: 2 = 2 standard deviations
// Returns: Array<AnomalyReport> | []
// Each report:
// {
//   current: 250,                  // Current p99
//   mean: 80,
//   stdDev: 15,
//   threshold: 2,
//   lowerBound: 50,                // mean - 2*stdDev
//   upperBound: 110,               // mean + 2*stdDev
//   deviation: 10,                 // Num of stdDevs from mean
//   type: 'HIGH',
//   severity: 'CRITICAL'
// }
```

**getStorageStats()**
```javascript
const stats = await aggregator.getStorageStats();
// Returns:
// {
//   directory: '/home/user/.continue/metrics',
//   fileCount: 7,
//   totalSizeBytes: 3500000,
//   totalSizeMB: '3.34'
// }
```

**cleanup(olderThanDays)**
```javascript
await aggregator.cleanup(7);
// Delete snapshots older than N days
// Default: 7 days retention
```

### TrendAnalyzer

#### Methods

**calculateRollingAverage(latencies, window)**
```javascript
const values = [50, 52, 51, 53, 55, 54];
const rolling = analyzer.calculateRollingAverage(values, 3);
// Returns: [51, 52, 53, 54]  (8 values → 6 results for window=3)
```

**calculateVariance(samples)**
```javascript
const stats = analyzer.calculateVariance([50, 51, 50, 49, 51]);
// Returns:
// {
//   mean: 50.2,
//   variance: 0.56,
//   stdDev: 0.75,
//   count: 5
// }
```

**detectAnomalies(currentValue, history, threshold)**
```javascript
const anomaly = analyzer.detectAnomalies(200, [50, 51, 49, 51, 50], 2);
// Returns AnomalyReport or null if no anomaly
```

**generateTrend(metrics, window)**
```javascript
const metrics = [
  { latency: { p99: 50 }, name: 'handler' },
  { latency: { p99: 52 } },
  { latency: { p99: 54 } },
  { latency: { p99: 56 } }
];
const trend = analyzer.generateTrend(metrics, 5);
// Returns: TrendReport (see getTrend() above)
```

**compareMetrics(current, baseline, tolerancePercent)**
```javascript
const comparison = analyzer.compareMetrics(
  { latency: { p99: 85 }, name: 'handler' },
  { latency: { p99: 80 }, name: 'handler' },
  10  // 10% tolerance
);
// Returns:
// {
//   hasRegression: true,
//   percentChange: 6.25,
//   tolerance: 10,
//   baseline: 80,
//   current: 85,
//   severity: 'LOW',
//   recommendation: '...'
// }
```

### MetricsStore

#### Methods

**initialize()**
```javascript
await store.initialize();
// Create ~/.continue/metrics/ directory if missing
```

**append(snapshot)**
```javascript
await store.append(snapshot);
// Append snapshot to daily file (atomic write)
// Validates snapshot structure
// Throws: StoreError on validation or I/O failure
```

**read(query)**
```javascript
const results = await store.read({
  handlerName: 'bridge:search',
  since: timestamp,
  until: timestamp,
  limit: 100
});
// Returns: MetricsSnapshot[]
```

**cleanup(olderThanTimestamp)**
```javascript
await store.cleanup(Date.now() - 7 * 24 * 60 * 60 * 1000);
// Delete files older than timestamp
```

**getStorageStats()**
```javascript
const stats = await store.getStorageStats();
// Returns stats object
```

**list()**
```javascript
const files = await store.list();
// Returns: string[] - filenames ['metrics-2024-01-15.jsonl', ...]
```

---

## Data Schema

### Snapshot

```typescript
interface MetricsSnapshot {
  timestamp: number;                    // Milliseconds since epoch
  handlers: HandlerMetricData[];
  metadata?: {
    collectionInterval?: number;
    bufferSize?: number;
    [key: string]: any;
  };
}
```

### HandlerMetricData

```typescript
interface HandlerMetricData {
  name: string;                         // e.g. 'bridge:search'
  latency: {
    p50: number;                        // Milliseconds
    p95: number;
    p99: number;
  };
  errorRate: number;                    // 0.0-1.0
  requestCount: number;                 // Total requests
  timeoutCount: number;                 // Timed-out requests
  cacheHitRate?: number;                // Optional, 0.0-1.0
}
```

### TrendReport

```typescript
interface TrendReport {
  handler: string;
  direction: 'INCREASING' | 'DECREASING' | 'STABLE' | 'UNKNOWN';
  slope: number;                        // Rate of change
  confidence: number;                   // 0-1
  currentP99: number;                   // Latest latency
  rollingAverage: number;               // N-sample rolling avg
  mean: number;
  variance: number;
  sampleCount: number;
  recommendation: string;               // 'INVESTIGATE_REGRESSION', etc.
}
```

### AnomalyReport

```typescript
interface AnomalyReport {
  current: number;
  mean: number;
  stdDev: number;
  threshold: number;
  lowerBound: number;
  upperBound: number;
  deviation: number;                    // Std devs from mean
  type: 'HIGH' | 'LOW';
  severity: 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW';
}
```

---

## Usage Examples

### Initialize Aggregator in core-server.js

```javascript
import { createHandlerMetricsAggregator } from './lib/handler-metrics-aggregator.mjs';

// On bridge startup:
const aggregator = createHandlerMetricsAggregator({
  collectionInterval: 5000,
  bufferSize: 100,
  retentionDays: 7,
  profilerRpc: bridgeServer.profilerHandler  // RPC to Step 96
}, logger, metricsCollector);

await aggregator.initialize();
await aggregator.start();

// On shutdown:
process.on('SIGINT', async () => {
  await aggregator.stop();
  process.exit(0);
});
```

### Query Last 24 Hours of Data

```javascript
const oneDayAgo = Date.now() - 24 * 60 * 60 * 1000;
const metrics = await aggregator.queryMetrics('bridge:search', {
  since: oneDayAgo,
  limit: 500
});

console.log(`Retrieved ${metrics.length} snapshots`);
metrics.forEach(m => {
  console.log(`P99: ${m.latency.p99}ms, Errors: ${(m.errorRate*100).toFixed(2)}%`);
});
```

### Generate Trend Report for Regression Detection (Step 98)

```javascript
const trend = await aggregator.getTrend('bridge:search', 5);

if (trend.direction === 'INCREASING' && trend.confidence > 0.8) {
  console.warn('⚠️ Performance regression detected');
  console.log(`  Direction: ${trend.direction}`);
  console.log(`  Current P99: ${trend.currentP99}ms`);
  console.log(`  Rolling Avg: ${trend.rollingAverage}ms`);
  console.log(`  Confidence: ${(trend.confidence * 100).toFixed(0)}%`);
  console.log(`  Recommendation: ${trend.recommendation}`);
}
```

### Feed Regression Detector (Step 98)

```javascript
// Step 98: Regression Detector compares current vs baseline
const baseline = await regressionDetector.loadBaseline(version);
const metrics = await aggregator.queryMetrics('bridge:search', {
  limit: 100,
  aggregation: 'average'
});

if (metrics.length > 0) {
  const comparison = analyzer.compareMetrics(
    metrics[0],           // Current (aggregated)
    baseline.handlers[0], // Baseline
    10                    // 10% tolerance
  );

  if (comparison.hasRegression) {
    console.log(`Regression detected: ${comparison.recommendation}`);
  }
}
```

### E2E Baseline Establishment (Step 110)

```javascript
// Step 110: E2E tests establish baseline from aggregated metrics
const latestSnapshot = await aggregator.getLatestSnapshot();
const baseline = {
  version: '2.0.0',
  timestamp: latestSnapshot.timestamp,
  handlers: latestSnapshot.handlers,
  trending: await Promise.all(
    latestSnapshot.handlers.map(h =>
      aggregator.getTrend(h.name, 5)
    )
  )
};

// Save baseline for Step 112 regression suite
await baselineManager.saveBaseline(baseline);
```

### Storage Management

```javascript
// Get storage stats
const stats = await aggregator.getStorageStats();
console.log(`Metrics storage: ${stats.totalSizeMB}MB (${stats.fileCount} files)`);

// Cleanup old data
await aggregator.cleanup(30);  // Keep only 30 days
console.log('Cleaned up old snapshots');
```

---

## Integration Points

### Step 96: ProfilerHandler (Upstream)

The aggregator subscribes to real-time snapshots from Step 96:
- Calls `profilerRpc.getProfilerData()` every 5 seconds
- Receives: handlers array with latency, error rates, request counts
- Buffering: In-memory buffer prevents excessive disk I/O

### Step 98: Regression Detector (Downstream)

Step 98 uses aggregator for historical comparison:
- Loads aggregated metrics: `aggregator.queryMetrics(handler, options)`
- Compares current vs baseline: `analyzer.compareMetrics(...)`
- Generates regression reports with confidence scores

### Step 110: E2E Scenario Tests (Downstream)

Step 110 establishes baseline using aggregated metrics:
- Queries trending data: `aggregator.getTrend(handler, window)`
- Creates baseline snapshot from current metrics
- Saves baseline for Step 112 regression comparison

### Step 112: Regression Suite (Downstream)

Step 112 performs regression testing using aggregated historical data:
- Loads historical baseline from Step 109
- Compares current run vs baseline
- Detects regressions with trending analysis
- Generates regression report for Part III gate

---

## Performance Tuning

### Collection Interval

```javascript
// Faster collection (1 second) - higher CPU/I/O
const agg = new HandlerMetricsAggregator({
  collectionInterval: 1000,  // Default: 5000ms
  logger
});

// Slower collection (10 seconds) - lower overhead
const agg = new HandlerMetricsAggregator({
  collectionInterval: 10000,
  logger
});
```

### Buffer Size

```javascript
// Larger buffer - less frequent disk I/O
const agg = new HandlerMetricsAggregator({
  bufferSize: 500,  // Default: 100
  logger
});

// Smaller buffer - faster persistence
const agg = new HandlerMetricsAggregator({
  bufferSize: 10,
  logger
});
```

### Storage Retention

```javascript
// Keep more history (30 days)
await aggregator.cleanup(30);  // Default: 7 days

// Aggressive cleanup (1 day only)
await aggregator.cleanup(1);
```

### Latency SLOs

- **Collection cycle**: <5s (profiler call + aggregation + buffer check)
- **Query latency**: <100ms for 24-hour range
- **Rolling average accuracy**: ±2% of manual calculation
- **Memory overhead**: <50MB total at runtime

---

## Error Handling

### Custom Exceptions

```javascript
import {
  AggregatorError,
  QueryError,
  PersistenceError
} from './handler-metrics-aggregator.mjs';

try {
  await aggregator.start();
} catch (err) {
  if (err instanceof AggregatorError) {
    console.error(`Aggregator error [${err.code}]: ${err.message}`);
  }
}

try {
  const metrics = await aggregator.queryMetrics('invalid', {});
} catch (err) {
  if (err instanceof QueryError) {
    console.error(`Query failed [${err.code}]: ${err.message}`);
  }
}

try {
  await aggregator.store.append(invalid);
} catch (err) {
  if (err instanceof PersistenceError) {
    console.error(`Persistence failed [${err.code}]: ${err.message}`);
  }
}
```

### Graceful Degradation

- **ProfilerHandler unavailable**: Logging warning, collection pauses gracefully
- **Disk full**: Logs error, stops collecting until disk space available
- **Corrupted snapshot file**: Skips file, continues reading other files
- **Missing storage directory**: Auto-creates on initialization

---

## Testing

### Node.js Test Suite

45+ tests across 9 suites:
```bash
npm test -- tests/handler-metrics-aggregator.test.mjs
```

**Test Coverage**:
- Suite 1: Initialization (4 tests)
- Suite 2: Collection lifecycle (5 tests)
- Suite 3: Snapshot persistence (6 tests)
- Suite 4: Query interface (8 tests)
- Suite 5: Trend analysis (6 tests)
- Suite 6: Storage management (4 tests)
- Suite 7: ProfilerHandler integration (5 tests)
- Suite 8: Error handling (3 tests)
- Suite 9: Performance & scale (4 tests)

### C# Test Suite

20+ tests for host-side metrics collection:
```bash
dotnet test VSIXProject1.Tests/Services/HandlerMetricsCollectorTests.cs
```

**Test Coverage**:
- Suite 1: Snapshot creation (4 tests)
- Suite 2: Persistence (4 tests)
- Suite 3: Storage path (3 tests)
- Suite 4: Error handling (4 tests)
- Suite 5: Integration (4 tests)
- Suite 6: Cleanup (2 tests)

---

## Troubleshooting

### Metrics Not Collecting

1. Verify ProfilerHandler is running: `aggregator.collector.profilerRpc`
2. Check collection is started: `aggregator.collector.isCollecting`
3. Verify storage directory exists: `ls ~/.continue/metrics/`
4. Check logger for errors

### High Memory Usage

1. Reduce collection interval: `collectionInterval: 10000`
2. Reduce buffer size: `bufferSize: 50`
3. Increase cleanup frequency: `await aggregator.cleanup(1)`

### Disk Space Issues

1. Check storage: `aggregator.getStorageStats()`
2. Cleanup old files: `await aggregator.cleanup(3)`
3. Reduce retention days in config

### Query Returns Empty

1. Verify data exists: `await aggregator.store.list()`
2. Check time range: `since` <= query time <= `until`
3. Verify handler name spelling
4. Ensure aggregator is collecting: `aggregator.collector.isCollecting`

---

## Related Documentation

- [Step 96: Profiler Integration](./profiler-integration-guide.md)
- [Step 98: Performance Test Framework](./performance-test-framework-guide.md)
- [Step 110: E2E Scenarios](./E2E-SCENARIOS-GUIDE.md)
- [Step 112: Regression Suite](./REGRESSION-SUITE-GUIDE.md)

---

**Status**: ✅ Complete  
**Last Verified**: 2024-01-15  
**Test Coverage**: 65+ tests (Node.js + C#)  
**Build Status**: All passing
