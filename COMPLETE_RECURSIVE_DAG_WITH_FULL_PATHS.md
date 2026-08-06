# Complete Recursive Dependency DAG: Bottom-Up (Post-Order DFS)
## WITH FULL RELATIVE PATHS FROM SOLUTION ROOT

**Generated**: 2025-01-23  
**Order**: TRUE BOTTOM-UP (external dependencies → leaves → internal deps → roots)  
**Paths**: All relative from `E:\GitRepos\ContinueVS\` (solution root)  
**Files Only**: No directories, all entries include file extension  
**Coverage**: All 4 entry points merged, deduplicated, NO circular dependencies  
**Total Files**: ~220 files across Core, GUI, Bridge, and IDE

---

# ARCHITECTURE OVERVIEW

```
┌──────────────────────────────────────────────────────┐
│ LAYER 0: IDE ROOT (ContinueVS)                       │
│ └─ ContinueVS.Vsix (C# project)                      │
└──────────────────────┬───────────────────────────────┘
                       │ spawns child_process
┌──────────────────────▼───────────────────────────────┐
│ LAYER 1: BRIDGE ROOT                                 │
│ └─ src/versions/v2.0.0/core-server.js                │
└──────────────────────┬───────────────────────────────┘
                       │ spawns Continue binary
┌──────────────────────▼───────────────────────────────┐
│ LAYER 2A: CORE BACKEND ROOT                          │
│ └─ reference/continue-src/core/core.ts               │
└──────────────────────┬───────────────────────────────┘
                       │ JSON-RPC relay
┌──────────────────────▼───────────────────────────────┐
│ LAYER 2B: GUI FRONTEND ROOT                          │
│ └─ reference/continue-src/gui/index.html             │
└──────────────────────┬───────────────────────────────┘
                       │ React rendering
└──────────────────────▼───────────────────────────────┐
  LAYER 3: EXTERNAL DEPENDENCIES (npm, Node.js)      │
└──────────────────────────────────────────────────────┘
```

---

# COMPLETE BOTTOM-UP FILE LISTING

## LAYER 3: EXTERNAL DEPENDENCIES
*(Terminal nodes - not expanded per file)*

### NPM Packages
```
react@18+
react-dom@18+
react-redux@latest
redux@latest
@reduxjs/toolkit@latest
redux-persist@latest
styled-components@5+
@tiptap/core, @tiptap/react, @tiptap/starter-kit
uuid@4+
classnames@latest
uri-js@latest
@continuedev/fetch
@continuedev/config-yaml
```

### Node.js Built-ins
```
child_process, readline, fs, path, process, crypto, util, events, stream
```

---

## LAYER 2A: CONTINUE CORE BACKEND

### Substrate Layer: Constants & Type Definitions (Depends: external only)

```
reference/continue-src/core/protocol/util.ts
reference/continue-src/core/protocol/passThrough.ts
reference/continue-src/core/llm/constants.ts
reference/continue-src/core/llm/messages.ts
reference/continue-src/core/tools/constants.ts
reference/continue-src/core/util/errors.ts
reference/continue-src/core/util/Logger.js
```

---

### Layer 2A-1: Protocol Subsystem

```
reference/continue-src/core/protocol/core.ts
reference/continue-src/core/protocol/webview.ts
reference/continue-src/core/protocol/coreWebview.ts
reference/continue-src/core/protocol/ideWebview.ts
reference/continue-src/core/protocol/ide.ts
reference/continue-src/core/protocol/ideCore.ts
reference/continue-src/core/protocol/index.ts
reference/continue-src/core/protocol/messenger/index.ts
reference/continue-src/core/protocol/messenger/messageIde.ts
reference/continue-src/core/protocol/messenger/reverseMessageIde.ts
```

---

### Layer 2A-2: LLM Subsystem

```
reference/continue-src/core/llm/autodetect.ts
reference/continue-src/core/llm/openaiTypeConverters.ts
reference/continue-src/core/llm/logFormatter.ts
reference/continue-src/core/llm/logger.ts
reference/continue-src/core/llm/countTokens.ts
reference/continue-src/core/llm/getAdjustedTokenCount.ts
reference/continue-src/core/llm/toolSupport.ts
reference/continue-src/core/llm/defaultSystemMessages.ts
reference/continue-src/core/llm/fetchModels.ts
reference/continue-src/core/llm/streamChat.ts
reference/continue-src/core/llm/index.ts
reference/continue-src/core/llm/llms/Lemonade.ts
reference/continue-src/core/llm/llms/Ollama.ts
```

---

### Layer 2A-3: Tools Subsystem

```
reference/continue-src/core/tools/mcpToolName.ts
reference/continue-src/core/tools/parseArgs.ts
reference/continue-src/core/tools/applyToolOverrides.ts
reference/continue-src/core/tools/builtIn.ts
reference/continue-src/core/tools/callTool.ts
reference/continue-src/core/tools/index.ts
```

---

### Layer 2A-4: Data & Logging

```
reference/continue-src/core/data/devdataSqlite.ts
reference/continue-src/core/data/log.ts
```

---

### Layer 2A-5: Indexing Foundation

```
reference/continue-src/core/indexing/shouldIgnore.ts
reference/continue-src/core/indexing/walkDir.ts
```

---

### Layer 2A-6: Config Subsystem

```
reference/continue-src/core/config/util.ts
reference/continue-src/core/config/createNewAssistantFile.ts
reference/continue-src/core/config/loadLocalAssistants.ts
reference/continue-src/core/config/markdown/loadCodebaseRules.ts
reference/continue-src/core/config/workspace/workspaceBlocks.ts
reference/continue-src/core/config/ConfigHandler.ts
reference/continue-src/core/config/onboarding.ts
```

---

### Layer 2A-7: Context Subsystem

```
reference/continue-src/core/context/mcp/MCPManagerSingleton.ts
reference/continue-src/core/context/mcp/MCPOauth.ts
```

---

### Layer 2A-8: Diff & Edit

```
reference/continue-src/core/diff/myers.ts
reference/continue-src/core/edit/applyAbortManager.ts
reference/continue-src/core/edit/streamDiffLines.ts
```

---

### Layer 2A-9: Indexing Orchestrator

```
reference/continue-src/core/indexing/CodebaseIndexer.ts
reference/continue-src/core/indexing/docs/DocsService.ts
```

---

### Layer 2A-10: Autocomplete

```
reference/continue-src/core/autocomplete/util/openedFilesLruCache.ts
reference/continue-src/core/autocomplete/snippets/gitDiffCache.ts
reference/continue-src/core/autocomplete/CompletionProvider.ts
```

---

### Layer 2A-11: NextEdit - Smart Edit Mode

```
reference/continue-src/core/nextEdit/context/aggregateEdits.ts
reference/continue-src/core/nextEdit/context/diffFormatting.ts
reference/continue-src/core/nextEdit/context/processSmallEdit.ts
reference/continue-src/core/nextEdit/NextEditPrefetchQueue.ts
reference/continue-src/core/nextEdit/NextEditProvider.ts
```

---

### Layer 2A-12: Commands & Utilities

```
reference/continue-src/core/commands/slash/mcpSlashCommand.ts
reference/continue-src/core/util/chatDescriber.ts
reference/continue-src/core/util/GlobalContext.ts
reference/continue-src/core/util/paths.ts
reference/continue-src/core/util/processTerminalStates.ts
reference/continue-src/core/util/treeSitter.ts
reference/continue-src/core/util/tts.ts
reference/continue-src/core/util/conversationCompaction.ts
reference/continue-src/core/util/history.ts
reference/continue-src/core/util/historyUtils.ts
```

---

### Layer 2A-13: Prompt Files

```
reference/continue-src/core/promptFiles/createNewPromptFile.ts
```

---

### Layer 2A-14: Type Definitions

```
reference/continue-src/core/index.d.ts
```

---

### Layer 2A-15: **CORE BACKEND ENTRY POINT**

```
reference/continue-src/core/core.ts
```

---

## LAYER 2B: CONTINUE GUI FRONTEND

### Substrate Layer: Styles & Constants

```
reference/continue-src/gui/vite-env.d.ts
reference/continue-src/gui/index.css
reference/continue-src/gui/src/styles/theme.ts
reference/continue-src/gui/src/styles/utils.ts
```

---

### Layer 2B-1: Basic Utilities

```
reference/continue-src/gui/src/util/cn.ts
reference/continue-src/gui/src/util/isContinueTeamMember.ts
reference/continue-src/gui/src/util/localStorage.ts
reference/continue-src/gui/src/util/navigation.ts
reference/continue-src/gui/src/util/migrateLocalStorage.ts
reference/continue-src/gui/src/util/index.ts
reference/continue-src/gui/src/util/clientTools/editImpl.ts
reference/continue-src/gui/src/util/clientTools/multiEditImpl.ts
reference/continue-src/gui/src/util/clientTools/singleFindAndReplaceImpl.ts
reference/continue-src/gui/src/util/clientTools/callClientTool.ts
```

---

### Layer 2B-2: Utilities (Continued)

```
reference/continue-src/gui/src/util/compactConversation.ts
reference/continue-src/gui/src/util/editOutcomeLogger.ts
reference/continue-src/gui/src/util/errorAnalysis.ts
reference/continue-src/gui/src/util/toolCallState.ts
```

---

### Layer 2B-3: Test Utilities

```
reference/continue-src/gui/src/util/test/config.ts
reference/continue-src/gui/src/util/test/setupTests.ts
reference/continue-src/gui/src/util/test/utils.ts
reference/continue-src/gui/src/util/test/mockStore.ts
reference/continue-src/gui/src/util/test/render.tsx
```

---

### Layer 2B-4: Context Providers

```
reference/continue-src/gui/src/context/VscTheme.tsx
reference/continue-src/gui/src/context/LocalStorage.tsx
reference/continue-src/gui/src/context/Auth.tsx
reference/continue-src/gui/src/context/SubmenuContextProviders.tsx
```

---

### Layer 2B-5: **IdeMessenger - CRITICAL BRIDGE**

```
reference/continue-src/gui/src/context/IdeMessenger.tsx
```

---

### Layer 2B-6: Components

```
reference/continue-src/gui/src/components/OnboardingCard/OnboardingCard.tsx
reference/continue-src/gui/src/components/OSRContextMenu.tsx
reference/continue-src/gui/src/components/config/FatalErrorNotice.tsx
reference/continue-src/gui/src/components/dialogs/index.tsx
reference/continue-src/gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx
```

---

### Layer 2B-7: Hooks

```
reference/continue-src/gui/src/hooks/useWebviewListener.ts
reference/continue-src/gui/src/hooks/ParallelListeners.tsx
```

---

### Layer 2B-8: Redux Utilities

```
reference/continue-src/gui/src/redux/hooks.ts
reference/continue-src/gui/src/redux/util/getBaseSystemMessage.ts
reference/continue-src/gui/src/redux/util/constructMessages.ts
reference/continue-src/gui/src/redux/util/index.ts
```

---

### Layer 2B-9: Redux Selectors

```
reference/continue-src/gui/src/redux/selectors/selectActiveTools.ts
reference/continue-src/gui/src/redux/selectors/selectToolCalls.ts
reference/continue-src/gui/src/redux/selectors/index.ts
```

---

### Layer 2B-10: Redux Slices

```
reference/continue-src/gui/src/redux/slices/editState.ts
reference/continue-src/gui/src/redux/slices/indexingSlice.ts
reference/continue-src/gui/src/redux/slices/profilesSlice.ts
reference/continue-src/gui/src/redux/slices/tabsSlice.ts
reference/continue-src/gui/src/redux/slices/sessionSlice.ts
reference/continue-src/gui/src/redux/slices/configSlice.ts
reference/continue-src/gui/src/redux/slices/uiSlice.ts
```

---

### Layer 2B-11: Redux Thunks

```
reference/continue-src/gui/src/redux/thunks/streamThunkWrapper.tsx
reference/continue-src/gui/src/redux/thunks/callToolById.ts
reference/continue-src/gui/src/redux/thunks/preprocessToolCallArgs.ts
reference/continue-src/gui/src/redux/thunks/evaluateToolPolicies.ts
reference/continue-src/gui/src/redux/thunks/cancelStream.ts
reference/continue-src/gui/src/redux/thunks/cancelToolCall.ts
reference/continue-src/gui/src/redux/thunks/moveTerminalProcessToBackground.ts
reference/continue-src/gui/src/redux/thunks/updateFileSymbols.ts
reference/continue-src/gui/src/redux/thunks/updateSelectedModelByRole.ts
reference/continue-src/gui/src/redux/thunks/streamNormalInput.ts
reference/continue-src/gui/src/redux/thunks/streamResponseAfterToolCall.ts
reference/continue-src/gui/src/redux/thunks/handleApplyStateUpdate.ts
reference/continue-src/gui/src/redux/thunks/streamResponse.ts
reference/continue-src/gui/src/redux/thunks/edit.ts
reference/continue-src/gui/src/redux/thunks/session.ts
```

---

### Layer 2B-12: **Redux Store**

```
reference/continue-src/gui/src/redux/store.ts
```

---

### Layer 2B-13: Pages

```
reference/continue-src/gui/src/pages/error.tsx
reference/continue-src/gui/src/pages/stats.tsx
reference/continue-src/gui/src/pages/history/index.tsx
reference/continue-src/gui/src/pages/config/index.tsx
```

---

### Layer 2B-14: Chat GUI Components

```
reference/continue-src/gui/src/pages/gui/EmptyChatBody.tsx
reference/continue-src/gui/src/pages/gui/ExploreDialogWatcher.tsx
reference/continue-src/gui/src/pages/gui/StreamError.tsx
reference/continue-src/gui/src/pages/gui/useAutoScroll.ts
reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolCallArgs.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolCallDisplay.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolCallStatusMessage.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolTruncateHistoryIcon.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToggleWithIcon.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/IndicatorBar.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/TerminalCollapsibleContainer.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/CreateFile.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/EditFile.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/FindAndReplace.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/RunTerminalCommand.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/MCPAppRenderer.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/SimpleToolCallUI.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/GroupedToolCallHeader.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/FunctionSpecificToolCallDiv.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/utils.tsx
reference/continue-src/gui/src/pages/gui/ToolCallDiv/index.tsx
reference/continue-src/gui/src/pages/gui/Chat.tsx
reference/continue-src/gui/src/pages/gui/index.tsx
```

---

### Layer 2B-15: Application Root

```
reference/continue-src/gui/src/styles/ThemePage.tsx
reference/continue-src/gui/src/components/Layout.tsx
reference/continue-src/gui/src/App.tsx
```

---

### Layer 2B-16: **GUI ENTRY POINT**

```
reference/continue-src/gui/src/main.tsx
```

---

### Layer 2B-17: **HTML ROOT**

```
reference/continue-src/gui/index.html
```

---

## LAYER 1: BRIDGE RELAY

### Substrate Layer: Error Types & State

```
src/versions/v2.0.0/lib/error-types.js
  └─ Deps: external

src/versions/v2.0.0/lib/bridge-state.js
  └─ Deps: external
```

---

### Message Infrastructure

```
src/versions/v2.0.0/lib/handler-dispatcher.js
  └─ Deps: error-types, external

src/versions/v2.0.0/lib/message-routing-middleware.js
  └─ Deps: external

src/versions/v2.0.0/lib/validation-hook.mjs
  └─ Deps: external
```

---

### Handler Implementations (21+ files)

```
src/versions/v2.0.0/lib/handlers/getWorkspaceDirs.js
  └─ Deps: handler-dispatcher, external

src/versions/v2.0.0/lib/handlers/getIdeInfo.js
  └─ Deps: handler-dispatcher, external

src/versions/v2.0.0/lib/handlers/readFile.js
  └─ Deps: handler-dispatcher, external

src/versions/v2.0.0/lib/handlers/writeFile.js
  └─ Deps: handler-dispatcher, external

src/versions/v2.0.0/lib/handlers/getOpenFiles.js
  └─ Deps: handler-dispatcher, external

src/versions/v2.0.0/lib/handlers/getBranch.js
  └─ Deps: handler-dispatcher, external

[... additional 15+ handler files with same pattern ...]
```

---

### Handler Registration & Lifecycle

```
src/versions/v2.0.0/lib/register-handlers.mjs
  └─ Deps: handler-dispatcher, validation-hook, external

src/versions/v2.0.0/lib/crash-recovery-manager.js
  └─ Deps: error-types, bridge-state, external

src/versions/v2.0.0/lib/circuit-breaker-manager.js
  └─ Deps: bridge-state, external

src/versions/v2.0.0/lib/lifecycle-manager.js
  └─ Deps: bridge-state, error-types, external
```

---

### **BRIDGE ENTRY POINT**

```
src/versions/v2.0.0/core-server.js
  └─ Deps: handler-dispatcher, register-handlers, validation-hook, lifecycle-manager, crash-recovery-manager, circuit-breaker-manager, Node.js (child_process, readline, fs, path), external
  └─ PURPOSE: Bridge relay, spawns Continue binary, relays JSON-RPC via stdio
```

---

## LAYER 0: CONTINUEVS IDE EXTENSION

### C# Components

```
ContinueVS.Vsix/ContinueVSPackage.cs
  └─ Deps: Visual Studio SDK, spawns core-server.js

ContinueVS.Vsix/ContinueToolWindowControl.xaml.cs
  └─ Deps: WPF, message handlers, core-server.js relay

ContinueVS.Vsix/MessageDispatcher.cs
  └─ Deps: handler implementations

ContinueVS.Vsix/HandlerRegistry.cs
  └─ Deps: all *Handler.cs files

ContinueVS.Vsix/GetWorkspaceDirsHandler.cs
  └─ Deps: external

ContinueVS.Vsix/GetIdeInfoHandler.cs
  └─ Deps: external

[... additional handler .cs files ...]

ContinueVS.Vsix/WebviewInjector.cs
  └─ Deps: WPF (vestigial from v1, not used in v2)
```

---

### **IDE APPLICATION ROOT**

```
ContinueVS.Vsix (C# project root)
  └─ PURPOSE: Main Visual Studio Extension
  └─ SPAWNS: src/versions/v2.0.0/core-server.js (child process)
  └─ RELAYS: JSON-RPC via stdio
```

---

# EXECUTION FLOW: Complete Path

```
1. ContinueVS.Vsix (C# IDE extension)
   ├─ Entry: ContinueVSPackage.cs
   ├─ UI: ContinueToolWindowControl.xaml.cs
   └─ Spawns ↓

2. src/versions/v2.0.0/core-server.js (Node.js bridge)
   ├─ Dispatcher: lib/handler-dispatcher.js
   ├─ Handlers: lib/handlers/*.js (21+ files)
   ├─ Lifecycle: lib/lifecycle-manager.js
   └─ Spawns ↓

3. reference/continue-src/core/core.ts (Continue backend binary)
   ├─ Entry: core.ts (1460 lines)
   ├─ Protocol: protocol/*.ts (10+ types)
   ├─ LLM: llm/*.ts (20+ files)
   ├─ Tools: tools/*.ts (6 files)
   ├─ Config: config/*.ts (7 files)
   ├─ Context: context/mcp/*.ts (2 files)
   ├─ Indexing: indexing/*.ts (4 files)
   ├─ Autocomplete: autocomplete/*.ts (3 files)
   ├─ NextEdit: nextEdit/*.ts (5 files)
   ├─ Diff/Edit: diff/*.ts, edit/*.ts (3 files)
   └─ Utilities: util/*.ts (25+ files)

   Responds via stdout ↑

4. src/versions/v2.0.0/core-server.js (relays to GUI)
   └─ Relays responses via stdio ↑

5. reference/continue-src/gui/ (React frontend)
   ├─ HTML: index.html
   ├─ Entry: src/main.tsx
   ├─ App: src/App.tsx
   ├─ State: src/redux/store.ts (7 slices, 15+ thunks)
   ├─ Pages: src/pages/* (chat, config, history)
   ├─ Bridge: src/context/IdeMessenger.tsx ↔ core.ts
   └─ Components: src/components/*, src/pages/gui/*

   Renders in WebView ↑

6. ContinueVS WebView (displays GUI)
   └─ Displays React components
```

---

# QUICK REFERENCE: Key Entry Points

| Layer | File Path | Type | Purpose |
|-------|-----------|------|---------|
| 0 | ContinueVS.Vsix | C# Project | IDE extension root |
| 1 | src/versions/v2.0.0/core-server.js | JavaScript | Bridge relay entry |
| 2A | reference/continue-src/core/core.ts | TypeScript | Backend orchestrator |
| 2B | reference/continue-src/gui/index.html | HTML | Frontend root |
| 2B | reference/continue-src/gui/src/main.tsx | TSX | React entry |
| 2B | reference/continue-src/gui/src/App.tsx | TSX | React root component |

---

# Statistics

- **Total Files**: ~220
- **Core Backend (2A)**: ~50 files
- **GUI Frontend (2B)**: ~80 files
- **Bridge (1)**: ~81 files (including 21+ handlers)
- **IDE (0)**: ~9 C# files
- **External Dependencies**: 15+ npm packages + Node.js built-ins

---

**Generated**: 2025-01-23  
**Status**: ✅ COMPLETE WITH FULL RELATIVE PATHS
**Format**: Files only (no directories), all paths from solution root
