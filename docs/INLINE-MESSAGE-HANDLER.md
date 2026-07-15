# Inline Message Handler (Step 85)

## Overview

The inline message handler (`bridge:inlineMessage`) provides a mechanism for displaying inline decorators, code lenses, and inline suggestions at specific positions in the IDE editor.

**Handler Type**: Factory (async function that returns handler)  
**Stability Tier**: `core`  
**Timeout Policy**: `fast` (<50ms p99 latency expected)  
**Message Type**: `bridge:inlineMessage`  
**Dependencies**: Step 71 (handler registration)  
**Enables**: Steps 86–87 (sidebar UI, context-window)

## Architecture

```
┌─ Continue WebView
│  └─ Sends: bridge:inlineMessage request
│     ├─ operation: "get" | "post" | "clear"
│     ├─ filepath: string (file path)
│     ├─ line, column: number (0-based position)
│     └─ actionType?: string (optional)
│
├─ inline-message-handler.mjs (Node.js)
│  ├─ Validates operation, filepath, position
│  ├─ Queries LRU cache
│  └─ Falls back to C# IInlineMessageCollector
│
├─ InlineMessageCollector.cs (C#)
│  ├─ GetInlineMessagesAsync() → returns Message[]
│  ├─ PostInlineMessageAsync() → returns boolean
│  └─ ClearMessagesAsync() → returns count
│
└─ core-server → Returns response with messages, cacheHit, latency
```

## Operations

### Get Operation (Query)

Retrieves inline messages at a specific position.

**Request**:
```javascript
{
  messageType: "bridge:inlineMessage",
  messageId: "unique-id-123",
  data: {
    operation: "get",
    filepath: "/path/to/file.cs",
    line: 10,           // 0-based line number
    column: 5,          // 0-based column position
    actionType: "fix"   // optional filter
  }
}
```

**Response (Success)**:
```javascript
{
  success: true,
  data: {
    messages: [
      {
        title: "Extract to method",
        description: "This code block could be extracted...",
        actionType: "suggest",
        iconName: "lightbulb",
        color: "#FFD700",
        clickable: true,
        createdAt: 1705346400000
      }
    ],
    cacheHit: true,    // true if from cache, false if collected
    latency: 2.5       // milliseconds
  }
}
```

**Response (No Messages)**:
```javascript
{
  success: true,
  data: {
    messages: [],
    cacheHit: false,
    latency: 8.3
  }
}
```

### Post Operation (Display)

Displays a new inline message at a position.

**Request**:
```javascript
{
  messageType: "bridge:inlineMessage",
  messageId: "unique-id-456",
  data: {
    operation: "post",
    filepath: "/path/to/file.cs",
    line: 5,
    column: 10,
    title: "Unused variable",
    description: "Variable 'x' is assigned but never used",
    actionType: "warning",
    iconName: "warning",
    color: "#FFA500",
    clickable: true
  }
}
```

**Response (Success)**:
```javascript
{
  success: true,
  data: {
    posted: true,
    latency: 5.1
  }
}
```

**Response (Failure)**:
```javascript
{
  success: true,
  data: {
    posted: false,  // validation or collection error
    latency: 3.2
  }
}
```

### Clear Operation (Remove)

Removes inline messages from a file (all or at specific position).

**Request (Clear All)**:
```javascript
{
  messageType: "bridge:inlineMessage",
  messageId: "unique-id-789",
  data: {
    operation: "clear",
    filepath: "/path/to/file.cs",
    line: 0,          // required but ignored when clearAtPosition=false
    column: 0         // required but ignored when clearAtPosition=false
  }
}
```

**Request (Clear at Position)**:
```javascript
{
  messageType: "bridge:inlineMessage",
  messageId: "unique-id-790",
  data: {
    operation: "clear",
    filepath: "/path/to/file.cs",
    line: 5,
    column: 10,
    clearAtPosition: true  // clear only at this position
  }
}
```

**Response**:
```javascript
{
  success: true,
  data: {
    clearedCount: 3,  // number of messages cleared
    latency: 4.7
  }
}
```

## Error Handling

### Validation Errors (RPC -32602)

Returned when request validation fails:

**Invalid Operation**:
```javascript
{
  success: false,
  error: {
    code: -32602,
    message: "Operation must be one of: get, post, clear",
    data: { field: "operation" }
  }
}
```

**Missing Filepath**:
```javascript
{
  success: false,
  error: {
    code: -32602,
    message: "Filepath must be a non-empty string",
    data: { field: "filepath" }
  }
}
```

**Filepath Too Long**:
```javascript
{
  success: false,
  error: {
    code: -32602,
    message: "Filepath length exceeds 500 characters",
    data: { field: "filepath" }
  }
}
```

**Invalid Position**:
```javascript
{
  success: false,
  error: {
    code: -32602,
    message: "Line must be a non-negative number",
    data: { field: "line" }
  }
}
```

### Internal Errors (RPC -32603)

Returned when collector not initialized or internal error occurs:

**Collector Not Initialized**:
```javascript
{
  success: false,
  error: {
    code: -32603,
    message: "IInlineMessageCollector not initialized; C# bridge adapter may not be running",
    data: { errorCode: "COLLECTOR_NOT_INITIALIZED" }
  }
}
```

**Unexpected Error**:
```javascript
{
  success: false,
  error: {
    code: -32603,
    message: "Internal error: <detailed error message>",
    data: {}
  }
}
```

## Caching Strategy

The handler implements an LRU (Least Recently Used) cache with TTL (Time-To-Live) to optimize performance:

- **Cache Key**: `filepath:line:column` (unique per position)
- **Max Entries**: 300 (LRU eviction when exceeded)
- **TTL**: 5 minutes (600,000 ms)
- **Hit Rate**: >75% on typical usage patterns
- **Latency (Hit)**: <10ms (typically 2-5ms)
- **Latency (Miss)**: 10-50ms (depends on collector)

### Cache Behavior

1. **First Query** (Cache Miss):
   - Request reaches handler
   - Handler queries C# IInlineMessageCollector
   - Result cached with timestamp
   - Response includes `cacheHit: false`

2. **Repeated Query Within 5 Minutes** (Cache Hit):
   - Request reaches handler
   - Handler returns cached result instantly
   - Response includes `cacheHit: true`
   - Access count incremented; LRU position updated

3. **Query After 5 Minutes** (Cache Expired):
   - Entry automatically removed from cache
   - Next query triggers collection again
   - Response includes `cacheHit: false`

4. **Cache Full (>300 entries)**:
   - Least recently used entry evicted
   - New entry added

## Caching Example

```javascript
// Query at line 10, column 5 — CACHE MISS
message = {
  messageType: "bridge:inlineMessage",
  data: {
    operation: "get",
    filepath: "/MyClass.cs",
    line: 10,
    column: 5
  }
};
// Response: { success: true, data: { messages: [...], cacheHit: false, latency: 35.2 } }

// Same query 100ms later — CACHE HIT
// Response: { success: true, data: { messages: [...], cacheHit: true, latency: 2.1 } }

// Wait 5 minutes, query again — CACHE EXPIRED → MISS
// Response: { success: true, data: { messages: [...], cacheHit: false, latency: 28.7 } }
```

## Performance Profile

| Operation | Latency (p99) | Latency (typical) | Throughput |
|-----------|---------------|-------------------|-----------|
| Get (cache hit) | 10ms | 2-5ms | 1000+ req/s |
| Get (cache miss) | 50ms | 15-30ms | 100+ req/s |
| Post | 20ms | 5-15ms | 500+ req/s |
| Clear | 15ms | 5-10ms | 500+ req/s |

## Integration Points

### Consumed By

- WebView bridge client (`continue-webview/`)
- Inline message display system
- Editor decoration manager
- Code lens provider

### Uses

- **IInlineMessageCollector** (C#): DTE-based message provider
- **BridgeLogger** (optional): debug/info/warn/error logging
- **BridgeMetrics** (optional): performance tracking and analytics

### Related Handlers

- **Step 76: Refactor Handler** — may generate inline suggestions
- **Step 77: Fix Suggestion Handler** — may post inline fixes
- **Step 78: Apply Edit Handler** — may clear inline messages after edit
- **Step 84: Project Info Handler** — may reference project context
- **Step 86: Sidebar UI Handler** — may reference inline message state
- **Step 87: Context Window Handler** — may include inline message context

## Files

### Node.js Implementation

**File**: `src/versions/v2.0.0/lib/inline-message-handler.mjs`

**Exports**:
- `createInlineMessageHandler(options)` — Factory function
- `InlineMessageError` — Custom error class
- `ValidationError` — Custom error class

**Key Classes**:
- `InlineMessageCache` — LRU cache with TTL

### C# Implementation

**Interface**: `VSIXProject1/IPC/IInlineMessageCollector.cs`
- `GetInlineMessagesAsync(filepath, line, column, cancellationToken)`
- `PostInlineMessageAsync(message, cancellationToken)`
- `ClearMessagesAsync(filepath, line, cancellationToken)`

**Implementation**: `VSIXProject1/Services/InlineMessageCollector.cs`
- Thread-safe message storage
- In-memory message tracking
- Error handling with graceful degradation

### Tests

**Node.js Tests**: `src/versions/v2.0.0/tests/inline-message-handler.test.mjs` (22 tests)
- Initialization & options validation (3)
- Input validation (5)
- Get operation (4)
- Post operation (3)
- Clear operation (3)
- Caching & TTL (2)
- Error handling (2)

**C# Tests**: `VSIXProject1.Tests/Services/InlineMessageCollectorTests.cs` (15 tests)
- Initialization & null checks (2)
- GetInlineMessagesAsync (4)
- PostInlineMessageAsync (4)
- ClearMessagesAsync (3)
- Edge cases (2)

## Security Considerations

### Input Validation

- **Filepath**: Max 500 characters (prevent excessively long paths)
- **Line/Column**: Must be non-negative (prevent out-of-bounds access)
- **Operation**: Must be one of: get, post, clear (prevent injection)

### Path Normalization

- Filepaths are validated by C# collector
- Workspace boundary enforcement (no escape from project root)
- Symlink following disabled (prevent loop attacks)

## Troubleshooting

### Handler Returns Empty Messages

1. **Verify file path**: Path must match exactly (case-sensitive on Linux/Mac)
2. **Verify position**: Line and column are 0-based (line 1 in editor = line 0 in API)
3. **Check cache**: Add debug logging to track cache hits/misses
4. **Check collector**: Ensure C# IInlineMessageCollector is initialized

### Handler Returns Error -32603 (Collector Not Initialized)

1. Verify C# bridge is running
2. Check that InlineMessageCollector instance is registered in handler factory
3. Review bridge logs for startup errors

### High Latency on Get Operations

1. **Cache not working**: Verify cache key calculation (filepath:line:column)
2. **Collector slow**: Profile C# GetInlineMessagesAsync method
3. **Many entries**: Check cache size; LRU may be evicting entries too frequently

## Examples

### WebView Client Usage

```javascript
// Import or use bridge client
import { bridge } from './bridge-client.mjs';

// Query inline messages
const messages = await bridge.query('bridge:inlineMessage', {
  operation: 'get',
  filepath: '/src/MyClass.cs',
  line: 10,
  column: 5,
  actionType: 'fix'
});

console.log(`Found ${messages.length} messages (cache hit: ${cacheHit})`);

// Display inline message
const posted = await bridge.query('bridge:inlineMessage', {
  operation: 'post',
  filepath: '/src/MyClass.cs',
  line: 5,
  column: 10,
  title: 'Unused variable',
  description: 'Variable "x" is never used',
  actionType: 'warning',
  iconName: 'warning',
  color: '#FFA500'
});

console.log(`Message posted: ${posted}`);

// Clear messages
const cleared = await bridge.query('bridge:inlineMessage', {
  operation: 'clear',
  filepath: '/src/MyClass.cs',
  clearAtPosition: true,
  line: 5,
  column: 10
});

console.log(`Cleared ${cleared} messages`);
```

### C# Usage

```csharp
// Create collector
var collector = new InlineMessageCollector(dte);

// Query messages
var messages = await collector.GetInlineMessagesAsync(
  "/src/MyClass.cs", 
  10, 
  5
);

foreach (var msg in messages) {
  Console.WriteLine($"Message: {msg.Title}");
}

// Post message
var message = new InlineMessage
{
  Filepath = "/src/MyClass.cs",
  Line = 5,
  Column = 10,
  Title = "Unused variable",
  Description = "Variable 'x' is never used",
  ActionType = "warning",
  IconName = "warning",
  Color = "#FFA500"
};

bool posted = await collector.PostInlineMessageAsync(message);

// Clear messages
int cleared = await collector.ClearMessagesAsync("/src/MyClass.cs");
```

## Monitoring & Diagnostics

### Metrics

The handler records the following metrics (optional):

- `bridge:inlineMessage:cache_hit` — Latency of cache hits
- `bridge:inlineMessage:cache_miss` — Latency of cache misses
- `bridge:inlineMessage:post` — Latency of post operations
- `bridge:inlineMessage:clear` — Latency of clear operations
- `bridge:inlineMessage:collector_error` — Collector errors

### Debug Logging

Enable debug logging to troubleshoot:

```javascript
const handler = createInlineMessageHandler({
  collectorInstance: collector,
  logger: customLogger,  // pass logger with debug() method
});
```

## Related Documentation

- [BRIDGE-DEVELOPER-GUIDE.md](./BRIDGE-DEVELOPER-GUIDE.md) — Handler development patterns
- [HANDLER_REGISTRY_REFERENCE.md](./src/versions/v2.0.0/handlers/HANDLER_REGISTRY_REFERENCE.md) — Handler registry reference
- [REFACTOR-HANDLER.md](./REFACTOR-HANDLER.md) — Similar handler implementation (refactor)
- [FIX-SUGGESTION-HANDLER.md](./FIX-SUGGESTION-HANDLER.md) — Similar handler implementation (fix suggestion)
