# ContinueVS Bridge Architecture — Detailed Technical Reference

**Version**: 2.1  
**Last Updated**: 2024-01-15  
**Status**: Active  
**Audience**: Core bridge developers, handler implementers, architecture reviewers  
**Related Documentation**: [protocol.md](protocol.md), [architecture.md](architecture.md), [exception-handling.md](exception-handling.md)

---

## Overview

The ContinueVS Bridge is an **npm-based message routing system** that bridges Visual Studio (C#) and the Continue AI engine (Node.js). This document provides a comprehensive technical foundation for understanding:

- **Bridge lifecycle** — initialization, runtime message loop, graceful shutdown
- **Core-server.js** — Node.js entry point and orchestration
- **Handler dispatcher** — message routing and handler registration
- **Handler adapter** — IDE state collection and type-safe handler creation
- **Transport layer** — stdio JSON-RPC protocol and message formats
- **Message flow** — end-to-end flow from WebView to Continue engine
- **Configuration & health** — bridge setup, feature flags, health checks, telemetry

---

## Architecture Overview

### Design Principles

The bridge is built on these core principles:

1. **Out-of-Process IPC** — No shared memory; stdio-based JSON-RPC communication
2. **Handler-Based Routing** — Message types map to registered handler functions
3. **Async-First** — All handlers are asynchronous; responses are Promise-based
4. **Error Isolation** — Handler errors don't crash the bridge; they propagate as error responses
5. **Stateless Handlers** — Each handler should be pure; context comes from parameters

### High-Level System Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                     Visual Studio (C#)                        │
│                  ContinueVSPackage (VSIX)                     │
│             ┌─────────────────────────────────┐               │
│             │   ContinueToolWindowControl     │               │
│             │  (WebView2 with React GUI)      │               │
│             │   window.postMessage() ←→       │               │
│             │   window.continueVS.onMessage() │               │
│             └─────────────────────────────────┘               │
│                      │                                        │
│                      │  stdio                                 │
│                      │  (JSON-RPC messages)                   │
│                      ↓                                        │
└──────────────────────────────────────────────────────────────┘
                    Parent Process
                    ────────────────

┌──────────────────────────────────────────────────────────────┐
│                   Node.js Child Process                       │
│                  src/versions/v2.0.0/...                      │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │           core-server.js                               │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  Initialization                                  │  │  │
│  │  │  • Parse CLI args                               │  │  │
│  │  │  • Validate npm package integrity (Step 12)     │  │  │
│  │  │  • Initialize logger, health check, telemetry   │  │  │
│  │  │  • Spawn Continue child process                 │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │                     │                                   │  │
│  │                     ↓                                   │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  Message Loop (stdin relay)                      │  │  │
│  │  │  ┌──────────────────────────────────────────┐   │  │  │
│  │  │  │ HandlerDispatcher                        │   │  │  │
│  │  │  │                                          │   │  │  │
│  │  │  │ if (messageType.startsWith('bridge:'))  │   │  │  │
│  │  │  │   → dispatch to handler                  │   │  │  │
│  │  │  │ else                                     │   │  │  │
│  │  │  │   → relay to Continue child process      │   │  │  │
│  │  │  └──────────────────────────────────────────┘   │  │  │
│  │  │         ↓              ↓                         │  │  │
│  │  │    [BRIDGE HANDLERS]  [CONTINUE RELAY]         │  │  │
│  │  │    (Steps 46–95)      (passthrough)            │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │                     │                                   │  │
│  │                     ↓                                   │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  Error Recovery & Shutdown                       │  │  │
│  │  │  • Continue crash → Restart (exponential backoff)│  │  │
│  │  │  • SIGTERM/SIGINT → Cleanup and exit            │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │        Registered Handler Map (Steps 50–61, 71)        │  │
│  │                                                        │  │
│  │  "bridge:getEditorState" → (data, context) => {...}   │  │
│  │  "bridge:onEditorStateChange" → (data, ctx) => {...}  │  │
│  │  "bridge:getWorkspaceDirs" → (data, ctx) => {...}     │  │
│  │  "bridge:readFile" → (data, ctx) => {...}             │  │
│  │  ... (30+ handlers registered at Step 71)              │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │           Continue Child Process Relay                 │  │
│  │                                                        │  │
│  │  Non-bridge messages pass through unchanged.           │  │
│  │  This allows the Continue engine to work as-is.        │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                               │
└──────────────────────────────────────────────────────────────┘
                    Child Process
                    ──────────────
```

---

## Bridge Lifecycle

### Startup Sequence

1. **CLI Argument Parsing** (core-server.js, line 100–130)
   - `--version` — Print bridge version and exit
   - `--health-check` — Run health check and exit with status code
   - `--log-level` — Set logger verbosity (debug, info, warn, error)
   - `--log-dir` — Directory for log files

2. **Initialization** (core-server.js, line 150–200)
   - Create logger instance (Step 25 or mock)
   - Create metrics collector (Step 26 or mock)
   - Create health check service (Step 24 or mock)
   - Log startup message with bridge version

3. **npm Package Validation** (core-server.js, line 210–250)
   - Check that npm package exists locally (`.cache/npm-packages/continue-vX.Y.Z.tgz`)
   - Validate package integrity using stored SHA256 checksums (Step 37)
   - If validation fails, report error and exit with code 1

   **Related Step 36**: Verify package contents before use
   - Extracts tar entry list without permanent extraction
   - Validates package.json structure
   - Confirms core-server.js entry point exists
   - Cross-references declared features with implementations
   - Returns structured report with metadata and error details
   - Used by `quickValidatePackage()` for fast startup checks
   - See: `src/versions/v2.0.0/lib/npm-package-validator.mjs`

4. **Log Directory Setup** (core-server.js, line 260–270)
   - Create `logs/` directory if not present
   - Set log stream for all subsequent messages

5. **Continue Child Process Spawning** (core-server.js, line 280–320)
   - Extract Continue npm package to temporary directory
   - Spawn `node scripts/continue-server.js` as child process
   - Attach stdin/stdout/stderr pipes for IPC

6. **Message Loop Initialization** (core-server.js, line 330–360)
   - Create readline interface on stdin
   - Create HandlerDispatcher instance (Step 14)
   - Register all handlers (Steps 50–61, consolidated at Step 71)
   - Enter message relay loop (await lines on stdin)

7. **Bridge Ready**
   - IDE can now send messages on stdin
   - Bridge routes them to handlers or relays to Continue
   - IDE receives responses on stdout

### Shutdown Sequence

1. **Graceful Signal Handling**
   - SIGTERM or SIGINT received
   - Log shutdown message
   - Set shutdown flag (prevents new message processing)

2. **Active Message Draining**
   - Allow in-flight handler promises to complete
   - Timeout after 5 seconds (Step 45)

3. **Child Process Termination**
   - Send SIGTERM to Continue child process
   - Wait up to 3 seconds for graceful exit
   - If still alive, send SIGKILL

4. **Resource Cleanup**
   - Close stdin/stdout readline interface
   - Close log file stream
   - Unregister all signal handlers

5. **Exit**
   - Exit with code 0 (success) or 1 (error)

### Error Recovery

If the Continue child process crashes:

```
Crash detected → Record error → Restart with exponential backoff

Restart Delays:
  Attempt 1 (1st crash): Wait 100ms, restart
  Attempt 2 (2nd crash): Wait 500ms, restart
  Attempt 3 (3rd crash): Wait 2000ms, restart
  After 3rd failure: Report to IDE; stop respawning

Max Retries: 3
Backoff Array: [100, 500, 2000] milliseconds
```

Logic (core-server.js, line 400–450):
```javascript
let restartAttempts = 0;
const RESTART_CONFIG = { maxRetries: 3, backoffMs: [100, 500, 2000] };

process.on('exit', () => {
  if (restartAttempts < RESTART_CONFIG.maxRetries) {
    const delay = RESTART_CONFIG.backoffMs[restartAttempts];
    restartAttempts++;
    setTimeout(() => spawn(...), delay);
  } else {
    logger.error('Continue process crashed 3 times; giving up');
    pushToIDE({ messageType: 'bridge:error', data: { code: 'PROCESS_CRASHED' } });
  }
});
```

---

## Core-server.js Entry Point

### File Location
`src/versions/v2.0.0/core-server.js` (647 lines)

### Purpose
Main entry point for the bridge. Handles:
- CLI argument parsing
- npm package integrity validation
- Logger initialization
- Continue child process spawning
- Message dispatch loop (stdin relay)
- Error recovery

### Key Functions

#### `main()`
Entry point function. Called at module load. Handles async initialization:
```javascript
async function main() {
  try {
    logger.info(`Starting ContinueVS Bridge v${BRIDGE_VERSION}`);

    // Parse CLI args
    const args = parseCliArgs(process.argv.slice(2));

    // Validate npm package
    await validateNpmPackage(args.packageVersion);

    // Initialize services
    const logger = createLogger(args.logLevel, args.logDir);
    const metrics = createMetrics();
    const healthCheck = createHealthCheck();

    // Spawn Continue
    const continueProcess = spawn(...);

    // Enter message loop
    await messageLoop(continueProcess);

  } catch (error) {
    console.error(`Fatal error: ${error.message}`);
    process.exit(1);
  }
}

main().catch(error => {
  console.error(`Unhandled error: ${error.message}`);
  process.exit(1);
});
```

#### `validateNpmPackage(version)`
Validates that the npm package exists and has valid integrity.
```javascript
async function validateNpmPackage(version) {
  const packagePath = path.join(
    process.cwd(),
    '.cache',
    'npm-packages',
    `continue-v${version}.tgz`
  );

  if (!fs.existsSync(packagePath)) {
    throw new Error(`npm package not found: ${packagePath}`);
  }

  // Validate SHA256 checksum (Step 37 manifest)
  const checksum = await computeSha256(packagePath);
  const manifest = loadVersionManifest(version); // Step 4

  if (checksum !== manifest.sha256) {
    throw new Error(
      `Checksum mismatch for ${packagePath}. ` +
      `Expected ${manifest.sha256}, got ${checksum}`
    );
  }
}
```

#### `validatePackageContents()` (Step 36)
**Module**: `src/versions/v2.0.0/lib/npm-package-validator.mjs`

Validates the *internal structure* of the downloaded npm package (.tgz archive) without permanent extraction. Ensures all required files and declared features are present.

**Validation Scope**:
1. Archive integrity — .tgz file is valid tar format
2. Package metadata — package.json exists with required fields
3. Entry point — lib/core-server.js is present
4. Required files — All mandatory files (package.json, core-server.js, handler-dispatcher.js)
5. Feature implementations — Declared features have corresponding implementations
6. Manifest consistency — Feature list matches actual files

**Key Functions**:
```javascript
// Orchestrator: Runs complete validation pipeline
const result = await validatePackageContents(packagePath, manifestPath);
// Returns: { valid, errors[], warnings[], fileCount, summary, timestamp }

// Quick check for startup (returns boolean, no throw)
const isValid = await quickValidatePackage(packagePath, manifestPath);
```

**Result Structure**:
```javascript
{
  valid: boolean,                    // true if all checks pass
  packagePath: string,               // input path
  manifestPath: string,              // input path
  archiveValid: boolean,             // tar format ok
  metadataValid: boolean,            // package.json ok
  entryPointValid: boolean,          // core-server.js found
  requiredFilesValid: boolean,       // all required files present
  featuresValid: boolean,            // all stable features have implementations
  fileCount: number,                 // total entries in archive
  errors: string[],                  // detailed error messages (stable features)
  warnings: string[],                // informational messages (experimental features)
  timestamp: string,                 // ISO 8601 when validation ran
  summary: {
    requiredFiles: string[],         // list of required files found
    entriesChecked: number,          // total tar entries processed
    validationDuration: number       // milliseconds
  }
}
```

**Error Types**:
- `ArchiveError` — .tgz file cannot be read or is corrupted
- `MetadataError` — Missing or invalid package.json, entry points, or required files
- `PackageValidationError` — General validation failure (parent of above)

**Feature-to-File Mappings**:
Defined in FEATURE_FILE_MAPPINGS constant:
- `coreEditorIntegration` → lib/core-server.js, lib/handler-dispatcher.js
- `diagnosticsCollection` → lib/handlers/diagnostics-handler.js
- `goToDefinition` → lib/handlers/goto-definition-handler.js
- `findReferences` → lib/handlers/find-references-handler.js
- `codeCompletion` → lib/handlers/completion-handler.js
- `search` → lib/handlers/search-handler.js
- `advancedSymbolSearch` → lib/handlers/symbol-search-handler.js (experimental)
- `webviewMessaging` → lib/handlers/webview-handler.js (experimental)

**Implementation Details**:
- Async/await throughout
- Reads tar entries without extracting files to disk
- Uses Node.js built-ins only (fs, stream, zlib, crypto)
- Memory-efficient streaming for large archives
- Temp resources automatically cleaned up
- Detailed error messages with recovery suggestions

**Integration Points**:
- **Step 35** → Receives .tgz package from download
- **Step 37** → Sends validation result to checksum generation  
- **Step 12** → Uses `quickValidatePackage()` for startup validation
- **Called from**: core-server.js startup sequence (line 210–250)

**Test Coverage**: 15/15 tests passing
- Valid packages, missing files, invalid archives, feature validation, error handling, resource cleanup



#### `messageLoop(continueProcess)`
Main relay loop. Reads lines from stdin, dispatches to handler or relays.
```javascript
async function messageLoop(continueProcess) {
  const readline = createInterface({ input: process.stdin });
  const dispatcher = new HandlerDispatcher({ logger, metrics, server });

  // Register all handlers (Steps 50–61 implementations, Step 71 consolidation)
  registerAllHandlers(dispatcher);

  for await (const line of readline) {
    try {
      const message = JSON.parse(line);

      // Check if bridge handler
      if (message.messageType.startsWith('bridge:')) {
        const result = await dispatcher.dispatch(message, { logger, metrics });
        console.log(JSON.stringify(result));
      } else {
        // Relay to Continue (passthrough)
        continueProcess.stdin.write(line + '\n');
      }

    } catch (error) {
      logger.error(`Message processing error: ${error.message}`);
      console.log(JSON.stringify({
        messageType: message?.messageType || 'unknown',
        messageId: message?.messageId || randomUUID(),
        success: false,
        error: error.message
      }));
    }
  }
}
```

#### `registerAllHandlers(dispatcher)`
Registers all bridge handlers with the dispatcher (Step 71).
```javascript
function registerAllHandlers(dispatcher) {
  // Step 50: getEditorState handler
  dispatcher.register(
    'bridge:getEditorState',
    handlers.getEditorStateHandler
  );

  // Step 51: onEditorStateChange subscription
  dispatcher.register(
    'bridge:onEditorStateChange',
    handlers.onEditorStateChangeHandler
  );

  // Step 52–61: Additional handlers (search, nav, completion, hover, etc.)
  // ... (30+ handlers total, registered here)

  logger.info(`Registered ${dispatcher.handlerCount()} bridge handlers`);
}
```

---

## Handler Dispatcher Architecture

### File Location
`src/versions/v2.0.0/lib/handler-dispatcher.js` (328 lines)

### Purpose
Manages handler registration and message routing. Routes:
- **Bridge messages** (prefixed `bridge:`) → to registered handlers
- **Other messages** → pass through to Continue relay

### Class: HandlerDispatcher

#### Constructor
```javascript
class HandlerDispatcher {
  constructor({ logger = null, metrics = null, server = null } = {}) {
    this.logger = logger || mockLogger();
    this.metrics = metrics || mockMetrics();
    this.server = server;
    this.handlers = new Map(); // messageType → handler function
  }
}
```

#### Key Methods

**`register(messageType, handler)`** — Register a handler for a message type
```javascript
register(messageType, handler) {
  if (!messageType || typeof handler !== 'function') {
    throw new Error(`Invalid handler registration`);
  }
  if (this.handlers.has(messageType)) {
    throw new Error(`Handler already registered for "${messageType}"`);
  }
  this.handlers.set(messageType, handler);
  this.logger.debug(`Registered handler for "${messageType}"`);
}
```

**`getHandler(messageType)`** — Retrieve a handler by message type
```javascript
getHandler(messageType) {
  return this.handlers.get(messageType) || null;
}
```

**`hasHandler(messageType)`** — Check if handler exists
```javascript
hasHandler(messageType) {
  return this.handlers.has(messageType);
}
```

**`dispatch(message, context)`** — Route a message to handler or relay
```javascript
async dispatch(message, context = {}) {
  const { messageType, messageId, data } = message;
  const handler = this.getHandler(messageType);

  if (!handler) {
    // Message should be relayed (not a bridge message)
    return {
      handled: false,
      shouldRelay: true
    };
  }

  try {
    const startTime = Date.now();
    const result = await handler(message, context);
    const latency = Date.now() - startTime;

    this.metrics.recordHandlerExecution(messageType, true, latency);

    return {
      handled: true,
      shouldRelay: false,
      response: {
        messageType,
        messageId,
        success: true,
        data: result
      }
    };
  } catch (error) {
    this.metrics.recordHandlerExecution(messageType, false, latency);

    return {
      handled: true,
      shouldRelay: false,
      response: {
        messageType,
        messageId,
        success: false,
        error: error.message
      }
    };
  }
}
```

#### Handler Registry (Map)

Internal `Map<messageType, handler>` structure:
```javascript
this.handlers = new Map([
  ['bridge:getEditorState', async (msg, ctx) => {...}],
  ['bridge:onEditorStateChange', async (msg, ctx) => {...}],
  ['bridge:getWorkspaceDirs', async (msg, ctx) => {...}],
  ['bridge:readFile', async (msg, ctx) => {...}],
  // ... 30+ handlers registered at Step 71
]);
```

#### Message Routing Algorithm

```
Input: message { messageType, messageId, data }

1. Check if messageType.startsWith('bridge:')
   ✓ YES → Lookup handler in registry
           Execute handler with (message, context)
           Wrap result in response envelope
           Return handled response

   ✗ NO  → Return { handled: false, shouldRelay: true }
           (core-server will pass to Continue relay)

2. Error Handling
   - If handler throws → Catch, record error, return error response
   - If handler times out → Return timeout error
   - If handler is missing → Log warning, relay message

Output: DispatchResult { handled, shouldRelay, response }
```

---

## Handler Adapter Pattern

### File Location
`src/versions/v2.0.0/lib/handler-adapter.js` (451 lines)

### Purpose
Provides a **type-safe factory** for creating handlers with automatic:
- Error wrapping and response formatting
- IDE state validation
- Handler execution tracking (latency, error rates)
- Debug logging

### Class: IDEStateAdapter

#### Constructor
```javascript
class IDEStateAdapter {
  constructor(dispatcher, options = {}) {
    if (!(dispatcher instanceof HandlerDispatcher)) {
      throw new Error('IDEStateAdapter requires HandlerDispatcher');
    }
    this.dispatcher = dispatcher;
    this.logger = options.logger || mockLogger();
    this.metrics = options.metrics || mockMetrics();
    this.enableLogging = options.enableLogging || false;
    this.handlerStats = new Map(); // Tracking per-handler stats
  }
}
```

#### Key Methods

**`createHandler(messageType, userFunction)`** — Factory for type-safe handlers

The adapter wraps a user-provided async function and handles error wrapping:

```javascript
createHandler(messageType, userFunction) {
  return async (message, context) => {
    const { messageId, data } = message;
    const startTime = Date.now();

    try {
      if (this.enableLogging) {
        this.logger.debug(
          `[${messageType}] Invoking handler with data:`,
          JSON.stringify(data)
        );
      }

      // Call user function with typed inputs
      const result = await userFunction(data, context);

      // Track success
      this._recordHandlerStat(messageType, true, Date.now() - startTime);

      return result; // Dispatcher will wrap as {success: true, data: result}

    } catch (error) {
      // Track error
      this._recordHandlerStat(messageType, false, Date.now() - startTime);

      this.logger.error(
        `[${messageType}] Handler error: ${error.message}`,
        error.stack
      );

      throw error; // Dispatcher will catch and wrap as error response
    }
  };
}
```

#### Usage Example

Creating a handler with the adapter:

```javascript
// Step 50: getEditorState handler implementation
const getEditorStateHandler = adapter.createHandler(
  'bridge:getEditorState',
  async (data, context) => {
    // User function receives typed inputs
    const editorState = await collectEditorState();
    return editorState; // Returns directly; adapter wraps
  }
);

// Register with dispatcher
dispatcher.register('bridge:getEditorState', getEditorStateHandler);

// IDE sends:
// {"messageType": "bridge:getEditorState", "messageId": "uuid-1", "data": {}}

// Adapter + Dispatcher returns:
// {"messageType": "bridge:getEditorState", "messageId": "uuid-1", "success": true, "data": {...}}
```

---

## Transport Layer: Stdio JSON-RPC Protocol

### Message Format

All messages are **line-delimited JSON** on stdin/stdout:

```json
{
  "messageType": "string",
  "messageId": "string (UUID)",
  "data": {}
}
```

**Example Request (IDE → Bridge):**
```json
{"messageType":"bridge:getEditorState","messageId":"550e8400-e29b-41d4-a716-446655440000","data":{}}
```

**Example Response (Bridge → IDE):**
```json
{"messageType":"bridge:getEditorState","messageId":"550e8400-e29b-41d4-a716-446655440000","success":true,"data":{"activeFile":"C:\\src\\Main.cs","cursorLine":42}}
```

**Example Error Response:**
```json
{"messageType":"bridge:getEditorState","messageId":"550e8400-e29b-41d4-a716-446655440000","success":false,"error":"No active editor"}
```

### Protocol Guarantees

| Guarantee | Description |
|-----------|-------------|
| **Line-delimited** | One JSON object per line; lines separated by `\n` |
| **Correlation** | `messageId` echoed in response for request–response matching |
| **No ordering** | Responses may arrive out of order; use `messageId` to match |
| **Async handlers** | Handlers may take arbitrarily long; no timeout enforced at transport layer |
| **Error propagation** | Handler errors wrapped in `success: false, error: string` |
| **Passthrough relay** | Non-bridge messages relayed unchanged to Continue |

### File I/O and Buffering

**Readline Interface** (`src/versions/v2.0.0/core-server.js`, line 350–370)

Uses Node.js `readline` module to handle line buffering:
```javascript
const readline = createInterface({
  input: process.stdin,
  crlfDelay: Infinity // Handle \r\n and \n
});

for await (const line of readline) {
  // One JSON object per iteration
  const message = JSON.parse(line);
  // ... dispatch or relay
}
```

**Output** (`stdout`)

Responses written directly to stdout:
```javascript
console.log(JSON.stringify(response)); // Outputs single JSON line + \n
```

### Error Response Format

Standard error response for any handler failure:

```javascript
{
  messageType: string,      // Echo of input messageType
  messageId: string,        // Echo of input messageId
  success: false,
  error: string             // Error message text
}
```

**Example (file read error):**
```json
{"messageType":"bridge:readFile","messageId":"uuid-2","success":false,"error":"File not found: /nonexistent/path.txt"}
```

---

## Message Flow: End-to-End

### Example: Editor State Request

**Flow Diagram:**
```
IDE (C#) → core-server.js → dispatcher → handler → response → IDE

1. [IDE] User opens a file in VS
   → EditorContextProvider detects change
   → Posts message: {"messageType":"bridge:onEditorStateChange","messageId":"uuid","data":{...}}
   → Writes to stdin

2. [core-server.js stdin] readline reads the line
   → JSON.parse() deserializes message
   → Checks: messageType.startsWith('bridge:') ✓
   → Looks up handler via dispatcher

3. [dispatcher] getHandler('bridge:onEditorStateChange')
   → Found: async (msg, ctx) => {...}
   → Calls handler with (message, context)

4. [handler] onEditorStateChangeHandler
   → Receives: data = { activeFile: "...", cursorLine: ... }
   → Executes user logic (update subscriptions, etc.)
   → Returns result or throws

5. [dispatcher] Wraps result
   → If success: {success: true, data: result}
   → If error: {success: false, error: message}
   → Adds messageType, messageId (echo)
   → Returns to core-server

6. [core-server.js stdout] Writes response
   → console.log(JSON.stringify(response))
   → IDE receives via window.continueVS.onMessage(json)

7. [IDE] WebView2 callback processes response
   → Updates UI state
   → Resolves pending promise
```

### Example: File Read Request with Relay

**Flow Diagram (Non-Bridge Message):**
```
IDE → core-server.js → Continue relay → Continue engine → response

1. [IDE] Posts: {"messageType":"complete","messageId":"uuid-3","data":{...}}

2. [core-server.js] Checks messageType.startsWith('bridge:') ✗
   → Not a bridge message
   → Calls dispatcher.dispatch() → returns {handled: false, shouldRelay: true}
   → Writes to continueProcess.stdin unchanged

3. [Continue Process] Receives "complete" message
   → Executes internal handler
   → Writes response to stdout

4. [core-server.js stdout relay] Reads Continue stdout
   → Writes response directly to stdout (IDE)

5. [IDE] Receives response from Continue
```

---

## Configuration & Feature Flags

### BridgeConfiguration Interface

Located: `src/VSIXProject1/Services/BridgeConfiguration.cs` (C# side, related to Step 18)

Passed to bridge via environment variables or config file.

**Configuration Properties:**

| Property | Type | Purpose | Step |
|----------|------|---------|------|
| `BridgeVersion` | string | Version of bridge (e.g., "2.0.0") | 1–4 |
| `FeatureFlagBridgeMode` | bool | Enable/disable bridge mode (Step 40) | 40 |
| `NpmPackageVersion` | string | Which Continue npm package to use | 2 |
| `LogLevel` | string | Logger verbosity: debug, info, warn, error | CLI arg |
| `LogDirectory` | string | Path to logs directory | CLI arg |
| `EnableTelemetry` | bool | Collect usage metrics (Step 26) | 26 |
| `HealthCheckInterval` | int | Health check interval in ms (Step 24) | 24 |
| `CrashRestartMaxRetries` | int | Max restarts on Continue crash | 13 |

### Passing Configuration to Bridge

**Via Environment Variables:**
```powershell
# C# → Node.js
$env:BRIDGE_VERSION = "2.0.0"
$env:BRIDGE_LOG_LEVEL = "debug"
$env:BRIDGE_FEATURE_FLAGS = "bridge_mode:true,telemetry:true"

node core-server.js --log-level debug --log-dir ./logs
```

**Via Config File:**
```json
{
  "bridge": {
    "version": "2.0.0",
    "logLevel": "debug",
    "featureFlags": {
      "bridgeMode": true,
      "enableTelemetry": true
    }
  }
}
```

### Feature Flags

**Step 40: Add feature flag for bridge mode**

**Environment Variable:** `FEATURE_FLAG_BRIDGE_MODE`

- **Source:** `ContinueOptionsPage.EnableBridgeMode` (Tools → Options → Continue → Bridge)
- **Exported by:** `BridgeConfigurationExtensions.ExportBridgeFlagsAsEnvironmentVariables()`
- **Values:**
  - `"true"` — Use npm-based bridge for all Continue communication (default)
  - `"false"` — Fall back to legacy translator binary

**Implementation Flow:**
1. User sets `Enable bridge mode` toggle in Tools → Options → Continue → Bridge
2. On extension startup, `ContinueVSPackage.InitializeAsync()` reads `ContinueOptionsPage.EnableBridgeMode`
3. Flag value is cached in `ContinueVSPackage.EnableBridgeMode` static property
4. During bridge lifecycle initialization (Step 45), `BridgeConfigurationExtensions.ExportBridgeFlagsAsEnvironmentVariables()` exports flag to environment
5. npm server (core-server.js) checks `process.env.FEATURE_FLAG_BRIDGE_MODE` at startup

**Usage in JavaScript (core-server.js):**
```javascript
if (process.env.FEATURE_FLAG_BRIDGE_MODE === 'false') {
  logger.warn('Bridge mode disabled; falling back to legacy translator');
  process.exit(1);
}
```

**Test Coverage:**
- Property defaults to `true` (safe for users)
- Property is readable/writable via VS Options UI
- Environment variable correctly exported for both `true`/`false` states
- Tests: `src/VSIXProject1.Tests/Settings/ContinueOptionsPageTests.cs`

---

## Health Checks and Monitoring

### Health Check Service (Step 24)

**File Location:**  
`src/versions/v2.0.0/lib/health-check.js`

**Purpose:**  
Periodically verify bridge and Continue process health.

**Checks Performed:**

1. **Continue Process Alive** — Check process.pid is still valid
2. **Stdio Responsive** — Send `ping` message, expect `pong` response within 1s
3. **npm Package Valid** — Re-validate checksums (Step 37)
4. **Handler Registry Populated** — Verify ≥ 30 handlers registered (Step 71)
5. **Logger Functional** — Write test message to log file

**API:**
```javascript
const healthCheck = new HealthCheck({ logger, metrics });

// Start periodic checks (every 30 seconds)
healthCheck.start(30000);

// Perform single check
const status = await healthCheck.check();
// Returns: { healthy: true|false, errors: [strings] }

// Stop checks
healthCheck.stop();
```

### Telemetry Collector (Step 26)

**File Location:**  
`src/versions/v2.0.0/lib/telemetry.js`

**Purpose:**  
Collect metrics on bridge operation: handler execution times, error rates, throughput.

**Metrics Recorded:**

| Metric | Description |
|--------|-------------|
| `handler.executions` | Handler invocation count (per handler type) |
| `handler.latency_ms` | Handler response time (histogram: p50, p95, p99) |
| `handler.errors` | Error count (per handler type) |
| `handler.error_rate` | Error rate as percentage |
| `bridge.messages_in` | Total messages received on stdin |
| `bridge.messages_out` | Total messages sent on stdout |
| `bridge.relay_messages` | Messages relayed (non-bridge passthrough) |
| `continue.restarts` | Count of Continue process restarts |
| `continue.crashes` | Count of unexpected crashes |

**API:**
```javascript
const metrics = new TelemetryCollector({ logger });

// Record handler execution
metrics.recordHandlerExecution(
  'bridge:getEditorState',
  true,  // success
  42     // latency in ms
);

// Record error
metrics.recordHandlerError('bridge:getEditorState', error);

// Get current metrics snapshot
const snapshot = metrics.getSnapshot();
// Returns: { handlers: {...}, bridge: {...}, continue: {...} }

// Export to external system
const json = metrics.toJSON();
```

### Logger Facade (Step 25)

**File Location:**  
`src/versions/v2.0.0/lib/logger.js`

**Purpose:**  
Unified logging with multiple outputs (console, file, telemetry).

**Levels:** debug, info, warn, error

**API:**
```javascript
const logger = new Logger({ level: 'info', logDir: './logs' });

logger.debug('Detailed debug info');
logger.info('General informational message');
logger.warn('Warning condition detected');
logger.error('Error occurred', new Error(...));
```

**Output Format:**
```
[2024-01-15T14:23:45.123Z] [INFO] Handler invoked: bridge:getEditorState
[2024-01-15T14:23:45.135Z] [DEBUG] Response: {"success":true,"data":{...}}
[2024-01-15T14:23:45.140Z] [ERROR] Handler error: File not found (stack trace follows)
```

---

## Crash Recovery

### Scenario: Continue Process Crashes

**Timeline:**
```
T=0ms    Continue process running normally
T=1000ms Continue crashes unexpectedly (e.g., exception)
T=1005ms core-server detects exit event
T=1010ms Log crash; check restart count
T=1020ms Restart wait begins (100ms for 1st attempt)
T=1120ms Continue respawned; message relay resumes
T=5000ms Another crash occurs (2nd attempt)
T=5010ms Log crash; check restart count
T=5020ms Restart wait begins (500ms for 2nd attempt)
T=5520ms Continue respawned again
T=10000ms Third crash (3rd attempt)
T=10010ms Log crash; check restart count
T=10020ms Restart wait begins (2000ms for 3rd attempt)
T=12020ms Continue respawned for 3rd time
T=15000ms Fourth crash — **max retries exceeded**
T=15010ms Log fatal error; push error message to IDE
T=15020ms Bridge exits or enters degraded mode
```

### Restart Logic

```javascript
let restartAttempts = 0;

process.on('exit', (code) => {
  logger.warn(`Continue process exited with code ${code}`);

  if (restartAttempts < 3) {
    const delay = [100, 500, 2000][restartAttempts];
    restartAttempts++;

    logger.info(`Scheduling restart in ${delay}ms (attempt ${restartAttempts})`);
    setTimeout(() => {
      continueProcess = spawn(...);
      attachHandlers(continueProcess);
    }, delay);
  } else {
    logger.error('Max restart attempts exceeded; bridge entering error state');
    pushErrorToIDE({
      messageType: 'bridge:error',
      data: { code: 'CONTINUE_CRASHED_MAX_RETRIES' }
    });
  }
});
```

---

## Summary & Next Steps

### What This Document Covers
✅ Bridge architecture overview  
✅ Bridge lifecycle (startup, runtime, shutdown)  
✅ core-server.js entry point and message loop  
✅ Handler dispatcher routing and registration  
✅ Handler adapter type-safe factory  
✅ Transport protocol (stdio JSON-RPC)  
✅ Message flow (end-to-end examples)  
✅ Configuration and feature flags  
✅ Health checks and telemetry  
✅ Crash recovery mechanisms  

### What This Document Does NOT Cover
- Individual handler implementations (see [BRIDGE-DEVELOPER-GUIDE.md](BRIDGE-DEVELOPER-GUIDE.md))
- npm package management (see [npm-cache-strategy.md](npm-cache-strategy.md))
- Protocol message types (see [protocol.md](protocol.md))
- Exception handling patterns (see [exception-handling.md](exception-handling.md))

### Next Steps in 155-Step Plan
- **Step 34**: Create npm dependency documentation
- **Steps 46–75**: Implement WebView handlers (use this guide to understand handler contract)
- **Steps 76–95**: Implement domain-specific handlers (see BRIDGE-DEVELOPER-GUIDE.md for patterns)
- **Step 97–99**: Create compliance, performance, and stress tests

### Related Documents
- **[BRIDGE-DEVELOPER-GUIDE.md](BRIDGE-DEVELOPER-GUIDE.md)** — Practical reference for creating handlers
- **[protocol.md](protocol.md)** — Full message type reference
- **[architecture.md](architecture.md)** — High-level architecture overview
- **[exception-handling.md](exception-handling.md)** — Custom exception patterns
- **[npm-cache-strategy.md](npm-cache-strategy.md)** — npm package management

---

**Document Version**: 2.1  
**Last Review**: 2024-01-15  
**Next Review**: After Step 45 completion gate
