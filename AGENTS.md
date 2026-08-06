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

| `reference/continue-src/gui/src/context/VscTheme.tsx` | 184 | 🟡 Provider | `VscThemeContext`, `VscThemeProvider`, `useVscTheme()` | VSCode theme color mapping (hljs→TextMate token rules) |

| `reference/continue-src/gui/src/context/LocalStorage.tsx` | 68 | 🟡 Provider | `LocalStorageContext`, `LocalStorageProvider`, `useLocalStorage()` | Font size persistence & CustomEvent synchronization |

| `reference/continue-src/gui/src/context/Auth.tsx` | 66 | 🟡 Provider | `AuthContext`, `AuthProvider`, `useAuth()` | Profile selection, refresh, loading state |

| `reference/continue-src/gui/src/context/SubmenuContextProviders.tsx` | 628 | 🟡 Provider | `SubmenuContextProvidersContext`, `SubmenuContextProvidersProvider`, `useSubmenuContextProviders()` | Context submenu search with MiniSearch, intelligent file sorting |

| `reference/continue-src/gui/src/context/IdeMessenger.tsx` | 277 | 🟡 Provider | `IdeMessengerContext`, `IdeMessengerProvider`, `IIdeMessenger` interface, `IdeMessenger` class | Webview↔IDE message routing (request/response, streaming, IDE API wrapper) |

| `reference/continue-src/gui/src/components/OnboardingCard/OnboardingCard.tsx` | 57 | 🟢 Component | `OnboardingCard`, `OnboardingCardState` type | LLM provider/model setup UI with tabs (API key, local) |

| `reference/continue-src/gui/src/components/OSRContextMenu.tsx` | 222 | 🟢 Component | `OSRContextMenu` | On-screen reader context menu (copy, cut, dev tools) for non-Mac platforms |

| `reference/continue-src/gui/src/components/config/FatalErrorNotice.tsx` | 75 | 🟢 Component | `FatalErrorIndicator` | Config error alert with profile name & reload/help links |

| `reference/continue-src/gui/src/components/dialogs/index.tsx` | 81 | 🟢 Component | `TextDialog` | Modal dialog with backdrop, Markdown rendering, Esc/close handlers |

| `reference/continue-src/gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx` | 349 | 🟢 Component | `TipTapEditor`, `TipTapEditorProps` interface, `TipTapEditorInner` | Rich text editor with image drag-drop, toolbar, focus/blur management, Tiptap integration |

| `reference/continue-src/gui/src/hooks/useWebviewListener.ts` | 39 | 🔵 Hook | `useWebviewListener()` | Register typed message listeners on webview; handle IDE→GUI messages with auto-respond |

| `reference/continue-src/gui/src/hooks/ParallelListeners.tsx` | 264 | 🔵 Hook | `ParallelListeners` component | Master event listener for all IDE/Core messages; dispatches Redux actions for config, session, indexing |

| `reference/continue-src/gui/src/redux/hooks.ts` | 5 | 🟢 Utils | `useAppDispatch`, `useAppSelector` | Typed Redux hooks with AppDispatch and RootState |

| `reference/continue-src/gui/src/redux/util/getBaseSystemMessage.ts` | 32 | 🟢 Utils | `getBaseSystemMessage()`, `NO_TOOL_WARNING` constant | Select mode-specific system prompt; append no-tools warning |

| `reference/continue-src/gui/src/redux/util/constructMessages.ts` | 230 | 🟢 Utils | `constructMessages()` | Build LLM message array from history; apply rules; handle tool calls; append summaries |

| `reference/continue-src/gui/src/redux/util/index.ts` | 126 | 🟢 Utils | `hasCurrentToolCalls()`, `findAllCurToolCalls()`, `findToolCallById()`, `findChatHistoryItemByToolCallId()`, `logToolUsage()` | Tool call state query functions; devdata logging |

| `reference/continue-src/gui/src/redux/selectors/selectActiveTools.ts` | 35 | 🟢 Selectors | `selectActiveTools` | Mode-aware tool filtering based on policies and groups |

| `reference/continue-src/gui/src/redux/selectors/selectToolCalls.ts` | 68 | 🟢 Selectors | `selectCurrentToolCalls`, `selectHasCurrentToolCalls`, `selectToolCallsByStatus`, `selectFirstPendingToolCall`, `selectToolCallById`, `selectApplyStateByToolCallId`, `selectPendingToolCalls`, `selectDoneApplyStates` | Tool call state queries and apply state tracking |

| `reference/continue-src/gui/src/redux/selectors/index.ts` | 56 | 🟢 Selectors | `selectSlashCommandComboBoxInputs`, `selectSlashCommands`, `selectSubmenuContextProviders`, `selectDefaultContextProviders`, `selectUseActiveFile` | Slash commands, context providers, and default context configuration |

| `reference/continue-src/gui/src/redux/slices/editState.ts` | 87 | 🟢 Slice | `editStateSlice`, `setReturnToModeAfterEdit`, `setCodeToEdit`, `clearCodeToEdit`, `updateEditStateApplyState`, `setLastNonEditSessionEmpty`, `setPreviousModeEditorContent` | Edit mode state: code to edit, apply state, return mode, editor content |

| `reference/continue-src/gui/src/redux/slices/indexingSlice.ts` | 66 | 🟢 Slice | `indexingSlice`, `updateIndexingStatus`, `setIndexingChatPeekHidden` | Indexing status tracking and chat peek visibility |

| `reference/continue-src/gui/src/redux/slices/profilesSlice.ts` | 151 | 🟢 Slice | `profilesSlice`, `setProfiles`, `setSelectedProfile`, `initializeProfilePreferences`, `bookmarkSlashCommand`, `unbookmarkSlashCommand`, `selectSelectedProfile`, `selectProfiles`, `selectBookmarkedSlashCommands` | Profile management, slash command bookmarks, preferences per profile |

| `reference/continue-src/gui/src/redux/slices/tabsSlice.ts` | 140 | 🟢 Slice | `tabsSlice`, `setTabs`, `updateTab`, `addTab`, `removeTab`, `setActiveTab`, `handleSessionChange` | Chat tabs with session linking and intelligent tab switching |

| `reference/continue-src/gui/src/redux/slices/sessionSlice.ts` | 1097 | 🟢 Slice | `sessionSlice`, `streamUpdate`, `newSession`, `submitEditorAndInitAtIndex`, `setToolGenerated`, `updateToolCallOutput`, `cancelToolCall`, `deleteMessage`, `updateApplyState`, `setMode`, `setIsInEdit`, `setHasReasoningEnabled`, `selectApplyStateByStreamId`, `selectApplyStateByToolCallId` | Core chat session: history, tool calls, streaming, apply states, reasoning |

| `reference/continue-src/gui/src/redux/slices/configSlice.ts` | 109 | 🟢 Slice | `configSlice`, `setConfigResult`, `updateConfig`, `setConfigLoading`, `selectSelectedChatModel`, `selectSelectedChatModelContextLength`, `selectUIConfig` | Configuration state: models, tools, rules, context providers, errors |

| `reference/continue-src/gui/src/redux/slices/uiSlice.ts` | 171 | 🟢 Slice | `uiSlice`, `setToolPolicy`, `toggleToolSetting`, `toggleToolGroupSetting`, `toggleRuleSetting`, `setReasoningSetting`, `setTTSActive`, `setOnboardingCard` | UI settings: tool/rule/reasoning policies, dialogs, onboarding, TTS |

| `reference/continue-src/gui/src/redux/thunks/streamThunkWrapper.tsx` | 61 | 🟡 Thunk | `streamThunkWrapper` | Error handling + retry wrapper for LLM streams (overloaded server retries + error dialog) |

| `reference/continue-src/gui/src/redux/thunks/callToolById.ts` | 150 | 🟡 Thunk | `callToolById` | Execute tool (client-side or via core); set output/error; log usage; stream response |

| `reference/continue-src/gui/src/redux/thunks/preprocessToolCallArgs.ts` | 65 | 🟡 Thunk | `preprocessToolCalls` | Validate tool arguments via core; dispatch error/processed args |

| `reference/continue-src/gui/src/redux/thunks/evaluateToolPolicies.ts` | 121 | 🟡 Thunk | `evaluateToolPolicies` | Dynamic tool policy evaluation per args; enforce policy hierarchy; mark disabled/allowed calls |

| `reference/continue-src/gui/src/redux/thunks/cancelStream.ts` | 18 | 🟡 Thunk | `cancelStream` | Abort LLM stream; clear dangling messages/tools |

| `reference/continue-src/gui/src/redux/thunks/cancelToolCall.ts` | 47 | 🟡 Thunk | `cancelToolCallThunk` | User rejection of tool call; optional message; stream response |

| `reference/continue-src/gui/src/redux/thunks/moveTerminalProcessToBackground.ts` | 82 | 🟡 Thunk | `moveTerminalProcessToBackground` | Preserve terminal output; mark process as backgrounded; continue LLM |

| `reference/continue-src/gui/src/redux/thunks/updateFileSymbols.ts` | 113 | 🟡 Thunk | `updateFileSymbolsFromFiles`, `updateFileSymbolsFromHistory` | Request symbols for file URIs from IDE; cache in state |

| `reference/continue-src/gui/src/redux/thunks/updateSelectedModelByRole.ts` | 58 | 🟡 Thunk | `updateSelectedModelByRole` | Update config with selected model; post to IDE |

| `reference/continue-src/gui/src/redux/thunks/streamNormalInput.ts` | 398 | 🟡 Thunk | `streamNormalInput` | Complete LLM pipeline: build messages, compile, stream, handle tool calls with policy evaluation |

| `reference/continue-src/gui/src/redux/thunks/streamResponseAfterToolCall.ts` | 88 | 🟡 Thunk | `streamResponseAfterToolCall` | Create tool message; check completion; continue stream if all done |

| `reference/continue-src/gui/src/redux/thunks/handleApplyStateUpdate.ts` | 217 | 🟡 Thunk | `handleApplyStateUpdate`, `applyForEditTool` | Track edit/apply states; auto-accept diffs; log outcomes; handle auto-format |

| `reference/continue-src/gui/src/redux/thunks/streamResponse.ts` | 104 | 🟡 Thunk | `streamResponseThunk` | Resolve editor content; submit user message; construct symbols; dispatch stream |

| `reference/continue-src/gui/src/redux/thunks/edit.ts` | 156 | 🟡 Thunk | `streamEditThunk`, `enterEdit`, `exitEdit` | Manage edit mode: enter/exit, resolve content, send to core, restore session |

| `reference/continue-src/gui/src/redux/thunks/session.ts` | 262 | 🟡 Thunk | `saveCurrentSession`, `loadLastSession`, `loadSession`, `selectChatModelForProfile`, `refreshSessionMetadata`, `deleteSession`, `updateSession` | Session lifecycle: load/save, title generation, metadata refresh |

| `reference/continue-src/gui/src/redux/store.ts` | 153 | 🔧 Config | `setupStore`, `RootState`, `AppDispatch`, `AppThunkDispatch`, `ThunkExtrasType`, `ThunkApiType`, `store`, `persistor` | Redux store setup: root reducer combiner, persist config, middleware, type exports |

| `reference/continue-src/gui/src/pages/error.tsx` | 62 | 📄 Page | `ErrorPage` | Global error boundary page; clears localStorage, resets session on recovery |

| `reference/continue-src/gui/src/pages/stats.tsx` | 141 | 📄 Page | `Stats` | Token usage analytics: per-day and per-model telemetry with table export |

| `reference/continue-src/gui/src/pages/history/index.tsx` | 18 | 📄 Page | `HistoryPage` | Session history browser; wraps History component with page header |

| `reference/continue-src/gui/src/pages/config/index.tsx` | 92 | 📄 Page | `ConfigPage` | Settings hub with tabbed sidebar; models, tools, rules, account preferences |

| `reference/continue-src/gui/src/pages/gui/EmptyChatBody.tsx` | 22 | 🧩 Component | `EmptyChatBody` | Conditional render: onboarding card OR conversation starters when chat empty |

| `reference/continue-src/gui/src/pages/gui/ExploreDialogWatcher.tsx` | 40 | 🧩 Component | `ExploreDialogWatcher` | Detects tutorial file closes, shows explore dialog if not dismissed |

| `reference/continue-src/gui/src/pages/gui/StreamError.tsx` | 300 | 🧩 Component | `StreamErrorDialog` | Error modal with status-specific guidance (429/404/401/403); actions to resubmit, check API keys, view config |

| `reference/continue-src/gui/src/pages/gui/useAutoScroll.ts` | 61 | 🪝 Hook | `useAutoScroll` | Auto-scroll to bottom on new user messages; pause on manual scroll up; resume on scroll to bottom |

| `reference/continue-src/gui/src/pages/gui/Chat.tsx` | 453 | 🧩 Component | `Chat` | Main chat UI: message history, input box, tool calls, edit mode, error boundaries, keyboard shortcuts |

| `reference/continue-src/gui/src/pages/gui/index.tsx` | 15 | 📄 Page | `GUI` | Root page layout: two-column (sidebar history + main chat) with responsive hide |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolCallArgs.tsx` | 49 | 🧩 Component | `ArgsToggleIcon`, `ArgsItems` | Toggle button + display for tool call arguments |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolCallDisplay.tsx` | 63 | 🧩 Component | `ToolCallDisplay` | Header with icon, tool favicon, status message, truncate button; clickable context items |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolCallStatusMessage.tsx` | 62 | 🧩 Component | `ToolCallStatusMessage` | Renders status-specific message (will/wants/is/tried) using tool metadata templates |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToolTruncateHistoryIcon.tsx` | 48 | 🧩 Component | `ToolTruncateHistoryIcon` | Truncate-history button (disabled while streaming) |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/ToggleWithIcon.tsx` | 58 | 🧩 Component | `ToggleWithIcon` | Chevron toggle with optional icon; hover-reveal chevron |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/IndicatorBar.tsx` | 19 | 🧩 Component | `IndicatorBar` | Collapsed/expanded state indicator with callout badge |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/TerminalCollapsibleContainer.tsx` | 61 | 🧩 Component | `TerminalCollapsibleContainer` | Collapsible terminal output with gradient fade + indicator bar |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/CreateFile.tsx` | 25 | 🧩 Component | `CreateFile` | Render file creation in styled markdown preview |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/EditFile.tsx` | 34 | 🧩 Component | `EditFile` | Render file changes in collapsible diff preview |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/FindAndReplace.tsx` | 343 | 🧩 Component | `FindAndReplaceDisplay` | Multi-diff viewer with stats, apply/reject buttons, collapsible edits |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/RunTerminalCommand.tsx` | 43 | 🧩 Component | `RunTerminalCommand` | Terminal output wrapper with status (running/completed/failed/background) |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/MCPAppRenderer.tsx` | 425 | 🧩 Component | `MCPAppRenderer` | MCP app iframe with AppBridge, CSP config, sandbox permissions, message routing |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/SimpleToolCallUI.tsx` | 90 | 🧩 Component | `SimpleToolCallUI` | Collapsible tool output with icon toggle + context items peek |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/GroupedToolCallHeader.tsx` | 37 | 🧩 Component | `GroupedToolCallHeader` | Collapsible header for grouped tool calls (Performing/Generating/Pending/Performed) |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/FunctionSpecificToolCallDiv.tsx` | 82 | 🧩 Component | `FunctionSpecificToolCallDiv` | Router to tool-specific renderers (CreateFile, EditFile, FindAndReplace, RunTerminalCommand) |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/utils.tsx` | 109 | 🧩 Utilities | `getStatusIntro`, `getGroupActionVerb`, `getStatusIcon`, `toolCallStateToContextItems` | Tool call rendering helpers: status verbs, icons, context item conversion |

| `reference/continue-src/gui/src/pages/gui/ToolCallDiv/index.tsx` | 137 | 🧩 Component | `ToolCallDiv` | Master tool call renderer: routes to MCP, SimpleUI, or FunctionSpecific; grouped/ungrouped modes |

| `reference/continue-src/gui/src/styles/ThemePage.tsx` | 232 | 🎨 Theme | `ThemePage` | Theme debugger: visual tests for all colors, missing variable detection, JetBrains/VS Code diffs |

| `reference/continue-src/gui/src/components/Layout.tsx` | 234 | 🧩 Component | `Layout` | Root container: listens to IDE events, manages edit mode, dialogs, auth/storage context wrapping |

| `reference/continue-src/gui/src/App.tsx` | 66 | 🚀 App | `App` | Entry point: router setup, provider stack (VscThemeProvider, MainEditorProvider, SubmenuContextProviders) |

| `reference/continue-src/gui/src/main.tsx` | 24 | 🚀 Entry | React DOM mounting, Redux store initialization, PersistGate hydration wrapping |

| `reference/continue-src/gui/index.html` | 13 | 📄 HTML | HTML5 doc: root div mount point, favicon, title, deferred src/main.tsx loader |

| `reference/continue-src/gui/src/util/clientTools/editImpl.ts`

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

| `myersDiff()` | `core/diff/myers.ts` | 29-57 | Function | Diff | Line-level diff using Myers algorithm; handles ignore trailing newline |

| `myersCharDiff()` | `core/diff/myers.ts` | 59-211 | Function | Diff | Character-level diff with line/char index tracking; split by newlines |

| `convertMyersChangeToDiffLines()` | `core/diff/myers.ts` | 5-19 | Function | Diff | Convert jsdiff Change → DiffLine[] (type: old/new/same) |

| `ApplyAbortManager` | `core/edit/applyAbortManager.ts` | 1-37 | Class (Singleton) | Manager | Manage AbortController instances per apply ID |

| `ApplyAbortManager.getInstance()` | `core/edit/applyAbortManager.ts` | 9-14 | Method | Accessor | Get singleton instance |

| `ApplyAbortManager.get(id)` | `core/edit/applyAbortManager.ts` | 16-23 | Method | Accessor | Get or create AbortController for ID |

| `ApplyAbortManager.abort(id)` | `core/edit/applyAbortManager.ts` | 25-31 | Method | Abort | Signal abort + delete controller |

| `streamDiffLines()` | `core/edit/streamDiffLines.ts` | 77-190 | AsyncGenerator | Stream | Render LLM completion → DiffLine stream; apply prompt templates & rules |

| `addIndentation()` | `core/edit/streamDiffLines.ts` | 61-71 | AsyncGenerator | Transform | Add indentation prefix to each DiffLine in stream |

| `PauseToken` | `core/indexing/CodebaseIndexer.ts` | 36-46 | Class | Control | Pause/resume state token for indexing lifecycle |

| `CodebaseIndexer` | `core/indexing/CodebaseIndexer.ts` | 48-872 | Class | Orchestrator | Multi-strategy codebase indexing (chunk, embeddings, FTS, snippets) |

| `CodebaseIndexer.getIndexesToBuild()` | `core/indexing/CodebaseIndexer.ts` | 146-210 | Method | Accessor | Determine which index types to build based on context providers |

| `CodebaseIndexer.refreshCodebaseIndex()` | `core/indexing/CodebaseIndexer.ts` | 724-769 | Method | Orchestrator | Acquire lock, walk dirs, index files, emit progress, release lock |

| `CodebaseIndexer.refreshDirs()` | `core/indexing/CodebaseIndexer.ts` | 334-457 | AsyncGenerator | Stream | Walk dirs, batch indexing with pause/abort/error handling |

| `CodebaseIndexer.refreshFiles()` | `core/indexing/CodebaseIndexer.ts` | 293-332 | AsyncGenerator | Stream | Index individual files and emit progress updates |

| `CodebaseIndexer.indexFiles()` | `core/indexing/CodebaseIndexer.ts` | 552-670 | AsyncGenerator | Stream | Batch index operations per codebase index type; collect warnings |

| `CodebaseIndexer.handleConfigUpdate()` | `core/indexing/CodebaseIndexer.ts` | 840-871 | Method | Handler | Catch embeddings model changes, trigger reindex if needed |

| `DocsService` | `core/indexing/docs/DocsService.ts` | 167-1292 | Class (Singleton) | Orchestrator | Documentation site indexing (crawl, chunk, embed, store in LanceDB+SQLite) |

| `DocsService.indexAndAdd()` | `core/indexing/docs/DocsService.ts` | 435-739 | Method | Orchestrator | Crawl docs, chunk, embed, store; emit progress; handle embeddings provider changes |

| `DocsService.syncDocs()` | `core/indexing/docs/DocsService.ts` | 927-1020 | Method | Handler | On config update: reindex changed docs, add new ones, update metadata |

| `DocsService.retrieveChunks()` | `core/indexing/docs/DocsService.ts` | 824-854 | Method | Query | Vector similarity search in LanceDB; filter by startUrl |

| `DocsService.retrieveChunksFromQuery()` | `core/indexing/docs/DocsService.ts` | 742-763 | Method | Query | Convert text query → embedding → retrieve chunks via retrieveChunks() |

| `DocsService.getEmbeddingsProvider()` | `core/indexing/docs/DocsService.ts` | 344-364 | Method | Accessor | Return config embeddings provider OR default TransformersJs OR undefined |

| `DocsService.delete()` | `core/indexing/docs/DocsService.ts` | 1282-1291 | Method | Delete | Remove from LanceDB, SQLite, config; abort pending indexing |

| `embedModelsAreEqual()` | `core/indexing/docs/DocsService.ts` | 93-102 | Function | Equality | Compare embeddings models by provider/title/chunk-size |

| `LanceDbDocsRow` | `core/indexing/docs/DocsService.ts` | 41-51 | Interface | Type | LanceDB row schema (title, starturl, content, path, vector, line ranges) |

| `MCPManagerSingleton` | `core/context/mcp/MCPManagerSingleton.ts` | 6-204 | Class (Singleton) | Manager | Lifecycle for all MCP connections |

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

| `VscThemeContext` | `gui/src/context/VscTheme.tsx` | 159-161 | Context | Provider | React Context with `theme: Record<string, string>` (hljs className → color hex) |

| `VscThemeProvider` | `gui/src/context/VscTheme.tsx` | 163-182 | Component | Provider | Context provider that listens for "setTheme" webview messages and updates theme state |

| `useVscTheme()` | `gui/src/context/VscTheme.tsx` | 184 | Hook | Provider | Hook to access `{ theme }` from VscThemeContext |

| `constructTheme()` | `gui/src/context/VscTheme.tsx` | 56-85 | Function | Provider | Map VSCode TextMate theme rules to hljs CSS class colors; fallback to light/dark theme |

| `fallbackTheme()` | `gui/src/context/VscTheme.tsx` | 87-150 | Function | Provider | Fallback to light (avg≥128) or dark theme based on editor background luminance |

| `LocalStorageContext` | `gui/src/context/LocalStorage.tsx` | 12-14 | Context | Provider | React Context with `fontSize: number` |

| `LocalStorageProvider` | `gui/src/context/LocalStorage.tsx` | 16-63 | Component | Provider | Context provider syncing fontSize from localStorage; listens for "localStorageChange" custom events |

| `useLocalStorage()` | `gui/src/context/LocalStorage.tsx` | 65-68 | Hook | Provider | Hook to access `{ fontSize }` from LocalStorageContext |

| `AuthContext` | `gui/src/context/Auth.tsx` | 17 | Context | Provider | React Context with `selectedProfile`, `profiles`, `refreshProfiles()` |

| `AuthProvider` | `gui/src/context/Auth.tsx` | 19-58 | Component | Provider | Context provider wrapping profile selection and refresh logic |

| `useAuth()` | `gui/src/context/Auth.tsx` | 60-66 | Hook | Provider | Hook to access auth context; throws if not within AuthProvider |

| `refreshProfiles()` | `gui/src/context/Auth.tsx` | 29-45 | Function | Provider | Call core `config/refreshProfiles`, show status toast, manage loading state |

| `SubmenuContextProvidersContext` | `gui/src/context/SubmenuContextProviders.tsx` | 51-52 | Context | Provider | React Context with `getSubmenuContextItems(providerTitle?, query)` |

| `SubmenuContextProvidersProvider` | `gui/src/context/SubmenuContextProviders.tsx` | 139-625 | Component | Provider | Context provider with MiniSearch indices, file polling (2s interval), intelligent sorting |

| `useSubmenuContextProviders()` | `gui/src/context/SubmenuContextProviders.tsx` | 627-628 | Hook | Provider | Hook to access `{ getSubmenuContextItems }` from SubmenuContextProvidersContext |

| `getSubmenuContextItems()` | `gui/src/context/SubmenuContextProviders.tsx` | 316-465 | Function | Provider | Search context items by provider/query; multi-criteria sort (priority, match quality, score, path length) |

| `calculateFileSortPriority()` | `gui/src/context/SubmenuContextProviders.tsx` | 224-278 | Function | Provider | 9-tier file sort priority (exact match, recent, starts with, word match, common files, common dirs, camelCase, path starts with, default) |

| `calculateMatchQuality()` | `gui/src/context/SubmenuContextProviders.tsx` | 280-314 | Function | Provider | Quality score: exact (100), prefix (50), contains (25), short name, dev file extensions |

| `loadSubmenuItems()` | `gui/src/context/SubmenuContextProviders.tsx` | 467-588 | Function | Provider | Load items for provider(s) via core, deduplicate, build MiniSearch indices, update fallback results |

| `hasExactWordMatch()` | `gui/src/context/SubmenuContextProviders.tsx` | 55-58 | Function | Provider | Check if text has exact word match for query (token boundary) |

| `isCommonDevFile()` | `gui/src/context/SubmenuContextProviders.tsx` | 60-92 | Function | Provider | Check if file has common dev extension or name (index, main, app, component, etc.) |

| `isInCommonDirectory()` | `gui/src/context/SubmenuContextProviders.tsx` | 94-112 | Function | Provider | Check if file path includes common directory (src, lib, components, utils, etc.) |

| `matchesCamelCaseOrAbbreviation()` | `gui/src/context/SubmenuContextProviders.tsx` | 114-137 | Function | Provider | Match query via camelCase capitals or word-initial abbreviation |

| `IIdeMessenger` | `gui/src/context/IdeMessenger.tsx` | 26-60 | Interface | Provider | IDE messenger interface: post(), respond(), request(), streamRequest(), llmStreamChat(), ide property |

| `IdeMessenger` | `gui/src/context/IdeMessenger.tsx` | 62-262 | Class | Provider | Implementation of IIdeMessenger; routes messages to VSCode/JetBrains, handles retries (exponential backoff), streams protocol |

| `IdeMessengerContext` | `gui/src/context/IdeMessenger.tsx` | 264-266 | Context | Provider | React Context wrapping IIdeMessenger instance |

| `IdeMessengerProvider` | `gui/src/context/IdeMessenger.tsx` | 268-277 | Component | Provider | Context provider that wraps children with IdeMessenger context |

| `_postToIde()` | `gui/src/context/IdeMessenger.tsx` | 78-112 | Method | Provider | Send message to VSCode (via vscode.postMessage) or JetBrains (via postIntellijMessage) |

| `post()` | `gui/src/context/IdeMessenger.tsx` | 114-136 | Method | Provider | One-way message send with retry logic (5 attempts, exponential backoff) |

| `respond()` | `gui/src/context/IdeMessenger.tsx` | 138-144 | Method | Provider | Respond to a specific IDE request message using messageId |

| `request()` | `gui/src/context/IdeMessenger.tsx` | 146-163 | Method | Provider | Async request: send, wait for response with matching messageId, return promise |

| `streamRequest()` | `gui/src/context/IdeMessenger.tsx` | 173-247 | Method | Provider | Async generator streaming requests; yields buffered chunks, supports AbortSignal cancellation |

| `llmStreamChat()` | `gui/src/context/IdeMessenger.tsx` | 249-261 | Method | Provider | Wrapper over streamRequest("llm/streamChat"); yields ChatMessage arrays, returns PromptLog |

| `OnboardingCard` | `gui/src/components/OnboardingCard/OnboardingCard.tsx` | 20-57 | Component | Component | Tabs for LLM provider setup (API key vs local); reads/writes onboarding status to localStorage |

| `OnboardingCardState` | `gui/src/components/OnboardingCard/OnboardingCard.tsx` | 11-14 | Type | Component | `{ show?, activeTab? }` state for onboarding card visibility/tab selection |

| `OSRContextMenu` | `gui/src/components/OSRContextMenu.tsx` | 13-220 | Component | Component | Context menu on right-click (Windows/Linux only); copy/cut/paste/dev-tools with smart positioning |

| `FatalErrorIndicator` | `gui/src/components/config/FatalErrorNotice.tsx` | 9-75 | Component | Component | Shows error alert if config has fatal errors; links to help docs and reload/view config buttons |

| `TextDialog` | `gui/src/components/dialogs/index.tsx` | 43-79 | Component | Component | Modal with semitransparent backdrop, centered container, Markdown/JSX rendering, Esc key handler |

| `TipTapEditor` | `gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx` | 343-349 | Component | Component | Rich text editor wrapper (memo-wrapped TipTapEditorInner) |

| `TipTapEditorInner` | `gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx` | 48-314 | Component | Component | Tiptap editor with image drag-drop, toolbar, focus/blur, keyboard handlers, streaming state |

| `TipTapEditorProps` | `gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx` | 27-44 | Type | Component | Editor props: availableContextProviders, availableSlashCommands, isMainInput, onEnter, editorState, toolbarOptions, placeholder, historyKey, inputId |

| `createEditorConfig()` | `gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx` | 60-64 | Function | Component | (imported from utils) Create Tiptap editor config with extensions and handlers |

| `useEditorEventHandlers()` | `gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx` | 168-173 | Hook | Component | (imported from utils) Return handleKeyUp, handleKeyDown for editor keyboard/autocomplete behavior |

| `handleImageFile()` | `gui/src/components/mainInput/TipTapEditor/TipTapEditor.tsx` | 252, 284 | Function | Component | (imported from utils) Process dropped/selected image file; return [fileName, dataUrl] |

| `useWebviewListener()` | `gui/src/hooks/useWebviewListener.ts` | 6-39 | Hook | Hook | Generic webview message listener; set up window message handler, auto-respond, cleanup on unmount |

| `ParallelListeners` | `gui/src/hooks/ParallelListeners.tsx` | 37-264 | Component | Hook | Master global event listener; coordinates all webview message handlers + Redux dispatch |

| `handleConfigUpdate()` | `gui/src/hooks/ParallelListeners.tsx` | 55-102 | Function | Hook | Process config updates from IDE; dispatch profiles, config, reasoning settings to Redux |

| `useAppDispatch()` | `gui/src/redux/hooks.ts` | 4 | Hook | Utils | Typed Redux dispatch hook (returns AppDispatch type) |

| `useAppSelector()` | `gui/src/redux/hooks.ts` | 5 | Hook | Utils | Typed Redux selector hook (accepts RootState type parameter) |

| `getBaseSystemMessage()` | `gui/src/redux/util/getBaseSystemMessage.ts` | 11-32 | Function | Utils | Select base system message by mode (agent/plan/chat); append NO_TOOL_WARNING if no tools |

| `NO_TOOL_WARNING` | `gui/src/redux/util/getBaseSystemMessage.ts` | 8-9 | Constant | Utils | Text warning: "THE USER HAS NOT PROVIDED ANY TOOLS..." (appended when agent/plan mode has no tools) |

| `constructMessages()` | `gui/src/redux/util/constructMessages.ts` | 37-230 | Function | Utils | Build LLM message array from history; filter by conversation summary; apply rules; convert tool calls to text if system-tools framework; return { messages, appliedRules, appliedRuleIndex } |

| `MessageWithContextItems` | `gui/src/redux/util/constructMessages.ts` | 33-36 | Type | Utils | `{ ctxItems: ContextItemWithId[], message: ChatMessage }` - helper for constructing messages |

| `hasCurrentToolCalls()` | `gui/src/redux/util/index.ts` | 12-16 | Function | Utils | Check if most recent assistant message has tool call states |

| `findAllCurToolCalls()` | `gui/src/redux/util/index.ts` | 40-64 | Function | Utils | Get all tool call states from most recent assistant message (scanning backward; stop at user message) |

| `findAllCurToolCallsByStatus()` | `gui/src/redux/util/index.ts` | 25-32 | Function | Utils | Get tool call states with specific status (pending, executing, succeeded, canceled, errored) |

| `findToolCallById()` | `gui/src/redux/util/index.ts` | 73-90 | Function | Utils | Find tool call state anywhere in history by toolCallId (reverse scan) |

| `findChatHistoryItemByToolCallId()` | `gui/src/redux/util/index.ts` | 92-100 | Function | Utils | Find chat history item (tool role) with matching toolCallId |

| `logToolUsage()` | `gui/src/redux/util/index.ts` | 102-126 | Function | Utils | Post devdata/log message with tool call details (function name, params, args, accepted, output, success status) |

| `selectActiveTools` | `gui/src/redux/selectors/selectActiveTools.ts` | 7-35 | Selector | Selectors | Mode-aware filter: chat→empty, plan→readonly+built-in only, agent→all enabled tools (based on policies + group settings) |

| `selectCurrentToolCalls` | `gui/src/redux/selectors/selectToolCalls.ts` | 12-15 | Selector | Selectors | Get all current tool calls from history via `findAllCurToolCalls()` |

| `selectHasCurrentToolCalls` | `gui/src/redux/selectors/selectToolCalls.ts` | 17-20 | Selector | Selectors | Check if current tool calls exist via `hasCurrentToolCalls()` |

| `selectToolCallsByStatus` | `gui/src/redux/selectors/selectToolCalls.ts` | 22-28 | Selector | Selectors | Filter current tool calls by status (pending/executing/succeeded/canceled/errored) |

| `selectFirstPendingToolCall` | `gui/src/redux/selectors/selectToolCalls.ts` | 30-36 | Selector | Selectors | Get first tool call with "generated" status; return undefined if none |

| `selectToolCallById` | `gui/src/redux/selectors/selectToolCalls.ts` | 39-45 | Selector | Selectors | Find specific tool call by ID via `findToolCallById()` |

| `selectApplyStateByToolCallId` | `gui/src/redux/selectors/selectToolCalls.ts` | 47-57 | Selector | Selectors | Find most recent apply state for tool call (from `codeBlockApplyStates.states`) |

| `selectPendingToolCalls` | `gui/src/redux/selectors/selectToolCalls.ts` | 60-63 | Selector | Selectors | Convenience selector for tool calls with "generated" status |

| `selectDoneApplyStates` | `gui/src/redux/selectors/selectToolCalls.ts` | 65-68 | Selector | Selectors | Filter apply states where status === "done" |

| `selectSlashCommandComboBoxInputs` | `gui/src/redux/selectors/index.ts` | 8-30 | Selector | Selectors | Transform slash commands → ComboBoxItems (title, description, content, source); handle MCP load failures |

| `selectSlashCommands` | `gui/src/redux/selectors/index.ts` | 32-37 | Selector | Selectors | Return slash commands array (empty fallback) |

| `selectSubmenuContextProviders` | `gui/src/redux/selectors/index.ts` | 39-44 | Selector | Selectors | Filter context providers by type === "submenu" |

| `selectDefaultContextProviders` | `gui/src/redux/selectors/index.ts` | 46-51 | Selector | Selectors | Extract default context providers from config.experimental.defaultContext |

| `selectUseActiveFile` | `gui/src/redux/selectors/index.ts` | 53-56 | Selector | Selectors | Check if "activeFile" in default context (boolean) |

| `EditState` | `gui/src/redux/slices/editState.ts` | 6-14 | Type | Slice | `{ codeToEdit: SetCodeToEditPayload[], applyState: ApplyState, returnToMode: MessageModes, lastNonEditSessionWasEmpty: boolean, previousModeEditorContent?: JSONContent }` |

| `INITIAL_EDIT_STATE` | `gui/src/redux/slices/editState.ts` | 21-27 | Constant | Slice | Default edit state with empty code, not-started apply state, chat mode |

| `setReturnToModeAfterEdit` | `gui/src/redux/slices/editState.ts` | 33-38 | Action | Slice | Set message mode to return to after exiting edit mode |

| `setCodeToEdit` | `gui/src/redux/slices/editState.ts` | 48-59 | Action | Slice | Set code to edit (normalizes single item or array) |

| `clearCodeToEdit` | `gui/src/redux/slices/editState.ts` | 60-62 | Action | Slice | Clear code to edit array |

| `updateEditStateApplyState` | `gui/src/redux/slices/editState.ts` | 39-47 | Action | Slice | Merge apply state updates into current edit apply state |

| `setLastNonEditSessionEmpty` | `gui/src/redux/slices/editState.ts` | 63-68 | Action | Slice | Track if non-edit session was empty on exit |

| `setPreviousModeEditorContent` | `gui/src/redux/slices/editState.ts` | 69-74 | Action | Slice | Save editor content before entering edit mode |

| `IndexingState` | `gui/src/redux/slices/indexingSlice.ts` | 4-9 | Type | Slice | `{ indexing: { statuses: Record<string, IndexingStatus>, hiddenChatPeekTypes: Record<IndexingStatus["type"], boolean> } }` |

| `updateIndexingStatus` | `gui/src/redux/slices/indexingSlice.ts` | 24-45 | Action | Slice | Update indexing status by ID; auto-unhide peek when all indexing of type complete |

| `setIndexingChatPeekHidden` | `gui/src/redux/slices/indexingSlice.ts` | 46-59 | Action | Slice | Toggle chat peek visibility for indexing type (docs, etc.) |

| `ProfilesState` | `gui/src/redux/slices/profilesSlice.ts` | 15-19 | Type | Slice | `{ profiles: ProfileDescription[], selectedProfileId: string | null, preferencesByProfileId: Record<string, PreferencesState> }` |

| `PreferencesState` | `gui/src/redux/slices/profilesSlice.ts` | 11-13 | Type | Slice | `{ bookmarkedSlashCommands: string[] }` |

| `setProfiles` | `gui/src/redux/slices/profilesSlice.ts` | 47-49 | Action | Slice | Set array of available profiles |

| `setSelectedProfile` | `gui/src/redux/slices/profilesSlice.ts` | 44-46 | Action | Slice | Set currently selected profile ID |

| `initializeProfilePreferences` | `gui/src/redux/slices/profilesSlice.ts` | 50-80 | Action | Slice | Initialize preferences for profile (backfill old preferences, bookmark default commands) |

| `bookmarkSlashCommand` | `gui/src/redux/slices/profilesSlice.ts` | 81-94 | Action | Slice | Add slash command name to bookmarks (no duplicates) |

| `unbookmarkSlashCommand` | `gui/src/redux/slices/profilesSlice.ts` | 95-111 | Action | Slice | Remove slash command name from bookmarks |

| `selectSelectedProfile` | `gui/src/redux/slices/profilesSlice.ts` | 114-120 | Selector | Slice | Find current profile by selectedProfileId |

| `selectProfiles` | `gui/src/redux/slices/profilesSlice.ts` | 122-124 | Selector | Slice | Get all profiles array |

| `selectBookmarkedSlashCommands` | `gui/src/redux/slices/profilesSlice.ts` | 126-130 | Selector | Slice | Get bookmarked slash commands for current profile |

| `Tab` | `gui/src/redux/slices/tabsSlice.ts` | 3-8 | Type | Slice | `{ id: string, title: string, isActive: boolean, sessionId?: string }` |

| `setTabs` | `gui/src/redux/slices/tabsSlice.ts` | 28-30 | Action | Slice | Replace entire tabs array |

| `updateTab` | `gui/src/redux/slices/tabsSlice.ts` | 31-39 | Action | Slice | Merge partial updates into tab by ID |

| `addTab` | `gui/src/redux/slices/tabsSlice.ts` | 40-47 | Action | Slice | Add new tab; deactivate others if new tab is active |

| `removeTab` | `gui/src/redux/slices/tabsSlice.ts` | 48-50 | Action | Slice | Remove tab by ID |

| `setActiveTab` | `gui/src/redux/slices/tabsSlice.ts` | 51-56 | Action | Slice | Set active tab; deactivate others |

| `handleSessionChange` | `gui/src/redux/slices/tabsSlice.ts` | 57-127 | Action | Slice | Smart session→tab matching: reuse existing tab, create new, or relink if no-session tab |

| `SessionState` | `gui/src/redux/slices/sessionSlice.ts` | 203-226 | Type | Slice | Comprehensive session dict: history, streaming, mode, symbols, apply states, reasoning, metadata |

| `ChatHistoryItemWithMessageId` | `gui/src/redux/slices/sessionSlice.ts` | 199-201 | Type | Slice | `ChatHistoryItem & { message: ChatMessage & { id: string } }` - adds UUID to message |

| `handleToolCallsInMessage` | `gui/src/redux/slices/sessionSlice.ts` | 77-103 | Function | Slice | Initialize tool call states from message; filter duplicate edit tools |

| `handleStreamingToolCallUpdates` | `gui/src/redux/slices/sessionSlice.ts` | 166-195 | Function | Slice | Apply tool call deltas during streaming; match by ID or update most recent |

| `streamUpdate` | `gui/src/redux/slices/sessionSlice.ts` | 524-684 | Action | Slice | Process streamed message chunks; accumulate content, handle thinking tags, update tool calls |

| `newSession` | `gui/src/redux/slices/sessionSlice.ts` | 686-711 | Action | Slice | Create new or load existing session (save old ID, reset history/symbols/apply states) |

| `submitEditorAndInitAtIndex` | `gui/src/redux/slices/sessionSlice.ts` | 357-413 | Action | Slice | Submit user input; truncate history and append empty assistant message for streaming |

| `truncateHistoryToMessage` | `gui/src/redux/slices/sessionSlice.ts` | 414-438 | Action | Slice | Truncate history to message; reset apply state index and error messages |

| `deleteMessage` | `gui/src/redux/slices/sessionSlice.ts` | 439-445 | Action | Slice | Delete user+assistant pair; reset error state |

| `deleteCompaction` | `gui/src/redux/slices/sessionSlice.ts` | 446-455 | Action | Slice | Remove conversation summary from message |

| `updateHistoryItemAtIndex` | `gui/src/redux/slices/sessionSlice.ts` | 456-477 | Action | Slice | Merge partial updates into history item |

| `setAppliedRulesAtIndex` | `gui/src/redux/slices/sessionSlice.ts` | 498-510 | Action | Slice | Set applied rules metadata for message |

| `addContextItemsAtIndex` | `gui/src/redux/slices/sessionSlice.ts` | 478-497 | Action | Slice | Append context items to message's context list |

| `addHighlightedCode` | `gui/src/redux/slices/sessionSlice.ts` | 764-806 | Action | Slice | Add selected code range as context item with file/line info |

| `setToolGenerated` | `gui/src/redux/slices/sessionSlice.ts` | 832-854 | Action | Slice | Mark tool call as "generated"; set tool from available tools |

| `updateToolCallOutput` | `gui/src/redux/slices/sessionSlice.ts` | 855-884 | Action | Slice | Set tool output context items; update corresponding tool message |

| `setProcessedToolCallArgs` | `gui/src/redux/slices/sessionSlice.ts` | 885-899 | Action | Slice | Store processed arguments for tool call (e.g., post-validation) |

| `cancelToolCall` | `gui/src/redux/slices/sessionSlice.ts` | 900-913 | Action | Slice | Set tool call status to "canceled" |

| `errorToolCall` | `gui/src/redux/slices/sessionSlice.ts` | 914-931 | Action | Slice | Set tool call status to "errored"; optionally set output |

| `acceptToolCall` | `gui/src/redux/slices/sessionSlice.ts` | 932-945 | Action | Slice | Set tool call status to "done" |

| `setToolCallCalling` | `gui/src/redux/slices/sessionSlice.ts` | 946-959 | Action | Slice | Set tool call status to "calling" |

| `updateApplyState` | `gui/src/redux/slices/sessionSlice.ts` | 807-826 | Action | Slice | Insert or merge apply state; increment curIndex on "done" |

| `resetNextCodeBlockToApplyIndex` | `gui/src/redux/slices/sessionSlice.ts` | 827-829 | Action | Slice | Reset apply state cursor to 0 |

| `setMode` | `gui/src/redux/slices/sessionSlice.ts` | 960-962 | Action | Slice | Set message mode (chat/agent/plan) |

| `setIsInEdit` | `gui/src/redux/slices/sessionSlice.ts` | 963-965 | Action | Slice | Set whether currently in edit mode |

| `setHasReasoningEnabled` | `gui/src/redux/slices/sessionSlice.ts` | 966-968 | Action | Slice | Set reasoning capability flag |

| `selectApplyStateByStreamId` | `gui/src/redux/slices/sessionSlice.ts` | 1028-1036 | Selector | Slice | Find apply state by streamId (edit mode, etc.) |

| `selectApplyStateByToolCallId` | `gui/src/redux/slices/sessionSlice.ts` | 1038-1048 | Selector | Slice | Find apply state by tool call ID |

| `ConfigState` | `gui/src/redux/slices/configSlice.ts` | 6-10 | Type | Slice | `{ configError?: ConfigValidationError[], config: BrowserSerializedContinueConfig, loading: boolean }` |

| `EMPTY_CONFIG` | `gui/src/redux/slices/configSlice.ts` | 12-38 | Constant | Slice | Default config with empty arrays for commands/providers/tools/models |

| `setConfigResult` | `gui/src/redux/slices/configSlice.ts` | 50-73 | Action | Slice | Load config+ validation errors from ConfigResult; invalidate on error |

| `updateConfig` | `gui/src/redux/slices/configSlice.ts` | 74-79 | Action | Slice | Replace config state |

| `setConfigLoading` | `gui/src/redux/slices/configSlice.ts` | 80-82 | Action | Slice | Set config loading state |

| `selectSelectedChatModel` | `gui/src/redux/slices/configSlice.ts` | 91-93 | Selector | Slice | Get selected chat model from config |

| `selectSelectedChatModelContextLength` | `gui/src/redux/slices/configSlice.ts` | 85-90 | Selector | Slice | Get chat model context length or DEFAULT_CONTEXT_LENGTH |

| `selectUIConfig` | `gui/src/redux/slices/configSlice.ts` | 94-96 | Selector | Slice | Get UI config object (or null) |

| `UIState` | `gui/src/redux/slices/uiSlice.ts` | 20-32 | Type | Slice | Dialog, onboarding, explore, file editing, tool/rule/reasoning settings, TTS |

| `RulePolicy` | `gui/src/redux/slices/uiSlice.ts` | 11 | Type | Slice | `"on" | "off"` - rule enable/disable setting |

| `ToolGroupPolicy` | `gui/src/redux/slices/uiSlice.ts` | 13 | Type | Slice | `"include" | "exclude"` - tool group membership setting |

| `DEFAULT_TOOL_SETTING` | `gui/src/redux/slices/uiSlice.ts` | 34 | Constant | Slice | `"allowedWithPermission"` - default tool policy |

| `DEFAULT_RULE_SETTING` | `gui/src/redux/slices/uiSlice.ts` | 35 | Constant | Slice | `"on"` - default rule policy |

| `addTool` | `gui/src/redux/slices/uiSlice.ts` | 80-83 | Action | Slice | Add tool to settings with default policy |

| `setToolPolicy` | `gui/src/redux/slices/uiSlice.ts` | 84-92 | Action | Slice | Set tool policy (allowedWithPermission/allowedWithoutPermission/disabled) |

| `clearToolPolicy` | `gui/src/redux/slices/uiSlice.ts` | 93-95 | Action | Slice | Delete tool setting (revert to default) |

| `toggleToolSetting` | `gui/src/redux/slices/uiSlice.ts` | 96-113 | Action | Slice | Cycle tool policy: allowedWithPermission → allowedWithoutPermission → disabled → allowedWithPermission |

| `toggleToolGroupSetting` | `gui/src/redux/slices/uiSlice.ts` | 114-122 | Action | Slice | Toggle tool group: include ↔ exclude |

| `addRule` | `gui/src/redux/slices/uiSlice.ts` | 124-126 | Action | Slice | Add rule setting with default "on" |

| `toggleRuleSetting` | `gui/src/redux/slices/uiSlice.ts` | 127-141 | Action | Slice | Toggle rule policy: on ↔ off |

| `setTTSActive` | `gui/src/redux/slices/uiSlice.ts` | 142-144 | Action | Slice | Set text-to-speech active state |

| `setReasoningSetting` | `gui/src/redux/slices/uiSlice.ts` | 145-151 | Action | Slice | Enable/disable reasoning for model |

| `setOnboardingCard` | `gui/src/redux/slices/uiSlice.ts` | 58-63 | Action | Slice | Merge partial onboarding state |

| `setDialogMessage` | `gui/src/redux/slices/uiSlice.ts` | 64-69 | Action | Slice | Set dialog content (JSX element) |

| `setShowDialog` | `gui/src/redux/slices/uiSlice.ts` | 70-72 | Action | Slice | Show/hide dialog |

| `setIsExploreDialogOpen` | `gui/src/redux/slices/uiSlice.ts` | 73-78 | Action | Slice | Show/hide explore samples dialog |

| `streamThunkWrapper` | `gui/src/redux/thunks/streamThunkWrapper.tsx` | 20-61 | Thunk | Thunk | Wrap stream thunk; retry on "overloaded" errors up to 3x with exponential backoff; show error dialog |

| `callToolById` | `gui/src/redux/thunks/callToolById.ts` | 19-150 | Thunk | Thunk | Execute tool call; route to client/core; log output; log usage; stream response if needed |

| `preprocessToolCalls` | `gui/src/redux/thunks/preprocessToolCallArgs.ts` | 12-65 | Function | Thunk | Validate tool args via core; dispatch error/processed args for all pending tool calls |

| `evaluateToolPolicies` | `gui/src/redux/thunks/evaluateToolPolicies.ts` | 74-121 | Function | Thunk | Evaluate dynamic tool policies; enforce policy hierarchy (disabled > permission > no-permission); return evaluated policies |

| `cancelStream` | `gui/src/redux/thunks/cancelStream.ts` | 9-18 | Thunk | Thunk | Abort stream; clear dangling messages and incomplete tool calls |

| `cancelToolCallThunk` | `gui/src/redux/thunks/cancelToolCall.ts` | 16-47 | Thunk | Thunk | User rejects tool call; add rejection message if continueAfterToolRejection enabled; stream response |

| `moveTerminalProcessToBackground` | `gui/src/redux/thunks/moveTerminalProcessToBackground.ts` | 20-82 | Thunk | Thunk | Preserve terminal output; abort stream; mark as backgrounded; accept tool; stream response |

| `updateFileSymbolsFromFiles` | `gui/src/redux/thunks/updateFileSymbols.ts` | 42-68 | Thunk | Thunk | Request symbols for specific file paths; dedup; dispatch to state |

| `updateFileSymbolsFromHistory` | `gui/src/redux/thunks/updateFileSymbols.ts` | 74-113 | Thunk | Thunk | Extract file URIs from history context items; skip existing; request symbols |

| `getContextItemsFromHistory` | `gui/src/redux/thunks/updateFileSymbols.ts` | 7-36 | Function | Thunk | Extract file context items from history (normal + toolbar code blocks) |

| `updateSelectedModelByRole` | `gui/src/redux/thunks/updateSelectedModelByRole.ts` | 7-58 | Thunk | Thunk | Update config with selected model; post to IDE for persistence |

| `streamNormalInput` | `gui/src/redux/thunks/streamNormalInput.ts` | 72-398 | Thunk | Thunk | Complete LLM chat pipeline: compile messages, stream, track tool calls, evaluate policies, execute/wait for approval |

| `buildReasoningCompletionOptions` | `gui/src/redux/thunks/streamNormalInput.ts` | 48-70 | Function | Thunk | Extend completion options with reasoning config (enable/budget) |

| `streamResponseAfterToolCall` | `gui/src/redux/thunks/streamResponseAfterToolCall.ts` | 37-88 | Thunk | Thunk | Create tool message; check if all tools done; continue stream if so |

| `areAllToolsDoneStreaming` | `gui/src/redux/thunks/streamResponseAfterToolCall.ts` | 17-35 | Function | Thunk | Check if all tool calls completed (done/errored/canceled) |

| `handleApplyStateUpdate` | `gui/src/redux/thunks/handleApplyStateUpdate.ts` | 21-153 | Thunk | Thunk | Track edit mode or chat apply states; auto-accept diffs; log outcomes; continue stream |

| `applyForEditTool` | `gui/src/redux/thunks/handleApplyStateUpdate.ts` | 155-217 | Thunk | Thunk | Apply code changes to file via IDE; handle errors; add auto-format context |

| `streamResponseThunk` | `gui/src/redux/thunks/streamResponse.ts` | 18-104 | Thunk | Thunk | Resolve editor content (context + slash commands); submit message; get symbols; dispatch stream |

| `streamEditThunk` | `gui/src/redux/thunks/edit.ts` | 29-74 | Thunk | Thunk | Resolve editor content; send prompt to IDE; set inactive |

| `enterEdit` | `gui/src/redux/thunks/edit.ts` | 118-156 | Thunk | Thunk | Enter edit mode: save Editor content, set mode, dispatch new session |

| `exitEdit` | `gui/src/redux/thunks/edit.ts` | 76-116 | Thunk | Thunk | Exit edit mode: restore session, editor content, mode; optionally load last session |

| `saveCurrentSession` | `gui/src/redux/thunks/session.ts` | 186-262 | Thunk | Thunk | Save session with title generation; optionally open new session |

| `loadLastSession` | `gui/src/redux/thunks/session.ts` | 140-170 | Thunk | Thunk | Load previous session with retry logic; restore model selection |

| `loadSession` | `gui/src/redux/thunks/session.ts` | 87-114 | Thunk | Thunk | Load specific session; optionally save current; restore model |

| `selectChatModelForProfile` | `gui/src/redux/thunks/session.ts` | 116-138 | Thunk | Thunk | Dispatch updateSelectedModelByRole from session chat model title |

| `refreshSessionMetadata` | `gui/src/redux/thunks/session.ts` | 34-52 | Thunk | Thunk | Request session list from IDE; update Redux state |

| `deleteSession` | `gui/src/redux/thunks/session.ts` | 54-68 | Thunk | Thunk | Delete session optimistically; load last if current; refresh metadata |

| `updateSession` | `gui/src/redux/thunks/session.ts` | 70-82 | Thunk | Thunk | Save session to IDE; optimistic metadata update; refresh list |

| `getSession` | `gui/src/redux/thunks/session.ts` | 23-32 | Function | Thunk | Request specific session from IDE history |

| `getChatTitleFromMessage` | `gui/src/redux/thunks/session.ts` | 172-184 | Function | Thunk | Extract title from last non-empty line of chat message (max 100 chars) |

| `setupStore` | `gui/src/redux/store.ts` | 106-131 | Function | Config | Initialize Redux store with persisted reducers, middleware, thunk extras, and logger |

| `RootState` | `gui/src/redux/store.ts` | 149 | Type | Config | Return type of rootReducer; used by all selectors and thunks |

| `AppDispatch` | `gui/src/redux/store.ts` | 151 | Type | Config | Store dispatch type for actions; used in hooks (useAppDispatch) |

| `AppThunkDispatch` | `gui/src/redux/store.ts` | 141-145 | Type | Config | Thunk dispatch type with state/extra/action generics; used in async thunks |

| `ThunkExtrasType` | `gui/src/redux/store.ts` | 134 | Type | Config | Shape of extra argument passed to thunks (IdeMessenger) |

| `ThunkApiType` | `gui/src/redux/store.ts` | 136-139 | Type | Config | Thunk API config: state + extra + dispatch |

| `persistor` | `gui/src/redux/store.ts` | 153 | Export | Config | redux-persist persistor instance; used with PersistGate wrapper |

| `rootReducer` | `gui/src/redux/store.ts` | 26-34 | Constant | Config | Combined reducer from all slices (session, ui, editModeState, config, indexing, tabs, profiles) |

| `persistConfig` | `gui/src/redux/store.ts` | 92-99 | Constant | Config | redux-persist configuration: version, storage, transforms, migration manifest |

| `migrations` | `gui/src/redux/store.ts` | 66-90 | Constant | Config | State migration manifest (v0: old sessionId → new session.id) |

| `saveSubsetFilters` | `gui/src/redux/store.ts` | 36-64 | Constant | Config | redux-persist-transform-filter array; specifies which slices/fields to persist |

| `ErrorPage` | `gui/src/pages/error.tsx` | 11-62 | Component | Page | Error boundary fallback page; clears persist cache, resets session, navigates to home |

| `Stats` | `gui/src/pages/stats.tsx` | 36-141 | Component | Page | Token usage analytics: fetch/display per-day and per-model token counts from IDE |

| `HistoryPage` | `gui/src/pages/history/index.tsx` | 6-18 | Component | Page | Session history page wrapper; renders History component with page header |

| `ConfigPage` | `gui/src/pages/config/index.tsx` | 11-92 | Component | Page | Settings hub: tab-based sidebar with top/bottom sections; responsive desktop/mobile layout |

| `EmptyChatBody` | `gui/src/pages/gui/EmptyChatBody.tsx` | 8-22 | Component | GUI | Render onboarding card (if first visit) OR conversation starter cards (empty chat) |

| `ExploreDialogWatcher` | `gui/src/pages/gui/ExploreDialogWatcher.tsx` | 29-40 | Component | GUI | Listener for tutorial file closes; dispatch `setIsExploreDialogOpen` if tutorial dismissed |

| `StreamErrorDialog` | `gui/src/pages/gui/StreamError.tsx` | 26-300 | Component | GUI | Multi-status error modal with resubmit/check-keys/view-config actions; custom messages per status code |

| `useAutoScroll` | `gui/src/pages/gui/useAutoScroll.ts` | 12-61 | Hook | GUI | Detect new user messages, auto-scroll to bottom, pause on manual scroll up, resume on scroll to bottom |

| `Chat` | `gui/src/pages/gui/Chat.tsx` | 106-453 | Component | GUI | Main chat interface: history rendering, input box, tool calls, edit mode, keyboard handlers, error boundary |

| `GUI` | `gui/src/pages/gui/index.tsx` | 4-15 | Component | Page | Root page layout: sidebar (History) + main (Chat) with responsive hide on small screens |

| `ArgsToggleIcon` | `gui/src/pages/gui/ToolCallDiv/ToolCallArgs.tsx` | 9-23 | Component | Tool | Toggle button to show/hide tool call arguments |

| `ArgsItems` | `gui/src/pages/gui/ToolCallDiv/ToolCallArgs.tsx` | 30-49 | Component | Tool | Display tool arguments as key-value pairs in code format |

| `ToolCallDisplay` | `gui/src/pages/gui/ToolCallDiv/ToolCallDisplay.tsx` | 17-62 | Component | Tool | Composite: icon + favicon + status message + truncate button + children content |

| `ToolCallStatusMessage` | `gui/src/pages/gui/ToolCallDiv/ToolCallStatusMessage.tsx` | 10-62 | Component | Tool | Status text using tool.wouldLikeTo/isCurrently/hasAlready templates + Mustache render |

| `ToolTruncateHistoryIcon` | `gui/src/pages/gui/ToolCallDiv/ToolTruncateHistoryIcon.tsx` | 10-48 | Component | Tool | Truncate-history button; disabled during streaming |

| `ToggleWithIcon` | `gui/src/pages/gui/ToolCallDiv/ToggleWithIcon.tsx` | 13-58 | Component | Tool | Chevron + optional icon; hover-reveal chevron for toggleable sections |

| `IndicatorBar` | `gui/src/pages/gui/ToolCallDiv/IndicatorBar.tsx` | 8-19 | Component | Tool | Collapsed/expanded indicator badge at top of container |

| `TerminalCollapsibleContainer` | `gui/src/pages/gui/ToolCallDiv/TerminalCollapsibleContainer.tsx` | 12-61 | Component | Tool | Collapsible terminal output with gradient fade + indicator; supports hidden lines count |

| `CreateFile` | `gui/src/pages/gui/ToolCallDiv/CreateFile.tsx` | 10-25 | Component | Tool | Render new file creation in markdown preview with language highlighting |

| `EditFile` | `gui/src/pages/gui/ToolCallDiv/EditFile.tsx` | 15-34 | Component | Tool | Render file edits in collapsible markdown preview diff |

| `FindAndReplaceDisplay` | `gui/src/pages/gui/ToolCallDiv/FindAndReplace.tsx` | 86-343 | Component | Tool | Multi-edit visual diff with stats, apply/reject buttons, collapsible edits, status icon |

| `RunTerminalCommand` | `gui/src/pages/gui/ToolCallDiv/RunTerminalCommand.tsx` | 10-43 | Component | Tool | Terminal output + status (running/completed/failed/background) via UnifiedTerminal |

| `McpAppRenderer` | `gui/src/pages/gui/ToolCallDiv/MCPAppRenderer.tsx` | 92-425 | Component | Tool | MCP app iframe renderer: AppBridge protocol, CSP/permissions handling, srcdoc sandbox |

| `SimpleToolCallUI` | `gui/src/pages/gui/ToolCallDiv/SimpleToolCallUI.tsx` | 20-90 | Component | Tool | Collapsible output with icon + status + context items peek; handles 0/1/multi items |

| `GroupedToolCallHeader` | `gui/src/pages/gui/ToolCallDiv/GroupedToolCallHeader.tsx` | 13-37 | Component | Tool | Collapsible group header: action verb + count (Performing, Generating, Pending, etc.) |

| `FunctionSpecificToolCallDiv` | `gui/src/pages/gui/ToolCallDiv/FunctionSpecificToolCallDiv.tsx` | 9-82 | Component | Tool | Router: dispatches to CreateFile, EditFile, FindAndReplace, or RunTerminalCommand |

| `getStatusIntro` | `gui/src/pages/gui/ToolCallDiv/utils.tsx` | 13-34 | Function | Tool | Status verb (will, wants to, is, tried to) based on tool call status |

| `getGroupActionVerb` | `gui/src/pages/gui/ToolCallDiv/utils.tsx` | 37-57 | Function | Tool | Group header verb (Performing, Generating, Pending, Performed, Attempted) |

| `getStatusIcon` | `gui/src/pages/gui/ToolCallDiv/utils.tsx` | 70-83 | Function | Tool | Status icon (Spinner, ArrowRight, Check, XMark) |

| `toolCallStateToContextItems` | `gui/src/pages/gui/ToolCallDiv/utils.tsx` | 85-95 | Function | Tool | Convert tool call output to ContextItemWithId array |

| `getIconByName` | `gui/src/pages/gui/ToolCallDiv/utils.tsx` | 63-68 | Function | Tool | Dynamically import Heroicons by name |

| `toolCallCtxItemToCtxItemWithId` | `gui/src/pages/gui/ToolCallDiv/utils.tsx` | 98-109 | Function | Tool | Wrap ContextItem with toolCall ID metadata |

| `ToolCallDiv` | `gui/src/pages/gui/ToolCallDiv/index.tsx` | 19-137 | Component | Tool | Master router: MCP vs SimpleUI vs FunctionSpecific; grouped/ungrouped layout; status-based rendering |

| `ThemePage` | `gui/src/styles/ThemePage.tsx` | 57-232 | Component | Theme | Theme debugger page: visual color samples, missing variables detector, refresh/cache clear controls |

| `ThemeTailwindClassExample` | `gui/src/styles/ThemePage.tsx` | 19-55 | Component | Theme | Individual color swatch with CSS var names + default fallback |

| `Layout` | `gui/src/components/Layout.tsx` | 40-232 | Component | Root | Root layout: IDE event listeners, edit/dialog state, provider wrapper (Auth, LocalStorage) |

| `App` | `gui/src/App.tsx` | 53-66 | Component | Root | App entry point: router initialization, provider stack (VscTheme, MainEditor, SubmenuContextProviders, ParallelListeners) |

| main | `gui/src/main.tsx` | 9-24 | Bootstrap | Entry | React DOM root creation, Redux store/persistor wrapping, IIFE async mount |

| `editToolImpl`

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



  ---



  ## 🎨 GUI CONTEXT PROVIDERS (4 files, 946 lines)



  **Overview**: React Context/Provider layer enabling theme, local storage, authentication, and context submenu functionality across the GUI. Supports webview/IDE messaging, Redux integration, and MiniSearch-based intelligent search/sorting.



  ### 1. Theme Context & Provider (VscTheme.tsx - 184 lines)



  **Purpose**: Map VSCode TextMate theme data to Highlight.js CSS class colors; provide fallback light/dark theme.



  **Core Components**:

  - `VscThemeContext` (lines 159-161): Context wrapping `{ theme: Record<string, string> }`

  - `VscThemeProvider` (lines 163-182): Provider listening to `useWebviewListener("setTheme")` events; updates `window.fullColorTheme` and re-renders theme state

  - `useVscTheme()` (line 184): Hook to consume theme context



  **Key Logic**:

  - `constructTheme(tmTheme)` (lines 56-85): Map VSCode TextMate theme rules to hljs class names via `hljsToTextMate` lookup table (54 className→scopes mappings)

  - `fallbackTheme()` (lines 87-150): Parse editor background color via `--vscode-editor-background` CSS var; if luminance ≥128 (light), use Windows light theme; else use Windows dark theme

  - Webview integration: Listens for `setTheme` messages with `data.theme`, updates global `window.fullColorTheme`, triggers re-render



  **Dependencies**:

  - `../hooks/useWebviewListener`

  - `../styles/utils.parseHexColor`

  - Used by: code rendering, syntax highlighting, comment folding



  ---



  ### 2. Local Storage Context & Provider (LocalStorage.tsx - 68 lines)



  **Purpose**: Expose selected localStorage values (currently `fontSize`) as a React Context; synchronize state across tabs via CustomEvent.



  **Core Components**:

  - `LocalStorageContext` (lines 12-14): Context wrapping `{ fontSize: number }`

  - `LocalStorageProvider` (lines 16-63): Provider with two `useEffect` hooks

  - `useLocalStorage()` (lines 65-68): Hook to consume localStorage context



  **Key Logic**:

  - `syncWithLocalStorage()` (lines 22-30): Read `getLocalStorage("ide")` and `getLocalStorage("fontSize")`; default fontSize to 15 for JetBrains, 14 for VSCode

  - Initial sync (useEffect, lines 33-35): Call on mount

  - Tab sync (useEffect, lines 38-56): Listen for `localStorageChange` CustomEvent with `event.detail.key === "fontSize"`; resync state if key matches

  - Dispatch pattern: Code outside this provider can emit `localStorageChange` events to trigger provider re-sync



  **Dependencies**:

  - `../util/localStorage.getLocalStorage`

  - Used by: editor font sizing, UI layout



  ---



  ### 3. Authentication Context & Provider (Auth.tsx - 66 lines)



  **Purpose**: Expose current profile selection and config-refresh action; integrate with Redux and IDE messenger.



  **Core Components**:

  - `AuthContext` (line 17): Context wrapping `{ selectedProfile, profiles, refreshProfiles }`

  - `AuthProvider` (lines 19-58): Provider returning Redux-backed state + callback thunk

  - `useAuth()` (lines 60-66): Hook with error if not within provider



  **Key Logic**:

  - Redux integration (lines 26-27): Selectors `selectProfiles` and `selectSelectedProfile` read `store.config.profiles` and `store.config.selectedProfile`

  - `refreshProfiles(reason?)` (lines 29-45): 

    1. Dispatch `setConfigLoading(true)`

    2. Call `ideMessenger.request("config/refreshProfiles", { reason })`

    3. Post success toast `["info", "Config refreshed"]` on success or error toast on failure

    4. Finally dispatch `setConfigLoading(false)`

  - Error handling: Logs error, shows error toast if refresh fails



  **Dependencies**:

  - `core/config/ProfileLifecycleManager.ProfileDescription`

  - Redux hooks: `useAppDispatch`, `useAppSelector`, `setConfigLoading`, `selectProfiles`, `selectSelectedProfile`

  - `IdeMessengerContext`

  - Used by: profile selector UI, config refresh actions



  ---



  ### 4. Submenu Context Provider (SubmenuContextProviders.tsx - 628 lines)



  **Purpose**: Power searchable context submenu (file browser, providers, etc.) with MiniSearch-backed search, intelligent file sorting, periodic open-file refresh, and loading state management.



  **Core Components**:

  - `SubmenuContextProvidersContext` (lines 51-52): Context wrapping `{ getSubmenuContextItems(providerTitle?, query, limit?) }`

  - `SubmenuContextProvidersProvider` (lines 139-625): Provider with MiniSearch indices, fallback results, periodic file polling, loading set, abort controller map

  - `useSubmenuContextProviders()` (lines 627-628): Hook to get `getSubmenuContextItems` function



  **Key Features**:



  *MiniSearch Integration (lines 151-156, 525-537)*:

  - Build one `MiniSearch` index per provider (file, web, code docs, etc.)

  - Config: prefix matching + fuzzy(2) scoring

  - Custom tokenizer: concat default tokens + `splitCamelCaseAndNonAlphaNumeric()` tokens



  *Open Files Polling (lines 159-217)*:

  - `useEffect` with 2-second interval

  - Call `ideMessenger.ide.getOpenFiles()` + `getWorkspaceDirs()`, compute unique relative paths

  - Update `lastOpenFilesRef.current` and fallback results if set changed (deduplicated by id)

  - Cleanup: unsubscribe mounted flag, clear interval



  *Intelligent File Sorting (lines 224-278, 280-314)*:

  - `calculateFileSortPriority()` (9 tiers): exact filename, recent (open files), starts with, word match, common dev extensions, common dirs (src/lib/components/etc.), camelCase/abbreviation, path starts with, default

  - `calculateMatchQuality()`: exact match (100) + prefix (50) + contains (25) + short name bonus + dev file extension bonus



  *Search & Sort (lines 316-465)*:

  - `getSubmenuContextItems(providerTitle?, query, limit=70)`: 

    1. Search all or one provider's MiniSearch index

    2. If file provider: enhance results with sort priority + match quality; multi-key sort by priority→quality→score→path length

    3. Else: sort by MiniSearch score descending

    4. If no results: return fallback items (used by file or provider) or loading placeholder



  *Loading & Refresh (lines 467-588, 590-596)*:

  - `loadSubmenuItems(providers: "dependsOnIndexing" | "all" | string[])`: Async load and index items per provider

    - Skip if provider not marked `dependsOnIndexing` and indexing disabled

    - Use separate `AbortController` per provider; cancel on new request

    - Call `ideMessenger.request("context/loadSubmenuItems", { title })`

    - Build MiniSearch index, deduplicate, store in `fallbackResults`

    - Manage `providersLoading` set for loading state

  - Webview listener `refreshSubmenuItems` triggers `loadSubmenuItems` on demand

  - `useEffect` (lines 599-614): Auto-refresh when new provider titles detected



  *Helper Functions*:

  - `hasExactWordMatch(text, query)` (lines 55-58): Token-boundary word match

  - `isCommonDevFile(fileName)` (lines 60-92): Check common extensions (.ts, .tsx, .js, etc.) + names (index, main, app, component, service, util, helper, config, types)

  - `isInCommonDirectory(filePath)` (lines 94-112): Check common dirs (src, lib, components, utils, helpers, services, pages, views, hooks, store, types, interfaces, models, api)

  - `matchesCamelCaseOrAbbreviation(fileName, query)` (lines 114-137): Camel-case capital extraction or word-initial abbreviation



  **Dependencies**:

  - Core types: `ContextProviderDescription`, `ContextProviderName`, `ContextSubmenuItemWithProvider`

  - Core utils: `deduplicateArray`, `splitCamelCaseAndNonAlphaNumeric`, `getShortestUniqueRelativeUriPaths`, `getUriPathBasename`

  - `minisearch` (external dependency)

  - Redux: `useAppSelector`, `selectSubmenuContextProviders`, config.disableIndexing

  - Webview: `useWebviewListener`

  - IDE messenger: `IdeMessengerContext`, `ideMessenger.ide.getOpenFiles()`, `ideMessenger.ide.getWorkspaceDirs()`, `ideMessenger.request("context/loadSubmenuItems")`

  - Used by: context submenu, file picker, provider menus



  ---



  ### Cross-provider Coordination



  **Theme + Rendering**:

  - `VscThemeContext.theme` is consumed by rendering layers to style code blocks and UI elements

  - Populated from VSCode's TextMate color scheme on webview load



  **Local Storage + Other Contexts**:

  - `LocalStorageProvider` and other providers can coordinate state via Redux or localStorage events

  - If settings change via IDE, emit `localStorageChange` to trigger re-sync



  **Auth + Submenu/Config**:

  - `AuthContext.selectedProfile` + `refreshProfiles()` let UI track and refresh the active profile

  - `SubmenuContextProvidersProvider` is independent but can show profile-based context items



  **Submenu + Redux**:

  - Provider reads `selectSubmenuContextProviders` selector to determine available context providers

  - Respects `config.disableIndexing` to skip non-essential providers when indexing is off

     - Loading state integrated via `providersLoading` set; fallback results cached for fast UI response



  ---



  ## 🌉 IDE MESSENGER (1 file, 277 lines)



  **Overview**: Bridge layer between GUI webview and IDE (VSCode/JetBrains) using bidirectional message passing. Supports request/response patterns, async streaming with cancellation, and transparent access to core IDE APIs.



  ### IDE Messenger Protocol & Implementation (IdeMessenger.tsx - 277 lines)



  **Purpose**: Implement `IIdeMessenger` interface; send/receive messages to VSCode (vscode API) or JetBrains (postIntellijMessage); handle message routing, retries, streaming.



  **Core Interfaces & Types**:

  - `IIdeMessenger` (lines 26-60): Interface defining post(), respond(), request(), streamRequest(), llmStreamChat(), ide property

  - Message structure: `{ messageId, messageType, data }` (core/protocol/messenger.Message)

  - Protocol: `FromWebviewProtocol`, `ToWebviewProtocol` (core/protocol)



  **IdeMessenger Class** (lines 62-262):



  *Properties*:

  - `ide: IDE` (line 63): IDE API wrapper (MessageIde instance)



  *Methods*:



  | Method | Lines | Purpose | Behavior |

  |--------|-------|---------|----------|

  | `constructor()` | 65-76 | Create IDE API wrapper | Init MessageIde with request adapter, pass-through post callback |

  | `_postToIde()` | 78-112 | Low-level message dispatch | Check vscode vs JetBrains; validate postIntellijMessage; build Message object; vscode.postMessage() |

  | `post()` | 114-136 | One-way async send + retry | Try _postToIde with error catch; if error and attempts < 5, setTimeout retry with exponential backoff (2^n * 1000ms) |

  | `respond()` | 138-144 | Respond to request | Send response using messageId (for request/response pairing) |

  | `request()` | 146-163 | Promise-based request/response | Send message, return Promise that resolves when matching messageId received; add/remove window message listener |

  | `streamRequest()` | 173-247 | Streaming request with cancellation | Async generator; post message, buffer incoming chunks, yield when buffer fills; support AbortSignal to cancel |

  | `llmStreamChat()` | 249-261 | LLM streaming shortcut | Call streamRequest("llm/streamChat"); unwrap generator; yield ChatMessage arrays, return PromptLog |



  *Error Handling*:

  - Max 5 retry attempts on send failure (exponential backoff)

  - Stream errors logged, not thrown (lines 200-202, 222-223)

  - AbortSignal cleanup: remove event listeners on cancel or stream end



  **Context Integration**:

  - `IdeMessengerContext` (lines 264-266): React Context wrapping IIdeMessenger

  - `IdeMessengerProvider` (lines 268-277): Provider component accepting optional custom messenger

  - Used throughout GUI for: IDE requests (`ide.getOpenFiles()`, `getWorkspaceDirs()`), LLM streaming (`llm/streamChat`), core config operations (`config/refreshProfiles`), UI operations (`openUrl`, `toggleDevTools`)



  **Key Patterns**:

  1. **Lazy Initialization**: `window.postIntellijMessage` checked at send time, not init time (JetBrains may not inject early)

  2. **UUID Message IDs**: Unique per request/stream; enables parallel requests

  3. **Generator Buffering**: Stream chunks buffered; yielded in batches (not per-chunk) to reduce overhead

  4. **Platform Detection**: VSCode uses `vscode.postMessage()`, JetBrains uses `window.postIntellijMessage()`



  ---



  ## 🎨 GUI COMPONENTS (5 files, 784 lines)



  **Overview**: React components for onboarding, error handling, context menu, dialogs, and rich text editing. Integrate with Redux, IDE Messenger, theme/auth contexts, and Tiptap editor framework.



  ### 1. Onboarding Card (OnboardingCard.tsx - 57 lines)



  **Purpose**: First-run setup wizard for LLM provider configuration; tab-based UI for API key or local model setup.



  **Core Components**:

  - `OnboardingCard` (lines 20-57): Renders ReusableCard with OnboardingCardTabs and dynamic tab content

  - `OnboardingCardState` type (lines 11-14): `{ show?, activeTab? }`



  **Key Logic**:

  - Redux integration (line 22): `useAppSelector(store => store.config.config)` to read model list

  - localStorage tracking (lines 24-26): Set `onboardingStatus = "Started"` on first render

  - Tab selection (lines 28-32): Default to `OnboardingModes.API_KEY` if no tab selected

  - Tab rendering (lines 34-43): Switch on `activeTab` to render `OnboardingProvidersTab` or `OnboardingLocalTab`

  - Close button visibility (line 49): Only show if not dialog-mode AND models available



  **Sub-components**:

  - `OnboardingCardTabs` — Tab switcher UI

  - `OnboardingProvidersTab` — API key entry (LLM provider config)

  - `OnboardingLocalTab` — Local model download/setup



  **Used by**: Initial app bootstrap; can also be shown as a dialog on config errors



  ---



  ### 2. OSR Context Menu (OSRContextMenu.tsx - 222 lines)



  **Purpose**: Right-click context menu for copy/cut/dev-tools on Windows/Linux (not Mac); respects OSR (On-Screen Reader) enabled state.



  **Core Component**:

  - `OSRContextMenu` (lines 13-220): Exports default; memo-optimized



  **Key Features**:



  *State Management* (lines 18-25):

  - `position`: Menu coordinates (top/left/bottom/right) for smart positioning (open towards window center)

  - `canCopy`, `canCut`, `canPaste`: Availability based on selection state

  - `selectedTextRef`, `selectedRangeRef`: Preserve DOM selection during menu interaction

  - `menuRef`: Reference for click-outside detection



  *Event Handlers* (lines 27-153):

  - `onMenuItemClick()` (27-41): Restore selection on menu click, hide menu

  - `contextMenuHandler()` (47-49): Prevent default right-click

  - `clickHandler()` (50-142):

    1. Detect right-click (event.button === 2)

    2. Extract selected text + range from DOM selection API

    3. Check click was within selection (via getClientRects)

    4. Check if right-clicked on editable content (isContentEditable)

    5. Compute menu position: smart positioning on 4 screen quadrants

  - `leaveWindowHandler()` (44-46): Hide menu on mouse leave



  *Conditional Rendering* (line 156):

  - Skip entire component if macOS (platform.current === "mac") or OSR disabled or no position



  *Menu Items* (lines 159-218):

  - Copy (lines 168-177): `document.execCommand("copy")` if text selected

  - Cut (lines 179-188): `document.execCommand("cut")` if editable + selected

  - Open Dev Tools (lines 209-217): `ideMessenger.post("toggleDevTools", undefined)`



  **Dependencies**:

  - `useIsOSREnabled()` hook

  - `IdeMessengerContext` for dev tools

  - Platform detection via `getPlatform()`



  ---



  ### 3. Fatal Error Notice (FatalErrorNotice.tsx - 75 lines)



  **Purpose**: Alert UI for config loading errors; shows profile name, reload/help buttons, link to config page.



  **Core Component**:

  - `FatalErrorIndicator` (lines 9-75): Exports as component (no props)



  **Key Logic**:



  *State Assembly* (lines 10-14):

  - `useAuth()`: Get `refreshProfiles`, `selectedProfile`

  - `useAppSelector`: Read `configError` array, `configLoading` flag

  - `useLocation(), useNavigate()`: Navigation within config pages



  *Fatality Check* (lines 16-18):

  - `useMemo`: Compute `hasFatalErrors = configError.some(e => e.fatal)`

  - Early return null if no fatal errors



  *Display Name* (lines 32-35):

  - Prefer `selectedProfile.title`

  - Fallback to `${ownerSlug}/${packageSlug}` from fullSlug

  - Fallback to literal "config"



  *UI Rendering* (lines 37-73):

  - Alert type "error" with message: "Error loading {displayName}. Chat is disabled until a model is available."

  - Three action buttons:

    1. Help (lines 44-54): `ideMessenger.post("openUrl", "https://docs.continue.dev/troubleshooting")`

    2. Reload (lines 55-66): Conditionally show "Reloading..." or Reload button; call `refreshProfiles("Clicked reload in fatal indicator")`

    3. View (lines 67-71): Show if not already on config page; navigate via `CONFIG_ROUTES.CONFIGS`



  **Dependencies**:

  - Auth context for profile + refresh

  - Redux config slice (error, loading)

  - React Router navigation

  - IDE Messenger for help link



  ---



  ### 4. Text Dialog (dialogs/index.tsx - 81 lines)



  **Purpose**: Modal dialog with centered content, backdrop blur, Markdown/JSX rendering, keyboard/click handlers.



  **Core Components**:

  - `TextDialog` (lines 43-79): Main dialog component

  - `ScreenCover` (lines 19-27): Styled backdrop div (fixed, full-screen, blur)

  - `DialogContainer` (lines 29-41): Styled centered container



  **Props**:

  - `showDialog: boolean` — Visibility toggle

  - `onEnter: () => void` — Enter key callback

  - `onClose: () => void` — Close callback

  - `message?: string | JSX.Element` — Content (Markdown string or React element)



  **Key Logic**:



  *Validation* (lines 50-52):

  - Skip rendering if message is neither string nor valid JSX element



  *Keyboard Handler* (lines 44-48):

  - Esc key calls `props.onClose()`



  *Rendering* (lines 54-78):

  - ScreenCover: fixed backdrop with click-to-close, tabIndex=-1

  - DialogContainer: centered, auto-positioned, overflow-auto with responsive width (xs: 90%, sm: 88%, md: 80%, max 600px)

  - CloseButton: XMarkIcon (Heroicons) top-right

  - Content: ReactMarkdown for string, or direct JSX element



  **Styling**:

  - `vscBackground`, `vscForeground` colors

  - Backdrop: rgba(0, 0, 0, 0.35) with 0.5px blur

  - Border radius: `defaultBorderRadius`

  - Box shadow: 0 0 10px rgba(0, 0, 0, 0.5)



  ---



  ### 5. TipTap Editor (mainInput/TipTapEditor/TipTapEditor.tsx - 349 lines)



  **Purpose**: Rich text editor for chat input; Tiptap with image drag-drop, toolbar, slash commands, context providers, focus/blur management.



  **Props** (lines 27-44):

  - `availableContextProviders: ContextProviderDescription[]` — For @ snippets

  - `availableSlashCommands: ComboBoxItem[]` — For / commands

  - `isMainInput: boolean` — Main vs history editor

  - `onEnter(editorState, modifiers, editor)` — Submit handler

  - `editorState?: JSONContent` — Controlled content

  - `toolbarOptions?: ToolbarOptions` — Toolbar config (hide buttons)

  - `placeholder?: string` — Placeholder text

  - `historyKey: string` — For state persistence

  - `inputId: string` — DOM/logging identifier



  **Key Architecture**:



  *Editor Initialization* (lines 48-64):

  - `createEditorConfig({ props, ideMessenger, dispatch })` — Build Tiptap Editor with extensions

  - `useMainEditor()` context — Register main editor instance

  - Register as main if `isMainInput`



  *State Management* (lines 82-145):

  - `shouldHideToolbar`: Show only when focused or main input

  - `isStreaming`: Lock editing during LLM response

  - `isInEdit`: Track edit mode (prevents blur hiding)

  - History/editing state from Redux



  *Focus/Blur Behavior* (lines 183-206):

  - `handleBlur()`: Skip if in-edit mode; 100ms delay before hiding toolbar (allows listbox/combobox interaction)

  - `handleFocus()`: Cancel blur timeout, show toolbar

  - Auto-focus main input after streaming (lines 124-128)

  - Recovery: Re-enable historical editors after streaming ends (lines 131-145)



  *Image Handling*:

  - Drop handler (lines 238-265): Check model supports images; extract dataUrl; insert via schema

  - Toolbar upload (lines 283-298): Same logic for file input

  - Utility: `handleImageFile(ideMessenger, file)` → Promise<[fileName, dataUrl]>



  *Keyboard & Interaction*:

  - `useEditorEventHandlers()` — Slash commands, @ autocomplete, keyboard focus

  - `insertCharacterWithWhitespace()` (151-166) — Insert @ or / with smart spacing

  - Drag-over feedback (lines 222-237): Show "drag me" overlay when hovering



  *Component Memoization* (lines 317-341):

  - Shallow comparison of toolbar options, command/provider arrays

  - `MemoInner` wrapper prevents re-renders on prop churn

  - Exported `TipTapEditor` wraps memoized inner (lines 343-349)



  **Dependencies**:

  - Tiptap: `@tiptap/react`, `EditorContent`, `Editor`

  - Redux: dispatch, selectors for model/streaming/edit state

  - IDE Messenger: image file encoding

  - Theme/auth contexts (not used directly, but passed through)

  - Custom hook: `useUpdatingRef`, `useMainEditor`, `useEditorEventHandlers`

  - Utilities: `createEditorConfig`, `handleImageFile`, `getPlaceholderText`



  **Integration Points**:

  - Toolbar (InputToolbar): Renders with hidden/shown state, context/command insertion

  - Drag overlay: Visual feedback during file drag

  - Input styling: StyledComponents (InputBoxDiv)



     ---



  ## ⚙️ GUI HOOKS & MESSAGE LISTENERS (2 files, 303 lines)



  **Overview**: React hooks for webview message subscription and global event coordination. `useWebviewListener` provides typed message registration; `ParallelListeners` orchestrates all IDE/Core messages into Redux actions.



  ### 1. Webview Message Listener Hook (useWebviewListener.ts - 39 lines)



  **Purpose**: Generic React hook for subscribing to typed webview messages from IDE/Core; auto-responds with handler result.



  **Implementation** (lines 6-39):

  - `useWebviewListener<T extends keyof ToWebviewProtocol>(messageType, handler, dependencies?, skip?)`

  - Generic over message type T (from protocol)

  - Handler signature: `(data: ToWebviewProtocol[T][0]) => Promise<ToWebviewProtocol[T][1]>`



  **Behavior**:

  1. Get IdeMessenger from context (line 12)

  2. In useEffect (lines 14-38):

     - If not skipped, create listener for window "message" event

     - On message: check messageType match, call handler, auto-respond with result

     - If skipped, listener not registered

     - Cleanup: remove listener on unmount

  3. Dependency array: `[...dependencies, skip, ideMessenger]`



  **Key Pattern**:

  - Automatically responds using `ideMessenger.respond(messageType, result, messageId)`

  - Enables request/response semantics without boilerplate

  - Allows conditional listener registration via `skip` parameter



  **Usage Examples** (from ParallelListeners):

  - `useWebviewListener("configUpdate", handleConfigUpdate, [handleConfigUpdate])`

  - `useWebviewListener("jetbrains/setColors", (data) => { setDocumentStylesFromTheme(data); }, [])`

  - `useWebviewListener("getWebviewHistoryLength", async () => history.length, [history])`



  ---



  ### 2. Parallel Event Listeners & Dispatcher (ParallelListeners.tsx - 264 lines)



  **Purpose**: Global event listener component; coordinates all IDE↔GUI messages and dispatches appropriate Redux actions. Runs once at app bootstrap.



  **Component Exports**:

  - `ParallelListeners` (lines 37-264): React component (returns empty fragment)

  - Default export: ParallelListeners



  **Key Responsibilities**:



  *Initial Configuration Load* (lines 48-102):

  - `handleConfigUpdate(isInitial, result)` callback:

    1. Skip if already loaded and isInitial = true

    2. Dispatch `setProfiles(profiles)`, `setSelectedProfile(profileId)`, `setConfigResult(configResult)`

    3. If new profile: dispatch `initializeProfilePreferences`

    4. Set fontSize in localStorage + DOM style

    5. Check reasoning model support + prior settings

    6. Dispatch `setHasReasoningEnabled` based on capability + past preference



  *Config Loading & Initial Session* (lines 105-142):

  - Call `ideMessenger.request("config/getSerializedProfileInfo")` on mount

  - Load initial session if `initialSessionId` exists

  - Poll every 2 seconds until initial load complete:

    - Post "docs/initStatuses" to core

    - Dispatch `updateFileSymbolsFromHistory`

    - Dispatch `refreshSessionMetadata`

    - Clear interval



  *Message Listeners* (registered via useWebviewListener):



  | Message Type | Handler | Action |

  |--------------|---------|--------|

  | `configUpdate` | `handleConfigUpdate` | Dispatch config + profile updates |

  | `jetbrains/setColors` | `setDocumentStylesFromTheme` | Apply JetBrains theme colors |

  | `getWebviewHistoryLength` | Return `history.length` | Respond with history count |

  | `getCurrentSessionId` | Return `sessionId` | Respond with current session |

  | `setInactive` | `cancelStream()` | Cancel LLM streaming |

  | `setTTSActive` | `setTTSActive(status)` | Dispatch TTS state |

  | `addContextItem` | `addContextItemsAtIndex` | Dispatch add context at index |

  | `indexing/statusUpdate` | `updateIndexingStatus` | Dispatch indexing progress |

  | `updateApplyState` | `handleApplyStateUpdate` | Dispatch apply state update |



  *JetBrains-Specific Initialization* (lines 162-195):

  - Load theme colors: `ideMessenger.request("jetbrains/getColors")` → `setDocumentStylesFromTheme`

  - Call `ideMessenger.request("jetbrains/onLoad")` → store windowId, serverUrl, workspacePaths, vscMachineId, vscMediaUrl in window globals



  *Session & History Tracking* (lines 155-259):

  - `updateFileSymbolsFromHistory` on session ID change

  - `setLastNonEditSessionEmpty` when exiting edit mode

  - `migrateLocalStorage` on mount



  ---



  ## 📋 REDUX UTILITIES (4 files, 393 lines)



  **Overview**: Redux hooks and utility functions for system messages, message construction, and tool call state queries. Support LLM prompt assembly, rule application, and tool execution tracking.



  ### 1. Redux Typed Hooks (hooks.ts - 5 lines)



  **Purpose**: Provide typed Redux hooks bound to AppDispatch and RootState.



  **Exports**:

  - `useAppDispatch()` (line 4): Hook that returns `AppDispatch` type

  - `useAppSelector()` (line 5): TypedUseSelectorHook<RootState>; selector hook with RootState type inference



  **Usage**:

  ```typescript

  const dispatch = useAppDispatch();  // Typed to AppDispatch

  const value = useAppSelector(state => state.slice.field);  // RootState inferred

  ```



  ---



  ### 2. Base System Message Selector (getBaseSystemMessage.ts - 32 lines)



  **Purpose**: Select mode-specific system prompt; optionally append tool warning.



  **Constants**:

  - `NO_TOOL_WARNING` (lines 8-9): String constant warning agent/plan modes when no tools available



  **Function** (lines 11-32):

  ```typescript

  function getBaseSystemMessage(

    messageMode: string,

    model: ModelDescription,

    activeTools?: Tool[],

  ): string

  ```



  **Logic**:

  1. Select base message by mode:

     - "agent" → `model.baseAgentSystemMessage ?? DEFAULT_AGENT_SYSTEM_MESSAGE`

     - "plan" → `model.basePlanSystemMessage ?? DEFAULT_PLAN_SYSTEM_MESSAGE`

     - else (chat) → `model.baseChatSystemMessage ?? DEFAULT_CHAT_SYSTEM_MESSAGE`

  2. If mode !== "chat" AND no tools: append `NO_TOOL_WARNING`

  3. Return final message



  **Usage**: Called in LLM message construction to establish system user message



  ---



  ### 3. Message Constructor (constructMessages.ts - 230 lines)



  **Purpose**: Convert Redux chat history into LLM-ready message array; apply rules; handle tool call rendering; append summaries.



  **Type** (lines 33-36):

  - `MessageWithContextItems`: `{ ctxItems: ContextItemWithId[], message: ChatMessage }`



  **Function** (lines 37-230):

  ```typescript

  function constructMessages(

    history: ChatHistoryItem[],

    baseSystemMessage: string | undefined,

    availableRules: RuleWithSource[],

    rulePolicies: RulePolicies,

    useSystemToolsFramework?: SystemMessageToolsFramework,

  ): { messages: ChatMessage[], appliedRules: RuleMetadata[], appliedRuleIndex: number }

  ```



  **Algorithm**:



  1. **Summary Detection** (lines 48-60):

     - Scan history backward for most recent `conversationSummary`

     - Keep only messages AFTER the summary



  2. **History Processing** (lines 64-176):

     - Filter out system/tool/empty messages initially

     - For user messages:

       - Normalize content to message parts

       - Add context items as prepended text parts

       - Track index for rule application

     - For thinking messages: pass through

     - For assistant messages:

       - If `useSystemToolsFramework`: convert tool calls to system message tool format (assistant + user message pair)

       - Else: keep assistant message + insert tool call result messages per `toolCallState`:

         - No output → `NO_TOOL_CALL_OUTPUT_MESSAGE`

         - Canceled → `CANCELLED_TOOL_CALL_MESSAGE`

         - Errored → `ERRORED_TOOL_CALL_OUTPUT_MESSAGE`

         - Output + RunTerminalCommand → `renderContextItemsWithStatus(output)` (per-item status)

         - Output + other → `renderContextItems(output)`



  3. **Rule Application** (lines 178-204):

     - Find last user/tool message (for rule context)

     - Collect context items from last user message to end

     - Call `getSystemMessageWithRules()` to merge system message with filter rules

     - Get `systemMessage` + `appliedRules` array



  4. **Summary Append** (lines 206-212):

     - If `summaryContent` exists: prepend "Previous conversation summary: {summary}"

     - Merge with system message



  5. **Final Assembly** (lines 214-229):

     - If final system message not empty: prepend as system role message

     - Extract just messages (drop contextItems)

     - Return `{ messages, appliedRules, appliedRuleIndex }`



  ---



  ### 4. Tool Call State Utilities (util/index.ts - 126 lines)



  **Purpose**: Query tool call states in history; log tool execution.



  **Functions**:



  | Function | Lines | Purpose | Behavior |

  |----------|-------|---------|----------|

  | `hasCurrentToolCalls()` | 12-16 | Check for current tool calls | Return `findAllCurToolCalls().length > 0` |

  | `findAllCurToolCalls()` | 40-64 | Get all current tool calls | Scan history backward; stop at user message; return tool call states array from most recent assistant |

  | `findAllCurToolCallsByStatus()` | 25-32 | Filter tool calls by status | Call `findAllCurToolCalls()`, filter by status param (pending/executing/succeeded/canceled/errored) |

  | `findToolCallById()` | 73-90 | Find tool call anywhere | Reverse scan history; search each message's toolCallStates array; return match or undefined |

  | `findChatHistoryItemByToolCallId()` | 92-100 | Find tool message | Find first match: `message.role === "tool"` AND `message.toolCallId === toolCallId` |

  | `logToolUsage()` | 102-126 | Log tool execution telemetry | Post `ideMessenger.post("devdata/log", { name: "toolUsage", data: {...} })` with tool call details |



  **Tool Usage Logging** (lines 102-126):

  - Posts to "devdata/log" with telemetry:

    - toolCallId, functionName, functionParams, toolCallArgs

    - accepted (boolean), output (ContextItem[]), succeeded (boolean)

  - Used for analytics/debugging



     ---



  ## 📊 REDUX SELECTORS (3 files, 159 lines)



  **Overview**: Memoized selectors for mode-aware tool filtering, tool call state queries, slash commands, and context provider configuration. Leverage Redux Toolkit's `createSelector` for efficient memoization and composition.



  ### 1. Active Tools Selector (selectActiveTools.ts - 35 lines)



  **Purpose**: Mode-aware tool filtering based on mode, policies, and group settings.



  **Selector** (lines 7-35):

  ```typescript

  export const selectActiveTools = createSelector(

    [

      (store: RootState) => store.session.mode,

      (store: RootState) => store.config.config.tools,

      (store: RootState) => store.ui.toolSettings,

      (store: RootState) => store.ui.toolGroupSettings,

    ],

    (mode, tools, policies, groupPolicies): Tool[] => { ... }

  )

  ```



  **Filtering Logic**:

  1. **Chat Mode** (line 15-16): Return empty array (no tools available in chat)

  2. **Agent/Plan Mode** (line 18-32):

     - Filter tools where:

       - `toolPolicy !== "disabled"` (check individual tool settings or default)

       - `groupPolicies[tool.group] !== "exclude"` (group not excluded)

     - If plan mode: additional filter for readonly tools + built-in tools only (line 28-30)

     - Otherwise (agent mode): return all enabled tools



  **Policy Resolution Precedence**:

  1. `policies[tool.function.name]` (individual tool setting)

  2. `tool.defaultToolPolicy` (tool-level default)

  3. `DEFAULT_TOOL_SETTING` (global constant fallback)



  ---



  ### 2. Tool Call Selectors (selectToolCalls.ts - 68 lines)



  **Purpose**: Memoized queries for tool call state inspection and apply state tracking.



  **Primary Selectors**:



  | Selector | Lines | Purpose | Behavior |

  |----------|-------|---------|----------|

  | `selectCurrentToolCalls` | 12-15 | Get all current tool calls | Call `findAllCurToolCalls(history)` (most recent assistant message's tool calls) |

  | `selectHasCurrentToolCalls` | 17-20 | Boolean: any current tool calls? | Call `hasCurrentToolCalls(history)` (length > 0) |

  | `selectToolCallsByStatus` | 22-28 | Filter by status (param) | Call `findAllCurToolCallsByStatus(history, status)` with status param |

  | `selectFirstPendingToolCall` | 30-36 | Get first "generated" tool call | Find all "generated" status; return [0] or undefined |



  **ID-Based Selectors**:



  | Selector | Lines | Purpose | Behavior |

  |----------|-------|---------|----------|

  | `selectToolCallById` | 39-45 | Lookup by toolCallId (param) | Call `findToolCallById(history, toolCallId)` |

  | `selectApplyStateByToolCallId` | 47-57 | Apply state for tool call (param) | Find most recent apply state in `codeBlockApplyStates.states` matching toolCallId |



  **Convenience Status Selectors**:



  | Selector | Lines | Purpose | Returns |

  |----------|-------|---------|---------|

  | `selectPendingToolCalls` | 60-63 | Alias for "generated" status | All pending tool calls |

  | `selectDoneApplyStates` | 65-68 | Filter apply states by status | All apply states where status === "done" |



  ---



  ### 3. Command & Context Selectors (index.ts - 56 lines)



  **Purpose**: Slash commands and context provider configuration selectors.



  **Slash Command Selectors**:



  | Selector | Lines | Purpose |

  |----------|-------|---------|

  | `selectSlashCommandComboBoxInputs` | 8-30 | Transform slash commands → ComboBoxItems for input autocomplete |

  | `selectSlashCommands` | 32-37 | Return raw slash commands array (empty fallback) |



  **selectSlashCommandComboBoxInputs Logic** (lines 8-30):

  - Map each command to ComboBoxItem:

    - title: `cmd.name`

    - description: `cmd.description`

    - type: `"slashCommand"` (literal type)

    - content: `cmd.prompt` (or fallback "[MCP Prompt - failed to load...]" if MCP source + no content)

    - source: `cmd.source` (e.g., "mcp-prompt", "config", etc.)

  - Returns empty array if slashCommands falsy



  **Context Provider Selectors**:



  | Selector | Lines | Purpose |

  |----------|-------|---------|

  | `selectSubmenuContextProviders` | 39-44 | Filter providers by type === "submenu" |

  | `selectDefaultContextProviders` | 46-51 | Extract `config.experimental.defaultContext` array |

  | `selectUseActiveFile` | 53-56 | Boolean: "activeFile" in default context? |



  ---



  ### Selectors in Redux Signal Chain



  **Tool Filtering Flow**:

  - `selectActiveTools` read by LLM message construction

  - Tool policies updated via config slice

  - Memoization prevents unnecessary UI rerenders on history change



  **Tool Call Queries**:

  - `selectCurrentToolCalls` → UI components displaying tool execution status

  - `selectFirstPendingToolCall` → Tool execution manager

  - `selectApplyStateByToolCallId` → Apply state UI (accept/reject/retry)

  - All backed by efficient history scanning via `redux/util` functions



  **Command & Context**:

  - `selectSlashCommandComboBoxInputs` → Input autocomplete dropdown

  - `selectDefaultContextProviders` → Sidebar default context panel

  - `selectUseActiveFile` → Configuration-driven feature flag



     ---



  ## 📦 REDUX STATE SLICES (7 files, 722 lines)



  **Overview**: Core Redux slices managing GUI application state. Covers session history/streaming, configuration, profiles, tabs, tools/rules policies, edit mode, and indexing. Each slice exports reducers (actions) and selectors for memoized queries.



  ### 1. Edit State Slice (editState.ts - 87 lines)



  **Purpose**: Manage edit mode state, code changes, and apply state tracking.



  **State** (lines 6-14):

  - `codeToEdit: SetCodeToEditPayload[]` - Array of code ranges to edit

  - `applyState: ApplyState` - Status of edit application (not-started/pending/done)

  - `returnToMode: MessageModes` - Mode to return to after editing (chat/agent/plan)

  - `lastNonEditSessionWasEmpty: boolean` - Track if exit from empty non-edit session

  - `previousModeEditorContent?: JSONContent` - Save editor state before entering edit



  **Key Actions**:

  - `setCodeToEdit()` - Normalize single/array to array

  - `updateEditStateApplyState()` - Merge apply state updates

  - `clearCodeToEdit()` - Reset code array

  - `setReturnToModeAfterEdit()` - Set exit mode

  - `setPreviousModeEditorContent()` - Save editor state



  ---



  ### 2. Indexing Slice (indexingSlice.ts - 66 lines)



  **Purpose**: Track background indexing progress and visibility state.



  **State** (lines 4-9):

  - `statuses: Record<string, IndexingStatus>` - Map of indexing ID → status details

  - `hiddenChatPeekTypes: Record<IndexingStatus["type"], boolean>` - Track hidden "peek" notifications per type (docs, etc.)



  **Key Actions**:

  - `updateIndexingStatus()` - Add/update status by ID; auto-unhide peek when all indexing of type complete

  - `setIndexingChatPeekHidden()` - Toggle visibility per indexing type



  ---



  ### 3. Profiles Slice (profilesSlice.ts - 151 lines)



  **Purpose**: Manage LLM profiles and user preferences per profile.



  **State** (lines 15-19):

  - `profiles: ProfileDescription[]` - Available profiles from config

  - `selectedProfileId: string | null` - Currently active profile

  - `preferencesByProfileId: Record<string, PreferencesState>` - Per-profile bookmarks and settings



  **Preferences** (lines 11-13):

  - `bookmarkedSlashCommands: string[]` - Top 5 frequently used commands



  **Key Actions**:

  - `setProfiles()` - Load profiles from config

  - `setSelectedProfile()` - Switch active profile

  - `initializeProfilePreferences()` - Initialize preferences for profile (backfill, bookmark defaults)

  - `bookmarkSlashCommand()` / `unbookmarkSlashCommand()` - Add/remove from bookmarks



  **Selectors**:

  - `selectSelectedProfile()` - Current profile object

  - `selectProfiles()` - All profiles array

  - `selectBookmarkedSlashCommands()` - Bookmarks for current profile



  ---



  ### 4. Tabs Slice (tabsSlice.ts - 140 lines)



  **Purpose**: Multi-tab chat sessions with intelligent session linking.



  **Tab Type** (lines 3-8):

  ```typescript

  interface Tab {

    id: string;               // Unique tab identifier

    title: string;            // Tab display title

    isActive: boolean;        // Currently rendered tab

    sessionId?: string;       // Linked session ID (optional)

  }

  ```



  **Key Actions**:

  - `addTab()` - Create new tab; deactivate others

  - `setActiveTab()` - Switch active tab

  - `removeTab()` / `setTabs()` - Remove/replace tabs

  - `handleSessionChange()` - Smart session↔tab matching logic (lines 57-127):

    1. If session matches active tab → update title

    2. If existing unlinked tab has session → reuse + clean

    3. If active tab has no session → link to active tab

    4. Otherwise → create new tab with session



  ---



  ### 5. Session Slice (sessionSlice.ts - 1097 lines)



  **Purpose**: Core chat session state: history, streaming, tool calls, apply states, reasoning.



  **SessionState** (lines 203-226): Comprehensive dict with:

  - `history: ChatHistoryItemWithMessageId[]` - Chat messages + context + tool call states

  - `isStreaming: boolean` - LLM response streaming active

  - `mode: MessageModes` - chat/agent/plan

  - `symbols: FileSymbolMap` - Current workspace file symbols cache

  - `codeBlockApplyStates: { states: ApplyState[], curIndex: number }` - Track edits

  - `hasReasoningEnabled?: boolean` - Extended thinking

  - `allSessionMetadata: BaseSessionMetadata[]` - History of past sessions



  **Message Streaming** (lines 524-684):

  - `streamUpdate()` processes message chunks:

    1. Create new message if role differs

    2. Handle `<think>...</think>` tags → reasoning state

    3. Accumulate content

    4. Apply tool call deltas

    5. Handle OpenAI Responses API output item IDs



  **Tool Call Management** (lines 832-959):

  - `setToolGenerated()` - Mark tool as "generated" phase

  - `updateToolCallOutput()` - Set output context + update tool message

  - `cancelToolCall()` / `errorToolCall()` / `acceptToolCall()` - Status transitions

  - `setToolCallCalling()` - Status = "calling"



  **History Manipulation**:

  - `submitEditorAndInitAtIndex()` (lines 357-413) - Append user/assistant pair for streaming

  - `truncateHistoryToMessage()` (lines 414-438) - Truncate to message; reset apply states

  - `deleteMessage()` - Delete user+assistant pair

  - `updateHistoryItemAtIndex()` - Merge updates

  - `addContextItemsAtIndex()` - Append context

  - `addHighlightedCode()` (lines 764-806) - Add code range as context with metadata



  **Apply State Tracking** (lines 807-826):

  - `updateApplyState()` - Insert or merge apply state; auto-increment index on "done"

  - Selectors: `selectApplyStateByStreamId()`, `selectApplyStateByToolCallId()`



  **Helper Functions**:

  - `handleToolCallsInMessage()` (lines 77-103) - Initialize tool states; filter duplicate edit tools

  - `handleStreamingToolCallUpdates()` (lines 166-195) - Apply deltas matching by ID

  - `filterMultipleEditToolCalls()` (lines 51-66) - Drop duplicate edit tool generation



  ---



  ### 6. Config Slice (configSlice.ts - 109 lines)



  **Purpose**: Store parsed configuration (models, tools, rules, context providers).



  **ConfigState** (lines 6-10):

  - `config: BrowserSerializedContinueConfig` - Full config object

  - `configError?: ConfigValidationError[]` - Parse/validation errors

  - `loading: boolean` - Loading state



  **EMPTY_CONFIG** (lines 12-38): Default structure with empty arrays and model role mappings.



  **Key Actions**:

  - `setConfigResult()` - Load config + errors from ConfigResult

  - `updateConfig()` - Replace config state

  - `setConfigLoading()` - Set loading flag



  **Selectors**:

  - `selectSelectedChatModel()` - Currently selected chat model

  - `selectSelectedChatModelContextLength()` - Context window (or default)

  - `selectUIConfig()` - UI-specific config object



  ---



  ### 7. UI Slice (uiSlice.ts - 171 lines)



  **Purpose**: UI settings, policies, dialogs, onboarding, and TTS state.



  **UIState** (lines 20-32):

  - `toolSettings: ToolPolicies` - Per-tool execution policies

  - `toolGroupSettings: ToolGroupPolicies` - Per-group include/exclude

  - `ruleSettings: RulePolicies` - Per-rule on/off

  - `reasoningSettings: ReasoningSettings` - Per-model reasoning enabled

  - `showDialog: boolean` + `dialogMessage: JSX.Element` - Modal dialog

  - `onboardingCard: OnboardingCardState` - First-run UI

  - `isExploreDialogOpen: boolean` - Samples/docs explore panel

  - `ttsActive: boolean` - Text-to-speech on/off



  **Policy Types**:

  - `ToolPolicy: "allowedWithPermission" | "allowedWithoutPermission" | "disabled"`

  - `RulePolicy: "on" | "off"`

  - `ToolGroupPolicy: "include" | "exclude"`



  **Key Actions**:

  - Tool settings:

    - `addTool()` - Add tool with default policy

    - `setToolPolicy()` - Set explicit policy

    - `toggleToolSetting()` - Cycle permissions

    - `clearToolPolicy()` - Delete (revert to default)

    - `toggleToolGroupSetting()` - Toggle group

  - Rule settings:

    - `addRule()` - Add with default "on"

    - `toggleRuleSetting()` - Toggle on/off

  - Other:

    - `setReasoningSetting()` - Enable reasoning per model

    - `setTTSActive()` - Toggle TTS

    - `setOnboardingCard()`, `setDialogMessage()`, `setShowDialog()` - Dialog/onboarding



  ---



  ### Redux Slices Cross-Coordination



  **Config → Selection → Message Construction**:

  - Config slice loads models, tools, rules

  - Selector slices filter active tools (via policies)

  - Message construction includes active tools in LLM prompt



  **Session → Apply State → UI Feedback**:

  - Session tracks apply states for code edits

  - UI queries apply states to show accept/reject buttons

  - Tab slice links sessions for multi-tab workflow



  **Streaming Pipeline**:

  - Session `streamUpdate()` appends/updates history

  - Tool actions modify tool call states in-place

  - Selectors memoize tool call queries for UI



  **Profile → Preferences → Slash Commands**:

  - Profiles slice manages bookmarks per profile

  - Selectors return bookmarks for autocomplete

  - Config slice provides all available commands



  ---



        ## 🔄 REDUX THUNKS (15 files, 1542 lines)



        **Overview**: Async side effect handlers orchestrating IDE messaging, LLM streaming, tool execution, and session management. Thunks compose lower-level actions and selectors into complex workflows with error handling and retry logic.



        ### 1. Stream Orchestration & Error Handling



        **streamThunkWrapper** (61 lines):

        - Wraps all LLM streaming operations

        - Detects "overloaded" errors (HTTP 529); retries up to 3× with exponential backoff (2s, 4s, 8s)

        - Shows StreamErrorDialog on final failure

        - Saves session after successful stream



        **streamNormalInput** (398 lines):

        - **Core LLM chat pipeline**:

          1. Build messages with `constructMessages()` (system + rules + context)

          2. Add tool framework (system message tools OR native tools)

          3. Build reasoning options if enabled

          4. Compile messages via `llm/compileChat` (IDE side)

          5. Stream from IDE and accumulate chunks

          6. Detect context pruning + apply rules indices

          7. Intercept system tool calls if needed

          8. Handle generation errors (premature close, etc.)



        - **Tool call sequence**:

          1. Mark generating calls as "generated" (line 295)

          2. Preprocess args via `preprocessToolCalls()` (line 309)

          3. Evaluate policies via `evaluateToolPolicies()` (line 318)

          4. Filter: auto-approved (dispatch immediately), needs-approval (stop), all-approved (dispatch all)

          5. Execute via `callToolById()` for auto-approved readonly built-in tools

          6. Return and wait for UI approval for others



        **streamResponse & streamResponseThunk** (104 lines):

        - Resolve editor content (slash commands, context providers, selected code)

        - Gather symbols for files

        - Submit user message to history

        - Dispatch `streamNormalInput`



        ---



        ### 2. Tool Call Execution Pipeline



        **callToolById** (150 lines):

        - Dispatch state change: generating → calling

        - Route: client-side tools (edit/search-replace) → call directly

        - Route: core tools → Messenger.request("tools/call")

        - Collect output + mcpUiState

        - Log usage telemetry

        - Dispatch `streamResponseAfterToolCall` if streamResponse=true

        - **Auto-approval logic**: edit tools always allowed; others depend on policies



        **preprocessToolCalls** (65 lines):

        - Promise.all() validate all tool args in parallel

        - Use core `tools/preprocessArgs` RPC

        - Dispatch errors/processed args via session actions

        - Errors marked as "errored" with user message



        **evaluateToolPolicies** (121 lines):

        - Dynamic policy evaluation: auth args + base policies

        - Edit tools: always allowedWithoutPermission

        - Non-edit: use tool policy + default fallback

        - Policy hierarchy enforcement:

          - disabled (most restrictive) cannot be overridden

          - allowedWithPermission cannot become allowedWithoutPermission

        - Return evaluated results + displayValue per policy

        - Mark disabled tools as errored with security violation message



        **streamResponseAfterToolCall** (88 lines):

        - Create tool message with output

        - Check if all tool calls in assistant message are complete (done/errored/canceled)

        - If all done: dispatch `streamNormalInput` (depth + 1) to continue conversation

        - Handles race conditions gracefully



        **cancelStream** (18 lines):

        - Abort streaming via `abortStream()` action

        - Clear any dangling messages/incomplete tool calls

        - Mark session as inactive



        **cancelToolCall** (47 lines):

        - User rejects tool call via UI

        - Add rejection context message (configurable)

        - Dispatch `streamResponseAfterToolCall` to continue



        ---



        ### 3. Edit Mode Management



        **enterEdit** (156 lines):

        - Validate code to edit exists

        - Save current editor content

        - Set return-to mode

        - Dispatch `saveCurrentSession` (to save context before switching)

        - Set `isInEdit = true`



        **exitEdit** (156 lines):

        - Clear code to edit

        - Restore editor content from saved state

        - Reject any pending diffs

        - Post `clearDecorations` to IDE

        - Decide: open new session OR load last session OR just clear

        - Restore mode



        **streamEditThunk** (156 lines):

        - Resolve editor content (no slash commands/context in edit mode)

        - Send prompt + code range to IDE via `edit/sendPrompt`

        - Set inactive



        **handleApplyStateUpdate** (217 lines):

        - Route by streamId:

          - **Edit mode** (EDIT_MODE_STREAM_ID): update editState, log on close → dispatch `exitEdit`

          - **Chat/Agent**: update session apply states

        - On done status + allowedWithoutPermission: auto-accept diff via IDE

        - On closed status:

          - Log tool outcome (accepted/errored)

          - Log edit outcome for Agent mode

          - If accepted: add auto-format context if present

          - Dispatch `streamResponseAfterToolCall` (depth + 1)



        **applyForEditTool** (217 lines):

        - Dispatch apply state: not-started

        - Request file application via IDE `applyToFile`

        - On error: dispatch errorToolCall + update with error message

        - On success: (handled by handleApplyStateUpdate via IDE callback)



        ---



        ### 4. File & Symbol Management



        **updateFileSymbols(FromFiles|FromHistory)** (113 lines):

        - `updateFileSymbolsFromFiles()`: Request symbols for specific file paths

        - `updateFileSymbolsFromHistory()`: Extract file URIs from context items in history; skip already-cached; request new

        - `getContextItemsFromHistory()`: Collect file references from normal context + toolbar code blocks



        ---



        ### 5. Session Lifecycle



        **saveCurrentSession** (262 lines):

        - Skip if history empty

        - Optionally open new session first

        - Generate title (fallback: first assistant response; fallback: from message)

        - Respect disableSessionTitles config

        - Format session object (id, title, workspaceDir, history, mode, chatModelTitle)

        - Request IDE save via `history/save`

        - Refresh metadata list



        **loadLastSession** (170 lines):

        - Get lastSessionId from session state

        - Request session from IDE (with retry on failure)

        - Dispatch `newSession()` with loaded session

        - Restore model selection



        **loadSession** (114 lines):

        - Optionally save current session first

        - Load specific session by ID

        - Dispatch `newSession()` + restore model



        **selectChatModelForProfile** (138 lines):

        - Wrapper to dispatch `updateSelectedModelByRole` with current profile



        **refreshSessionMetadata** (52 lines):

        - Request session list from IDE

        - Update Redux state with metadata



        **deleteSession** (68 lines):

        - Optimistic delete from metadata

        - Load last session if current is deleted

        - Request IDE delete

        - Refresh metadata



        **updateSession** (82 lines):

        - Optimistic metadata update

        - Request IDE save

        - Refresh metadata



        **updateSelectedModelByRole** (58 lines):

        - Update config with selected model for role

        - Post to IDE for persistence



        ---



        ### 6. Utility Functions & Helpers



        **buildReasoningCompletionOptions()** (70 lines):

        - Extend completion options with reasoning settings

        - Set reasoning=true if enabled

        - Add reasoningBudgetTokens if provider supports (skip Ollama)



        **areAllToolsDoneStreaming()** (35 lines):

        - Check all tool calls completed or optionally canceled (continueAfterToolRejection)

        - Determine if LLM should stream next response



        **moveTerminalProcessToBackground** (82 lines):

        - Preserve existing terminal output

        - Mark process as backgrounded in IDE

        - Accept tool call

        - Continue stream



        ---



        ### Thunk Cross-Coordination



        **Streaming Pipeline**:

        - User submits → `streamResponseThunk` → resolve content

        - `streamNormalInput` → compile + stream

        - Tool calls generated → preprocess → evaluate policies

        - Auto-approved → `callToolById` (parallel)

        - Streams response → `streamResponseAfterToolCall`

        - All done → cycle back to `streamNormalInput` (depth + 1)



        **Edit Mode**:

        - `enterEdit` → save session → stream edits

        - `streamEditThunk` → send to IDE

        - IDE applies → `handleApplyStateUpdate` → `exitEdit` → restore session



        **Session Management**:

        - `saveCurrentSession` generates title (title describer or fallback)

        - `loadLastSession` restores model selection

        - `refreshSessionMetadata` keeps list in sync



        **Error Handling**:

        - `streamThunkWrapper` catches and retries overloaded errors

        - Tool policy evaluation disables unsafe calls

        - Argument validation prevents execution errors

        - Apply errors logged with outcomes



        ---



              ---



              ## 🏪 REDUX STORE (1 file, 153 lines)



              **Overview**: Central Redux configuration, combining all slices into a persistent store with middleware setup, state migrations, and type exports for thunks and components.



              ### Store Architecture



              **Root Reducer Composition** (lines 26-34):

              ```

              rootReducer = combineReducers({

                session,           // Chat history, tool calls, edit state

                ui,                // Tool policies, settings, dialogs

                editModeState,     // Temporary edit mode metadata

                config,            // Models, tools, rules, LLM settings

                indexing,          // Codebase indexing status

                tabs,              // Multi-tab chat session tracking

                profiles,          // User workspace/profile preferences

              })

              ```



              ### Persistence Configuration



              **redux-persist Setup** (lines 92-99):

              - **Key**: `"root"` (identifies localStorage entry)

              - **Version**: 1 (enables state schema migrations)

              - **Storage**: Browser localStorage

              - **Transforms**: `saveSubsetFilters` (selective persistence, see below)

              - **stateReconciler**: `autoMergeLevel2` (merge loaded state with runtime defaults)

              - **Migrate**: `createMigrate()` with migration manifest for schema upgrades



              **Selective Persistence** (lines 36-64):

              - **session**: Save only `id`, `lastSessionId`, `title`, `mode` (not `history` or `symbols` due to size/risk)

              - **editModeState**: Save `returnToMode`, `lastNonEditSessionWasEmpty`, `codeToEdit`

              - **config**: Persist nothing (rebuilt from IDE on load)

              - **ui**: Save `toolSettings`, `toolGroupSettings`, `ruleSettings`, `reasoningSettings`

              - **indexing**: Persist nothing

              - **tabs**: Save `tabs` array (tab IDs and titles)

              - **profiles**: Save `preferencesByProfileId`, `selectedProfileId`



              **State Migrations** (lines 66-90):

              - **v0→v1**: Convert old `oldState.state.sessionId` → new `session.id` structure; initialize `tabs` array

              - Provides forward compatibility for localStorage upgrades



              ### Store Setup & Middleware



              **setupStore(options)** (lines 106-131):

              - Configures Redux Toolkit with:

                - **Reducer**: `persistedReducer` (wraps `rootReducer` with redux-persist)

                - **Middleware**:

                  - Redux Toolkit defaults (exceptions: `serializableCheck: false` for non-serializable thunk extras)

                  - **Thunk extras**: `ideMessenger` passed to all async thunks via `extra` argument

                - **Optional**: Redux logger middleware (commented out by default)



              **Logger Configuration** (lines 109-114):

              - `collapsed: true` – Collapse action logs in console

              - `timestamp: false` – Omit timestamps

              - `diff: true` – Show state diff after each action



              ### Type Exports for Type Safety



              **ThunkExtrasType** (line 134):

              ```typescript

              type ThunkExtrasType = { ideMessenger: IIdeMessenger }

              ```

              - Shape of extra argument in `createAsyncThunk`

              - Enables IDE messaging in thunks via `extra.ideMessenger`



              **ThunkApiType** (lines 136-139):

              ```typescript

              type ThunkApiType = {

                state: RootState,

                extra: ThunkExtrasType

              }

              ```

              - Complete thunk API configuration

              - Used in `createAsyncThunk({ ..., asyncThunkCreator(arg, thunkApi) })`



              **AppThunkDispatch** (lines 141-145):

              ```typescript

              type AppThunkDispatch = ThunkDispatch<RootState, ThunkExtrasType, UnknownAction>

              ```

              - Dispatch type for async thunks

              - Includes thunk handling + normal action dispatch

              - Used in client tools and utility functions



              **RootState** (line 149):

              ```typescript

              type RootState = ReturnType<typeof rootReducer>

              ```

              - Entire Redux state tree shape

              - Used in all selectors and thunk type signatures



              **AppDispatch** (line 151):

              ```typescript

              type AppDispatch = typeof store.dispatch

              ```

              - Regular (non-thunk) dispatch type

              - Used in React hooks: `useAppDispatch()`, `useAppSelector()`



              ### Store Instances



              **store** (line 147):

              - Global Redux store instance created via `setupStore({})`

              - Default: no custom IdeMessenger (uses global instance from IdeMessenger constructor)

              - Used to dispatch actions/thunks throughout the app



              **persistor** (line 153):

              - redux-persist persistor instance

              - Wrapped in `PersistGate` in root App component

              - Delays app render until persisted state rehydrates from localStorage

              - Handles purge/flush operations for logout/session clear



              ### Integration Points



              **Thunk Access to IdeMessenger**:

              - All async thunks receive `extra.ideMessenger` as 3rd parameter

              - Enables request/post to IDE without coupling slices to context

              - Example: `extra.ideMessenger.request("tools/call", ...)`



              **Selector Type Safety**:

              - `RootState` type enables compile-time safety for `useAppSelector(state => ...)`

              - TypeScript auto-completes paths within state tree



              **Dispatch Type Safety**:

              - `AppDispatch` ensures only valid actions/thunks can be dispatched

              - `AppThunkDispatch` in utilities provides thunk-specific types



              ---



                    ### Redux & Hooks Cross-Coordination



  **useAppDispatch + useAppSelector**:

  - Used throughout GUI to access/modify Redux state

  - TypeSafe: AppDispatch + RootState types prevent misuse

  - Combined with thunks for async operations



  **System Message + Message Construction**:

  - `getBaseSystemMessage()` called within `constructMessages()` workflow

  - Establishes initial system context; rules layer additional guidance

  - Tool call rendering handles framework-specific formatting



  **Tool Call Utilities + ParallelListeners**:

  - `ParallelListeners` dispatches actions that modify chat history

  - Tool call functions query resulting history state

  - `logToolUsage()` sends telemetry to IDE via IdeMessenger



  ---



  ### Component Cross-Coordination



  **IdeMessenger in Components**:

  - `OSRContextMenu` toggles dev tools

  - `FatalErrorNotice` opens docs links

  - `TipTapEditor` encodes images for insertion

  - `OnboardingCard` (indirectly via Redux/auth dispatch)



  **Theme/Auth in Components**:

  - `FatalErrorNotice` uses Auth context to refresh profiles

  - `TipTapEditor` reads selectedModel for image support check

  - Dialog uses VSCode theme colors



  **Redux in Components**:

  - `OnboardingCard` reads config.modelsByRole

  - `FatalErrorNotice` reads config error/loading state

  - `TipTapEditor` reads session (streaming, isInEdit), config (selectedModel)



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



## 📄 PAGES (4 files, 313 lines)



**Overview**: React Router page components rendering top-level UI routes (error boundary, analytics, history browser, settings hub). Each page uses hooks (useNavigationListener), IdeMessenger, selectors, and thunks for lifecycle and state management.



### 1. Error Boundary Page



**ErrorPage** (62 lines):

- **Route**: `/error` (global error boundary fallback)

- **Imports**: `useRouteError()` from React Router, `IdeMessengerContext`, `useDispatch`, `useNavigate`

- **Behavior**:

  - Displays error status/message text (lines 34-38)

  - Clears localStorage (`persist:root`, `inputHistory_chat`)

  - Dispatches `newSession()` to reset Redux state

  - Navigates back to home (`/`)

  - Shows FlagIcon initially, then ArrowPathIcon after 500ms (retry animation)

  - Opens GitHub/discussions links via IdeMessenger.post("openUrl")



### 2. Stats/Analytics Page



**Stats** (141 lines):

- **Route**: `/stats` (More menu)

- **Imports**: `useNavigationListener()`, `IdeMessengerContext`, styled-components tables, `CopyIconButton`

- **State**: `days[]` (day + tokens), `models[]` (model name + tokens)

- **Lifecycle**:

  - Line 49: Fetch `stats/getTokensPerDay` from IDE on mount

  - Line 55: Fetch `stats/getTokensPerModel` from IDE on mount

- **UI Layout**:

  - PageHeader with back navigation

  - Table 1: Tokens per day (day, generated, prompt counts)

  - Table 2: Tokens per model (model, generated, prompt counts)

  - CopyIconButton on each table for copy-to-clipboard (formatted via `table()` library)

  - Hover effect on rows (vscInputBackground color)



### 3. History Page



**HistoryPage** (18 lines):

- **Route**: `/history` (Chat history browser)

- **Simplest page**: Just wraps History component with PageHeader

- **Imports**: History component, PageHeader, `getFontSize()` utility

- **Props**: Responsive font sizing via `getFontSize()`

- **Navigation**: Back button in PageHeader goes to home



### 4. Config / Settings Hub



**ConfigPage** (92 lines):

- **Route**: `/config?tab={tabId}` (Settings, Models, Tools, Rules, etc.)

- **Imports**: `useNavigationListener()`, `useSearchParams`, `TabGroup`, config tab modules, AccountDropdown

- **Architecture**:

  - **Sidebar (left)**: Vertical tab list (desktop: 160px, mobile: 48px compressed)

    - `topTabSections`: Settings, Models, Tools (top of sidebar)

    - `bottomTabSections`: Rules, Keyboard Shortcuts, About (bottom of sidebar)

    - AccountDropdown at very bottom

  - **Main Content (right)**: Renders active tab component

- **Layout**:

  - Desktop (md+): Full sidebar + content visible

  - Mobile (sm-): Sidebar collapsed (w-12), warning alert that sidebar is too narrow (resize to expand)

  - Uses CSS grid: `flex h-full flex-row overflow-hidden`

- **Navigation**:

  - `handleTabClick()` routing: `tab == "back"` → `/`, else → `/config?tab={tabId}`

- **Features**:

  - SearchParams for tab state (survives back/forward)

  - DeprecationBanner overlay on settings page

  - Responsive media query alerts for small screens



### Page Integration Pattern



**Hooks Used Across All Pages**:

- `useNavigationListener()` (Stats, Config) – Sync page state with router history

- `useNavigate()`, `useRouteError()` – Router navigation

- `useContext(IdeMessengerContext)` (Error, Stats) – Request IDE data

- `useSearchParams()` (ConfigPage) – URL state for active tab



**localStorage Access**:

- ErrorPage clears `persist:root` + `inputHistory_chat` on recovery

- ConfigPage persists settings via Redux



**IDE Messaging**:

- Stats page: `stats/getTokensPerDay`, `stats/getTokensPerModel` requests

- Error page: `openUrl` posts for link handling



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

---



## Layer 2A-2: MCP Context Management (MCPManagerSingleton, MCPOauth)



### Purpose

Lifecycle management for Model Context Protocol (MCP) server connections, OAuth2 authentication/token persistence, and MCP client pooling for dynamic tool discovery.



### Key Classes



**MCPManagerSingleton** (`core/context/mcp/MCPManagerSingleton.ts:6-204`)  

Singleton managing the pool of active MCP connections.

- **getInstance()** — Lazy singleton accessor

- **setConnections(servers[], forceRefresh, extras?)** — Sync connection pool to config; compare transport options; trigger refreshConnections if changed (lines 68-109)

- **createConnection(id, options)** — Factory; add new MCPConnection to pool

- **getConnection(id)** — Retrieve MCPConnection by server id

- **refreshConnection(serverId)** — Connect single server; notify onConnectionsRefreshed

- **refreshConnections(force)** — Race all connections against abort signal (lines 155-175)

- **getStatuses()** — Return MCPServerStatus[] + Client objects

- **getPrompt(serverName, promptName, args?)** — Call client.getPrompt()

- **setEnabled(serverId, enabled)** — Toggle server connection on/off

- **shutdown()** — Close all clients; abort; clear connections



**MCPConnectionOauthProvider** (`core/context/mcp/MCPOauth.ts:70-231`)  

Implements MCP SDK's OAuthClientProvider interface; manages OAuth state/token storage via GlobalContext.

- **constructor(oauthServerUrl, ide)** — Initialize with redirect URL (sync default + async IDE override via getExternalUri)

- **redirectUrl** (getter) — Dynamic or localhost:3000

- **getRedirectUrlWithState(state)** — URL with state param

- **ensureRedirectUrl()** — Wait for async init

- **clientMetadata** (getter) — OAuth client info (Continue branding, auth methods, grant types)

- **saveClientInformation(info)** / **clientInformation()** — Persist & retrieve OAuth client metadata

- **saveTokens(tokens)** / **tokens()** — Persist & retrieve OAuth tokens

- **saveCodeVerifier(v)** / **codeVerifier()** — For PKCE flow

- **redirectToAuthorization(url)** — Open browser; start localhost:3000 server if needed

- **clear()** — Erase all stored OAuth data



### Key Functions



**performAuth(serverId, serverUrl, ide)** (`core/context/mcp/MCPOauth.ts:243-271`)  

Begin OAuth flow for a server.

- Generate unique state parameter (UUID)

- Store authenticatingContexts[url] = {serverId, ide, state}

- Map state → serverUrl for callback matching

- Call MCP SDK auth(provider, {serverUrl})



**handleMCPOauthCode(authCode, state?)** (`core/context/mcp/MCPOauth.ts:276-344`)  

OAuth redirect callback handler (invoked from localhost:3000 server).

- Lookup serverUrl from state param (or fallback to single context)

- Close localhost server

- Call MCPManagerSingleton.getInstance().refreshConnection(serverId) if AUTHORIZED

- Clean up authenticatingContexts & stateToServerUrl

- Show error toast on IDE if auth failed



**getOauthToken(mcpServerUrl, ide)** (`core/context/mcp/MCPOauth.ts:233-237`)  

Convenience: retrieve cached access_token for a server.



**removeMCPAuth(serverUrl, ide)** (`core/context/mcp/MCPOauth.ts:346-349`)  

Clear all stored tokens & client info for a server (e.g., on disable).



### OAuth Redirect Server (localhost:3000)



- Created on-demand by `redirectToAuthorization()` if not already listening

- Listens for `GET /?code=...&state=...`

- Parses query params; calls handleMCPOauthCode() asynchronously

- Returns HTML confirmation; closes after response

- Singleton `serverInstance` reused across multiple auth flows



### State & Storage



- **authenticatingContexts** — Map<serverUrl, {serverId, ide, state}> (in-memory, short-lived)

- **stateToServerUrl** — Map<state, serverUrl> (for concurrent auth matching)

- **GlobalContext.mcpOauthStorage** — Persistent:

  - `[serverUrl].clientInformation` — OAuthClientInformationFull

  - `[serverUrl].tokens` — OAuthTokens {access_token, refresh_token, expires_at, ...}

  - `[serverUrl].codeVerifier` — PKCE verifier



### Dependencies



| From | To | Purpose |

|------|----|---------| 

| MCPManagerSingleton | @modelcontextprotocol/sdk | Client class template & MCPConnection transport |

| MCPManagerSingleton | ./MCPConnection | MCPConnection class for lifecycle |

| MCPOauth | @modelcontextprotocol/sdk/client/auth | OAuthClientProvider interface, auth() flow |

| MCPOauth | @modelcontextprotocol/sdk/shared/auth | OAuthClientInformationSchema, OAuthTokensSchema (Zod) |

| MCPOauth | http, url, uuid | localhost:3000 server, state parameter |

| handleMCPOauthCode | MCPManagerSingleton | Dynamic import & refreshConnection() call |

| performAuth | GlobalContext | Store state/tokens |



### Integration Points



1. From `core/config/load.ts` (line ~520): Parse `config.experimental.modelContextProtocolServers` → call `mcpManager.setConnections()`

2. From `core/core.js` (main loop): MCPManagerSingleton.getInstance().getStatuses() → include in runtime config

3. From MCP tool execution: MCPManagerSingleton.getInstance().getConnection(serverId).client.callTool()

4. From OAuth trigger (e.g., tool auth required): performAuth(serverId, url, ide) → localhost:3000 redirect handler

5. On profile/config change: ConfigHandler.reloadConfig() → triggers setConnections() again


## Layer 2A-3: Diff & Edit Streaming (Myers Algorithm, ApplyAbortManager, streamDiffLines)

### Purpose
Core diff algorithm (Myers), lifecycle/cancellation management for apply operations, and streaming diff output generation from LLM completions with prompt template & rule support.

### Key Components

**Myers Diff Algorithm** (`core/diff/myers.ts:1-211`)  
Fast line and character-level diff implementation wrapping the `diff` npm library.
- **myersDiff(oldContent, newContent)** (lines 29-57) — Line-level diff
  - Uses jsdiff with `ignoreNewlineAtEof: true` (treat "foo" and "foo\n" as same)
  - Combines old/new pairs with identical trimmed content → "same"
  - Removes trailing empty old lines
  - Returns DiffLine[] with type ∈ {old, new, same}
- **myersCharDiff(oldContent, newContent)** (lines 59-211) — Character-level diff
  - Track indices: oldIndex, newIndex, oldLineIndex, newLineIndex, oldCharIndexInLine, newCharIndexInLine
  - Handle newlines separately; split on `\n` then process each part
  - Return DiffChar[] with full position metadata
- **convertMyersChangeToDiffLines(change)** (lines 5-19) — jsdiff Change adapter
  - Map change type (added → "new", removed → "old", else → "same")
  - Split value by `\n`, exclude trailing empty line

**ApplyAbortManager** (`core/edit/applyAbortManager.ts:1-37`)  
Singleton managing AbortController instances for cancellable apply operations.
- **getInstance()** — Lazy singleton getter
- **get(id: string)** — Get or create AbortController for apply ID
- **abort(id: string)** — Signal abort + remove controller
- **clear()** — Abort all controllers + clear map

**streamDiffLines()** (`core/edit/streamDiffLines.ts:77-190`)  
Main edit/apply streaming pipeline: render LLM completion → diff stream with rule support.
- **Signature**: `async* streamDiffLines(options, llm, abortController, overridePrompt?, rulesToInclude?)`
- **Input options**:
  - `type: 'edit' | 'apply'`
  - `prefix, highlighted, suffix, input, language`
  - `newCode` (only for apply mode)
- **Pipeline**:
  1. Extract indentation from highlighted or prefix/suffix boundary (lines 87-95)
  2. Construct edit/apply prompt via LLM template OR use override prompt (lines 100-104)
  3. Add system message if rules present (invoke getSystemMessageWithRules) (lines 108-150)
  4. Call recursiveStream(llm, abortController, type, prompt, prediction) for completion (lines 159-165)
  5. Parse completion lines: filter English, extract code blocks, apply line transforms (lines 167-177)
  6. Compute streamDiff(oldLines, newLines) (line 179)
  7. Filter leading/trailing newline insertions (line 180)
  8. Add back indentation if insertion-only edit (lines 182-184)
  9. Yield all DiffLine objects (lines 187-189)

**addIndentation()** (`core/edit/streamDiffLines.ts:61-71`)  
Async generator helper: prepend indentation string to each DiffLine.line.

### Helper Functions

**modelIsInept(model)** — Heuristic: return true if model name excludes "gpt" OR "claude"; applies END-token filtering if inept

**constructEditPrompt(prefix, highlighted, suffix, llm, userInput, language)** — Render edit prompt via llm.promptTemplates?.edit or gptEditPrompt default

**constructApplyPrompt(originalCode, newCode, llm)** — Render apply prompt via llm.promptTemplates?.apply or defaultApplyPrompt

### Architecture

```
LLM Completion Stream
  ↓ [via recursiveStream]
  ↓
Line Parsing & Filtering
  ├─ filterEnglishLinesAtStart()
  ├─ filterCodeBlockLines()
  ├─ stopAtLines()
  ├─ skipLines()
  ├─ removeTrailingWhitespace()
  └─ filterEnglishLinesAtEnd() [if inept model]
  ↓
streamDiff(oldLines, newLines) — Compare old highlighted vs parsed new
  ↓
DiffLine Stream
  ├─ filterLeadingAndTrailingNewLineInsertion()
  ├─ addIndentation() [if insertion-only & has prefix]
  └─ yield → caller
```

### Dependencies

| From | To | Purpose |
|------|----|---------| 
| myersDiff / myersCharDiff | `diff` npm package | jsdiff.diffLines() / diffChars() |
| streamDiffLines | myersDiff (via streamDiff) | Compute line-level deletions/insertions |
| streamDiffLines | getSystemMessageWithRules | Merge rules into system prompt |
| streamDiffLines | gptEditPrompt, defaultApplyPrompt | Default prompt templates |
| streamDiffLines | recursiveStream | Execute LLM completion w/ abort support |
| streamDiffLines | streamLines | Parse completion into string lines |
| streamDiffLines | Line filters | Clean up LLM output (English text, code blocks) |
| ApplyAbortManager | (none; self-contained) | — |

### Integration Points

1. **From Edit Command Handler**: Call `streamDiffLines(options, llm, abortController, undefined, rulesToInclude)`; stream results to IDE
2. **From Apply Handler**: Call `streamDiffLines(options, llm, abortController, overridePrompt?, rulesToInclude)` with type='apply'
3. **From IDE UI**: Create ApplyAbortManager.getInstance().get(applyId) → pass abortController to streamDiffLines
4. **User Cancellation**: IDE calls ApplyAbortManager.getInstance().abort(applyId) → triggers abort signal in streamDiffLines

### Edit vs Apply Distinction

| Mode | Input | Template | Use Case |
|------|-------|----------|----------|
| **edit** | highlighted selection + prefix/suffix context | gptEditPrompt | User asks to modify (non-apply) |
| **apply** | original code + new code | defaultApplyPrompt | Agent mode: validate/merge changes |

---

## Layer 2A-9: Indexing Orchestration (CodebaseIndexer, DocsService)

### Purpose
Asynchronous orchestration of two parallel indexing pipelines: codebase file indexing (multi-strategy: chunk, embeddings, full-text search, code snippets) and documentation site indexing (crawl → chunk → embed → store in LanceDB+SQLite). Both accept config updates, emit progress, and respect pause/abort signals.

### Key Components

**CodebaseIndexer** (`core/indexing/CodebaseIndexer.ts:48-872`)  
Orchestrates multi-strategy indexing of workspace files with batching, pause/abort, and progress tracking.

- **Constructor** (lines 85-104): Initialize with ConfigHandler, IDE, messenger; create PauseToken; set up config listener via `init()`
- **getIndexesToBuild()** (lines 146-210): Query context providers for required index types (chunk, embeddings, FTS, snippets); instantiate matching CodebaseIndex implementations
- **refreshCodebaseIndex(dirs)** (lines 724-769): Main entry point — acquire IndexLock, wait if locked (timeout 10s), yield waitForDBIndex updates, call refreshDirs, release lock
- **refreshDirs(dirs, abortSignal)** (lines 334-457): Walk directories via walkDirAsync, discover files, yield progress, call indexFiles per directory, handle abort/pause, collect and emit warnings
- **refreshFiles(files)** (lines 293-332): Index array of files (not dirs); for each file call refreshFile; emit progress per file; handle empty files list
- **refreshFile(file, workspaceDirs)** (lines 241-291): Single-file update — find in workspace dirs, get file stats, for each index type call getComputeDeleteAddRemove, apply singleFileIndexOps filters, update index
- **indexFiles(directory, files, branch, repoName)** (lines 552-670): Batch-mode indexing — for each codebase index type: compute/delete/add/removeTag ops, batch in filesPerBatch (200), handle and collect sub-errors as warnings, emit progress
- **handleConfigUpdate(configResult)** (lines 840-871): On config change (via ConfigHandler listener), check if embeddings model changed; if yes, trigger full reindex via refreshCodebaseIndex

**PauseToken** (`core/indexing/CodebaseIndexer.ts:36-46`)  
Simple state holder for pause/resume lifecycle control.
- **paused property** (getter/setter): Read/write boolean flag
- Used by CodebaseIndexer to support user pause of indexing (without losing progress)

**DocsService** (`core/indexing/docs/DocsService.ts:167-1292`)  
Orchestrates crawl → chunk → embed → store pipeline for documentation sites with dual LanceDB+SQLite storage and singleton pattern.

- **Constructor & Singleton** (lines 186-229):
  - Instance state: config, ideInfo promise, github token, sqlite DB, Lance table names set, indexing queue (Set), status map
  - `createSingleton(configHandler, ide, messenger)`: Create and store singleton
  - `getSingleton()`: Retrieve global singleton
  - `isInitialized` promise waits for config load
- **indexAndAdd(siteIndexingConfig, forceReindex)** (lines 435-739): Main crawl-to-storage pipeline
  - Check if already indexing (queue.has); deterministic embeddings provider test
  - Instantiate DocsCrawler (local or remote) with maxDepth/useLocalCrawling
  - Crawl pages via async generator (emit progress 0-15%)
  - Chunk into articles (select chunker based on crawler type: markdown vs HTML)
  - Generate embeddings per article (emit progress 50-80%)
  - Finalize: delete old if reindexing, add favicon, store to LanceDB+SQLite (emit progress 85-100%)
  - Remove from failedDocs on success; mark failed on error
- **syncDocs(oldConfig, newConfig, forceReindex)** (lines 927-1020): On config update handler
  - Compare old/new config docs; determine added/changed/removed
  - For changed: reindex if URL/depth changed; update metadata if only title/favicon changed
  - For added: call indexAndAdd (not force reindex)
  - For removed: delete via delete()
  - Emit submenu refresh to GUI
- **retrieveChunks(startUrl, vector, nRetrieve, isRetry)** (lines 824-854): Vector similarity query
  - Get embedding provider and Lance table (embedded table name by provider ID)
  - Search by vector, filter by startUrl, limit nRetrieve
  - Convert LanceDbDocsRow[] → Chunk[]
- **retrieveChunksFromQuery(query, startUrl, nRetrieve)** (lines 742-763): Text query helper
  - Call getEmbeddingsProvider → embed([query])
  - Call retrieveChunks(startUrl, vector, nRetrieve)
- **getEmbeddingsProvider()** (lines 344-364): Provider resolution
  - Return config embeddings if set
  - Else return TransformersJs default if supported (not JetBrains IDE)
  - Else return undefined
- **delete(startUrl)** (lines 1282-1291): Complete removal
  - Delete from queue, abort pending indexing, delete from LanceDB, delete metadata from SQLite, remove from config, refresh GUI
- **abort(startUrl)** (lines 301-314): Cancel indexing for startUrl; mark status as aborted
- **shouldCancel(startUrl, startedWithEmbedder)** (lines 317-332): Check abort or embeddings provider change mid-indexing
- **hasMetadata(startUrl)** (lines 396-408): Query SQLite for indexed doc with current embeddings provider ID
- **listMetadata()** (lines 410-423): List all indexed docs for current embeddings provider
- **getOrCreateSqliteDb()** (lines 878-902): Lazy init of SQLite DB at DocsSqlitePath; create docs table if missing; run migrations
- **getOrCreateLanceTable(initVector, startUrl)** (lines 1071-1116): Lazy init of LanceDB table (name sanitized by provider ID); create if no vector provided; run migrations
- **add(params)** (lines 1221-1224): Internal — add to both LanceDB and SQLite
- **addToLance(params)** (lines 1119-1143): Map chunks + embeddings → LanceDbDocsRow[] (lowercase schema); insert into Lance table
- **addMetadataToSqlite** / **updateMetadataInSqlite** (lines 1145-1184): Insert or update SQLite metadata row

**embedModelsAreEqual(llm1, llm2)** (`core/indexing/docs/DocsService.ts:93-102`)  
Utility to detect embeddings provider changes:
- Compare provider name, title, max chunk size
- Returns boolean; false if either is null/undefined

**LanceDbDocsRow** (`core/indexing/docs/DocsService.ts:41-51`)  
LanceDB table row schema (lowercase field names per LanceDB convention):
- title, starturl, content, path, startline, endline, vector, [key: string]: any

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     INDEXING ORCHESTRATORS                       │
└─────────────────────────────────────────────────────────────────┘

═══ CODEBASE INDEXING FLOW ════════════════════════════════════════

IDE/Config Change
   ↓
ConfigHandler.onConfigUpdate()
   ↓
CodebaseIndexer.handleConfigUpdate()
   ├─ Detect embeddings model change?
   └─ YES → refreshCodebaseIndex(workspaceDirs)
      ↓
   IndexLock.lock() [prevent concurrent writes]
      ↓
   walkDirAsync(dir) → discover files
      ↓
   getIndexesToBuild() → [ChunkIndex, LanceDbIndex, FtsIndex, SnippetsIndex]
      ↓
   For each index type:
      ├─ getComputeDeleteAddRemove(tag, stats, readFile, repoName)
      │    Returns: {compute[], del[], addTag[], removeTag[]}
      ├─ batchRefreshIndexResults(results) [200 files/batch]
      │    ↓
      │    For each batch:
      │       ├─ index.update(tag, subResult, markComplete, repoName)
      │       └─ → async generator (emit desc per operation)
      │       ↓
      │       yield progress update
      └─ markComplete(lastUpdated, UpdateLastUpdated)
         ↓
   IndexLock.unlock()
      ↓
   messenger.send("refreshSubmenuItems")

═══ DOCS INDEXING FLOW ════════════════════════════════════════════

GUI / DocsService.indexAndAdd(siteConfig, forceReindex=false)
   ↓
Queue check: if already indexing → return
   ↓
Embeddings provider test: provider.embed(["continue-test-run"])
   ↓
DocsCrawler.crawl(new URL(startUrl))
   └─ Local walk OR Remote fetch per useLocalCrawling flag
      ↓ [emit progress 0-15%]
      Yields: PageData[] {path, title, content, subpath}
   ↓
For each page:
   ├─ htmlPageToArticleWithChunks() OR markdownPageToArticleWithChunks()
   │    → ArticleWithChunks {article, chunks: Chunk[]}
   └─ → articles: ArticleWithChunks[]
      ↓ [emit progress 50-80%]
   ↓
For each article:
   ├─ provider.embed(chunks.map(c => c.content))
   └─ → embeddings: number[][]
   ↓
Check shouldCancel(startUrl, embedderIdAtStart)
   ├─ Abort status?
   └─ Embeddings provider changed?
   ↓
addToLance(chunks, siteConfig, embeddings)
   ├─ Create table if not exists
   ├─ Map chunks → LanceDbDocsRow[] (vectors, starturl, content, path, lines)
   └─ table.add(rows)
   ↓
addMetadataToSqlite(siteConfig, favicon)
   └─ INSERT docs metadata (title, startUrl, favicon, embeddingsProviderId)
   ↓ [emit progress 100%, status: complete]
   ↓
removeFromFailedGlobalContext(siteConfig)

═══ DUAL DATABASE STRATEGY ════════════════════════════════════════

SQLite (docs/../docs_db.db):
  - Fast metadata lookup (title, startUrl, favicon, embeddingsProviderId)
  - Persists doc ownership info
  - Supports across multiple embeddings providers (provider ID column)

LanceDB (vectordb/<providerId>_docs):
  - Vector index for similarity search
  - Chunk details (content, path, line ranges)
  - Separate table per embeddings provider (sanitized table name)
  - Enables provider switch without data loss
```

### Dependencies

| From | To | Purpose |
|------|----|---------| 
| CodebaseIndexer | ConfigHandler | Config load, update listener |
| CodebaseIndexer | IDE | Read files, get stats, workspace dirs, git branch/repo |
| CodebaseIndexer | ChunkCodebaseIndex, LanceDbIndex, FullTextSearchCodebaseIndex, CodeSnippetsCodebaseIndex | Multi-strategy index implementations |
| CodebaseIndexer | walkDirAsync | Directory traversal with .gitignore/.continueignore |
| CodebaseIndexer | IndexLock (refreshIndex module) | DB lock acquire/release/timeout |
| CodebaseIndexer | messenger (IMessenger) | Send progress/errors to IDE |
| CodebaseIndexer | getComputeDeleteAddRemove | Compute index operations (compute, delete, addTag, removeTag) |
| DocsService | ConfigHandler | Config load, update listener |
| DocsService | IDE | Get IDE info, read files, show toast, git info |
| DocsService | DocsCrawler | Fetch pages (local fs walk OR remote HTTP crawl) |
| DocsService | htmlPageToArticleWithChunks / markdownPageToArticleWithChunks | Parse pages into chunks |
| DocsService | embedModelsAreEqual | Detect embeddings provider changes |
| DocsService | TransformersJsEmbeddingsProvider | Default embeddings fallback |
| DocsService | ILLM (embeddings models) | provider.embed(texts[]) for vector generation |
| DocsService | sqlite3 / open() | SQLite metadata DB |
| DocsService | vectordb (LanceDB) | LanceDB vector index |
| DocsService | editConfigFile | Persist doc config to .continue/config.json |
| DocsService | GlobalContext | failedDocs tracking across sessions |
| DocsService | messenger (IMessenger) | Send status updates, report errors to IDE |

### Integration Points

1. **Config Update Trigger**: 
   - ConfigHandler.onConfigUpdate() calls both CodebaseIndexer.handleConfigUpdate() and DocsService.handleConfigUpdate()
   - Detects embeddings model change → trigger full reindex or sync

2. **Progress Communication**:
   - CodebaseIndexer.updateProgress(IndexingProgressUpdate) → messenger.request("indexProgress", update)
   - DocsService.handleStatusUpdate(IndexingStatus) → messenger.send("indexing/statusUpdate", update)
   - GUI subscribed to both channels; UI shows progress bar

3. **Pause/Abort**:
   - User pauses codebase indexing → CodebaseIndexer.paused = true
   - CodebaseIndexer.refreshDirs() checks pauseToken, yields "Indexing Paused", waits in loop
   - User cancels docs indexing → abort(startUrl) marks status aborted, queue.delete()
   - indexAndAdd checks shouldCancel(startUrl, embedderId) periodically

4. **Error Handling**:
   - CodebaseIndexer: SQLite errors matching errorsRegexesToClearIndexesOn → shouldClearIndexes flag; collect non-fatal index type errors as warnings
   - DocsService: LLMError special handling via messenger.request("reportError", error); other errors mark config as "failed" in GlobalContext

5. **Lock Mechanism** (CodebaseIndexer only):
   - IndexLock.lock(dirs) stores timestamp at lock file
   - If lock stale (>10s since last timestamp), auto-unlock
   - Prevents SQLite concurrent writes across multiple IDE windows

### Performance Considerations

| Aspect | Strategy |
|--------|----------|
| **Batching** | CodebaseIndexer: 200 files per batch (limit RAM usage for local embeddings) |
| **Concurrency** | Both single-threaded async generators; promise gates via IndexLock or docsIndexingQueue |
| **Pause Throttle** | CodebaseIndexer: 100ms loop check; DocsService: 20-100ms per operation based on queue size |
| **Embeddings Calls** | Batch per article to reduce API calls; test embeddings provider at start |
| **Dual DB Trade-off** | SQLite fast metadata, LanceDB fast vectors; both queried independently via startUrl + provider ID filters |

### Edit vs Reindex Distinction

| Flow | Trigger | Scope | Reset DB? |
|------|---------|-------|-----------|
| **Initial Index** | First config load OR added doc in GUI | New doc | Create tables |
| **Re-sync (Config Change)** | Config update (doc URL/depth changed) | Affected doc | No; delete old + insert new |
| **Force Reindex (GUI)** | User clicks "Reindex" | Specific doc | Yes; deleteIndexes() then indexAndAdd(forceReindex=true) |
| **Embeddings Switch** | Embeddings model changed | All docs | Implicit; new provider ID → new LanceDB table |

---




---



## 🚀 GUI APPLICATION SHELL & LAYOUT



### Overview



The top-level GUI application is composed of three layers:



1. **App.tsx**: Entry point; memory-router initialization; provider composition stack

2. **Layout.tsx**: Root container shell; global webview event listeners; IDE-driven state orchestration (edit mode, dialogs, session transitions)

3. **ThemePage.tsx**: Theme debugger page; visual testing and validation of CSS theme variables; platform-specific theme injection (JetBrains vs VS Code)



These files coordinate to provide the global application runtime, connect the IDE (via webview messages), manage major state transitions, and ensure theme consistency across all UI surfaces.



### Key Concepts



**Provider Stack (App.tsx)**  

Wraps the router in a multi-layer context provider tower for global state distribution:

- `VscThemeProvider`: VSCode theme color mapping (hljs token rules→TextMate)

- `MainEditorProvider`: TipTap editor instance and global editor state

- `SubmenuContextProvidersProvider`: Context submenu search & intelligent sorting

- `ParallelListeners`: Master event listener (Redux dispatch wrapper)



**Webview Message Routing (Layout.tsx)**  

`useWebviewListener` handlers respond to IDE events with Redux actions:

- `newSession`: Clear chat, optionally exit edit mode

- `focusContinueInputWithNewSession`: Navigate home, clear session, open new

- `addModel`: Navigate to /models config

- `navigateTo`: Toggle-navigate or force specific route

- `setupLocalConfig`, `setupApiKey`: Open onboarding card with mode

- `focusEdit`: Add current file selection, enter edit mode, focus editor

- `setCodeToEdit`: Update code buffer for edit mode

- `exitEditMode`: Clean exit from edit mode



**Theme State (ThemePage.tsx)**  

Platform-specific logic:

- **JetBrains**: Request colors via `jetbrains/getColors` message; inject into DOM; mark missing

- **VS Code**: Read computed CSS variables from document root; validate against `THEME_COLORS` defaults



### File Details



**App.tsx** (66 lines)

- `createMemoryRouter`: Route tree with Layout as root, ErrorPage error boundary

- Routes: `/` (chat), `/index.html` (chat), `/history`, `/stats`, `/config`, `/theme`

- App component: Nests RouterProvider inside provider stack

- Exports: `App` (default)



**Layout.tsx** (234 lines)

- `LayoutTopDiv`: Styled scrollable container with stable scrollbar gutter

- `GridDiv`: Template grid (1fr auto) for main content + error indicator

- `Layout` component:

  - useWebviewListener hooks (8 handlers): newSession, focusContinueInputWithNewSession, addModel, navigateTo, setupLocalConfig, setupApiKey, focusEdit, setCodeToEdit, exitEditMode

  - Copy-to-clipboard Cmd+C handler

  - Onboarding card auto-open on new user + home route

  - Wraps Outlet in LocalStorageProvider → AuthProvider → custom grid div

  - Renders TextDialog (dispatch setShowDialog), Outlet (route content), FatalErrorIndicator (non-home only), tooltip portal div

- Exports: `Layout` (default)



**ThemePage.tsx** (232 lines)

- `ThemeTailwindClassExample` component (19-55):

  - Props: colorName, varNames, defaultColor, isMissing?

  - Renders: color name + CSS var list + default hex in 3-column layout

  - Styling: Text error red if missing

- `ThemePage` component (57-232):

  - State: listToggled (demo), missingVars (`useState<string[]>`)

  - refreshColors():

    - JetBrains: Call `jetbrains/getColors`, invoke `setDocumentStylesFromTheme(result.content)`, collect missing

    - VS Code: Iterate `THEME_COLORS`, read getComputedStyle(), mark CSS vars not found

  - useEffect: Call refreshColors() on mount

  - JSX: Back to Chat link, theme tester UI (30+ color swatches), Missing Colors section, All Theme Colors grid

  - Button: Refresh Missing Colors

  - Conditional button: Clear Theme Cache (JetBrains only)

- Exports: `ThemePage` (default)



### Dependencies



`App.tsx` imports:

- `react-router-dom`: RouterProvider, createMemoryRouter

- `./components/Layout`

- Page components: ConfigPage, ErrorPage, Chat, History, Stats, ThemePage

- Providers: MainEditorProvider, SubmenuContextProvidersProvider, VscThemeProvider, ParallelListeners

- `./util/navigation`: ROUTES



`Layout.tsx` imports:

- `react`: useContext, useEffect

- `react-router-dom`: Outlet, useLocation, useNavigate

- `styled-components`

- Contexts: IdeMessengerContext, AuthProvider, LocalStorageProvider

- Hooks: useWebviewListener, useMainEditor, useOnboardingCard, useAppDispatch, useAppSelector

- Redux: setCodeToEdit, setShowDialog, enterEdit, exitEdit, saveCurrentSession

- UI: CustomScrollbarDiv, FatalErrorIndicator, TextDialog, OSRContextMenu

- Utils: fontSize, isMetaEquivalentKeyPressed, ROUTES



`ThemePage.tsx` imports:

- `react`: useState, useContext, useEffect, useMemo

- `react-router-dom`: useNavigate

- `@heroicons/react/24/outline`: CheckCircleIcon, ExclamationCircleIcon, ExclamationTriangleIcon

- Contexts: IdeMessengerContext

- Components: Button

- Utils: isJetBrains, ROUTES

- Theme: clearThemeLocalCache, setDocumentStylesFromTheme, THEME_COLORS, varWithFallback



### Call Chain Example: "setCodeToEdit" Event



1. IDE sends message: `{messageType: "setCodeToEdit", payload: "const x = 1;"}`

2. `ParallelListeners` receives & dispatches `PostedCoreMessage` to Redux

3. `Layout.tsx` `useWebviewListener("setCodeToEdit")` fires async handler

4. Handler calls `dispatch(setCodeToEdit({codeToEdit: payload}))`

5. `editState` slice updates `state.editState.codeToEdit`

6. `TipTapEditor` (wrapped in MainEditorProvider) reflects change; user may modify

7. Editor submit → `streamEditThunk` → Core receives code



### Integration Points



1. **App.tsx ↔ React Router**:

   - Setup memory-based router with catch-all error boundary

   - Lazy-load pages by route

   - Provide `useNavigate` hook to all child components



2. **Layout.tsx ↔ IDE (Webview)**:

   - Hook all major IDE-originated events

   - Dispatch Redux actions (edit mode, session, dialog)

   - Trigger theming/editor focus/copy operations



3. **Layout.tsx ↔ Redux Store**:

   - Listen to state changes (showDialog, isInEdit)

   - Dispatch thunks (saveCurrentSession, enterEdit, exitEdit)

   - Update slices (editState, session, ui)



4. **ThemePage.tsx ↔ IDE**:

   - Request platform-specific theme colors (JetBrains: jetbrains/getColors)

   - Inject CSS variables into document root

   - Validate against fallback defaults; report missing



5. **ParallelListeners ↔ Layout**:

   - Core message events trigger webview listeners

   - Listeners dispatch Redux thunks

   - UI re-renders on state changes

---



## 🆎 BOOTSTRAP & ENTRY POINTS



### Overview



Two minimal files form the entry layer that bootstraps the entire Continue GUI application:



1. **index.html** (13 lines): HTML5 document that provides the DOM mount point and loads the TypeScript entry

2. **main.tsx** (24 lines): React DOM initialization, Redux store setup, and PersistGate wrapping



Together, they form the **lowest-level entry surface** for the webview-rendered application, connecting the static HTML page to the React component tree and Redux state management.



### Key Concepts



**HTML Page Structure (index.html)**

- Single-page application HTML template

- Mount point: `<div id="root">` (line 10)

- Script loader: Vite module script pointing to `/src/main.tsx` (line 11)

- Favicon: Play button image `/play_button.png` (line 5)

- Title: "Continue" (displayed in IDE webview tab)

- Minimal head metadata (UTF-8, viewport, favicon)



**React DOM Entry (main.tsx)**

- **IIFE async pattern** (line 9): Wraps entire initialization in async closure for future async operations

- **Container lookup** (line 10): Gets `#root` element from HTML, typed as HTMLElement

- **React 18 root creation** (line 13): Uses `ReactDOM.createRoot(container)` for concurrent features

- **Provider nesting** (lines 16-21):

  - `React.StrictMode`: Development checks (unmount/remount, warnings on lifecycle issues)

  - `Provider` from react-redux: Injects Redux store into all child components

  - `PersistGate` from redux-persist: Delays rendering until persisted state hydrates (loading={null} = no fallback UI)

  - `App`: Main application component (router, layouts, pages)



### File Details



**index.html** (13 lines)

- Lines 1-7: Standard HTML5 boilerplate (doctype, lang, meta, favicon, viewport, title)

- Line 10: React mount point `<div id="root"></div>`

- Line 11: Deferred module script loading `src/main.tsx` (Vite will transform to bundle)

- No inline styles or content; completely delegated to React



**main.tsx** (24 lines)

- Lines 1-7: Imports (React, ReactDOM, redux-persist, redux, App, styles)

- Lines 9-24: IIFE async function containing:

  - Line 10: Query and type-cast `document.getElementById("root")`

  - Line 13: Create React 18 root via `ReactDOM.createRoot(container)`

  - Lines 15-22: JSX tree with provider nesting and final `root.render()` call



### Dependencies



**index.html** depends on:

- Vite build system (processes `src/main.tsx` module script)

- Browser DOM API (getElementById, script module loading)

- Favicon asset: `/play_button.png`



**main.tsx** imports:

- `react`: React, StrictMode

- `react-dom/client`: ReactDOM.createRoot

- `react-redux`: Provider

- `redux-persist/integration/react`: PersistGate

- `./App`: Application root component (see GUI APPLICATION SHELL section)

- `./index.css`: Global styles (animations, scrollbars, theme)

- `./redux/store`: setupStore(), persistor, store (see REDUX STORE section)



### Provider Stack Initialization Sequence



1. **HTML loads** (`index.html`)

   - Browser parses HTML; creates `<div id="root"></div>`

   - Encounters deferred module script: `/src/main.tsx`



2. **main.tsx IIFE executes**

   - Async function begins

   - Queries DOM for `#root` container

   - Creates React 18 root



3. **Provider nesting (inside-out initialization)**

   - React.StrictMode wraps all

   - Provider (redux) initializes store (loaded from localStorage via redux-persist)

   - PersistGate hydrates persisted state before rendering children

   - App component mounts once all state ready



4. **App mounts**

   - Router setup (createMemoryRouter in App.tsx)

   - Layout component mounts (global listeners, webview setup)

   - First route (/ or /) renders Chat or home page

   - All contexts available: VscThemeProvider, MainEditorProvider, SubmenuContextProviders, IdeMessenger, Auth, LocalStorage



### Integration Points



1. **index.html ↔ Webview Host (IDE)**:

   - IDE loads HTML in webview container

   - Favicon displayed in tab

   - Title "Continue" shown in webview tab

   - Script module loading handled by Vite dev server or bundled assets



2. **main.tsx ↔ Redux Store**:

   - Retrieves `store` and `persistor` from `./redux/store`

   - PersistGate hydrates persisted slices (ui, session, profiles, etc.)

   - Provider gives all descendants access to `useAppSelector` and `useAppDispatch`



3. **main.tsx ↔ Global React Context**:

   - StrictMode enables concurrent features and development warnings

   - App component accesses all providers set up in App.tsx (VscTheme, MainEditor, SubmenuContextProviders, ParallelListeners)



4. **main.tsx ↔ CSS Assets**:

   - Imports `./index.css` (global styles with Tailwind directives, custom animations)

   - Applied to all rendered components via React



### Mount Flow Diagram



```

┌─────────────────────────────────────────┐

│  Browser / IDE Webview                  │

│  ├─ index.html                          │

│  │  └─ <div id="root"></div>           │

│  │  └─ <script src="/src/main.tsx">    │

│  └─ (loads main.tsx via Vite)          │

└─────────────────────────────────────────┘

           ↓

┌─────────────────────────────────────────┐

│  main.tsx IIFE async                    │

│  ├─ document.getElementById("root")     │

│  ├─ ReactDOM.createRoot(container)      │

│  └─ root.render(JSX tree)               │

└─────────────────────────────────────────┘

           ↓

┌─────────────────────────────────────────┐

│  React.StrictMode                       │

│  ├─ Provider (Redux store)              │

│  │  └─ PersistGate (hydrate state)      │

│  │     └─ App (router + layout)         │

│  └─ (all child components mount)        │

└─────────────────────────────────────────┘

           ↓

┌─────────────────────────────────────────┐

│  GUI Rendered                           │

│  ├─ Layout (root shell)                 │

│  ├─ Outlet (current route: Chat/etc)    │

│  └─ All contexts ready (IDE, Theme)     │

└─────────────────────────────────────────┘

```



### Lifecycle Implications



- **Cold Start**: HTML loaded → main.tsx IIFE runs → Redux hydrates → App mounts → First route renders (Chat is default)

- **Hot Reload** (dev): CSS/JS changes re-import; React hot-module-replacement re-renders tree; Redux state persists

- **Persist Rehydration**: PersistGate prevents flashing of old UI; waits for localStorage restore before rendering

- **StrictMode Effects**: In development, components mount/unmount/remount twice to catch side-effect cleanup issues


