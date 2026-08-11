# ContinueVS Architecture Document

**Purpose:** High-level system architecture understanding for WPF + C# backend redesign.  
**Scope:** Visual, logical, and data flow analysis of Continue's TS/React architecture.  
**Output:** Subsystems, boundaries, async points, and control flow ready for .NET mapping.

---

## 1. System Overview

Continue is an in-IDE AI code assistant that coordinates three major planes:

```
┌─────────────────────────────────────────────────────┐
│                IDE (Visual Studio)                  │ ← Files, LSP, editor state
└──────────────────────┬──────────────────────────────┘
                       │ IPC (stdio or webview bridge)
                       ▼
┌─────────────────────────────────────────────────────┐
│              Core Backend (Node.js)                 │
│  • Config loading                                   │
│  • LLM orchestration (streaming, token counting)    │
│  • Tool routing (code edit, file ops, search)       │
│  • MCP server management                            │
│  • Session history                                  │
│  • Codebase indexing                                │
│  • Context retrieval                                │
└──────────────────────┬──────────────────────────────┘
                       │ Protocol (JSON messages)
                       ▼
┌─────────────────────────────────────────────────────┐
│            GUI (React in Webview)                   │
│  • Chat UI (prompt input, streaming responses)      │
│  • Config UI (models, tools, settings)              │
│  • Edit mode (in-editor code change UI)             │
│  • Theme rendering (VSCode variable injection)      │
└─────────────────────────────────────────────────────┘
```

**Key insight for .NET port:** 
- The **webview GUI layer** is thin and can be replaced with WPF (native .NET UI).
- The **Core Backend** is where most work lives (config, LLM, tools, indexing).
- The **IDE layer** is already abstracted via a protocol (can become .NET calls).

---

## 2. Subsystems Definition

### 2.1 Configuration Subsystem

**Responsibility:** Load, validate, mutate, and persist Continue's configuration.

**Input Triggers:**
- First IDE launch
- User opens config UI
- Config file on-disk changes (file watcher)
- User edits model settings

**Output Events:**
- `configUpdate` (GUI receives new models, tools, profiles)
- `profileChanged` (internal state change)
- `modelAdded`, `toolDisabled` (incremental updates)

**Key Components (TS):**
- `ConfigHandler.ts` — orchestrator; manages profile lifecycle, cascading reload
- `load.ts` — JSON parse; runtime → browser serialization
- `loadLocalAssistants.ts` — scan `.continue/` for agents, prompts
- `onboarding.ts` — template defaults for new users
- `applyToolOverrides.ts` — apply user config (disable, rename tools)

**Data Artifacts:**
- `~/.continue/config.json` (user config)
- `.continue/{agents,configs,prompts}/` (workspace-local definitions)

**Async Points:**
- File I/O (read config.json)
- Listener dispatch (config changed → indexer reacts)

**Coupling:**
- ← Depends on: LLM utilities (model detection), Tool definitions
- → Provides config to: Indexing subsystem, Tool routing, LLM selection

---

### 2.2 Tool Execution Subsystem

**Responsibility:** Route tool calls from UI to executors (built-in, MCP, HTTP), handle responses.

**Input Triggers:**
- User calls slash command (e.g., `/search`)
- User runs `/edit` in chat
- Core invokes a tool internally (context gathering)
- IDE requests file operations (read, write, search)

**Output Events:**
- `toolCallUpdate` (partial/streaming results to GUI)
- `toolCallDone` (final result)
- `editApplied` (file changes confirmed)

**Key Components (TS):**
- `callTool.ts` — router; dispatches to HTTP/MCP/built-in
- `builtIn.ts` — enum; read, edit, search, run, view, fetch, codebase
- `parseArgs.ts` — argument parsing (JSON deep-parse, type coercion)
- `mcpToolName.ts` — normalize MCP server/tool names

**Tool Types:**
1. **Built-in** (hardcoded): read file, edit file, search codebase, run subprocess
2. **MCP** (Model Context Protocol): agent-managed tools from external servers
3. **HTTP** (custom endpoints): tools via REST

**Async Points:**
- File I/O (read, write)
- Subprocess execution (git, npm)
- HTTP requests (MCP, custom tools)
- Streaming tool call results back to core

**Coupling:**
- ← Depends on: IDE (file ops, subprocess), MCP servers, Tool config
- → Provides results to: LLM orchestrator (context), Chat session (for display)

---

### 2.3 LLM Orchestration Subsystem

**Responsibility:** Execute LLM requests, handle streaming, count tokens, log interactions.

**Input Triggers:**
- User sends message in chat
- Agent/system prompts a completion
- Tool needs LLM-driven operation (e.g., rewrite code)

**Output Events:**
- `streamChunk` (partial LLM output to GUI)
- `streamDone` (completion finished)
- `toolCall` (LLM invokes a tool)
- `logInteraction` (saved to history and analytics)

**Key Components (TS):**
- `core.ts` (line 1460) — main orchestrator; message routing, session lifecycle
- `autodetect.ts` — model capability detection (streaming, tool use, function calling)
- `openaiTypeConverters.ts` — normalize chat messages, function schema
- `countTokens.ts` — token counting for context window management
- `logFormatter.ts`, `logger.ts` — interaction logging (human-readable + structured)

**Models Supported:**
- OpenAI, Anthropic, Ollama, Groq, Gemini, Azure, local endpoints

**Async Points:**
- LLM API calls (remote or local)
- Streaming response chunk handling
- Tool call interleaving (LLM → call tool → resume LLM)
- Token counting (may call embeddings API)

**Coupling:**
- ← Depends on: Config (model selection), Tool subsystem (for tool calling), Session history
- → Provides: Chat messages to GUI, tool invocations to Tool subsystem, session logs to History

---

### 2.4 Session & History Subsystem

**Responsibility:** Persist conversation history, manage session state, enable undo/redo.

**Input Triggers:**
- User sends message (new turn)
- User edits message (reroll)
- User clears chat
- User loads previous session

**Output Events:**
- `sessionUpdate` (GUI chat tree updated)
- `historySaved` (session persisted)
- `historyLoaded` (previous session restored)

**Key Components (TS):**
- History storage (likely in config or DB)
- Session state (session ID, turns, metadata)

**Data Artifacts:**
- Persisted session files in `.continue/` or local DB

**Async Points:**
- Session I/O (read, write)
- Session load/restore

**Coupling:**
- ← Depends on: LLM (for message content), Config (for session format)
- → Provides: Session history to GUI, context for new turns

---

### 2.5 Codebase Indexing Subsystem

**Responsibility:** Walk workspace files, compute embeddings, index into vector DB for RAG.

**Input Triggers:**
- First config load (auto-start index)
- User enables indexing
- Workspace changes (file system watcher)
- Config changes (new rules, ignore patterns)

**Output Events:**
- `indexProgress` (GUI progress bar)
- `indexingStatusUpdate` (current file, estimated time)
- `indexDone` (ready for context retrieval)

**Key Components (TS):**
- `CodebaseIndexer` — walks workspace, batches files, computes embeddings
- `DocsService` — indexes external documentation (web crawl, local markdown)
- `shouldIgnore.ts` — walk up tree checking `.gitignore`, `.continueignore`
- `walkDir.ts` — depth-first traversal with caching

**Async Points:**
- File system I/O (walk directories)
- Embeddings API calls (batch)
- SQLite writes
- Vector DB (LanceDB) writes

**Coupling:**
- ← Depends on: Config (ignore rules, embeddings model), IDE (file system access)
- → Provides: Indexed vectors to Context retrieval subsystem

---

### 2.6 Context Retrieval Subsystem

**Responsibility:** Fetch relevant code snippets for chat context (RAG).

**Input Triggers:**
- User sends message (automatic retrieval)
- User explicitly adds context (via UI menu)
- Agent queries context

**Output Events:**
- `addContextItem` (GUI shows included context)
- `contextItems` (returned to LLM for prompt building)

**Key Components (TS):**
- Context providers (code mention, recent edits, tags, docs)
- RAG query against indexed codebase + docs service
- Intelligent sorting and de-duplication

**Async Points:**
- Vector DB queries
- LLM-driven context ranking (optional)

**Coupling:**
- ← Depends on: Indexing (for vector DB), Config (providers enabled/disabled)
- → Provides: Context to LLM orchestrator

---

### 2.7 MCP Integration Subsystem

**Responsibility:** Spawn and manage external MCP servers; route their tools/resources.

**Input Triggers:**
- Config specifies MCP server
- User requests MCP resource
- MCP tool is invoked via UI

**Output Events:**
- `mcpServerStatus` (GUI shows connected servers)
- `toolCall` (MCP tool result)

**Key Components (TS):**
- MCP server lifecycle (spawn, subscribe to stdio)
- MCP tool registry and invocation
- Resource fetching (e.g., web page via MCP resource)

**Async Points:**
- Server startup
- Stdio message passing with MCP servers

**Coupling:**
- ← Depends on: Config (server definitions), Tool subsystem
- → Provides: MCP tools to Tool routing, resources to context

---

### 2.8 UI State & Theming Subsystem

**Responsibility:** Render chat, config pages, theme injection; coordinate with IDE.

**Input Triggers:**
- Chat message sent (display response stream)
- Config changed (refresh UI state)
- IDE theme changes
- IDE focus changes (switch active file)

**Output Events:**
- `navigateTo` (change route)
- `applyToFile` (apply edit result to code)
- `showFile` (jump to location)
- `exitEditMode` (exit in-editor edit)

**Key Components (TS):**
- `App.tsx` — router setup (memory history, routes)
- `Layout.tsx` — global listener dispatcher, session/edit mode orchestration
- Chat page — input, message stream rendering (TipTap editor)
- Config pages — model/tool settings, profiles
- Edit mode — in-editor view for code transformation
- `ThemePage.tsx` — theme debugging, VSCode/JetBrains theme injection

**State Management:**
- Redux store (ui, session, editState, config slices)
- Redux-persist (localStorage hydration)
- React contexts (IdeMessenger, MainEditor, Auth, LocalStorage)

**Async Points:**
- Webview message dispatch (IDE ↔ GUI)
- Route transitions
- Theme variable requests (JetBrains)

**Coupling:**
- ← Depends on: LLM (for streaming responses), Session (for history), Config (for UI content)
- → Provides: User input to Core, file edits to IDE, navigation to Core

---

### 2.9 IDE Abstraction Layer

**Responsibility:** Hide IDE-specific APIs behind a protocol interface.

**Input Triggers:**
- Core needs to read file (readFile message)
- Core needs to write code (writeFile message)
- Core queries LSP (gotoDefinition, getReferences)

**Output Events:**
- File contents returned
- Write confirmed
- LSP results returned

**Key Components (TS):**
- `IDE` interface — defines contract (readFile, writeFile, getProblems, etc.)
- `MessageIde` — remote proxy (wraps request/on functions, implements IDE)
- `ReverseMessageIde` — local listener (receives protocol messages, calls local IDE)
- Protocol message types (core.ts, ide.ts, webview.ts)
- `IMessenger` — abstraction over transport (in-process, stdio, webview)

**Protocol Message Categories:**
- **File ops** (readFile, writeFile, readRangeInFile, saveFile, deleteFile)
- **Git** (getBranch, getRepoName, getGitRootPath)
- **LSP** (gotoDefinition, getReferences, getDocumentSymbols, getProblems)
- **Subprocess** (subprocess)
- **Config** (refreshProfiles, getSerializedProfileInfo, addModel)
- **History** (list, delete, load, save)
- **Context** (getContextItems)
- **MCP** (reloadServer, getPrompt, setServerEnabled)
- **UI** (configUpdate, indexProgress, navigationEvents, themeEvents)

**Async Points:**
- All protocol messages are async (request/response)
- Messages can stream (tool calls, LLM completion)

**Coupling:**
- ← Depends on: Core orchestrator (sends requests), IDE implementation (responds)
- → Provides: Unified interface that hides IDE details

---

## 3. Data Flow Diagrams

### 3.1 Chat Message Flow (User → Core → LLM → Tool → Response)

```
┌──────────────────────────────────────────────────────┐
│ User types message in Chat UI (React)               │
└────────────────────────┬─────────────────────────────┘
                         │ dispatch(sendMessage)
                         ▼
┌──────────────────────────────────────────────────────┐
│ Redux thunk: streamChatThunk                         │
│ • Dispatch Redux state: sending=true                 │
└────────────────────────┬─────────────────────────────┘
                         │ messenger.request("llm/streamChat", {messages...})
                         ▼
┌──────────────────────────────────────────────────────┐
│ Core: LLM Orchestrator                               │
│ 1. Retrieves config (model selected)                 │
│ 2. Calls getContextItems (RAG)                       │
│ 3. Builds prompt (system + context + user message)   │
│ 4. Stream LLM completion                             │
└────────────────────────┬─────────────────────────────┘
                         │ stream chunks via generator
                         ▼
┌──────────────────────────────────────────────────────┐
│ Within LLM call:                                      │
│ • Token counting (within context window?)            │
│ • Function calling detection (model called a tool?)  │
│ • Tool invocation (Tool subsystem)                   │
│   → Tool result streamed back to LLM                 │
│ • Resume LLM completion                              │
└────────────────────────┬─────────────────────────────┘
                         │ chunk → messenger
                         ▼
┌──────────────────────────────────────────────────────┐
│ GUI receives chunk (ParallelListeners)               │
│ • Dispatch: addChatMessage({delta: chunk})           │
│ • Redux: append to current message                   │
│ • React: re-render chat window                       │
└────────────────────────┬─────────────────────────────┘
                         │ User sees response streaming
                         ▼
┌──────────────────────────────────────────────────────┐
│ LLM completes (stream ends)                          │
│ • Save session (history)                             │
│ • Send: logInteraction (telemetry)                   │
└──────────────────────────────────────────────────────┘
```

---

### 3.2 Agent Mode / Edit Flow

```
┌──────────────────────────────────────────────────────┐
│ User clicks "/edit" or agent enters edit mode        │
└────────────────────────┬─────────────────────────────┘
                         │ Layout listener: focusEdit
                         ▼
┌──────────────────────────────────────────────────────┐
│ Editor:                                               │
│ • Capture current file + selection                   │
│ • Display in-editor UI (TipTap with diff)            │
│ • Allow user to refine what to edit                  │
└────────────────────────┬─────────────────────────────┘
                         │ User submits
                         ▼
┌──────────────────────────────────────────────────────┐
│ Core: streamEditThunk                                │
│ • Prompt: "rewrite this code"                        │
│ • Call LLM (with edit tools enabled)                 │
│ • LLM calls: /edit tool                              │
└────────────────────────┬─────────────────────────────┘
                         │ Tool result: new code
                         ▼
┌──────────────────────────────────────────────────────┐
│ Tool subsystem: /edit handler                        │
│ • Diff old vs new                                    │
│ • Return diff to LLM                                 │
└────────────────────────┬─────────────────────────────┘
                         │ LLM refines if needed
                         ▼
┌──────────────────────────────────────────────────────┐
│ GUI: Display final diff in editor                    │
│ • User accepts or rejects                            │
└────────────────────────┬─────────────────────────────┘
                         │ if accept
                         ▼
┌──────────────────────────────────────────────────────┐
│ IDE: applyToFile                                     │
│ • Write new code to disk                             │
│ • Exit edit mode                                     │
└──────────────────────────────────────────────────────┘
```

---

### 3.3 Config Update Cascade

```
┌──────────────────────────────────────────────────────┐
│ Config file changes (user edits ~/.continue/config.json)
└────────────────────────┬─────────────────────────────┘
                         │ File watcher detects
                         ▼
┌──────────────────────────────────────────────────────┐
│ ConfigHandler: onConfigUpdate()                      │
│ • Reload config.json                                 │
│ • Notify listeners                                   │
└────────────┬──────────────────────────────┬──────────┘
             │                              │
             ▼                              ▼
    ┌─────────────────────┐    ┌───────────────────────┐
    │ IndexingSubsystem   │    │ LLM Config (model)    │
    │ • Check ignore rules│    │ • Update model select │
    │ • Reindex if needed │    │ • Model capability    │
    └─────────────────────┘    └───────────────────────┘
             │
             ▼
    ┌─────────────────────┐
    │ Broadcast message   │
    │ messenger.send()    │
    │ "configUpdate"      │
    └─────────┬───────────┘
              │
              ▼
    ┌─────────────────────┐
    │ GUI (Layout.tsx)    │
    │ • Dispatch Redux    │
    │   updateConfig()    │
    │ • Re-render UI      │
    └─────────────────────┘
```

---

## 4. Key Boundaries & Async Points

### 4.1 Synchronous vs. Asynchronous Boundaries

| Boundary | Direction | Protocol | Sync/Async |
|----------|-----------|----------|------------|
| IDE ↔ Core | Bidirectional | stdio JSON (Node.js) / IPC | Async (request/response) |
| Core ↔ GUI | Bidirectional | webview message post | Async (send/listen) |
| Core ↔ LLM API | Request/Response | HTTP REST | Async (streaming) |
| Core ↔ MCP Server | Bidirectional | stdio JSON | Async (streaming) |
| Core ↔ File system | Request | fs API | Async (callbacks) |
| IDE ↔ IDE APIs | Sync/Async | Direct DTE calls (VSIX) | Mixed (most async) |

### 4.2 Critical Async Operations

**High Latency (≥100ms expected):**
- LLM inference (500ms–5s or more)
- Codebase indexing (seconds to minutes)
- Embeddings generation (batch calls)
- MCP server startup
- Remote file I/O

**Medium Latency (10–100ms):**
- LSP queries (gotoDefinition, getReferences)
- Local vector DB queries
- Subprocess execution
- Configuration reload

**Low Latency (<10ms):**
- In-memory config access
- Redux state updates
- Message routing

---

## 5. State Management & Persistence

### 5.1 State Locations

| State | Owner | Scope | Persistence |
|-------|-------|-------|-------------|
| **Session (chat history)** | Core | Per-session | Saved to file (loadable later) |
| **Config (models, tools, settings)** | ConfigHandler | Global | ~/.continue/config.json |
| **UI State (current route, dialogs)** | Redux | GUI | localStorage (redux-persist) |
| **Edit Mode Buffer** | Redux (editState) | Per-session | Memory (lost on close) |
| **Index Data** | CodebaseIndexer + DocsService | Global | SQLite + LanceDB (persistent) |
| **Context Provider Cache** | Core | In-memory | Lost on restart |

### 5.2 Redux Store Structure (GUI)

```
{
  ui: {
    // Current page/route
    currentPage,
    // Dialog state
    showDialog, dialogMessage, dialogButtons,
    // Edit mode visual state
    isInEdit, codeToEdit, codeEditApplied,
  },
  session: {
    // Current session ID and turns
    sessionId, messages, context,
    // Metadata
    title, timestamp,
  },
  config: {
    // Config snapshot (from Core)
    models, selectedModel, tools, profiles,
  },
  profiles: {
    // Profiles for model/tool grouping
    ...
  },
}
```

---

## 6. Message Protocol Summary

### 6.1 High-Level Categories

| Category | Origin | Destination | Example | Pattern |
|----------|--------|-------------|---------|---------|
| **LLM Chat** | GUI | Core | `llm/streamChat` | Request → streaming response |
| **Config** | Core/IDE | GUI | `configUpdate` | Send (no response) |
| **History** | GUI | Core | `history/load` | Request → response |
| **Tools** | Core | IDE/Tool runners | `tools/call` | Request → response |
| **Context** | Core | GUI | `context/getContextItems` | Request → response |
| **File Ops** | Core | IDE | `readFile`, `writeFile` | Request → response |
| **UI Navigation** | IDE | Core | `navigateTo` | Send (no response) |
| **Indexing** | Core | GUI | `indexProgress` | Send (streaming) |
| **MCP** | Core | MCP servers | `subprocess` (spawn) | Bidirectional stream |

### 6.2 Messenger Pattern

All protocol communication uses `IMessenger<ToProtocol, FromProtocol>`:

```
// From GUI to Core
messenger.request("llm/streamChat", { messages })
  .then(streamIterator => {
    // consume async iterator
    for await (const chunk of streamIterator) { ... }
  })

// From Core to GUI (unsolicited)
messenger.send("configUpdate", { models, tools })  // fire-and-forget

// From Core, routing to IDE
messenger.request("readFile", { filepath })
  .then(contents => ...)
```

---

## 7. Error Handling Strategy

### 7.1 Error Classes

- **LLMError** — LLM API failure (rate limit, invalid API key, model not available)
- **ToolError** — Tool execution failure (file not found, subprocess error)
- **ConfigError** — Configuration invalid (bad YAML, missing required field)
- **IndexingError** — Indexing operation failure (SQLite locked, embeddings API down)
- **NetworkError** — Network issues (timeout, no connectivity)

### 7.2 Propagation

- Errors from tool calls are returned as error objects in tool result
- Errors from LLM API are either retried or surfaced to user
- Errors from indexing are logged and session continues (non-fatal)
- Critical config errors prevent startup (caught early)

---

## 8. Extension / Integration Points for .NET Port

### 8.1 Direct Mappings

| TS Component | .NET Equivalent | Notes |
|--------------|-----------------|-------|
| ConfigHandler | ConfigService (class) | Implement IConfigService |
| LLM orchestrator | LlmService (class) | Async Task-based streaming |
| Tool routing | ToolService (class) | Route to handlers |
| Indexing | IndexingService (class) | Async file walk + batch ops |
| Session history | SessionRepository (class) | Use SqliteService or EF |
| Redux store | ViewModel + ObservableCollection | MVVM Light + data binding |
| Webview messenger | MessengerService (interface) | Can impl in-process or cross-process |
| React component | WPF UserControl | Data binding instead of JSX |

### 8.2 Protocol Remains (JSON over IPC/HTTP)

- Continue to use message-based communication for IDE ↔ Core
- Platform changes (node →  .NET), but message shapes are the same
- Allows future replacement of GUI independently

### 8.3 New Boundaries in .NET

- **MVVM Binding layer** — ViewModel observes backend service changes
- **DI Container** — Inject services into ViewModels
- **Async Task patterns** — Replace Promise generators with async/await
- **WPF Styling** — Replace Tailwind/CSS with XAML brushes and data templates

---

## 9. Risks & Open Questions for Porting

### 9.1 Risks

- **Token counting library** — TS has `js-tiktoken`; .NET needs equivalent or shimming
- **Embeddings batching** — Logic lives in indexer; must preserve batch semantics
- **Stream handling** — TS async generators; C# uses IAsyncEnumerable
- **MCP subprocess** — Spawning node (or any process) from .NET; WinForms/WPF considerations
- **LanceDB vector DB** — TS uses npm package; .NET may need wrapper or SQL alternative
- **Redux DevTools** — TS has time-travel debugging; .NET MVVM less instrumented by default

### 9.2 Open Questions

1. **MCP server spawning**: Should Core spawn MCP processes, or should IDE host and bridge them?
2. **Vector DB**: Use LanceDB .NET bindings (if available) or fall back to SQLite FTS5 for search?
3. **Theme variables**: How to handle VSCode theme color injection in WPF?
4. **Edit mode**: Implement in-editor diff view as WPF control or keep minimal and show in separate pane?
5. **Message transport**: Keep existing Node.js Core as separate process, or fully port to .NET?

---

## 10. Summary Table: Subsystems at a Glance

| Subsystem | Owner | Key Dependency | Output | Async? | MVP Priority |
|-----------|-------|-----------------|--------|--------|-------------|
| Config | ConfigHandler | File I/O | configUpdate | ✓ | ⭐⭐⭐ |
| Tool Exec | callTool.ts | IDE, MCP | toolCall result | ✓ | ⭐⭐⭐ |
| LLM Orch | core.ts | Config, Tools, Session | streamChunk | ✓ | ⭐⭐⭐ |
| Session | Session storage | Config | sessionUpdate | ✓ | ⭐⭐ |
| Indexing | CodebaseIndexer | Config, IDE | indexProgress | ✓ | ⭐⭐ |
| Context | Context providers | Indexing | contextItems | ✓ | ⭐⭐ |
| MCP | MCP manager | Config, Tools | toolCall | ✓ | ⭐ |
| UI State | Redux | Core | UI updates | ✓ | ⭐⭐⭐ |
| IDE Abstraction | IMessenger | IDE implementation | protocol responses | ✓ | ⭐⭐⭐ |

**MVP Focus (⭐⭐⭐):** Config → Tool Exec → LLM Orch → UI State → IDE Abstraction

---

**End of Architecture Document**
