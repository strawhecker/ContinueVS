# ContinueVS Bridge Developer Guide

**Version**: 2.1  
**Last Updated**: 2024-01-15  
**Status**: Active  
**Audience**: Handler implementers, developers adding new bridge features  
**Prerequisites**: Read [BRIDGE-ARCHITECTURE-DETAILED.md](BRIDGE-ARCHITECTURE-DETAILED.md) first  
**Related Documents**: [protocol.md](protocol.md), [exception-handling.md](exception-handling.md), [npm-cache-strategy.md](npm-cache-strategy.md)

---

## Quick Start

If you're here to **implement a new handler**, follow this checklist:

1. ✅ Read [Handler Contract](#handler-contract) (5 min)
2. ✅ Follow [Anatomy of a Handler](#anatomy-of-a-handler) (10 min)
3. ✅ Use [Creating a New Handler](#creating-a-new-handler) walkthrough (20 min)
4. ✅ Add tests using [Testing Handlers](#testing-handlers) patterns (15 min)
5. ✅ Register at [Step 71: Register All Handlers](#step-71-register-all-handlers) (5 min)
6. ✅ Check [Common Patterns](#common-patterns) for guidance (10 min)
7. ✅ Debug using [Debugging Handlers](#debugging-handlers) if needed (varies)

**Total Time**: ~75 minutes for a simple handler; 2–3 hours for complex handlers with subscriptions.

---

## Development Environment Setup

### Prerequisites

**Node.js**
- Version: 18.0.0 or higher
- Download: https://nodejs.org/ (LTS recommended)
- Verify: `node --version` (should print `v18.x.x` or higher)

**npm**
- Installed with Node.js
- Verify: `npm --version` (should print `9.x.x` or higher)

**Testing Framework**
- Mocha: `npm install --save-dev mocha@latest` (or use existing project setup)
- Chai: `npm install --save-dev chai@latest` (for assertions)

**IDE**
- VS Code recommended for Node.js debugging
- Visual Studio 2022+ with Node.js debugging support

### Repository Structure

```
E:\GitRepos\ContinueVS\
├── src/
│   └── versions/
│       └── v2.0.0/
│           ├── core-server.js                    [Entry point]
│           ├── lib/
│           │   ├── handler-dispatcher.js         [Routing]
│           │   ├── handler-adapter.js            [Factory]
│           │   ├── health-check.js               [Monitoring]
│           │   ├── logger.js                     [Logging]
│           │   └── telemetry.js                  [Metrics]
│           ├── handlers/                         [Step 50–61, 76–95]
│           │   ├── editor-context.js
│           │   ├── file-system.js
│           │   ├── git-integration.js
│           │   └── ... (30+ handlers)
│           ├── types/
│           │   └── handlers.d.js                 [Type hints]
│           └── tests/
│               ├── unit/
│               │   ├── handler-dispatcher.test.js
│               │   ├── handler-adapter.test.js
│               │   └── handlers/ (one test per handler)
│               └── integration/
│                   └── bridge.integration.test.js
├── docs/
│   ├── BRIDGE-ARCHITECTURE-DETAILED.md          [You are here →]
│   ├── BRIDGE-DEVELOPER-GUIDE.md                [Architecture reference]
│   ├── protocol.md                              [Message types]
│   ├── exception-handling.md                    [Error patterns]
│   └── npm-cache-strategy.md                    [npm setup]
└── package.json                                  [Dependencies]
```

### Running Tests

```bash
# All tests
npm test

# Specific test file
npm test -- tests/unit/handlers/editor-context.test.js

# Watch mode (auto-rerun on file change)
npm test -- --watch

# With coverage
npm test -- --coverage
```

---

## Handler Contract

### What is a Handler?

A **handler** is an asynchronous function that:
1. Receives a message from the IDE (C# client)
2. Processes the message (fetch data, transform, etc.)
3. Returns a result or throws an error
4. The bridge wraps the result in a response envelope

### Handler Signature

```typescript
// TypeScript-like signature
type HandlerFunction = (
  message: {
    messageType: string;      // e.g., "bridge:getEditorState"
    messageId: string;         // UUID for correlation
    data: any;                 // Handler-specific payload
  },
  context: {
    logger: Logger;
    metrics: Telemetry;
    server: CoreServer;
  }
) => Promise<any>;
```

### Example Handler

```javascript
// Simple: No parameters
async function pingHandler(message, context) {
  return { pong: true, timestamp: Date.now() };
}

// With data payload
async function readFileHandler(message, context) {
  const { filepath } = message.data;

  if (!filepath) {
    throw new Error('Missing required parameter: filepath');
  }

  const content = await fs.promises.readFile(filepath, 'utf-8');
  return { content };
}

// Complex: IDE state collection
async function getEditorStateHandler(message, context) {
  const ide = context.server.ide; // IDE state provider

  return {
    activeFile: ide.getActiveFilePath(),
    cursorLine: ide.getCursorLine(),
    cursorColumn: ide.getCursorColumn(),
    selectedText: ide.getSelectedText()
  };
}
```

### Response Format

The bridge **automatically wraps** the handler result:

**Success Response:**
```javascript
{
  messageType: "bridge:readFile",
  messageId: "uuid-123",
  success: true,
  data: { content: "..." }
}
```

**Error Response:**
```javascript
{
  messageType: "bridge:readFile",
  messageId: "uuid-123",
  success: false,
  error: "File not found: /path/to/file.txt"
}
```

**Important**: You don't manually create these response envelopes; the dispatcher does it automatically.

---

## EditorContextCollector

### Overview

The **EditorContextCollector** is a centralized cache manager for IDE editor state. It receives updates from the C# `EditorContextProvider` (active file, cursor position) via message handlers, normalizes the state, and exposes synchronous getters for handlers to query without refetching from the IDE.

**Key benefits:**
- ✅ **Decouples handlers from IDE integration** — Handlers query the collector, not the IDE directly
- ✅ **Reduces latency** — Cached state (no network round-trip per query)
- ✅ **Centralizes validation** — State normalization happens in one place
- ✅ **Manages subscriptions** — Built-in callback system for state change listeners (used by `onEditorStateChange` handler)

### Architecture

```
IDE (C#)
  └─ EditorContextProvider sends:
     • "currentFile" → {filepath, contents, cursorPosition}
     • "didChangeActiveTextEditor" → {filepath}

     ↓ (line-delimited JSON)

Bridge Node.js Server
  └─ EditorContextCollector (Step 48)
     • Receives updates via messageHandler subscription
     • Normalizes & caches state
     • Exposes synchronous getters

     ↓ (consumed by handlers)

  Step 50: getEditorStateHandler — queries collector.getActiveFile()
  Step 51: onEditorStateChangeHandler — subscribes via collector.onStateChange()
```

### Cached State Shape

```javascript
{
  activeFile: {
    filepath: "C:\\src\\Main.cs",
    contents: "using System;...",
    cursorLine: 42,        // 0-based line number
    cursorColumn: 10       // 0-based column offset
  },
  selection: {
    start: { line: 42, character: 10 },
    end: { line: 42, character: 20 },
    text: "selectedText"
  },
  lastUpdate: "2024-01-15T14:30:00.123Z"  // ISO timestamp
}
```

### Basic Usage

```javascript
import { EditorContextCollector } from '../lib/editor-context-collector.js';

// Step 46: During bridge initialization
const collector = new EditorContextCollector({ logger, metrics });
await collector.registerMessageHandlers(server);

// Step 50: In getEditorState handler
async function getEditorStateHandler(message, context) {
  const activeFile = collector.getActiveFile();

  if (!activeFile) {
    return { activeFile: null };
  }

  return {
    filepath: activeFile.filepath,
    cursorPosition: { 
      line: activeFile.cursorLine, 
      character: activeFile.cursorColumn 
    },
    contents: activeFile.contents
  };
}

// Step 51: In onEditorStateChange subscription
async function onEditorStateChangeHandler(message, context) {
  const { callback } = message.data;

  collector.onStateChange((newState, oldState) => {
    // Compare old and new to detect changes
    if (newState.activeFile?.filepath !== oldState.activeFile?.filepath) {
      console.log('Active file changed');
    }

    // Send update to webview
    context.server.messageHandler.emit('editorStateChanged', newState);
  });

  return { subscribed: true };
}
```

### Public API

**Constructor**
```javascript
const collector = new EditorContextCollector({
  logger: LoggerInstance,        // optional
  metrics: MetricsInstance       // optional
});
```

**Message Handler Registration** (async)
```javascript
await collector.registerMessageHandlers(server);
// Subscribe to "currentFile" and "didChangeActiveTextEditor" messages
```

**Synchronous Getters**
```javascript
collector.getActiveFile()      // → ActiveFile | null
collector.getCursorPosition()  // → {line, character} | null
collector.getSelection()       // → {start, end, text} | null
```

**Subscription**
```javascript
collector.onStateChange((newState, oldState) => {
  // Invoked whenever state changes
  // newState and oldState available for diffing
});
```

**Cleanup**
```javascript
collector.dispose();  // Remove all listeners (for shutdown/test cleanup)
```

### Error Handling

**EditorContextError** — Thrown during setup/registration failures:
```javascript
try {
  await collector.registerMessageHandlers(invalidServer);
} catch (error) {
  if (error instanceof EditorContextError) {
    console.error(`Setup failed: ${error.operationType}`);
  }
}
```

**StateValidationError** — Thrown when incoming data is malformed:
```javascript
try {
  collector.updateFileContext('', 'contents', {line: 0, character: 0});
} catch (error) {
  if (error instanceof StateValidationError) {
    console.error(`Validation failed in field: ${error.fieldName}`);
  }
}
```

### Performance Characteristics

- **Getters**: O(1) synchronous — no await needed
- **Message handling**: O(n) where n = number of listeners (typically 1–3)
- **Memory**: ~10–100 KB per file (depending on file size)
- **Timestamp**: ISO string (cached, updated on every state change)

### Related Steps

- **Step 49** — Selection tracker (parallel; similar pattern)
- **Step 50** — getEditorState handler (depends on Step 48; uses collector getters)
- **Step 51** — onEditorStateChange subscription (depends on Step 48; uses collector callbacks)
- **Step 67** — Handler tests (edge cases; tests collector functionality)
- **Step 71** — Handler registration (register handlers that depend on collector)

### Testing

```javascript
import { describe, it } from 'mocha';
import { EditorContextCollector } from '../lib/editor-context-collector.js';

describe('Editor Context', () => {
  it('should cache active file and return it via getActiveFile()', () => {
    const collector = new EditorContextCollector();
    collector.updateFileContext('file.cs', 'code', {line: 5, character: 10});

    const active = collector.getActiveFile();
    expect(active.filepath).to.equal('file.cs');
    expect(active.cursorLine).to.equal(5);
  });

  it('should invoke listener when state changes', (done) => {
    const collector = new EditorContextCollector();

    collector.onStateChange(() => {
      done();  // Callback was invoked
    });

    collector.updateFileContext('new.cs', 'text', {line: 0, character: 0});
  });
});
```

See `src/versions/v2.0.0/tests/editor-context-collector.test.mjs` for comprehensive test suite (32 tests, 100% coverage).

---

## SelectionTracker

### Overview

The **SelectionTracker** is a dedicated module that manages fine-grained text selection state within the active editor. It subscribes to "currentFile" messages from `EditorContextCollector`, extracts and caches selection data (start position, end position, selected text), and emits change events for handlers that need to react to selection changes.

**Key benefits:**
- ✅ **Separation of Concerns** — EditorContextCollector handles activeFile + cursor; SelectionTracker owns selection logic
- ✅ **Specialized Query Methods** — `isMultilineSelection()`, `getSelectedRange()`, `getSelectionLength()` for easy analysis
- ✅ **Change Notifications** — `onSelectionChange()` subscription for handlers like `onEditorStateChange` (Step 51)
- ✅ **State Validation** — Normalizes positions, validates ranges, gracefully handles malformed data

### Architecture

```
IDE (C#)
  └─ EditorContextProvider sends:
     • "currentFile" → {..., selection: {start, end, text}}

     ↓ (line-delimited JSON)

Bridge Node.js Server
  └─ SelectionTracker (Step 49)
     • Receives updates via messageHandler subscription
     • Normalizes & caches selection state
     • Exposes synchronous getters + subscriptions

     ↓ (consumed by handlers)

  Step 51: onEditorStateChange — subscribes via tracker.onSelectionChange()
  Step 60+: Various handlers — query tracker.getSelection(), tracker.isMultilineSelection()
```

### Cached State Shape

```javascript
{
  selection: {
    start: { line: 0, character: 10 },    // 0-based position
    end: { line: 0, character: 20 },      // 0-based position
    text: "selectedText",                  // Actual selected text
    isMultiline: false                     // Convenience flag
  },
  lastUpdate: "2024-01-15T14:30:00.123Z"  // ISO timestamp
}
```

### Basic Usage

```javascript
import { SelectionTracker } from '../lib/selection-tracker.mjs';

// Step 46: During bridge initialization
const tracker = new SelectionTracker({ logger, metrics });
await tracker.registerMessageHandlers(server);

// Step 51: In onEditorStateChange subscription
async function onEditorStateChangeHandler(message, context) {
  tracker.onSelectionChange((newSelection, oldSelection) => {
    console.log(`Selection changed to: "${newSelection?.text || '(cleared)'}"`);

    // Notify Continue via webview
    context.server.messageHandler.emit('editorStateChanged', {
      hasSelection: tracker.hasSelection(),
      isMultiline: tracker.isMultilineSelection(),
      length: tracker.getSelectionLength(),
      range: tracker.getSelectedRange()
    });
  });

  return { subscribed: true };
}

// Step 76+: In refactor or code action handlers
async function refactorHandler(message, context) {
  if (!tracker.hasSelection()) {
    return { error: 'No text selected' };
  }

  const range = tracker.getSelectedRange();
  console.log(`Refactoring lines ${range.startLine}–${range.endLine}`);

  if (tracker.isMultilineSelection()) {
    console.log('User selected multiple lines — can apply block refactor');
  }

  return { success: true };
}
```

### Public API

**Constructor**
```javascript
const tracker = new SelectionTracker({
  logger: LoggerInstance,        // optional
  metrics: MetricsInstance       // optional
});
```

**Message Handler Registration** (async)
```javascript
await tracker.registerMessageHandlers(server);
// Subscribe to "currentFile" messages from EditorContextCollector
```

**Update Selection** (typically called internally via message handler)
```javascript
tracker.updateSelection(
  { line: 0, character: 10 },    // start position
  { line: 0, character: 20 },    // end position
  'selectedText'                  // text content
);
```

**Synchronous Getters**
```javascript
tracker.getSelection()            // → {start, end, text, isMultiline} | null
tracker.hasSelection()            // → boolean
tracker.isMultilineSelection()   // → boolean
tracker.getSelectedRange()       // → {startLine, startChar, endLine, endChar} | null
tracker.getSelectionLength()     // → number (character count)
```

**Subscription**
```javascript
tracker.onSelectionChange((newSelection, oldSelection) => {
  // Invoked whenever selection changes
  // newSelection and oldSelection available for diffing
  // oldSelection is null on first change
});
```

**Cleanup**
```javascript
tracker.dispose();  // Remove all listeners (for shutdown/test cleanup)
```

### Error Handling

**SelectionTrackerError** — Thrown during setup/registration failures:
```javascript
try {
  await tracker.registerMessageHandlers(invalidServer);
} catch (error) {
  if (error instanceof SelectionTrackerError) {
    console.error(`Setup failed: ${error.operationType}`);
  }
}
```

**StateValidationError** — Thrown when incoming data is malformed:
```javascript
try {
  tracker.updateSelection(
    { line: 'invalid' },           // Invalid line number
    { line: 0, character: 10 },
    'text'
  );
} catch (error) {
  if (error instanceof StateValidationError) {
    console.error(`Validation failed in field: ${error.fieldName}`);
  }
}
```

### Performance Characteristics

- **Getters**: O(1) synchronous — no await needed
- **Message handling**: O(n) where n = number of listeners (typically 1–3)
- **Memory**: ~1–5 KB per selection (very small; positions + text)
- **Change detection**: Deep equality check (avoids spurious events)

### Related Steps

- **Step 48** — EditorContextCollector (provides "currentFile" messages)
- **Step 51** — onEditorStateChange subscription (depends on Step 49; consumes tracker callbacks)
- **Step 60+** — Various handlers (query tracker state for refactoring, code actions, etc.)
- **Step 67** — Handler tests (validates SelectionTracker integration)

### Testing

```javascript
import { describe, it } from 'mocha';
import { SelectionTracker } from '../lib/selection-tracker.mjs';

describe('Selection Tracking', () => {
  it('should update selection and return it via getSelection()', () => {
    const tracker = new SelectionTracker();
    tracker.updateSelection(
      { line: 0, character: 5 },
      { line: 0, character: 10 },
      'hello'
    );

    const selection = tracker.getSelection();
    expect(selection.text).to.equal('hello');
    expect(selection.isMultiline).to.be.false;
  });

  it('should detect multiline selections', () => {
    const tracker = new SelectionTracker();
    tracker.updateSelection(
      { line: 0, character: 0 },
      { line: 3, character: 10 },
      'multi\nline\ntext'
    );

    expect(tracker.isMultilineSelection()).to.be.true;
    const range = tracker.getSelectedRange();
    expect(range.endLine).to.equal(3);
  });

  it('should invoke listener when selection changes', (done) => {
    const tracker = new SelectionTracker();

    tracker.onSelectionChange((newSel, oldSel) => {
      expect(newSel.text).to.equal('test');
      expect(oldSel).to.be.null;
      done();
    });

    tracker.updateSelection(
      { line: 0, character: 0 },
      { line: 0, character: 4 },
      'test'
    );
  });
});
```

See `src/versions/v2.0.0/tests/selection-tracker.test.mjs` for comprehensive test suite (27 tests, covering all query methods, error handling, message integration, and edge cases).

---

## Get Editor State Handler (Step 50)

### Overview

The **getEditorState handler** is a stateless query handler that returns a snapshot of the current editor state. It queries the `EditorContextCollector` (Step 48) and synthesizes a complete `EditorState` response containing file path, cursor position, selection, and diagnostics.

**Message Type**: `bridge:getEditorState`
**Input**: BridgeMessage (no parameters required)
**Output**: BridgeResponse with EditorState data
**Dependencies**: EditorContextCollector (Step 48)

### Architecture

```
[Continue/IDE] sends bridge:getEditorState request
  ↓
[dispatcher] routes to getEditorStateHandler
  ↓
[handler] queries EditorContextCollector for:
  • activeFile (filepath, contents, cursorLine, cursorColumn, language, projectPath, diagnosticsCount)
  • cursorPosition (line, character)
  • selection (text, start, end)
  ↓
[handler] assembles EditorState typedef
  ↓
[dispatcher] wraps in BridgeResponse { success: true, data: EditorState }
  ↓
[core-server] sends response via stdio
```

### EditorState Typedef

```javascript
{
  activeFile: "/home/user/file.cs" | null,          // Current file path or null
  cursorLine: 42,                                    // 0-based line number
  cursorColumn: 10,                                  // 0-based column offset
  selectedText: "foo",                               // Selected text or empty string
  selectionStart: 100,                               // Selection start offset (0-based)
  selectionEnd: 103,                                 // Selection end offset (0-based)
  fileContent: "using System;\n...",                 // Full file contents
  language: "csharp",                                // Language ID (e.g., "csharp", "python")
  projectPath: "/home/user/project",                 // Workspace root path
  diagnosticsCount: 3,                               // Number of diagnostics at cursor
  lastUpdate: "2024-01-15T10:30:00.000Z"             // ISO timestamp (optional)
}
```

### Usage

```javascript
import { getEditorStateHandler, createGetEditorStateHandler } from '../lib/get-editor-state-handler.mjs';

// Option 1: Use handler directly (requires context.editorContextCollector)
const response = await getEditorStateHandler(message, context);

// Option 2: Create bound handler for dependency injection (Step 71)
const boundHandler = createGetEditorStateHandler(editorContextCollector);
dispatcher.registerHandler('bridge:getEditorState', boundHandler);

// Expected response (success):
{
  success: true,
  data: {
    activeFile: "C:\\project\\Main.cs",
    cursorLine: 5,
    cursorColumn: 10,
    selectedText: "Program",
    selectionStart: 40,
    selectionEnd: 47,
    fileContent: "using System;\nclass Program {}",
    language: "csharp",
    projectPath: "C:\\project",
    diagnosticsCount: 2,
    lastUpdate: "2024-01-15T10:30:00.000Z"
  }
}

// Expected response (no file open):
{
  success: true,
  data: {
    activeFile: null,
    cursorLine: 0,
    cursorColumn: 0,
    selectedText: "",
    selectionStart: -1,
    selectionEnd: -1,
    fileContent: "",
    language: "unknown",
    projectPath: "",
    diagnosticsCount: 0,
    lastUpdate: null
  }
}
```

### Error Handling

**GetEditorStateError** — Thrown when collector is not available:
```javascript
try {
  const response = await getEditorStateHandler(msg, { editorContextCollector: null });
} catch (error) {
  if (error instanceof GetEditorStateError) {
    console.error(`Error: ${error.message}`);
    console.error(`Operation: ${error.operationType}`);
  }
}

// Returns:
{
  success: false,
  error: {
    code: "EDITOR_STATE_ERROR",
    message: "EditorContextCollector not initialized in context",
    details: { operationType: "init" }
  }
}
```

### Performance Characteristics

- **Latency**: ~1–2 ms (synchronous collector queries)
- **Memory**: No allocations (returns existing cached state)
- **Throughput**: Can handle hundreds of concurrent requests
- **Timestamp**: Generated at handler execution time (not collector cache time)

### Related Steps

- **Step 48** — EditorContextCollector (state source)
- **Step 49** — SelectionTracker (parallel implementation)
- **Step 51** — onEditorStateChange subscription (subscription variant)
- **Step 62** — Handler type definitions (EditorState typedef)
- **Step 67** — Handler tests (editor context) — includes tests for this handler
- **Step 71** — Handler registration (registers this handler)

### Testing

```javascript
import { describe, it } from 'mocha';
import { getEditorStateHandler } from '../lib/get-editor-state-handler.mjs';
import { createEditorContextCollectorMock } from './mocks/editor-context-collector-mock.mjs';

describe('getEditorStateHandler', () => {
  it('should return complete editor state', async () => {
    // Arrange
    const collector = createEditorContextCollectorMock({
      activeFile: {
        filepath: 'C:\\project\\Main.cs',
        contents: 'code',
        cursorLine: 5,
        cursorColumn: 10,
        language: 'csharp',
        projectPath: 'C:\\project',
        diagnosticsCount: 2
      },
      cursorPosition: { line: 5, character: 10 },
      selection: { text: 'text', start: 40, end: 44 }
    });
    const context = { editorContextCollector: collector };

    // Act
    const response = await getEditorStateHandler({}, context);

    // Assert
    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.activeFile, 'C:\\project\\Main.cs');
    assert.strictEqual(response.data.cursorLine, 5);
    assert.strictEqual(response.data.selectedText, 'text');
  });

  it('should handle missing collector gracefully', async () => {
    const response = await getEditorStateHandler({}, { editorContextCollector: null });
    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'EDITOR_STATE_ERROR');
  });
});
```

See `src/versions/v2.0.0/tests/get-editor-state-handler.test.mjs` for comprehensive test suite (15 tests, covering happy path, errors, partial state, and edge cases).

---

## Anatomy of a Handler

### Step 1: Define the Message Type

Message types follow a naming convention:

```
bridge:              [prefix — identifies as bridge message]
<domain>:            [category — e.g., editor, file, config, llm]
<verb>               [action — e.g., getState, onChange, execute]

Examples:
  bridge:getEditorState       [domain=editor, verb=getState]
  bridge:onEditorStateChange  [domain=editor, verb=onChange]
  bridge:readFile             [domain=file, verb=read]
  bridge:applyDiff            [domain=file, verb=applyDiff]
  bridge:getBranch            [domain=git, verb=getBranch]
```

### Step 2: Define Request Payload Schema

Document what data the handler expects:

```javascript
/**
 * Message: bridge:readFile
 * 
 * Request Payload:
 *   filepath (string, required) — Absolute path to file
 *   encoding (string, optional) — File encoding (default: 'utf-8')
 *   maxBytes (number, optional) — Max bytes to read (default: none)
 * 
 * Response Payload (on success):
 *   content (string) — File contents
 *   byteLength (number) — Bytes read
 *   encoding (string) — Encoding used
 * 
 * Error Cases:
 *   "File not found: <path>"
 *   "Permission denied: <path>"
 *   "File too large: requested <N> bytes, max is <M>"
 */
async function readFileHandler(message, context) {
  // ... implementation
}
```

### Step 3: Implement the Handler

Start with the basic structure:

```javascript
async function myHandler(message, context) {
  const { data, messageId } = message;
  const { logger, metrics, server } = context;

  // Step 1: Extract and validate input
  const { requiredParam, optionalParam = 'default' } = data;

  if (!requiredParam) {
    throw new Error('Missing required parameter: requiredParam');
  }

  // Step 2: Log start (optional, helps debugging)
  logger?.debug(`[myHandler] Starting with requiredParam=${requiredParam}`);

  // Step 3: Perform async work
  try {
    const result = await doExpensiveOperation(requiredParam);

    // Step 4: Validate result before returning
    if (!result || typeof result !== 'object') {
      throw new Error('Unexpected result format');
    }

    // Step 5: Log completion
    logger?.debug(`[myHandler] Completed successfully`);

    // Step 6: Return result (dispatcher wraps it)
    return result;

  } catch (error) {
    // Errors are automatically wrapped by dispatcher
    logger?.error(`[myHandler] Error: ${error.message}`);
    throw error; // Re-throw; dispatcher catches it
  }
}
```

### Step 4: Add JSDoc Comments

Document the handler for future maintainers:

```javascript
/**
 * Handler: bridge:readFile
 * 
 * Reads the contents of a file from the filesystem.
 * Used by the IDE to load file contents for display/editing.
 * 
 * @param {Object} message - Message envelope
 * @param {string} message.messageId - Correlation UUID
 * @param {Object} message.data - Request payload
 * @param {string} message.data.filepath - File path (absolute)
 * @param {string} [message.data.encoding='utf-8'] - File encoding
 * @param {number} [message.data.maxBytes=10485760] - Max bytes (10MB default)
 * @param {Object} context - Dispatch context
 * @param {Logger} context.logger - Logger instance
 * @param {Telemetry} context.metrics - Metrics collector
 * @param {CoreServer} context.server - Core server reference
 * @returns {Promise<{content: string, byteLength: number, encoding: string}>}
 * @throws {Error} If file not found, permission denied, etc.
 */
async function readFileHandler(message, context) {
  // ...
}
```

---

## Creating a New Handler

### Example 1: Simple Handler (No Parameters)

**Scenario**: Step 50 — getEditorState handler (receives no input)

```javascript
// File: src/versions/v2.0.0/handlers/editor-context.js

/**
 * Handler: bridge:getEditorState
 * 
 * Returns the current editor state: active file, cursor position, selection.
 * Called by the IDE to sync editor context.
 */
export async function getEditorStateHandler(message, context) {
  const { logger, server } = context;

  try {
    const ide = server.getIDEState(); // Hypothetical API

    return {
      activeFile: ide.activeFilePath,
      cursorLine: ide.cursorLine,
      cursorColumn: ide.cursorColumn,
      selectedText: ide.selectedText,
      diagnostics: ide.diagnostics // Current errors/warnings
    };
  } catch (error) {
    logger?.error(`getEditorState error: ${error.message}`);
    throw error;
  }
}
```

**Registration** (Step 71):
```javascript
// In registerAllHandlers(dispatcher):
dispatcher.register(
  'bridge:getEditorState',
  getEditorStateHandler
);
```

**Test** (Step 67):
```javascript
// File: tests/unit/handlers/editor-context.test.js
import { expect } from 'chai';
import { getEditorStateHandler } from '../../../src/versions/v2.0.0/handlers/editor-context.js';

describe('getEditorStateHandler', () => {
  it('should return current editor state', async () => {
    // Arrange
    const message = {
      messageType: 'bridge:getEditorState',
      messageId: 'test-uuid',
      data: {}
    };
    const mockServer = {
      getIDEState: () => ({
        activeFilePath: 'C:\\src\\Main.cs',
        cursorLine: 42,
        cursorColumn: 10
      })
    };
    const context = { logger: null, server: mockServer };

    // Act
    const result = await getEditorStateHandler(message, context);

    // Assert
    expect(result.activeFile).to.equal('C:\\src\\Main.cs');
    expect(result.cursorLine).to.equal(42);
  });
});
```

### Example 2: Handler with Input Parameters

**Scenario**: Step 52 — readFile handler (receives filepath)

```javascript
// File: src/versions/v2.0.0/handlers/file-system.js

/**
 * Handler: bridge:readFile
 * 
 * Reads file contents from disk.
 * 
 * Request: { filepath: string, encoding?: string, maxBytes?: number }
 * Response: { content: string, byteLength: number }
 */
export async function readFileHandler(message, context) {
  const { filepath, encoding = 'utf-8', maxBytes = 10485760 } = message.data;
  const { logger } = context;

  // Validate input
  if (!filepath || typeof filepath !== 'string') {
    throw new Error('Missing or invalid parameter: filepath');
  }

  if (filepath.includes('..')) {
    throw new Error('Path traversal not allowed');
  }

  logger?.debug(`[readFile] Reading: ${filepath}`);

  try {
    // Read file with size check
    const stats = await fs.promises.stat(filepath);
    if (stats.size > maxBytes) {
      throw new Error(
        `File too large: ${stats.size} bytes exceeds max ${maxBytes}`
      );
    }

    const content = await fs.promises.readFile(filepath, encoding);

    return {
      content,
      byteLength: content.length,
      encoding
    };
  } catch (error) {
    if (error.code === 'ENOENT') {
      throw new Error(`File not found: ${filepath}`);
    }
    if (error.code === 'EACCES') {
      throw new Error(`Permission denied: ${filepath}`);
    }
    throw error;
  }
}
```

**Registration**:
```javascript
dispatcher.register('bridge:readFile', readFileHandler);
```

**Test**:
```javascript
describe('readFileHandler', () => {
  it('should read file contents', async () => {
    const message = {
      messageType: 'bridge:readFile',
      messageId: 'test-uuid',
      data: { filepath: '/tmp/test.txt' }
    };
    const context = { logger: null };

    // Mock fs.promises.readFile
    const fs = {
      promises: {
        stat: async () => ({ size: 100 }),
        readFile: async () => 'Hello World'
      }
    };

    const result = await readFileHandler(message, context);
    expect(result.content).to.equal('Hello World');
  });

  it('should throw on file not found', async () => {
    const message = {
      messageType: 'bridge:readFile',
      messageId: 'test-uuid',
      data: { filepath: '/nonexistent.txt' }
    };

    try {
      await readFileHandler(message, { logger: null });
      expect.fail('Should have thrown');
    } catch (error) {
      expect(error.message).to.include('File not found');
    }
  });
});
```

### Example 3: Handler with Subscriptions

**Scenario**: Step 51 — onEditorStateChange (sends updates over time)

```javascript
// File: src/versions/v2.0.0/handlers/editor-context.js

/**
 * Handler: bridge:onEditorStateChange
 * 
 * Subscribes to editor state changes. Sends periodic updates
 * whenever the active file or cursor position changes.
 * 
 * Request: {} (no parameters)
 * Response (on change):
 *   - Sends multiple updates with same messageId
 *   - Each update: { success: true, data: { activeFile, cursorLine, ... } }
 */
export async function onEditorStateChangeHandler(message, context) {
  const { messageId } = message;
  const { logger, server } = context;

  // Create a subscription
  const subscription = server.ide.onDidChangeActiveTextEditor(async (editor) => {
    const state = {
      activeFile: editor.document.fileName,
      cursorLine: editor.selection.active.line,
      cursorColumn: editor.selection.active.character
    };

    // Send update back to IDE with same messageId (for correlation)
    server.pushToIDE({
      messageType: 'bridge:onEditorStateChange',
      messageId,
      success: true,
      data: state
    });

    logger?.debug(`Sent editor state update: ${editor.document.fileName}`);
  });

  // Return cleanup function
  return {
    subscriptionId: subscription.id,
    unsubscribe: () => subscription.dispose()
  };
}
```

**Key Pattern**: Subscriptions **don't wait** for completion. They return immediately with a subscription ID; updates are sent asynchronously via `pushToIDE()`.

---

## Message Routing Middleware (Step 47)

### Purpose

The **MiddlewareChain** system enables composable message routing between `core-server.js` and the handler dispatcher. Middleware functions can intercept messages in three phases:

1. **Pre-dispatch**: Validate, transform, or log incoming messages
2. **Dispatch**: Route to handler via the dispatcher
3. **Post-dispatch**: Transform, log, or handle errors from responses

### Architecture

**Middleware Signature**:
```javascript
async function middleware(message, next, context) {
  // Pre-dispatch phase
  message.startTime = Date.now();

  // Call next middleware or dispatcher
  const result = await next();

  // Post-dispatch phase
  result.duration = Date.now() - message.startTime;
  return result;
}
```

**Execution Order**:
```
Message
  ↓
[Validation Hook] ← registered by Step 73
  ↓
[User Middleware 1] ← registered with chain.use()
  ↓
[User Middleware 2] ← registered with chain.use()
  ↓
[Logging Hook] ← registered by Step 72
  ↓
HandlerDispatcher.dispatch()
```

### Basic Usage

**Setup in core-server.js**:
```javascript
import {
  MiddlewareChain,
  wrapDispatcher,
  createMiddlewareChain
} from './lib/message-routing-middleware.mjs';

// Create middleware chain
const chain = createMiddlewareChain({
  logger: config.logger,
  metrics: config.metrics,
  server: coreServer
});

// Register custom middleware (Steps 72-74 will do this)
chain.registerHook('validationHook', validationMiddleware);
chain.registerHook('loggingHook', loggingMiddleware);
chain.registerHook('errorRecoveryHook', errorRecoveryMiddleware);

// Wrap dispatcher
const wrappedDispatcher = wrapDispatcher(chain, dispatcher);

// Use in message loop
for await (const line of readline) {
  const message = JSON.parse(line);
  const result = await wrappedDispatcher.dispatch(message, context);
  console.log(JSON.stringify(result.response || message));
}
```

### Implementing Middleware

**Example: Custom Timing Middleware**:
```javascript
const timingMiddleware = async (message, next, context) => {
  const start = Date.now();

  try {
    const result = await next();
    const duration = Date.now() - start;

    context.metrics?.recordHandlerExecution?.(
      message.messageType,
      duration,
      result.response?.success
    );

    return result;
  } catch (err) {
    const duration = Date.now() - start;
    context.logger?.error(`Handler failed after ${duration}ms`, {
      messageType: message.messageType,
      error: err.message
    });
    throw err;
  }
};

chain.use(timingMiddleware);
```

### Available Hooks

**Three built-in hooks are available for Steps 72-74**:

| Hook | Purpose | Injection Point | Example Use |
|------|---------|-----------------|-------------|
| `validationHook` | Pre-dispatch validation | First (before user middleware) | Check message schema, rate limiting |
| `loggingHook` | Post-dispatch logging | Last (after user middleware) | Log request/response, audit trail |
| `errorRecoveryHook` | Error handling | Wraps entire chain | Catch errors, send fallback responses |

**Register a hook**:
```javascript
chain.registerHook('validationHook', async (message, next, context) => {
  // Validate message before dispatch
  if (!message.messageId) {
    throw new Error('Missing messageId');
  }
  return next();
});
```

### Error Handling

**Middleware exceptions are wrapped and propagated**:
```javascript
import { MiddlewareExecutionError } from './lib/message-routing-middleware.mjs';

try {
  const result = await chain.execute(message, dispatcher, context);
} catch (err) {
  if (err instanceof MiddlewareExecutionError) {
    console.error(`Middleware error in [${err.operation}]: ${err.message}`);
    console.error('Original error:', err.originalError);
  }
}
```

### Testing Middleware

**Mock-based testing pattern**:
```javascript
import { MiddlewareChain } from './lib/message-routing-middleware.mjs';

// Test that middleware executes in correct order
const chain = new MiddlewareChain();
const execution = [];

const mw1 = async (msg, next) => {
  execution.push('mw1-pre');
  const result = await next();
  execution.push('mw1-post');
  return result;
};

chain.use(mw1);

// Mock dispatcher
const dispatcher = {
  async dispatch(msg, ctx) {
    execution.push('dispatch');
    return { response: { success: true } };
  }
};

const message = { messageType: 'bridge:test', messageId: 'msg-1' };
await chain.execute(message, dispatcher);

assert.deepStrictEqual(
  execution,
  ['mw1-pre', 'dispatch', 'mw1-post']
);
```

### Performance Considerations

- **Middleware stacking**: Each middleware adds ~0.1–0.5ms overhead
- **Hook order**: Validation hooks first (fail fast), logging hooks last
- **Error recovery**: Keep try-catch blocks focused (don't catch and swallow validation errors)

---

## Handler Registration

### Step 71: Register All Handlers

All handlers are registered in a single location. This enables:
- Central visibility (see all handlers at a glance)
- Clear dependency order
- Easy testing

**Location**: `src/versions/v2.0.0/core-server.js`, function `registerAllHandlers(dispatcher)`

**Example Registration Block**:

```javascript
/**
 * Step 71: Register all handlers with dispatcher
 * 
 * Called during core-server.js startup (line 180–220).
 * Handlers are registered in logical groups by domain.
 * 
 * Related Steps:
 *   - Step 50: getEditorState
 *   - Step 51: onEditorStateChange
 *   - Step 52: readFile, writeFile
 *   - ... (30+ handlers)
 */
function registerAllHandlers(dispatcher) {
  const logger = dispatcher.logger;

  // Step 50–51: Editor Context (2 handlers)
  dispatcher.register(
    'bridge:getEditorState',
    handlers.getEditorStateHandler
  );
  dispatcher.register(
    'bridge:onEditorStateChange',
    handlers.onEditorStateChangeHandler
  );

  // Step 52: File System (2 handlers)
  dispatcher.register(
    'bridge:readFile',
    handlers.readFileHandler
  );
  dispatcher.register(
    'bridge:writeFile',
    handlers.writeFileHandler
  );

  // Step 55–61: Search, Navigation, Completion, Hover (6 handlers)
  dispatcher.register('bridge:search', handlers.searchHandler);
  dispatcher.register('bridge:goToDefinition', handlers.goToDefinitionHandler);
  dispatcher.register('bridge:findReferences', handlers.findReferencesHandler);
  dispatcher.register('bridge:codeCompletion', handlers.codeCompletionHandler);
  dispatcher.register('bridge:hoverInfo', handlers.hoverInfoHandler);
  dispatcher.register('bridge:testExplorer', handlers.testExplorerHandler);

  // ... (additional handler groups)

  logger.info(`Registered ${dispatcher.getHandlerCount()} bridge handlers`);
}
```

### Naming Conventions

| Category | Pattern | Examples |
|----------|---------|----------|
| **Query** | `bridge:<domain>:<verb>` | `bridge:getEditorState`, `bridge:getWorkspaceDirs` |
| **Mutation** | `bridge:<domain>:<verb>` | `bridge:writeFile`, `bridge:applyDiff` |
| **Subscription** | `bridge:on<Domain><Event>` | `bridge:onEditorStateChange`, `bridge:onDiagnosticsUpdate` |
| **Action** | `bridge:<domain>:<verb>` | `bridge:formatDocument`, `bridge:refactor` |

---

## Testing Handlers

### Unit Test Structure

Every handler should have a unit test file:

```
src/versions/v2.0.0/handlers/
├── editor-context.js
├── file-system.js
└── ...

tests/unit/handlers/
├── editor-context.test.js
├── file-system.test.js
└── ...
```

### Test Template

```javascript
// File: tests/unit/handlers/my-handler.test.js

import { expect } from 'chai';
import sinon from 'sinon'; // For mocking
import { myHandler } from '../../../src/versions/v2.0.0/handlers/my-domain.js';

describe('myHandler', () => {
  // ============================================
  // Test 1: Happy path (success case)
  // ============================================
  describe('Success Case', () => {
    it('should return expected result', async () => {
      // Arrange: Set up inputs and mocks
      const message = {
        messageType: 'bridge:myMessage',
        messageId: 'test-uuid',
        data: { param: 'value' }
      };

      const mockLogger = { debug: sinon.stub(), error: sinon.stub() };
      const context = { logger: mockLogger };

      // Act: Call handler
      const result = await myHandler(message, context);

      // Assert: Verify result
      expect(result).to.exist;
      expect(result).to.have.property('expectedField');
      expect(mockLogger.debug.called).to.be.true;
    });
  });

  // ============================================
  // Test 2: Input validation
  // ============================================
  describe('Input Validation', () => {
    it('should throw on missing required parameter', async () => {
      const message = {
        messageType: 'bridge:myMessage',
        messageId: 'test-uuid',
        data: {} // Missing required 'param'
      };

      try {
        await myHandler(message, { logger: null });
        expect.fail('Should have thrown');
      } catch (error) {
        expect(error.message).to.include('required parameter');
      }
    });

    it('should handle optional parameters with defaults', async () => {
      const message = {
        messageType: 'bridge:myMessage',
        messageId: 'test-uuid',
        data: { param: 'value' } // No 'optionalParam'
      };

      const result = await myHandler(message, { logger: null });
      expect(result.optionalField).to.equal('default-value');
    });
  });

  // ============================================
  // Test 3: Error cases
  // ============================================
  describe('Error Cases', () => {
    it('should throw on external service failure', async () => {
      const message = {
        messageType: 'bridge:myMessage',
        messageId: 'test-uuid',
        data: { param: 'trigger-error' }
      };

      // Mock external service to fail
      const mockService = {
        doWork: sinon.stub().rejects(new Error('Service down'))
      };

      try {
        await myHandler(message, { service: mockService, logger: null });
        expect.fail('Should have thrown');
      } catch (error) {
        expect(error.message).to.include('Service down');
      }
    });
  });
});
```

### Testing with Mocks

Use `sinon` for mocking dependencies:

```javascript
import sinon from 'sinon';

describe('Handler with mocks', () => {
  it('should call IDE state provider', async () => {
    // Create mock IDE state
    const mockIDE = {
      getActiveFilePath: sinon.stub().returns('C:\\Main.cs'),
      getCursorLine: sinon.stub().returns(42)
    };

    const mockServer = {
      ide: mockIDE
    };

    const context = { server: mockServer, logger: null };
    const message = {
      messageType: 'bridge:getEditorState',
      messageId: 'uuid',
      data: {}
    };

    const result = await getEditorStateHandler(message, context);

    // Assert mocks were called
    expect(mockIDE.getActiveFilePath.called).to.be.true;
    expect(mockIDE.getCursorLine.called).to.be.true;
    expect(result.activeFile).to.equal('C:\\Main.cs');
    expect(result.cursorLine).to.equal(42);
  });
});
```

### Integration Test

Test handlers in the context of the full dispatcher:

```javascript
// File: tests/integration/handler-dispatcher.test.js

import { expect } from 'chai';
import { HandlerDispatcher } from '../src/versions/v2.0.0/lib/handler-dispatcher.js';
import { IDEStateAdapter } from '../src/versions/v2.0.0/lib/handler-adapter.js';
import * as handlers from '../src/versions/v2.0.0/handlers/index.js';

describe('Handler Dispatcher Integration', () => {
  let dispatcher;
  let adapter;

  beforeEach(() => {
    dispatcher = new HandlerDispatcher({ logger: null });
    adapter = new IDEStateAdapter(dispatcher);
  });

  it('should dispatch message to registered handler', async () => {
    // Register handler
    dispatcher.register('bridge:test', handlers.testHandler);

    // Send message
    const message = {
      messageType: 'bridge:test',
      messageId: 'uuid',
      data: { value: 'hello' }
    };

    // Dispatch
    const result = await dispatcher.dispatch(message, {});

    // Verify response
    expect(result.handled).to.be.true;
    expect(result.response.success).to.be.true;
    expect(result.response.data.value).to.equal('hello');
  });

  it('should handle handler errors gracefully', async () => {
    // Register handler that throws
    dispatcher.register('bridge:error', async () => {
      throw new Error('Test error');
    });

    const message = {
      messageType: 'bridge:error',
      messageId: 'uuid',
      data: {}
    };

    const result = await dispatcher.dispatch(message, {});

    expect(result.handled).to.be.true;
    expect(result.response.success).to.be.false;
    expect(result.response.error).to.include('Test error');
  });
});
```

---

## Common Patterns

### Pattern 1: Input Validation

Always validate required parameters:

```javascript
export async function myHandler(message, context) {
  const { requiredParam, optionalParam = 'default' } = message.data;

  // Check required param
  if (!requiredParam) {
    throw new Error('Missing required parameter: requiredParam');
  }

  // Validate type
  if (typeof requiredParam !== 'string') {
    throw new Error(
      `Invalid type for requiredParam: expected string, got ${typeof requiredParam}`
    );
  }

  // Validate range/format
  if (requiredParam.length > 1000) {
    throw new Error('requiredParam exceeds max length of 1000');
  }

  // ... handler logic
}
```

### Pattern 2: Error Classification

Use custom error classes (see [exception-handling.md](exception-handling.md)):

```javascript
import {
  BridgeError,
  ValidationError,
  NotFoundError,
  PermissionError
} from '../lib/errors.js';

export async function myHandler(message, context) {
  try {
    // File operation
    const result = await fs.promises.readFile(filepath);
  } catch (error) {
    if (error.code === 'ENOENT') {
      throw new NotFoundError(`File not found: ${filepath}`);
    }
    if (error.code === 'EACCES') {
      throw new PermissionError(`Access denied: ${filepath}`);
    }
    throw new BridgeError(`Unexpected error: ${error.message}`);
  }
}
```

### Pattern 3: Async/Await

Always use `async/await` for clarity:

```javascript
// ✓ Good: Clear async flow
export async function myHandler(message, context) {
  const step1 = await operation1();
  const step2 = await operation2(step1);
  return step2;
}

// ✗ Avoid: Promise chains (harder to read)
export function myHandler(message, context) {
  return operation1()
    .then(step1 => operation2(step1))
    .then(step2 => step2);
}

// ✗ Avoid: Promise.all for sequential ops (not parallel)
export async function myHandler(message, context) {
  const [step1, step2] = await Promise.all([operation1(), operation2()]);
}
```

**Exception**: Use `Promise.all()` when operations are truly parallel:

```javascript
export async function myHandler(message, context) {
  // These don't depend on each other; run in parallel
  const [fileContent, diagnostics, symbols] = await Promise.all([
    readFile(filepath),
    fetchDiagnostics(filepath),
    extractSymbols(filepath)
  ]);

  return { fileContent, diagnostics, symbols };
}
```

### Pattern 4: Timeout Handling

For operations that might hang, use a timeout wrapper:

```javascript
function withTimeout(promise, timeoutMs) {
  return Promise.race([
    promise,
    new Promise((_, reject) =>
      setTimeout(() => reject(new Error('Operation timed out')), timeoutMs)
    )
  ]);
}

export async function myHandler(message, context) {
  const result = await withTimeout(
    expensiveOperation(),
    5000 // 5 second timeout
  );
  return result;
}
```

### Pattern 5: Logging for Debugging

Strategic logging helps with troubleshooting:

```javascript
export async function myHandler(message, context) {
  const { logger } = context;
  const { filepath } = message.data;

  logger?.debug(`[myHandler] Starting; filepath=${filepath}`);

  try {
    const result = await readFile(filepath);
    logger?.debug(`[myHandler] Read ${result.length} bytes`);
    return result;
  } catch (error) {
    logger?.error(`[myHandler] Failed: ${error.message}`, error.stack);
    throw error;
  }
}
```

### Pattern 6: Caching (Optional)

For expensive operations, consider caching:

```javascript
const cache = new Map();

export async function getSymbolsHandler(message, context) {
  const { filepath } = message.data;

  // Check cache
  if (cache.has(filepath)) {
    context.logger?.debug(`[getSymbols] Cache hit for ${filepath}`);
    return cache.get(filepath);
  }

  // Cache miss; fetch
  context.logger?.debug(`[getSymbols] Cache miss for ${filepath}`);
  const symbols = await extractSymbols(filepath);

  // Store in cache (with 1 minute TTL)
  cache.set(filepath, symbols);
  setTimeout(() => cache.delete(filepath), 60000);

  return symbols;
}
```

---

## Debugging Handlers

### Enable Debug Logging

Set environment variable before running:

```bash
# PowerShell
$env:DEBUG = "bridge:*"
node core-server.js --log-level debug

# Bash
DEBUG=bridge:* node core-server.js --log-level debug
```

Or programmatically:

```javascript
// In core-server.js before starting loop
const logger = new Logger({ level: 'debug', logDir: './logs' });
const dispatcher = new HandlerDispatcher({ logger });

dispatcher.logger.debug('Debug logging enabled');
```

### Add Console Logging (Temporary)

For quick debugging, add `console.log()` statements:

```javascript
export async function myHandler(message, context) {
  console.log('Handler called with:', message); // Remove after debugging

  const result = await doWork();

  console.log('Handler returning:', result); // Remove after debugging

  return result;
}
```

### Use Node.js Debugger

**In VS Code:**

1. Create `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "node",
      "request": "launch",
      "name": "Launch Bridge",
      "program": "${workspaceFolder}/src/versions/v2.0.0/core-server.js",
      "console": "integratedTerminal",
      "skipFiles": ["<node_internals>/**"]
    }
  ]
}
```

2. Set breakpoints in handler code
3. Press F5 to start debugging
4. Send test message on stdin; execution will break at breakpoint

### Inspect Message Flow

Log all messages to understand flow:

```javascript
// In core-server.js, in messageLoop()
for await (const line of readline) {
  const message = JSON.parse(line);

  // Log every message
  console.error(`[IN] ${message.messageType} (${message.messageId})`);

  const result = await dispatcher.dispatch(message, context);

  // Log every response
  console.error(`[OUT] ${result.response?.messageType} (${result.response?.messageId})`);

  console.log(JSON.stringify(result.response || message));
}
```

### Common Issues & Solutions

| Issue | Symptom | Solution |
|-------|---------|----------|
| **Handler not found** | `handled: false` in response | Check handler is registered at Step 71 |
| **Timeout** | No response, IDE hangs | Add timeout wrapper; check for infinite loops |
| **Wrong response format** | `success: false` but should be `true` | Ensure handler doesn't throw for success cases |
| **Missing dependencies** | `Cannot find module` | Run `npm install` and check imports |
| **Async bug** | Handler runs but returns immediately | Use `await` on async operations |
| **Context is undefined** | `Cannot read property of undefined` | Verify context passed to dispatcher |

---

## Performance Considerations

### RPC Latency Budget

Handlers should complete within these latencies:

| Operation | Budget | Notes |
|-----------|--------|-------|
| **In-memory query** | < 50ms | (e.g., getEditorState) |
| **Disk I/O** | < 200ms | (e.g., readFile up to 1MB) |
| **External API call** | < 500ms | (e.g., git status) |
| **Complex computation** | < 1000ms | (e.g., symbol extraction) |

**Monitor latency:**
```javascript
// In handler
const start = Date.now();
const result = await doWork();
const latency = Date.now() - start;

context.logger?.debug(
  `[myHandler] Completed in ${latency}ms`
);
```

### Chunking Large Responses

If a response exceeds 10MB, split into chunks:

```javascript
export async function readLargeFileHandler(message, context) {
  const { filepath, chunkSize = 1024 * 1024 } = message.data; // 1MB chunks

  const fileSize = await getFileSize(filepath);

  // Return file in chunks
  const chunks = [];
  for (let offset = 0; offset < fileSize; offset += chunkSize) {
    const chunk = await readFileChunk(filepath, offset, chunkSize);
    chunks.push({
      offset,
      data: chunk,
      isLast: offset + chunkSize >= fileSize
    });
  }

  return { chunks };
}
```

### Avoid Blocking Operations

Never use synchronous I/O:

```javascript
// ✗ Bad: Synchronous I/O blocks entire bridge
const content = fs.readFileSync(filepath);

// ✓ Good: Async I/O doesn't block
const content = await fs.promises.readFile(filepath);
```

---

## Troubleshooting Checklist

| Check | Action |
|-------|--------|
| Handler registered? | Verify in Step 71; run `dispatcher.listHandlers()` |
| Message type correct? | Check exact string match (case-sensitive) |
| Input validation? | Add explicit error messages for missing params |
| Async/await used? | Check all awaits are present |
| Errors caught? | Verify try/catch wraps async operations |
| Dependencies available? | Check imports and `npm install` |
| Timeout? | Add timeout wrapper; check for infinite loops |
| Large response? | Split into chunks; check size |
| Performance? | Profile with context.metrics |
| Test passing? | Run `npm test` before submitting |

---

## Summary & Next Steps

### What This Guide Covers
✅ Handler contract and signature  
✅ Anatomy of a handler (definition, input, output, JSDoc)  
✅ Creating handlers (simple, parameterized, subscriptions)  
✅ Handler registration at Step 71  
✅ Testing handlers (unit, integration, mocks)  
✅ Common patterns (validation, error handling, async, logging)  
✅ Debugging techniques (logging, breakpoints, message tracing)  
✅ Performance considerations (latency budgets, chunking)  
✅ Troubleshooting checklist  

### What This Guide Does NOT Cover
- Individual handler implementations (see [protocol.md](protocol.md) for message types)
- Bridge architecture (see [BRIDGE-ARCHITECTURE-DETAILED.md](BRIDGE-ARCHITECTURE-DETAILED.md))
- Exception classes (see [exception-handling.md](exception-handling.md))
- npm package setup (see [npm-cache-strategy.md](npm-cache-strategy.md))

### Next Steps in 155-Step Plan

**Immediate (Steps 46–75):**
- Step 46: WebView bootstrap handler
- Steps 50–61: Implement core handlers (editor context, file system, search, navigation, etc.)
- Step 71: Register all handlers with dispatcher (use Step 71 registration block from this guide)
- Step 75: WebView integration tests

**Future (Steps 76–95):**
- Implement domain-specific handlers (refactor, fix-suggestion, apply-edit, git, terminal, etc.)
- Use common patterns from this guide for consistency

**Quality (Steps 97–99):**
- Compliance tests (all handlers implement contract)
- Performance tests (latency budgets met)
- Stress tests (concurrent message handling)

### Key Takeaway

Handlers are **simple async functions** that:
1. Receive a typed request (`message.data`)
2. Perform work (I/O, computation, etc.)
3. Return a result or throw an error
4. The bridge wraps the result automatically

The dispatcher handles routing, error wrapping, and response formatting. **You only implement the handler function logic.**

---

**Document Version**: 2.1  
**Last Review**: 2024-01-15  
**Next Review**: After Step 71 completion
