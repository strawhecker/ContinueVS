# ContinueVS TypeScript Navigation Index

**Source:** Scanned from actual TS/TSX source files only. Last verified: 2026-08-05  
**Purpose:** LLM-optimized reference for code navigation. Start with the three tables below.

---

## 📂 FILE INVENTORY (START HERE)

**All source files scanned, organized by complexity and role.**

| File | Lines | Role | Key Exports | Purpose |
|------|-------|------|-------------|---------|
| **`src/versions/v2.0.0/core-server.js`** | **673** | **🔴 CRITICAL** | `BridgeServer`, `parseArgs`, `HandlerDispatcher` | Node.js entry point; spawns Continue process; relays stdio messages |
| `reference/continue-src/core/config/load.ts` | 904 | 🟠 Pipeline | `loadContinueConfigFromJson`, `finalToBrowserConfig`, `intermediateToFinalConfig` | Config JSON → Runtime → Browser serialization |
| `reference/continue-src/core/tools/index.ts` | 56 | 🟠 Factory | `getBaseToolDefinitions`, `getConfigDependentToolDefinitions`, `serializeTool` | Tool definitions; runtime→browser stripping |
| `reference/continue-src/core/tools/mcpToolName.ts` | 18 | 🟡 Driver | `getMCPToolName()`, `getToolNameFromMCPServer()` | Normalize MCP server/tool names: sanitize non-alphanumeric, remove duplicates |
| `reference/continue-src/core/tools/parseArgs.ts` | 153 | 🟡 Utilities | `safeParseToolCallArgs()`, `coerceArgsToSchema()`, `getStringArg()`, `getNumberArg()`, `getBooleanArg()`, `getOptionalStringArg()` | Tool arg parsing, JSON deep-parse handling, type coercion (string/number/boolean) |
| `reference/continue-src/core/tools/applyToolOverrides.ts` | 69 | 🟡 Utilities | `applyToolOverrides()`, `ApplyToolOverridesResult` | Apply config tool overrides (disable, rename, re-describe); validation errors |
| `reference/continue-src/core/tools/builtIn.ts` | 32 | 🟡 Config | `BuiltInToolNames` enum (20 values), `BUILT_IN_GROUP_NAME`, `CLIENT_TOOLS_IMPLS` | Enum: read/edit/search/run/view/rule/fetch/codebase tools + repo map |
| `reference/continue-src/core/tools/callTool.ts` | 280 | 🟠 Dispatcher | `callTool()`, `callBuiltInTool()`, `callToolFromUri()`, `encodeMCPToolUri()`, `decodeMCPToolUri()` | Route tool calls to HTTP/MCP/built-in; fetch MCP UI resources; handle errors |
| `reference/continue-src/core/data/devdataSqlite.ts` | 92 | 🟡 Storage | `DevDataSqliteDb` (static class), `logTokensGenerated()`, `getTokensPerDay()`, `getTokensPerModel()` | Local SQLite token accounting (model, provider, prompt/generated counts by day/model) |
| `reference/continue-src/core/data/log.ts` | 238 | 🟠 Logger | `DataLogger` (singleton), `logDevData()`, `logLocalData()`, `logToOneDestination()`, `parseEventData()` | Local (JSON-L) + remote (HTTPS POST) dev data logging with schema versioning |
| `reference/continue-src/core/indexing/shouldIgnore.ts` | 74 | 🟡 Matcher | `shouldIgnore()` | Walk up tree from file, check default/.gitignore/.continueignore rules; symlink detection |
| `reference/continue-src/core/indexing/walkDir.ts` | 347 | 🟠 Walker | `DFSWalker`, `walkDirAsync()`, `walkDir()`, `walkDirs()`, `getIgnoreContext()`, `walkDirCache` | Depth-first traversal with dual-level caching (30s), ignore context chain, relative URI paths |
| `reference/continue-src/core/config/util.ts` | 210 | 🟡 Utility | `addModel()`, `deleteModel()`, `getModelByRole()` | Model config mutations + CPU target validation |
| `reference/continue-src/core/config/createNewAssistantFile.ts` | 69 | 🟡 Utility | `createNewAssistantFile()` | Create onboarding config file in .continue/agents/ |
| `reference/continue-src/core/config/loadLocalAssistants.ts` | 156 | 🟡 Loader | `isContinueConfigRelatedUri()`, `getAllDotContinueDefinitionFiles()` | Scan .continue/{agents,assistants,configs,prompts} + colocated rules |
| `reference/continue-src/core/config/markdown/loadCodebaseRules.ts` | 129 | 🟡 Loader | `loadCodebaseRules()`, `CodebaseRulesCache` (singleton) | Load rules.md from workspace + cache |
| `reference/continue-src/core/config/workspace/workspaceBlocks.ts` | 213 | 🟡 Creator | `createNewWorkspaceBlockFile()`, `createNewGlobalRuleFile()` | Create .continue/{blockType}/new-{name}.yaml/md |
| `reference/continue-src/core/config/ConfigHandler.ts` | 369 | 🟠 Orchestrator | `ConfigHandler` (class) | Profile lifecycle, cascading reload, listener dispatch |
| `reference/continue-src/core/config/onboarding.ts` | 171 | 🟡 Setup | `setupBestConfig()`, `setupLocalConfig()`, `setupProviderConfig()` | Onboarding model templates + defaults |
| `reference/continue-src/core/index.d.ts` | 2022 | 📘 Types | `Tool`, `ContinueConfig`, `BrowserSerializedContinueConfig`, `IDE`, `ILLM` | Complete type system (runtime & serializable) |
| `reference/continue-src/core/promptFiles/createNewPromptFile.ts` | 76 | 🟡 Generator | `createNewPromptFileV2()`, prompt file templates | Create onboarding .prompt files in .continue/prompts; YAML frontmatter + body parsing |
| `reference/continue-src/core/core.ts` | 1460 | 🔴 Orchestrator | `Core` (main class), message routing/abort control, IDE lifecycle init | Core brain: config/indexing/LLM/tools/history/session/MCP wiring; messenger recv/send dispatch |
| `reference/continue-src/gui/vite-env.d.ts` | 1 | 🟢 TypeDef | Vite client types reference | Vite build environment type definitions |
| `reference/continue-src/gui/index.css` | 146 | 🎨 Styles | Tailwind directives + custom classes | Global styles: animations (fadeIn, rerenderFlash), scrollbars, text truncation, theming |
| `reference/continue-src/gui/src/styles/theme.ts` | 286 | 🎨 Theme | `THEME_COLORS` object, `THEME_CSS_VARS`, `THEME_CSS_VAR_DEFAULTS` | VSCode theme variable mapping (30+ colors); dark mode defaults; blue accent palette |
| `reference/continue-src/gui/src/styles/utils.ts` | 45 | 🎨 Utilities | `parseHexColor()`, `parseColorForHex()` | Hex↔RGB color conversion; CSS var→hex parsing from DOM |
| `reference/continue-src/gui/src/util/cn.ts` | 20 | 🎨 Utilities | `cn()` | Merge Tailwind CSS classes with intelligent conflict resolution (clsx + tailwind-merge) |
| `reference/continue-src/gui/src/util/isContinueTeamMember.ts` | 7 | 🟡 Validator | `isContinueTeamMember()` | Check if user email is @continue.dev team member |
| `reference/continue-src/gui/src/util/localStorage.ts` | 58 | 🟡 Storage | `getLocalStorage()`, `setLocalStorage()`, `LocalStorageKey` enum | Type-safe localStorage with JSON parsing/serialization; custom events |
| `reference/continue-src/gui/src/util/navigation.ts` | 35 | 🟡 Router | `ROUTES`, `ConfigTab`, `buildConfigRoute()`, `CONFIG_ROUTES` | Route definitions and config tab navigation builders |
| `reference/continue-src/gui/src/util/migrateLocalStorage.ts` | 68 | 🟡 Migration | `migrateLocalStorage()`, `migrateToolPolicies()` | Migrate legacy tool settings to new ToolPolicy names |
| `reference/continue-src/gui/src/util/index.ts` | 89 | 🟡 Platform | `getPlatform()`, `isMetaEquivalentKeyPressed()`, `getMetaKeyLabel()`, `getFontSize()`, `isJetBrains()`, `isWebEnvironment()`, `isPrerelease()` | Platform detection; keyboard/UI helpers; IDE checks |
| `reference/continue-src/gui/src/util/compactConversation.ts` | 58 | 🟡 Hook | `useCompactConversation()`, `useDeleteCompaction()` | React hooks for conversation compaction + state management |
| `reference/continue-src/gui/src/util/editOutcomeLogger.ts` | 208 | 🟡 Logger | `assembleEditOutcomeData()`, `logAgentModeEditOutcome()`, `extractModelInfo()`, `extractPromptAndCompletion()`, `extractCodeChanges()` | Edit outcome telemetry assembly and logging to devdata |
| `reference/continue-src/gui/src/util/errorAnalysis.ts` | 177 | 🟡 Analyzer | `analyzeError()`, `ErrorAnalysis` type | LLM error parsing with provider-specific messaging + help links |
| `reference/continue-src/gui/src/util/toolCallState.ts` | 79 | 🟡 Utilities | `addToolCallDeltaToState()`, `isEditTool()` | Merge streamed tool call deltas; edit tool detection |
| `reference/continue-src/gui/src/util/test/config.ts` | 61 | 🟢 Test | `triggerConfigUpdate()`, `addAndSelectChatModel()`, `addAndSelectMockLlm()` | Test config mutations (config updates, model selection) |
| `reference/continue-src/gui/src/util/test/setupTests.ts` | 72 | 🟢 Test | Vitest setup (afterEach, afterAll hooks) | DOM mocks for ProseMirror, matchMedia, bounding rectangles |
| `reference/continue-src/gui/src/util/test/utils.ts` | 102 | 🟢 Test | `logAllTestIds()`, `getElementByTestId()`, `verifyNotPresentByTestId()`, `getElementByText()`, `getMainEditor()`, `sendInputWithMockedResponse()` | DOM query helpers and input simulation |
| `reference/continue-src/gui/src/util/test/mockStore.ts` | 114 | 🟢 Test | `getEmptyRootState()`, `createMockStore()` | Redux store mock with action tracking & thunk injection |
| `reference/continue-src/gui/src/util/test/render.tsx` | 77 | 🟢 Test | `renderWithProviders()`, `ExtendedRenderOptions` type | RTL wrapper with Redux, router, auth, editor providers |
| `reference/continue-src/gui/src/util/clientTools/editImpl.ts` | 53 | 🟡 Tool | `editToolImpl` | Client-side implementation of EditExistingFile tool (dispatch applyForEditTool) |
| `reference/continue-src/gui/src/util/clientTools/multiEditImpl.ts` | 43 | 🟡 Tool | `multiEditImpl` | Client-side implementation of MultiEdit tool (validate + execute multi-find-replace) |
| `reference/continue-src/gui/src/util/clientTools/singleFindAndReplaceImpl.ts` | 51 | 🟡 Tool | `singleFindAndReplaceImpl` | Client-side implementation of SingleFindAndReplace tool (validate + execute find-replace) |
| `reference/continue-src/gui/src/util/clientTools/callClientTool.ts` | 68 | 🟡 Dispatcher | `callClientTool()`, `ClientToolImpl`, `ClientToolExtras`, `ClientToolOutput`, `ClientToolResult` | Route client tool calls to implementations (EditExistingFile, SingleFindAndReplace, MultiEdit) |
| `reference/continue-src/core/protocol/util.ts` | 53 | 🟢 Schema | `ErrorWebviewMessage`, `WebviewSingleMessage`, `WebviewGeneratorMessage`, `WebviewMessage`, generator type helpers | Envelope types for single/streaming responses; error payloads (status+error) |
| `reference/continue-src/core/protocol/passThrough.ts` | 109 | 🟢 Router | `WEBVIEW_TO_CORE_PASS_THROUGH` (80 types), `CORE_TO_WEBVIEW_PASS_THROUGH` (12 types) | Message whitelist for bidirectional routing; KT enum sync required |
| `reference/continue-src/core/llm/constants.ts` | 37 | 🟡 Config | `DEFAULT_MAX_TOKENS`, `DEFAULT_CONTEXT_LENGTH`, `DEFAULT_TEMPERATURE`, `PROXY_URL`, `LLMConfigurationStatuses`, `NEXT_EDIT_MODELS` | LLM defaults (4k tokens, 32k context, 0.5°, 128k pruning); config enums |
| `reference/continue-src/core/llm/messages.ts` | 73 | 🟡 Validators | `messageHasToolCalls()`, `messageIsEmpty()`, `chatMessageIsEmpty()`, `addSpaceToAnyEmptyMessages()`, `isUserOrToolMsg()`, `isToolMessageForId()`, `messageHasToolCallId()` | Role-specific message validation; empty content detection; space-padding for providers |
| `reference/continue-src/core/llm/autodetect.ts` | 537 | 🟡 Utilities | `autodetectTemplateType`, provider/model capability lists | Model detection; prompt template mapping |
| `reference/continue-src/core/llm/openaiTypeConverters.ts` | 1120 | 🟡 Utilities | `toChatMessage`, `toOpenAIFunction`, reasoning field handlers | OpenAI API message conversion |
| `reference/continue-src/core/llm/logFormatter.ts` | 426 | 🟡 Utilities | `LLMLogFormatter` | Human-readable LLM interaction logging |
| `reference/continue-src/core/llm/logger.ts` | 43 | 🟡 Utilities | `LLMLogger`, `LLMInteractionLog` | LLM event bus & interaction tracking |
| `reference/continue-src/core/llm/countTokens.ts` | 570 | 🟡 Utilities | `countTokens`, `countTokensAsync`, `countToolsTokens` | Context window token counting |
| `reference/continue-src/core/llm/getAdjustedTokenCount.ts` | 38 | 🟡 Utilities | `getAdjustedTokenCountFromModel` | Model-specific token multipliers |
| `reference/continue-src/core/llm/toolSupport.ts` | 523 | 🟡 Utilities | `PROVIDER_TOOL_SUPPORT` | Tool calling capability detection (20+ providers) |
| `reference/continue-src/core/llm/defaultSystemMessages.ts` | 91 | 🟡 Config | Chat/Agent/Plan mode system prompts | Default instructions for each mode |
| `reference/continue-src/core/llm/fetchModels.ts` | 258 | 🟡 Utilities | `fetchOllamaModels`, `fetchOpenRouterModels`, `FetchedModel` | Model discovery from registries |
| `reference/continue-src/core/commands/slash/mcpSlashCommand.ts` | 42 | 🟡 Builder | `constructMcpSlashCommand()`, `stringifyMcpPrompt()` | MCP prompt to slash-command adapter; message role extraction |
| `reference/continue-src/core/util/chatDescriber.ts` | 116 | 🟡 Generator | `ChatDescriber` (static), `describe()`, `describeWithBaseLlmApi()` | Auto-generate 3-4 word session titles via LLM; 16-token limit |
| `reference/continue-src/core/util/GlobalContext.ts` | 185 | 🟠 Storage | `GlobalContext` (class), `GlobalContextType`, `GlobalContextModelSelections` | Persistent global state (workspace profile, model roles, MCP OAuth); corrupted-file salvage |
| `reference/continue-src/core/util/paths.ts` | 513 | 🟠 PathsMgr | File/folder path generators (30+ helpers); `CONTINUE_GLOBAL_DIR` env resolution | Config/session/index directory management; .continue hierarchy; config.yaml/ts setup |
| `reference/continue-src/core/util/processTerminalStates.ts` | 119 | 🟡 StateTracker | Background/foreground process maps; kill, status, output tracking | Dual-state process management (backgrounded vs running); SIGTERM→SIGKILL escalation (5s timeout) |
| `reference/continue-src/core/util/treeSitter.ts` | 311 | 🟠 Parser | `LanguageName` enum (22 languages), `supportedLanguages` map, parser/language loaders | Tree-sitter language detection; wasm caching; symbol extraction API |
| `reference/continue-src/core/util/tts.ts` | 106 | 🟡 Audio | `TTS` (static class), `sanitizeMessageForTTS()` | Cross-platform TTS (macOS `say`, Windows PowerShell, Linux `espeak`); process lifecycle |
| `reference/continue-src/core/util/conversationCompaction.ts` | 112 | 🟡 Summary | `compactConversation()`, `CompactionParams` | Conversation history summarization; incremental compression with prior summary reuse; technical accuracy focus |
| `reference/continue-src/core/util/history.ts` | 197 | 🟠 SessionMgr | `HistoryManager` (load/save/list/delete), `safeParseArray()` | Session persistence (JSON), list filtering (workspace/limit/offset), corrupted-file recovery, message counts |
| `reference/continue-src/core/util/historyUtils.ts` | 105 | 🟡 Exporter | `toMarkDown()`, `shareSession()`, date/timezone formatting helpers | Session→markdown export; codeblock metadata reformatting; multi-root workspace support
| `reference/continue-src/core/protocol/core.ts` | 348 | 🟢 Schema | `ToCoreFromIdeOrWebviewProtocol`, `OnboardingModes`, `ListHistoryOptions` | Core→IDE/Webview message types (100+ routes: history, config, context, MCP, autocomplete, nextEdit) |
| `reference/continue-src/core/protocol/webview.ts` | 44 | 🟢 Schema | `ToWebviewFromIdeOrCoreProtocol` | Webview←IDE/Core messages (config, indexing, menu refresh, context items, sessions) |
| `reference/continue-src/core/protocol/coreWebview.ts` | 7 | 🟢 Combiner | `ToCoreFromWebviewProtocol`, `ToWebviewFromCoreProtocol` | Union types: webview↔core with profile selection |
| `reference/continue-src/core/protocol/ideWebview.ts` | 82 | 🟢 Schema | `ToIdeFromWebviewProtocol`, `ToWebviewFromIdeProtocol` | IDE←→Webview (file ops, UI state, apply/edit, session share) |
| `reference/continue-src/core/protocol/ide.ts` | 95 | 🟢 Schema | `ToIdeFromWebviewOrCoreProtocol`, `ToWebviewOrCoreFromIdeProtocol` | IDE←Webview/Core (50+ LSP/file/git/debug methods); IDE→both (activity) |
| `reference/continue-src/core/protocol/ideCore.ts` | 5 | 🟢 Combiner | `ToIdeFromCoreProtocol`, `ToCoreFromIdeProtocol` | Union types: IDE↔core |
| `reference/continue-src/core/protocol/index.ts` | 32 | 🟢 Router | `IProtocol`, `ToIdeProtocol`, `FromIdeProtocol`, `ToWebviewProtocol`, `FromWebviewProtocol`, `ToCoreProtocol`, `FromCoreProtocol` | Protocol endpoint combinations (IDE/Webview/Core) |
| `reference/continue-src/core/protocol/messenger/index.ts` | 185 | 🟠 Bus | `IMessenger`, `InProcessMessenger`, `Message` | Generic in-process messenger with send/request/invoke/on pattern |
| `reference/continue-src/core/protocol/messenger/messageIde.ts` | 230 | 🟠 Bridge | `MessageIde` | IDE implementation via messenger requests (wraps protocol as method calls) |
| `reference/continue-src/core/protocol/messenger/reverseMessageIde.ts` | 172 | 🟠 Bridge | `ReverseMessageIde` | Handler for incoming IDE requests; delegates to concrete IDE instance |
| `reference/continue-src/core/llm/autodetect.ts` | 537 | 🟡 Detector | `autodetectTemplateType()`, `modelSupportsImages()`, `modelSupportsReasoning()`, provider/model lists | Model→template mapping; 30+ providers; vision/reasoning capability detection |
| `reference/continue-src/core/llm/openaiTypeConverters.ts` | 1120 | 🟡 Converter | `toChatMessage()`, `toOpenAIFunction()`, `appendReasoningFieldsIfSupported()` | OpenAI message format conversion; handle thinking/reasoning/tool-calls |
| `reference/continue-src/core/llm/logFormatter.ts` | 426 | 🟡 Formatter | `LLMLogFormatter` (class) | Human-readable LLM stream logging with overlapping interaction tracking & text wrapping |
| `reference/continue-src/core/llm/logger.ts` | 43 | 🟡 Logger | `LLMLogger`, `LLMInteractionLog` (singleton + instance) | Event bus for LLM interactions; log items with timestamps |
| `reference/continue-src/core/llm/countTokens.ts` | 570 | 🟡 Counter | `countTokens()`, `countTokensAsync()`, `countToolsTokens()`, `countChatMessageTokens()` | Tiktoken/Llama encoders; image/tool/message token accounting |
| `reference/continue-src/core/llm/getAdjustedTokenCount.ts` | 38 | 🟡 Adjuster | `getAdjustedTokenCountFromModel()` | Safety multipliers for Claude/Gemini/Mistral tokenizers (1.18–1.26x) |
| `reference/continue-src/core/llm/toolSupport.ts` | 523 | 🟡 Detector | `PROVIDER_TOOL_SUPPORT` (provider-dispatch map) | Per-provider model→tool-calling capability detection (20+ providers) |
| `reference/continue-src/core/llm/defaultSystemMessages.ts` | 91 | 🟡 Prompts | System message constants (Chat/Agent/Plan modes) | Mode-specific instructions (edit, codeblock, lazy comments) |
| `reference/continue-src/core/llm/fetchModels.ts` | 258 | 🟡 Registry | `fetchOllamaModels()`, `fetchOpenRouterModels()`, `FetchedModel` | Model discovery from Ollama/OpenRouter/Anthropic/Gemini APIs |
| `reference/continue-src/core/llm/streamChat.ts` | 147 | 🟢 Orchestrator | `llmStreamChat()` async generator | Route to slash-command or model streamChat; yield messages; TTS integration |
| `reference/continue-src/core/llm/index.ts` | 1504 | 🟠 Base | `BaseLLM` (abstract), `LLMError`, `isModelInstaller()` | LLM base class; model registry; chat/fim/completion methods; tool overrides |
| `reference/continue-src/core/llm/llms/Lemonade.ts` | 12 | 🟡 Provider | `Lemonade` class | OpenAI-compatible wrapper (extends OpenAI; localhost:8000) |
| `reference/continue-src/core/llm/llms/Ollama.ts` | 833 | 🟡 Provider | `Ollama` class, `OllamaChatMessage`, `OllamaModelFileParams` | Ollama API adapter; FIM/chat/tool support; model installer; streaming chat |
| `reference/continue-src/core/llm/index.ts` | 1504 | 🟢 Core | `BaseLLM`, `LLMError`, `isModelInstaller()` | Abstract LLM base class + model registry |
| `reference/continue-src/core/llm/llms/Lemonade.ts` | 12 | 🟡 Provider | `Lemonade` class | OpenAI-compatible wrapper (extends OpenAI) |
| `reference/continue-src/core/llm/llms/Ollama.ts` | 833 | 🟡 Provider | `Ollama` class | Ollama API adapter (local model inference) |
| `reference/continue-src/core/tools/constants.ts` | 4 | 🟡 Config | `NO_TOOL_CALL_OUTPUT_MESSAGE`, `CANCELLED_TOOL_CALL_MESSAGE`, `ERRORED_TOOL_CALL_OUTPUT_MESSAGE` | Tool execution status strings (no output, user cancel, error) |
| `reference/continue-src/core/autocomplete/util/openedFilesLruCache.ts` | 20 | 🟡 Cache | `openedFilesLruCache`, `cacheElementType`, `prevFilepaths` | LRU cache of open files (max 20) in viewing order |
| `reference/continue-src/core/autocomplete/snippets/gitDiffCache.ts` | 73 | 🟡 Cache | `GitDiffCache`, `getDiffFn`, `getDiffsFromCache` | Singleton git diff cache (60s TTL) |
| `reference/continue-src/core/autocomplete/CompletionProvider.ts` | 316 | 🟠 Generator | `CompletionProvider`, `provideInlineCompletionItems()` | Inline completion orchestration + caching |
| `reference/continue-src/core/nextEdit/context/aggregateEdits.ts` | 628 | 🟠 Aggregator | `EditAggregator`, `EditClusterConfig` | Time/line-based edit clustering for small edits |
| `reference/continue-src/core/nextEdit/context/diffFormatting.ts` | 247 | 🟡 Formatter | `createDiff()`, `createBeforeAfterDiff()`, `DiffFormatType` | Unified diff + before/after formatting |
| `reference/continue-src/core/nextEdit/context/processSmallEdit.ts` | 53 | 🟡 Utilities | `processSmallEdit()` | Small edit processing pipeline entry point |
| `reference/continue-src/core/nextEdit/NextEditPrefetchQueue.ts` | 152 | 🟡 Queue | `PrefetchQueue`, `ProcessedItem` | Singleton queue for prefetch (disabled) |
| `reference/continue-src/core/nextEdit/NextEditProvider.ts` | 628 | 🟠 Generator | `NextEditProvider`, `provideInlineCompletionItemsWithChain()` | Next edit prediction orchestrator + chains |
| `reference/continue-src/core/diff/myers.ts` | 211 | 🟡 Diff | `myersDiff()`, `myersCharDiff()`, `convertMyersChangeToDiffLines()` | Line/char-level diff using Myers algorithm |
| `reference/continue-src/core/edit/applyAbortManager.ts` | 37 | 🟡 Manager | `ApplyAbortManager` (singleton) | Manage AbortController instances per apply ID |
| `reference/continue-src/core/edit/streamDiffLines.ts` | 190 | 🟠 Streaming | `streamDiffLines()`, `addIndentation()` | Stream diff lines for edit operations with rule support |
| `reference/continue-src/core/indexing/CodebaseIndexer.ts` | 872 | 🟠 Orchestrator | `CodebaseIndexer`, `PauseToken` | Orchestrate codebase indexing (chunk, embeddings, FTS, snippets) |
| `reference/continue-src/core/indexing/docs/DocsService.ts` | 1292 | 🟠 Orchestrator | `DocsService`, `LanceDbDocsRow`, `embedModelsAreEqual()` | Documentation site indexing with LanceDB + SQLite storage |
| `reference/continue-src/core/config/util.ts` | 210 | 🟡 Utilities | `addModel()`, `deleteModel()`, `getModelByRole()`, `isSupportedLanceDbCpuTargetForLinux()` | Model config mutations + CPU target validation + prompt template serialization |
| `reference/continue-src/core/config/createNewAssistantFile.ts` | 69 | 🟡 Utility | `createNewAssistantFile()` | Create onboarding config file in .continue/agents/ |
| `reference/continue-src/core/config/loadLocalAssistants.ts` | 156 | 🟡 Loader | `isContinueConfigRelatedUri()`, `getAllDotContinueDefinitionFiles()`, `getDotContinueSubDirs()` | Scan .continue/{agents,assistants,configs,prompts} + colocated rules |
| `reference/continue-src/core/config/markdown/loadCodebaseRules.ts` | 129 | 🟡 Loader | `loadCodebaseRules()`, `CodebaseRulesCache` (singleton) | Load rules.md files; parse & cache codebase-colocated rules |
| `reference/continue-src/core/config/workspace/workspaceBlocks.ts` | 213 | 🟡 Creator | `createNewWorkspaceBlockFile()`, `createNewGlobalRuleFile()`, `getFileContent()`, `findAvailableFilename()` | Create/manage .continue block files (rules, prompts, context, models, etc.) |
| `reference/continue-src/core/config/ConfigHandler.ts` | 369 | 🟠 Orchestrator | `ConfigHandler` (class), `cascadeInit()`, `getLocalProfiles()`, lifecycle methods | Config lifecycle manager; profile loading, cascading reload, listener dispatch |
| `reference/continue-src/core/config/onboarding.ts` | 171 | 🟡 Setup | `setupBestConfig()`, `setupLocalConfig()`, `setupProviderConfig()`, model constants | Onboarding model templates for Anthropic/OpenAI/Gemini + Ollama defaults |
| `reference/continue-src/core/context/mcp/MCPManagerSingleton.ts` | 204 | 🟠 Manager | `MCPManagerSingleton`, `setConnections()`, `refreshConnections()`, `getStatuses()` | Singleton MCP client lifecycle + transport comparison + connection lifecycle |
| `reference/continue-src/core/context/mcp/MCPOauth.ts` | 349 | 🟡 Auth | `MCPConnectionOauthProvider`, `handleMCPOauthCode()`, OAuth state/token storage | OAuth2 redirect handler (port 3000); client info & token persistence via GlobalContext |
| `reference/continue-src/core/util/errors.ts` | 71 | 🟠 Error | `ContinueError` (custom class), `ContinueErrorReason` (enum), `getRootCause()` (recursive cause traversal) | Error taxonomy: 29 codes covering Find&Replace, Multi-Edit, Files, Terminal, Search, Rules, Skills |
| `reference/continue-src/gui/index.html` | 16 | 🟢 Bootstrap | (html) | WebView2 mount point |
| `reference/continue-src/gui/src/main.tsx` | 24 | 🟢 Bootstrap | `App`, Redux `Provider`, `PersistGate` | React root + Redux setup |
| `reference/continue-src/gui/src/redux/store.ts` | 145 | 🟢 Redux | `store`, `persistor`, `RootState`, middleware | Redux store with IdeMessenger thunk extra |
| `reference/continue-src/gui/src/redux/slices/configSlice.ts` | 109 | 🟢 Redux | `setConfigResult`, `EMPTY_CONFIG`, `selectUIConfig` | GUI config state + `tools: []` fallback |
| `reference/continue-src/core/config/ProfileLifecycleManager.ts` | 142 (partial) | 🟠 Pipeline | `getSerializedConfig`, `finalToBrowserConfig` dispatch | Calls core serialization; sends to Redux |

---

## 🔗 DEPENDENCY GRAPH (File Dependencies)

```
src/versions/v2.0.0/core-server.js
├─ ./lib/handler-dispatcher.js (imports HandlerDispatcher)
├─ ./lib/register-handlers.mjs (registers 19+ message handlers)
└─ ./lib/validation-hook.mjs (message validation)

reference/continue-src/core/
├─ config/
│  └─ load.ts (main pipeline)
│     ├─ load.ts:644 → calls serializeTool() [tools/index.ts:53-56]
│     ├─ load.ts:476 → calls getBaseToolDefinitions() [tools/index.ts:6-16]
│     └─ load.ts:132-135 → exported finalToBrowserConfig
│
├─ tools/
│  ├─ index.ts
│  │  ├─ serializeTool() [line 53]
│  │  └─ getBaseToolDefinitions() [line 6]
│  └─ constants.ts (output message enums)
│
├─ llm/
│  ├─ constants.ts (defaults)
│  ├─ messages.ts (validation fns)
│  └─ index.ts (ILLM interface)
│
├─ protocol/
│  ├─ util.ts (message envelopes)
│  └─ passThrough.ts (routing rules)
│
├─ util/
│  └─ errors.ts (ContinueError + 29 codes)
│
└─ index.d.ts (complete types)
   ├─ Tool [line 1132-1168]
   ├─ ContinueConfig [line 1820-1841]
   └─ BrowserSerializedContinueConfig [line 1843-1863]

reference/continue-src/gui/
├─ index.html (WebView2 mount)
│  └─ src/main.tsx (React root)
│     └─ src/redux/store.ts (Redux setup)
│        └─ src/redux/slices/configSlice.ts
│           └─ receives BrowserSerializedContinueConfig
│              └─ stores in state.config.tools
│
└─ ProfileLifecycleManager.ts
   ├─ load.ts:finalToBrowserConfig()
   └─ → dispatch setConfigResult() → configSlice.ts:51
```

---

## 🔍 SYMBOL QUICK REFERENCE

**Jump to a symbol: use Ctrl+F to search this table.**

| Symbol | File | Line | Type | Category | Purpose |
|--------|------|------|------|----------|---------|
| `ErrorWebviewMessage` | `core/protocol/util.ts` | 3-7 | Type | Protocol | Error response envelope |
| `SuccessWebviewSingleMessage<T>` | `core/protocol/util.ts` | 9-13 | Type | Protocol | Single (non-streaming) response |
| `WebviewMessage<T,R>` | `core/protocol/util.ts` | 51-53 | Type | Protocol | Union of Single \| Generator |
| `WebviewGeneratorMessage<T,R>` | `core/protocol/util.ts` | 40-42 | Type | Protocol | Streaming/generator response |
| `WEBVIEW_TO_CORE_PASS_THROUGH` | `core/protocol/passThrough.ts` | 9-90 | Const | Protocol | 90 GUI→Core message types |
| `CORE_TO_WEBVIEW_PASS_THROUGH` | `core/protocol/passThrough.ts` | 95-109 | Const | Protocol | 12 Core→GUI message types |
| `DEFAULT_MAX_TOKENS` | `core/llm/constants.ts` | 1 | Const | Config | 4096 |
| `DEFAULT_CONTEXT_LENGTH` | `core/llm/constants.ts` | 2 | Const | Config | 32,768 |
| `LLMConfigurationStatuses` | `core/llm/constants.ts` | 17-21 | Enum | Config | VALID, MISSING_API_KEY, MISSING_ENV_SECRET |
| `messageHasToolCalls()` | `core/llm/messages.ts` | 3-5 | Function | Validate | Check if msg has toolCalls |
| `messageIsEmpty()` | `core/llm/messages.ts` | 7-17 | Function | Validate | Check if content is empty |
| `chatMessageIsEmpty()` | `core/llm/messages.ts` | 56-72 | Function | Validate | Role-specific emptiness check |
| `autodetectTemplateType()` | `core/llm/autodetect.ts` | (var lines) | Function | Utility | Map model name → prompt template type |
| `PROVIDER_HANDLES_TEMPLATING` | `core/llm/autodetect.ts` | 46-112 | Const[] | Config | 24 providers with server-side templating |
| `PROVIDER_SUPPORTS_IMAGES` | `core/llm/autodetect.ts` | 114-136 | Const[] | Config | 20 providers with image input support |
| `MODEL_SUPPORTS_IMAGES` | `core/llm/autodetect.ts` | 138-150 | Const[] | Config | Regex patterns for vision-capable models |
| `toChatMessage()` | `core/llm/openaiTypeConverters.ts` | 109-180+ | Function | Convert | Continue ChatMessage → OpenAI format |
| `appendReasoningFieldsIfSupported()` | `core/llm/openaiTypeConverters.ts` | 45-107 | Function | Convert | Add o1/DeepSeek reasoning fields |
| `LLMLogFormatter` | `core/llm/logFormatter.ts` | 76-150+ | Class | Logging | Format LLM interactions with timestamps |
| `LLMLogger` | `core/llm/logger.ts` | 10-28 | Class | Logging | LLM event bus (publish/subscribe) |
| `LLMInteractionLog` | `core/llm/logger.ts` | 30-43 | Class | Logging | Per-interaction event recorder |
| `countTokens()` | `core/llm/countTokens.ts` | 112-132 | Function | Tokens | Sync token counter for content |
| `countTokensAsync()` | `core/llm/countTokens.ts` | 94-110 | Function | Tokens | Async token counter (worker-based) |
| `countToolsTokens()` | `core/llm/countTokens.ts` | 135-150+ | Function | Tokens | Count tokens consumed by tool definitions |
| `LlamaEncoding` | `core/llm/countTokens.ts` | 28-36 | Class | Tokens | Sync Llama tokenizer wrapper |
| `NonWorkerAsyncEncoder` | `core/llm/countTokens.ts` | 38-50 | Class | Tokens | Async wrapper for sync encoders |
| `getAdjustedTokenCountFromModel()` | `core/llm/getAdjustedTokenCount.ts` | 20-38 | Function | Utility | Apply provider-specific token multipliers (Claude 1.23x, Gemini 1.18x, Mistral 1.26x) |
| `PROVIDER_TOOL_SUPPORT` | `core/llm/toolSupport.ts` | 3-150+ | Record | Utility | 20+ provider → tool calling capability checkers |
| `CODEBLOCK_FORMATTING_INSTRUCTIONS` | `core/llm/defaultSystemMessages.ts` | 4-7 | Const | Config | Always include language & file path in code blocks |
| `EDIT_CODE_INSTRUCTIONS` | `core/llm/defaultSystemMessages.ts` | 9-47 | Const | Config | Abbreviated placeholders for unmodified sections |
| `DEFAULT_CHAT_SYSTEM_MESSAGE` | `core/llm/defaultSystemMessages.ts` | 51-60 | Const | Config | Chat mode: use Apply Button or switch to Agent Mode |
| `DEFAULT_AGENT_SYSTEM_MESSAGE` | `core/llm/defaultSystemMessages.ts` | 62-74 | Const | Config | Agent mode: call read-only tools, use edit tools for changes |
| `DEFAULT_PLAN_SYSTEM_MESSAGE` | `core/llm/defaultSystemMessages.ts` | 78-91 | Const | Config | Plan mode: read-only only, offer Agent Mode for writes |
| `FetchedModel` | `core/llm/fetchModels.ts` | 3-11 | Interface | Utility | Model metadata: name, icon, context, tools support |
| `fetchOllamaModels()` | `core/llm/fetchModels.ts` | 64-122 | Function | Utility | Scrape ollama.com library; return models + capabilities |
| `fetchOpenRouterModels()` | `core/llm/fetchModels.ts` | 124-150+ | Function | Utility | Query OpenRouter API v1/models endpoint |
| `getOllamaIcon()` | `core/llm/fetchModels.ts` | 51-62 | Function | Utility | Map Ollama model names → brand icons |
| `OLLAMA_ICON_MAP` | `core/llm/fetchModels.ts` | 15-49 | Record | Config | 40+ model→icon mappings (meta, mistral, deepseek, etc.) |
| `OLLAMA_EXCLUDED_CAPABILITIES` | `core/llm/fetchModels.ts` | 13 | Const[] | Config | Skip models with vision, audio, embedding |
| `llmStreamChat()` | `core/llm/streamChat.ts` | 9-147 | AsyncGenerator | Protocol | Route chat via slash-cmd or model.streamChat() |
| `LLMError` | `core/llm/index.ts` | 71-78 | Class | Error | Signals LLM operation failure + references ILLM |
| `BaseLLM` | `core/llm/index.ts` | 90-303+ | Abstract Class | Core | LLM interface impl: chat, complete, FIM, token counting |
| `isModelInstaller()` | `core/llm/index.ts` | 80-86 | Function | Utility | Type guard for ModelInstaller (installModel, isInstallingModel) |
| `Lemonade` | `core/llm/llms/Lemonade.ts` | 5-10 | Class | Provider | Extends OpenAI; defaults to http://localhost:8000/api/v1/ |
| `Ollama` | `core/llm/llms/Ollama.ts` | 152-833 | Class | Provider | Extends BaseLLM; implements ModelInstaller (model download) |
| `OllamaChatMessage` | `core/llm/llms/Ollama.ts` | 19-30 | Type | Provider | role, content, images[], thinking?, tool_calls[] |
| `OllamaModelFileParams` | `core/llm/llms/Ollama.ts` | 33-65 | Interface | Config | temperature, top_p, top_k, num_predict, stop, etc. |
| `OllamaBaseOptions` | `core/llm/llms/Ollama.ts` | 68-74 | Interface | Config | model, options, format, stream, keep_alive |
| `OLlamaChatOptions` | `core/llm/llms/Ollama.ts` | 86-92 | Interface | Config | messages + optional tools, think flag |
| `getMCPToolName()` | `core/tools/mcpToolName.ts` | 2-4 | Function | Utility | Get MCP tool name with server prefix |
| `getToolNameFromMCPServer()` | `core/tools/mcpToolName.ts` | 6-18 | Function | Utility | Normalize server name → tool name prefix |
| `safeParseToolCallArgs()` | `core/tools/parseArgs.ts` | 3-25 | Function | Parser | Parse JSON or object tool call args; fallback {} |
| `coerceArgsToSchema()` | `core/tools/parseArgs.ts` | 34-63 | Function | Parser | Convert JSON-parsed objects back to strings for string-typed fields |
| `getStringArg()` | `core/tools/parseArgs.ts` | 65-102 | Function | Parser | Extract string argument; stringify objects; validate non-empty |
| `getOptionalStringArg()` | `core/tools/parseArgs.ts` | 104-113 | Function | Parser | Extract optional string argument; return undefined if missing |
| `getNumberArg()` | `core/tools/parseArgs.ts` | 115-131 | Function | Parser | Extract & floor number; parse string "123" → 123 |
| `getBooleanArg()` | `core/tools/parseArgs.ts` | 133-150+ | Function | Parser | Extract boolean; parse "true"/"false" strings |
| `applyToolOverrides()` | `core/tools/applyToolOverrides.ts` | 14-68 | Function | Config | Apply config overrides (disable, rename, re-describe tools) |
| `ApplyToolOverridesResult` | `core/tools/applyToolOverrides.ts` | 4-7 | Interface | Config | { tools: Tool[], errors: ConfigValidationError[] } |
| `BuiltInToolNames` | `core/tools/builtIn.ts` | 1-24 | Enum | Config | 19 built-in tools: read_file, edit_existing_file, run_terminal_command, etc. |
| `BUILT_IN_GROUP_NAME` | `core/tools/builtIn.ts` | 26 | Const | Config | "Built-In" group marker |
| `CLIENT_TOOLS_IMPLS` | `core/tools/builtIn.ts` | 28-32 | Const[] | Config | [EditExistingFile, SingleFindAndReplace, MultiEdit] |
| `callTool()` | `core/tools/callTool.ts` | 235-280 | AsyncFunction | Dispatcher | Call tool (built-in, HTTP, or MCP); return contextItems + errors |
| `callBuiltInTool()` | `core/tools/callTool.ts` | 187-230 | AsyncFunction | Dispatcher | Route to built-in tool implementations |
| `callToolFromUri()` | `core/tools/callTool.ts` | 67-185 | AsyncFunction | Dispatcher | Call HTTP (http://, https://) or MCP (mcp://) tool |
| `encodeMCPToolUri()` | `core/tools/callTool.ts` | 52-54 | Function | Utility | Encode MCP tool → mcp://mcpId/toolName URI |
| `decodeMCPToolUri()` | `core/tools/callTool.ts` | 56-65 | Function | Utility | Decode mcp:// URI → [mcpId, toolName] |
| `callHttpTool()` | `core/tools/callTool.ts` | 28-50 | AsyncFunction | Dispatcher | POST to HTTP tool endpoint with arguments |
| `DevDataSqliteDb` | `core/data/devdataSqlite.ts` | 11-92 | Class | Telemetry | Singleton SQLite DB for token usage logging |
| `logTokensGenerated()` | `core/data/devdataSqlite.ts` | 40-51 | StaticAsync | Telemetry | Log (promptTokens, generatedTokens) → tokens_generated table |
| `getTokensPerDay()` | `core/data/devdataSqlite.ts` | 53-62 | StaticAsync | Query | Aggregate tokens_prompt + tokens_generated by calendar date |
| `getTokensPerModel()` | `core/data/devdataSqlite.ts` | 64-73 | StaticAsync | Query | Aggregate tokens by model name |
| `DataLogger` | `core/data/log.ts` | 21-238 | Class (Singleton) | Telemetry | Local + remote event data logging orchestrator |
| `logDevData()` | `core/data/log.ts` | 104-117 | AsyncMethod | Telemetry | Log to local file + all remote endpoints |
| `logLocalData()` | `core/data/log.ts` | 71-102 | AsyncMethod | Telemetry | Append JSON-L to local .continue file |
| `logToOneDestination()` | `core/data/log.ts` | 162-237 | AsyncMethod | Telemetry | POST to https:// or write to file:// URI with schema validation |
| `shouldIgnore()` | `core/indexing/shouldIgnore.ts` | 15-74 | AsyncFunction | Walker | Check if file excluded by .gitignore/.continueignore (walk UP tree) |
| `walkDirAsync()` | `core/indexing/walkDir.ts` | 266-273 | AsyncGenerator | Walker | DFS walk directory; yield file/dir URIs; respect ignores |
| `walkDir()` | `core/indexing/walkDir.ts` | 275-285 | AsyncFunction | Walker | Collect all results from walkDirAsync into array |
| `walkDirs()` | `core/indexing/walkDir.ts` | 287-297 | AsyncFunction | Walker | Walk all workspace directories in parallel |
| `getIgnoreContext()` | `core/indexing/walkDir.ts` | 299-347 | AsyncFunction | Utility | Load .gitignore + .continueignore at directory; return Ignore object |
| `WalkerOptions` | `core/indexing/walkDir.ts` | 9-15 | Interface | Config | include (files/dirs/both), recursive, returnRelativeUrisPaths, source |
| `walkDirCache` | `core/indexing/walkDir.ts` | 72 | Singleton | Utility | Cache listDir() and ignore patterns (30s TTL) |
| `addModel()` | `core/config/util.ts` | 26-92 | Function | Config | Add model to runtime/serialized config (dedup by title) |
| `deleteModel()` | `core/config/util.ts` | 94-105 | Function | Config | Remove model from config by title |
| `getModelByRole()` | `core/config/util.ts` | 107-122 | Function | Utility | Lookup ILLM by experimental modelRole |
| `isSupportedLanceDbCpuTargetForLinux()` | `core/config/util.ts` | 130-177 | Function | Hardware | Check /proc/cpuinfo for AVX2+FMA; cache result |
| `serializePromptTemplates()` | `core/config/util.ts` | 199-210 | Function | Utility | Strip function templates → strings for serialization |
| `createNewAssistantFile()` | `core/config/createNewAssistantFile.ts` | 42-69 | AsyncFunction | UI | Create default config.yaml in .continue/agents/ |
| `isContinueConfigRelatedUri()` | `core/config/loadLocalAssistants.ts` | 16-30 | Function | Detector | Check if URI is .continuerc.json, .prompt, agent file, etc. |
| `isContinueAgentConfigFile()` | `core/config/loadLocalAssistants.ts` | 32-44 | Function | Detector | Check if URI is in .continue/{agents,assistants,configs} |
| `isColocatedRulesFile()` | `core/config/loadLocalAssistants.ts` | 46-48 | Function | Detector | Check if basename === rules.md |
| `getAllDotContinueDefinitionFiles()` | `core/config/loadLocalAssistants.ts` | 131-156 | AsyncFunction | Loader | Load YAML/Markdown from ~/.continue and .continue/subdir |
| `getDotContinueSubDirs()` | `core/config/loadLocalAssistants.ts` | 104-125 | Function | Utility | Get full .continue/{subDirName} paths (workspace + global) |
| `CodebaseRulesCache` | `core/config/markdown/loadCodebaseRules.ts` | 10-62 | Class (Singleton) | Cache | In-memory rules.md cache with refresh/update/remove |
| `loadCodebaseRules()` | `core/config/markdown/loadCodebaseRules.ts` | 67-129 | AsyncFunction | Loader | Walk workspace; scan rules.md; parse via markdownToRule() |
| `getContentsForNewBlock()` | `core/config/workspace/workspaceBlocks.ts` | 27-86 | Function | Factory | Return template ConfigYaml for blockType (context, models, rules, etc.) |
| `getFileExtension()` | `core/config/workspace/workspaceBlocks.ts` | 88-93 | Function | Utility | .md for rules/prompts; .yaml for others |
| `getFileContent()` | `core/config/workspace/workspaceBlocks.ts` | 95-112 | Function | Factory | Markdown/YAML template body for new block |
| `findAvailableFilename()` | `core/config/workspace/workspaceBlocks.ts` | 114-155 | AsyncFunction | Utility | Find first non-existent (.continue/blockType/new-name-N.ext) |
| `createNewWorkspaceBlockFile()` | `core/config/workspace/workspaceBlocks.ts` | 157-184 | AsyncFunction | UI | Create + open workspace .continue/blockType/new-{name}.{ext} |
| `createNewGlobalRuleFile()` | `core/config/workspace/workspaceBlocks.ts` | 186-213 | AsyncFunction | UI | Create + open ~/.continue/rules/new-{name}.md |
| `ConfigHandler` | `core/config/ConfigHandler.ts` | 31-369 | Class | Orchestrator | Multi-profile lifecycle, cascading reload, listeners |
| `cascadeInit()` | `core/config/ConfigHandler.ts` | 83-136 | AsyncMethod | Lifecycle | Load profiles, select current, save preference, reloadConfig |
| `loadProfiles()` | `core/config/ConfigHandler.ts` | 138-159 | AsyncMethod | Loader | Load ~/.continue + workspace agents/assistants/configs |
| `getLocalProfiles()` | `core/config/ConfigHandler.ts` | 161-190 | AsyncMethod | Loader | Return array of ProfileLifecycleManager (global + workspace) |
| `reloadConfig()` | `core/config/ConfigHandler.ts` | 237-283 | AsyncMethod | Lifecycle | Clear non-current profiles; call currentProfile.reloadConfig(); notify listeners |
| `getSerializedConfig()` | `core/config/ConfigHandler.ts` | 301-315 | AsyncMethod | Getter | Await isInitialized; return BrowserSerializedConfig |
| `loadConfig()` | `core/config/ConfigHandler.ts` | 317-330 | AsyncMethod | Getter | Await isInitialized; return runtime ContinueConfig |
| `setSelectedProfileId()` | `core/config/ConfigHandler.ts` | 207-231 | AsyncMethod | Lifecycle | Validate profileId; save to GlobalContext; reloadConfig |
| `setupBestConfig()` | `core/config/onboarding.ts` | 94-99 | Function | Onboarding | Pass-through (for future best default models) |
| `setupLocalConfig()` | `core/config/onboarding.ts` | 101-126 | Function | Onboarding | Inject Llama 3.1, Qwen, Nomic Embed (Ollama) + existing models |
| `setupProviderConfig()` | `core/config/onboarding.ts` | 132-171 | Function | Onboarding | Inject Anthropic/OpenAI/Gemini models for provider + apiKey |
| `CANCELLED_TOOL_CALL_MESSAGE` | `core/tools/constants.ts` | 2 | Const | Config | "The user cancelled this tool call." |
| `ContinueError` | `core/util/errors.ts` | 14-22 | Class | Error | Custom error with reason enum |
| `ContinueErrorReason` | `core/util/errors.ts` | 24-71 | Enum | Error | 29 error codes (Find&Replace, Multi-Edit, Files, etc.) |
| `getRootCause()` | `core/util/errors.ts` | 7-12 | Function | Error | Traverse err.cause chain |
| `Tool` | `core/index.d.ts` | 1132-1168 | Interface | Type | Full tool definition WITH runtime functions |
| `ContinueConfig` | `core/index.d.ts` | 1820-1841 | Interface | Type | Runtime config (core-side) WITH functions |
| `BrowserSerializedContinueConfig` | `core/index.d.ts` | 1843-1863 | Interface | Type | Serializable config (GUI-side) WITHOUT functions |
| `IDE` | `core/index.d.ts` | 831-936 | Interface | Type | IDE abstraction (read/write files, git, LSP, etc.) |
| `ILLM` | `core/index.d.ts` | (var lines) | Interface | Type | LLM provider interface (chat, complete, etc.) |
| `ModelDescription` | `core/index.d.ts` | 1226-1250 | Interface | Type | Serializable model metadata (for GUI) |
| `ToolExtras` | `core/index.d.ts` | 1111-1123 | Interface | Type | Context passed during tool execution |
| `ContinueSDK` | `core/index.d.ts` | 940-953 | Interface | Type | Context for slash commands |
| `ContextProviderDescription` | `core/index.d.ts` | (var lines) | Type | Type | Metadata sent to GUI |
| `ContextProviderExtras` | `core/index.d.ts` | (var lines) | Type | Type | Runtime passed to provider |
| `getBaseToolDefinitions()` | `core/tools/index.ts` | 6-16 | Function | Factory | Returns 9 base tools (readFile, createFile, etc.) |
| `getConfigDependentToolDefinitions()` | `core/tools/index.ts` | (var lines) | Function | Factory | Returns conditional tools (web search, experimental) |
| `serializeTool()` | `core/tools/index.ts` | 53-56 | Function | Util | Removes `preprocessArgs`, `evaluateToolCallPolicy` |
| `loadContinueConfigFromJson()` | `core/config/load.ts` | 791-898 | Function | Entry | Main config load pipeline |
| `finalToBrowserConfig()` | `core/config/load.ts` | 625-661 | Function | Serialize | Runtime → Browser serialization |
| `intermediateToFinalConfig()` | `core/config/load.ts` | 473-500 | Function | Util | Intermediate → ContinueConfig |
| `loadSerializedConfig()` | `core/config/load.ts` | 120-178 | Function | Util | JSON → SerializedContinueConfig |
| `BridgeServer` | `src/versions/v2.0.0/core-server.js` | 208-212 | Class | Node | Core-server bridge |
| `parseArgs()` | `src/versions/v2.0.0/core-server.js` | 595-618 | Function | Node | CLI arg parser (--version, --health-check, etc.) |
| `HandlerDispatcher` | `src/versions/v2.0.0/core-server.js` | imported | Class | Node | Routes messages to handlers |
| `setConfigResult()` | `gui/redux/slices/configSlice.ts` | line 51 | Action | Redux | Stores BrowserSerializedContinueConfig |
| `EMPTY_CONFIG` | `gui/redux/slices/configSlice.ts` | 12-44 | Const | Redux | Fallback with `tools: []` |
| `selectUIConfig()` | `gui/redux/slices/configSlice.ts` | line 76 | Selector | Redux | Get UI settings |
| `ToCoreFromIdeOrWebviewProtocol` | `core/protocol/core.ts` | 63+ | Type | Protocol | IDE/Webview → Core message types (config, context, tools, etc.) |
| `ToWebviewFromIdeOrCoreProtocol` | `core/protocol/webview.ts` | 11-44 | Type | Protocol | IDE/Core → Webview message types (configUpdate, indexProgress, etc.) |
| `ToCoreFromWebviewProtocol` | `core/protocol/coreWebview.ts` | 4-6 | Type | Protocol | Webview-specific extensions: `didChangeSelectedProfile` |
| `ToWebviewFromCoreProtocol` | `core/protocol/coreWebview.ts` | 7 | Type | Protocol | Core → Webview (same as ToWebviewFromIdeOrCoreProtocol) |
| `ToIdeFromWebviewProtocol` | `core/protocol/ideWebview.ts` | 16-53 | Type | Protocol | Webview → IDE (apply, show, focus, onboarding, etc.) |
| `ToWebviewFromIdeProtocol` | `core/protocol/ideWebview.ts` | 55-82 | Type | Protocol | IDE → Webview (session, theme, colors, edit mode, etc.) |
| `ToIdeFromWebviewOrCoreProtocol` | `core/protocol/ide.ts` | 21-91 | Type | Protocol | Webview/Core → IDE (file ops, git, LSP, debug, terminal) |
| `ToWebviewOrCoreFromIdeProtocol` | `core/protocol/ide.ts` | 93-95 | Type | Protocol | IDE → Webview/Core (didChangeActiveTextEditor) |
| `ToIdeFromCoreProtocol` | `core/protocol/ideCore.ts` | 4 | Type | Protocol | Core → IDE (re-export from ToIdeFromWebviewOrCoreProtocol) |
| `ToCoreFromIdeProtocol` | `core/protocol/ideCore.ts` | 5 | Type | Protocol | IDE → Core (re-export from ToCoreFromIdeOrWebviewProtocol) |
| `IProtocol` | `core/protocol/index.ts` | 12 | Type | Protocol | Base: `Record<string, [any, any]>` (request/response pair) |
| `ToIdeProtocol` | `core/protocol/index.ts` | 15 | Type | Protocol | Composite: AllWebview→IDE + AllCore→IDE |
| `FromIdeProtocol` | `core/protocol/index.ts` | 16-18 | Type | Protocol | Composite: AllIDE→Webview + AllIDE→Core + AllIDE↔Both |
| `ToWebviewProtocol` | `core/protocol/index.ts` | 21-23 | Type | Protocol | Composite: AllIDE→Webview + AllCore→Webview + AllIDE↔Both |
| `FromWebviewProtocol` | `core/protocol/index.ts` | 24-25 | Type | Protocol | Composite: AllWebview→IDE + AllWebview→Core |
| `ToCoreProtocol` | `core/protocol/index.ts` | 28-30 | Type | Protocol | Composite: AllIDE→Core + AllWebview→Core + AllIDE↔Both |
| `FromCoreProtocol` | `core/protocol/index.ts` | 31-32 | Type | Protocol | Composite: AllCore→Webview + AllCore→IDE |
| `Message<T>` | `core/protocol/messenger/index.ts` | 6-10 | Interface | Messenger | Generic message: `{ messageType, messageId, data }` |
| `IMessenger<ToP, FromP>` | `core/protocol/messenger/index.ts` | 21-49 | Interface | Messenger | Protocol: `send`, `on`, `request`, `invoke`, `onError` |
| `InProcessMessenger<ToP, FromP>` | `core/protocol/messenger/index.ts` | 51-149 | Class | Messenger | In-memory messenger (local protocol handler) |
| `MessageIde` | `core/protocol/messenger/messageIde.ts` | 21-230 | Class | Messenger | IDE proxy that sends requests over protocol |
| `ReverseMessageIde` | `core/protocol/messenger/reverseMessageIde.ts` | 6-172 | Class | Messenger | IDE listener that handles incoming protocol requests |
| `constructMcpSlashCommand()` | `core/commands/slash/mcpSlashCommand.ts` | 5-17 | Function | Builder | Construct SlashCommandWithSource from MCP client/name/description |
| `stringifyMcpPrompt()` | `core/commands/slash/mcpSlashCommand.ts` | 19-42 | Function | Formatter | Format MCP prompt messages with role tags (user/assistant) |
| `ChatDescriber` | `core/util/chatDescriber.ts` | 10-116 | Class (Static) | Generator | Auto-title sessions via LLM (16-token limit, 3-4 words) |
| `describe()` | `core/util/chatDescriber.ts` | 16-48 | StaticAsync | Generator | Generate title from chat history via model.chat() |
| `describeWithBaseLlmApi()` | `core/util/chatDescriber.ts` | 51-108 | StaticAsync | Generator | Generate title via BaseLlmApi (CLI fallback) |
| `GlobalContext` | `core/util/GlobalContext.ts` | 62-185 | Class | Storage | Persistent global state: workspace/model/OAuth/doc configs |
| `GlobalContext.update()` | `core/util/GlobalContext.ts` | 63-116 | Method | Storage | Write key→value to globalContext.json with salvage on corruption |
| `GlobalContext.get()` | `core/util/GlobalContext.ts` | 118-144 | Method | Storage | Read key from globalContext.json with recovery |
| `GlobalContext.getSharedConfig()` | `core/util/GlobalContext.ts` | 146-159 | Method | Storage | Load sharedConfig with validation; repair on error |
| `getContinueGlobalPath()` | `core/util/paths.ts` | 69-76 | Function | Paths | Get ~/.continue (or CONTINUE_GLOBAL_DIR env) ✓create if missing |
| `getSessionsFolderPath()` | `core/util/paths.ts` | 78-84 | Function | Paths | Get ~/.continue/sessions ✓create if missing |
| `getIndexFolderPath()` | `core/util/paths.ts` | 86-92 | Function | Paths | Get ~/.continue/index ✓create if missing |
| `getGlobalContextFilePath()` | `core/util/paths.ts` | 94-96 | Function | Paths | Get ~/.continue/index/globalContext.json |
| `getConfigYamlPath()` | `core/util/paths.ts` | 119-130 | Function | Paths | Get ~/.continue/config.yaml; create with defaults if missing |
| `getPrimaryConfigFilePath()` | `core/util/paths.ts` | 132-138 | Function | Paths | Prefer config.yaml over config.json |\r\n| `getTsConfigPath()` | `core/util/paths.ts` | 176-200 | Function | Paths | Setup tsconfig.json for user config.ts compilation |
| `getConfigTsPath()` | `core/util/paths.ts` | 140-169 | Function | Paths | Get ~/.continue/config.ts; setup types/ + package.json |
| `markProcessAsBackgrounded()` | `core/util/processTerminalStates.ts` | 19-21 | Function | StateTracker | Mark tool call as backgrounded (skip kill-on-abort) |
| `markProcessAsRunning()` | `core/util/processTerminalStates.ts` | 32-46 | Function | StateTracker | Track foreground process with output callback |
| `killTerminalProcess()` | `core/util/processTerminalStates.ts` | 70-86 | AsyncFunction | StateTracker | SIGTERM→5s→SIGKILL escalation for process |
| `getParserForFile()` | `core/util/treeSitter.ts` | 121-138 | AsyncFunction | Parser | Load tree-sitter parser for file extension |
| `getLanguageForFile()` | `core/util/treeSitter.ts` | 145-167 | AsyncFunction | Parser | Detect language name from file ext; load wasm (cached) |
| `getQueryForFile()` | `core/util/treeSitter.ts` | 174-198 | AsyncFunction | Parser | Load tree-sitter query (.scm file) for language |
| `LanguageName` | `core/util/treeSitter.ts` | 8-36 | Enum | Config | 22 languages (cpp, c_sharp, python, typescript, rust, etc.) |
| `supportedLanguages` | `core/util/treeSitter.ts` | 38-114 | Record | Config | File extension → LanguageName map (100+ entries) |
| `TTS` | `core/util/tts.ts` | 34-106 | Class (Static) | Audio | Cross-platform TTS (say/PowerShell/espeak) |
| `sanitizeMessageForTTS()` | `core/util/tts.ts` | 18-32 | Function | Audio | Remove unsafe chars for exec context |\r\n| `compactConversation()` | `core/util/conversationCompaction.ts` | 19-112 | AsyncFunction | Summary | Generate LLM summary of conversation history |
| `HistoryManager` | `core/util/history.ts` | 24-197 | Class | SessionMgr | Load/save/list/delete sessions from ~/.continue/sessions/ |
| `HistoryManager.list()` | `core/util/history.ts` | 25-58 | Method | SessionMgr | List sessions with filtering (workspace/limit/offset); reverse chronological |
| `HistoryManager.load()` | `core/util/history.ts` | 91-109 | Method | SessionMgr | Load session JSON; return NEW_SESSION_TITLE on missing |
| `HistoryManager.save()` | `core/util/history.ts` | 111-192 | Method | SessionMgr | Persist session + update sessions.json metadata |
| `toMarkDown()` | `core/util/historyUtils.ts` | 41-65 | Function | Exporter | Convert ChatMessage[] → markdown with blockquote format |
| `shareSession()` | `core/util/historyUtils.ts` | 67-105 | AsyncFunction | Exporter | Export session to markdown file; workspace-relative paths |
| `createNewPromptFileV2()` | `core/promptFiles/createNewPromptFile.ts` | 38-76 | AsyncFunction | Generator | Create ~/.continue/prompts/new-prompt-file.prompt with template |
| `Core` | `core/core.ts` | 89-1460 | Class (🔴 Main) | Orchestrator | Main Continue orchestrator: config/indexing/LLM/tools/history wiring |
| `Core.invoke()` | `core/core.ts` | 111-116 | Method | Protocol | Type-safe invoke on messenger (ToCoreProtocol) |
| `Core.send()` | `core/core.ts` | 118-124 | Method | Protocol | Fire-and-forget send to IDE/Webview (FromCoreProtocol) |
| `THEME_COLORS` | `gui/src/styles/theme.ts` | 5-183 | Object | Config | 30+ VSCode CSS vars with dark mode defaults (blue accent palette) |
| `THEME_CSS_VARS` | `gui/src/styles/theme.ts` | 188-190 | Const[] | Config | Flat array of all CSS variable names from THEME_COLORS |
| `THEME_CSS_VAR_DEFAULTS` | `gui/src/styles/theme.ts` | 192-200 | Record | Config | CSS var name → default color value mapping |
| `parseHexColor()` | `gui/src/styles/utils.ts` | 1-19 | Function | Utility | Parse hex color string → {r, g, b} |
| `parseColorForHex()` | `gui/src/styles/utils.ts` | 21-45 | Function | Utility | Parse CSS color var (hex/rgb/rgba) → hex string |
| `cn()` | `gui/src/util/cn.ts` | 18-20 | Function | Utility | Merge Tailwind CSS classes (clsx + tailwind-merge) |
| `isContinueTeamMember()` | `gui/src/util/isContinueTeamMember.ts` | 4-6 | Function | Validator | Check if email includes @continue.dev |
| `getLocalStorage()` | `gui/src/util/localStorage.ts` | 26-44 | Function | Storage | Type-safe localStorage getter with JSON parsing |
| `setLocalStorage()` | `gui/src/util/localStorage.ts` | 46-58 | Function | Storage | Type-safe localStorage setter with event dispatch |
| `LocalStorageKey` | `gui/src/util/localStorage.ts` | 21-24 | Enum | Storage | Type-safe keys for localStorage (IsExploreDialogOpen, HasDismissedExploreDialog) |
| `LocalStorageTypes` | `gui/src/util/localStorage.ts` | 4-19 | Type | Storage | 8+ typed fields (onboarding, dismissals, ide, font size, etc.) |
| `ConfigTab` | `gui/src/util/navigation.ts` | 2-9 | Type | Router | Union of config tab names (models, rules, tools, configs, indexing, settings, help) |
| `ROUTES` | `gui/src/util/navigation.ts` | 12-19 | Const | Router | Route constants (HOME, CONFIG, THEME, STATS) |
| `buildConfigRoute()` | `gui/src/util/navigation.ts` | 22-24 | Function | Router | Build config URL with optional tab parameter |
| `CONFIG_ROUTES` | `gui/src/util/navigation.ts` | 27-35 | Record | Router | Pre-built config route URLs for all tabs |
| `migrateLocalStorage()` | `gui/src/util/migrateLocalStorage.ts` | 66-68 | Function | Migration | Migrate legacy tool settings to new names |
| `getPlatform()` | `gui/src/util/index.ts` | 7-18 | Function | Platform | Detect OS (mac/linux/windows/unknown) |
| `isMetaEquivalentKeyPressed()` | `gui/src/util/index.ts` | 20-34 | Function | Keyboard | Platform-aware meta/ctrl detection |
| `getMetaKeyLabel()` | `gui/src/util/index.ts` | 36-38 | Function | Keyboard | Platform-aware meta key label (⌘ or Ctrl) |
| `getAltKeyLabel()` | `gui/src/util/index.ts` | 40-48 | Function | Keyboard | Platform-aware alt key label (⌥ or Alt) |
| `getFontSize()` | `gui/src/util/index.ts` | 50-52 | Function | UI | Get font size from localStorage (JetBrains: 15px, else: 14px) |
| `fontSize()` | `gui/src/util/index.ts` | 54-56 | Function | UI | Return computed font size with offset in px |
| `isJetBrains()` | `gui/src/util/index.ts` | 58-60 | Function | IDE | Check if running in JetBrains IDE |
| `isShareSessionSupported()` | `gui/src/util/index.ts` | 62 | Function | Feature | Share sessions NOT supported in JetBrains |
| `isWebEnvironment()` | `gui/src/util/index.ts` | 64-70 | Function | Environment | Check if NOT Electron (web browser environment) |
| `isPrerelease()` | `gui/src/util/index.ts` | 72-85 | Function | Version | Check if minor version is odd (prerelease) |
| `isLocalProfile()` | `gui/src/util/index.ts` | 87-89 | Function | Profile | Always returns true (TODO: implement profile check) |
| `useCompactConversation()` | `gui/src/util/compactConversation.ts` | 10-43 | Hook | Session | React hook for compacting conversation at specified index; handles loading state via `ideMessenger.request()` and `loadSession()` |
| `useDeleteCompaction()` | `gui/src/util/compactConversation.ts` | 45-58 | Hook | Session | React hook for deleting compaction; dispatches Redux action and saves current session |
| `assembleEditOutcomeData()` | `gui/src/util/editOutcomeLogger.ts` | 151-181 | Function | Logger | Assemble edit outcome telemetry object from history, config, tool call, and apply state |
| `logAgentModeEditOutcome()` | `gui/src/util/editOutcomeLogger.ts` | 186-208 | Function | Logger | Log Agent Mode edit to devdata via `ideMessenger.post("devdata/log", ...)` |
| `extractModelInfo()` | `gui/src/util/editOutcomeLogger.ts` | 12-50 | Function | Logger | Extract model provider/name from assistant message or config |
| `extractPromptAndCompletion()` | `gui/src/util/editOutcomeLogger.ts` | 55-117 | Function | Logger | Extract user prompt and assistant completion from history |
| `extractCodeChanges()` | `gui/src/util/editOutcomeLogger.ts` | 122-146 | Function | Logger | Extract code diffs and line counts from ApplyState |
| `ErrorAnalysis` | `gui/src/util/errorAnalysis.ts` | 3-12 | Interface | Analyzer | Error breakdown: parsedError, statusCode, message, model/provider info, help/API key URLs, custom error message |
| `analyzeError()` | `gui/src/util/errorAnalysis.ts` | 38-177 | Function | Analyzer | Analyze LLM error with provider detection; map error patterns (401/invalid API key, 402/insufficient balance, OpenAI org verification, Ollama tool call parsing, etc.) to custom messages |
| `parseErrorMessage()` | `gui/src/util/errorAnalysis.ts` | 14-36 | Function | Analyzer | Parse multi-line error message; split on `\n\n`, extract JSON error/message fields |
| `addToolCallDeltaToState()` | `gui/src/util/toolCallState.ts` | 8-70 | Function | Utilities | Merge streamed tool call delta into tool call state; handle incremental name/args streaming with JSON validation |
| `isEditTool()` | `gui/src/util/toolCallState.ts` | 77-79 | Function | Utilities | Check if tool name is EditExistingFile, SingleFindAndReplace, or MultiEdit |
| `triggerConfigUpdate()` | `gui/src/util/test/config.ts` | 15-33 | Function | Test | Test helper to trigger config update via ideMessenger.mockMessageToWebview() |
| `addAndSelectChatModel()` | `gui/src/util/test/config.ts` | 35-49 | Function | Test | Add model to config and select as chat role (uses triggerConfigUpdate) |
| `addAndSelectMockLlm()` | `gui/src/util/test/config.ts` | 51-61 | Function | Test | Add mock LLM to config for testing (provider="mock") |
| `logAllTestIds()` | `gui/src/util/test/utils.ts` | 26-35 | Function | Test | Log all data-testid attributes in DOM to console |
| `getElementByTestId()` | `gui/src/util/test/utils.ts` | 36-45 | Function | Test | Async wrapper around screen.findByTestId with fallback error logging |
| `verifyNotPresentByTestId()` | `gui/src/util/test/utils.ts` | 47-50 | Function | Test | Assert element with testId is not in DOM |
| `getElementByText()` | `gui/src/util/test/utils.ts` | 52-66 | Function | Test | Async wrapper around screen.findByText with logDomText fallback |
| `getMainEditor()` | `gui/src/util/test/utils.ts` | 68-75 | Function | Test | Get Tiptap Editor instance from editor container |
| `sendInputWithMockedResponse()` | `gui/src/util/test/utils.ts` | 77-102 | Function | Test | Simulate user input with mocked LLM response (set ideMessenger.chatResponse, click send button) |
| `getEmptyRootState()` | `gui/src/util/test/mockStore.ts` | 26-51 | Function | Test | Create Redux root state with all slices initialized; handles non-serializable streamAborter |
| `createMockStore()` | `gui/src/util/test/mockStore.ts` | 53-114 | Function | Test | Create Redux store with mock ideMessenger extra, action tracking, and thunk dispatch override |
| `ExtendedRenderOptions` | `gui/src/util/test/render.tsx` | 17-21 | Type | Test | RTL render options extended with store, routerProps, mockIdeMessenger |
| `renderWithProviders()` | `gui/src/util/test/render.tsx` | 31-77 | Function | Test | Async RTL render wrapper with Redux Provider, MemoryRouter, AuthProvider, IdeMessengerProvider, MainEditorProvider, and ResizeObserver mock |
| `editToolImpl` | `gui/src/util/clientTools/editImpl.ts` | 6-53 | ClientToolImpl | Tool | Execute EditExistingFile: resolve path, dispatch applyForEditTool |
| `multiEditImpl` | `gui/src/util/clientTools/multiEditImpl.ts` | 8-43 | ClientToolImpl | Tool | Execute MultiEdit: validate, read file, execute find+replace, dispatch apply |
| `singleFindAndReplaceImpl` | `gui/src/util/clientTools/singleFindAndReplaceImpl.ts` | 8-51 | ClientToolImpl | Tool | Execute SingleFindAndReplace: validate, read file, execute replace, dispatch apply |
| `callClientTool()` | `gui/src/util/clientTools/callClientTool.ts` | 31-68 | Function | Dispatcher | Route client tool calls (EditExistingFile→editImpl, SingleFindAndReplace→singleImpl, MultiEdit→multiImpl) |
| `ClientToolExtras` | `gui/src/util/clientTools/callClientTool.ts` | 10-14 | Interface | Tool | Extras object: getState, dispatch (AppThunkDispatch), ideMessenger (IIdeMessenger) |
| `ClientToolImpl` | `gui/src/util/clientTools/callClientTool.ts` | 25-29 | Type | Tool | Function type: (args, toolCallId, extras) → Promise<ClientToolOutput> |
| `ClientToolOutput` | `gui/src/util/clientTools/callClientTool.ts` | 16-19 | Interface | Tool | output (ContextItem[] | undefined), respondImmediately (boolean) |
| `ClientToolResult` | `gui/src/util/clientTools/callClientTool.ts` | 21-23 | Interface | Tool | Extends ClientToolOutput + optional error (ContinueError) |

---

## 🟢 PROTOCOL: Message Types & Messenger Infrastructure (10 files, 1,082 lines)

**Overview**: The protocol system defines bidirectional message contracts between IDE, Core, and Webview using TypeScript discriminated unions. Each side sends/receives typed messages with guaranteed request/response pairs.

### Protocol File Structure (Dependency Order)

```
core/protocol/
├─ core.ts (348 lines)
│  ├─ ToCoreFromIdeOrWebviewProtocol: ~100+ message types (config, history, context, tools, mcp, etc.)
│  ├─ OnboardingModes enum
│  └─ ListHistoryOptions interface
│
├─ webview.ts (44 lines)
│  └─ ToWebviewFromIdeOrCoreProtocol: 9 message types (configUpdate, indexProgress, etc.)
│
├─ ide.ts (95 lines)
│  ├─ ToIdeFromWebviewOrCoreProtocol: 40+ IDE methods mapped to message types
│  └─ ToWebviewOrCoreFromIdeProtocol: 1 message type (didChangeActiveTextEditor)
│
├─ coreWebview.ts (7 lines) [COMPOSITE]
│  ├─ ToCoreFromWebviewProtocol = ToCoreFromIdeOrWebviewProtocol + { didChangeSelectedProfile }
│  └─ ToWebviewFromCoreProtocol = ToWebviewFromIdeOrCoreProtocol
│
├─ ideWebview.ts (82 lines) [COMPOSITE]
│  ├─ ToIdeFromWebviewProtocol = ToIdeFromWebviewOrCoreProtocol + UI-specific (apply, show, edit, etc.)
│  └─ ToWebviewFromIdeProtocol = ToWebviewFromIdeOrCoreProtocol + IDE-specific (session, theme, etc.)
│
├─ ideCore.ts (5 lines) [COMPOSITE]
│  ├─ ToIdeFromCoreProtocol = ToIdeFromWebviewOrCoreProtocol (reuse)
│  └─ ToCoreFromIdeProtocol = ToCoreFromIdeOrWebviewProtocol (reuse)
│
├─ index.ts (32 lines) [COMPOSITE - ROUTING HUB]
│  ├─ IProtocol: base type `Record<string, [any, any]>`
│  ├─ ToIdeProtocol = ToIdeFromWebviewProtocol & ToIdeFromCoreProtocol
│  ├─ FromIdeProtocol = ToWebviewFromIdeProtocol & ToCoreFromIdeProtocol & ToWebviewOrCoreFromIdeProtocol
│  ├─ ToWebviewProtocol = ToWebviewFromIdeProtocol & ToWebviewFromCoreProtocol & ToWebviewOrCoreFromIdeProtocol
│  ├─ FromWebviewProtocol = ToIdeFromWebviewProtocol & ToCoreFromWebviewProtocol
│  ├─ ToCoreProtocol = ToCoreFromIdeProtocol & ToCoreFromWebviewProtocol & ToWebviewOrCoreFromIdeProtocol
│  └─ FromCoreProtocol = ToWebviewFromCoreProtocol & ToIdeFromCoreProtocol
│
└─ messenger/
   ├─ index.ts (185 lines)
   │  ├─ Message<T>: generic message envelope
   │  ├─ FromMessage<From, T>: typed response wrapper
   │  ├─ IMessenger<To, From>: async/sync interface
   │  └─ InProcessMessenger<To, From>: in-memory implementation
   │
   ├─ messageIde.ts (230 lines)
   │  └─ MessageIde: wraps IDE in remote proxy (sends requests over protocol)
   │
   └─ reverseMessageIde.ts (172 lines)
      └─ ReverseMessageIde: unwraps protocol to IDE (receives requests, calls local IDE)
```

### Protocol Message Categories

| Category | File | Count | Examples |
|----------|------|-------|----------|
| **Config** | `core.ts` | 13 | `config/addModel`, `config/refreshProfiles`, `config/getSerializedProfileInfo` |
| **History** | `core.ts` | 6 | `history/list`, `history/delete`, `history/load`, `history/save` |
| **Context** | `core.ts` | 1+ | `context/getContextItems` (UI querying for context) |
| **MCP** | `core.ts` | 4+ | `mcp/reloadServer`, `mcp/getPrompt`, `mcp/setServerEnabled` |
| **Tools** | `passThrough.ts` | 3 | `tools/call`, `tools/evaluatePolicy`, `tools/preprocessArgs` |
| **LLM** | `passThrough.ts` | 4 | `llm/complete`, `llm/streamChat`, `llm/listModels` |
| **IDE File Ops** | `ide.ts` | 15+ | `readFile`, `writeFile`, `readRangeInFile`, `saveFile` |
| **IDE Git** | `ide.ts` | 3 | `getBranch`, `getRepoName`, `getGitRootPath` |
| **IDE LSP** | `ide.ts` | 6 | `gotoDefinition`, `getReferences`, `getDocumentSymbols` |
| **Webview UI** | `webview.ts` | 9 | `configUpdate`, `indexProgress`, `addContextItem`, `sessionUpdate` |
| **Webview/IDE Specific** | `ideWebview.ts` | 30+ | `applyToFile`, `showFile`, `focusEditor`, `addToChat`, `setTheme` |
| **Shared Events** | `ide.ts` | 1 | `didChangeActiveTextEditor` (both directions) |

### Key Protocol Types (Core Definitions)

**Base Protocol Message (ToCoreFromIdeOrWebviewProtocol - 348 lines, lines 63+):**

```typescript
{
  // System
  ping: [string, string]                   // Health check
  abort: [undefined, void]                 // Cancel ongoing operation

  // History (6 types)
  history/list: [ListHistoryOptions, BaseSessionMetadata[]]
  history/delete: [{ id: string }, void]
  history/load: [{ id: string }, Session]

  // Config (13 types) 
  config/addModel: [{ model, role? }, void]
  config/refreshProfiles: [undefined | { selectProfileId? }, void]
  config/getSerializedProfileInfo: [undefined, { result, profileId, profiles }]

  // MCP (4+ types)
  mcp/reloadServer: [{ id: string }, void]
  mcp/getPrompt: [{ serverName, promptName, args? }, { prompt, description }]

  // Context (1+ types)
  context/getContextItems: [{ name, query, selectedCode, isInAgentMode }, ContextItemWithId[]]
}
```

**Webview-specific Additions (coreWebview.ts lines 4-6):**

```typescript
ToCoreFromWebviewProtocol = ToCoreFromIdeOrWebviewProtocol & {
  didChangeSelectedProfile: [{ id: string }, void]
}
```

**IDE Abstraction (ide.ts lines 21-91):**

Maps all `IDE` interface methods to protocol message types:

| IDE Method | Protocol Message | Example |
|-----------|-----------------|---------|
| `readFile(path)` | `readFile: [{ filepath }, string]` | Line 36 |
| `writeFile(path, contents)` | `writeFile: [{ path, contents }, void]` | Line 25 |
| `getProblems(filepath)` | `getProblems: [{ filepath }, Problem[]]` | Line 37 |
| `gotoDefinition(location)` | `gotoDefinition: [{ location }, RangeInFile[]]` | Line 83 |
| `getReferences(location)` | `getReferences: [{ location }, RangeInFile[]]` | Line 86 |
| `subprocess(command, cwd)` | `subprocess: [{ command, cwd }, [string, string]]` | Line 33 |

**Messenger Interface (IMessenger - lines 21-49):**

```typescript
interface IMessenger<ToProtocol, FromProtocol> {
  // Async: send message, wait for response
  request<T extends keyof FromProtocol>(
    messageType: T,
    data: FromProtocol[T][0]
  ): Promise<FromProtocol[T][1]>

  // Sync: register handler, auto-routes to registered function
  on<T extends keyof ToProtocol>(
    messageType: T,
    handler: (msg: Message<ToProtocol[T][0]>) => ToProtocol[T][1]
  ): void

  // Sync: invoke registered handler (for in-process only)
  invoke<T extends keyof ToProtocol>(
    messageType: T,
    data: ToProtocol[T][0]
  ): ToProtocol[T][1]

  // Fire-and-forget: send message without waiting for response
  send<T extends keyof FromProtocol>(
    messageType: T,
    data: FromProtocol[T][0]
  ): string // messageId
}
```

### Messenger Implementations

**1. InProcessMessenger (in-memory, lines 51-149):**
- Used when Core and IDE are in the same process
- `myTypeListeners`: Core's handlers (responding to ToProtocol messages)
- `externalTypeListeners`: IDE's handlers (responding from FromProtocol messages)
- No serialization; direct function calls

**2. MessageIde (remote proxy, lines 21-230):**
- Wraps `request()` and `on()` functions (passed via constructor)
- Implements `IDE` interface
- All IDE method calls → `request()` over protocol (async)
- Example: `readFile(filepath)` → `request("readFile", { filepath })` → waits for response

**3. ReverseMessageIde (remote listener, lines 6-172):**
- Constructor takes `_on()` handler and local `IDE` instance
- In `initializeListeners()`: registers all IDE protocol message handlers
- Each handler calls the local IDE method and returns result
- Example: `on("readFile", (data) => ide.readFile(data.filepath))`

### Protocol Routing Example

```
Webview calls: config.refreshProfiles()

→ Webview sends: Message<ToCoreFromWebviewProtocol["config/refreshProfiles"]>
  {
    messageType: "config/refreshProfiles",
    messageId: "uuid-123",
    data: { selectProfileId: "profile-456" }
  }

→ Core's InProcessMessenger.on("config/refreshProfiles", handler)
  handler(msg) receives { selectProfileId: "profile-456" }
  → returns: void (or Promise<void>)

→ Webview waits on: messenger.request("config/refreshProfiles", {...})
  → Promise resolves with: void
```

---

## 🟡 LLM UTILITIES: Autodetection, Conversion, Logging, Token Counting (5 files, 2,253 lines)

**Overview**: Utilities for model capability detection, OpenAI message conversion, LLM operation logging, and token counting for context window management.

### LLM Utility Files

| File | Lines | Purpose | Key Exports |
|------|-------|---------|------------|
| `autodetect.ts` | 537 | **Model capability detection** | `autodetectTemplateType()`, provider/model capability lists |
| `openaiTypeConverters.ts` | 1120 | **OpenAI message format conversion** | `toChatMessage()`, `toOpenAIFunction()`, streaming response handlers |
| `logFormatter.ts` | 426 | **Human-readable LLM log formatting** | `LLMLogFormatter` class, multiline interaction formatting |
| `logger.ts` | 43 | **LLM event logging infrastructure** | `LLMLogger`, `LLMInteractionLog` |
| `countTokens.ts` | 570 | **Token counting for context window** | `countTokens()`, `countTokensAsync()`, `countToolsTokens()` |

### 1. Autodetect: Model Capability Detection (autodetect.ts - 537 lines)

**Purpose**: Determine model capabilities and prompt templates based on provider/model name.

**Key Constants (Lines 46-136)**:

| List | Count | Purpose | Examples |
|------|-------|---------|----------|
| `PROVIDER_HANDLES_TEMPLATING` | 24 providers | Providers that handle chat templates server-side | openai, ollama, anthropic, groq, gemini |
| `PROVIDER_SUPPORTS_IMAGES` | 20 providers | Providers with image input support | openai, anthropic, gemini, cohere, bedrock |
| `MODEL_SUPPORTS_IMAGES` | 10+ regex | Models with vision capabilities | gpt-4o, gpt-4-turbo, claude-3, gemini-1.5, llava |

**Main Export**:
```typescript
autodetectTemplateType(modelName: string): TemplateType | undefined
// Returns: "anthropic" | "chatml" | "llama2" | "llama3" | "deepseek" | "mistral" | etc.
// Maps model names to prompt format handlers
```

**Template Message Imports** (Lines 10-27): 16 template providers for chat formatting (e.g., `llama2TemplateMessages`, `chatmlTemplateMessages`, `anthropicTemplateMessages`)

**Edit Prompt Imports** (Lines 28-44): 13 edit prompt templates for code generation (e.g., `gptEditPrompt`, `claudeEditPrompt`, `mistralEditPrompt`)

### 2. OpenAI Type Converters: Message Format Translation (openaiTypeConverters.ts - 1,120 lines)

**Purpose**: Convert Continue message types to OpenAI API format (and vice versa) for streaming, tool calls, and reasoning models.

**Key Functions**:

| Function | Input | Output | Purpose | Lines |
|----------|-------|--------|---------|-------|
| `toChatMessage()` | `ChatMessage` + options | `ChatCompletionMessageParam \| null` | Convert to OpenAI format with reasoning field support | 109-180+ |
| `appendReasoningFieldsIfSupported()` | message + capabilities | modified message | Add o1/DeepSeek reasoning fields (`reasoning`, `reasoning_content`, `reasoning_details`) | 45-107 |
| `toOpenAIFunction()` | `Tool` | `ChatCompletionCreateParams.Function` | Convert tool definition to OpenAI function schema | (var lines) |
| Streaming handlers | `ResponseStreamEvent` | `ToolCallDelta`, `ContentDelta`, etc. | Handle OpenAI streaming response types (includes reasoning streaming) | (var lines) |

**Reasoning Model Support** (Lines 45-107):
- Handles `reasoning` field (o1 model)
- Handles `reasoning_content` field (DeepSeek Reasoner)
- Handles `reasoning_details` field (Claude with signed reasoning)
- Provider-specific flag configuration: `includeReasoningField`, `includeReasoningDetailsField`, `includeReasoningContentField`

**Special Cases**:
- Thinking messages → merged into following assistant messages (return `null`)
- Tool messages → mapped to OpenAI `{ role: "tool", tool_call_id, content }`
- Empty content → replaced with space (some providers reject empty)
- Stripping images when converting to non-vision models

### 3. Log Formatter: Human-Readable LLM Logs (logFormatter.ts - 426 lines)

**Purpose**: Format LLM interactions (chat, complete, fim) with timestamps and multi-interaction support.

**Key Class: LLMLogFormatter**

**Constructor** (Lines 93-101):
```typescript
constructor(
  logger: LLMLogger,
  output: Writable,
  wrapWidth: number = 100
)
```

**Features**:
- Multi-interaction support with prefix markers (line 19): ` ` (space), `|`, `&`, `%`, `#`
- Absolute timestamp on first line + relative deltas for subsequent lines
- Line wrapping at 100 chars by default (preserves code formatting)
- Interaction tracking via `interactions` map (line 78)

**Log Format Example** (Lines 45-75):
```
01:23:45.6 [Chat]
           Options: {
             "maxTokens": 1000,
           }
           Role: system
           | You are a helpful assistant.
      +0.2 Role: assistant
           | How can I help you today?
|01:23:46.1 [Complete]  ← Second interaction starts while first continues
|           Options: {...}
      +0.3 Success
           PromptTokens: 50
```

**Tracked Fields**:
- `interactions`: active LLM operations indexed by `interactionId`
- `lastLineStartItem`: for determining when to emit timestamps
- `openLine`: tracks if output stream has unclosed line

### 4. Logger: LLM Event Emission (logger.ts - 43 lines)

**Purpose**: Centralized event bus for LLM operations (chat, complete, fim).

**Key Classes**:

| Class | Methods | Purpose |
|-------|---------|---------|
| `LLMLogger` | `createInteractionLog()`, `onLogItem()` | Hub for logging events across interactions |
| `LLMInteractionLog` | `logItem()` | Per-interaction event recorder |

**Flow**:
1. `LLMLogger.createInteractionLog()` → returns `LLMInteractionLog` with unique `interactionId`
2. During operation: `log.logItem({ type, timestamp, ... })`
3. Logger broadcasts to all listeners: `onLogItem((item) => ...)`
4. `LLMLogFormatter` receives items and formats output

**Interaction ID Generation** (line 14):
```typescript
public createInteractionLog(): LLMInteractionLog {
  return new LLMInteractionLog(this, (this.nextId++).toString());
}
```

### 5. Token Counting: Context Window Management (countTokens.ts - 570 lines)

**Purpose**: Count tokens for content/messages to manage context window and model constraints.

**Key Functions**:

| Function | Input | Output | Purpose | Lines |
|----------|-------|--------|---------|-------|
| `countTokens()` | `MessageContent`, modelName | number | Sync token count (uses cached GPT or Llama tokenizer) | 112-132 |
| `countTokensAsync()` | `MessageContent`, modelName | Promise<number> | Async token count (uses worker pool when available) | 94-110 |
| `countToolsTokens()` | `Tool[]`, modelName | number | Count tokens used by tool definitions (12 base + per-tool overhead) | 135-150+ |

**Tokenizer Strategy** (Lines 52-85):
- GPT models → js-tiktoken library (lines 77-81)
- Llama models → llamaTokenizer (line 53)
- Autodetect based on model name via `autodetectTemplateType()` (line 10)
- Worker pool for async (when `IS_BINARY` false), otherwise synchronous

**Encoding Classes**:

| Class | Purpose |
|-------|---------|
| `LlamaEncoding` (lines 28-36) | Sync interface to llamaTokenizer |
| `NonWorkerAsyncEncoder` (lines 38-50) | Async wrapper for sync encoders |
| `LlamaAsyncEncoder` (imported from asyncEncoder.js) | Worker-based async Llama tokenizer |

**Token Counting Details**:
- Image tokens → 1024 per image (conservative estimate, line 88)
- Tool tokens base → 12 (OpenAI overhead, line 139)
- Per-tool tokens → name + description + parameter keys (lines 141-149)
- **Adjustment** → calls `getAdjustedTokenCountFromModel()` for model-specific overrides (line 21, 131)

**Token Counts Applied** (line 1):
- `DEFAULT_PRUNING_LENGTH = 128,000` (imported from constants.ts)
- Determines when to prune old messages from context

---

## 🟡 LLM UTILITIES (Continued): Token Adjustment, Tool Support, System Prompts, Model Discovery (4 files, 910 lines)

**Overview**: Complementary LLM utilities for token count adjustment, tool capability detection, default system messages, and model discovery from external registries.

### Additional LLM Utility Files

| File | Lines | Purpose | Key Exports |
|------|-------|---------|------------|
| `getAdjustedTokenCount.ts` | 38 | **Token count normalization** | `getAdjustedTokenCountFromModel()` with 3 multipliers |
| `toolSupport.ts` | 523 | **Tool calling capability matrix** | `PROVIDER_TOOL_SUPPORT` record for 20+ providers |
| `defaultSystemMessages.ts` | 91 | **Mode-specific system prompts** | Chat/Agent/Plan mode defaults + formatting rules |
| `fetchModels.ts` | 258 | **Model discovery from registries** | Ollama + OpenRouter model fetchers |

### 1. Token Count Adjustment (getAdjustedTokenCount.ts - 38 lines)

**Purpose**: Normalize token counts across models that use different tokenizers (GPT vs Claude vs Gemini).

**Key Function** (Lines 20-38):
```typescript
export function getAdjustedTokenCountFromModel(
  baseTokens: number,
  modelName: string,
): number {
  // Apply provider-specific multipliers (safety buffers)
  // Then Math.ceil() for conservative estimate
}
```

**Multipliers** (Lines 7-9):
Based on empirical token ratio differences from https://medium.com/@disparate-ai/not-all-tokens-are-created-equal:

| Provider | Multiplier | Reason |
|----------|-----------|--------|
| **Claude (Anthropic)** | 1.23 | Anthropic tokenizer ~23% more tokens |
| **Gemini** | 1.18 | Gemini tokenizer ~18% more tokens |
| **Mistral** (incl. Mixtral) | 1.26 | Mistral family ~26% more tokens |
| **Others** (OpenAI, Llama, etc.) | 1.0 | Use GPT/Llama baseline |

**Why**: Can't import all tokenizers (MB-scale each). Using safety buffers prevents context overflow.

---

### 2. Tool Support Detection (toolSupport.ts - 523 lines)

**Purpose**: Determine if a model supports tool/function calling by provider and model name.

**Key Export** (Lines 3-150+):
```typescript
export const PROVIDER_TOOL_SUPPORT: Record<string, (model: string) => boolean> = {
  anthropic: (model) => { /* Claude v3+ but not v2 */ },
  openai: (model) => { /* GPT-4, GPT-3.5-turbo, o1+, Codex */ },
  azure: (model) => { /* GPT-4, o1+ */ },
  gemini: (model) => { /* All gemini models */ },
  mistral: (model) => { /* Large, Small, Nemo, Codestral, etc. */ },
  ollama: (model) => { /* Specific model families */ },
  // ... 14 more providers
};
```

**Providers Supported** (20+):
- anthropic, openai, azure, gemini, vertexai, cohere
- mistral, bedrock, xAI, ollama, groq
- cerebras, deepinfra, fireworks, together, replicate
- ... and more

**Tool Support Rules by Provider**:

| Provider | Rule | Examples |
|----------|------|----------|
| **Anthropic** | Claude v3+ (exclude v2 & instant) | claude-3-opus, claude-3.5-sonnet |
| **OpenAI** | GPT-4/3.5+, o1+, Codex, Gemma, special models | gpt-4-turbo, o1, gpt-oss, codex |
| **Mistral** | Exclude Mamba, include Large/Small/Nemo/Codestral | mistral-large, codestral, mixtral-8x22b |
| **Ollama** | Per-model family, exclude :cloud variants | llama, mistral, deepseek, qwen, command, granite |
| **Bedrock** | Claude/Nova/DeepSeek (exclude v2) | claude-3-sonnet, nova-pro |
| **Gemini** | All (except Lite on VertexAI) | gemini-2.0-flash, gemini-1.5-pro |

---

### 3. Default System Messages (defaultSystemMessages.ts - 91 lines)

**Purpose**: Provide mode-specific system prompts that guide LLM behavior.

**3 Mode-Specific Prompts**:

| Message | Mode | Purpose | Key Rules |
|---------|------|---------|-----------|
| `DEFAULT_CHAT_SYSTEM_MESSAGE` | **Chat** | User asks for changes | Offer Apply Button; mention Agent Mode; include formatting rules |
| `DEFAULT_AGENT_SYSTEM_MESSAGE` | **Agent** | Auto-make changes | Call read-only tools simultaneously; use edit tools for writes; abbreviated code snippets |
| `DEFAULT_PLAN_SYSTEM_MESSAGE` | **Plan** | Understand & plan | Read-only only; no write tools; offer Agent Mode for implementation |

**Shared Formatting Instructions** (injected into all 3):

| Instruction | Lines | Purpose |
|-------------|-------|---------|
| `CODEBLOCK_FORMATTING_INSTRUCTIONS` | 4-7 | Always include language + file path in code blocks |
| `EDIT_CODE_INSTRUCTIONS` | 9-47 | Use abbreviated placeholders (`// ... existing code ...`) for unmodified sections |
| `BRIEF_LAZY_INSTRUCTIONS` | 49 | For 20+ line blocks, use lazy comments |

**Chat Mode Example** (Lines 51-60):
```
You are in chat mode.
If the user asks to make changes offer that they can use the Apply Button on the code block, 
or switch to Agent Mode to make the suggested updates automatically.
[FORMATTING RULES INJECTED]
```

**Agent Mode Example** (Lines 62-74):
```
You are in agent mode.
If you need to use multiple tools, you can call multiple read-only tools simultaneously.
[FORMATTING RULES INJECTED]
For implementing changes, use the edit tools.
```

**Plan Mode Example** (Lines 78-91):
```
You are in plan mode, in which you help the user understand and construct a plan.
Only use read-only tools. Do not use any tools that would write to non-temporary files.
[FORMATTING RULES INJECTED]
When ready to implement changes, request to switch to Agent mode.
```

---

### 4. Model Discovery (fetchModels.ts - 258 lines)

**Purpose**: Dynamically discover available models from external registries (Ollama, OpenRouter).

**Data Structure** (Lines 3-11):
```typescript
interface FetchedModel {
  name: string;           // Display name
  modelId?: string;       // Provider model ID
  description?: string;   // Brief description
  icon?: string;          // Brand icon filename
  contextLength?: number; // Token window size
  maxTokens?: number;     // Max completion tokens
  supportsTools?: boolean;// Tool calling capability
}
```

**Function 1: fetchOllamaModels() (Lines 64-122)**
- **Source**: Scrapes `https://ollama.com/library`
- **HTML Parsing**: Extract model names via `x-test-model class=`
- **Capabilities Filtering**: Skip models with vision, audio, embedding
- **Deduplication**: Track seen models via Set
- **Returns**: Array of `FetchedModel` with Ollama icon mappings

**Function 2: fetchOpenRouterModels() (Lines 124-150+)**
- **Source**: Calls `https://openrouter.ai/api/v1/models`
- **JSON Response**: Map from `data.data[]` array
- **Fields Mapped**: name, id (modelId), context_length, top_provider.max_completion_tokens, supported_parameters
- **Returns**: Array of `FetchedModel` with OpenRouter icon

**Icon Mapping** (Lines 15-49):
```typescript
const OLLAMA_ICON_MAP: Record<string, string> = {
  llama: "meta.png",
  mistral: "mistral.png",
  deepseek: "deepseek.png",
  qwen: "qwen.png",
  command: "cohere.png",
  granite: "ibm.png",
  // ... 40+ mappings
};
```

**Capability Exclusions** (Line 13):
```typescript
const OLLAMA_EXCLUDED_CAPABILITIES = ["vision", "audio", "embedding"];
```
Models with these tags are excluded (assumed too specialized for code).

**Icon Selection Logic** (Lines 51-62):
1. Check exact match in `OLLAMA_ICON_MAP`
2. Find longest prefix match (e.g., "wizard-" prefix for "wizard-coder")
3. Fall back to "ollama.png"

**Error Handling**:
- Network failures → log error, return empty array
- Missing fields → skip model
- Malformed responses → graceful degradation

---

## 🟢 CORE LLM INFRASTRUCTURE: Chat Streaming, Provider Registry, Lemonade & Ollama (4 files, 2496 lines)

**Overview**: The core LLM runner (`llmStreamChat`) orchestrates chat requests via slash commands or direct model calls, backed by a `BaseLLM` abstract class that all providers (OpenAI, Anthropic, Ollama, Lemonade, etc.) inherit from. Lemonade wraps OpenAI-compatible APIs, while Ollama provides local model inference with advanced feature detection, model installation, tool calling, and reasoning support.

### 1. LLM Chat Streaming Orchestrator (streamChat.ts - 147 lines)

**Purpose**: Entry point for GUI chat requests; routes through slash-commands or direct LLM provider calls.

**Main Export** (Lines 9-147):
```typescript
export async function* llmStreamChat(
  configHandler: ConfigHandler,
  abortController: AbortController,
  msg: Message<ToCoreProtocol["llm/streamChat"][0]>,
  ide: IDE,
  messenger: IMessenger<ToCoreProtocol, FromCoreProtocol>,
): AsyncGenerator<ChatMessage, PromptLog> { ... }
```

**Input Message Structure** (Lines 26-31):
```typescript
const {
  legacySlashCommandData,       // Slash-cmd metadata (if any)
  completionOptions,            // maxTokens, temperature, etc.
  messages,                      // Chat history
  messageOptions,                // Additional rendering hints
} = msg.data;
```

**Dual Routing Logic**:

| Condition | Path | Handler | Returns |
|-----------|------|---------|---------|
| **Has slash-command data** | Lines 52-113 | Execute `slashCommand.run()` generator, stream chunks as `{ role: "assistant", content: chunk }` | PromptLog from generator |
| **Direct model call** | Lines 114-142 | Call `model.streamChat()` generator, stream response chunks | PromptLog from generator |

**Error Handling** (Lines 40-49):
Pre-constructed `errorPromptLog` with model title, provider name, and completion options to return on abort/error.

**TTS Integration** (Lines 22-24, 133-135):
- On stream start: `TTS.kill()` (stop any prior read)
- On stream end: `TTS.read(nextValue.completion)` if `config.experimental.readResponseTTS`

**Abort Flow** (Lines 96-100, 122-126):
- Check `abortController.signal.aborted`
- If aborted: call `gen.return(errorPromptLog)` to unwind generator
- Break loop

---

### 2. Base LLM Class & Provider Registry (index.ts - 1504 lines total, 400 shown)

**Purpose**: Abstract base class implementing the ILLM interface; handles templating, token counting, logging, config serialization, and model capability detection.

**Core Class** (Lines 90-303+):
```typescript
export abstract class BaseLLM implements ILLM {
  static providerName: string;
  static defaultOptions: Partial<LLMOptions> | undefined;

  // Provider-specific capability flags (overridable by subclasses)
  protected supportsReasoningField: boolean = false;
  protected supportsReasoningDetailsField: boolean = false;
  protected supportsReasoningContentField: boolean = false;

  // Instance properties
  uniqueId: string;
  model: string;
  title?: string;
  apiKey?: string;
  apiBase?: string;

  // Token & context
  _contextLength: number | undefined;
  completionOptions: CompletionOptions;

  constructor(_options: LLMOptions) { ... }
  get providerName(): string { ... }
  get contextLength(): number { ... }
  get underlyingProviderName(): string { ... }
}
```

**Capability Detection Methods** (Lines 108-143):

| Method | Purpose | Rules |
|--------|---------|-------|
| `supportsImages()` | Check if model can accept image inputs | Delegates to `modelSupportsImages()` from autodetect.ts |
| `supportsFim()` | Fill-in-the-middle completion | Ollama, Mistral only (default false) |
| `supportsCompletions()` | Legacy completions endpoint | Disable for Groq, Mistral, DeepSeek + special bases |
| `supportsPrefill()` | Prompt prefix continuation | Ollama, Anthropic, Mistral only |

**Constructor Flow** (Lines 206-303):
1. Merge defaults + user options
2. Call `findLlmInfo()` to auto-detect context length, max tokens from model name
3. Auto-detect template type via `autodetectTemplateType(model)`
4. Load/merge prompt templates
5. Load logger, hooks, API key location
6. Create OpenAI adapter via `constructLlmApi()`
7. Set embedding batch/chunk sizes

**Key Properties** (Lines 145-200):

| Property | Type | Purpose |
|----------|------|---------|
| `model` | string | Model name (e.g., "gpt-4-turbo") |
| `apiKey` | string \| undefined | API key or path-to-env-var |
| `apiKeyLocation` | string | Env var name storing the key |
| `apiBase` | string \| undefined | Custom API endpoint |
| `template` | TemplateType \| undefined | "chat", "fillInTheMiddle", "raw" |
| `promptTemplates` | Record\<string, PromptTemplate\> | Role-specific templates |
| `cacheBehavior` | CacheBehavior | Caching strategy |
| `capabilities` | ModelCapability \| undefined | Vision, reasoning, etc. |
| `roles` | ModelRole[] \| undefined | chat, edit, autocomplete |

**Error & Logging** (Lines 71-78):
```typescript
export class LLMError extends Error {
  constructor(message: string, public llm: ILLM) {
    super(message);
  }
}
```

**Type Guard** (Lines 80-86):
```typescript
export function isModelInstaller(provider: any): provider is ModelInstaller {
  return provider.installModel && provider.isInstallingModel;
}
```

**Logging Lifecycle** (Lines 340-400):
- `_logEnd()` counts prompt/completion/thinking tokens
- Logs to `DevDataSqliteDb` (telemetry)
- Logs to DataLogger (analytics)
- Updates interaction log with success/error/cancel status

---

### 3. Lemonade Provider (llms/Lemonade.ts - 12 lines)

**Purpose**: Simple wrapper extending OpenAI for OpenAI-compatible endpoints.

**Class Definition** (Lines 5-10):
```typescript
class Lemonade extends OpenAI {
  static providerName = "lemonade";
  static defaultOptions: Partial<LLMOptions> = {
    apiBase: "http://localhost:8000/api/v1/",
  };
}
```

**Behavior**:
- Inherits all OpenAI logic (chat, completions, embeddings)
- Default endpoint: `http://localhost:8000/api/v1/`
- No additional overrides needed

**Use Case**: Local Lemonade server; drop-in replacement for OpenAI.

---

### 4. Ollama Provider (llms/Ollama.ts - 833 lines)

**Purpose**: Full implementation of Ollama API adapter; supports local models with model installation, FIM, reasoning/thinking, tool calling, and complex message reordering.

**Class Definition** (Lines 152-158):
```typescript
class Ollama extends BaseLLM implements ModelInstaller {
  static providerName = "ollama";
  static defaultOptions: Partial<LLMOptions> = {
    apiBase: "http://localhost:11434/",
    model: "codellama-7b",
    maxEmbeddingBatchSize: 64,
  };

  private static modelsBeingInstalled: Set<string> = new Set();
  private static modelsBeingInstalledMutex = new Mutex();
  private fimSupported: boolean = false;
}
```

**Ollama Message Types** (Lines 19-30):
```typescript
type OllamaChatMessage = {
  role: ChatMessageRole;
  content: string;
  images?: string[] | null;              // Multi-modal support
  thinking?: string;                     // Reasoning field
  tool_calls?: {                          // Tool invocation
    function: {
      name: string;
      arguments: JSONSchema7Object;
    };
  }[];
};
```

**Ollama Request Interfaces**:

| Interface | Lines | Fields | Used For |
|-----------|-------|--------|----------|
| `OllamaModelFileParams` | 33-65 | temperature, top_p, top_k, num_predict, stop, num_ctx, mirostat, seed, etc. | Model parameter formatting |
| `OllamaBaseOptions` | 68-74 | model, options, format ("json"), stream, keep_alive | Base chat/generate params |
| `OllamaChatOptions` | 86-92 | messages[], tools[], think (reasoning flag), inherits OllamaBaseOptions | Chat endpoint specifically |
| `OllamaRawOptions` | 76-84 | prompt, suffix, images[], system, template, context, raw | Raw generation endpoint |

**Ollama Response Types** (Lines 94-141):
- `OllamaBaseResponse`: model, created_at, timing metrics (if done=true)
- `OllamaRawResponse`: response field + base
- `OllamaChatResponse`: message field + base
- `N8nChatResponse`: n8n workflow metadata (alternative response type)
- `OllamaErrorResponse`: error string

**Key Methods**:

| Method | Lines | Purpose |
|--------|-------|---------|
| `ensureModelInfo()` | 173-248 | Fetch model metadata; auto-detect context length, FIM support, stop tokens |
| `_getModel()` | 297-299 | Map Continue model name → Ollama tag (e.g., "mistral-7b" → "mistral:7b") |
| `_getModelFileParams()` | 306-322 | Convert CompletionOptions → OllamaModelFileParams |
| `_convertToOllamaMessage()` | 324-389 | Convert ChatMessage → OllamaChatMessage; handle tool calls, images, content rendering |
| `_reorderMessagesForToolCompat()` | 470-494 | Move system messages before tool blocks (Mistral/Ministral don't allow system→tool) |
| `_streamComplete()` | 417-461 | Generate text from raw prompt (streaming) |
| `_streamChat()` | 496-600+ | Send chat messages; stream response chunks with thinking support |

**Model Mapping** (Lines 252-295):
40+ Continue model names → Ollama tags:
- `mistral-7b` → `mistral:7b`
- `llama3.1-70b` → `llama3.1:70b`
- `qwen2.5-coder-7b` → `qwen2.5-coder:7b`
- `codellama-34b` → `codellama:34b`
- `deepseek-33b` → `deepseek-coder:33b`
- etc.

**FIM Detection** (Lines 237-242):
```typescript
// If model template contains ".Suffix" variable, assume FIM support
this.fimSupported = !!body?.template?.includes(".Suffix");
```

**Message Reordering for Tool Compat** (Lines 470-494):
Problem: Some Ollama models (Mistral, Ministral) reject `tool → system` message sequences.
Solution: Find system messages directly after tool results and move them before the assistant+tool block.

**Tool Calling Integration** (Lines 514-523):
```typescript
if (options.tools?.length && ollamaMessages.at(-1)?.role === "user") {
  chatOptions.tools = options.tools.map((tool) => ({
    type: "function",
    function: {
      name: tool.function.name,
      description: tool.function.description,
      parameters: tool.function.parameters,
    },
  }));
}
```
- Only include tools if last message is user role
- Convert Continue tool format → Ollama function format

**Image Support** (Lines 366-386):
- Extract images from `ChatMessage.content[]` with `imageUrl` type
- Convert data URLs → base64
- Skip invalid URLs with warning

**Thinking/Reasoning Support** (Lines 537-593):
- Parse `<think>` and `</think>` tags in n8n responses
- Emit `ThinkingChatMessage` objects during streaming
- Handle full thinking field in non-streaming responses

**Keep-Alive Configuration** (Lines 402, 510):
```typescript
keep_alive: options.keepAlive ?? 60 * 30  // 30 minutes default
```
Controls how long model stays loaded in Ollama memory.

---

## 🔴 CRITICAL: Node.js Entry Point (core-server.js)

**File**: `src/versions/v2.0.0/core-server.js` (673 lines)  
**Shebang**: `#!/usr/bin/env node`

### Architecture Overview

```
Visual Studio (C#) — WebView2
    ↓ [stdio pipes + JSON-RPC]
    ↓
core-server.js (Node.js)
    ├─ Validates npm package integrity
    ├─ Spawns `continue` binary (child process)
    ├─ Establishes line-delimited JSON relay
    ├─ Routes IDE messages via HandlerDispatcher
    └─ Handles graceful shutdown & restarts
    ↓ [stdio relay]
    ↓
Continue Process (TypeScript compiled)
    ├─ Plugin SDK server
    └─ Chat/autocomplete logic
```

### Lifecycle (Main Entry Point, Lines 627-673)

| Step | Code | Purpose |
|------|------|---------|
| 1 | `parseArgs()` (lines 595-618) | CLI flags: `--version`, `--health-check`, `--log-level`, `--log-dir` |
| 2 | `BridgeLogger(config.logsDir, config.logLevel)` (line 638) | Initialize file logger |
| 3 | `HealthCheckService(logger)` (line 639) | Health monitoring service |
| 4 | `BridgeServer(serverConfig)` (line 656) | Create relay server instance |
| 5 | **`server.start()`** (line 659) | **Begin bridging** |
| 6 | `process.exit(code)` (line 665) | Exit with status code |

### Server.start() Flow (Lines 229-272)

| Step | Method | Purpose |
|------|--------|---------|
| 1 | `_validateNpmPackages()` (line 237) | Step 12: npm integrity check (stub) |
| 2 | `mkdirSync(logsDir)` (line 242) | Create logs directory |
| 3 | `registerAllHandlersWithDispatcher(this)` (line 247) | **Step 71**: Register all 19+ message handlers |
| 4 | `createValidationHook()` (line 215-218) | **Step 73**: Init message validation hook |
| 5 | **`_spawnContinue()`** (line 261) | **CRITICAL**: Spawn Continue binary as child process |
| 6 | `_setupSignalHandlers()` (line 264) | SIGTERM/SIGINT handler |

### Message Handler Dispatcher (Lines 208-212, 355-377)

**Class**: `HandlerDispatcher` (imported from `./lib/handler-dispatcher.js`)

```javascript
this.dispatcher = new HandlerDispatcher({
  logger: this.logger,
  metrics: null,  // Step 26 injects metrics
  server: this,
});
```

**Public Methods**:
```javascript
registerHandler(messageType, handler)       // Register handler for type
dispatchMessage(message)                    // Route message to handler (async)
getDispatcherDiagnostics()                  // Return handler count/list
```

### Continue Process Spawning (Lines 395-438)

**Binary Path**: `node_modules/.bin/continue` (child process)  
**Stdio**: `['pipe', 'pipe', 'pipe']` (stdin, stdout, stderr)

```javascript
this.continueProcess = spawn(continueBin, [], {
  stdio: ['pipe', 'pipe', 'pipe'],
  cwd: __dirname,
  env: {
    ...process.env,
    NODE_ENV: 'production',
    BRIDGE_VERSION: BRIDGE_VERSION,  // '2.0.0'
  },
});
```

**Error Handling**:
- Process error → Log, increment metrics.errors, reject
- Process exit (code, signal) → Log warning
- Not shutting down? → Attempt restart with exponential backoff

**Restart Config** (Lines 84-87):
```javascript
maxRetries: 3
backoffMs: [100, 500, 2000]  // Attempt 1, 2, 3 delays
// After 3 failures → Stop respawning, report to IDE
```

### Stdio Relay Setup (Lines 446-450+)

**Flow**:
1. Read line-delimited JSON from Continue stdout
2. Parse each line as complete message
3. Relay to IDE stdout (parent process)
4. Capture stderr for error logging

```javascript
this.stdinLineReader = createInterface({
  input: this.continueProcess.stdout,
  crlfDelay: Infinity,  // Flexible line ending
});
```

### Message Protocol

**Format**: Line-delimited JSON  
**Shape** (Lines 24-29):
```javascript
{
  "messageType": "string (e.g., 'ping', 'getEditorState', 'onEditorStateChange')",
  "messageId": "string (correlation UUID)",
  "data": { /* payload varies per messageType */ }
}
```

**Protocol Version**: `1.0` (Line 90)

### Key Configuration (Lines 80-91)

| Constant | Value | Purpose |
|----------|-------|---------|
| `BRIDGE_VERSION` | `'2.0.0'` | Bridge release version |
| `CONTINUE_PACKAGE_VERSION` | `'2.0.5'` | Expected Continue npm version |
| `MESSAGE_PROTOCOL_VERSION` | `'1.0'` | Message format version |
| `RESTART_CONFIG.maxRetries` | `3` | Max crash recovery attempts |
| `RESTART_CONFIG.backoffMs` | `[100, 500, 2000]` | Exponential backoff delays (ms) |

### Metrics Tracking (Lines 199-205)

**Object**: `this.metrics`

```javascript
{
  messagesFromContinue: 0,  // Received from Continue
  messagesToContinue: 0,    // Sent to Continue
  errors: 0,                // Total errors
  restarts: 0,              // Continue process restarts
  startTime: Date.now(),    // Server start timestamp
}
```

### Step Dependencies

- **Step 12**: npm package validation (stub in `_validateNpmPackages()`)
- **Step 14**: Handler dispatcher (`HandlerDispatcher` import)
- **Step 25**: Logger facade (`BridgeLogger` class)
- **Step 71**: Handler registration (`registerAllHandlersWithDispatcher`)
- **Step 73**: Message validation (`createValidationHook`)
- **Step 26**: Telemetry (placeholder for metrics injection)

---

## 🟢 Protocol & Message Types

### Message Protocol Utilities (protocol/util.ts - 53 lines)

**Webview Message Types** (Generic):

| Type | Status | Content | Line | Purpose |
|------|--------|---------|------|---------|
| `ErrorWebviewMessage` | error | `{ status: "error", error: string, done: true }` | 3-7 | Error response from core to GUI |
| `SuccessWebviewSingleMessage<T>` | success | `{ status: "success", done: true, content: T }` | 9-13 | Single (non-streaming) response |
| `WebviewSingleMessage<T>` | union | Error \| Success | 15-17 | Single message wrapper |
| `WebviewGeneratorMessage<T, R>` | generating | `{ status: "success", done: false, content: T }` OR `{ status: "success", done: true, content: R }` or Error | 40-42 | Streaming/generator response |
| `WebviewMessage<T, R>` | generic | Single \| Generator | 51-53 | Any webview message type |

**Generator Type Utilities**:
- `GeneratorYieldType<T>` - Extract yield type from async generator
- `GeneratorReturnType<T>` - Extract return type from async generator
- `WebviewProtocolGeneratorMessage<T>` - Map protocol message type to generator response shape

---

### Message Pass-Through Types (protocol/passThrough.ts - 109 lines)

**GUI → Core Messages** (90 types, `WEBVIEW_TO_CORE_PASS_THROUGH`):

| Category | Message Types |
|----------|---|
| **System** | `ping`, `abort` |
| **History** | `history/list`, `history/delete`, `history/load`, `history/save`, `history/clear` |
| **Logging** | `devdata/log` |
| **Config** | `config/addModel`, `config/newPromptFile`, `config/newAssistantFile`, `config/ideSettingsUpdate`, `config/addLocalWorkspaceBlock`, `config/addGlobalRule`, `config/deleteRule`, `config/getSerializedProfileInfo`, `config/deleteModel`, `config/refreshProfiles`, `config/openProfile`, `config/updateSharedConfig`, `config/updateSelectedModel` |
| **MCP** | `mcp/reloadServer`, `mcp/getPrompt`, `mcp/startAuthentication`, `mcp/removeAuthentication`, `mcp/setServerEnabled` |
| **Context** | `context/getContextItems`, `context/getSymbolsForFiles`, `context/loadSubmenuItems`, `context/addDocs`, `context/removeDocs`, `context/indexDocs` |
| **Autocomplete** | `autocomplete/complete`, `autocomplete/cancel`, `autocomplete/accept` |
| **Next Edit** | `nextEdit/predict`, `nextEdit/reject`, `nextEdit/accept`, `nextEdit/startChain`, `nextEdit/deleteChain`, `nextEdit/isChainAlive`, `nextEdit/queue/*` (7 types) |
| **TTS** | `tts/kill` |
| **LLM** | `llm/complete`, `llm/streamChat`, `llm/listModels`, `llm/compileChat` |
| **Indexing** | `index/setPaused`, `index/forceReIndex`, `index/indexingProgressBarInitialized`, `indexing/reindex`, `indexing/abort`, `indexing/setPaused` |
| **Docs** | `docs/initStatuses`, `docs/getDetails`, `docs/getIndexedPages` |
| **Onboarding** | `onboarding/complete`, `addAutocompleteModel`, `didChangeSelectedProfile` |
| **Tools** | `tools/call`, `tools/evaluatePolicy`, `tools/preprocessArgs` |
| **Other** | `streamDiffLines`, `chatDescriber/describe`, `conversation/compact`, `stats/getTokensPerDay`, `stats/getTokensPerModel`, `isItemTooBig`, `process/*` (3 types), `models/fetch` |

**Core → GUI Messages** (9 types, `CORE_TO_WEBVIEW_PASS_THROUGH`):

| Message | Purpose |
|---------|---------|
| `configUpdate` | Config changed on core, push to GUI |
| `indexProgress` | Codebase indexing progress (legacy) |
| `indexing/statusUpdate` | Docs indexing progress (new) |
| `addContextItem` | Add context item to chat |
| `refreshSubmenuItems` | Context provider submenu changed |
| `isContinueInputFocused` | Focus state sync |
| `setTTSActive` | TTS state update |
| `getWebviewHistoryLength` | Sync history length |
| `getCurrentSessionId` | Session ID sync |
| `sessionUpdate` | Session state changed |
| `didCloseFiles` | Files were closed in IDE |
| `toolCallPartialOutput` | Streaming tool output |

---

## 🟡 LLM Configuration Constants

### LLM Defaults (llm/constants.ts - 37 lines)

**Token & Context Defaults**:

| Constant | Value | Purpose |
|----------|-------|---------|
| `DEFAULT_MAX_TOKENS` | 4096 | Default max completion tokens |
| `DEFAULT_CONTEXT_LENGTH` | 32,768 | Default context window |
| `DEFAULT_TEMPERATURE` | 0.5 | Default sampling temperature |
| `DEFAULT_PRUNING_LENGTH` | 128,000 | Max tokens before context pruning |
| `DEFAULT_REASONING_TOKENS` | 2,048 | Max reasoning/thinking tokens (for o1, etc.) |

**Embedding Defaults**:

| Constant | Value | Purpose |
|----------|-------|---------|
| `DEFAULT_MAX_CHUNK_SIZE` | 500 | Max tokens per embedding (with 12-token safety buffer) |
| `DEFAULT_MAX_BATCH_SIZE` | 64 | Max embeddings per batch request |
| `PROXY_URL` | http://localhost:65433 | Local proxy endpoint |

**LLM Arguments**:

```javascript
DEFAULT_ARGS = {
  maxTokens: 4096,
  temperature: 0.5,
}
```

**LLM Configuration Status Enum** (`LLMConfigurationStatuses`):

| Status | Value | Meaning |
|--------|-------|---------|
| `VALID` | "valid" | LLM is properly configured |
| `MISSING_API_KEY` | "missing-api-key" | API key not set |
| `MISSING_ENV_SECRET` | "missing-env-secret" | Environment secret not found |

**Next Edit Models Enum** (`NEXT_EDIT_MODELS`):

| Model | Value | Purpose |
|-------|-------|---------|
| `MERCURY_CODER` | "mercury-coder" | NextEdit model variant |
| `INSTINCT` | "instinct" | NextEdit model variant |

---

## 🟡 Chat Message Utilities

### Message Validation Functions (llm/messages.ts - 73 lines)

| Function | Input | Output | Purpose |
|----------|-------|--------|---------|
| `messageHasToolCalls(msg)` | ChatMessage | boolean | True if role='assistant' AND toolCalls array present |
| `messageIsEmpty(msg)` | ChatMessage | boolean | True if content is empty string or all-empty array |
| `addSpaceToAnyEmptyMessages(msgs)` | ChatMessage[] | ChatMessage[] | Convert empty content to " " (some providers don't accept empty) |
| `isUserOrToolMsg(msg)` | ChatMessage \| undefined | boolean | True if role='user' OR role='tool' |
| `isToolMessageForId(msg, toolCallId)` | ChatMessage \| undefined, string | boolean | True if role='tool' AND toolCallId matches |
| `messageHasToolCallId(msg, toolCallId)` | ChatMessage \| undefined, string | boolean | True if role='assistant' AND toolCalls contains matching ID |
| `chatMessageIsEmpty(msg)` | ChatMessage | boolean | Check if message is "empty" per role (handles all roles: system, user, assistant, thinking, tool) |

**Edge Cases Handled**:
- Empty strings vs. whitespace-only strings
- Array content with multiple empty text items
- Content arrays with mixed empty/non-empty items
- Role-specific emptiness (thinking/tool messages never empty)
- Assistant messages with toolCalls but no content (not empty if toolCalls present)

---

## 🟠 Tool Execution Messages & Errors

### Tool Call Output Constants (tools/constants.ts - 4 lines)

| Constant | Value | Purpose |
|----------|-------|---------|
| `NO_TOOL_CALL_OUTPUT_MESSAGE` | "No tool output" | When tool executed but returned nothing |
| `CANCELLED_TOOL_CALL_MESSAGE` | "The user cancelled this tool call." | User abort/cancel |
| `ERRORED_TOOL_CALL_OUTPUT_MESSAGE` | "There was an error calling the tool." | Tool execution error fallback |

---

## 🔴 Error Handling & Classification

### Error Utilities (util/errors.ts - 71 lines)

**Root Cause Extraction**:
```typescript
getRootCause(err: any): any  // Traverse err.cause chain to find root
```

**ContinueError Class**:
```typescript
class ContinueError extends Error {
  reason: ContinueErrorReason  // Enum-based error classification
  constructor(reason, message?)
}
```

**Error Classification Enum** (`ContinueErrorReason`):

| Category | Error Codes |
|----------|------------|
| **Find & Replace** (8 errors) | FindAndReplaceIdenticalOldAndNewStrings, FindAndReplaceMissingOldString, FindAndReplaceNonFirstEmptyOldString, FindAndReplaceMissingNewString, FindAndReplaceInvalidReplaceAll, FindAndReplaceOldStringNotFound, FindAndReplaceMultipleOccurrences, FindAndReplaceMissingFilepath |
| **Multi-Edit** (4 errors) | MultiEditEditsArrayRequired, MultiEditEditsArrayEmpty, MultiEditSubsequentEditsOnCreation, MultiEditEmptyOldStringNotFirst |
| **General Edit** (1 error) | EditToolFileNotRead |
| **File Operations** (7 errors) | FileAlreadyExists, FileNotFound, FileWriteError, FileIsSecurityConcern, ParentDirectoryNotFound, FileTooLarge, PathResolutionFailed |
| **Line Operations** (2 errors) | InvalidLineNumber, DirectoryNotFound |
| **Execution** (2 errors) | CommandExecutionFailed, CommandNotAvailableInRemote |
| **Search** (1 error) | SearchExecutionFailed |
| **Config** (2 errors) | RuleNotFound, SkillNotFound |
| **Catch-all** (2 errors) | Unspecified (known error, no code), Unknown (unexpected error) |

**Usage Pattern**:
```typescript
throw new ContinueError(ContinueErrorReason.FileNotFound, "File at /path/to/file not found")
```

## Tool Flow: From Core Config to Browser GUI

**Complete end-to-end flow**:

```
1. ProfileLifecycleManager.getSerializedConfig() [lines 119-141]
   ↓
2. await finalToBrowserConfig(result.config, this.ide) [line 132-135]
   ↓
3. load.ts: finalToBrowserConfig(final: ContinueConfig) [lines 625-661]
   ├─ Input: ContinueConfig.tools = Tool[] (WITH preprocessArgs, evaluateToolCallPolicy)
   ├─ Line 644: tools: final.tools.map(serializeTool)
   ├─ serializeTool() removes runtime functions [tools/index.ts:53-56]
   └─ Output: BrowserSerializedContinueConfig.tools = Omit<Tool>[] (stateless)
   ↓
4. configSlice.setConfigResult() [redux/slices/configSlice.ts line 51]
   ├─ Receives: ConfigResult<BrowserSerializedContinueConfig>
   ├─ Line 66: state.config = config  (stores serialized tools)
   └─ GUI has access via: selectUIConfig or direct state.config.tools
   ↓
5. Tools rendered in GUI with displayTitle, group, icon, readonly status
```

---

## Tool Count Zero Root Cause Analysis

**Question**: Why does GUI show `tools: []` in some cases?

**Answer**: The chain must NOT break at any point:

| Step | File | Potential Failure Point |
|------|------|------------------------|
| 1 | `reference/continue-src/core/config/load.ts:476` | `getBaseToolDefinitions()` returns empty or falsy |
| 2 | `reference/continue-src/core/config/load.ts:644` | `final.tools` is empty or `serializeTool` strips tools incorrectly |
| 3 | `reference/continue-src/core/config/ProfileLifecycleManager.ts:132-135` | `finalToBrowserConfig()` not called or result discarded |
| 4 | `reference/continue-src/gui/src/redux/slices/configSlice.ts:66` | `setConfigResult()` not dispatched or payload is null |
| 5 | **EMPTY_CONFIG fallback** (configSlice line 15) | If step 4 fails, GUI defaults to `tools: []` |

**Most likely causes**:
- ✗ `intermediateToFinalConfig()` NOT being called (step 5 returns early)
- ✗ `finalToBrowserConfig()` result payload is `{ config: null, errors: [...], ...}`
- ✗ Redux `setConfigResult()` dispatch never fires or receives null config

## Scanned Files Reference

| File | Range | Status | Purpose |
|------|-------|--------|---------|
| **`src/versions/v2.0.0/core-server.js`** | **1-673 (complete)** | **✓ CRITICAL** | **Node.js entry point; spawns Continue; relays stdio** |
| `reference/continue-src/core/config/load.ts` | 1-904 (complete) | ✓ | Configuration pipeline (JSON → runtime → browser) |
| `reference/continue-src/core/config/ProfileLifecycleManager.ts` | 1-50, 80-142 | ✓ | Redux dispatch of serialized config |
| `reference/continue-src/core/core.ts` | 1-50 | ✓ partial | Core exports, tool invocation import |
| `reference/continue-src/core/tools/index.ts` | 1-56 (complete) | ✓ | Tool definitions factory, serializeTool function |
| `reference/continue-src/gui/index.html` | 1-16 | ✓ | WebView2 entry (mounts React root) |
| `reference/continue-src/gui/src/main.tsx` | 1-24 | ✓ | React root + Redux provider+persist |
| `reference/continue-src/gui/src/redux/store.ts` | 1-145 | ✓ | Redux store (IdeMessenger middleware) |
| `reference/continue-src/gui/src/redux/slices/configSlice.ts` | 1-109 | ✓ | GUI config state (EMPTY_CONFIG fallback) |
| `reference/continue-src/core/index.d.ts` | 1-2022 (complete) | ✓ | Complete type system (Tool, IDE, Config) |

---

## Load Flow Implementation (load.ts - Lines 1-904)

### Configuration Load Pipeline
**Execution order**: `loadContinueConfigFromJson()` (lines 791-898)

| Step | Function | Input | Output | Purpose |
|------|----------|-------|--------|---------|
| 1 | `loadSerializedConfig()` (lines 120-178) | config.json + workspace configs | SerializedContinueConfig | Parse JSON, merge workspace overrides, validate |
| 2 | `serializedToIntermediateConfig()` (lines 180-212) | SerializedContinueConfig | Config | Convert JSON to Config object (legacy slash commands) |
| 3 | `modifyAnyConfigWithSharedConfig()` | Config | Config | Apply organization/user shared settings |
| 4 | `buildConfigTsandReadConfigJs()` (lines 764-789) | config.ts file | Config | Optionally call user's `modifyConfig()` hook |
| 5 | `intermediateToFinalConfig()` (lines 884-892) | Config | **ContinueConfig** | **Build models, tools, context providers** |
| 6 | Return `ConfigResult<ContinueConfig>` | - | ContinueConfig + errors | Final runtime config ready for browser serialization |

**Line 901-903**: Exports `finalToBrowserConfig` and `loadContinueConfigFromJson`

---

### ContinueConfig Construction (intermediateToFinalConfig)
**Location**: Lines 473-500 (within `intermediateToFinalConfig`)

**Tools Assignment** (Line 476):
```javascript
tools: getBaseToolDefinitions(),  // <-- Base tools ONLY, not config-dependent
```

**Model Role Assignment** (Lines 479-488):
```javascript
modelsByRole: {
  chat: models,                   // Chat models
  edit: models,                   // Edit models (same as chat unless overridden)
  apply: models,                  // Code apply (same as chat)
  summarize: models,              // Summarization (same as chat)
  autocomplete: [...tabAutocompleteModels],  // Tab completion (separate config)
  embed: newEmbedder ? [newEmbedder] : [],   // Embedding model (optional)
  rerank: newReranker ? [newReranker] : [],  // Reranker model (optional)
  subagent: [],                   // Sub-agent models (empty)
}
```

**MCP & Rules** (Lines 477, 499):
```javascript
mcpServerStatuses: [],     // Populated later by MCPManagerSingleton
rules: [],                 // Populated from systemMessage and YAML
```

**Context Providers** (Lines 376-394):
- Load from config.contextProviders via `loadConfigContextProviders()`
- Wrap custom providers in `CustomContextProviderClass`
- All providers are `IContextProvider` instances (with `.description` property)

---

### Browser Serialization Function (finalToBrowserConfig)
**Location**: Lines 625-661

**Input**: `ContinueConfig` (runtime, with functions)
**Output**: `BrowserSerializedContinueConfig` (serializable, functions removed)

| Runtime Property | Browser Equivalent | Transformation |
|---|---|---|
| `tools: Tool[]` | `tools: Omit<Tool, "preprocessArgs", "evaluateToolCallPolicy">[]` | **Line 644**: `tools: final.tools.map(serializeTool)` |
| `slashCommands: SlashCommandWithSource[]` | `slashCommands: SlashCommandDescWithSource[]` | **Line 632-634**: Strip `.run` function, add `isLegacy` flag |
| `contextProviders: IContextProvider[]` | `contextProviders: ContextProviderDescription[]` | **Line 636**: Extract `.description` only |
| `modelsByRole: ILLM[][]` | `modelsByRole: ModelDescription[][]` | **Line 647-650**: Map via `llmToSerializedModelDescription()` |
| `selectedModelByRole: ILLM \| null` | `selectedModelByRole: ModelDescription \| null` | **Line 653-657**: Same mapping function |
| Other props (ui, experimental, rules, docs, mcpServerStatuses) | Same | Passed through as-is |

**Line 659**: Comment: "data not included here because client doesn't need"

---

### serializeTool Function (tools/index.ts Lines 53-56)
**Location**: `reference/continue-src/core/tools/index.ts`

```typescript
export function serializeTool(tool: Tool) {
  const { preprocessArgs, evaluateToolCallPolicy, ...rest } = tool;
  return rest;
}
```

**What it does**:
- Takes full Tool with runtime functions
- Destructures to **remove** `preprocessArgs` and `evaluateToolCallPolicy`
- Returns `Omit<Tool, "preprocessArgs", "evaluateToolCallPolicy">` (stateless tool)

**Why**:
- These are function references that cannot be serialized to JSON
- GUI doesn't need these because tool execution happens on core side only
- GUI only needs metadata: name, displayTitle, group, parameters, icons, etc.

---

### getBaseToolDefinitions Function (tools/index.ts Lines 6-16)
**Location**: `reference/continue-src/core/tools/index.ts`

**Exported as a factory function** (not array, to prevent duplicates on reload):
```typescript
export const getBaseToolDefinitions = () => [
  toolDefinitions.readFileTool,
  toolDefinitions.createNewFileTool,
  toolDefinitions.runTerminalCommandTool,
  toolDefinitions.globSearchTool,
  toolDefinitions.viewDiffTool,
  toolDefinitions.readCurrentlyOpenFileTool,
  toolDefinitions.lsTool,
  toolDefinitions.createRuleBlock,
  toolDefinitions.fetchUrlContentTool,
];
```

**Comment** (Line 5): "I'm writing these as functions because we've messed up 3 TIMES by pushing to const, causing duplicate tool definitions on subsequent config loads."

**Base tools** (9 minimum):
1. **readFileTool** - `read_file(path)`
2. **createNewFileTool** - `create_new_file(path, contents)`
3. **runTerminalCommandTool** - `run_terminal_command(command, cwd)`
4. **globSearchTool** - `glob_search(pattern)`
5. **viewDiffTool** - `view_diff()`
6. **readCurrentlyOpenFileTool** - `read_currently_open_file()`
7. **lsTool** - `ls(dir)` / `list_directory()`
8. **createRuleBlock** - `create_rule(rule_text)` / system prompt rule
9. **fetchUrlContentTool** - `fetch_url_content(url)`

**Additional conditional tools** (via `getConfigDependentToolDefinitions()`):
- `requestRuleTool` - Dynamic rule loading
- `readSkillTool` - Reference file loading
- `searchWebTool` - Web search (always added)
- `viewRepoMapTool` - Experimental
- `viewSubdirectoryTool` - Experimental
- `codebaseTool` - Experimental
- `readFileRangeTool` - Experimental
- `multiEditTool` or `editFileTool` - Conditional on model
- `singleFindAndReplaceTool` - Non-agent fallback
- `grepSearchTool` - Only on local (not remote)

---

### MCP Server Integration (Lines 520-545)
**MCP Tools Addition Flow**:

1. Parse `config.experimental?.modelContextProtocolServers` (array)
2. Map each server to `InternalMcpOptions` with:
   - Auto-generated `id` (continue-mcp-server-1, etc.)
   - Default `name` field
   - Merged `requestOptions` (HTTP config)
   - Transport options spread (stdio, websocket, sse, etc.)

3. Load additional MCP configs from JSON via `loadJsonMcpConfigs()` (lines 537-541)
4. Call `mcpManager.setConnections(mcpOptions, false)` → triggers MCP tool discovery
5. MCP tools are added to `continueConfig.mcpServerStatuses[].tools[]` later

**Note** (Line 520): "Config is reloaded again once connected!" → MCP tools fetched on next load cycle

---

### Config Merging Strategy (Lines 98-100)
**Key merge behavior** for models, contextProviders, slashCommands:

```javascript
const configMergeKeys = {
  models: (a, b) => a.title === b.title,  // Merge by title
  contextProviders: (a, b) => {
    if (a.name !== "http" || b.name !== "http") {
      return a.name === b.name;            // Merge by name (non-HTTP)
    }
    return a.name === b.name && a.params?.url === b.params?.url;  // For HTTP, also check URL
  },
  slashCommands: (a, b) => a.name === b.name,  // Merge by name
  customCommands: (a, b) => a.name === b.name, // Merge by name
};
```

---

### Export Summary (Lines 900-904)
```typescript
export {
  finalToBrowserConfig,                    // For ProfileLifecycleManager
  loadContinueConfigFromJson,              // Main entry point
  type BrowserSerializedContinueConfig,    // Type export
};
```

---

## Core Type System (index.d.ts)

### Tool Interface (Lines 1132-1168)
**Runtime Function Tool Definition**

| Property | Type | Purpose | Notes |
|----------|------|---------|-------|
| `type` | `"function"` | Discriminator | Always "function" |
| `function.name` | string | Tool identifier | Must match function invocation |
| `function.description` | string \| undefined | LLM prompt help | Optional parameter documentation |
| `function.parameters` | Record<string, any> \| undefined | JSON Schema | Function argument schema |
| `function.strict` | boolean \| null \| undefined | Schema validation | Enforce strict schema compliance |
| `displayTitle` | string | **User-facing label** | Shown in GUI, core responsibility |
| `wouldLikeTo` | string \| undefined | Agent narrative | "Augment user-facing descriptions" |
| `isCurrently` | string \| undefined | Agent narrative | Contextual capability flag |
| `hasAlready` | string \| undefined | Agent narrative | Precondition narrative |
| `readonly` | boolean | **Execute permission** | false=callable, true=view-only |
| `isInstant` | boolean \| undefined | Latency hint | true=sub-10ms (UI-safe) |
| `uri` | string \| undefined | Tool source | Package/project link |
| `faviconUrl` | string \| undefined | Visual branding | Icon for UI display |
| `group` | string | **Tool category** | "Files", "Search", "IDE", "Git", MCP server name |
| `originalFunctionName` | string \| undefined | Munged name tracking | If function.name was transformed |
| `systemMessageDescription.prefix` | string | Prompt injection | What to tell LLM about tool |
| `systemMessageDescription.exampleArgs` | Array<[string, string \| number]> | Few-shot learning | LLM prompt examples |
| `defaultToolPolicy` | ToolPolicy | **Security enforcement** | Authorization rules (from @continuedev/terminal-security) |
| `toolCallIcon` | string \| undefined | Visual indicator | Icon string for tool calls |
| `preprocessArgs` | Function \| undefined | **RUNTIME ONLY** | `(args, {ide}) => Promise<processed>` |
| `evaluateToolCallPolicy` | Function \| undefined | **RUNTIME ONLY** | `(basePolicy, parsedArgs, processedArgs) => ToolPolicy` |
| `mcpMeta` | McpToolMeta \| undefined | MCP server metadata | `{ ui?: { resourceUri?: string } }` |

**SERIALIZATION NOTE** (Line 1857):
- `BrowserSerializedContinueConfig.tools` uses `Omit<Tool, "preprocessArgs", "evaluateToolCallPolicy">[]`
- Runtime functions stripped before GUI delivery (stateless tools only)

---

### ContinueConfig Interface (Lines 1820-1841)
**Runtime Configuration (Core State)**

| Property | Type | Purpose |
|----------|------|---------|
| `tools` | Tool[] | **Full Tool array WITH runtime functions** |
| `mcpServerStatuses` | MCPServerStatus[] | MCP server connection state + tool roster |
| `rules` | RuleWithSource[] | Tool execution rules + auth policies |
| `modelsByRole` | Record<ModelRole, ILLM[]> | Models mapped to roles (chat/edit/embed) |
| `selectedModelByRole` | Record<ModelRole, ILLM \| null> | Active model per role |
| `slashCommands` | SlashCommandWithSource[] | Commands with source metadata |
| `contextProviders` | IContextProvider[] | Runtime context fetch objects |
| `ui` | ContinueUIConfig \| undefined | UI behavior settings |
| `experimental` | ExperimentalConfig \| undefined | Feature flags + MCP options |
| `analytics` | AnalyticsConfig \| undefined | Telemetry config |
| `docs` | SiteIndexingConfig[] \| undefined | Documentation site crawlers |
| `completionOptions` | BaseCompletionOptions \| undefined | Default LLM sampling params |
| `requestOptions` | RequestOptions \| undefined | HTTP/proxy/TLS settings |

---

### BrowserSerializedContinueConfig Interface (Lines 1843-1863)
**GUI Runtime State (Browser/Serializable)**

| Property | Type | Purpose | Difference from ContinueConfig |
|----------|------|---------|------------------------------|
| `tools` | Omit<Tool, "preprocessArgs", "evaluateToolCallPolicy">[] | **GUI tools (no runtime functions)** | ❌ Runtime functions stripped |
| `slashCommands` | SlashCommandDescWithSource[] | Commands metadata | ✓ Description-only (no `run` function) |
| `contextProviders` | ContextProviderDescription[] | Provider metadata | ✓ Description-only (no `getContextItems`) |
| `modelsByRole` | Record<ModelRole, ModelDescription[]> | Models (serializable) | ✓ Use ModelDescription instead of ILLM |
| `selectedModelByRole` | Record<ModelRole, ModelDescription \| null> | Active model | ✓ Use ModelDescription instead of ILLM |
| `mcpServerStatuses` | MCPServerStatus[] | MCP metadata | ✓ Same as runtime |
| `rules` | RuleWithSource[] | Rules metadata | ✓ Same as runtime |
| `ui`, `experimental`, `analytics`, `docs` | same | same | ✓ Same as runtime |

**KEY DIFFERENCE**: Line 1857 `tools` definition shows explicit stripping of `preprocessArgs` and `evaluateToolCallPolicy`

---

### IDE Interface (Lines 831-936)
**IDE Abstraction Layer**

**File Operations**
| Method | Returns | Purpose |
|--------|---------|---------|
| `readFile(fileUri)` | Promise<string> | Full file contents |
| `readRangeInFile(fileUri, range)` | Promise<string> | Partial file read |
| `writeFile(path, contents)` | Promise<void> | File write (create/overwrite) |
| `removeFile(path)` | Promise<void> | File delete |
| `fileExists(fileUri)` | Promise<boolean> | File presence check |
| `saveFile(fileUri)` | Promise<void> | Persist editor buffer |
| `getOpenFiles()` | Promise<string[]> | Open editor paths |
| `getCurrentFile()` | Promise<{path, contents, isUntitled}> | Active editor state |
| `openFile(path)` | Promise<void> | Switch editor focus |
| `showLines(fileUri, startLine, endLine)` | Promise<void> | Reveal range to user |
| `listDir(dir)` | Promise<[string, FileType][]> | Directory listing |
| `getFileStats(files[])` | Promise<FileStatsMap> | File metadata |

**Context Retrieval**
| Method | Returns | Purpose |
|--------|---------|---------|
| `getClipboardContent()` | Promise<{text, copiedAt}> | Clipboard access |
| `getTerminalContents()` | Promise<string> | Terminal output buffer |
| `getSearchResults(query, maxResults?)` | Promise<string> | Full-text search |
| `getFileResults(pattern, maxResults?)` | Promise<string[]> | Glob file search |
| `getPinnedFiles()` | Promise<string[]> | User-pinned files |
| `getDiff(includeUnstaged)` | Promise<string[]> | Git diff output |

**Git & Repository**
| Method | Returns | Purpose |
|--------|---------|---------|
| `getBranch(dir)` | Promise<string> | Current branch name |
| `getRepoName(dir)` | Promise<string \| undefined> | Repository name |
| `getGitRootPath(dir)` | Promise<string \| undefined> | Git root directory |
| `getTags(artifactId)` | Promise<IndexTag[]> | Git tags (with branch/dir) |

**LSP Integration**
| Method | Returns | Purpose |
|--------|---------|---------|
| `gotoDefinition(location)` | Promise<RangeInFile[]> | Symbol definition locations |
| `gotoTypeDefinition(location)` | Promise<RangeInFile[]> | Type definition (VSCode only) |
| `getReferences(location)` | Promise<RangeInFile[]> | All symbol usages |
| `getDocumentSymbols(textDocumentId)` | Promise<DocumentSymbol[]> | File outline |
| `getSignatureHelp(location)` | Promise<SignatureHelp \| null> | Function signature (VSCode only) |
| `getProblems(fileUri?)` | Promise<Problem[]> | Diagnostics/errors |

**IDE Metadata & Configuration**
| Method | Returns | Purpose |
|--------|---------|---------|
| `getIdeInfo()` | Promise<IdeInfo> | IDE type/version/platform (vscode \| jetbrains) |
| `getIdeSettings()` | Promise<IdeSettings> | Remote config, sync period, test environment |
| `getWorkspaceDirs()` | Promise<string[]> | Workspace root directories |
| `getUniqueId()` | Promise<string> | Machine/user identifier |
| `isTelemetryEnabled()` | Promise<boolean> | Analytics opt-in status |
| `isWorkspaceRemote()` | Promise<boolean> | SSH/container detection |

**Debugging & Execution**
| Method | Returns | Purpose |
|--------|---------|---------|
| `getDebugLocals(threadIndex)` | Promise<string> | Debugger variable state |
| `getTopLevelCallStackSources(threadIndex, stackDepth)` | Promise<string[]> | Debugger call stack |
| `getAvailableThreads()` | Promise<Thread[]> | Debugger thread list |
| `subprocess(command, cwd?)` | Promise<[string, string]> | Shell execution (stdout, stderr tuple) |
| `runCommand(command, options?)` | Promise<void> | Terminal + UI integration |

**UI/UX & External**
| Method | Returns | Purpose |
|--------|---------|---------|
| `showVirtualFile(title, contents)` | Promise<void> | Create read-only file tab |
| `showToast(type, message, ...)` | Promise<any> | User notifications |
| `openUrl(url)` | Promise<void> | External link handler |
| `getExternalUri?(uri)` | Promise<string> | VSCode webview URI mapping |

**Events & Callbacks**
| Method | Returns | Purpose |
|--------|---------|---------|
| `onDidChangeActiveTextEditor(callback)` | void | Subscribe to editor changes |

**Secrets Storage**
| Method | Returns | Purpose |
|--------|---------|---------|
| `readSecrets(keys[])` | Promise<Record<string, string>> | Retrieve stored secrets |
| `writeSecrets(secrets{})` | Promise<void> | Persist secrets encrypted |

---

### ModelDescription Interface (Lines 1226-1250)
**Serializable Model Metadata (for GUI)**

| Property | Type | Purpose |
|----------|------|---------|
| `title` | string | Display name in UI |
| `provider` | string | Provider class name |
| `underlyingProviderName` | string | Actual API provider (openai, anthropic, etc.) |
| `model` | string | Model identifier (gpt-4, claude-3, etc.) |
| `contextLength` | number \| undefined | Token window size |
| `template` | TemplateType \| undefined | Prompt template (llama2, chatml, none, etc.) |
| `completionOptions` | BaseCompletionOptions \| undefined | Sampling parameters |
| `capabilities` | ModelCapability \| undefined | Feature flags (uploadImage, tools, nextEdit) |
| `roles` | ModelRole[] \| undefined | Supported roles (chat, edit, embed, etc.) |
| `apiKey`, `apiBase`, `deployment`, etc. | various | Provider-specific config (stripped in browser) |

---

### ToolExtras Context (Lines 1111-1123)
**Parameters Passed During Tool Execution**

| Property | Type | Purpose |
|----------|------|---------|
| `ide` | IDE | IDE abstraction (file, git, LSP access) |
| `llm` | ILLM | LLM for sub-queries |
| `fetch` | FetchFunction | HTTP client with auth/proxy |
| `tool` | Tool | Self-reference to invoked tool |
| `toolCallId` | string \| undefined | Unique invocation ID |
| `onPartialOutput` | Function \| undefined | Streaming callback for context updates |
| `config` | ContinueConfig | Full runtime config (access to other tools, models, etc.) |
| `codeBaseIndexer` | CodebaseIndexer \| undefined | Search/indexing API |

---

### ContinueSDK Context (Lines 940-953)
**Parameters Available to Slash Commands & Invokable Rules**

| Property | Type | Purpose |
|----------|------|---------|
| `ide` | IDE | Full IDE access (file ops, git, diagnostics) |
| `llm` | ILLM | Primary LLM for command |
| `config` | ContinueConfig | Runtime config (tools, models, settings) |
| `history` | ChatMessage[] | Conversation history |
| `input` | string | User input/query string |
| `contextItems` | ContextItemWithId[] | User-selected context |
| `selectedCode` | RangeInFile[] | Highlighted code ranges |
| `params` | Record<string, any> \| undefined | Command parameters |
| `fetch` | FetchFunction | HTTP client |
| `abortController` | AbortController | Cancellation token |
| `addContextItem` | Function | Runtime context injection |

---

### Context Provider System (Lines 188-231)
**Environment for Context Resolution**

**ContextProviderDescription** (Metadata, sent to GUI)
| Property | Type | Purpose |
|----------|------|---------|
| `title` | ContextProviderName | Registered provider name |
| `displayTitle` | string | User-facing label |
| `description` | string | Help text |
| `type` | ContextProviderType | "normal" \| "query" \| "submenu" |
| `dependsOnIndexing` | ContextIndexingType[] \| undefined | Index prerequisites |

**ContextProviderExtras** (Runtime, passed to provider)
| Property | Type | Purpose |
|----------|------|---------|
| `config` | ContinueConfig | Access to all config |
| `llm` | ILLM | Query capability |
| `ide` | IDE | File/git access |
| `selectedCode` | RangeInFile[] | User selection |
| `fullInput` | string | Full query string |
| `fetch` | FetchFunction | HTTP access |
| `isInAgentMode` | boolean | Agent-specific behavior flag |
| `embeddingsProvider` | ILLM \| null | Embedding model |
| `reranker` | ILLM \| null | Reranker model |

---

### ModelRole Type (Semantic Model Categories)
**Where LLM is selected by role** (Lines 1838-1839, 1861-1862)

Typical roles:
- `"chat"` - Conversation primary model
- `"edit"` | `"apply"` - Code generation/editing
- `"embed"` - Dense embeddings (e.g., all-MiniLM-L6-v2)
- `"rerank"` - Semantic search reranking
- `"summarize"` - Long-form summarization
- `"autocomplete"` | `"tab"` - In-editor completions
- `"subagent"` - Sub-task delegation

---

### Serialization Pipeline (Runtime → Browser)

**Assembly** (Line 1835 in ContinueConfig)
```
ContinueConfig.tools: Tool[] (WITH preprocessArgs, evaluateToolCallPolicy)
```

**Serialization** (Line 1857 in BrowserSerializedContinueConfig)
```
BrowserSerializedContinueConfig.tools: Omit<Tool, "preprocessArgs", "evaluateToolCallPolicy">[]
(WITHOUT runtime functions - stateless only)
```

**GUI Reception** (configSlice.ts Line 66)
```
state.config = config  // Stores BrowserSerializedContinueConfig
```

**This EXACT type arrives in GUI:** `Omit<Tool, "preprocessArgs", "evaluateToolCallPolicy">[]`

## GUI Bootstrap Chain

**HTML Entry:** `reference/continue-src/gui/index.html` (line 12)
- Loads: `<script type="module" src="/src/main.tsx"></script>`
- Mounts to: `<div id="root"></div>` (line 11)

**React Root:** `reference/continue-src/gui/src/main.tsx` (lines 1-24)
| Line | Token | Purpose |
|------|-------|---------|
| 1-7 | imports | React, ReactDOM, redux Provider, PersistGate, App, store, persistor |
| 9 | `(async () => {` | IIFE entry point |
| 11 | `document.getElementById("root")` | Get DOM mount point |
| 14 | `ReactDOM.createRoot(container)` | Initialize React root |
| 17-21 | `<Provider><PersistGate><App />` | Provider stack (Redux + persistence) |
| 5 | `import App from "./App"` | Next file in chain

## Redux State Tree (GUI Client State)

**Root Reducers** (lines 26-34):
| Slice | Purpose |
|-------|---------|
| `config` | Runtime config state (NOT persisted - empty filter line 52) |
| `ui` | UI state incl. **toolSettings**, **toolGroupSettings**, ruleSettings, reasoningSettings |
| `session` | Active session ID, mode, title |
| `editModeState` | Edit mode toggle, code buffer |
| `tabs` | Tab collection (chat windows) |
| `profiles` | Available profiles + selected profile ID |
| `indexing` | Indexing progress state |

**Tool State Location** (lines 47-56):
- **Persisted**: `ui.toolSettings`, `ui.toolGroupSettings`, `ui.ruleSettings` (stored in localStorage)
- **Not persisted**: `config` slice (line 52: empty filter)
- **Bridge**: `IdeMessenger` injected as thunk extra (line 17, 105)

**Middleware Chain** (lines 116-123):
- Default Redux middleware
- `serializableCheck: false` (allows IdeMessenger object)
- Thunk with `ideMessenger` as extra argument

**Next in chain** (line 20):
- `./slices/configSlice` - where runtime config is stored
- `./slices/uiSlice` - where tool UI settings live (HIGHEST PRIORITY)

## Config Redux Slice (GUI Runtime State)

**State Shape** (lines 6-10):
| Field | Type | Purpose |
|-------|------|---------|
| `config` | `BrowserSerializedContinueConfig` | **HOLDS SERIALIZED TOOLS** from core (line 8) |
| `loading` | boolean | Config fetch in progress |
| `configError` | `ConfigValidationError[]` | Errors from config loading |

**EMPTY_CONFIG Template** (lines 12-44):
- **Line 15**: `tools: []` — **DEFINES ZERO TOOLS DEFAULT**
- modelsByRole: chat/apply/edit/summarize/autocomplete/rerank/embed/subagent all empty
- selectedModelByRole: all null

**Actions** (lines 101-102):
- `setConfigResult(payload: ConfigResult<BrowserSerializedContinueConfig>)` — Stores config from core
- `updateConfig(config: BrowserSerializedContinueConfig)` — Updates local config
- `setConfigLoading(loading: boolean)` — Loading state toggle

**Action Handlers** (lines 49-74):
- **Line 51-58**: `setConfigResult` — receives ConfigResult from core (lines 132-135 in ProfileLifecycleManager), updates `state.config` with serialized config INCLUDING tools
- **Line 62**: If no config: fallback to `EMPTY_CONFIG` (which has `tools: []`)
- **Line 65-70**: If config exists: `state.config = config` (preserves tools from core)

**Selectors** (lines 76-89):
- `selectSelectedChatModel` - gets chat model from config
- `selectUIConfig` - gets UI settings
- **Note**: No selector for tools (direct state access needed)

**Root Issue Connection**:
- Line 15 `tools: []` in EMPTY_CONFIG is the fallback when core fails
- Line 66 `state.config = config` is where core's serialized tools should appear
- If core sends `tools: []`, this slice receives and stores it as-is

---

## 🎨 GUI UTILITIES & CLIENT TOOLS (10 files, 405 lines)

**Overview**: GUI helper utilities spanning CSS composition, localStorage abstraction, navigation routing, platform detection, and client-side tool implementations for edit/search/replace operations.

### 1. CSS & Component Utilities

**cn.ts (20 lines)**
- Combines `clsx` (conditional class composition) with `tailwind-merge` (intelligent Tailwind conflict resolution)
- **Export**: `cn(...inputs: ClassValue[])`
- **Usage**: GUI components that need conditional Tailwind classes without duplicates or conflicts
- **Example**: `cn('px-2 py-1', { 'px-4': isActive })` → merges and removes conflicting padding classes

**isContinueTeamMember.ts (7 lines)**
- Simple email validator for Continue team membership
- **Export**: `isContinueTeamMember(email?: string): boolean`
- **Logic**: Returns true only if email includes `@continue.dev`
- **Usage**: Feature gating, admin UI, internal tools visibility

### 2. Storage & Persistence Layer

**localStorage.ts (58 lines)**
- Strongly-typed localStorage wrapper with JSON parsing, serialization, and custom events
- **Key Exports**:
  - `LocalStorageTypes` (8+ typed fields): onboarding status, IDE type, font size, extension version, input history, indexing toggle, deprecation banner state
  - `LocalStorageKey` enum: `IsExploreDialogOpen`, `HasDismissedExploreDialog`
  - `getLocalStorage<T>(key: T): LocalStorageTypes[T] | undefined` - JSON parse with error handling
  - `setLocalStorage<T>(key: T, value: LocalStorageTypes[T]): void` - JSON stringify + emit `localStorageChange` CustomEvent
- **Dependencies**: `OnboardingStatus` type from components; used by GUI utilities and Redux

**migrateLocalStorage.ts (68 lines)**
- Migration routine for legacy GUI tool settings in localStorage/Redux
- **Key Exports**: 
  - `migrateLocalStorage(dispatch: AppDispatch)` - Public entry point
  - `migrateToolPolicies(dispatch)` - Internal migration logic
- **Migration Logic**: Maps legacy builtin tool keys (e.g., `builtin_read_file`, `builtin_edit_existing_file`) to new `BuiltInToolNames` enum values
- **Policy Values**: Only migrates valid `ToolPolicy` values (`allowedWithPermission`, `allowedWithoutPermission`, `disabled`)
- **Dispatch**: Calls `setToolPolicy(...)` and `clearToolPolicy(...)` via Redux `uiSlice`

### 3. Navigation & Routing

**navigation.ts (35 lines)**
- Route definitions and config-panel navigation utilities
- **Key Exports**:
  - `ConfigTab` (union type): `models | rules | tools | configs | indexing | settings | help`
  - `ROUTES` (const object): HOME, HOME_INDEX, CONFIG, THEME, STATS
  - `buildConfigRoute(tab?: ConfigTab): string` - Builds `/config?tab=...` or `/config`
  - `CONFIG_ROUTES` (record): Pre-built URLs for all config tabs
- **Usage**: URL generation, route guards, tab-based navigation in config pages

### 4. Platform & Environment Detection

**index.ts (89 lines)**
- Miscellaneous GUI environment helpers and cross-platform adapters
- **Key Exports**:
  - `Platform` (type): `mac | linux | windows | unknown`
  - `getPlatform(): Platform` - Detects OS via `window.navigator.platform`
  - `isMetaEquivalentKeyPressed({ metaKey, ctrlKey }): boolean` - Platform-aware: mac → metaKey, else → ctrlKey
  - `getMetaKeyLabel(): string` - Returns `⌘` (mac) or `Ctrl` (else)
  - `getAltKeyLabel(): string` - Returns `⌥` (mac) or `Alt` (else)
  - `getFontSize(): number` - Reads from localStorage; defaults to 15px (JetBrains) or 14px
  - `fontSize(n: number): string` - Returns computed size as CSS px string
  - `isJetBrains(): boolean` - IDE type check (localStorage `ide === jetbrains`)
  - `isShareSessionSupported(): boolean` - Share sessions NOT supported in JetBrains; true for VSCode
  - `isWebEnvironment(): boolean` - True if NOT Electron (native web browser)
  - `isPrerelease(): boolean` - True if extension minor version is odd
  - `isLocalProfile(_profile: ProfileDescription): boolean` - Always returns true (TODO implementation)

### 5. Client-Side Tool Implementations

**callClientTool.ts (68 lines)** — Dispatcher & Type Definitions
- Main dispatcher for GUI-side builtin client tools (EditExistingFile, SingleFindAndReplace, MultiEdit)
- **Key Types**:
  - `ClientToolExtras` (interface): `getState`, `dispatch: AppThunkDispatch`, `ideMessenger: IIdeMessenger`
  - `ClientToolImpl` (type): `(args, toolCallId, extras) → Promise<ClientToolOutput>`
  - `ClientToolOutput` (interface): `output: ContextItem[] | undefined`, `respondImmediately: boolean`
  - `ClientToolResult` (interface): Extends ClientToolOutput + optional `error: ContinueError`
- **Export**: `callClientTool(toolCallState: ToolCallState, extras: ClientToolExtras): Promise<ClientToolResult>`
- **Routing Logic** (switch on `toolCall.function.name`):
  - `BuiltInToolNames.EditExistingFile` → `editToolImpl(...)`
  - `BuiltInToolNames.SingleFindAndReplace` → `singleFindAndReplaceImpl(...)`
  - `BuiltInToolNames.MultiEdit` → `multiEditImpl(...)`
- **Error Normalization**: Converts `Error` → `ContinueError(ContinueErrorReason.Unspecified)` or `.Unknown`

**editImpl.ts (53 lines)** — EditExistingFile Implementation
- Client-side handler for `BuiltInToolNames.EditExistingFile`
- **Export**: `editToolImpl: ClientToolImpl`
- **Execution Flow**:
  1. Validate `args.filepath` and `args.changes` required
  2. Strip leading `./` from filepath
  3. Resolve relative path via `resolveRelativePathInDir(...)`
  4. Fallback: check open files if path not found
  5. Throw error if file does not exist
  6. Generate `streamId` via `uuid()`
  7. Dispatch Redux thunk `applyForEditTool({ streamId, text, toolCallId, filepath })`
  8. Return `{ respondImmediately: false, output: undefined }` (completion via apply-state)
- **Dependencies**: `resolveRelativePathInDir`, `extras.ideMessenger.ide.getOpenFiles()`, Redux thunk `applyForEditTool`

**singleFindAndReplaceImpl.ts (51 lines)** — SingleFindAndReplace Implementation
- Client-side handler for `BuiltInToolNames.SingleFindAndReplace`
- **Export**: `singleFindAndReplaceImpl: ClientToolImpl`
- **Execution Flow**:
  1. Validate args via `validateSingleEdit(old_string, new_string, replace_all)`
  2. Validate filepath via `validateSearchAndReplaceFilepath(...)`
  3. Read file contents via `extras.ideMessenger.ide.readFile(fileUri)`
  4. Execute replacement via `executeFindAndReplace(contents, oldString, newString, replaceAll, startIndex=0)`
  5. Generate `streamId` via `uuid()`
  6. Dispatch Redux thunk `applyForEditTool({ streamId, text, toolCallId, filepath, isSearchAndReplace: true })`
  7. Return `{ respondImmediately: false, output: undefined }` (completion via apply-state)
- **Dependencies**: Core validation (`validateSingleEdit`, `executeFindAndReplace`, `validateSearchAndReplaceFilepath`), Redux thunk

**multiEditImpl.ts (43 lines)** — MultiEdit Implementation
- Client-side handler for `BuiltInToolNames.MultiEdit`
- **Export**: `multiEditImpl: ClientToolImpl`
- **Execution Flow**:
  1. Validate edits via `validateMultiEdit(args)`
  2. Validate filepath via `validateSearchAndReplaceFilepath(...)`
  3. Read file contents via `extras.ideMessenger.ide.readFile(fileUri)`
  4. Execute multi-replace via `executeMultiFindAndReplace(contents, edits)` (list of find-replace pairs)
  5. Generate `streamId` via `uuid()`
  6. Dispatch Redux thunk `applyForEditTool({ streamId, text, toolCallId, filepath, isSearchAndReplace: true })`
  7. Return `{ respondImmediately: false, output: undefined }` (completion via apply-state)
- **Dependencies**: Core validation (`validateMultiEdit`, `executeMultiFindAndReplace`, `validateSearchAndReplaceFilepath`), Redux thunk
- **Note**: `validateMultiEdit` is deliberately duplicated at both arg-preprocessing stage (core) and here (to handle race conditions if file changes while tool call is pending)

### 6. Conversation & Session Management

**compactConversation.ts (58 lines)** — Conversation Compaction Hooks
- React hooks for managing conversation compaction and deletion
- **Key Exports**:
  - `useCompactConversation(): (index: number) => Promise<void>` - Compact conversation at index
  - `useDeleteCompaction(): (index: number) => void` - Delete compaction at index
- **useCompactConversation Flow**:
  1. Extract current session ID from Redux state
  2. Dispatch `setCompactionLoading({ index, loading: true })`
  3. Call `ideMessenger.request("conversation/compact", { index, sessionId })`
  4. Reload session via `loadSession({ sessionId, saveCurrentSession: false })`
  5. Finally: Dispatch `setCompactionLoading({ index, loading: false })`
- **useDeleteCompaction Flow**:
  1. Dispatch `deleteCompaction(index)` Redux action
  2. Dispatch `saveCurrentSession({openNewSession: false, generateTitle: false})`
- **Dependencies**: `IdeMessengerContext`, Redux hooks/thunks, `sessionSlice` actions

### 7. Edit Outcome Telemetry

**editOutcomeLogger.ts (208 lines)** — Edit Outcome Logging & Assembly
- Core telemetry for edit tool acceptance/rejection in Agent Mode
- **Key Exports**:
  - `assembleEditOutcomeData(history, config, toolCallState, applyState, accepted)` - Assemble telemetry object
  - `logAgentModeEditOutcome(...)` - Send telemetry to devdata logger
  - `extractModelInfo()`, `extractPromptAndCompletion()`, `extractCodeChanges()` - Decomposed helpers
- **Telemetry Fields** (assembled in object):
  - `streamId` - Apply state ID
  - `timestamp` - ISO timestamp
  - `modelProvider`, `modelName`, `modelTitle` - LLM details (from assistant message or config fallback)
  - `prompt` - User message text (reconstructed from content array, handling text parts)
  - `completion` - Assistant message text
  - `previousCode`, `newCode` - Before/after file contents (from `applyState.originalFileContent` and `applyState.fileContent`)
  - `filepath` - Target file path
  - `previousCodeLines`, `newCodeLines`, `lineChange` - Code metrics (proper empty-string handling)
  - `accepted` - Boolean outcome flag
- **Model Extraction**:
  - Search assistant message history for tool call by ID
  - Extract `provider::model` string from message, split on `::` 
  - Fallback to `config.selectedModelByRole.chat` if not in message
- **Prompt/Completion Extraction**:
  - Walk history backward from assistant message to find prior user message
  - Handle both string and content array formats
  - For content arrays, extract text parts and join
- **Code Changes**:
  - Calculate line counts: empty string = 0 lines, else split on `\n`
  - Compute `lineChange = newCodeLines - previousCodeLines`
- **Dispatch**: Posts to devdata via `ideMessenger.post("devdata/log", { name: "editOutcome", data: editOutcomeData })`

### 8. Error Analysis & Provider-Specific Messaging

**errorAnalysis.ts (177 lines)** — Smart Error Classification & Messaging
- Contextual error analysis for LLM failures with provider-specific help
- **Key Exports**:
  - `analyzeError(error: unknown, selectedModel: any): ErrorAnalysis` - Analyze and classify error
  - `ErrorAnalysis` (interface): `parsedError`, `statusCode`, `message`, `modelTitle`, `providerName`, `apiKeyUrl`, `helpUrl`, `customErrorMessage`
  - `parseErrorMessage(fullErrMsg: string): string` - Extract core error from multi-line messages
- **Error Classification Logic**:
  1. **Parse Error Format**: Split on `\n\n`, extract JSON if present, otherwise return raw text
  2. **Extract Status Code**: Parse HTTP status from message (e.g., "401 Unauthorized" → 401)
  3. **Provider Detection**: Match against `selectedModel.underlyingProviderName` and provider registry
  4. **Custom Message Mapping** (pattern matching on `message + parsedError`):
     - **OpenAI Org Verification** (`organization must be verified to generate reasoning summaries|stream`): Add link + suggest `useResponsesApi: false`
     - **Invalid API Key** (`incorrect|invalid api key|invalid x-api-key`): Check for `secrets.` string templating failure
     - **Missing Auth** (`missing bearer or basic authentication`): Suggest adding `apiKey` to model config
     - **Ollama Tool Parse** (`error parsing tool call`): Suggest resubmit + system-message-only tools workaround
     - **402 Insufficient Balance**: Personalized message with provider label
- **Provider Lookup**: Search `providers` config for `foundProvider.title`, `foundProvider.apiKeyUrl`

### 9. Tool Call Streaming & Merging

**toolCallState.ts (79 lines)** — Streamed Tool Call Delta Merging
- Low-level utilities for handling OpenAI-style streamed tool calls
- **Key Exports**:
  - `addToolCallDeltaToState(toolCallDelta: ToolCallDelta, currentState?: ToolCallState): ToolCallState` - Merge delta into state
  - `isEditTool(toolName: string): boolean` - Check if tool is edit-related
- **Delta Merging Logic (for name)**:
  - If delta name starts with current name: use delta (progressive streaming case)
  - Else if current name doesn't start with delta: concatenate
  - Else: use current name (delta is already contained)
- **Delta Merging Logic (for args)**:
  - Try to parse current args as JSON
  - If successful: args complete, don't add delta
  - If parse fails: concatenate current + delta, then attempt incremental parse via `incrementalParseJson()`
- **State Return**: Full `ToolCallState` with `status: "generating"`, merged `function.name/arguments`, and `parsedArgs` (result of incremental parse)
- **Edit Tool Detection**: List of 3 hardcoded names: `EditExistingFile`, `SingleFindAndReplace`, `MultiEdit` from `BuiltInToolNames`

### 10. Testing Utilities & Fixtures

**setupTests.ts (72 lines)** — Vitest Environment Setup
- Global setup file for test environment initialization
- **Key Exports**: None (runs as side effect during test startup)
- **Vitest Lifecycle Hooks**:
  - `afterEach()` (line 3): Clears all mock calls and return values
  - `afterAll()` (line 7): Resets all mocks completely
- **DOM Mocks**:
  - **matchMedia** (lines 33-45): Mock `window.matchMedia()` required for CSS media queries; returns object with `matches: false`, event listeners, etc.
  - **getClientRects** (lines 48-61): Mock Element.prototype for ProseMirror compatibility; returns array-like object with single rect `[0]` and `item()` method (metrics: top/bottom/left/right/width/height)
  - **getBoundingClientRect** (lines 63-71): Mock Element.prototype for layout calculations
- **Error Suppression** (lines 12-30):
  - Catches `error` events and `unhandledrejection` events
  - Filters for ProseMirror-related errors (`getClientRects`, `prosemirror`)
  - Prevents them from failing tests (calls `event.preventDefault()`)

**config.ts (61 lines)** — Redux Config Test Helpers
- Utilities for simulating config changes during tests
- **Key Exports**:
  - `triggerConfigUpdate(params: TestConfigUpdateParams)` - Simulate config update message
  - `addAndSelectChatModel(store, ideMessenger, llmDesc)` - Add model to config
  - `addAndSelectMockLlm(store, ideMessenger)` - Add hard-coded mock LLM
- **triggerConfigUpdate Flow**:
  1. Extract current store state
  2. Call `ideMessenger.mockMessageToWebview("configUpdate", { ... })`
  3. Pass `profileId`, `profiles`, and edited config via `result` object
  4. Optional `editConfig` callback allows mutation of config before send
- **Model Selection**:
  - `addAndSelectChatModel()` uses `editConfig` to add model to `modelsByRole.chat` and set as `selectedModelByRole.chat`
  - `addAndSelectMockLlm()` selects a hard-coded mock with `provider: "mock"`, `model: "mock"`
- **Dependencies**: `MockIdeMessenger`, Redux store, core config types

**utils.ts (102 lines)** — DOM Query & Interaction Helpers
- Testing library utilities for querying and interacting with rendered UI
- **Key Exports**:
  - `logDomText()` - Log cleaned body text (filters common fixture noise)
  - `logAllTestIds()` - Log all `data-testid` attributes in DOM
  - `getElementByTestId(testId: string): Promise<HTMLElement>` - Assert element with testId exists
  - `verifyNotPresentByTestId(testId: string): Promise<void>` - Assert element NOT in DOM
  - `verifyNotPresentByText(text: string): Promise<void>` - Assert text NOT in DOM
  - `getElementByText(text: string): Promise<HTMLElement>` - Assert element with text exists
  - `getMainEditor(): Promise<Editor>` - Get Tiptap editor instance from container
  - `sendInputWithMockedResponse(ideMessenger, input, response)` - Simulate user message + receive LLM response
- **logDomText Cleanup** (lines 12-22):
  - Removes noise like "No results", "Aa", "Models", "Rules", keyboard shortcuts
  - Filters common UI labels (Select model, Mock LLM, Chat)
- **Element Lookup** (lines 36-75):
  - `getElementByTestId()`: Uses `screen.findByTestId()` with `waitFor()`; logs all testIds if not found
  - `getElementByText()`: Uses `screen.findByText()` with `waitFor()`; logs DOM text if not found
  - `getMainEditor()`: Gets element by testId `editor-input-main`, accesses `element.editor` property
- **User Interaction** (lines 77-102):
  - `sendInputWithMockedResponse()`: 
    1. Set `ideMessenger.chatResponse` before action
    2. Get main editor, find send button by testId
    3. Use `editor.commands.insertContent(input)` to add text
    4. Wait for text to appear in DOM
    5. Click send button with `act()`

**mockStore.ts (114 lines)** — Redux Store Mock Factory
- Factory for creating Redux store with mocked ideMessenger and action tracking
- **Key Exports**:
  - `getEmptyRootState(): RootState` - Create fresh Redux root state
  - `createMockStore(initialState?, mockMessenger?): EnhancedStore + { mockIdeMessenger, getActions, clearActions }`
- **getEmptyRootState Logic** (lines 26-51):
  - Initialize all 6 slices with INITIAL_* constants `(config, ui, editModeState, indexing, profiles, tabs, session)`
  - Handle non-serializable `streamAborter` by:
    1. Destructure `{ streamAborter, ...serializableSession }` from INITIAL_SESSION_STATE
    2. Deep copy both with `copyOf()`
    3. Reconnect with fresh `new AbortController()` in result
- **createMockStore Logic** (lines 53-114):
  - Set up `configureStore()` with 6 reducers
  - Merge preloaded state with `getEmptyRootState()` and optional `initialState`
  - Configure middleware:
    - Serializable check: ignore `session.streamAborter` and `ui.dialogMessage`
    - Thunk extra argument: inject `{ ideMessenger: mockIdeMessenger }`
  - Override `store.dispatch()` with `vi.fn()` to:
    1. Detect if action is thunk (function)
    2. If thunk: call with `store.dispatch`, `store.getState`, and ideMessenger extra
    3. If normal action: track in `actions` array, then call `originalDispatch()`
  - Expose helper methods:
    - `getActions()` - Return tracked action array
    - `clearActions()` - Splice array to reset
- **Return**: Enhanced store + `{ mockIdeMessenger, getActions, clearActions }`

**render.tsx (77 lines)** — React Testing Library Render Wrapper
- Custom render function wrapping component with all required providers for testing
- **Key Exports**:
  - `ExtendedRenderOptions` (type) - RTL render options extended with `store`, `routerProps`, `mockIdeMessenger`
  - `renderWithProviders(ui: React.ReactElement, options?: ExtendedRenderOptions): Promise<...>` - Async render with providers
- **Provider Stack** (lines 50-63):
  - `<MemoryRouter>` - React Router (in-memory history for navigation testing)
  - `<IdeMessengerProvider>` - IDEMessenger context
  - `<Provider store={store}>` - Redux store provider
  - `<AuthProvider>` - Authentication context
  - `<MainEditorProvider>` - Tiptap editor provider
  - `<ParallelListeners />` - Global event listener hook
- **setupMocks** (lines 23-29):
  - Mock `global.ResizeObserver` with `vi.fn()` returning `{ observe, unobserve, disconnect }` mocks
- **renderWithProviders Flow** (lines 31-77):
  1. Call `setupMocks()` to initialize ResizeObserver mock
  2. Use provided `mockIdeMessenger` or create new `MockIdeMessenger()`
  3. Merge render options: create store if not provided (via `setupStore()`), default to empty `routerProps`
  4. Set up `userEvent.setup()` for user interaction simulation
  5. Create Wrapper component with full provider stack
  6. Call `render(ui, { wrapper: Wrapper, ...renderOptions })` inside `act()`
  7. Return object with `{ user, store, ideMessenger, ...rendered }`

### Cross-Cutting Concerns (Expanded)

**GUI/Core Integration**:
- Client tools depend on core edit/search-and-replace validators (`core/edit/searchAndReplace/*`)
- All three edit tools coordinate through a single Redux thunk `applyForEditTool`
- `migrateLocalStorage` depends on `BuiltInToolNames` from `core/tools/builtIn` to ensure naming consistency
- `localStorage.ts` event dispatch allows tab-wide synchronization of settings changes

**Redux Coordination**:
- Tool policies persisted in `ui.toolSettings` slice
- Migration routine re-dispatches policies via `uiSlice` reducers
- Client tool extras pass `AppThunkDispatch` for apply thunk execution

---

## 🔧 TOOLS INFRASTRUCTURE (6 files, 608 lines)

**Overview**: Tool definitions, registry, argument parsing, execution dispatch, and MCP integration. Covers tool discovery (built-in + config-dependent), argument type coercion, tool override application, and routing to implementations (built-in sync, HTTP remote, or MCP protocol).

### 1. Tool Definition Factory (index.ts - 56 lines)

**Purpose**: Entry point for tool discovery; returns base tools and config-dependent tools.

**Base Tools** (Lines 6-16):
Returns 9 always-available tools:
- `readFileTool` — Read file content (full)
- `createNewFileTool` — Create file with content
- `runTerminalCommandTool` — Execute shell command
- `globSearchTool` — File glob pattern search
- `viewDiffTool` — Show unified diff
- `readCurrentlyOpenFileTool` — Read current editor file
- `lsTool` — List directory
- `createRuleBlock` — Create rule block
- `fetchUrlContentTool` — HTTP GET & parse

**Config-Dependent Tools** (Lines 18-51):
Returns conditional tools based on params:

| Task | Condition | Tools Added | Lines |
|------|-----------|------------|-------|
| **Rule & Skill Tools** | Always | `requestRuleTool`, `readSkillTool` | 24-25 |
| **Web Search** | Always | `searchWebTool` | 27 |
| **Experimental** | `enableExperimentalTools: true` | viewRepoMap, viewSubdirectory, codebase, readFileRange | 29-36 |
| **Multi-Edit Support** | `modelName` is recommended agent model | `multiEditTool` | 38-39 |
| **Edit Fallback** | Model NOT recommended for agent | `editFileTool`, `singleFindAndReplaceTool` | 40-42 |
| **Remote Grep** | `!isRemote` (local only) | `grepSearchTool` | 46-48 |

**Serialization** (Lines 53-56):
```typescript
export function serializeTool(tool: Tool) {
  const { preprocessArgs, evaluateToolCallPolicy, ...rest } = tool;
  return rest;  // Strip runtime function fields for GUI
}
```

---

### 2. MCP Tool Naming (mcpToolName.ts - 18 lines)

**Purpose**: Normalize MCP server + tool names into a combined identifier.

**Functions**:

| Function | Lines | Input | Output | Example |
|----------|-------|-------|--------|---------|
| `getMCPToolName()` | 2-4 | MCPServerStatus, MCPTool | Normalized name | `"my_server_my_tool"` |
| `getToolNameFromMCPServer()` | 6-18 | serverName, toolName | Prefixed tool name | Input: ("My-Server", "fetch") → Output: "my_server_fetch" |

**Naming Rules** (Lines 8-12):
1. Lowercase server name: `"My-Server"` → `"my-server"`
2. Replace non-alphanumeric sequences with single `_`: `"-"` → `"_"`
3. Remove leading/trailing `_`: `"_my_server_"` → `"my_server"`
4. Remove duplicate `_`: `"my__server"` → `"my_server"`
5. If tool name already starts with prefix, return as-is (line 14-15)

---

### 3. Tool Argument Parser (parseArgs.ts - 153 lines)

**Purpose**: Safely extract, type-coerce, and validate tool call arguments from LLM output.

**Core Functions**:

| Function | Lines | Purpose | Behavior |
|----------|-------|---------|----------|
| `safeParseToolCallArgs()` | 3-25 | Parse tool call args | 1) If args is already object, return it; 2) Try JSON.parse(args string); 3) On error, return {} |
| `coerceArgsToSchema()` | 34-63 | Fix JSON over-parsing | Re-stringify values that should be strings (e.g., JSON file content parsed to object) |
| `getStringArg()` | 65-102 | Extract + validate string | Throws if missing, non-string, or empty (unless allowEmpty=true); re-stringifies objects |
| `getOptionalStringArg()` | 104-113 | Extract optional string | Returns undefined if missing; else calls getStringArg() |
| `getNumberArg()` | 115-131 | Extract + validate number | Parses string "123" → 123; Math.floor(); throws if invalid or NaN |
| `getBooleanArg()` | 133-150+ | Extract boolean | "true"/"false" strings → true/false; throws if invalid |

**Argument Coercion Issue** (Lines 27-32):
LLMs often return JSON-like strings for arguments (e.g., `file_content: "{...json...}"`). JSON.parse() deeply parses this to an object, but the tool expects a string. `coerceArgsToSchema()` checks the JSON schema and re-stringifies string-typed fields.

**Error Handling**:
- Missing arg → `Error: 'argName' is required`
- Empty string (when not allowed) → `Error: Argument must not be empty`
- Type mismatch → `Error: Argument must be a valid number`

---

### 4. Tool Override Application (applyToolOverrides.ts - 69 lines)

**Purpose**: Apply user-defined config overrides to tool definitions (disable, rename, re-describe).

**Main Function** (Lines 14-68):
```typescript
export function applyToolOverrides(
  tools: Tool[],
  overrides: ToolOverride[] | undefined,
): ApplyToolOverridesResult { ... }
```

**Override Fields** (Lines 25-66):

| Field | Type | Effect |
|-------|------|--------|
| `override.name` | string | Identify tool to override |
| `override.disabled` | boolean | If true, remove tool from list |
| `override.description` | string | Update function description |
| `override.displayTitle` | string | Update human display name |
| `override.wouldLikeTo` | string | Update action phrase |
| `override.isCurrently` | string | Update status phrase |
| `override.hasAlready` | string | Update completion phrase |
| `override.systemMessageDescription` | object | Prefix + exampleArgs for system message |

**Return Type** (Lines 4-7):
```typescript
interface ApplyToolOverridesResult {
  tools: Tool[];              // Filtered/modified tools
  errors: ConfigValidationError[];  // Validation errors
}
```

**Error Handling** (Lines 28-34):
If override name doesn't match any tool, add validation error (non-fatal) and continue.

---

### 5. Built-In Tool Enum (builtIn.ts - 32 lines)

**Purpose**: Define all 19 built-in tool names + group metadata.

**BuiltInToolNames Enum** (Lines 1-24):

| Name | Value | Purpose |
|------|-------|---------|
| ReadFile | `"read_file"` | Read file content (full) |
| ReadFileRange | `"read_file_range"` | Read specific line range |
| EditExistingFile | `"edit_existing_file"` | Edit/modify file |
| SingleFindAndReplace | `"single_find_and_replace"` | Single regex replacement |
| MultiEdit | `"multi_edit"` | Multiple edits in one call |
| ReadCurrentlyOpenFile | `"read_currently_open_file"` | Read current editor file |
| CreateNewFile | `"create_new_file"` | Create new file with content |
| RunTerminalCommand | `"run_terminal_command"` | Execute shell command |
| GrepSearch | `"grep_search"` | Grep pattern search |
| FileGlobSearch | `"file_glob_search"` | Glob pattern file search |
| SearchWeb | `"search_web"` | Web search (e.g., Brave) |
| ViewDiff | `"view_diff"` | Show unified diff |
| LSTool | `"ls"` | List directory |
| CreateRuleBlock | `"create_rule_block"` | Create rule block |
| RequestRule | `"request_rule"` | Request rule |
| FetchUrlContent | `"fetch_url_content"` | HTTP GET content |
| CodebaseTool | `"codebase"` | Codebase search/analyze |
| ReadSkill | `"read_skill"` | Read skill/knowledge |
| ViewRepoMap | `"view_repo_map"` | Show repo structure (excluded from allTools) |
| ViewSubdirectory | `"view_subdirectory"` | Show subdir structure (excluded from allTools) |

**Constants**:
- `BUILT_IN_GROUP_NAME = "Built-In"` — Group marker (line 26)
- `CLIENT_TOOLS_IMPLS = [EditExistingFile, SingleFindAndReplace, MultiEdit]` — Tools run on client, not server (lines 28-32)

---

### 6. Tool Call Dispatcher (callTool.ts - 280 lines)

**Purpose**: Route tool calls to implementations; dispatch built-in, HTTP, or MCP tools; handle errors gracefully.

**Main Entry Point** (Lines 235-280):
```typescript
export async function callTool(
  tool: Tool,
  toolCall: ToolCall,
  extras: ToolExtras,
): Promise<{
  contextItems: ContextItem[];
  errorMessage: string | undefined;
  errorReason?: ContinueErrorReason;
  mcpUiState?: McpUiState;
}> { ... }
```

**Dispatch Logic** (Lines 247-251):
```typescript
const { contextItems, mcpUiState } = tool.uri
  ? await callToolFromUri(tool.uri, args, extras)    // HTTP or MCP
  : { contextItems: await callBuiltInTool(...), ... };  // Built-in
```

**Tool Execution Paths**:

| Path | Handler | Lines | Invocation |
|------|---------|-------|-----------|
| **Built-in (no URI)** | `callBuiltInTool()` | 187-230 | Switch statement routes to 19 impl functions |
| **HTTP / External** | `callToolFromUri()` | 67-185 | Supports http://, https://, mcp:// |
| **HTTP Tool** | `callHttpTool()` | 28-50 | POST args to HTTP endpoint |
| **MCP Tool** | MCPManager | 88-181 | Get MCP client, call tool, fetch UI resource |

**Built-In Tool Routing** (Lines 192-229):
Maps `BuiltInToolNames` → implementation:
- `ReadFile` → `readFileImpl()`
- `EditExistingFile` → (handled on client)
- `RunTerminalCommand` → `runTerminalCommandImpl()`
- `GrepSearch` → `grepSearchImpl()`
- etc. (15 more)

**URI Format Handling**:

| Protocol | Format | Handler | Lines |
|----------|--------|---------|-------|
| `http://` | `http://api.example.com/tool` | `callHttpTool()` | 82-86 |
| `https://` | `https://api.example.com/tool` | `callHttpTool()` | 82-86 |
| `mcp://` | `mcp://server-id/tool-name` | MCPManager | 87-181 |

**MCP Tool Call** (Lines 87-181):
1. Decode `mcp://` URI → [mcpId, toolName] via `decodeMCPToolUri()`
2. Get MCP client from `MCPManagerSingleton`
3. Coerce args to schema with `coerceArgsToSchema()`
4. Call `client.callTool()` with args
5. Fetch UI resource if `mcpMeta.ui.resourceUri` present
6. Convert response → `ContextItem[]`
7. Return contextItems + mcpUiState

**HTTP Tool POST** (Lines 28-50):
```json
POST /tool
{
  "arguments": { "arg1": "value1", ... }
}
```
Expects JSON response with `output: ContextItem[]` field.

**URI Encoding/Decoding** (Lines 52-65):
```typescript
encodeMCPToolUri("server-id", "tool-name")
  → "mcp://server-id/tool-name"

decodeMCPToolUri("mcp://server-id/tool-name")
  → ["server-id", "tool-name"]
```
Uses `encodeURIComponent()` for proper URL encoding (e.g., spaces, special chars).

**Error Handling** (Lines 245-278):
1. Parse args via `safeParseToolCallArgs()` (fallback {})
2. Try dispatch to built-in or URI handler
3. Catch errors:
   - If `ContinueError` → extract reason + message
   - If `Error` → extract message
   - Return `{ contextItems: [], errorMessage, errorReason }`
4. Apply favicon to all context items (line 252-256)

**Context Item Assembly** (Lines 144-180):
MCP responses map to ContextItem:
- Text items → `{ name, description: "Tool output", content: text, icon }`
- Resource items → `{ name, description, content: resource.text, icon }`
- Unknown types → error ContextItem with explanation

---

## Core Type Definitions (index.d.ts - Complete Type System)

**Tool Definition** (lines 1132-1168):
| Property | Type | Purpose |
|----------|------|---------|
| `type` | `"function"` | Always function type |
| `function.name` | string | Tool identifier |
| `displayTitle` | string | User-facing label |
| `group` | string | Tool category |
| `readonly` | boolean | Execute permission |

**Runtime Config** (line 1820-1841):
- `ContinueConfig.tools: Tool[]` — Full runtime tools WITH preprocessors/validators

**Browser Serialized** (line 1843-1863) — **WHAT GUI RECEIVES**:
- `BrowserSerializedContinueConfig.tools: Omit<Tool, "preprocessArgs", "evaluateToolCallPolicy">[]` — STRIPPED of runtime functions
- This exact type arrives in `configSlice.setConfigResult()` (line 51)

---

## Layer 2A-3: Telemetry & Indexing Foundation

### Purpose
Local SQLite token tracking, structured dev-data logging (local JSON-L + remote HTTPS), and directory walking with .gitignore/.continueignore caching for the indexing pipeline.

### Key Classes

**DevDataSqliteDb** (`core/data/devdataSqlite.ts:11-92`)  
Singleton SQLite database for token usage telemetry.
- **createTables()** — CREATE IF NOT EXISTS tokens_generated; ALTER ADD tokens_prompt if missing
- **logTokensGenerated(model, provider, promptTokens, generatedTokens)** — INSERT into tokens_generated
- **getTokensPerDay()** — SELECT date, sum(tokens_prompt + tokens_generated) GROUP BY day
- **getTokensPerModel()** — SELECT model, sum(tokens_prompt + tokens_generated) GROUP BY model
- **get()** — Lazy singleton; opens DB at `getDevDataSqlitePath()`

**DataLogger** (`core/data/log.ts:21-238`)  
Singleton structured event logging with schema validation and multi-destination support.
- **getInstance()** — Lazy singleton
- **addBaseValues(body, eventName, schema, zodSchema)** — Inject timestamps, userAgent, userId, selectedProfileId via Zod shape introspection
- **logLocalData(event)** — Append JSON-L to local .continue file (schema validation via devDataVersionedSchemas)
- **logDevData(event)** — Orchestrate local + all remote (config.data[]) logging
- **parseEventData(event, schema, level)** — Schema lookup (devDataVersionedSchemas) + base-value injection + Zod parse
- **logToOneDestination(dataConfig, event)** — Route to HTTPS (POST with bearer token) or file:// (JSON-L)
- **ideSettingsPromise, ideInfoPromise** — Resolved on init to populate user token + IDE version in base values
- Imports `@continuedev/config-yaml` dev-data schemas (0.2.0 version used locally)

### Key Functions

**shouldIgnore(fileUri, ide, rootDirCandidates?)** (`core/indexing/shouldIgnore.ts:15-74`)  
Check whether a file should be excluded from indexing.
- Walk UP from file to root, checking ignores at each level
- Detect symlinks on direct parent (exclude if found)
- Use `defaultIgnoreFileAndDir` + `getGlobalContinueIgArray()` + per-dir `.gitignore/.continueignore`
- Return true if any ignore rule matches

**walkDirAsync(path, ide, optionOverrides?)** (`core/indexing/walkDir.ts:266-273`)  
Depth-first directory traversal yielding file/directory URIs.
- Async generator; yields path per entry
- Options: `include` (files|dirs|both), `recursive`, `returnRelativeUrisPaths`, `source`, `overrideDefaultIgnores`
- Default: files only, absolute URIs, recursive

**walkDir(uri, ide, optionOverrides?)** (`core/indexing/walkDir.ts:275-285`)  
Collect all results from walkDirAsync into array.

**walkDirs(ide, optionOverrides?, dirs?)** (`core/indexing/walkDir.ts:287-297`)  
Parallel walk all workspace directories (defaults to `ide.getWorkspaceDirs()`).

**getIgnoreContext(currentDir, currentDirEntries, ide, defaultAndGlobalIgnores)** (`core/indexing/walkDir.ts:299-347`)  
Load local .gitignore and .continueignore, return composite Ignore object.
- Async read both files (if present) via `gitIgArrayFromFile()`
- Precedence: defaultAndGlobalIgnores (in middle) ← gitignore (first) ← local .continueignore (last/highest)
- Short-circuit if both files empty

### Caching & Performance

**WalkDirCache** (`core/indexing/walkDir.ts:42-72`)  
Cache wrapper for listDir() and ignore patterns.
- `dirListCache: Map<uri, {time, entries: Promise}>`
- `dirIgnoreCache: Map<uri, {time, ignore: Promise}>`
- TTL: 30 seconds (LIST_DIR_CACHE_TIME, IGNORE_FILE_CACHE_TIME)
- `invalidate()` — Clear all caches

**DFSWalker** (`core/indexing/walkDir.ts:74-258`)  
Core DFS implementation with inline caching.
- Stack-based traversal (entries are promises; resolve once)
- Per-directory ignore context accumulation (stack of {ignore, dirname})
- Relative path matching against each ignore rule
- Symlink skipping + optional include/exclude filtering
- Metrics: dirs traversed, cache hits, ignore time

### Dependencies

| From | To | Purpose |
|------|----|---------| 
| devdataSqlite | sqlite3, sqlite | DB ops |
| devdataSqlite | ../util/paths | `getDevDataSqlitePath()` |
| devdataSqlite | ../indexing/refreshIndex | `DatabaseConnection` type |
| log | @continuedev/config-yaml | devDataVersionedSchemas |
| log | ../core.js | `Core` class (configHandler) |
| log | ../util/paths | `getDevDataFilePath(relPath)` |
| log | @continuedev/fetch | `fetchwithRequestOptions()` |
| log | uri-js | URI parsing/validation |
| shouldIgnore | ./walkDir | `getIgnoreContext()` |
| shouldIgnore | ../util/uri | `findUriInDirs()`, `getUriPathBasename()` |
| shouldIgnore | ./continueignore | `getGlobalContinueIgArray()` |
| shouldIgnore | ./ignore | `defaultIgnoreFileAndDir` |
| walkDir | ignore | Ignore pattern matching |
| walkDir | ./continueignore | `getGlobalContinueIgArray()` |
| walkDir | ./ignore | `defaultIgnoreFileAndDir`, `gitIgArrayFromFile()` |
| walkDir | ../util/uri | `joinPathsToUri()` |

### Data Contracts

**Dev Data Event** (from @continuedev/config-yaml)  
```ts
interface DevDataLogEvent {
  name: string;           // Event name (validated against schema)
  data: Record<string, any>;  // Event payload
}
```

**Token Metrics** (devdataSqlite)  
```ts
schema tokens_generated {
  id INTEGER PRIMARY KEY,
  model TEXT,
  provider TEXT,
  tokens_prompt INTEGER,           // Token count in prompt
  tokens_generated INTEGER,        // Token count in completion
  timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
}
```

**Data Config** (from ContinueConfig)  
```ts
interface DataConfig {
  schema: string;                 // Version (e.g., "0.2.0")
  destination: "https://..." | "file://...";  // Remote or local
  level?: "all" | "noCode";
  events?: string[];              // If set, filter to these event names
  apiKey?: string;                // For HTTPS auth
  requestOptions?: RequestInit;   // Proxy, timeout, etc.
}
```

**Walker Options**  
```ts
interface WalkerOptions {
  include?: "files" | "dirs" | "both";      // What to yield
  returnRelativeUrisPaths?: boolean;        // Relative or absolute URIs
  source?: string;                           // Debug source label
  overrideDefaultIgnores?: Ignore;          // Custom ignore rules
  recursive?: boolean;                       // Default: true
}
```

### Integration Points

1. **From `core/tools/callTool.ts`**: Built-in tools (readFile, etc.) use `shouldIgnore()` to filter results
2. **From `core/config/load.ts`**: Config loading triggers `getDevDataSqlitePath()` to validate dev-data persistence
3. **To `core/config/index.ts`** or **`core/index.d.ts`**: `DevDataSqliteDb.logTokensGenerated()` called when LLM returns completion tokens
4. **To browser/GUI**: Log events filtered by `DataLogger.logDevData()` before sending to remote

---

## Layer 2A-1: Config Infrastructure (Handlers, Loaders, Creators)

### Purpose
Profile lifecycle management, multi-tier config discovery (workspace .continue + ~/.continue), custom block creation (rules/prompts/agents), onboarding templates, and cascading reload orchestration.

### Key Classes

**ConfigHandler** (`core/config/ConfigHandler.ts:31-369`)  
Central orchestrator managing profiles, lifecycle, and listeners.
- **cascadeInit(reason)** — Load profiles, select current, save preference, reloadConfig
- **loadProfiles()** — Discover ProfileLifecycleManager[] from ~/.continue + workspace .continue/
- **getLocalProfiles(options)** — Return global + workspace profiles
- **reloadConfig(reason)** — Clear non-current; await currentProfile.reloadConfig(); notify listeners
- **getSerializedConfig()** — Promise<ConfigResult<BrowserSerializedContinueConfig>>
- **loadConfig()** — Promise<ConfigResult<ContinueConfig>>
- **setSelectedProfileId(profileId)** — Validate, save, reloadConfig
- **onConfigUpdate(listener)** — Subscribe to config changes
- **isInitialized**: Promise<void>

**CodebaseRulesCache** (`core/config/markdown/loadCodebaseRules.ts:10-62`)  
Singleton cache for rules.md.
- **getInstance()** — Lazy singleton
- **refresh(ide)** — Walk + scan rules.md; populate this.rules
- **update(ide, uri)** — Load single rule; upsert by sourceFile
- **remove(uri)** — Delete rule by sourceFile

### Key Functions

**createNewAssistantFile(ide)** (`core/config/createNewAssistantFile.ts:42-69`)  
Create template config.yaml in .continue/agents/

**isContinueConfigRelatedUri(uri)** (`core/config/loadLocalAssistants.ts:16-30`)  
Check if URI is config-related (.continuerc.json, .continue/**, agent files, etc.)

**isContinueAgentConfigFile(uri)** (`core/config/loadLocalAssistants.ts:32-44`)  
Check if URI in .continue/{agents,assistants,configs}/

**isColocatedRulesFile(uri)** (`core/config/loadLocalAssistants.ts:46-48`)  
Check if basename === rules.md

**getAllDotContinueDefinitionFiles(ide, options, subDirName)** (`core/config/loadLocalAssistants.ts:131-156`)  
Load YAML/Markdown from ~/.continue + workspace .continue/subDirName

**loadCodebaseRules(ide)** (`core/config/markdown/loadCodebaseRules.ts:67-129`)  
Walk workspace; scan rules.md; parse via markdownToRule(); return RuleWithSource[]

**createNewWorkspaceBlockFile(ide, blockType)** (`core/config/workspace/workspaceBlocks.ts:157-184`)  
Create + open .continue/{blockType}/new-{name}.{ext}

**createNewGlobalRuleFile(ide)** (`core/config/workspace/workspaceBlocks.ts:186-213`)  
Create + open ~/.continue/rules/new-{name}.md

**addModel(model, role?)** (`core/config/util.ts:26-92`)  
Add model to config; deduplicate by stringify; auto-suffix title

**deleteModel(title)** (`core/config/util.ts:94-105`)  
Remove model from config by title

**getModelByRole<T>(config, role)** (`core/config/util.ts:107-122`)  
Lookup ILLM by experimental modelRole[role]

**isSupportedLanceDbCpuTargetForLinux(ide?)** (`core/config/util.ts:130-177`)  
Check Linux CPU for AVX2+FMA; cache result; show warning if unsupported

**setupBestConfig(config)** (`core/config/onboarding.ts:94-99`)  
Pass-through; placeholder for best defaults

**setupLocalConfig(config)** (`core/config/onboarding.ts:101-126`)  
Inject Ollama models (Llama 3.1, Qwen 2.5-Coder, Nomic Embed)

**setupProviderConfig(config, provider, apiKey)** (`core/config/onboarding.ts:132-171`)  
Inject provider models (OpenAI/Anthropic/Gemini) + apiKey

### Dependencies

| From | To | Purpose |
|------|----|---------| 
| util | editConfigFile | Edit config files (JSON + YAML) |
| util | GlobalContext | Cache CPU target |
| loadLocalAssistants | walkDir | Directory traversal |
| loadCodebaseRules | walkDirs, markdownToRule | Scan + parse rules.md |
| workspaceBlocks | @continuedev/config-yaml | BlockType, createRuleMarkdown, etc. |
| ConfigHandler | ProfileLifecycleManager | Profile state + reload |
| ConfigHandler | getAllDotContinueDefinitionFiles | Discover profiles |
| onboarding | @continuedev/config-yaml | ConfigYaml, ModelConfig |

### Initialization & Reload Flow

1. **ConfigHandler constructor** → cascadeInit() async
2. **cascadeInit()**:
   - loadProfiles() → discover all profiles
   - Select currentProfile (from lastSelectedProfileForWorkspace or first)
   - Emit "init" event
3. **reloadConfig()**:
   - currentProfile.reloadConfig()
   - notifyConfigListeners(ConfigResult)
4. **Listeners** receive {config, errors, configLoadInterrupted}

### Integration Points

1. From `core/core.js`: Instantiate ConfigHandler → onConfigUpdate()
2. From GUI `configSlice.ts`: getSerializedConfig() → setConfigResult()
3. To tool execution: Use loaded config (models, tools)
4. To messenger: Send config updates to IDE/Webview
5. Workspace changes → ConfigHandler.refreshAll()
6. Profile selection → setSelectedProfileId() → cascadeInit()
