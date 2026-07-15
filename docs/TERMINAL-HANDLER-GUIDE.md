# Terminal Handler Implementation Guide (Step 82)

**Related Steps**: Step 71 (handler registration), Step 61 (debug-session pattern), Step 81 (git-integration pattern)  
**Status**: Complete  
**Last Updated**: 2024-01-15

---

## Overview

The **Terminal Handler** provides bidirectional terminal control between Continue's webview and Visual Studio's integrated terminal. It enables:

- **Command Execution**: Run commands with real-time output streaming
- **Output Streaming**: Incremental output delivery for long-running operations
- **Input Queueing**: Send input to running terminal without blocking
- **State Tracking**: Query terminal status (idle, busy, running, error)
- **Subscriptions**: Listen to terminal output events in real-time

---

## Architecture Diagram

```
┌─────────────────┐
│  Continue IDE   │
│   (WebView)     │
└────────┬────────┘
         │
         │ bridge:executeTerminalCommand (request)
         ▼
┌─────────────────────────────────────────────┐
│  Handler Dispatcher (Step 14)               │
│  ↓                                          │
│  Message Routing Middleware (Step 47)       │
│  ↓                                          │
│  Request/Response Validation (Step 73)      │
│  ↓                                          │
│  createTerminalHandler(context)             │
└────────┬────────────────────────────────────┘
         │
         │ TerminalHandler.handle() routes to operations
         ▼
┌─────────────────────────────────────────────┐
│  TerminalHandler (Node.js, terminal-handler.mjs) │
│                                             │
│  Operations:                                │
│  • execute(command, timeout)                │
│  • sendInput(text)                          │
│  • clear()                                  │
│  • getStatus()                              │
│  • subscribe(onTerminalOutput)              │
└────────┬────────────────────────────────────┘
         │
         │ Calls C# collector via context injection
         ▼
┌─────────────────────────────────────────────┐
│  TerminalCollector (C#, Services/TerminalCollector.cs) │
│                                             │
│  • ExecuteCommandAsync()                    │
│  • SendInputAsync()                         │
│  • ClearTerminalAsync()                     │
│  • GetStatusAsync()                         │
│  • Command queueing (sequential exec)       │
│  • Output streaming (async IAsyncEnumerable)│
└────────┬────────────────────────────────────┘
         │
         │ Uses DTE.ExecuteCommand / process execution
         ▼
┌─────────────────────────────────────────────┐
│  Visual Studio DTE Terminal                 │
│  or External Process (child_process)        │
└─────────────────────────────────────────────┘
         │
         │ Output chunks flow back
         ▼
┌─────────────────────────────────────────────┐
│  TerminalCollector emits TerminalOutput     │
└────────┬────────────────────────────────────┘
         │
         │ Node handler accumulates chunks
         ▼
┌─────────────────────────────────────────────┐
│  Handler returns { success, data: { chunks: [...] } } │
└────────┬────────────────────────────────────┘
         │
         │ Core Server sends response
         ▼
┌─────────────────┐
│  Continue IDE   │
│   (WebView)     │
│  Updates UI     │
│  with output    │
└─────────────────┘
```

---

## Message Types

### 1. bridge:executeTerminalCommand

**Request**: Execute a terminal command with output streaming

```json
{
  "messageType": "bridge:executeTerminalCommand",
  "messageId": "msg-uuid-v4",
  "data": {
    "operation": "execute",
    "command": "npm test",
    "cwd": "/path/to/project",
    "timeoutMs": 30000
  }
}
```

**Response**: Chunks of output with completion status

```json
{
  "success": true,
  "data": {
    "chunks": [
      {
        "text": "Running test suites...",
        "isPartial": true,
        "isError": false,
        "timestamp": 1705339200000
      },
      {
        "text": "\nTests passed!",
        "isPartial": false,
        "isError": false,
        "timestamp": 1705339205000
      }
    ],
    "isComplete": true,
    "commandText": "npm test"
  }
}
```

**Error Response**:

```json
{
  "success": false,
  "error": "Command execution timeout after 30000ms",
  "code": "COMMAND_ERROR",
  "rpcErrorCode": -32601
}
```

---

### 2. bridge:executeTerminalCommand (sendInput variant)

**Request**: Send input to running terminal

```json
{
  "messageType": "bridge:executeTerminalCommand",
  "messageId": "msg-uuid-v4",
  "data": {
    "operation": "sendInput",
    "text": "y\n"
  }
}
```

**Response**:

```json
{
  "success": true,
  "data": {
    "queued": true
  }
}
```

---

### 3. bridge:executeTerminalCommand (clear variant)

**Request**: Clear terminal state

```json
{
  "messageType": "bridge:executeTerminalCommand",
  "messageId": "msg-uuid-v4",
  "data": {
    "operation": "clear"
  }
}
```

**Response**:

```json
{
  "success": true,
  "data": {
    "cleared": true
  }
}
```

---

### 4. bridge:executeTerminalCommand (getStatus variant)

**Request**: Query terminal status

```json
{
  "messageType": "bridge:executeTerminalCommand",
  "messageId": "msg-uuid-v4",
  "data": {
    "operation": "getStatus"
  }
}
```

**Response**:

```json
{
  "success": true,
  "data": {
    "state": "idle",
    "isResponsive": true,
    "commandCount": 3,
    "lastOutput": "Build complete"
  }
}
```

---

### 5. bridge:onTerminalOutput

**Request**: Subscribe to terminal output events

```json
{
  "messageType": "bridge:executeTerminalCommand",
  "messageId": "msg-uuid-v4",
  "data": {
    "operation": "subscribe"
  }
}
```

**Response**: Subscription registered

```json
{
  "success": true,
  "data": {
    "subscriptionId": "sub_1"
  }
}
```

**Subsequent Events** (pushed to webview):

```json
{
  "messageType": "bridge:onTerminalOutput",
  "subscriptionId": "sub_1",
  "data": {
    "chunk": "New output",
    "isPartial": true,
    "isError": false,
    "timestamp": 1705339210000
  }
}
```

---

## API Reference

### TerminalHandler (Node.js)

#### Class: TerminalHandler

```javascript
constructor(collector, logger = null, metrics = null)
```

- **collector**: C# TerminalCollector instance (required)
- **logger**: BridgeLogger instance (optional, graceful degradation)
- **metrics**: TelemetryCollector instance (optional, graceful degradation)

#### Method: handle(message, context)

Routes messages to operations based on `message.data.operation`

```javascript
const response = await handler.handle(
  {
    messageType: 'bridge:executeTerminalCommand',
    data: { operation: 'execute', command: 'npm test', timeoutMs: 30000 }
  },
  { logger, metrics, server }
);
```

**Returns**: `{ success, data?, error?, code?, rpcErrorCode? }`

#### Method: unsubscribe(subscriptionId)

Remove a subscription

```javascript
const removed = handler.unsubscribe('sub_1');
```

#### Method: getSubscriptions()

List active subscriptions

```javascript
const subs = handler.getSubscriptions(); // ['sub_1', 'sub_2', ...]
```

---

### TerminalCollector (C#)

#### Interface: ITerminalCollector

```csharp
IAsyncEnumerable<TerminalOutput> ExecuteCommandAsync(
  string command, 
  int timeoutMs, 
  string workingDirectory = null
);

Task SendInputAsync(string text);
Task ClearTerminalAsync();
Task<TerminalStatus> GetStatusAsync();
```

#### Class: TerminalCollector

```csharp
public TerminalCollector(
  DTE dte, 
  IBridgeLogger logger = null, 
  ITelemetryCollector metrics = null
);
```

---

## Performance Characteristics

| Operation | Latency | Throughput | Memory |
|-----------|---------|-----------|--------|
| execute (start) | <100ms | N/A | <1MB |
| execute (per chunk) | <200ms | 1MB/s | <1MB |
| sendInput | <50ms | N/A | <10KB |
| clear | <50ms | N/A | <10KB |
| getStatus | <10ms | N/A | <1KB |
| subscribe | <5ms | N/A | <1KB |

**Streaming Performance**:
- Chunk size: 100-500 characters (default: 100)
- Output batching: Incremental (no all-at-once buffering)
- Memory per handler: <10MB
- Max concurrent commands: 1 (queued sequentially)

---

## Error Codes

| Error Code | RPC Code | Description | Recovery |
|-----------|----------|-------------|----------|
| TERMINAL_ERROR | -32600 | Generic terminal error | Retry or check DTE availability |
| COMMAND_ERROR | -32601 | Command execution failed (exit code, timeout, not found) | Check command syntax, verify environment |
| STREAM_ERROR | -32603 | Output streaming failed | Retry command or reduce output size |
| STATE_ERROR | -32602 | Invalid operation for current state | Wait for previous operation or clear terminal |
| MISSING_COLLECTOR | -32600 | TerminalCollector not injected | Check handler context initialization |

---

## Usage Examples

### Example 1: Execute NPM Test

```javascript
// Node.js handler
const message = {
  messageType: 'bridge:executeTerminalCommand',
  messageId: 'msg-001',
  data: {
    operation: 'execute',
    command: 'npm test --verbose',
    cwd: '/path/to/project',
    timeoutMs: 120000 // 2 minutes for tests
  }
};

const response = await handler.handle(message, { logger, metrics });

if (response.success) {
  for (const chunk of response.data.chunks) {
    console.log(chunk.text);
  }
} else {
  console.error(`Error: ${response.error} (${response.code})`);
}
```

### Example 2: Subscribe to Terminal Output

```javascript
// Node.js handler
const subMessage = {
  messageType: 'bridge:executeTerminalCommand',
  messageId: 'msg-002',
  data: { operation: 'subscribe' }
};

const subResponse = await handler.handle(subMessage, { logger });
const subId = subResponse.data.subscriptionId;

// Later, when terminal output occurs:
handler.emit({
  chunk: 'Output line',
  isPartial: false,
  isError: false,
  timestamp: Date.now()
});
```

### Example 3: C# Usage (TerminalCollector)

```csharp
// C# side
var dte = GetDTE(); // Get from VSIX context
var collector = new TerminalCollector(dte, logger, metrics);

// Execute command with streaming
await foreach (var output in collector.ExecuteCommandAsync(
    "dotnet build", 
    timeoutMs: 60000,
    workingDirectory: "/path/to/project"))
{
    Console.WriteLine($"[{output.Timestamp}] {output.Chunk}");
}

// Get status
var status = await collector.GetStatusAsync();
Console.WriteLine($"State: {status.State}, Commands: {status.CommandCount}");

// Send input
await collector.SendInputAsync("y\n");

// Clear
await collector.ClearTerminalAsync();
```

---

## Troubleshooting

### Issue: "TerminalCollector not injected"

**Cause**: Context missing collector instance during factory initialization

**Solution**: Ensure handler-dispatcher passes `collector` in context when calling `createTerminalHandler(context)`

```javascript
// In handler-dispatcher.js or setup
const context = {
  collector: new TerminalCollector(dte, logger, metrics),
  logger,
  metrics
};
const handler = createTerminalHandler(context);
```

### Issue: Commands timeout unexpectedly

**Cause**: Default timeout (30000ms) too short for long-running operations

**Solution**: Increase `timeoutMs` in request or adjust default in handler

```javascript
data: {
  operation: 'execute',
  command: 'npm install --verbose',
  timeoutMs: 180000 // 3 minutes for large installs
}
```

### Issue: Output missing or truncated

**Cause**: Streaming interrupted or chunks not collected

**Solution**: Verify all chunks are being consumed in handler; don't break early from streaming loop

```javascript
const allChunks = [];
await foreach (const chunk of stream) {
  allChunks.push(chunk); // Must consume ALL chunks
}
```

### Issue: DTE terminal not responding

**Cause**: Visual Studio terminal unavailable or locked

**Solution**: Check DTE status via `getStatus()`, retry operation, or fallback to external process

```csharp
var status = await collector.GetStatusAsync();
if (!status.IsResponsive) {
  // Fallback: Use child_process (execFile) instead
}
```

---

## Testing

### Unit Tests

**Node.js**: `src/versions/v2.0.0/tests/terminal-handler.test.mjs`

```bash
npx mocha src/versions/v2.0.0/tests/terminal-handler.test.mjs --timeout 15000
```

**C#**: `VSIXProject1.Tests/Services/TerminalCollectorTests.cs`

```bash
dotnet test VSIXProject1.Tests.csproj --filter TerminalCollectorTests
```

### Mock Fixtures

Use `terminal-collector-mock.mjs` for isolated testing:

```javascript
import { createMockTerminalCollector, createMockContext } from 'src/versions/v2.0.0/tests/mocks/terminal-collector-mock.mjs';

const context = createMockContext({ delayMs: 10, chunkSize: 100 });
const handler = createTerminalHandler(context);
```

---

## Integration with Continue Workflows

### Workflow 1: Run Tests & Show Results

1. Continue suggests `npm test`
2. User clicks "Run"
3. WebView sends `bridge:executeTerminalCommand` with `operation: 'execute'`
4. Handler streams output to webView in real-time
5. WebView displays pass/fail summary

### Workflow 2: Auto-fix with Terminal Feedback

1. Continue detects linting errors
2. Suggests fix + run linter
3. User approves
4. Handler executes `eslint --fix src/`
5. Terminal output streamed and displayed in sidebar
6. WebView confirms "3 issues fixed"

### Workflow 3: Interactive Terminal Input

1. User runs npm interactive mode: `npm init`
2. Handler sends input via `bridge:executeTerminalCommand` (sendInput operation)
3. Terminal prompts displayed in WebView
4. User responds via WebView input field
5. Handler queues input → C# collector → terminal

---

## Related Files

- **Handler**: `src/versions/v2.0.0/lib/terminal-handler.mjs` (372 lines)
- **Handler Tests**: `src/versions/v2.0.0/tests/terminal-handler.test.mjs` (535 lines)
- **Collector**: `VSIXProject1/Services/TerminalCollector.cs` (305 lines)
- **Collector Tests**: `VSIXProject1.Tests/Services/TerminalCollectorTests.cs` (280 lines)
- **Mock Fixtures**: `src/versions/v2.0.0/tests/mocks/terminal-collector-mock.mjs` (235 lines)
- **Registry**: `src/versions/v2.0.0/lib/handler-registry.mjs` (updated with 2 entries)

**Total Implementation**: ~1,960 lines of code + tests

---

## References

- **Step 82**: Terminal Handler implementation
- **Step 71**: Handler registration & dispatcher
- **Step 61**: DebugSessionHandler (subscription pattern)
- **Step 81**: GitIntegrationHandler (CLI command pattern)
- **Step 47**: Message routing middleware
- **Step 73**: Request/response validation
- **Step 25**: Bridge logger facade
- **Step 26**: Telemetry collector
