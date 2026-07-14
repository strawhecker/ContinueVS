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

## Document Provider (Step 52)

### Overview

The **DocumentProvider** is a centralized cache for open documents in the IDE. It receives document lifecycle updates from the C# layer via message handlers and maintains normalized document state. Handlers (Steps 53–61) query this provider instead of re-fetching from the IDE, improving performance and reducing coupling to IDE services.

**Module**: `src/versions/v2.0.0/lib/document-provider.mjs`
**Message Types**: `openDocuments`, `didOpenDocument`, `didChangeDocument`, `didCloseDocument`
**Export Classes**: `DocumentProvider`, `DocumentProviderError`, `DocumentValidationError`
**Related Steps**: 46 (bootstrap), 53–61 (handlers), 67 (tests), 71 (registry)

### Public API

**Constructor**
```javascript
const provider = new DocumentProvider({
  logger: LoggerInstance,          // optional; defaults to no-op
  metrics: MetricsInstance         // optional; defaults to no-op
});
```

**Message Handler Registration** (async)
```javascript
await provider.registerMessageHandlers(server);
// Subscribe to openDocuments, didOpenDocument, didChangeDocument, didCloseDocument
// Throws DocumentProviderError if server or messageHandler invalid
```

**Synchronous Getters** (thread-safe)
```javascript
provider.getDocument(filepath)               // → Document | null
provider.getAllDocuments()                   // → Document[]
provider.getDocumentByLanguage('csharp')    // → Document[]
provider.getDocumentMetadata(filepath)       // → {filepath, language, isDirty, lines, metadata} | null
provider.hasDocument(filepath)               // → boolean
provider.getDocumentCount()                  // → number
```

**Subscriptions**
```javascript
const unsubscribe1 = provider.onDocumentChange((newDoc, oldDoc) => {
  console.log(`Document changed: ${newDoc.filepath}`);
});

const unsubscribe2 = provider.onDocumentOpen((doc) => {
  console.log(`Document opened: ${doc.filepath}`);
});

const unsubscribe3 = provider.onDocumentClose((filepath) => {
  console.log(`Document closed: ${filepath}`);
});

// Later: stop listening
unsubscribe1();
unsubscribe2();
unsubscribe3();
```

**Cleanup**
```javascript
provider.dispose();  // Clear cache and remove all listeners
```

### Document Typedef

```javascript
{
  filepath: string,                // Absolute file path
  contents: string,                // Full file contents
  language: string,                // Programming language (e.g., 'csharp', 'javascript', 'python')
  isDirty: boolean,                // Whether document has unsaved changes
  encoding: string,                // Character encoding (default: 'utf-8')
  lines: number,                   // Cached line count (calculated from contents)
  lastModified: number,            // Unix timestamp of last modification
  metadata: {                       // Additional metadata
    projectPath?: string,
    compiler?: string,
    framework?: string,
    customData?: any
  }
}
```

### Message Types

**openDocuments** — Bulk load of all open documents (initial sync)
```javascript
// From IDE (C#)
{
  messageType: "openDocuments",
  data: {
    documents: [
      { filepath, contents, language, isDirty, metadata, ... },
      // ...
    ]
  }
}

// Effect: Clears cache, adds all documents, emits onDocumentOpen for each
```

**didOpenDocument** — Single document opened
```javascript
// From IDE (C#)
{
  messageType: "didOpenDocument",
  data: { filepath, contents, language, isDirty, metadata }
}

// Effect: Adds document to cache, emits onDocumentOpen
```

**didChangeDocument** — Document modified (content or dirty flag)
```javascript
// From IDE (C#)
{
  messageType: "didChangeDocument",
  data: { filepath, contents, isDirty }
}

// Effect: Updates document, emits onDocumentChange with old state
```

**didCloseDocument** — Document closed
```javascript
// From IDE (C#)
{
  messageType: "didCloseDocument",
  data: { filepath }
}

// Effect: Removes document from cache, emits onDocumentClose
```

### Error Handling

**DocumentProviderError** — Thrown during setup/registration failures:
```javascript
try {
  await provider.registerMessageHandlers(invalidServer);
} catch (error) {
  if (error instanceof DocumentProviderError) {
    console.error(`Setup failed: ${error.operationType}`);
    // operationType: 'registration', 'initialization', etc.
    if (error.originalError) {
      console.error(`Original error: ${error.originalError.message}`);
    }
  }
}
```

**DocumentValidationError** — Thrown when incoming data is malformed:
```javascript
// Validation happens internally; logged but not thrown externally
// Handlers log errors and continue gracefully
```

### Usage in Handlers

**Step 53: Symbol Extractor**
```javascript
// Extract symbols from file with optional filtering
import { SymbolExtractor } from '../lib/symbol-extractor.mjs';

const extractor = new SymbolExtractor({
  logger: context.logger,
  metrics: context.metrics,
  documentProvider: documentProvider,
  cacheSize: 100
});

// Extract all symbols
const result = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: incomingSymbolTableFromCSharp
});

// Extract with filtering
const methods = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: incomingSymbolTableFromCSharp,
  kind: 'method',
  scope: 'public'
});

// Search by pattern
const matching = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: incomingSymbolTableFromCSharp,
  searchPattern: /Test/i
});
```

**Step 54: Diagnostics Collector**
```javascript
// Initialize and register diagnostics collector
import { DiagnosticsCollector } from '../lib/diagnostics-collector.mjs';

const collector = new DiagnosticsCollector({
  logger: context.logger,
  metrics: context.metrics
});

// Register to receive messages from C# IDE
await collector.registerMessageHandlers(bridgeServer);

// Query diagnostics for a file
const allDiags = collector.getDiagnosticsForFile('src/main.cs');
const errors = collector.getDiagnosticsForFile('src/main.cs', 'error');
const warnings = collector.getDiagnosticsForFile('src/main.cs', 'warning');

// Query diagnostics at a cursor position
const diagnosticsAtCursor = collector.getDiagnosticsRange('src/main.cs', 10, 5);

// Query diagnostics in a selection
const diagnosticsInSelection = collector.getDiagnosticsRange(
  'src/main.cs',
  10, 5,  // startLine, startColumn
  10, 20  // endLine, endColumn
);

// Get all diagnostics across all files
const allByFile = collector.getAllDiagnostics(); // Map<filepath, Diagnostic[]>

// Get all diagnostics filtered by severity
const allErrors = collector.getDiagnosticsBySeverity('error');   // Map<filepath, Diagnostic[]>
const allWarnings = collector.getDiagnosticsBySeverity('warning');
const allInfos = collector.getDiagnosticsBySeverity('info');

// Listen for diagnostics changes
collector.onDiagnosticsChange((event) => {
  console.log(`${event.filepath} diagnostics changed: ${event.diagnostics.length} issues`);
  console.log(`Change type: ${event.changeType}`); // "open" | "update" | "close"
});

// Query diagnostic counts
const count = collector.getDiagnosticsCount('src/main.cs');
const totalCount = collector.getDiagnosticsCount();
const hasIssues = collector.hasDiagnostics('src/main.cs');

// Cleanup
collector.dispose();
```

**Diagnostic Structure** (from C# IDE):
```javascript
{
  code: string,           // e.g., "CS0001", "IDE0001"
  message: string,        // Human-readable description
  severity: string,       // "error" | "warning" | "info"
  line: number,           // 0-based line number
  column: number,         // 0-based column offset
  endLine?: number,       // 0-based end line (optional)
  endColumn?: number,     // 0-based end column (optional)
  file: string            // Absolute file path
}
```

**Message Types** (sent by C# IDE):
- `didOpenDiagnostics` — Initial diagnostics for a file that was opened
- `didUpdateDiagnostics` — Updated diagnostics (file modified, re-analysis complete)
- `didCloseDiagnostics` — Diagnostics for a file that was closed

**Error Classes**:
- `DiagnosticsCollectorError` — Registration or initialization failure
- `DiagnosticsValidationError` — Invalid diagnostic data (missing required fields, invalid values)

**Step 55+: Search, Navigation, Completions, etc.**
```javascript
// All handlers follow similar pattern
const docs = provider.getDocumentByLanguage('csharp');
docs.forEach((doc) => {
  // Process document
});
```

### Performance Characteristics

- **Getters**: O(1) synchronous — no await needed
- **Message handling**: O(1) per message; O(n) for listeners where n = subscriber count
- **Memory**: 100 bytes overhead per document + file content size
- **Cache invalidation**: Automatic on close; update on change
- **Listener isolation**: Exceptions in one listener do not affect others

### Related Steps

- **Step 46** — WebView bootstrap handler (instantiates provider)
- **Step 48** — Editor context collector (parallel caching pattern)
- **Step 49** — Selection tracker (parallel state tracking)
- **Step 53–61** — Handler implementations (consume provider queries)
- **Step 67** — Handler tests (test provider integration)
- **Step 71** — Handler registration (handlers register dependencies on provider)

### Testing

```javascript
import { describe, it } from 'mocha';
import { DocumentProvider } from '../lib/document-provider.mjs';
import { createMockServer, getMockCSharpDocument } from './mocks/document-mock.mjs';

describe('Document Provider', () => {
  it('should cache and retrieve documents', async () => {
    const provider = new DocumentProvider();
    const server = createMockServer();
    await provider.registerMessageHandlers(server);

    const doc = getMockCSharpDocument();
    server.messageHandler.emit('openDocuments', {
      data: { documents: [doc] }
    });

    const retrieved = provider.getDocument(doc.filepath);
    expect(retrieved).to.exist;
    expect(retrieved.filepath).to.equal(doc.filepath);
    expect(retrieved.language).to.equal('csharp');
  });

  it('should notify listeners of changes', (done) => {
    const provider = new DocumentProvider();
    const server = createMockServer();
    await provider.registerMessageHandlers(server);

    const doc = getMockCSharpDocument();
    server.messageHandler.emit('openDocuments', {
      data: { documents: [doc] }
    });

    provider.onDocumentChange((newDoc, oldDoc) => {
      expect(newDoc.isDirty).to.be.true;
      done();
    });

    server.messageHandler.emit('didChangeDocument', {
      data: {
        filepath: doc.filepath,
        contents: doc.contents + '\n// change',
        isDirty: true
      }
    });
  });
});
```

See `src/versions/v2.0.0/tests/document-provider.test.mjs` for comprehensive test suite (31 tests, 100% coverage, 8 suites: initialization, registration, cache ops, queries, state tracking, listeners, disposal, edge cases).

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

## Symbol Extractor (Step 53)

### Overview

The **Symbol Extractor** is a stateful query handler that parses JSON symbol tables (sent by the C# bridge) and returns filtered, hierarchical code symbols (classes, methods, properties, etc.). It validates symbol structures, builds a tree hierarchy, and caches parsed tables for performance.

**Message Type**: `bridge:extractSymbols`  
**Input**: BridgeMessage with { filepath, symbolTable?, kind?, scope?, searchPattern?, includeChildren? }  
**Output**: BridgeResponse with { symbols: SymbolInfo[], metadata: {...}, filepath }  
**Dependencies**: DocumentProvider (Step 52) — optional context provider  
**Cache**: LRU-based, configurable (default 100 tables)  
**Performance**: Parse ~5–10ms; cache hits <1ms; 80%+ improvement on repeated calls  

### Architecture

```
┌─────────────────┐
│ C# Bridge       │ — Extracts symbols via Roslyn
└────────┬────────┘
         │ sends JSON symbol table
         ↓
┌────────────────────────────────┐
│ Symbol Extractor               │ — Validates & parses
├────────────────────────────────┤
│ • parseSymbolTable()           │ — Parse + validate JSON
│ • extractSymbols()             │ — Filter + return results
│ • _buildSymbolHierarchy()      │ — Organize into tree
│ • _filterSymbols()             │ — Apply criteria
└────────┬───────────────────────┘
         │ caches parsed table
         ↓
┌─────────────────┐
│ Symbol Cache    │ — LRU map (filepath → table)
└────────┬────────┘
         │
         ↓
┌────────────────────────────────┐
│ Handler Response               │ — SymbolInfo[]
└────────────────────────────────┘
```

### Key Classes & Methods

**Class: SymbolExtractor**

```javascript
constructor(options = {})
  // options.logger — Logger (optional, defaults to silent)
  // options.metrics — Metrics collector (optional)
  // options.documentProvider — DocumentProvider (optional)
  // options.cacheSize — Cache size (default 100)
```

**Core Methods**:

1. **`async extractSymbols(filepath, options)`** — Main extraction API
   - Parameters:
     - `filepath` (string, required) — File to extract from
     - `options.symbolTable` (object) — Pre-parsed symbol table
     - `options.kind` (string) — Filter by kind ("class", "method", "property", etc.)
     - `options.scope` (string) — Filter by scope ("public", "private", "protected")
     - `options.searchPattern` (string|RegExp) — Filter by name pattern
     - `options.includeChildren` (boolean) — Include nested symbols (default: true)
   - Returns: `{ symbols, metadata, filepath }`
   - Cache: Checks cache first; caches parsed table on miss

2. **`async parseSymbolTable(symbolTableJson)`** — Parse & validate JSON
   - Accepts object or JSON string
   - Normalizes line/column numbers (0-based)
   - Validates required fields (name, kind, line, column, file)
   - Builds hierarchy tree (populates children arrays)
   - Returns: `{ symbols, symbolCount, fileCount }`
   - Throws: SymbolTableError, SymbolValidationError

3. **`getCacheStats()`** — Get cache metrics
   - Returns: `{ size, maxSize, entries }`

4. **`clearCache(filepath?)`** — Clear cache (all or specific)

5. **`dispose()`** — Cleanup and clear cache

**Error Classes**:

- **SymbolExtractionError** — Extraction/registration failures
  - `operationType` — "registration", "extraction", "parsing", "filtering"
  - `originalError` — Wrapped error

- **SymbolValidationError** — Field validation failures
  - `fieldName` — Which field failed ("name", "kind", "line", "column", "file")
  - `value` — Invalid value

- **SymbolTableError** — JSON parsing/structure failures
  - `operationType` — "parse", "validate", "normalize"
  - `jsonParseError` — Original JSON error

### Usage in Handlers

**Extract All Symbols**:
```javascript
const extractor = new SymbolExtractor({
  logger: context.logger,
  metrics: context.metrics
});

const result = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: incomingSymbolTableFromCSharp
});

// result = {
//   symbols: [
//     { name: 'MyClass', kind: 'class', line: 10, column: 0, file: '...', scope: 'public', children: [...] },
//     { name: 'MyMethod', kind: 'method', line: 15, column: 2, ... }
//   ],
//   metadata: { count: 2, byKind: { class: 1, method: 1 }, byScope: { public: 2 } },
//   filepath: 'MyClass.cs'
// }
```

**Filter by Kind (e.g., Only Methods)**:
```javascript
const methods = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: symbolTable,
  kind: 'method'
});
```

**Filter by Scope (e.g., Only Public)**:
```javascript
const publicSymbols = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: symbolTable,
  scope: 'public'
});
```

**Search by Name Pattern**:
```javascript
const matching = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: symbolTable,
  searchPattern: /^Test/i  // Case-insensitive regex
});
```

**Combine Filters**:
```javascript
const publicMethods = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: symbolTable,
  kind: 'method',
  scope: 'public',
  searchPattern: 'Get'
});
// Returns methods named 'Get*' that are public
```

### Response Examples

**Success Response (Multiple Symbols)**:
```javascript
{
  success: true,
  data: {
    symbols: [
      {
        name: 'Program',
        kind: 'class',
        line: 1,
        column: 0,
        file: 'C:\\project\\Program.cs',
        scope: 'public',
        documentation: 'Main application class',
        children: [
          {
            name: 'Main',
            kind: 'method',
            line: 5,
            column: 4,
            file: 'C:\\project\\Program.cs',
            scope: 'public',
            documentation: 'Entry point',
            children: []
          }
        ]
      }
    ],
    metadata: {
      count: 1,
      byKind: { class: 1 },
      byScope: { public: 1 },
      parseTime: 7,
      parsedAt: 1705335000000
    },
    filepath: 'C:\\project\\Program.cs'
  }
}
```

**Success Response (Filtered, No Results)**:
```javascript
{
  success: true,
  data: {
    symbols: [],
    metadata: {
      count: 0,
      byKind: {},
      byScope: {},
      parseTime: 1,
      parsedAt: 1705335000000
    },
    filepath: 'C:\\project\\Program.cs'
  }
}
```

**Error Response (Invalid Symbol Table)**:
```javascript
{
  success: false,
  error: "name: must be a non-empty string"
}
```

### Error Handling

**Validation Errors**:
```javascript
try {
  await extractor.extractSymbols('test.cs', {
    symbolTable: { symbols: [{ kind: 'class' }] }  // Missing 'name'
  });
} catch (error) {
  if (error instanceof SymbolValidationError) {
    console.error(`Field: ${error.fieldName}, Value: ${error.value}`);
  }
}
```

**Parse Errors**:
```javascript
try {
  await extractor.extractSymbols('test.cs', {
    symbolTable: 'invalid json {{'
  });
} catch (error) {
  if (error instanceof SymbolTableError) {
    console.error(`Operation: ${error.operationType}`);
    console.error(`JSON Error: ${error.jsonParseError?.message}`);
  }
}
```

### Performance Characteristics

- **Parse time**: 5–10ms per symbol table (includes validation + hierarchy building)
- **Cache hits**: <1ms (direct Map lookup + filter application)
- **Memory**: ~100 bytes overhead + symbol data size
- **LRU eviction**: Oldest table evicted when cache exceeds `cacheSize`
- **Improvement**: 80%+ latency reduction on repeated calls (second+ requests use cache)

### Caching Strategy

```javascript
// First call: parses and caches
const result1 = await extractor.extractSymbols('MyClass.cs', {
  symbolTable: largeSymbolTable
});
// Time: ~8ms

// Second call: uses cache (same filepath)
const result2 = await extractor.extractSymbols('MyClass.cs', {
  kind: 'method'  // No symbolTable provided
});
// Time: <1ms (cached table + filter)

// Third file: adds to cache
const result3 = await extractor.extractSymbols('AnotherClass.cs', {
  symbolTable: anotherSymbolTable
});
// Time: ~8ms

// Cache stats
const stats = extractor.getCacheStats();
// { size: 2, maxSize: 100, entries: [...] }
```

### Related Steps

- **Step 14** — Handler Dispatcher (routes messages)
- **Step 47** — Message Routing Middleware (integrates handler)
- **Step 50** — Get Editor State Handler (parallel handler)
- **Step 52** — Document Provider (optional context provider)
- **Step 54** — Diagnostics Collector (references symbols)
- **Step 55–59** — Search, Navigation, Completion (consume symbols)
- **Step 62** — Handler Type Definitions (SymbolInfo typedef)
- **Step 66** — Handler Registry (includes symbol extractor)
- **Step 68** — Handler Tests (search/navigation) — integration tests
- **Step 71** — Handler Registration (dispatcher registration)

### Testing

```javascript
import { describe, it } from 'mocha';
import { SymbolExtractor, SymbolValidationError } from '../lib/symbol-extractor.mjs';

describe('SymbolExtractor', () => {
  it('should parse valid symbol table', async () => {
    const extractor = new SymbolExtractor();
    const table = {
      symbols: [
        { name: 'MyClass', kind: 'class', line: 1, column: 0, file: 'test.cs', scope: 'public' },
        { name: 'MyMethod', kind: 'method', line: 5, column: 2, file: 'test.cs', scope: 'public', parent: 'MyClass' }
      ]
    };

    const result = await extractor.extractSymbols('test.cs', { symbolTable: table });
    assert.strictEqual(result.symbols.length, 2);
    assert.strictEqual(result.symbols[0].children.length, 1);
  });

  it('should filter by kind', async () => {
    const extractor = new SymbolExtractor();
    const table = {
      symbols: [
        { name: 'MyClass', kind: 'class', line: 1, column: 0, file: 'test.cs', scope: 'public' },
        { name: 'MyMethod', kind: 'method', line: 5, column: 2, file: 'test.cs', scope: 'public' }
      ]
    };

    const result = await extractor.extractSymbols('test.cs', {
      symbolTable: table,
      kind: 'method'
    });
    assert.strictEqual(result.symbols.length, 1);
    assert.strictEqual(result.symbols[0].name, 'MyMethod');
  });

  it('should cache parsed tables', async () => {
    const extractor = new SymbolExtractor();
    const table = { symbols: [{ name: 'Test', kind: 'class', line: 1, column: 0, file: 'test.cs' }] };

    // First call
    await extractor.extractSymbols('test.cs', { symbolTable: table });
    assert.ok(extractor._cache.has('test.cs'));

    // Second call reuses cache
    const result2 = await extractor.extractSymbols('test.cs', { kind: 'class' });
    assert.ok(result2.symbols.length >= 0);
  });

  it('should reject invalid symbol table', async () => {
    const extractor = new SymbolExtractor();
    const invalidTable = { symbols: [{ kind: 'class' }] }; // Missing 'name'

    try {
      await extractor.extractSymbols('test.cs', { symbolTable: invalidTable });
      assert.fail('Should have thrown');
    } catch (error) {
      assert.ok(error instanceof SymbolValidationError);
    }
  });
});
```

See `src/versions/v2.0.0/tests/symbol-extractor.test.mjs` for comprehensive test suite (21 tests, 100% coverage, 6 suites: initialization, parsing, filtering, queries, integration, handler export).

---

## Go-To-Definition Handler (Step 56)

### Overview

The **Go-To-Definition Handler** implements IDE symbol navigation (Ctrl+Click, F12 equivalent). It resolves symbol definitions by cursor position, returning the file/line/column of the symbol's declaration. Supports hierarchical navigation via symbol tables and graceful fallback to text-based search.

**Message Type**: `bridge:goToDefinition`  
**Input**: BridgeMessage with { filepath, line, column, searchScope? }  
**Output**: BridgeResponse with { location: DefinitionLocation|null, alternatives?: DefinitionLocation[] }  
**Dependencies**: SymbolExtractor (Step 53) — required; DocumentProvider (Step 52) — optional  
**Performance**: File-scoped <50ms; project-scoped <200ms; workspace-scoped <500ms  
**Error Classes**: `DefinitionError`, `DefinitionValidationError`

### Message Format

```javascript
// Request: IDE sends cursor position
{
  messageType: 'bridge:goToDefinition',
  messageId: 'msg-uuid-1',
  data: {
    filepath: '/path/to/file.cs',
    line: 10,                        // 0-based, cursor line
    column: 15,                      // 0-based, cursor column
    searchScope: 'file'              // optional: 'file' | 'project' | 'workspace'
  }
}

// Response: Handler returns definition location
{
  success: true,
  data: {
    location: {
      file: '/path/to/file.cs',
      line: 5,
      column: 0,
      name: 'MyClass',               // Symbol name
      kind: 'class'                  // Symbol kind
    },
    alternatives: [                  // optional: overloads, base implementations
      { file: '...', line: 15, column: 4, name: 'MyClass', kind: 'class' },
      { file: '...', line: 25, column: 4, name: 'MyClass', kind: 'class' }
    ]
  }
}

// Error response
{
  success: false,
  error: 'Validation: filepath – must be a non-empty string'
}
```

### Resolution Pipeline

```
IDE Cursor (filepath, line, column)
    ↓
[1] Validate input (bounds, types)
    ↓
[2] Query SymbolExtractor for symbol table
    ↓
[3] Binary search symbol tree for innermost symbol at cursor
    ↓
[4] If symbol found → resolve definition location
    ↓
[5] Find alternatives (overloads, base implementations)
    ↓
[6] Return location + alternatives
    ↓
[7] If not found & searchScope ≠ 'file' → fallback text search
```

### Input Validation

- `filepath` (required): Non-empty string, must be valid path
- `line` (required): Non-negative integer, 0-based
- `column` (required): Non-negative integer, 0-based
- `searchScope` (optional): 'file' | 'project' | 'workspace', default 'file'

### Error Codes

| Error | Cause | Example |
|-------|-------|---------|
| `Validation: filepath – ...` | Missing or invalid filepath | filepath is empty or null |
| `Validation: line – ...` | Invalid line number | line < 0 |
| `Validation: column – ...` | Invalid column number | column < 0 |
| `Validation: searchScope – ...` | Invalid scope | searchScope = 'invalid' |
| `extraction: Failed to extract symbols` | SymbolExtractor error | Corrupted symbol table JSON |
| `Internal error: ...` | Unexpected exception | Uncaught error in handler |

### Usage Example

```javascript
import { createGoToDefinitionHandler } from '../lib/go-to-definition-handler.mjs';
import { SymbolExtractor } from '../lib/symbol-extractor.mjs';
import { DocumentProvider } from '../lib/document-provider.mjs';

// Initialize dependencies
const symbolExtractor = new SymbolExtractor();
const documentProvider = new DocumentProvider();

// Create handler
const handler = createGoToDefinitionHandler({
  symbolExtractor,
  documentProvider,
  logger: logger,
  metrics: metrics
});

// Register with dispatcher
dispatcher.register('bridge:goToDefinition', handler);

// IDE calls handler via stdio
const message = {
  messageType: 'bridge:goToDefinition',
  messageId: 'msg-1',
  data: { filepath: '/src/MyClass.cs', line: 10, column: 5 }
};

const response = await handler(message, context);
console.log(response.data.location);
// { file: '/src/MyClass.cs', line: 1, column: 0, name: 'MyClass', kind: 'class' }
```

### Architecture

```
┌──────────────────┐
│ IDE Cursor Pos   │ (line, column in open file)
└────────┬─────────┘
         │
         ↓
┌──────────────────────────────────────┐
│ Go-To-Definition Handler             │
├──────────────────────────────────────┤
│ 1. validateGoToDefinitionInput()     │ — Check bounds, types
│ 2. SymbolExtractor.extractSymbols()  │ — Get symbol table
│ 3. extractSymbolAtCursor()           │ — Binary search tree
│ 4. resolveDefinitionLocation()       │ — Format response
│ 5. findAlternativeDefinitions()      │ — Collect overloads
└────────┬─────────────────────────────┘
         │
         ↓
┌──────────────────┐
│ Definition Loc   │ { file, line, col, name, kind }
└──────────────────┘
```

### Related Steps

- **Step 14** — Handler Dispatcher (routes messages)
- **Step 47** — Message Routing Middleware (integrates handler)
- **Step 52** — Document Provider (fallback search)
- **Step 53** — Symbol Extractor (main source)
- **Step 54** — Diagnostics Collector (parallel infrastructure)
- **Step 55** — Search Handler (similar pattern)
- **Step 57** — Find References Handler (complementary navigation)
- **Step 62** — Handler Type Definitions (DefinitionLocation typedef)
- **Step 68** — Handler Tests (search/navigation) — integration tests
- **Step 71** — Handler Registration (dispatcher registration)

### Performance Characteristics

| Operation | Scope | Typical Time | Notes |
|-----------|-------|--------------|-------|
| Symbol lookup | File | <50ms | Cache hit from SymbolExtractor |
| Definition resolve | File | <10ms | O(log n) binary search |
| Alternatives (same file) | File | <20ms | O(n) linear scan of tree |
| Cross-file fallback | Project | <200ms | Text-based search in open docs |
| Workspace fallback | Workspace | <500ms | Search all open documents |

### Testing

```javascript
import { describe, it } from 'vitest';
import { createGoToDefinitionHandler } from '../lib/go-to-definition-handler.mjs';
import {
  getNestedSymbolTable,
  getOverloadedMethodsTable,
  getValidGoToDefinitionMessage
} from './mocks/go-to-definition-fixtures.mjs';

describe('Go-To-Definition Handler', () => {
  it('should resolve nested method definition', async () => {
    const symbolExtractor = {
      extractSymbols: async () => ({
        success: true,
        data: getNestedSymbolTable()
      })
    };

    const handler = createGoToDefinitionHandler({ symbolExtractor });
    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-1',
      data: { filepath: '/path/to/file.cs', line: 12, column: 5 }
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location.name).toBe('DoSomething');
    expect(response.data.location.kind).toBe('method');
  });

  it('should collect overload alternatives', async () => {
    const symbolExtractor = {
      extractSymbols: async () => ({
        success: true,
        data: getOverloadedMethodsTable()
      })
    };

    const handler = createGoToDefinitionHandler({ symbolExtractor });
    const message = getValidGoToDefinitionMessage();

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.alternatives).toBeDefined();
    expect(response.data.alternatives.length).toBeGreaterThan(0);
  });

  it('should return null for symbol not at cursor', async () => {
    const symbolExtractor = {
      extractSymbols: async () => ({
        success: true,
        data: { symbols: [] }
      })
    };

    const handler = createGoToDefinitionHandler({ symbolExtractor });
    const message = getValidGoToDefinitionMessage();

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeNull();
  });

  it('should wrap validation errors', async () => {
    const symbolExtractor = {
      extractSymbols: async () => ({ success: true, data: {} })
    };

    const handler = createGoToDefinitionHandler({ symbolExtractor });
    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-1',
      data: { line: 5, column: 10 } // Missing filepath
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toMatch(/filepath/i);
  });
});
```

See `src/versions/v2.0.0/tests/go-to-definition-handler.test.mjs` for comprehensive test suite (20 tests, 6 suites: validation, symbol extraction, resolution, fallbacks, error handling, edge cases).

---

## Code-Completion Handler (Step 58)

### Overview

The **codeCompletion handler** is a stateless query handler that generates intelligent code completion suggestions at a cursor position. It queries the `DocumentProvider` (Step 52) for the active document and the `SymbolExtractor` (Step 53) to retrieve available symbols at the cursor position. Symbols are filtered by accessibility scope, ranked by relevance, and mapped to `CompletionItem[]` format.

**Message Type**: `bridge:getCompletion`  
**Input**: BridgeMessage with `{ file: string, line: number, column: number }`  
**Output**: BridgeResponse with CompletionItem[] data  
**Dependencies**: DocumentProvider (Step 52), SymbolExtractor (Step 53)

### Architecture

```
[Continue/IDE] sends bridge:getCompletion request { file, line, column }
  ↓
[dispatcher] routes to codeCompletionHandler
  ↓
[handler] validates input (types, non-negative bounds)
  ↓
[handler] queries DocumentProvider for active document
  ↓ (graceful if document missing — returns empty completion list)
[handler] calls SymbolExtractor.extractSymbols(file, line, column)
  ↓ (graceful if extraction fails — returns partial results)
[handler] filters symbols by accessibility scope (public, imported, keywords)
  ↓
[handler] ranks by relevance (distance from cursor, type, alphabetical)
  ↓
[handler] maps each symbol to CompletionItem typedef
  ↓
[dispatcher] wraps in BridgeResponse { success: true, data: CompletionItem[] }
  ↓
[core-server] sends response via stdio
```

### CompletionItem Typedef

```javascript
{
  label: "calculateSum",                             // Display text in UI
  kind: "Function",                                  // Completion kind (Class, Method, Property, etc.)
  detail: "(a: number, b: number) => number",        // Type signature or additional info (optional)
  documentation: "Adds two numbers and returns sum", // Docstring/comment (optional)
  insertText: "calculateSum($1, $2)",                // Text to insert (optional, default: label)
  sortText: "Function_calculateSum"                  // Sort priority (optional)
}
```

### Error Handling

The handler implements graceful degradation:

- **Missing document**: Returns empty `CompletionItem[]` (valid state, not an error)
- **Symbol extraction error**: Returns partial results (available symbols only)
- **No symbols at position**: Returns empty `CompletionItem[]` (valid state)
- **Invalid input (missing file, negative line/column)**: Returns `{ success: false, error: message }`
- **Missing provider/extractor in context**: Returns `{ success: false, error: message }`

### Usage

```javascript
import { createCodeCompletionHandler } from '../lib/code-completion-handler.mjs';

// Create handler with injected dependencies
const handler = createCodeCompletionHandler(dispatcher, {
  logger: bridgeLogger,
  metrics: bridgeMetrics
});

// Register with dispatcher (Step 71)
dispatcher.register('bridge:getCompletion', handler);

// Handler receives message and context
const message = {
  messageType: 'bridge:getCompletion',
  messageId: 'req-1',
  data: {
    file: '/home/user/src/main.js',
    line: 10,
    column: 5
  }
};

const context = {
  documentProvider: provider,
  symbolExtractor: extractor,
  logger: bridgeLogger,
  metrics: bridgeMetrics
};

const response = await handler(message, context);

// Expected response (success):
{
  success: true,
  data: [
    {
      label: "calculateSum",
      kind: "Function",
      detail: "(a: number, b: number) => number",
      documentation: "Adds two numbers",
      insertText: "calculateSum($1, $2)",
      sortText: "Function_calculateSum"
    },
    {
      label: "config",
      kind: "Variable",
      detail: "Object",
      documentation: "Global configuration",
      insertText: "config",
      sortText: "Variable_config"
    }
  ]
}

// Expected response (error):
{
  success: false,
  error: "file must be a non-empty string"
}
```

### Implementation Details

#### Symbol Filtering

Symbols are filtered by accessibility:
- **Included**: Public symbols, imported modules, language keywords
- **Excluded**: Private symbols (marked with `isPrivate` or kind `Private`)

#### Symbol Ranking

Symbols are ranked by relevance (in priority order):
1. **Distance from cursor**: Symbols closer to cursor position rank higher (max 1000 points, −10 per line distance)
2. **Type**: Locals > Imported > Keywords (500, 300, 100 points respectively)
3. **Frequency**: Symbols used multiple times rank higher (50 points per use)
4. **Alphabetical**: Secondary sort for equal scores

Example ranking:
```javascript
// Given cursor at line 10:
const localVar = 5;           // line 10, kind: Local       → score ~1500 (near + local)
const importedFunc = import_1; // line 2, kind: Imported    → score ~1300 (far + imported)
const keyword = 'async';       // kind: Keyword             → score ~100 (keyword)
```

#### Kind Mapping

Symbols are mapped from internal kinds to CompletionItem kinds for UI:

| Symbol Kind | CompletionItem Kind |
|---|---|
| Class | Class |
| Method | Method |
| Function | Function |
| Property | Property |
| Variable | Variable |
| Local | Variable |
| Keyword | Keyword |
| Interface | Interface |
| Enum | Enum |
| Module, Namespace, Package, Import | Module |
| Unknown | Text |

### Testing

See `src/versions/v2.0.0/tests/code-completion-handler.test.mjs` for comprehensive test suite (22 tests, 6 suites):

| Suite | Tests | Coverage |
|---|---|---|
| Initialization | 3 | Valid/invalid dispatcher, dependency injection |
| Document Query | 4 | Get active document, missing files, errors, metadata |
| Symbol Extraction | 5 | Extract symbols, empty results, errors, options passing, missing properties |
| Completion Filtering | 4 | Filter private symbols, include public, CompletionItem format, kind mapping |
| Error Handling | 5 | Invalid file, negative line/column, missing provider/extractor, null data |
| Edge Cases | 3 | Empty document, position out-of-bounds, ranking by relevance |
| Metrics Recording | 2 | Record success/error metrics with latency |

---

## Find-References Handler (Step 57)

The **Find-References Handler** locates all references to a symbol within IDE context. It complements Step 56 (go-to-definition) by providing reverse navigation: instead of "go to definition," this handler answers "show me all uses of this symbol." Returns rich reference metadata (location, kind: declaration/read/write/import) for refactoring tools and AI comprehension.

### Architecture

Find-references aggregates symbols across three scopes:

| Scope | Behavior | Typical Performance |
|-------|----------|---------------------|
| **file** | Query current SymbolExtractor table only | <50ms |
| **project** | Combine file scope + text search across open documents | <250ms |
| **workspace** | Same as project (Continue has no boundary) | <750ms |

**Aggregation Flow**:
1. Extract symbol at cursor via SymbolExtractor
2. Search SymbolExtractor table for all matching names → ReferenceLocation[]
3. If project/workspace scope, query DocumentProvider for cross-file text matches
4. Deduplicate locations, format with kind (declaration, read, write, import)
5. Truncate if > 2000 references (set `truncated: true`)

### Handler Signature

```javascript
import { createFindReferencesHandler } from './lib/find-references-handler.mjs';

const handler = createFindReferencesHandler({
  symbolExtractor,    // Required: SymbolExtractor (Step 53)
  documentProvider,   // Optional: DocumentProvider (Step 52)
  logger,             // Optional: Logger instance
  metrics             // Optional: Metrics collector
});

// Invoke
const response = await handler(
  {
    messageType: 'bridge:findReferences',
    messageId: 'msg-1',
    data: {
      filepath: '/path/to/file.cs',
      line: 5,
      column: 10,
      searchScope: 'project'  // 'file' | 'project' | 'workspace' (default: 'file')
    }
  },
  { logger, metrics, server }  // Dispatch context
);

// Response
// {
//   success: true,
//   data: {
//     references: [
//       { file: '/main.cs', line: 20, column: 5, text: 'MySymbol', kind: 'read' },
//       { file: '/utils.cs', line: 15, column: 3, text: 'MySymbol', kind: 'write' },
//       ...
//     ],
//     totalCount: 42,
//     truncated: false  // undefined if not truncated
//   }
// }
```

### Error Handling

The handler wraps errors into structured exceptions:

**ReferenceValidationError**
- Thrown when input fails validation (missing/invalid filepath, negative line/column, invalid scope)
- Returns `{ success: false, error: 'Validation: fieldName – message' }`

**ReferenceError**
- Thrown when execution fails (symbol extraction, reference aggregation, I/O)
- Returns `{ success: false, error: 'operationType: message' }`
- operationType: 'extraction', 'aggregation', 'search', 'io'

### Related Steps

- **Step 14** — Handler Dispatcher (routes messages)
- **Step 47** — Message Routing Middleware (integrates handler)
- **Step 52** — Document Provider (fallback search for project/workspace scopes)
- **Step 53** — Symbol Extractor (main reference source)
- **Step 54** — Diagnostics Collector (parallel infrastructure)
- **Step 55** — Search Handler (similar pattern)
- **Step 56** — Go-To-Definition Handler (complementary navigation)
- **Step 62** — Handler Type Definitions (ReferenceLocation typedef)
- **Step 68** — Handler Tests (search/navigation) — integration tests
- **Step 71** — Handler Registration (dispatcher registration)

### Performance Characteristics

| Operation | Scope | Typical Time | Notes |
|-----------|-------|--------------|-------|
| Symbol lookup | File | <50ms | Cache hit from SymbolExtractor |
| Reference aggregation | File | <50ms | O(n) linear scan of symbol tree |
| Cross-file search | Project | <200ms | Text-based search in open docs |
| Deduplication | Project | <50ms | Set-based dedup by location |
| Workspace scope | Workspace | <750ms | All open documents + symbol table |

### Testing

The find-references handler includes 28 comprehensive test cases (7 suites):

```javascript
import { describe, it } from 'vitest';
import { createFindReferencesHandler } from '../lib/find-references-handler.mjs';

describe('find-references-handler: Reference Aggregation', () => {
  it('should aggregate references from file scope', async () => {
    const symbolExtractor = {
      extractSymbols: async () => ({
        success: true,
        data: {
          symbols: [
            {
              name: 'MyFunc',
              kind: 'method',
              file: '/file.cs',
              line: 0, column: 0,
              endLine: 1, endColumn: 1,
              children: []
            },
            {
              name: 'MyFunc',
              kind: 'reference',
              file: '/file.cs',
              line: 10, column: 2,
              endLine: 10, endColumn: 8,
              children: []
            }
          ]
        }
      })
    };

    const handler = createFindReferencesHandler({ symbolExtractor });
    const response = await handler(
      {
        messageType: 'bridge:findReferences',
        messageId: 'msg-1',
        data: { filepath: '/file.cs', line: 0, column: 0, searchScope: 'file' }
      },
      {}
    );

    expect(response.success).toBe(true);
    expect(response.data.references.length).toBeGreaterThanOrEqual(2);
  });
});
```

See `src/versions/v2.0.0/tests/find-references-handler.test.mjs` for full test suite (28 tests, 7 suites: validation, symbol extraction, aggregation, formatting, fallback logic, error handling, edge cases).

### Reference Kind Classification

The handler annotates each reference with a **kind** to help refactoring tools understand the usage:

| Kind | Example | Notes |
|------|---------|-------|
| `declaration` | `class MyClass { }` | Symbol definition site |
| `read` | `var x = myVar;` | Read-only usage |
| `write` | `myVar = 5;` | Assignment or mutation |
| `import` | `using MyNamespace;` | Import/using statement |

### Integration Example

```javascript
// In core-server.js handler dispatcher (Step 71)
import { createFindReferencesHandler } from './lib/find-references-handler.mjs';

const findReferencesHandler = createFindReferencesHandler({
  symbolExtractor: globalSymbolExtractor,     // Shared from Step 53
  documentProvider: globalDocumentProvider,   // Shared from Step 52
  logger: bridgeLogger,                       // Shared logger
  metrics: bridgeMetrics                      // Shared metrics
});

// Register handler
dispatcher.on('bridge:findReferences', findReferencesHandler);
```

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

### Step 71: Register All Handlers with Dispatcher

**Problem**: Handler registry exists but is not automatically integrated into bridge startup. Handlers need explicit registration during server initialization.

**Solution**: Create a registration orchestrator that consumes the static registry and injects handler registration into the `BridgeServer.start()` lifecycle—after npm validation but before spawning Continue.

---

#### Architecture Flow

```
BridgeServer.start()
    ↓
  npm validation (Step 12) ✓
    ↓
  Handler registration (Step 71) ← NEW
    ├─ Load HANDLER_REGISTRY
    ├─ Validate all handlers
    ├─ Instantiate factory handlers
    ├─ Register with dispatcher
    └─ Log & record metrics
    ↓
  Spawn Continue process
    ↓
  Setup signal handlers
    ↓
  Bridge ready for messages
```

---

#### Registration Orchestrator

**Location**: `src/versions/v2.0.0/lib/register-handlers.mjs`

**Core Function**:
```javascript
/**
 * Register all handlers with the dispatcher.
 * Called during BridgeServer.start() after npm validation, before spawning Continue.
 *
 * @param {Object} server - BridgeServer instance (with registerHandler, logger, metrics)
 * @param {Object} options - Optional configuration
 *   @param {boolean} options.throwOnError - If true, throw on first error (default: false)
 *   @param {boolean} options.silent - If true, suppress logging (default: false)
 * @returns {Promise<RegistrationResult>} Result with count, success, errors, duration, details
 */
export async function registerAllHandlersWithDispatcher(server, options = {}) {
  // 1. Validate server and registry
  // 2. Import HANDLER_REGISTRY from handler-registry.mjs
  // 3. For each handler:
  //    a. Instantiate if factory (isFactory=true)
  //    b. Register via server.registerHandler(messageType, handler)
  //    c. Log at debug level
  //    d. Track success/error
  // 4. Log final result at info level
  // 5. Record metrics (if available)
  // 6. Return RegistrationResult
}
```

**RegistrationResult Type**:
```javascript
{
  count: number;              // Number of handlers successfully registered
  success: boolean;           // Whether all handlers registered
  errors: Error[];            // Non-fatal errors (logged only)
  duration: number;           // Time in milliseconds
  details: [
    {
      messageType: string;    // e.g., "bridge:bootstrap"
      registered: boolean;    // Whether this handler registered
      error: string | null;   // Error message (if registered=false)
      isFactory: boolean;     // Whether handler was instantiated
    }
  ]
}
```

---

#### Handler Patterns: Static vs Factory

**Static Handlers** (registered as-is):
```javascript
// bootstrap-handler.js
export const bootstrapHandler = async (message, context) => ({
  success: true,
  data: { version: '2.0.0' }
});

// Registry entry:
{
  messageType: 'bridge:bootstrap',
  handler: bootstrapHandler,
  isFactory: false,  // Static
  ...
}
```

**Factory Handlers** (instantiated during registration):
```javascript
// go-to-definition-handler.mjs
export function createGoToDefinitionHandler(context = {}) {
  // Setup handler-specific state (collector, logger)
  const { symbolExtractor, logger } = context;

  return async (message, context) => ({
    success: true,
    data: { location: '...' }
  });
}

// Registry entry:
{
  messageType: 'bridge:goToDefinition',
  handler: createGoToDefinitionHandler,
  isFactory: true,  // Factory
  ...
}
```

**Registration Process**:
- **Static**: `register(messageType, handler)` → directly register
- **Factory**: `register(messageType, handler())` → instantiate first, then register

---

#### Integration with BridgeServer

**In `core-server.js` `start()` method**:
```javascript
// Step 71: Register all handlers with dispatcher (before spawning Continue)
const registrationResult = await registerAllHandlersWithDispatcher(this);
if (!registrationResult.success) {
  this.logger.warn('Handler registration completed with errors', {
    count: registrationResult.count,
    errorCount: registrationResult.errors.length,
    duration: registrationResult.duration,
  });
}
```

**Timing**:
1. ✅ After: npm package validation (Step 12)
2. ✅ Before: Continue process spawn
3. ✅ Before: Signal handler setup

**Error Handling**:
- Validation errors (invalid server/registry) → throw, halt startup
- Registration errors (duplicate handler) → log warning, continue (non-fatal)
- Instantiation errors (factory fails) → log warning, continue (non-fatal)

---

#### Error Handling

**HandlerRegistrationError** — Custom error with operation context:
```javascript
class HandlerRegistrationError extends Error {
  name: 'HandlerRegistrationError';
  operation: 'validation' | 'registry_load' | 'instantiation' | 'registration';
  details: Object;  // Context-specific details
  timestamp: string;  // ISO string
}
```

**Operation Types**:
- `validation`: Server or registry invalid (throws, halts startup)
- `registry_load`: Failed to load HANDLER_REGISTRY (throws, halts startup)
- `instantiation`: Factory handler call failed (logged, non-fatal)
- `registration`: Duplicate handler or dispatcher error (logged, non-fatal)

---

#### Logging & Observability

**Debug Level** (per handler):
```
[2024-01-15T10:30:45.123Z] [DEBUG] Registered handler: bridge:getEditorState {
  "stabilityTier": "core",
  "timeoutPolicy": "fast",
  "isFactory": false
}
```

**Info Level** (summary):
```
[2024-01-15T10:30:45.145Z] [INFO] Handler registration complete: 10/10 handlers registered in 22ms {
  "success": true,
  "errorCount": 0
}
```

**Warn Level** (errors):
```
[2024-01-15T10:30:45.150Z] [WARN] Failed to register handler bridge:customHandler: Factory instantiation failed {
  "operation": "instantiation",
  "details": {
    "messageType": "bridge:customHandler",
    "originalError": "Context missing required field: symbolExtractor"
  }
}
```

---

#### Metrics Recording

**If metrics collector available**:
```javascript
server.metrics.record('handler_registration_count', 10);        // handlers registered
server.metrics.record('handler_registration_duration', 22);     // milliseconds
server.metrics.record('handler_registration_errors', 0);        // errors encountered
```

**Metrics are optional** — registration continues even if metrics.record() fails.

---

#### Testing

**Test Suite**: `src/versions/v2.0.0/tests/register-handlers.test.mjs` (18+ tests)

**Test Suites**:
1. **Happy Path** (3 tests) — All 10 handlers register, logging works
2. **Factory Instantiation** (3 tests) — Factories instantiated, static handlers as-is
3. **Error Handling** (4 tests) — Invalid server, missing registry, duplicates
4. **Logging & Metrics** (3 tests) — Debug/info logs, metrics recording
5. **Performance** (2 tests) — Registration <50ms, non-blocking
6. **Idempotency** (2 tests) — Duplicate detection, cleanup/re-register
7. **Integration** (1+ tests) — BridgeServer lifecycle, handler diagnostics

**Running Tests**:
```bash
# All tests
npx mocha src/versions/v2.0.0/tests/register-handlers.test.mjs --timeout 15000

# Specific suite
npx mocha src/versions/v2.0.0/tests/register-handlers.test.mjs --grep "Happy Path"

# Performance test
npx mocha src/versions/v2.0.0/tests/register-handlers.test.mjs --grep "less than 50ms"
```

---

#### Dependencies

**Blocks**:
- Steps 72–75 (middleware, integration tests) — require handlers registered

**Enabled by**:
- Steps 50–61 (all handlers implemented)
- Step 66 (handler registry created)
- Step 14 (dispatcher ready)
- Step 45 (server lifecycle)

**No new npm dependencies** — Uses only Node.js built-ins and existing imports.

---

#### Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| "Handler already registered" | Second registration attempt | Clear dispatcher and retry (testing only) |
| "Server missing registerHandler" | Invalid server instance | Pass `BridgeServer` instance, not mock |
| "Factory handler returns non-function" | Factory implementation error | Verify factory returns async function |
| "Registration timeout" | Very slow factory | Check factory for blocking I/O |
| Registration logs not showing | `silent: true` option set | Pass `silent: false` or omit option |

---

## Message Logging Middleware (Step 72)

### Problem

Handler execution lacks structured logging and performance monitoring. Without message-level telemetry, debugging is difficult and performance bottlenecks are invisible. You need to capture inbound/outbound messages, track latency per message type, and record metrics for monitoring.

### Solution

Create a message logging middleware that wraps the MiddlewareChain (Step 47) to capture inbound/outbound messages, track latency per message type, and integrate with IBridgeLogger + IBridgeTelemetryCollector for structured logging and metrics aggregation.

---

### Architecture

```
WebView Message
      ↓
BridgeMessage (normalized by Step 63 Protocol Adapter)
      ↓
MessageLoggingMiddleware.executeWithLogging()
  ├─ Log inbound: messageType, messageId, timestamp, size
  ├─ MiddlewareChain.execute()
  │   └─ Dispatcher → Handler
  ├─ Log outbound: status, latency (ms), size, handler name
  ├─ Update latency histogram (fast/normal/slow)
  └─ Record metrics
      ↓
IBridgeLogger (structured log capture)
IBridgeTelemetryCollector (metrics aggregation)
```

---

### Core Responsibilities

| Responsibility | Implementation | Details |
|---|---|---|
| **Inbound Logging** | Log message metadata on arrival | messageType, messageId, timestamp, payload size |
| **Outbound Logging** | Log response status & latency | success/error status, latency (ms), response size, handler name |
| **Latency Tracking** | Categorize per message type | fast (<50ms), normal (50-500ms), slow (>500ms) |
| **Metrics Aggregation** | Compute performance statistics | Total messages, error rate, avg/p95/p99 latency |
| **Error Logging** | Capture handler errors separately | Error type, context, frequency |
| **Configuration** | Customizable logging behavior | Sample rate, detailed logging, payload inclusion |
| **Graceful Degradation** | No-op if logger/telemetry null | Non-cascading failure when dependencies absent |

---

### Usage Example

**Basic Initialization**:
```javascript
import { MessageLoggingMiddleware } from './lib/message-logging-middleware.mjs';
import { IBridgeLogger } from './lib/logger.mjs';
import { IBridgeTelemetryCollector } from './lib/telemetry.mjs';

const logger = new IBridgeLogger();
const metrics = new IBridgeTelemetryCollector();
const middlewareChain = /* from Step 47 */;

const logging = new MessageLoggingMiddleware({
  middlewareChain,
  logger,
  metrics,
  config: {
    enableDetailedLogging: false,
    includePayloads: false,
    sampleRate: 1.0,      // Log 100% of messages
    metricsWindow: 1000   // Track last 1000 messages for p95/p99
  }
});
```

**Execute Message**:
```javascript
const message = {
  messageType: 'bridge:getEditorState',
  messageId: 'msg-uuid',
  data: { file: '/path/to/file.ts' }
};

try {
  const result = await logging.executeWithLogging(
    message,
    dispatcher,
    { logger, metrics }
  );
  console.log('Result:', result);
} catch (err) {
  console.error('Message failed:', err);
}
```

**Query Metrics**:
```javascript
const snapshot = logging.getMetrics();
console.log(`
  Total messages: ${snapshot.inbound.total}
  Success rate: ${100 - snapshot.summary.errorRate}%
  Avg latency: ${snapshot.outbound.averageLatency}ms
  p95 latency: ${snapshot.outbound.p95Latency}ms
  p99 latency: ${snapshot.outbound.p99Latency}ms
  Latency breakdown:
    - Fast (<50ms): ${snapshot.latency.fast}
    - Normal (50-500ms): ${snapshot.latency.normal}
    - Slow (>500ms): ${snapshot.latency.slow}
`);
```

---

### Configuration Reference

| Option | Type | Default | Purpose | Example |
|--------|------|---------|---------|---------|
| `enableDetailedLogging` | boolean | false | Log full payloads in all messages | `true` for debugging, `false` for production |
| `includePayloads` | boolean | false | Include message data in structured logs | `true` to see request/response bodies |
| `sampleRate` | 0-1 | 1.0 | Log every Nth message (0.1 = 10%) | `0.1` to reduce logging I/O in high-throughput |
| `metricsWindow` | number | 1000 | Bounded history for p95/p99 calculation | `1000` = track last 1000 messages |

---

### Metrics Output Structure

```javascript
{
  inbound: {
    total: 150,
    byType: {
      'bridge:getEditorState': 50,
      'bridge:search': 40,
      'bridge:hover': 60
    }
  },
  outbound: {
    successCount: 148,
    errorCount: 2,
    averageLatency: 125.5,      // milliseconds
    p95Latency: 450,            // 95th percentile
    p99Latency: 580             // 99th percentile
  },
  latency: {
    fast: 60,       // < 50ms
    normal: 82,     // 50-500ms
    slow: 8         // > 500ms
  },
  errors: {
    total: 2,
    byType: {
      'TimeoutError': 1,
      'ValidationError': 1
    }
  },
  summary: {
    errorRate: 1.33,             // percentage
    avgLatencyCategory: 'normal'  // 'fast'|'normal'|'slow'
  }
}
```

---

### Integration with Other Steps

| Step | Relationship | Usage |
|------|--------------|-------|
| **Step 47** | MiddlewareChain foundation | `executeWithLogging()` wraps chain execution |
| **Step 63** | BridgeProtocolAdapter | Logs normalized BridgeMessage contracts |
| **Step 64** | TimeoutManager | Can consume latency metrics from getMetrics() |
| **Step 71** | Handler registration (blocker consumer) | Dispatcher uses logging middleware for all message execution |
| **Step 73** | Request/response validation | Validation middleware follows logging in chain |
| **Step 74** | Error recovery middleware | Error recovery follows in chain |
| **Step 75** | WebView integration tests | E2E tests verify logging + handler execution |

---

### Error Handling

**LoggingMiddlewareError** — Custom exception with operation context:
```javascript
class LoggingMiddlewareError extends Error {
  operationType: 'inboundLogging' | 'outboundLogging' | 'metricsAggregation';
  originalError?: Error;  // Root cause
}
```

**Graceful Degradation**:
- If `logger` is null → no-op (no logs)
- If `metrics` is null → no-op (no metrics)
- If both null → middleware still executes chain unaffected
- If logger.info() throws → caught, error logged to fallback

**Example**:
```javascript
const middleware = new MessageLoggingMiddleware({
  middlewareChain: chain,
  logger: null,    // No logging
  metrics: null    // No metrics
});

// Still executes chain normally
const result = await middleware.executeWithLogging(message, dispatcher);
```

---

### Performance Considerations

| Aspect | Impact | Mitigation |
|--------|--------|-----------|
| **Latency overhead** | <1ms per message | Histogram calculation is O(1); logging is async-safe |
| **Memory usage** | Bounded by metricsWindow | Default 1000 messages ≈ 50KB; configurable |
| **Sampling** | Reduces logging I/O | `sampleRate: 0.1` → log 10% of messages |
| **Detailed logging** | Increases I/O | Disabled by default; enable only for debugging |

---

### Testing

**Test Suite**: `src/versions/v2.0.0/tests/message-logging-middleware.test.mjs` (22 tests)

**Test Coverage**:
- **Initialization** (3 tests) — With/without logger/metrics, config validation
- **Inbound Logging** (4 tests) — Message metadata capture, graceful degradation
- **Outbound Logging** (4 tests) — Success/error logging, latency capture
- **Latency Tracking** (4 tests) — Histogram categorization, aggregation
- **Error Logging** (4 tests) — Handler errors, error rate tracking
- **Metrics & Cleanup** (3 tests) — Aggregation accuracy, metric reset

**Running Tests**:
```bash
# All tests
npx mocha src/versions/v2.0.0/tests/message-logging-middleware.test.mjs --timeout 10000

# Specific suite
npx mocha src/versions/v2.0.0/tests/message-logging-middleware.test.mjs --grep "Inbound Logging"

# With reporter
npx mocha src/versions/v2.0.0/tests/message-logging-middleware.test.mjs --reporter json --output test-results.json
```

**Expected Results**:
- 22/22 tests passing
- Execution time: ~500ms
- No external npm dependencies

---

### Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| "middlewareChain is required" | Missing constructor param | Pass middlewareChain from Step 47 |
| No logs appearing | Sample rate too low or logger null | Check sampleRate config, verify logger instance |
| Metrics all zeros | executeWithLogging() not called | Verify middleware is used in message flow |
| p95/p99 not changing | Not enough messages | Increase metricsWindow or run more tests |
| Memory growing unbounded | metricsWindow too large | Reduce metricsWindow config value |
| Errors not recorded | Logger failing silently | Add try-catch in _logError(); check logger implementation |

---

### File References

- **Implementation**: `src/versions/v2.0.0/lib/message-logging-middleware.mjs` (~450 lines)
- **Tests**: `src/versions/v2.0.0/tests/message-logging-middleware.test.mjs` (~550 lines)
- **Related**: Step 47 (MiddlewareChain), Step 63 (Protocol Adapter), Step 64 (TimeoutManager)

---

## Error Recovery Middleware (Step 74)

### Purpose

The **Error Recovery Middleware** is the final layer in the MiddlewareChain (Step 47) that catches and gracefully handles all failures from validation (Step 73), logging (Step 72), timeouts (Step 64), and handler dispatch (Step 14/71).

Key responsibilities:
- **Catch all errors** — Converts validation, timeout, dispatcher, and unexpected exceptions to JSON-RPC error responses
- **Never crash** — Fail-soft design ensures middleware always emits error response (never throws)
- **Recover transient errors** — Retries TimeoutError up to 3x with exponential backoff; never retries validation or handler errors
- **Rollback state** — Optional handler-based state rollback on failure (fail-soft if not supported)
- **Record metrics** — Error rate tracking, histogram by type, recovery success rate
- **Alert on threshold** — Triggers alert when error rate exceeds 1% over 5-second window
- **Correlate errors** — All logs and metrics include messageId for end-to-end tracing

### Architecture

**Error Flow Through Middleware Stack**:
```
Message → ValidationHook (Step 73)
  ├─ ValidationError thrown
  └─ Caught by ErrorRecoveryHook → emit -32600 error response (stop)

Message → LoggingMiddleware (Step 72)
  ├─ LoggingError thrown (non-blocking)
  └─ Caught by ErrorRecoveryHook → log and continue

Message → Dispatcher (Step 14/71)
  ├─ TimeoutError thrown
  ├─ HandlerError thrown
  ├─ UnknownError thrown
  └─ All caught by ErrorRecoveryHook → recover or emit error response

Response → ErrorRecoveryHook (Step 74)
  ├─ Classify error type
  ├─ Attempt recovery (retry, rollback, escalation)
  ├─ Record metrics + alert if needed
  └─ Emit JSON-RPC error response with correlation ID
```

### Error Code Mapping (JSON-RPC 2.0)

| Error Type | Code | Source | Recovery |
|---|---|---|---|
| ValidationError | -32600 | Envelope/request validation (Step 73) | Reject immediately, never retry |
| TimeoutError | -32603 | RPC deadline exceeded (Step 64) | Retry up to 3x with exponential backoff |
| HandlerError | -32603 | Unhandled exception from handler (Step 71) | Log with stack trace, escalate |
| UnknownError | -32603 | Unexpected middleware exception | Log, telemetry alert if rate >1% |

### Deliverables

**File Structure**:
```
src/versions/v2.0.0/lib/
├── error-types.mjs                 (150 lines, 7 custom error classes)
├── error-recovery-helpers.mjs       (450 lines, 20+ helper functions)
├── error-recovery-actions.mjs       (380 lines, 4 recovery classes + factories)
├── error-recovery-metrics.mjs       (340 lines, 4 metric collectors)
└── error-recovery-hook.mjs          (380 lines, main middleware class)

src/versions/v2.0.0/tests/
└── error-recovery-hook.test.mjs     (550 lines, 11 test suites, 38+ tests)
```

### Error Type Hierarchy

```javascript
import {
  ErrorRecoveryError,           // Base class, JSON-RPC code + correlation ID
  ValidationError,              // Envelope/payload validation (code -32600)
  TimeoutError,                 // RPC deadline (code -32603, isTransient=true)
  HandlerError,                 // Unhandled handler exception (code -32603)
  RecoveryActionError,          // Recovery failed (code -32000)
  AlertingError,                // Telemetry recording failed (code -32000)
  UnknownError,                 // Catch-all for unexpected exceptions (code -32603)
} from './lib/error-types.mjs';
```

### Middleware Hook Signature

Compatible with MiddlewareChain (Step 47):

```javascript
/**
 * Error recovery middleware hook
 * @param {Object} message - Bridge message { messageType, messageId, data }
 * @param {Function} next - Next middleware or dispatcher
 * @param {Object} context - Middleware context { logger?, metrics?, server? }
 * @returns {Promise<Object>} { handled, shouldRelay, response }
 *   - response.success: false if error occurred
 *   - response.error: { code, message, data { operation, messageId, ... } }
 */
async function errorRecoveryHook(message, next, context = {}) {
  try {
    // Invoke next middleware/dispatcher
    const result = await next(message);
    return result; // Pass through if success
  } catch (error) {
    // Catch any error and convert to JSON-RPC response
    const classification = classifyError(error);
    const messageId = message?.messageId;

    // Log with correlation ID
    logger.error(formatErrorForLogging(error, messageId, includeStack));

    // Record metrics
    metrics?.recordError(classification.type, messageId);

    // Attempt recovery if error is transient
    if (classification.isRecoverable) {
      const recovery = await orchestrator.orchestrate(error, ...);
      if (recovery.recovered) {
        return { handled: true, success: true, data: recovery.result };
      }
    }

    // Emit error response
    return {
      handled: true,
      shouldRelay: false,
      response: {
        messageType: message.messageType,
        messageId,
        success: false,
        error: buildErrorResponse(error, messageId),
      },
    };
  }
}
```

### Configuration

```javascript
const middleware = createErrorRecoveryMiddleware({
  logger: bridgeLogger,           // Optional: IBridgeLogger instance
  metrics: telemetryCollector,    // Optional: IBridgeTelemetryCollector instance
  server: coreServer,             // Optional: CoreServer for context
  policies: {
    enableRetry: true,            // Default: retry transient errors
    enableRollback: true,         // Default: attempt state rollback
    enableAlerting: true,         // Default: record metrics + alerts
    maxRetries: 3,                // Default: 3 retry attempts
    alertThreshold: 0.01,         // Default: 1% error rate threshold
  },
  includeStackTrace: false,       // Default: don't include stack in responses
});

// Register with MiddlewareChain (Step 47)
const hook = createErrorRecoveryHook(config);
middlewareChain.registerHook('errorRecovery', hook);
```

### Recovery Actions

**Retry Logic** (for TimeoutError only):
- Retries up to 3 times (configurable)
- Exponential backoff: 100ms, 200ms, 400ms (caps at 5s)
- Never retries ValidationError or HandlerError (permanent failures)
- Records retry attempts in metrics

**State Rollback** (optional, per-handler):
- Handler must implement `onError(originalState)` callback
- Silently skipped if handler doesn't support rollback (fail-soft)
- Logged and recorded in metrics if attempted

**Error Escalation** (always):
- Records error type + count to metrics
- Checks if error rate exceeds 1% over 5-second window
- Logs alert if threshold exceeded
- Non-blocking: escalation failures don't affect main response

### Metrics & Observability

**Built-in Metrics Collectors**:
```javascript
import {
  ErrorRateCollector,           // Track error rate over sliding window
  ErrorTypeHistogram,           // Distribution by type
  RecoverySuccessTracker,       // Recovery outcome statistics
  ErrorRecoveryMetricsCollector, // Composite collector
} from './lib/error-recovery-metrics.mjs';

const metrics = createErrorRecoveryMetricsCollector(5000); // 5s window

// Record errors and recovery attempts
metrics.recordError('timeout', 'msg-1');
metrics.recordSuccess();
metrics.recordRecoveryAttempt(true, 'timeout', 50); // success, type, delayMs

// Query metrics
const summary = metrics.getSummary();
// {
//   errorRate: "1.50%",
//   errorCount: 3,
//   totalRequests: 200,
//   recoverySuccessRate: "66.67%",
//   avgRecoveryDelayMs: 42,
// }

const detailed = metrics.getMetrics();
// {
//   timestamp: "2024-01-15T...",
//   errorRate: { rate: 0.015, errorCount: 3, totalCount: 200, windowMs: 5000 },
//   errorDistribution: { validation: 10%, timeout: 50%, handler: 30%, unknown: 10% },
//   recoveryStats: { successRate: 0.667, totalAttempts: 3, avgDelayMs: 42 },
//   recoveryByType: { timeout: 0.667, handler: 0.5 },
//   alertThresholdExceeded: false,
// }
```

### Testing

**Test Coverage**: 11 test suites, 38+ tests

```bash
# Run all error recovery tests
npx mocha src/versions/v2.0.0/tests/error-recovery-hook.test.mjs

# Run specific suite
npx mocha src/versions/v2.0.0/tests/error-recovery-hook.test.mjs --grep "Error Classification"

# With reporter
npx mocha src/versions/v2.0.0/tests/error-recovery-hook.test.mjs --reporter json --output results.json
```

**Test Suites**:
1. Error Classification (5 tests) — Identifies error types, codes, recoverability
2. Response Building (6 tests) — Constructs JSON-RPC error responses
3. Middleware Execution (5 tests) — Catches errors, emits responses, never crashes
4. State Rollback (4 tests) — Optional handler-based state recovery
5. Retry Logic (4 tests) — Exponential backoff, transient-only retry
6. Error Rate Monitoring (3 tests) — Sliding window, alert thresholds
7. Graceful Degradation (3 tests) — Operates without logger/metrics/server
8. Correlation & Observability (3 tests) — messageId correlation, metrics recording
9. Edge Cases (2 tests) — Nested error causes, circular references
10. Integration with MiddlewareChain (3 tests) — Registers as hook, executes in sequence
11. Metrics Collector Integration (3 tests) — Error rate, type distribution, recovery stats

**Expected Results**:
- 38/38 tests passing
- Execution time: ~3 seconds
- Test coverage: >90% for core middleware

### Performance Characteristics

| Operation | Latency | Notes |
|---|---|---|
| Error classification | <1ms | Object type checking |
| Response building | <2ms | JSON object construction |
| Retry backoff calculation | <1ms | Math operation |
| Metrics recording | <1ms | Array push, window calculation |
| State rollback | <50ms | Depends on handler implementation |
| **Total error recovery overhead** | **<10ms** | Per message, no blocking I/O |
| **p99 error handling** | **<20ms** | Including logging + metrics |

### Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| Error responses not emitted | Middleware not registered | Add `createErrorRecoveryHook(config)` to MiddlewareChain |
| Stack traces appearing in responses | `includeStackTrace: true` | Set to `false` for production; debug only |
| Alerts firing too frequently | Alert threshold too low (default 1%) | Increase alertThreshold config (e.g., 0.05 for 5%) |
| Retries not happening | Error not classified as transient | Only TimeoutError is retried; validate error type |
| Rollback not called | Handler doesn't implement `onError()` | Add `async onError(state) {}` method to handler |
| Metrics all zeros | Metrics passed as null | Provide metrics instance or use null gracefully |
| Memory growing | Sliding window too large | Reduce windowMs or increase cleanup frequency |
| Circular reference error | Error.originalError chain | Auto-sanitized by `sanitizeErrorForSerialization()` |

### Usage Example: Step 72–74 Middleware Integration

```javascript
import { MiddlewareChain } from './lib/message-routing-middleware.mjs';
import { MessageLoggingMiddleware } from './lib/message-logging-middleware.mjs';
import { ErrorRecoveryMiddleware } from './lib/error-recovery-hook.mjs';
import { createErrorRecoveryMetricsCollector } from './lib/error-recovery-metrics.mjs';

// Create middleware chain
const chain = new MiddlewareChain({
  logger: bridgeLogger,
  metrics: telemetryCollector,
  server: coreServer,
});

// Step 72: Add logging middleware
const loggingMiddleware = new MessageLoggingMiddleware({
  middlewareChain: chain,
  logger: bridgeLogger,
  metrics: telemetryCollector,
});

// Step 73: Validation would be added here (when implemented)

// Step 74: Add error recovery middleware
const errorMetrics = createErrorRecoveryMetricsCollector(5000);
const recoveryMiddleware = new ErrorRecoveryMiddleware({
  logger: bridgeLogger,
  metrics: errorMetrics,
  server: coreServer,
  policies: {
    enableRetry: true,
    enableRollback: true,
    enableAlerting: true,
    maxRetries: 3,
    alertThreshold: 0.01,
  },
  includeStackTrace: false,
});

// Register middleware in execution order
chain.registerMiddleware(loggingMiddleware);
// chain.registerMiddleware(validationMiddleware); // Step 73
chain.registerMiddleware(recoveryMiddleware);

// Message flow:
// Message → LoggingMiddleware (Step 72)
//         → [ValidationMiddleware (Step 73)]
//         → Dispatcher (Step 14/71)
//         → ErrorRecoveryMiddleware (Step 74)
//
// If error occurs in any stage:
//   → ErrorRecoveryMiddleware catches it
//   → Classifies error type (validation, timeout, handler, unknown)
//   → Attempts recovery if transient (retry + backoff)
//   → Records metrics (error rate, type histogram, recovery stats)
//   → Emits JSON-RPC error response with messageId correlation
//   → Logs error with full context for debugging
```

### File References

- **Implementation**: `src/versions/v2.0.0/lib/error-recovery-hook.mjs` (~380 lines)
- **Error Types**: `src/versions/v2.0.0/lib/error-types.mjs` (~150 lines)
- **Helpers**: `src/versions/v2.0.0/lib/error-recovery-helpers.mjs` (~450 lines)
- **Recovery Actions**: `src/versions/v2.0.0/lib/error-recovery-actions.mjs` (~380 lines)
- **Metrics**: `src/versions/v2.0.0/lib/error-recovery-metrics.mjs` (~340 lines)
- **Tests**: `src/versions/v2.0.0/tests/error-recovery-hook.test.mjs` (~550 lines)
- **Related**: Step 47 (MiddlewareChain), Step 72 (Logging), Step 73 (Validation), Step 64 (TimeoutManager)

---

## Handler Registry (Step 66)

### Purpose

The **Handler Registry** is a centralized catalog of all bridge handlers that enables:
- **Single Source of Truth**: All handlers documented in one place
- **Metadata-Driven Operations**: Step 71 (orchestration) and Steps 72–74 (middleware) query metadata without tight coupling
- **Extensibility**: Steps 76–95 handlers added by editing registry entries only (no code changes)
- **Observable**: Middleware can determine handler properties (timeout, stability) for logging, validation, error recovery

### Architecture

**Registry Location**: `src/versions/v2.0.0/lib/handler-registry.mjs`

**Exports**:
```javascript
// Get all handlers in registration order (for Step 71)
getAllHandlers() → Array<HandlerEntry>

// Get metadata for specific handler (for Steps 72–74)
getHandlerMetadata(messageType) → HandlerEntry | throws HandlerNotFoundError

// Filter handlers by stability (experimental, core, deprecated)
getHandlersByStabilityTier(tier) → Array<HandlerEntry>

// Filter handlers by timeout policy (fast, medium, slow)
getHandlersByTimeoutPolicy(policy) → Array<HandlerEntry>

// Check if handler exists
hasHandler(messageType) → boolean

// Exception types
HandlerRegistryError, HandlerNotFoundError
```

### Handler Metadata Schema

```typescript
interface HandlerEntry {
  messageType: string;              // "bridge:getEditorState"
  handler: Function;                // async (message, context) => Promise<HandlerResponse>
  timeoutPolicy: "fast" | "medium" | "slow";  // 2s | 10s | 30s
  stabilityTier: "core" | "experimental" | "deprecated";
  description: string;              // Human-readable purpose
  relatedSteps: number[];           // [50, 71, 63]
  dependencies: (string|number)[];  // [48, 49, "EditorContextCollector"]
}
```

### Timeout Policies

| Policy | Duration | Use Case | Example |
|--------|----------|----------|---------|
| `fast` | 2s | Synchronous lookups, simple queries | editor state, symbol info |
| `medium` | 10s | I/O operations, workspace scans | search, navigation |
| `slow` | 30s | Long-running operations | debugging, testing |

### Stability Tiers

| Tier | Support | Behavior | Example |
|------|---------|----------|---------|
| `core` | Production | Fully supported, high reliability | bootstrap, getEditorState |
| `experimental` | Community | Beta, API may change | testExplorer, debugSession |
| `deprecated` | Maintenance | Plan to remove, use alternatives | (none yet) |

### Usage Example: Step 71 Handler Registration

```javascript
import { getAllHandlers } from './lib/handler-registry.mjs';
import { HandlerDispatcher } from './lib/handler-dispatcher.mjs';

export function registerAllHandlers(dispatcher, context) {
  const allHandlers = getAllHandlers();

  // Handlers registered in order:
  // 1. bootstrap (gateway)
  // 2. editor context (Steps 50–51)
  // 3. navigation (Steps 55–57)
  // 4. code intelligence (Steps 58–59)
  // 5. advanced (Steps 60–61, 76–95)

  for (const entry of allHandlers) {
    dispatcher.register(entry.messageType, entry.handler);
    context.logger?.debug(`Registered handler: ${entry.messageType}`);
  }
}
```

### Usage Example: Step 72–74 Middleware Integration

**Message Logging Middleware** (Step 72):
```javascript
import { getHandlerMetadata } from './lib/handler-registry.mjs';

export function createLoggingMiddleware(logger) {
  return async function loggingMiddleware(message, next) {
    const meta = getHandlerMetadata(message.messageType);
    const tier = meta.stabilityTier === 'core' ? '📌' : '⚠️';

    logger.debug(`${tier} ${message.messageType} (timeout: ${meta.timeoutPolicy})`);
    const startTime = Date.now();

    try {
      const result = await next();
      const duration = Date.now() - startTime;
      logger.debug(`✓ ${message.messageType} (${duration}ms)`);
      return result;
    } catch (err) {
      logger.error(`✗ ${message.messageType}: ${err.message}`);
      throw err;
    }
  };
}
```

**Error Recovery Middleware** (Step 74):
```javascript
import { getHandlerMetadata } from './lib/handler-registry.mjs';

export function createErrorRecoveryMiddleware(logger) {
  return async function errorRecoveryMiddleware(message, next) {
    const meta = getHandlerMetadata(message.messageType);

    // Experimental handlers get graceful fallback
    if (meta.stabilityTier === 'experimental') {
      try {
        return await next();
      } catch (err) {
        logger.warn(`Experimental handler failed, returning safe default: ${err.message}`);
        return {
          success: false,
          error: 'Handler not available (experimental)',
          retryable: true
        };
      }
    }

    // Core handlers: throw to caller
    return next();
  };
}
```

### Adding New Handlers (Steps 76–95)

1. **Implement handler** in `src/versions/v2.0.0/lib/` (e.g., `refactor-handler.mjs`)
2. **Add registry entry** in `handler-registry.mjs`:

```javascript
{
  messageType: 'bridge:refactor',
  handler: createRefactorHandler(),
  timeoutPolicy: 'medium',
  stabilityTier: 'experimental',
  description: 'Code refactoring operations',
  relatedSteps: [76, 71],
  dependencies: [50, 53, 54]
}
```

3. **Update metadata table** in `HANDLER_REGISTRY_REFERENCE.md`
4. **Run tests** to validate:

```bash
npx mocha src/versions/v2.0.0/tests/handler-registry.test.mjs --timeout 5000
```

### Testing

**Test Coverage**: 22 tests across 6 suites

| Suite | Tests | Coverage |
|-------|-------|----------|
| Module Load & Exports | 3 | Registry loads, exports functions, returns safe copies |
| Metadata Completeness | 5 | Required fields, valid timeout/stability values |
| Registration Order | 4 | Bootstrap first, context before navigation, no duplicates |
| Lookup Functions | 4 | getHandlerMetadata(), getAllHandlers(), hasHandler() |
| Stability & Timeout Filters | 4 | getHandlersByStabilityTier(), getHandlersByTimeoutPolicy() |
| Extensibility | 3 | New handlers follow pattern, metadata schema extensible |

**Run Tests**:
```bash
npx mocha src/versions/v2.0.0/tests/handler-registry.test.mjs
```

### Documentation

- **Implementation**: `src/versions/v2.0.0/lib/handler-registry.mjs` (~500 lines)
- **Tests**: `src/versions/v2.0.0/tests/handler-registry.test.mjs` (~520 lines)
- **Reference**: `src/versions/v2.0.0/handlers/HANDLER_REGISTRY_REFERENCE.md` (metadata tables)

---

## Handler Tests: Editor Context Integration (Step 67)

### Purpose

**Step 67** creates integration tests validating the two editor context handlers (Steps 50–51) working together with their dependencies (EditorContextCollector, SelectionTracker). This bridges the gap between individual unit tests and end-to-end WebView integration tests (Step 75).

### Architecture

**Test Location**: `src/versions/v2.0.0/tests/editor-context-handler-integration.test.mjs`

**Handlers Tested**:
- **Step 50**: `getEditorStateHandler` — queries editor context and returns snapshot
- **Step 51**: `onEditorStateChange` handler — subscribes to editor state changes

**Dependencies Mocked**:
- **Step 48**: EditorContextCollector — source of editor state
- **Step 49**: SelectionTracker — manages selection subscriptions

### Test Suites

#### Suite 1: Handler Initialization & Dependency Injection (3 tests)

Validates both handlers initialize correctly and reject invalid dependencies:

```javascript
✓ should initialize both handlers successfully with valid mocks
✓ should reject getEditorStateHandler when collector is null
✓ should reject getEditorStateHandler when context is undefined
```

**Purpose**: Ensure handlers fail fast with helpful errors when dependencies are missing.

---

#### Suite 2: getEditorState + onEditorStateChange Lifecycle (5 tests)

Validates handler response schemas, state consistency, and state change propagation:

```javascript
✓ should call getEditorState and return editor state snapshot
✓ should handle multiple rapid getEditorState calls returning consistent state
✓ should reflect state change between getEditorState calls
✓ should handle null selection gracefully
✓ should maintain cursor position consistency after editor state queries
```

**Response Schema Tested**:
```javascript
{
  success: true,
  data: {
    activeFile: '/path/to/file.js',           // From EditorContextCollector
    cursorLine: 10,                           // From EditorContextCollector
    cursorColumn: 5,                          // From EditorContextCollector
    selectedText: 'selection',                // From EditorContextCollector
    selectionStart: { line: 10, character: 5 },
    selectionEnd: { line: 10, character: 15 },
    fileContent: '...',                       // Optional
    language: 'javascript',                   // Optional
    diagnosticsCount: 0,                      // Optional
    lastUpdate: '2024-01-15T12:34:56.789Z'    // ISO timestamp
  }
}
```

---

#### Suite 3: State Consistency Across Handlers (4 tests)

Validates that state changes in EditorContextCollector propagate correctly to handler responses:

```javascript
✓ should return consistent selection between getEditorState and SelectionTracker
✓ should propagate EditorContextCollector state changes to getEditorState responses
✓ should maintain active file consistency when collector state changes
✓ should handle null/cleared selection consistently across handler calls
```

**Scenario**: 
- Initial state: file.js with "consistent" selection
- Mutate collector to: other.js with "propagated" selection
- Verify: Next getEditorState call reflects new state

---

#### Suite 4: Error Recovery & Edge Cases (4 tests)

Validates robustness with extreme conditions:

```javascript
✓ should gracefully handle collector returning all null values
✓ should handle very rapid state changes without losing data
✓ should handle formatter edge case: very long selection text
✓ should recover from collector state listener errors
```

**Edge Cases Tested**:
- All values null → returns safe defaults
- 5 rapid state mutations → each query returns correct state
- 10KB selection text → preserved and returned
- Listener throw in collector → handler still succeeds

### Test Execution

**Command**:
```bash
cd src/versions/v2.0.0
npx mocha tests/editor-context-handler-integration.test.mjs --timeout 5000
```

**Expected Output**:
```
Suite 1: Handler Initialization & Dependency Injection
  ✓ should initialize both handlers successfully with valid mocks
  ✓ should reject getEditorStateHandler when collector is null
  ✓ should reject getEditorStateHandler when context is undefined

Suite 2: getEditorState + onEditorStateChange Lifecycle
  ✓ should call getEditorState and return editor state snapshot
  ✓ should handle multiple rapid getEditorState calls returning consistent state
  ✓ should reflect state change between getEditorState calls
  ✓ should handle null selection gracefully
  ✓ should maintain cursor position consistency after editor state queries

Suite 3: State Consistency Across Handlers
  ✓ should return consistent selection between getEditorState and SelectionTracker
  ✓ should propagate EditorContextCollector state changes to getEditorState responses
  ✓ should maintain active file consistency when collector state changes
  ✓ should handle null/cleared selection consistently across handler calls

Suite 4: Error Recovery & Edge Cases
  ✓ should gracefully handle collector returning all null values
  ✓ should handle very rapid state changes without losing data
  ✓ should handle formatter edge case: very long selection text
  ✓ should recover from collector state listener errors

16 passing (120ms)
```

### Mock Factories

The test suite provides reusable mock factories used by integration tests:

#### `createMockEditorContextCollector(initialState)`

Returns mock collector with:
- `getActiveFile()` → file object with { filepath, contents }
- `getCursorPosition()` → { line, character }
- `getSelection()` → { start, end, text }
- `setState(newState)` → updates state and notifies listeners
- `onStateChange(callback)` → subscribe to mutations

#### `createMockSelectionTracker(initialSelection)`

Returns mock tracker with:
- `hasSelection()` → boolean
- `getSelection()` → selection object
- `onSelectionChange(callback)` → subscribe to changes
- `setSelection(newSelection)` → update and notify

#### `createMockDispatcher()`

Returns mock dispatcher for onEditorStateChange tests with:
- `sendMessage(message)` → track dispatched messages
- `getMessages()` → array of sent messages
- `getLastMessage()` → most recent message

### Integration Points

**Upstream** (Dependencies):
- Step 48: EditorContextCollector (mocked)
- Step 49: SelectionTracker (mocked)

**Downstream** (Uses):
- Step 70: Handler integration tests — includes this suite pattern
- Step 71: Handler registration — validates metadata
- Step 75: WebView integration tests — end-to-end scenarios

### Documentation

| File | Purpose | Lines |
|------|---------|-------|
| `tests/editor-context-handler-integration.test.mjs` | Integration test suite | 646 |
| `lib/get-editor-state-handler.mjs` | Handler under test | 319 |
| `tests/get-editor-state-handler.test.mjs` | Unit tests (Step 50) | 552 |
| `tests/onEditorStateChange-handler.test.mjs` | Unit tests (Step 51) | 435 |

---

## Bridge Protocol Adapter (Step 63)

### Purpose

The **Bridge Protocol Adapter** translates between two communication layers:

- **Inbound**: C# transport `Message` objects (JSON-RPC envelope: `{messageType, messageId, data}`)
- **Outbound**: Node handler results (`{success, data/error}`)

The adapter normalizes protocol semantics, tracks RPC correlations by messageId, enforces timeouts, and provides middleware integration hooks for logging, validation, and error recovery.

### Architecture

**Message Flow**:
```
[Transport.SendMessage(Message)]
           ↓
[Adapter.translateInbound(Message)]
           ↓
[BridgeMessage + HandlerContext] → Handler
           ↓
[Handler returns HandlerResponse]
           ↓
[Adapter.translateOutbound(HandlerResponse)]
           ↓
[Message envelope] → Transport
```

### Core Responsibilities

| Responsibility | Method | Returns |
|---|---|---|
| **Inbound Translation** | `translateInbound(message, context?)` | `{bridgeMessage, handlerContext}` |
| **Outbound Translation** | `translateOutbound(response, messageId, messageType)` | `Message` envelope |
| **RPC Correlation** | `trackPendingRequest(messageId, timeoutMs?)` | `Promise<response>` |
| **RPC Resolution** | `resolvePendingRequest(messageId, response)` | `boolean` |
| **RPC Rejection** | `rejectPendingRequest(messageId, error)` | `boolean` |
| **Middleware Hooks** | `registerHook(hookName, handler)` | `void` |

### Example Usage

**Instantiation**:
```javascript
import { createBridgeProtocolAdapter } from './lib/bridge-protocol-adapter.mjs';

const adapter = createBridgeProtocolAdapter({
  logger: coreServer.logger,
  metrics: coreServer.metrics,
  defaultTimeoutMs: 30000,
  enableTracing: false
});
```

**Inbound Translation** (in core-server.js message loop):
```javascript
// Receive raw C# Message
const rawMessage = JSON.parse(line); // {messageType, messageId, data}

// Translate to handler contract
const { bridgeMessage, handlerContext } = await adapter.translateInbound(
  rawMessage,
  { server: coreServer }
);

// Dispatch to handler
const handlerResponse = await dispatcher.dispatch(bridgeMessage, handlerContext);

// Translate back to C# format
const responseMessage = await adapter.translateOutbound(
  handlerResponse,
  rawMessage.messageId,
  rawMessage.messageType
);

// Send to IDE
transport.sendMessage(responseMessage);
```

**RPC Correlation** (for async operations):
```javascript
// Track outbound RPC call
const pendingResponse = adapter.trackPendingRequest(messageId, 5000);

// Send request to Continue
sendToIDE(requestMessage);

// Later, when response arrives from Continue
const response = await pendingResponse;
console.log(response);
```

### Middleware Hooks

Available hooks for Steps 72–74 integration:

| Hook | Invocation | Purpose |
|------|-----------|---------|
| `pre-translate` | Before inbound translation | Log/validate raw message |
| `post-translate` | After inbound translation | Inspect normalized message |
| `pre-handler-response` | Before outbound translation | Transform/log response |
| `post-handler-response` | After outbound translation | Finalize message envelope |

**Example Hook**:
```javascript
// Step 72: Message Logging Middleware
adapter.registerHook('pre-translate', async (message) => {
  logger.debug(`[RPC IN] ${message.messageType} (${message.messageId})`);
});

// Step 73: Validation Middleware
adapter.registerHook('pre-translate', async (message) => {
  if (!message.messageType?.startsWith('bridge:')) {
    throw new ValidationError('messageType', message.messageType, 'Must start with "bridge:"');
  }
});

// Step 74: Error Recovery Middleware
adapter.registerHook('post-handler-response', async (message) => {
  if (!message.data.success) {
    logger.error(`[RPC ERROR] ${message.messageType}: ${message.data.error}`);
  }
});
```

### Error Handling

**Exception Hierarchy**:
```
ProtocolAdapterError (base)
  ├─ TimeoutError          (RPC call exceeded timeout window)
  ├─ ValidationError       (message field validation failed)
  └─ (other ProtocolAdapterError instances with operationType)
```

**Catching Errors**:
```javascript
import {
  ProtocolAdapterError,
  TimeoutError,
  ValidationError
} from './lib/bridge-protocol-adapter.mjs';

try {
  const { bridgeMessage } = await adapter.translateInbound(message);
} catch (error) {
  if (error instanceof ValidationError) {
    console.error(`Field validation failed: ${error.fieldName} = ${error.value}`);
  } else if (error instanceof TimeoutError) {
    console.error(`RPC timeout after ${error.timeoutMs}ms: ${error.messageId}`);
  } else if (error instanceof ProtocolAdapterError) {
    console.error(`Protocol adapter error [${error.operationType}]: ${error.message}`);
  }
}
```

### Testing

**Test Coverage**: 27 comprehensive tests across 7 suites

```bash
# Run all protocol adapter tests
node src/versions/v2.0.0/tests/bridge-protocol-adapter.test.mjs

# Expected output: ✅ ALL 27 TESTS PASSING
```

---

## Timeout Manager for RPC Calls (Step 64)

### Purpose

The **TimeoutManager** provides dedicated, reusable timeout lifecycle management for pending RPC requests. Extracted from inline timeout enforcement in Step 63, it offers:

- **Policy-driven timeout configuration** — per-handler or global timeouts
- **Metrics collection** — p99 latency, timeout rate, average wait time
- **Graceful degradation** — optional logger/metrics (no-op if null)
- **Request lifecycle tracking** — start → resolve/reject/timeout with cleanup

### Architecture

**Request Lifecycle**:
```
trackRequest(messageId, timeoutMs, messageType?)
  ↓
[Start: record timestamp]
  ↓
  ├─ resolveRequest(messageId, response) → cleanup + record latency
  ├─ rejectRequest(messageId, error) → cleanup + record latency
  └─ [setTimeout] → timeout fires → reject + cleanup + record latency + increment counter
  ↓
getMetrics() → {totalRequests, timeouts, averageWaitMs, p99WaitMs, requestsPerSecond}
```

### Core Responsibilities

| Responsibility | Method | Returns |
|---|---|---|
| **Track Request** | `trackRequest(messageId, timeoutMs?, messageType?)` | `Promise<response>` |
| **Resolve** | `resolveRequest(messageId, response)` | `boolean` |
| **Reject** | `rejectRequest(messageId, error)` | `boolean` |
| **Get Metrics** | `getMetrics()` | `{totalRequests, timeouts, averageWaitMs, p99WaitMs, requestsPerSecond, pendingRequests}` |
| **Cleanup** | `clearExpired(maxAgeMs)` | `number` (cleaned count) |
| **Dispose** | `dispose()` | `void` |
| **Pending Count** | `getPendingCount()` | `number` |

### TimeoutPolicy Configuration

**Contract**:
```javascript
{
  defaultTimeoutMs: 5000,                    // Fallback timeout (ms)
  handlerTimeouts: Map<string, number>,      // Per-messageType overrides
  retryOnTimeout: false,                     // (optional) enable retry
  maxRetries: 0                              // (optional) max attempts
}
```

**Timeout Hierarchy** (applied in order):
1. Explicit `timeoutMs` parameter to `trackRequest()`
2. Handler-specific from `handlerTimeouts.get(messageType)`
3. Policy default `defaultTimeoutMs`

### Example Usage

**Instantiation**:
```javascript
import { createTimeoutManager, createDefaultPolicy } from './lib/timeout-manager.mjs';

const policy = {
  defaultTimeoutMs: 5000,
  handlerTimeouts: new Map([
    ['bridge:getEditorState', 2000],    // Fast
    ['bridge:search', 30000],           // Slow
    ['bridge:codeCompletion', 15000]    // Medium
  ])
};

const manager = createTimeoutManager(policy, logger, metrics);

// Or use factory for common defaults
const defaultManager = createTimeoutManager(createDefaultPolicy(), logger);
```

**Track & Resolve Request**:
```javascript
// Outbound: Call handler with timeout
const pendingResponse = manager.trackRequest(
  'msg-uuid-1234',          // messageId
  null,                      // use policy timeout
  'bridge:getEditorState'   // handler-specific timeout
);

// Send to handler
const request = {
  messageType: 'bridge:getEditorState',
  messageId: 'msg-uuid-1234',
  data: {}
};
handlerDispatcher.dispatch(request);

// Later: resolve when response arrives
const response = await pendingResponse;
manager.resolveRequest('msg-uuid-1234', response);

console.log('Success:', response);
```

**Query Metrics**:
```javascript
const metrics = manager.getMetrics();
console.log(`
  Total Requests: ${metrics.totalRequests}
  Timeouts: ${metrics.timeouts}
  Average Wait: ${metrics.averageWaitMs}ms
  P99 Latency: ${metrics.p99WaitMs}ms
  Request Rate: ${metrics.requestsPerSecond}/sec
  Pending: ${metrics.pendingRequests}
`);

// Use for monitoring (Step 72–74 middleware)
if (metrics.timeouts / metrics.totalRequests > 0.05) {
  logger.warn('High timeout rate detected');
}
```

**Cleanup & Disposal**:
```javascript
// Remove requests older than 2 minutes
const cleaned = manager.clearExpired(120000);
console.log(`Cleaned up ${cleaned} expired requests`);

// Full cleanup (usually called on shutdown)
manager.dispose();
```

### Error Handling

**Exception Hierarchy**:
```
TimeoutManagerError (base)
  └─ TimeoutError (RPC timeout fired)
```

**Catching Errors**:
```javascript
import { TimeoutManager, TimeoutError, TimeoutManagerError } from './lib/timeout-manager.mjs';

try {
  const manager = new TimeoutManager(policy);
  const response = await manager.trackRequest('msg-123', 1000);
} catch (error) {
  if (error instanceof TimeoutError) {
    console.error(`Request timed out after ${error.timeoutMs}ms: ${error.messageId}`);
  } else if (error instanceof TimeoutManagerError) {
    console.error(`Manager error: ${error.message} (${error.operation})`);
  }
}
```

### Integration Points

**Step 63: Protocol Adapter**  
TimeoutManager can be used alongside (optional migration from inline timeouts):
```javascript
// Step 63 still owns RPC tracking, but could delegate to TimeoutManager
const pendingResponse = timeoutManager.trackRequest(messageId, timeoutMs);
adapter.setPendingRequest(messageId, pendingResponse);
```

**Step 71: Handler Registration**  
Register handlers with per-type timeout policies:
```javascript
// Different timeouts for different handler classes
const fastHandlers = new Map([
  ['bridge:getEditorState', 2000],
  ['bridge:onEditorStateChange', 2000]
]);

const slowHandlers = new Map([
  ['bridge:search', 30000],
  ['bridge:codeCompletion', 15000]
]);

const manager = createTimeoutManager({
  defaultTimeoutMs: 5000,
  handlerTimeouts: new Map([...fastHandlers, ...slowHandlers])
}, logger);
```

**Step 72–74: Middleware**  
Subscribe to metrics for monitoring/telemetry:
```javascript
// Step 72: Message Logging Middleware
setInterval(() => {
  const metrics = manager.getMetrics();
  logger.info(`[RPC Metrics] Total: ${metrics.totalRequests}, Timeouts: ${metrics.timeouts}, P99: ${metrics.p99WaitMs}ms`);
}, 60000);

// Step 73: Validation Middleware
const metrics = manager.getMetrics();
if (metrics.p99WaitMs > 10000) {
  logger.warn('P99 latency exceeds 10 seconds');
}

// Step 74: Error Recovery Middleware
if (metrics.timeouts > 0.1 * metrics.totalRequests) {
  logger.error('Timeout rate > 10%, consider increasing timeouts');
}
```

### Testing

**Test Coverage**: 33 comprehensive tests across 10 suites

```bash
# Run all timeout manager tests
npx mocha src/versions/v2.0.0/tests/timeout-manager.test.mjs --timeout 10000

# Expected output: ✅ ALL 33 TESTS PASSING
```

**Test Suites**:
1. Initialization & Policy Validation (3 tests)
2. Request Tracking (3 tests)
3. Request Resolution & Rejection (3 tests)
4. Timeout Enforcement (4 tests)
5. Metrics Collection (4 tests)
6. Cleanup & Disposal (3 tests)
7. Edge Cases & Degradation (5 tests)
8. Factory Functions (3 tests)
9. Logger Integration (2 tests)
10. Metrics Integration (2 tests)

---

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

### Handler Integration Testing (Steps 67–70)

When multiple handlers share dependencies (DocumentProvider, SymbolExtractor, cache), test their interaction together:

#### Key Principles

1. **Shared State** — Both handlers access the same DocumentProvider and SymbolExtractor instances
2. **Realistic Flows** — Test user scenarios: completion → hover → edit → completion again
3. **Cache Effectiveness** — Verify cache hit rates when handlers share query results
4. **Non-Cascading Errors** — Ensure one handler's failure doesn't poison the other
5. **Performance Gates** — Combined latency (all handlers on same document) < 100ms p99

#### Example: Code-Completion & Hover-Info Integration

```javascript
// File: tests/handler-tests-code-completion.test.mjs (Step 69)

import { createCodeCompletionHandler } from '../lib/code-completion-handler.mjs';
import { createHoverInfoHandler } from '../lib/hover-info-handler.mjs';
import {
  createSharedDocumentProvider,
  createSharedSymbolExtractor,
  createCompletionHoverScenario,
} from './mocks/handler-integration-helpers.mjs';

describe('Handler Integration - Completion + Hover Flow', () => {
  let completion, hover;
  let docProvider, symbolExtractor;

  beforeEach(() => {
    // Create shared dependencies
    docProvider = createSharedDocumentProvider({
      '/service.cs': { content: 'code', language: 'csharp' }
    });

    symbolExtractor = createSharedSymbolExtractor({
      '/service.cs': [
        { name: 'GetUser', kind: 'method', line: 20, column: 4 },
        { name: 'GetUserById', kind: 'method', line: 30, column: 4 },
      ]
    });

    // Initialize both handlers with shared dependencies
    completion = createCodeCompletionHandler({
      documentProvider: docProvider,
      symbolExtractor,
    });

    hover = createHoverInfoHandler({
      documentProvider: docProvider,
      symbolExtractor,
    });
  });

  it('should completion and hover handlers share cache', async () => {
    const statsBefore = symbolExtractor.getCacheStats();

    // Completion query populates cache
    await completion.handle({
      data: { file: '/service.cs', line: 20, column: 10 }
    });

    // Hover query should hit cache
    await hover.handle({
      data: { filepath: '/service.cs', line: 20, column: 10 }
    });

    const statsAfter = symbolExtractor.getCacheStats();
    expect(statsAfter.hitRate).to.be.greaterThan(0);
  });

  it('should realistic flow: completion → hover → edit → completion', async () => {
    // Step 1: Get completions
    const completions = await completion.handle({
      data: { file: '/service.cs', line: 20, column: 10 }
    });

    // Step 2: Hover on completion
    const hover1 = await hover.handle({
      data: { filepath: '/service.cs', line: 20, column: 10 }
    });

    // Step 3: Edit document
    docProvider.updateDocument('/service.cs', 'new content');

    // Step 4: Get completions again
    const completions2 = await completion.handle({
      data: { file: '/service.cs', line: 20, column: 10 }
    });

    expect(completions).to.exist;
    expect(hover1).to.exist;
    expect(completions2).to.exist;
  });

  it('should combined latency meet performance gate (p99 < 100ms)', async () => {
    const latencies = [];

    for (let i = 0; i < 10; i++) {
      const start = performance.now();

      await completion.handle({
        data: { file: '/service.cs', line: 20, column: 10 }
      });

      await hover.handle({
        data: { filepath: '/service.cs', line: 20, column: 10 }
      });

      latencies.push(performance.now() - start);
    }

    const sorted = latencies.sort((a, b) => a - b);
    const p99 = sorted[Math.floor(sorted.length * 0.99)];

    expect(p99).to.be.lessThan(100);
  });
});
```

#### Shared Dependencies Pattern

Use the `handler-integration-helpers.mjs` module to create mocks:

```javascript
import {
  createSharedDocumentProvider,      // DocumentProvider with lifecycle tracking
  createSharedSymbolExtractor,       // SymbolExtractor with cache instrumentation
  createSharedDiagnosticsCollector,  // Diagnostics with error injection
  createSharedLogger,                // Logger with log capture
  createSharedMetrics,               // Metrics with percentile calculation
  createHandlerPair,                 // Convenient pair factory
} from './mocks/handler-integration-helpers.mjs';

// Option 1: Manual setup (fine-grained control)
const docProvider = createSharedDocumentProvider();
const symbolExtractor = createSharedSymbolExtractor();
const logger = createSharedLogger();

const handler1 = createHandler1({ documentProvider: docProvider, symbolExtractor, logger });
const handler2 = createHandler2({ documentProvider: docProvider, symbolExtractor, logger });

// Option 2: Factory setup (convenient)
const { completion, hover, dependencies } = createHandlerPair(
  createCodeCompletionHandler,
  createHoverInfoHandler,
  { initialDocs: { '/test.cs': { ... } } }
);

// Access shared state for assertions
const cacheStats = dependencies.symbolExtractor.getCacheStats();
const logs = dependencies.logger.getLogs();
```

#### Error Non-Cascading Pattern

Ensure one handler's failure doesn't poison the other:

```javascript
it('should handler A error not affect handler B', async () => {
  // Handler A configured with bad dependency
  const handlerA = createHandler1({ documentProvider: null });

  // Handler B configured correctly
  const handlerB = createHandler2({ documentProvider: docProvider });

  // Handler A fails (expected)
  try {
    await handlerA.handle({ data: {} });
  } catch (e) {
    // Expected: Handler A throws
  }

  // Handler B still works (no cascade)
  const result = await handlerB.handle({ data: { filepath: '/test.cs' } });
  expect(result).to.exist;
});
```

#### Performance Gate Validation

```javascript
it('should combined ops stay under performance gate', async () => {
  const measurements = [];

  for (let iter = 0; iter < 20; iter++) {
    const start = performance.now();

    // Run realistic scenario
    await handler1.handle({ data: { ... } });
    await handler2.handle({ data: { ... } });
    await handler2.handle({ data: { ... } });

    measurements.push(performance.now() - start);
  }

  // Calculate p99 latency
  const sorted = measurements.sort((a, b) => a - b);
  const p99 = sorted[Math.floor(sorted.length * 0.99)];

  // Gate: p99 < 100ms for combined operations
  assert(p99 < 100, `p99 ${p99}ms exceeds gate of 100ms`);
});
```

#### Step 70: Composite Handler Integration

Step 70 orchestrates all handler pairs (Steps 67, 68, 69) into a single comprehensive scenario:

```
Step 67 (editor-context handler tests)
   ↓
Step 68 (search/navigation handler tests)
   ↓
Step 69 (code-completion handler tests)
   ↓
Step 70 (composite orchestration) = 67 + 68 + 69 combined
```

**Purpose**: Validate that all three handler test suites work together coherently, with shared state, cross-handler message routing, and combined performance characteristics.

**File**: `src/versions/v2.0.0/tests/handler-tests-integration.test.mjs`

**Test Suites** (22 tests total):

1. **Initialization & Handler Registration** (4 tests)
   - Initialize all shared dependencies (DocumentProvider, SymbolExtractor, diagnostics, logger, metrics)
   - Verify document access and symbol extraction
   - Verify diagnostics and metrics recording

2. **Context-to-Completion Workflow** (5 tests)
   - Retrieve editor context for completion trigger
   - Extract symbols at completion position
   - Maintain cache consistency across context changes
   - Validate completion request format
   - Record workflow metrics

3. **Search-to-Navigation Workflow** (5 tests)
   - Search across multiple documents
   - Locate results in multiple files
   - Chain go-to-definition with search results
   - Find references without cross-contamination
   - Track search-to-navigation state

4. **Complex Multi-Handler Scenarios** (5 tests)
   - Handle editor state change with context propagation
   - Execute completion with search fallback on multi-file context
   - Maintain hover info cache during multi-file navigation
   - Record comprehensive metrics across multiple handlers

5. **Performance & Error Handling** (3 tests)
   - Cached queries meet performance gate (<5ms)
   - Concurrent multi-file operations timely (<100ms)
   - Gracefully handle missing documents without cascading errors

6. **State Consistency Validation** (2 tests)
   - Maintain consistency across rapid successive calls
   - Prevent state corruption during parallel handler invocations

**Implementation Pattern**:

```javascript
// File: tests/handler-tests-integration.test.mjs (Step 70)

import { describe, it, beforeEach } from 'mocha';
import assert from 'assert';
import { performance } from 'perf_hooks';
import {
  createSharedDocumentProvider,
  createSharedSymbolExtractor,
  createSharedDiagnosticsCollector,
  createSharedLogger,
  createSharedMetrics,
} from './mocks/handler-integration-helpers.mjs';

describe('Handler Integration - Initialization', () => {
  let documentProvider, symbolExtractor, diagnosticsCollector, logger, metrics;

  beforeEach(() => {
    // Initialize all shared mocks once per suite
    documentProvider = createSharedDocumentProvider({
      '/app.cs': { content: 'public class App {}' },
      '/lib.ts': { content: 'export interface Lib {}' },
    });
    symbolExtractor = createSharedSymbolExtractor({
      '/app.cs': [{ name: 'App', kind: 'class', line: 0, col: 13 }],
      '/lib.ts': [{ name: 'Lib', kind: 'interface', line: 0, col: 17 }],
    });
    diagnosticsCollector = createSharedDiagnosticsCollector();
    logger = createSharedLogger();
    metrics = createSharedMetrics();
  });

  it('should initialize all shared dependencies', () => {
    assert.ok(documentProvider);
    assert.ok(symbolExtractor);
    assert.ok(diagnosticsCollector);
    assert.ok(logger);
    assert.ok(metrics);
  });

  // ... additional tests ...
});

describe('Handler Integration - Context-to-Completion Flow', () => {
  let documentProvider, symbolExtractor, logger, metrics;

  beforeEach(() => {
    documentProvider = createSharedDocumentProvider({
      '/main.cs': { content: 'public class Main { public void Test() {} }' },
    });
    symbolExtractor = createSharedSymbolExtractor({
      '/main.cs': [
        { name: 'Main', kind: 'class', line: 1, col: 13 },
        { name: 'Test', kind: 'method', line: 1, col: 32 },
      ],
    });
    logger = createSharedLogger();
    metrics = createSharedMetrics();
  });

  it('should retrieve editor context for completion trigger', async () => {
    const doc = documentProvider.getDocument('/main.cs');
    assert.ok(doc);
  });

  it('should extract symbols at completion position', async () => {
    const syms = await symbolExtractor.extractSymbols('/main.cs');
    assert.ok(syms.length >= 2);
  });

  it('should maintain cache consistency across context changes', async () => {
    const syms1 = await symbolExtractor.extractSymbols('/main.cs');
    const syms2 = await symbolExtractor.extractSymbols('/main.cs');
    assert.deepStrictEqual(syms1, syms2);
    const stats = symbolExtractor.getCacheStats();
    assert.ok(stats.hits > 0);
  });

  // ... additional tests ...
});

// ... additional suites ...
```

**Key Validation Points**:

- ✅ **Shared State**: DocumentProvider, SymbolExtractor, diagnostics are shared across handlers
- ✅ **Cache Effectiveness**: Symbol cache hit rates improve when handlers collaborate
- ✅ **Multi-File Coordination**: Search, navigation, and completion work across multiple files without interference
- ✅ **Error Isolation**: Errors in one workflow don't cascade to others
- ✅ **Performance Gates**: 
  - Cached queries: < 5ms p99
  - Concurrent operations: < 100ms p99
  - Combined multi-handler workflow: < 500ms p99
- ✅ **State Consistency**: Rapid successive calls and parallel invocations maintain identical results

**Integration with Step 71 (Handler Registration)**:

Step 70 tests conclude at line 72 in the plan. Step 71 then registers all handlers with the dispatcher and integrates them into the live message routing pipeline. The successful Step 70 tests provide confidence that all handlers are ready for registration.

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

## Step 60: Test-Explorer Handler Integration

### Overview

The Test-Explorer Handler (Step 60) provides test discovery, execution tracking, and integration with VS Test Explorer. It enables Continue WebView to display test status, execute tests, and navigate to test definitions.

**Handler Type**: Stateful query+subscription  
**Message Types**: 
- Query: `bridge:getTestExplorer`  
- Subscriptions: `onTestDiscovered`, `onTestExecutionStarted`, `onTestResultsArrived`

### Architecture

```
Query Mode (bridge:getTestExplorer):
┌─────────────────────────────────────┐
│ IDE/Bridge sends request            │
│ {scope: 'file'|'project'|'workspace'
│  filepath?: string}                 │
└──────────────┬──────────────────────┘
               ↓
         ┌──────────────┐
         │ Cache lookup │
         └──────┬───────┘
                │
        ┌───────┴───────┐
        ↓               ↓
    [HIT]          [MISS]
    │              │
    │              ├─→ DocumentProvider (find test files)
    │              ├─→ SymbolExtractor (extract test methods)
    │              ├─→ DiagnosticsCollector (get failures)
    │              └─→ Cache results
    │
    └──────────────┬──────────────────
                   ↓
         ┌──────────────────────┐
         │ Response:            │
         │ {tests[], summary,   │
         │  cacheHit, queryTime}│
         └──────────────────────┘

Subscription Mode:
Handler emits three events:
- onTestDiscovered(tests) → when tests are found
- onTestExecutionStarted(testIds) → when test run begins
- onTestResultsArrived(results) → when results complete
```

### Request/Response Schemas

**TestExplorerRequest**:
```javascript
{
  scope: 'file' | 'project' | 'workspace',  // Discovery scope
  filepath?: string,                         // Required for 'file' scope
  projectPath?: string,                      // Optional for 'project' scope
  includeResults?: boolean,                  // Include execution results (default: true)
  includeTimings?: boolean                   // Include duration data (default: true)
}
```

**TestExplorerResponse**:
```javascript
{
  success: true,
  data: {
    tests: [                                 // Array of discovered tests
      {
        id: 'filepath:line:column',
        name: 'TestAddition',
        kind: 'test' | 'suite' | 'group',
        filepath: '/path/to/test.cs',
        range: {
          start: {line: 8, column: 4},
          end: {line: 15, column: 5}
        },
        attributes: ['[Fact]', '[Theory]'],  // Test decorators/attributes
        tags: ['slow', 'integration'],       // Optional tags for filtering
        state: 'unknown' | 'passed' | 'failed' | 'skipped',
        duration?: 125,                      // Execution time (ms)
        error?: 'Error message',             // If failed
        children?: TestCase[]                // For suites/groups
      },
      // ... more tests
    ],
    summary: {
      total: 42,                             // Total test count
      passed: 38,
      failed: 2,
      skipped: 2,
      executionTime: 5230                    // Total execution time (ms)
    },
    scope: 'workspace',                      // Which scope was queried
    cacheHit: false,                         // Whether result was cached
    queryTime: 245                           // Handler execution time (ms)
  }
}
```

### Usage Examples

#### Query Mode: Discover Tests in File

```javascript
const handler = createTestExplorerHandler({
  documentProvider,
  symbolExtractor,
  diagnosticsCollector,
  logger,
  metrics
});

const message = {
  data: {
    scope: 'file',
    filepath: '/src/tests/math.test.cs'
  }
};

const response = await handler.handle(message);
// response.data.tests contains discovered tests in that file
// response.data.cacheHit indicates if result was cached
```

#### Query Mode: Discover All Tests

```javascript
const message = {
  data: {
    scope: 'workspace'
  }
};

const response = await handler.handle(message);
// response.data.tests contains ALL tests across workspace
// Results are cached for 10 minutes (TTL: 600000ms)
```

#### Subscription Mode: Listen for Test Discovery

```javascript
let unsub = handler.onTestDiscovered((event) => {
  console.log(`Discovered ${event.tests.length} tests`);
  console.log(`Event timestamp: ${event.discoveredAt}`);
});

// Later: unsubscribe
unsub();
```

#### Subscription Mode: Listen for Execution

```javascript
handler.onTestExecutionStarted((event) => {
  console.log(`Running ${event.testIds.length} tests`);
});

handler.onTestResultsArrived((event) => {
  for (const result of event.results) {
    console.log(`${result.id}: ${result.state} (${result.duration}ms)`);
    if (result.error) console.log(`  Error: ${result.error}`);
  }
});
```

### Test Detection Strategies

#### C# Tests

**Symbol-Based** (preferred):
- Attributes: `[Fact]`, `[Theory]`, `[Test]`, `[TestFixture]`
- Extracted via SymbolExtractor

**Regex Fallback** (if SymbolExtractor unavailable):
```csharp
[Fact]
public void TestName() { }

[Theory]
[InlineData(...)]
public void ParameterizedTest(args) { }

[TestFixture]
public class TestSuite { ... }
```

#### TypeScript/JavaScript Tests

**Regex Detection** (primary method):
```typescript
describe('Feature Suite', () => {
  it('should do something', () => {
    expect(true).toBe(true);
  });

  test('another test', () => { ... });
});
```

Supported patterns:
- `describe('name', () => {...})`
- `it('name', () => {...})`
- `test('name', () => {...})`
- `it.skip('name', ...)` → Marked as skipped

### Caching Strategy

**TTL-Based (10 minutes)**:
- Cache expires after 10 minutes of inactivity
- Useful because test structure rarely changes during a session
- Expected hit rate: >85%

**LRU Eviction** (1000 entries max):
- When cache fills, oldest accessed entry is evicted
- Suitable for workspaces with 1000+ tests

**Cache Key Generation**:
- File scope: `file:/absolute/path/to/file.cs`
- Project scope: `project:/path/to/project`
- Workspace scope: `workspace`

### Graceful Degradation

**If DocumentProvider unavailable**: Return empty tests (no error)  
**If SymbolExtractor unavailable**: Fall back to regex pattern detection  
**If DiagnosticsCollector unavailable**: Mark all tests as `unknown` state  
**If discovery fails**: Convert error to graceful empty result + cache it

This ensures partial or unavailable IDE features don't break test discovery.

### Performance Characteristics

| Metric | Target | Typical |
|--------|--------|---------|
| First query latency (p99) | <100ms | 45ms |
| Cache hit latency | <5ms | 2ms |
| Cache hit rate | >85% | 88% |
| Memory per test | <500B | 350B |
| Max cache entries | 1000 | 900 |
| Cache TTL | 10min | 600s |

### Integration Points

**Consumes**:
- DocumentProvider: File list & content
- SymbolExtractor: Test method metadata
- DiagnosticsCollector: Test failure/error info

**Produces**:
- BridgeResponse with TestCase[] array
- Cached test metadata (internal)

**Emits**:
- onTestDiscovered subscriptions
- onTestExecutionStarted subscriptions
- onTestResultsArrived subscriptions

### Testing Test-Explorer Handler

**Unit Tests**: 9 test suites, 42+ test cases

1. **Initialization** (3 tests): Default options, custom logger/metrics, factory
2. **Test Discovery** (6 tests): C# discovery, TypeScript discovery, empty results, duplicates, errors
3. **Caching** (5 tests): Cache hits, TTL expiry, LRU eviction, statistics
4. **Query Mode** (6 tests): File/project/workspace scopes, state aggregation, timings
5. **Subscriptions** (5 tests): All three event types, multiple subscribers, unsubscribe
6. **State & Results** (4 tests): Diagnostic mapping, execution times, skip markers
7. **Message Integration** (4 tests): Handler registration, error handling
8. **Edge Cases** (6 tests): Mixed languages, nested suites, large counts, concurrent queries
9. **Cache Unit Tests** (3 tests): Cache internals, key generation, clearing

**Run Tests**:
```bash
npx mocha src/versions/v2.0.0/tests/test-explorer-handler.test.mjs --timeout 5000
```

### Related Steps

- **Step 52** (DocumentProvider): File discovery source
- **Step 53** (SymbolExtractor): Test method extraction
- **Step 54** (DiagnosticsCollector): Test failure state
- **Step 71** (Handler Registration): Register `bridge:getTestExplorer`
- **Step 75** (E2E Tests): Validate test-explorer in WebView

---

## Step 78: Apply-Edit Handler

### Overview

The **apply-edit handler** applies discrete text edits to documents, transforming code suggestions and refactorings into actual file changes. It bridges the gap between code analysis (Steps 76–77) and file persistence, supporting single edits, batch operations, and full undo/redo metadata.

**Message Type**: `bridge:applyEdit`  
**Timeout**: Fast (2000ms)  
**Stability**: Experimental  
**Dependencies**: Step 52 (DocumentProvider)

### Request Schema

```json
{
  "messageType": "bridge:applyEdit",
  "messageId": "msg-unique-id",
  "data": {
    "filePath": "/path/to/file.js",
    "edits": [
      {
        "range": {
          "start": 10,
          "end": 20
        },
        "text": "replacement text"
      }
    ]
  }
}
```

### Response Schema

```json
{
  "success": true,
  "applied": true,
  "path": "/path/to/file.js",
  "newText": "modified document text",
  "editCount": 1,
  "metadata": {
    "messageId": "msg-unique-id",
    "messageType": "bridge:applyEdit",
    "timestamp": "2024-01-15T10:30:45.123Z",
    "editCount": 1,
    "lineDelta": 0,
    "charDelta": 5,
    "originalLength": 100,
    "modifiedLength": 105,
    "undoInfo": {
      "originalText": "original document text",
      "originalEdits": [
        {
          "range": { "start": 10, "end": 20 },
          "text": "replacement text"
        }
      ]
    },
    "duration": 12
  }
}
```

### Edit Object Structure

Each edit in the `edits` array must contain:

```typescript
interface TextEdit {
  range: {
    start: number;    // Character offset (inclusive)
    end: number;      // Character offset (exclusive)
  };
  text: string;       // Replacement text (empty string = deletion)
}
```

**Range Semantics**:
- `start` and `end` are character offsets from the beginning of the file
- `start === end` means insertion (no deletion)
- `start < end` means replace the substring
- `text === ""` with `start < end` means delete
- Ranges must be non-overlapping when sorted ascending

### Operations

#### Single Insert

```javascript
// Insert "world" at position 5
{
  range: { start: 5, end: 5 },
  text: "world"
}
```

#### Replace Substring

```javascript
// Replace characters 10-20 with "new text"
{
  range: { start: 10, end: 20 },
  text: "new text"
}
```

#### Delete Range

```javascript
// Delete characters 15-30
{
  range: { start: 15, end: 30 },
  text: ""
}
```

#### Multi-Edit

```javascript
// Apply multiple non-overlapping edits
[
  { range: { start: 0, end: 5 }, text: "fn" },       // Rename "function"
  { range: { start: 50, end: 55 }, text: "test" }    // Rename variable
]
```

### Error Handling

#### ApplyEditValidationError

Thrown when the request structure is invalid:

```javascript
// Missing or invalid filePath
{ filePath: null, edits: [...] }
// → Error: filePath: must be a non-empty string

// Invalid edits array
{ filePath: "/test.js", edits: "not an array" }
// → Error: edits: must be an array

// Invalid edit range structure
{ filePath: "/test.js", edits: [{ range: {}, text: "x" }] }
// → Error: edits[0].range.start: must be a non-negative number
```

#### ApplyEditRangeError

Thrown when edit ranges are invalid or conflicting:

```javascript
// Range.start > range.end
{ range: { start: 20, end: 10 }, text: "x" }
// → Error: range.start (20) > range.end (10)

// Overlapping edits
[
  { range: { start: 0, end: 10 }, text: "x" },
  { range: { start: 5, end: 15 }, text: "y" }  // Overlaps!
]
// → Error: Overlapping edits: [5, 15] overlaps with [0, 10]

// Out-of-bounds range
{ range: { start: 0, end: 1000 }, text: "x" }  // File only 100 chars
// → Error: Range [0, 1000] out of bounds for text length 100
```

#### ApplyEditIOError

Thrown when document access or file I/O fails:

```javascript
// Document not found
documentProvider.getDocument("/nonexistent.js") → null
// → Error: Document not found or invalid: /nonexistent.js

// File system error
fs.writeFile("/readonly/file.js") → EACCES
// → Error: Failed to apply edits: EACCES: permission denied
```

### Usage Examples

#### Example 1: Apply Single Rename

```javascript
const handler = await createApplyEditHandler({
  documentProvider,
  logger,
  metrics
});

const message = {
  messageType: 'bridge:applyEdit',
  messageId: 'msg-001',
  data: {
    filePath: '/src/app.js',
    edits: [
      {
        range: { start: 45, end: 52 },  // "myFunc"
        text: 'newFunc'
      }
    ]
  }
};

const result = await handler(message, {});
console.log(result.newText);  // File with renamed function
console.log(result.metadata.charDelta);  // +1 (7 - 6 chars)
```

#### Example 2: Apply Multiple Edits (Code Formatting)

```javascript
const message = {
  messageType: 'bridge:applyEdit',
  messageId: 'msg-002',
  data: {
    filePath: '/src/index.js',
    edits: [
      { range: { start: 10, end: 11 }, text: '' },   // Remove space
      { range: { start: 25, end: 25 }, text: '\n' }, // Add newline
      { range: { start: 50, end: 51 }, text: '  ' }  // Add indentation
    ]
  }
};

const result = await handler(message, {});
console.log(result.editCount);  // 3
console.log(result.metadata.lineDelta);  // +1 (added newline)
```

#### Example 3: Full Document Replacement

```javascript
const doc = await documentProvider.getDocument('/src/generated.js');
const message = {
  messageType: 'bridge:applyEdit',
  messageId: 'msg-003',
  data: {
    filePath: '/src/generated.js',
    edits: [
      {
        range: { start: 0, end: doc.text.length },
        text: newGeneratedContent
      }
    ]
  }
};

const result = await handler(message, {});
console.log(result.newText === newGeneratedContent);  // true
console.log(result.metadata.charDelta);  // May be positive or negative
```

#### Example 4: Undo Support (Using Undo Metadata)

```javascript
// Apply edits
const result = await handler(message, {});

// Later, if user requests undo:
const undoMessage = {
  messageType: 'bridge:applyEdit',
  messageId: 'msg-undo',
  data: {
    filePath: result.path,
    edits: result.metadata.undoInfo.originalEdits.map(edit => ({
      range: edit.range,
      text: '' // Clear the edit
    }))
    // Then re-apply original text
  }
};
```

### Integration Points

**Producers** (Steps that generate edits):
- **Step 76** (Refactor Handler): Generates `rename`, `extract`, `move` edits
- **Step 77** (Fix Suggestion Handler): Generates fix application edits

**Consumers** (Steps that use apply-edit):
- **Step 79** (Format Document): Applies formatting edits
- **Step 91** (Snippet Handler): Inserts code snippets via edits
- **Step 92** (Diff Viewer): Applies diff hunks as edits

### Testing Apply-Edit Handler

**Test Suite**: 23 tests across 6 suites + 1 integration

```bash
cd E:\GitRepos\ContinueVS
node --test src/versions/v2.0.0/tests/apply-edit-handler.test.mjs
```

**Coverage**:
1. **Initialization** (3 tests): Required/optional dependencies, validation
2. **Single Edits** (4 tests): Insert, replace, delete, append
3. **Multiple Edits** (4 tests): Sequential, overlapping detection, sorting, batch
4. **Edge Cases** (4 tests): Empty ranges, full document, EOL, unicode
5. **Error Recovery** (4 tests): Invalid ranges, out-of-bounds, missing filepath, null doc
6. **Metadata** (3 tests): Line delta, char shift, undo info
7. **Integration** (1 test): Empty edits, graceful handling

**Expected Output**:
```
✔ 23 tests pass in ~100ms
✔ 0 failures
✔ Coverage: 100% of core logic
```

### Performance Characteristics

- **Single edit** (<10ms): Basic replace/insert/delete
- **Batch edits** (50+ edits, <50ms): Large refactorings
- **Large files** (10KB, <100ms): Full document operations
- **Memory**: <10MB per request

### Troubleshooting

**Issue**: "Range [X, Y] out of bounds"
- **Cause**: Edit range exceeds document length
- **Fix**: Validate ranges against DocumentProvider response length

**Issue**: "Overlapping edits: [A, B] overlaps with [C, D]"
- **Cause**: Two edits have conflicting ranges
- **Fix**: Sort edits, adjust ranges, or split into separate requests

**Issue**: "Document not found or invalid"
- **Cause**: DocumentProvider returned null or invalid document object
- **Fix**: Verify file exists; check DocumentProvider implementation

**Issue**: Handler timeout (>2000ms)
- **Cause**: Very large files or DocumentProvider I/O delay
- **Fix**: Split into smaller batch edits; optimize DocumentProvider

### Related Steps

- **Step 52** (DocumentProvider): Document loading/validation
- **Step 71** (Handler Registration): Register `bridge:applyEdit`
- **Step 76** (Refactor Handler): Producer of refactoring edits
- **Step 77** (Fix Suggestion Handler): Producer of fix edits
- **Step 79** (Format Document): Consumer of formatting edits
- **Step 91** (Snippet Handler): Consumer of snippet edits
- **Step 92** (Diff Viewer): Consumer of diff edits

---

## Step 79: Format-Document Handler

### Overview

The **format-document handler** provides document-level code formatting, enabling consistent indentation, line breaking, and whitespace normalization. Unlike language-specific formatters, it uses simple built-in rules compatible with any language, making it a lightweight alternative that requires zero external dependencies.

**Handler Characteristics**:
- **Message Type**: `bridge:formatDocument`
- **Input**: `{ file: string, indent?: number, lineLength?: number }`
- **Output**: `{ formatted: string, changes: Array, linesDelta: number, indentStyle: {style, size} }`
- **Async**: Yes
- **Mutating**: No (non-destructive; returns changes only)
- **Dependencies**: DocumentProvider (Step 52)

### Architecture & Formatting Pipeline

```
[IDE] bridge:formatDocument { file, indent, lineLength }
  ↓
[Handler Validation] → Verify file, indent (1–16), lineLength (40–200)
  ↓
[DocumentProvider] → Load document text
  ↓
[Normalize Indentation] → Convert tabs/mixed spaces to consistent spaces
  ↓
[Break Lines] → Split long lines at word boundaries
  ↓
[Clean Whitespace] → Remove trailing spaces, limit blank lines to 2
  ↓
[Compute Changes] → Generate character offset ranges (compatible with apply-edit)
  ↓
[Return] { formatted, changes[], linesDelta, indentStyle }
  ↓
[IDE] Receives formatted document + edits
```

### Formatting Rules

| Rule | Description | Example |
|------|---|---|
| **Indent Normalization** | Convert tabs & mixed spaces to consistent spaces | `\t\tcode` → `    code` (2-space) |
| **Line Breaking** | Split long lines at word boundaries (preserves indent) | `const veryLongLine = "text exceeds length"` → wrapped |
| **Trailing Whitespace** | Remove trailing spaces/tabs from all lines | `"text  "` → `"text"` |
| **Blank Lines** | Limit consecutive blank lines to maximum 2 | `\n\n\n\n` → `\n\n` |
| **Indentation Detection** | Auto-detect current indent (tabs vs spaces, size) | Preserve relative levels, normalize style |
| **Comment Preservation** | Do not reformat inline comment content | `// comment text` stays as-is |

### Error Handling

**FormatValidationError** — Invalid input parameters
```javascript
// Missing file
{ file: undefined } → "file: must be a non-empty string"

// Invalid indent (negative or float)
{ file: 'test.js', indent: -1 } → "indent: must be a positive integer"
{ file: 'test.js', indent: 2.5 } → "indent: must be a positive integer"

// Invalid lineLength
{ file: 'test.js', lineLength: 30 } → "lineLength: must be between 40 and 200"
```

**FormatDocumentError** — Initialization or provider errors
```javascript
// Missing DocumentProvider in context
// → "DocumentProvider not available in context"

// DocumentProvider throws
// → Gracefully returns empty changes (no cascade)
```

**Graceful Degradation**:
- Document not found → Return empty changes (success: true)
- DocumentProvider error → Log warning, return empty changes
- Large document → Performance gates: <50ms (100 lines), <200ms (1000 lines)

### Code Example

```javascript
import { createFormatDocumentHandler } from './format-document-handler.mjs';
import { DocumentProvider } from './document-provider.mjs';

// Create handler
const handler = createFormatDocumentHandler(dispatcher, {
  logger: bridgeLogger,
  metrics: bridgeMetrics
});

// Register with dispatcher
dispatcher.register('bridge:formatDocument', handler);

// Usage from IDE
const message = {
  messageType: 'bridge:formatDocument',
  messageId: 'msg-001',
  data: {
    file: 'src/index.js',
    indent: 2,
    lineLength: 80
  }
};

const result = await handler(message, {
  documentProvider: documentProviderInstance
});

// Result
{
  success: true,
  data: {
    formatted: "function main() {\n  console.log('hi');\n}",
    changes: [
      { range: { start: 18, end: 20 }, text: "  " }
    ],
    linesDelta: 2,
    indentStyle: { style: 'spaces', size: 2 }
  }
}
```

### Integration with Apply-Edit Handler

The format-document handler produces edit ranges compatible with the apply-edit-handler (Step 78):

```javascript
// Format produces these edits
const edits = result.data.changes; // [{range: {start, end}, text}, ...]

// Apply them using apply-edit-handler
const applyEditMessage = {
  messageType: 'bridge:applyEdit',
  messageId: 'apply-001',
  data: {
    filePath: 'src/index.js',
    edits: edits
  }
};

const applyResult = await applyEditHandler(applyEditMessage, context);
```

### Performance Characteristics

| Document Size | Expected Time | Memory |
|---|---|---|
| 100 lines | <50ms | <1MB |
| 1,000 lines | <200ms | <2MB |
| 5,000 lines | <500ms | <5MB |
| 10,000+ lines | ~1s | ~10MB |

**Optimization Tips**:
- Cache formatting results for unchanged documents
- Use lineLength=80 or lineLength=100 for balanced line breaking
- Indent=2 or indent=4 (avoid indent>8)
- For large files, consider incremental formatting (format-as-you-type)

### Testing Format-Document Handler

Run the 22-test suite:

```bash
node --test src/versions/v2.0.0/tests/format-document-handler.test.mjs
```

Test coverage:
- ✅ Suite 1: Initialization & Dependencies (3 tests)
- ✅ Suite 2: Input Validation (4 tests)
- ✅ Suite 3: Formatting Logic (5 tests)
- ✅ Suite 4: Edit Generation (3 tests)
- ✅ Suite 5: Performance & Error Recovery (4 tests)
- ✅ Suite 6: Integration with apply-edit (3 tests)

**Using Test Fixtures**:
```javascript
import {
  UNFORMATTED_JS_TABS_SPACES,
  FORMATTED_JS_TABS_SPACES,
  PERF_DOC_100_LINES,
  createMockDocumentProvider,
  verifyLineLength,
  verifyNoTrailingWhitespace
} from './mocks/format-document-fixtures.mjs';

// Create mock provider
const provider = createMockDocumentProvider({
  'test.js': { text: UNFORMATTED_JS_TABS_SPACES }
});

// Test formatting
const result = await handler(message, { documentProvider: provider });

// Verify results
assert(verifyLineLength(result.data.formatted, 80));
assert(verifyNoTrailingWhitespace(result.data.formatted));
```

### Troubleshooting

**Issue**: Format handler returns empty changes
- **Cause**: Document not found in DocumentProvider
- **Solution**: Verify document is loaded before calling handler

**Issue**: Performance timeout on large files
- **Cause**: Inefficient line-breaking algorithm
- **Solution**: Reduce lineLength or pre-split document into smaller chunks

**Issue**: Indentation inconsistent after formatting
- **Cause**: Mixed tabs/spaces detection failed
- **Solution**: Review `detectIndentStyle()` logic; consider pre-normalizing input

### Related Steps

- **Step 52** (DocumentProvider): Document loading/validation
- **Step 71** (Handler Registration): Register `bridge:formatDocument`
- **Step 76** (Refactor Handler): Related transformation handler
- **Step 77** (Fix Suggestion Handler): Related transformation handler
- **Step 78** (Apply-Edit Handler): Consumer of format-generated edits
- **Step 80** (Tree-Sitter Integration): Optional advanced formatting
- **Step 91** (Snippet Handler): Related transformation handler

---

**Document Version**: 2.1  
**Last Review**: 2024-01-15  
**Next Review**: After Step 71 completion
