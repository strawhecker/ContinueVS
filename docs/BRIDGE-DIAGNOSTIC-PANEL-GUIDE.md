# Bridge Diagnostic Panel Guide (Step 102)

**Status**: ✅ COMPLETE  
**Version**: 1.0.0  
**Last Updated**: 2024-01-15

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Message Contract](#message-contract)
4. [Diagnostic Data Schema](#diagnostic-data-schema)
5. [Error Queue Format](#error-queue-format)
6. [Severity Levels](#severity-levels)
7. [Graceful Degradation](#graceful-degradation)
8. [Integration Points](#integration-points)
9. [Usage Examples](#usage-examples)
10. [Performance Characteristics](#performance-characteristics)
11. [API Reference](#api-reference)

---

## Overview

The **Bridge Diagnostic Panel** provides on-demand health snapshots and diagnostics aggregation for the ContinueVS bridge. Complements [Step 101 (Metrics Dashboard)](./METRICS-DASHBOARD-GUIDE.md) by offering snapshot-based diagnostics (vs. continuous streaming) for troubleshooting and monitoring.

**Purpose**: Enable developers and support teams to quickly assess bridge health, identify performance bottlenecks, and troubleshoot errors without instrumenting real-time dashboards.

**Key Characteristics**:
- ✅ **On-demand**: Called explicitly when diagnostics needed (not streaming)
- ✅ **Aggregated**: Combines health, metrics, errors, and statistics into single response
- ✅ **Graceful degradation**: Returns partial data if metric sources unavailable
- ✅ **Structured**: Well-defined JSON schema with severity levels
- ✅ **Performant**: Response generation <50ms for typical scenarios
- ✅ **Thread-safe**: Circular error queue protected by locks

---

## Architecture

### System Flow

```
┌─────────────────────────────────────────────────────────────┐
│ WebView / Debugger / Support Tool                            │
└────────────┬────────────────────────────────────────────────┘
             │ bridge:getDiagnosticPanel { operation, filter }
             │
             ▼
┌─────────────────────────────────────────────────────────────┐
│ core-server.js (Step 13)                                     │
│  ↓ Handler Dispatcher (Step 14)                             │
│  ↓ Message Routing Middleware (Step 47)                     │
└────────────┬────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────┐
│ diagnostic-panel-handler.mjs (Step 102)                      │
│  ├─ createDiagnosticPanelHandler() factory                  │
│  ├─ Validate request: operation, filter                     │
│  └─ aggregateDiagnostics():                                 │
│      ├─ Collect health from Step 24 (HealthCheckService)   │
│      ├─ Collect metrics from Step 96 (ProfilerHandler)     │
│      ├─ Collect errors from Step 25 (BridgeLogger)         │
│      └─ Calculate summary statistics                        │
└────────────┬────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────┐
│ JSON-RPC Response                                            │
│ { health, handlers, errors, summary, timestamp }           │
└─────────────────────────────────────────────────────────────┘
```

### Snapshot Model vs. Streaming (Step 101)

| Aspect | Step 101 (Metrics Dashboard) | Step 102 (Diagnostic Panel) |
|--------|------------------------------|----------------------------|
| **Model** | Subscription-based streaming | On-demand snapshot |
| **Purpose** | Real-time performance visualization | Troubleshooting & health assessment |
| **Trigger** | Continuous (every N seconds) | Explicit client request |
| **Data** | Per-handler latency, throughput, cache hit | Health, errors, handler stats, summary |
| **Latency** | Push (low latency) | Pull (<50ms response) |
| **Memory** | Subscription buffers | No buffering |
| **Use Case** | Dashboard display | Support troubleshooting, debugging |

---

## Message Contract

### Request Message

**Handler Type**: `bridge:getDiagnosticPanel`  
**HTTP Method** (metaphor): GET (read-only, idempotent)

```json
{
  "messageType": "bridge:getDiagnosticPanel",
  "messageId": "msg-1705334800000",
  "data": {
    "operation": "get-all",
    "filter": null
  }
}
```

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `messageType` | string | ✅ | Must be `"bridge:getDiagnosticPanel"` |
| `messageId` | string | ✅ | Unique request identifier (≤256 chars) |
| `data.operation` | string | ✅ | Operation: `"get-all"`, `"filter-tier"`, `"filter-handler-name"` |
| `data.filter` | string | ❌ | Filter value (required if operation is `filter-*`) |

**Valid Operations**:

```javascript
// Get all diagnostics (no filtering)
{ operation: "get-all", filter: null }

// Get only "core" tier handlers
{ operation: "filter-tier", filter: "core" }

// Get handlers matching name pattern
{ operation: "filter-handler-name", filter: "search" }
```

### Response Message

**Success**:

```json
{
  "success": true,
  "messageId": "msg-1705334800000",
  "data": {
    "health": { /* ... */ },
    "handlers": [ /* ... */ ],
    "errors": [ /* ... */ ],
    "summary": { /* ... */ },
    "timestamp": "2024-01-15T10:30:45.123Z"
  }
}
```

**Failure**:

```json
{
  "success": false,
  "messageId": "msg-1705334800000",
  "error": {
    "code": -32603,
    "message": "Invalid operation: unknown-op. Must be one of: get-all, filter-tier, filter-handler-name",
    "details": {
      "messageId": "msg-1705334800000",
      "operation": "unknown-op"
    }
  }
}
```

**Error Codes**:

| Code | Name | Cause |
|------|------|-------|
| `-32603` | Internal Error | Aggregation failure, invalid operation, missing dependencies |
| `-32602` | Invalid Params | Malformed request envelope (missing data, invalid filter) |

---

## Diagnostic Data Schema

### Health Object

```json
{
  "status": "healthy",
  "reason": "All handlers responding normally",
  "timestamp": "2024-01-15T10:30:45.123Z",
  "uptime": "2h 30m 15s",
  "lastCheckTime": "2024-01-15T10:30:45.123Z"
}
```

| Field | Type | Values | Description |
|-------|------|--------|-------------|
| `status` | string | `healthy`, `degraded`, `error`, `unknown` | Bridge health state |
| `reason` | string | — | Human-readable reason for status |
| `timestamp` | string | ISO 8601 | Snapshot timestamp |
| `uptime` | string\|null | e.g., `"2h 30m 15s"` | Bridge process uptime |
| `lastCheckTime` | string | ISO 8601 | Last health check timestamp |

### Handler Object

```json
{
  "name": "bridge:search",
  "tier": "core",
  "status": "INFO",
  "latency": {
    "p50": 15,
    "p95": 35,
    "p99": 75
  },
  "errorRate": 0.003,
  "throughput": 85.2,
  "requestCount": 852,
  "timeoutCount": 2,
  "cacheHitRate": 0.78
}
```

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Handler message type (e.g., `bridge:search`) |
| `tier` | string | Stability tier: `core`, `utility`, `experimental` |
| `status` | string | Severity: `INFO` (green), `WARNING` (yellow), `CRITICAL` (red) |
| `latency.p50` | number | 50th percentile latency (ms) |
| `latency.p95` | number | 95th percentile latency (ms) |
| `latency.p99` | number | 99th percentile latency (ms) |
| `errorRate` | number | Error rate (0.0–1.0) |
| `throughput` | number | Requests per second |
| `requestCount` | number | Total requests processed |
| `timeoutCount` | number | Number of timed-out requests |
| `cacheHitRate` | number\|null | Cache hit rate (0.0–1.0) or null if not cached |

### Error Object

```json
{
  "timestamp": "2024-01-15T10:30:35.000Z",
  "severity": "WARNING",
  "message": "Handler latency approaching threshold (p99 > 200ms)",
  "context": {
    "requestId": "req-42",
    "duration": "245.50"
  },
  "handlerName": "bridge:codeCompletion",
  "code": "SLOW_RESPONSE"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `timestamp` | string | ISO 8601 error timestamp |
| `severity` | string | `CRITICAL`, `WARNING`, or `INFO` |
| `message` | string | Error description |
| `context` | object\|null | Additional error context |
| `handlerName` | string\|null | Handler that generated error (if applicable) |
| `code` | string\|null | Error code (e.g., `TIMEOUT`, `SLOW_RESPONSE`) |

### Summary Object

```json
{
  "overallHealth": "degraded",
  "totalHandlers": 5,
  "totalRequests": 3732,
  "totalTimeouts": 10,
  "errorCount": 15,
  "avgErrorRate": "0.0053",
  "avgThroughput": "89.25",
  "maxLatencyP99Ms": 350,
  "criticalHandlers": 0,
  "warningHandlers": 1,
  "uptime": "2h 30m 15s"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `overallHealth` | string | Aggregate health: `healthy`, `degraded`, `error` |
| `totalHandlers` | number | Number of handlers queried |
| `totalRequests` | number | Sum of all handler request counts |
| `totalTimeouts` | number | Sum of all handler timeouts |
| `errorCount` | number | Total errors in queue |
| `avgErrorRate` | string | Mean error rate across handlers |
| `avgThroughput` | string | Mean throughput across handlers (req/sec) |
| `maxLatencyP99Ms` | number | Highest p99 latency among handlers (ms) |
| `criticalHandlers` | number | Handlers with p99 > 500ms |
| `warningHandlers` | number | Handlers with p99 > 200ms && ≤ 500ms |
| `uptime` | string\|null | Bridge process uptime |

---

## Error Queue Format

The error queue stores up to **100 entries** in FIFO (First-In-First-Out) order. Oldest entries are evicted when capacity is reached.

### Queue Properties

```javascript
{
  maxSize: 100,
  order: 'FIFO',
  evictionPolicy: 'oldest-first',
  threadSafe: true,
  returnOrder: 'newest-first' // API returns errors ordered by timestamp DESC
}
```

### Example Error Queue Response

```json
{
  "errors": [
    {
      "timestamp": "2024-01-15T10:30:40.000Z",
      "severity": "CRITICAL",
      "message": "Handler timeout after 5000ms",
      "handlerName": "bridge:codeCompletion",
      "code": "TIMEOUT"
    },
    {
      "timestamp": "2024-01-15T10:30:35.000Z",
      "severity": "WARNING",
      "message": "Handler latency approaching threshold",
      "handlerName": "bridge:search",
      "code": "SLOW_RESPONSE"
    }
  ]
}
```

---

## Severity Levels

Severity is determined by **p99 latency** of the handler:

| Severity | Condition | P99 Latency | Color (UI) |
|----------|-----------|------------|-----------|
| **CRITICAL** | p99 > 500ms | > 500ms | 🔴 Red |
| **WARNING** | 200ms < p99 ≤ 500ms | 200–500ms | 🟡 Yellow |
| **INFO** | p99 ≤ 200ms | ≤ 200ms | 🟢 Green |

### Mapping Algorithm

```javascript
function determineSeverityFromLatency(p99LatencyMs) {
  if (p99LatencyMs > 500) return 'CRITICAL';
  if (p99LatencyMs > 200) return 'WARNING';
  return 'INFO';
}
```

---

## Graceful Degradation

The diagnostic panel gracefully handles missing or unavailable metric sources. If a dependency is unavailable, the handler omits that field or returns a default/empty value.

### Degradation Scenarios

| Scenario | ProfilerHandler | HealthCheckService | BridgeLogger | Response |
|----------|-----------------|-------------------|--------------|----------|
| **All available** | ✅ | ✅ | ✅ | Full diagnostic snapshot |
| **No profiler** | ❌ | ✅ | ✅ | Health + errors, empty handlers |
| **No health check** | ✅ | ❌ | ✅ | Metrics + errors, status='unknown' |
| **No logger** | ✅ | ✅ | ❌ | Health + metrics, errors=[] |
| **All missing** | ❌ | ❌ | ❌ | Minimal snapshot: health='unknown', handlers=[], errors=[], summary=empty |

**Success Guarantee**: Diagnostic panel handler **always returns HTTP 200** with structured data. Failures only occur if the request is malformed (invalid operation, missing data) or a critical exception occurs during aggregation.

---

## Integration Points

### Consumed By

1. **Step 103 (Crash Recovery)** — Uses diagnostic health to decide auto-recovery actions
2. **Step 110 (E2E Scenario Tests)** — Validates diagnostic panel accuracy
3. **Step 113 (Manual Testing Guide)** — References diagnostic panel for troubleshooting workflows
4. **WebView UI** — Displays health snapshot on-demand
5. **Support/Debugging Tools** — Query bridge health during troubleshooting

### Consumes

1. **Step 24 (HealthCheckService)** — Bridge health status
2. **Step 25 (BridgeLogger)** — Recent error events
3. **Step 96 (ProfilerHandler)** — Per-handler metrics (latency, errors, throughput)
4. **Step 101 (Metrics Stream)** — Optional source for continuous metrics (deferred)

---

## Usage Examples

### Example 1: Get Full Diagnostic Snapshot

**Request**:
```json
{
  "messageType": "bridge:getDiagnosticPanel",
  "messageId": "msg-1705334800000",
  "data": { "operation": "get-all" }
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "health": {
      "status": "healthy",
      "reason": "All handlers responding normally",
      "uptime": "2h 30m 15s"
    },
    "handlers": [
      {
        "name": "bridge:search",
        "tier": "core",
        "status": "INFO",
        "latency": { "p50": 15, "p95": 35, "p99": 75 },
        "errorRate": 0.003,
        "throughput": 85.2,
        "requestCount": 852,
        "timeoutCount": 2
      }
    ],
    "errors": [
      {
        "timestamp": "2024-01-15T10:30:40.000Z",
        "severity": "WARNING",
        "message": "Handler latency approaching threshold",
        "handlerName": "bridge:search"
      }
    ],
    "summary": {
      "overallHealth": "healthy",
      "totalHandlers": 5,
      "totalRequests": 3732,
      "criticalHandlers": 0,
      "warningHandlers": 1
    }
  }
}
```

### Example 2: Filter to Core Handlers Only

**Request**:
```json
{
  "messageType": "bridge:getDiagnosticPanel",
  "messageId": "msg-1705334801000",
  "data": {
    "operation": "filter-tier",
    "filter": "core"
  }
}
```

**Response**: (Same structure, but handlers array filtered to tier='core' only)

### Example 3: Troubleshooting Slow Handler

**Workflow**:
1. **Call diagnostic panel**: Get full snapshot
2. **Identify issue**: Find handler with p99 > 500ms (CRITICAL)
3. **Check error queue**: Review recent errors for that handler
4. **Next steps**: 
   - If timeouts: Check bridge process resource usage
   - If errors: Review error context for clues
   - If latency: Consider disabling caching or reducing query complexity

---

## Performance Characteristics

### Response Time

| Scenario | Error Count | Handler Count | Response Time |
|----------|-------------|---------------|---------------|
| Healthy bridge | 0 | 5 | <5ms |
| Typical load | 20 | 10 | <15ms |
| Heavy load | 100 | 20 | <50ms |
| High degradation | 100+ | 30+ | <50ms |

**Performance Guarantee**: Response generation **always <50ms** regardless of error queue size or handler count.

### Memory Impact

- **Per request**: <1MB working memory
- **Error queue**: ~5-10KB per entry × 100 max = ~500KB–1MB
- **No streaming buffers**: Unlike metrics stream, no subscription buffers

### Concurrent Safety

- ✅ Thread-safe error queue (protected by lock)
- ✅ Multiple concurrent requests supported
- ✅ No blocking I/O (all data already collected by existing services)

---

## API Reference

### Node.js Handler Factory

```javascript
import { createDiagnosticPanelHandler } from 'diagnostic-panel-handler.mjs';

const handler = createDiagnosticPanelHandler({
  profilerHandler: null,        // Optional: ProfilerHandler (Step 96)
  healthCheckService: null,     // Optional: HealthCheckService (Step 24)
  bridgeLogger: null,           // Optional: BridgeLogger (Step 25)
  telemetryCollector: null,     // Optional: IBridgeTelemetryCollector
  logger: null                  // Optional: Logger facade
});

// Invoke handler
const response = await handler(message, context);
```

### C# Service

```csharp
var panel = new DiagnosticsPanel(
  transport,           // IBridgeTransport
  healthCheckService,  // HealthCheckService
  bridgeLogger,        // IBridgeLogger (optional)
  telemetryCollector   // IBridgeTelemetryCollector (optional)
);

// Query methods
var health = await panel.GetBridgeHealthAsync();
var handlers = await panel.GetHandlerStatsAsync();
var errors = await panel.GetRecentErrorsAsync();
var summary = await panel.GetDiagnosticSummaryAsync();

// Manual error queue management
panel.AddErrorEntry(new DiagnosticErrorEntry { /* ... */ });
panel.ClearErrorQueue();
```

---

## Related Steps

- **Step 24**: Health Check Service (consumed)
- **Step 25**: Bridge Logger (consumed)
- **Step 96**: Profiler Handler (consumed)
- **Step 101**: Metrics Dashboard (complementary)
- **Step 103**: Crash Recovery (consumer)

---

**End of Document**
