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
