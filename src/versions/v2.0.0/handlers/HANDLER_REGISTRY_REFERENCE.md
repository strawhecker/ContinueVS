#!/usr/bin/env node

/**
 * Handler Registry Reference — Metadata Catalog
 *
 * This document provides a quick reference for all registered handlers,
 * their properties, and registration order. For detailed information,
 * see the handler registry implementation (handler-registry.mjs).
 *
 * Related:
 *   - Step 66: Handler Registry (implementation)
 *   - Step 71: Handler Registration Orchestration
 *   - Steps 72–74: Middleware (uses registry metadata)
 *
 * Adding New Handlers (Steps 76–95):
 *   1. Implement handler function or factory in src/versions/v2.0.0/lib/
 *   2. Add entry to HANDLER_REGISTRY array in handler-registry.mjs
 *   3. Add row to table below
 *   4. Run tests: npx mocha tests/handler-registry.test.mjs
 *
 * @file src/versions/v2.0.0/handlers/HANDLER_REGISTRY_REFERENCE.md
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

# Handler Registry Reference

**Last Updated**: 2024-01-15  
**Total Handlers**: 12 (Steps 46–61, 76–94 implemented)  
**Future Expansion**: Steps 95+ (add entries below "Phase 10" section)

---

## Timeout Policy Definitions

| Policy | Duration | Use Case |
|--------|----------|----------|
| `fast` | 2 seconds | Synchronous lookups, simple queries (editor state, symbol info) |
| `medium` | 10 seconds | I/O operations, workspace scans, compilation checks |
| `slow` | 30 seconds | Long-running operations, debug sessions, test execution |

---

## Stability Tier Definitions

| Tier | Status | Support | Notes |
|------|--------|---------|-------|
| `core` | Production Ready | Fully supported | Used in critical paths; must be reliable |
| `experimental` | Beta | Community feedback | Undergoing improvements; API may change |
| `deprecated` | End-of-Life | Maintenance only | Planning replacement; use alternatives |

---

## Handler Catalog

### Phase 1: Bootstrap & Gateway

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 1 | `bridge:bootstrap` | `bootstrapHandler` | medium | core | Gateway handler; initializes bridge, enables all subsequent handlers | 46 | 45, BridgeLifecycleManager |

### Phase 2: Editor Context

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 2 | `bridge:getEditorState` | `getEditorStateHandler` | fast | core | Retrieves current editor state: file, cursor, selection, content | 50 | 48, 49, EditorContextCollector |
| 3 | `bridge:onEditorStateChange` | (subscription handler) | fast | core | Subscribes to editor state changes; streams updates | 51 | 49, SelectionTracker |

### Phase 3: Backing Services

**Note**: These services support navigation & intellisense handlers but are not directly invoked as bridge handlers.

| Service | Module | Purpose | Step | Used By |
|---------|--------|---------|------|---------|
| DocumentProvider | `document-provider.mjs` | Document content access and caching | 52 | search, navigation |
| SymbolExtractor | `symbol-extractor.mjs` | Symbol parsing and location tracking | 53 | navigation, intellisense |
| DiagnosticsCollector | `diagnostics-collector.mjs` | Error and warning aggregation | 54 | intellisense |

### Phase 4: Search & Navigation

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 4 | `bridge:search` | `searchHandler` | medium | core | Full-text search across workspace with filtering | 55 | 52, DocumentProvider |
| 5 | `bridge:goToDefinition` | `createGoToDefinitionHandler()` | medium | core | Navigate to symbol definition | 56 | 53, SymbolExtractor |
| 6 | `bridge:findReferences` | `createFindReferencesHandler()` | medium | core | Find all references to a symbol | 57 | 53, SymbolExtractor |

### Phase 5: Code Intelligence

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 7 | `bridge:codeCompletion` | `createCodeCompletionHandler()` | fast | core | Code completion suggestions at cursor | 58 | 50, getEditorStateHandler |
| 8 | `bridge:hoverInfo` | `createHoverInfoHandler()` | fast | core | Type info & documentation at hover | 59 | 53, SymbolExtractor |

### Phase 6: Advanced Features

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 9 | `bridge:testExplorer` | `createTestExplorerHandler()` | medium | experimental | Test discovery and execution | 60 | 84, ProjectInfoHandler |
| 10 | `bridge:debugSession` | `DebugSessionHandler` | slow | experimental | Debug session lifecycle management | 61 | 82, TerminalHandler |

---

## Future Handlers (Steps 76–95)

Add new handlers following this template:

### Phase 7: Refactoring & Code Modification (Steps 76–79)

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 11 | `bridge:refactor` | `createRefactorHandler()` | medium | experimental | Code refactoring operations | 76 | 50, 53, 54 |
| 12 | `bridge:fixSuggestion` | `createFixSuggestionHandler()` | medium | experimental | AI-driven fix suggestions | 77 | 54, 58 |
| 13 | `bridge:applyEdit` | `createApplyEditHandler()` | fast | experimental | Apply text edits to documents | 78 | 52 |
| 14 | `bridge:formatDocument` | `createFormatDocumentHandler()` | medium | experimental | Document formatting | 79 | 52 |

### Phase 8: Integration Services (Steps 81–84)

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 15 | `bridge:gitIntegration` | `createGitIntegrationHandler()` | medium | experimental | Git operations (blame, history) | 81 | 83, FileSystemHandler |
| 16 | `bridge:terminal` | `createTerminalHandler()` | slow | experimental | Terminal command execution | 82 | 83, FileSystemHandler |
| 17 | `bridge:fileSystem` | `createFileSystemHandler()` | medium | experimental | File system operations | 83 | none |
| 18 | `bridge:projectInfo` | `createProjectInfoHandler()` | fast | experimental | Project metadata and structure | 84 | 83, FileSystemHandler |

### Phase 9: UI & UX (Steps 85–92)

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 19 | `bridge:inlineMessage` | `createInlineMessageHandler()` | fast | experimental | Inline messages in editor | 85 | 50 |
| 20 | `bridge:sidebar` | `createSidebarUIHandler()` | fast | experimental | Sidebar UI updates | 86 | 50, 55 |
| 21 | `bridge:contextWindow` | `createContextWindowHandler()` | fast | experimental | Context window management | 87 | 50 |
| 22 | `bridge:modelInfo` | `createModelInfoHandler()` | fast | experimental | LLM model information | 88 | none |
| 23 | `bridge:streamingResponse` | `createStreamingResponseHandler()` | slow | experimental | Streaming response handling | 89 | 65, MessagePriorityQueue |
| 24 | `bridge:codeLens` | `createCodeLensHandler()` | fast | experimental | Code lens decorations | 90 | 56, 57 |
| 25 | `bridge:snippet` | `createSnippetHandler()` | fast | experimental | Code snippet insertion | 91 | 52 |
| 26 | `bridge:diffViewer` | `createDiffViewerHandler()` | medium | experimental | Diff viewing and navigation | 92 | 52 |

### Phase 10: Testing, Refactoring & Workspace Management (Steps 93–95)

| # | Message Type | Handler Function | Timeout | Stability | Description | Step | Dependencies |
|---|--------------|------------------|---------|-----------|-------------|------|--------------|
| 27 | `bridge:refactorTests` | `createRefactorTestsHandler()` | slow | experimental | Test generation for refactored code | 93 | 93 |
| 28 | `bridge:workspaceReload` | `createWorkspaceReloadHandler()` | medium | core | Scoped/full workspace reload with cache invalidation | 94 | 52, 53, 54 |

---

## Metadata Fields

All handler registry entries include:

```typescript
{
  messageType: string;           // Bridge message type (unique)
  handler: Function;             // Async handler (message, context) => Promise<HandlerResponse>
  timeoutPolicy: "fast" | "medium" | "slow";  // Expected duration
  stabilityTier: "core" | "experimental" | "deprecated";  // Production status
  description: string;           // Human-readable purpose
  relatedSteps: number[];        // Steps that created/use this handler
  dependencies: (string|number)[]; // Step numbers or module names
}
```

---

## Usage Examples

### Step 71: Register All Handlers

```javascript
import { getAllHandlers } from './lib/handler-registry.mjs';

export function registerAllHandlers(dispatcher, context) {
  const allHandlers = getAllHandlers();

  for (const entry of allHandlers) {
    dispatcher.register(entry.messageType, entry.handler);
  }
}
```

### Step 72: Message Logging Middleware

```javascript
import { getHandlerMetadata } from './lib/handler-registry.mjs';

export async function loggingMiddleware(message, next) {
  try {
    const meta = getHandlerMetadata(message.messageType);
    logger.debug(`[${meta.stabilityTier}] Invoking ${message.messageType}`);
    const result = await next();
    logger.debug(`[${meta.stabilityTier}] ${message.messageType} completed`);
    return result;
  } catch (err) {
    logger.error(`Handler ${message.messageType} failed:`, err);
    throw err;
  }
}
```

### Step 74: Error Recovery Middleware

```javascript
import { getHandlersByStabilityTier } from './lib/handler-registry.mjs';

export async function errorRecoveryMiddleware(message, next) {
  const meta = getHandlerMetadata(message.messageType);

  // Use different recovery strategies based on stability
  if (meta.stabilityTier === 'experimental') {
    try {
      return await next();
    } catch (err) {
      logger.warn(`Experimental handler failed, falling back: ${err.message}`);
      return { success: false, error: 'Handler unavailable' };
    }
  }

  return next();
}
```

---

## Metrics & Analytics

Registry metadata enables rich telemetry:

```javascript
import {
  getAllHandlers,
  getHandlersByStabilityTier,
  getHandlersByTimeoutPolicy
} from './lib/handler-registry.mjs';

const telemetry = {
  totalHandlers: getAllHandlers().length,
  coreHandlers: getHandlersByStabilityTier('core').length,
  experimentalHandlers: getHandlersByStabilityTier('experimental').length,
  fastHandlers: getHandlersByTimeoutPolicy('fast').length,
  mediumHandlers: getHandlersByTimeoutPolicy('medium').length,
  slowHandlers: getHandlersByTimeoutPolicy('slow').length
};
```

---

## Cross-References

- **handler-registry.mjs**: Registry implementation with validation logic
- **handler-registry.test.mjs**: Unit tests (22 tests, 100% coverage)
- **BRIDGE-DEVELOPER-GUIDE.md**: Extending the bridge with new handlers
- **Step 71**: Handler registration orchestration
- **Step 72–74**: Middleware using registry metadata
- **Steps 76–95**: Future handler implementations
