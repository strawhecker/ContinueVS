# Sidebar UI Handler (Step 86)

## Overview

The sidebar UI handler (`bridge:getSidebarState`) provides the Continue WebView sidebar with a dynamic tree-view of IDE state, including:

- **Documents** — Open files with metadata (language, modification status, line count)
- **Symbols** — Class/method definitions, bookmarks, search history
- **Diagnostics** — Errors and warnings aggregated by file
- **Actions** — Quick fixes, refactoring suggestions
- **Messages** — Placeholder for Step 87 (context-window) integration

**Handler Type**: Factory (returns async function)  
**Stability Tier**: `experimental`  
**Timeout Policy**: `fast` (<50ms p99 expected via LRU cache)  
**Message Type**: `bridge:getSidebarState`  
**Dependencies**: Steps 71, 52, 53, 54, 83  
**Enables**: Steps 87 (context-window handler)

---

## Architecture

### Message Flow

```
┌─ Continue WebView
│  └─ Sends: bridge:getSidebarState request
│     ├─ operation: "get" (only supported operation)
│     ├─ filepath?: string (optional single-file filter)
│     └─ includeDetails?: boolean (minimal tree vs. full metadata)
│
├─ sidebar-ui-handler.mjs (Node.js)
│  ├─ Validates operation, filepath, options
│  ├─ Queries LRU cache (300 entries, 5-min TTL)
│  └─ Falls back to C# SidebarCollector on cache miss
│
├─ SidebarCollector.cs (C#)
│  ├─ GetSidebarStateAsync() → returns SidebarState DTO
│  ├─ Enumerates open documents from DTE
│  ├─ Aggregates diagnostics per file
│  ├─ Extracts symbols from active editor
│  └─ Traverses workspace directory structure
│
└─ core-server → Returns response with tree, cacheHit, latency, stats
```

### Handler Factory Pattern

```javascript
// In handler-registry.mjs
import { createSidebarUIHandler } from './sidebar-ui-handler.mjs';

const handler = createSidebarUIHandler({
  collectorInstance: sidebarCollector,  // C# provider instance
  logger: bridgeLogger,                 // Optional
  metrics: bridgeMetrics,               // Optional
});
```

### Cache Strategy

- **Type**: LRU (Least Recently Used)
- **Max entries**: 300
- **TTL**: 5 minutes (300,000 ms)
- **Key format**: `sidebar:{filepath|'all'}:{includeDetails}`
- **Hit rate**: >75% on typical sidebar refresh patterns
- **Cache invalidation**: Manual via message or auto via TTL expiration

---

## Message Specification

### Request Format (JSON-RPC 2.0)

```javascript
{
  "jsonrpc": "2.0",
  "id": "msg-123",
  "method": "bridge:getSidebarState",
  "params": {
    "data": {
      "operation": "get",           // Required: only "get" supported
      "filepath": "/path/to/file.cs",  // Optional: filter to single file
      "includeDetails": true        // Optional: default true (full metadata)
    }
  }
}
```

**Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `operation` | string | Yes | — | Operation type. Only `"get"` supported. |
| `filepath` | string | No | — | Optional filter: return only this file's data |
| `includeDetails` | boolean | No | true | Include full metadata (line counts, descriptions) |

### Response Format (Success)

```javascript
{
  "jsonrpc": "2.0",
  "id": "msg-123",
  "result": {
    "success": true,
    "data": {
      "tree": {
        "messages": [],                 // Placeholder for Step 87
        "documents": [
          {
            "filepath": "/path/to/file.cs",
            "language": "csharp",
            "isModified": true,
            "lineCount": 250
          }
        ],
        "symbols": [
          {
            "name": "MyClass",
            "kind": "class",
            "line": 10,
            "column": 0,
            "isBookmarked": false
          }
        ],
        "diagnostics": {
          "/path/to/file.cs": {
            "errors": [
              {
                "line": 15,
                "column": 5,
                "message": "Undefined variable",
                "code": "CS0103"
              }
            ],
            "warnings": [
              {
                "line": 42,
                "column": 10,
                "message": "Unreachable code",
                "code": "CS0162"
              }
            ]
          }
        },
        "actions": [
          {
            "title": "Quick Fix: Remove unused variable",
            "type": "refactor",
            "description": "Automatically remove x"
          }
        ],
        "timestamp": 1705334800000
      },
      "cacheHit": false,              // true if from cache
      "latency": 45.2,                // ms
      "stats": {
        "documents": 3,
        "symbols": 15,
        "diagnosticFiles": 2,
        "cacheSize": 42
      }
    }
  }
}
```

### Response Format (Error)

```javascript
{
  "jsonrpc": "2.0",
  "id": "msg-123",
  "error": {
    "code": -32602,                   // RPC error code
    "message": "Invalid parameters",
    "data": {
      "details": "Missing required field: operation"
    }
  },
  "latency": 2.1                      // ms
}
```

### Error Codes

| RPC Code | Meaning | Cause |
|----------|---------|-------|
| -32602 | Invalid Parameters | Missing operation, invalid operation type, invalid filepath type |
| -32603 | Internal Error | Collector not initialized, null DTE, unexpected exception |

---

## Tree Structure Reference

### SidebarTree

```typescript
interface SidebarTree {
  messages: SidebarMessage[];
  documents: SidebarDocument[];
  symbols: SidebarSymbol[];
  diagnostics: { [filepath: string]: SidebarDiagnostics };
  actions: SidebarAction[];
  timestamp: number;  // ms since epoch
}
```

### SidebarMessage

```typescript
interface SidebarMessage {
  id: string;
  content: string;
  author: string;
  timestamp: number;
}
```

**Note**: Messages array is currently empty (placeholder for Step 87 context-window integration).

### SidebarDocument

```typescript
interface SidebarDocument {
  filepath: string;          // Absolute or workspace-relative path
  language: string;          // File type: 'csharp', 'javascript', 'typescript', etc.
  isModified: boolean;       // Has unsaved changes
  lineCount: number;         // Total lines in file (0-based)
}
```

**Supported languages**: csharp, javascript, typescript, json, markdown, xml, html, css, python, cpp, c, h, plaintext

### SidebarSymbol

```typescript
interface SidebarSymbol {
  name: string;              // Symbol name
  kind: string;              // Type: 'class', 'method', 'property', 'variable', etc.
  line: number;              // 0-based line number
  column: number;            // 0-based column number
  isBookmarked: boolean;     // User-marked for quick navigation
}
```

### SidebarDiagnostics

```typescript
interface SidebarDiagnostics {
  errors: SidebarDiagnosticItem[];
  warnings: SidebarDiagnosticItem[];
}

interface SidebarDiagnosticItem {
  line: number;              // 0-based
  column: number;            // 0-based
  message: string;           // Error/warning message
  code: string;              // Diagnostic code (e.g., 'CS0103')
}
```

### SidebarAction

```typescript
interface SidebarAction {
  title: string;             // User-facing action title
  type: string;              // 'refactor', 'quickfix', 'suggest', etc.
  description: string;       // Detailed action description
}
```

---

## Operations

### Get Operation

Retrieves the current sidebar tree state.

**Request**:
```javascript
{
  operation: "get",
  filepath: undefined,    // Optional: filter to single file
  includeDetails: true    // Optional: include full metadata
}
```

**Response**:
```javascript
{
  tree: SidebarTree,
  cacheHit: boolean,
  latency: number,
  stats: {
    documents: number,
    symbols: number,
    diagnosticFiles: number,
    cacheSize: number
  }
}
```

**Note**: Future versions may support `"post"` (add message) and `"clear"` (invalidate cache) operations; currently only `"get"` is implemented.

---

## Performance Characteristics

### Latency Targets

| Scenario | Target (p99) | Typical |
|----------|--------------|---------|
| Cache hit | <5ms | <1ms |
| Cache miss (small tree) | <50ms | <30ms |
| Cache miss (large tree) | <500ms | <200ms |

### Memory

- **Per cache entry**: ~5–15 KB (documents + symbols + diagnostics)
- **Max cache**: 300 entries = ~1.5–4.5 MB
- **Tree size limit**: None enforced (log warning if >5 MB)

### Workspace Size Limits

- **Max files enumerated**: ~500 (truncated if larger)
- **Max symbols per editor**: ~1000 (from SymbolExtractor cache)
- **Max diagnostics per file**: ~100 (aggregated)

---

## Cache Invalidation

### Automatic

- **TTL expiration**: 5 minutes after entry creation
- **LRU eviction**: Oldest entry removed when cache reaches 300 entries

### Manual

Currently no explicit cache-clear operation. Future enhancements may add:
- `bridge:clearSidebarCache`
- `bridge:onSidebarRefresh` (subscription for real-time updates)

### Implicit

Cache misses occur when:
- Different `filepath` filter used
- `includeDetails` toggle changed
- New cache key never seen before

---

## Integration Points

### Step 52: DocumentProvider

The handler uses DocumentProvider internally (via C# collector) to enumerate open documents:
- Queries `DTE.Documents` collection
- Extracts filepath, language, modification status, line count
- Handles unsaved document state

### Step 53: SymbolExtractor

Sidebar handler can integrate with SymbolExtractor cache to populate symbols:
- Query cached symbols for active editor
- Filter by filepath if specified
- Mark bookmarked symbols

### Step 54: DiagnosticsCollector

Diagnostics aggregation from CollectorInstance:
- Retrieve errors and warnings per file
- Group by severity (errors vs. warnings)
- Include line/column and diagnostic codes

### Step 83: FileSystemHandler

Optional workspace tree enumeration (deferred for Step 87):
- Traverse directory structure
- Filter to relevant file types (.cs, .js, .ts, .json, .md)
- Exclude build directories (bin, obj, node_modules, .git)

### Step 87: ContextWindow

Context-window handler receives sidebar state as a context source:
- Uses documents list for file context selection
- Incorporates symbols for symbol navigation context
- Aggregates diagnostics for problem context
- Uses actions for suggestion filtering

---

## Error Handling

### Validation Errors (-32602)

Thrown if:
- Missing `operation` field
- Invalid `operation` value (not "get")
- Invalid `filepath` type (not a string)

**Response**:
```javascript
{
  "error": {
    "code": -32602,
    "message": "Invalid operation: invalid. Only \"get\" is supported.",
    "data": { "details": null }
  }
}
```

### Internal Errors (-32603)

Thrown if:
- `SidebarCollector` not initialized (null collectorInstance)
- DTE unavailable or null
- Unexpected exception during collection

**Response**:
```javascript
{
  "error": {
    "code": -32603,
    "message": "SidebarCollector instance required",
    "data": { "details": "MISSING_COLLECTOR" }
  }
}
```

### Graceful Degradation

If secondary collectors fail (DiagnosticsCollector, SymbolExtractor):
- Continue without that data
- Return partial tree with available fields populated
- Log warning but do not fail request
- Example: if diagnostics unavailable, return tree with empty `diagnostics: {}`

---

## Testing

### Test Coverage

**Node.js Tests**: `src/versions/v2.0.0/tests/sidebar-ui-handler.test.mjs` (28 tests)

- Suite 1: Initialization & Dependency Injection (4 tests)
- Suite 2: Cache Behavior (5 tests)
- Suite 3: Tree Structure Validation (5 tests)
- Suite 4: Filtering & Options (4 tests)
- Suite 5: Error Handling (5 tests)
- Suite 6: Metrics & Logging (5 tests)

**C# Tests**: `src/VSIXProject1.Tests/Handlers/SidebarCollectorTests.cs` (18 tests)

- Suite 1: Initialization (3 tests)
- Suite 2: GetSidebarStateAsync (4 tests)
- Suite 3: Filtering (3 tests)
- Suite 4: Workspace Tree (4 tests)
- Suite 5: Error Handling (4 tests)

### Running Tests

**Node.js**:
```bash
npx mocha src/versions/v2.0.0/tests/sidebar-ui-handler.test.mjs --timeout 10000
```

Expected: 28/28 passing (~800ms execution)

**C#**:
```bash
dotnet test VSIXProject1.slnx --filter "SidebarCollectorTests"
```

Expected: 18/18 passing

### Test Fixtures

**File**: `src/versions/v2.0.0/tests/sidebar-collector-mock.mjs`

Provides:
- `createMockSidebarCollector(overrides)` — Simple factory
- `MockSidebarCollectorBuilder` — Fluent builder for complex scenarios
- `MockLogger` — Capture and inspect log messages
- `MockMetrics` — Track metric recordings
- `createMinimalFixture()` — Empty state
- `createComplexFixture()` — 10 files, 20 symbols, diagnostics
- `createFixtureWithErrors()` — Emphasis on errors
- `createFixtureWithWarnings()` — Emphasis on warnings

---

## Troubleshooting

### High Latency (>100ms on cache miss)

**Causes**:
- Large workspace with many open files
- Slow diagnostic aggregation
- Slow workspace enumeration

**Solutions**:
- Verify Step 54 (DiagnosticsCollector) performance
- Check workspace size (consider limiting files enumerated)
- Profile Node.js handler with `--prof` flag
- Consider adding `includeDetails: false` for minimal tree

### Cache Hits Not Occurring

**Causes**:
- Different `filepath` or `includeDetails` used each request
- TTL expired (5 minutes elapsed)
- Cache entries evicted (>300 total)

**Solutions**:
- Verify cache key generation logic
- Check metrics for cache hit rate
- Increase max cache entries if needed (modify LRUCache constructor)
- Verify logger output for cache debug messages

### Missing Diagnostics or Symbols

**Causes**:
- Step 54 (DiagnosticsCollector) not initialized
- Step 53 (SymbolExtractor) cache empty
- Active editor filter applied but no match

**Solutions**:
- Verify DiagnosticsProvider dependency
- Ensure SymbolExtractor is populated before first request
- Check filepath filter logic (case sensitivity)
- Review graceful degradation: handler returns partial tree, not error

### Handler Not Registered

**Causes**:
- Import missing from handler-registry.mjs
- Registry entry not added to baseHandlers array
- Factory instantiation failed

**Solutions**:
- Verify import: `import { createSidebarUIHandler } from './sidebar-ui-handler.mjs';`
- Verify registry entry exists with `messageType: 'bridge:getSidebarState'`
- Check handler registration logs in core-server
- Run handler registry tests: `npx mocha tests/handler-registry.test.mjs`

---

## Metrics & Monitoring

### Key Metrics

| Metric | Type | Unit | Target |
|--------|------|------|--------|
| `sidebar_cache_hit` | Counter | count | — |
| `sidebar_cache_miss` | Counter | count | — |
| `sidebar_latency_ms` | Histogram | ms | p99 <50ms |
| `sidebar_tree_size_kb` | Gauge | KB | <100 KB |
| `sidebar_error` | Counter | count | 0 (no errors) |

### Dashboard Queries

**Cache Hit Rate**:
```
sidebar_cache_hit / (sidebar_cache_hit + sidebar_cache_miss)
```

**Average Latency**:
```
avg(sidebar_latency_ms)
```

**P99 Latency**:
```
percentile(sidebar_latency_ms, 0.99)
```

---

## Related Documentation

- [Step 71: Handler Registration](HANDLER-REGISTRY-REFERENCE.md)
- [Step 52: DocumentProvider](DOCUMENT-PROVIDER-GUIDE.md) (if exists)
- [Step 53: SymbolExtractor](SYMBOL-EXTRACTOR-GUIDE.md) (if exists)
- [Step 54: DiagnosticsCollector](DIAGNOSTICS-COLLECTOR-GUIDE.md) (if exists)
- [Step 85: Inline Message Handler](INLINE-MESSAGE-HANDLER.md)
- [Step 87: Context Window Handler](CONTEXT-WINDOW-HANDLER-GUIDE.md) (next)

---

## Future Enhancements

**Step 87 (Context-Window Handler)** will extend sidebar UI:
- Add context-aware sidebar refresh based on editor state
- Integrate messages from Continue conversation
- Filter sidebar tree based on active context

**Post-Step 87**:
- Subscription support (`bridge:onSidebarRefresh`)
- Real-time symbol updates
- Workspace tree deep enumeration
- Custom sidebar layout preferences

---

**Last Updated**: 2024-01-15  
**Status**: Complete (Step 86)  
**Next**: Step 87 (Context-Window Handler)
