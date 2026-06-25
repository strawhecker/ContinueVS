# Protocol Reference

> Generated from ContinueTranslator output for Continue v2.0.0 `core/protocol/**`.
> Reference for Phase 3 message handler implementation in VSIXProject1.
> Review: `output\review-018.md` â€” verdict: **GAPS FOUND**.
> Protocol entry form: `[PayloadType, ResponseType]`. Direction: `A â† B` = B sends, A handles.

---

## Supporting Types

### `OnboardingModes` (Enum â€” `Protocol/core.Enums.cs`)
**Values:** `API_KEY`, `LOCAL`
**Gaps:** String literal initialisers â€” invalid C#. Emitter must map to integer-backed enum with `[EnumMember(Value = "...")]`.

### `ListHistoryOptions` (Interface â€” `Protocol/core.Interfaces.cs`)
| Name | C# Type | Notes |
|---|---|---|
| offset | double? | optional pagination offset |
| limit | double? | optional max results |
| workspaceDirectory | string? | optional workspace filter |
**Gaps:** Properties emitted with raw `number \| undefined` / `string \| undefined` union syntax.

### `GetGhTokenArgs` (Interface â€” `Protocol/ide.Interfaces.cs`)
| Name | C# Type | Notes |
|---|---|---|
| force | bool? | force token refresh |
**Gaps:** Property emitted with raw `boolean \| undefined`.

### `ErrorWebviewMessage` (Interface â€” `Protocol/util.Interfaces.cs`)
| Name | C# Type | Notes |
|---|---|---|
| status | string | TS literal `"error"` â€” treat as string constant |
| error | string | error text |
| done | bool | TS literal `true` â€” treat as bool constant |
**Gaps:** TS string/bool literal types not mapped; properties are raw TS syntax.

### `SuccessWebviewSingleMessage<T>` (Interface â€” `Protocol/util.Interfaces.cs`)
| Name | C# Type | Notes |
|---|---|---|
| done | bool | TS literal `true` |
| status | string | TS literal `"success"` |
| content | T | response payload |
**Gaps:** Not declared generic in output; TS literal types not mapped.

### `Message<T>` (Interface â€” `Protocol/Messenger/index.Interfaces.cs`)
| Name | C# Type | Notes |
|---|---|---|
| messageType | string | message discriminator key |
| messageId | string | correlation ID |
| data | T | typed payload |
**Gaps:** None on this type itself.

### `IMessenger` (Interface â€” `Protocol/Messenger/index.Interfaces.cs`)
Methods: `onError`, `send<T>`, `on<T>`, `request<T>`, `invoke<T>`
**Gaps:** All method signatures contain unresolved TS indexed-access types (`FromProtocol[T][N]`,
`ToProtocol[T][N]`); `on<T>` has an unclosed parenthesis â€” broken C# syntax throughout.

## Composite Protocol Aliases

All emitted as empty `public static class â€¦Alias {}` stubs â€” TS intersection types have no direct C# equivalent.

| Alias | Composed From |
|---|---|
| `ToIdeProtocol` | ToIdeFromWebviewProtocol & ToIdeFromCoreProtocol |
| `FromIdeProtocol` | ToWebviewFromIdeProtocol & ToCoreFromIdeProtocol & ToWebviewOrCoreFromIdeProtocol |
| `ToWebviewProtocol` | ToWebviewFromIdeProtocol & ToWebviewFromCoreProtocol & ToWebviewOrCoreFromIdeProtocol |
| `FromWebviewProtocol` | ToIdeFromWebviewProtocol & ToCoreFromWebviewProtocol |
| `ToCoreProtocol` | ToCoreFromIdeProtocol & ToCoreFromWebviewProtocol & ToWebviewOrCoreFromIdeProtocol |
| `FromCoreProtocol` | ToWebviewFromCoreProtocol & ToIdeFromCoreProtocol |

---

## Messages: Core â† IDE / Webview (`ToCoreFromIdeOrWebviewProtocol`)

### `ping`
**Payload:** `string`
**Expected Response:** `string`
**Gaps:** None

### `abort`
**Payload:** none
**Expected Response:** void
**Gaps:** None

### `cancelApply`
**Payload:** none
**Expected Response:** void
**Gaps:** None

### `history/list`
**Payload:** `ListHistoryOptions` (see Supporting Types)
**Expected Response:** `BaseSessionMetadata[]`
**Gaps:** None on this entry; payload type has its own gaps.

### `history/delete`
| Name | C# Type | Notes |
|---|---|---|
| id | string | session ID |
**Expected Response:** void
**Gaps:** None

### `history/load`
| Name | C# Type | Notes |
|---|---|---|
| id | string | session ID |
**Expected Response:** `Session`
**Gaps:** None

### `history/save`
**Payload:** `Session`
**Expected Response:** void
**Gaps:** None

### `history/clear`
**Payload:** none
**Expected Response:** void
**Gaps:** None

### `config/addOpenAiKey`
**Payload:** `string` (API key)
**Expected Response:** void
**Gaps:** None

### `config/ideSettingsUpdate`
**Payload:** `IdeSettings`
**Expected Response:** void
**Gaps:** None

### `config/deleteModel`
| Name | C# Type | Notes |
|---|---|---|
| title | string | model title to remove |
**Expected Response:** void
**Gaps:** None

### `config/getSerializedProfileInfo`
**Payload:** none
**Expected Response:** inline object `{ result, profileId, profiles }` â€” no named C# type emitted
**Gaps:** Inline response object not mapped to a named C# type.

### `autocomplete/complete`
**Payload:** `AutocompleteInput`
**Expected Response:** `string[]`
**Gaps:** None

### `autocomplete/cancel`
**Payload:** none
**Expected Response:** void
**Gaps:** None

### `autocomplete/accept`
| Name | C# Type | Notes |
|---|---|---|
| completionId | string | ID of the accepted completion |
**Expected Response:** void
**Gaps:** None

### `llm/complete`
| Name | C# Type | Notes |
|---|---|---|
| prompt | string | â€” |
| completionOptions | LLMFullCompletionOptions | â€” |
| title | string | model title |
**Expected Response:** `string`
**Gaps:** None

### `llm/streamChat`
| Name | C# Type | Notes |
|---|---|---|
| messages | ChatMessage[] | chat history |
| completionOptions | LLMFullCompletionOptions | â€” |
| title | string | model title |
| messageOptions | MessageOption? | optional |
**Expected Response:** `IAsyncEnumerable<ChatMessage>` (TS `AsyncGenerator<ChatMessage, PromptLog>`)
**Gaps:** TS `AsyncGenerator` not fully mapped; `PromptLog` final return value is dropped.

### `auth/getAuthUrl`
| Name | C# Type | Notes |
|---|---|---|
| useOnboarding | bool | â€” |
**Expected Response:** `string` (url field from inline `{ url: string }`)
**Gaps:** Inline response object not mapped to a named C# type.

### `onboarding/complete`
**Payload:** `CompleteOnboardingPayload`
**Expected Response:** void
**Gaps:** None

### `tts/kill`
**Payload:** none
**Expected Response:** void
**Gaps:** None

### `isItemTooBig`
| Name | C# Type | Notes |
|---|---|---|
| item | ContextItemWithId | â€” |
**Expected Response:** `bool`
**Gaps:** None

### `mdm/setLicenseKey`
| Name | C# Type | Notes |
|---|---|---|
| licenseKey | string | â€” |
**Expected Response:** `bool`
**Gaps:** None

### `config/addModel`
| Name | C# Type | Notes |
|---|---|---|
| model | object | `SerializedContinueConfig["models"][number]` â€” inline model config |
| role | string? | optional `keyof ExperimentalModelRoles` |
**Expected Response:** void
**Gaps:** `SerializedContinueConfig["models"][number]` is an indexed-access type â€” emitted as `object`.|

### `config/addLocalWorkspaceBlock`
| Name | C# Type | Notes |
|---|---|---|
| blockType | string | `BlockType` enum value |
| baseFilename | string? | optional filename |
**Expected Response:** void
**Gaps:** `BlockType` not yet emitted to ContinueProtocol.cs.

### `config/addGlobalRule`
| Name | C# Type | Notes |
|---|---|---|
| baseFilename | string? | optional |
**Expected Response:** void
**Gaps:** Payload is `undefined \| { baseFilename?: string }` â€” treated as optional object.

### `config/deleteRule`
| Name | C# Type | Notes |
|---|---|---|
| filepath | string | path to the rule file |
**Expected Response:** void
**Gaps:** None

### `config/newPromptFile`
**Payload:** none
**Expected Response:** void
**Gaps:** None

### `config/newAssistantFile`
**Payload:** none
**Expected Response:** void
**Gaps:** None

### `config/refreshProfiles`
| Name | C# Type | Notes |
|---|---|---|
| reason | string? | optional refresh reason |
| selectProfileId | string? | optional profile to select |
**Expected Response:** void
**Gaps:** Payload is `undefined \| { reason?: string; selectProfileId?: string }` â€” optional object.

### `config/openProfile`
| Name | C# Type | Notes |
|---|---|---|
| profileId | string? | ID of profile to open (nullable) |
**Expected Response:** void
**Gaps:** None

### `config/updateSharedConfig`
**Payload:** `SharedConfigSchema`
**Expected Response:** `SharedConfigSchema`
**Gaps:** `SharedConfigSchema` not yet emitted to ContinueProtocol.cs.

### `config/updateSelectedModel`
| Name | C# Type | Notes |
|---|---|---|
| profileId | string | â€” |
| role | string | `ModelRole` enum value |
| title | string? | model title (nullable) |
**Expected Response:** `GlobalContextModelSelections`
**Gaps:** `ModelRole` and `GlobalContextModelSelections` not yet emitted to ContinueProtocol.cs.

### `context/getContextItems`
| Name | C# Type | Notes |
|---|---|---|
| name | string | context provider name |
| query | string | query string |
| fullInput | string | full user input |
| selectedCode | RangeInFile[] | selected code ranges |
| isInAgentMode | bool | â€” |
**Expected Response:** `ContextItemWithId[]`
**Gaps:** None

### `context/getSymbolsForFiles`
| Name | C# Type | Notes |
|---|---|---|
| uris | string[] | file URIs to get symbols for |
**Expected Response:** `FileSymbolMap`
**Gaps:** `FileSymbolMap` not yet emitted to ContinueProtocol.cs.

### `context/loadSubmenuItems`
| Name | C# Type | Notes |
|---|---|---|
| title | string | context provider title |
**Expected Response:** `ContextSubmenuItem[]`
**Gaps:** `ContextSubmenuItem` not yet emitted to ContinueProtocol.cs.

### `context/addDocs`
**Payload:** `SiteIndexingConfig`
**Expected Response:** void
**Gaps:** `SiteIndexingConfig` not yet emitted to ContinueProtocol.cs.

### `context/removeDocs`
| Name | C# Type | Notes |
|---|---|---|
| startUrl | string | `Pick<SiteIndexingConfig, "startUrl">` |
**Expected Response:** void
**Gaps:** None

### `context/indexDocs`
| Name | C# Type | Notes |
|---|---|---|
| reIndex | bool | whether to force a full re-index |
**Expected Response:** void
**Gaps:** None

### `llm/listModels`
| Name | C# Type | Notes |
|---|---|---|
| title | string | model title |
**Expected Response:** `string[]?` (`string[] \| undefined`)
**Gaps:** None

### `llm/compileChat`
| Name | C# Type | Notes |
|---|---|---|
| messages | ChatMessage[] | chat history |
| options | LLMFullCompletionOptions | â€” |
**Expected Response:** `CompiledMessagesResult`
**Gaps:** `CompiledMessagesResult` not yet emitted to ContinueProtocol.cs.

---

## Messages: IDE â† Webview / Core (`ToIdeFromWebviewOrCoreProtocol`)

### `getWorkspaceDirs`
**Payload:** none
**Expected Response:** `string[]`
**Gaps:** None

### `getIdeInfo`
**Payload:** none
**Expected Response:** `IdeInfo`
**Gaps:** None

### `getIdeSettings`
**Payload:** none
**Expected Response:** `IdeSettings`
**Gaps:** None

### `readFile`
| Name | C# Type | Notes |
|---|---|---|
| filepath | string | â€” |
**Expected Response:** `string`
**Gaps:** None

### `writeFile`
| Name | C# Type | Notes |
|---|---|---|
| path | string | â€” |
| contents | string | â€” |
**Expected Response:** void
**Gaps:** `Task<void>` not valid C# â€” should be `Task`.

### `saveFile`
| Name | C# Type | Notes |
|---|---|---|
| filepath | string | â€” |
**Expected Response:** void
**Gaps:** `Task<void>` not valid C# â€” should be `Task`.

### `fileExists`
| Name | C# Type | Notes |
|---|---|---|
| filepath | string | â€” |
**Expected Response:** `bool`
**Gaps:** None

### `openFile`
| Name | C# Type | Notes |
|---|---|---|
| path | string | â€” |
**Expected Response:** void
**Gaps:** `Task<void>` not valid C# â€” should be `Task`.

### `openUrl`
**Payload:** `string` (URL)
**Expected Response:** void
**Gaps:** `Task<void>` not valid C# â€” should be `Task`.

### `getOpenFiles`
**Payload:** none
**Expected Response:** `string[]`
**Gaps:** None

### `isTelemetryEnabled`
**Payload:** none
**Expected Response:** `bool`
**Gaps:** None

### `isWorkspaceRemote`
**Payload:** none
**Expected Response:** `bool`
**Gaps:** None

### `getUniqueId`
**Payload:** none
**Expected Response:** `string`
**Gaps:** None

### `getBranch`
| Name | C# Type | Notes |
|---|---|---|
| dir | string | workspace directory |
**Expected Response:** `string`
**Gaps:** None

### `showToast`
**Payload:** `Parameters<IDE["showToast"]>` (TS utility â€” unresolved)
**Expected Response:** `Awaited<ReturnType<IDE["showToast"]>>` (TS utility â€” unresolved)
**Gaps:** Payload and response use unresolved TS conditional/utility types.

---

## Messages: Webview â† IDE / Core (`ToWebviewFromIdeOrCoreProtocol`)

### `configUpdate`
| Name | C# Type | Notes |
|---|---|---|
| result | ConfigResult\<BrowserSerializedContinueConfig\> | serialized config |
| profileId | string? | current profile (nullable) |
| profiles | ProfileDescription[] | all available profiles |
**Expected Response:** void (push event â€” no reply)
**Gaps:** Inline payload object not mapped to a named C# type.

### `indexProgress`
**Payload:** `IndexingProgressUpdate`
**Expected Response:** void
**Gaps:** None

### `getDefaultModelTitle`
**Payload:** none
**Expected Response:** `string?`
**Gaps:** None

### `setTTSActive`
**Payload:** `bool`
**Expected Response:** void
**Gaps:** None

### `didChangeActiveTextEditor` (also `ToWebviewOrCoreFromIdeProtocol`)
| Name | C# Type | Notes |
|---|---|---|
| filepath | string | path of newly active editor |
**Expected Response:** void
**Gaps:** None

### `didChangeSelectedProfile` (Core â† Webview only â€” `ToCoreFromWebviewProtocol`)
| Name | C# Type | Notes |
|---|---|---|
| id | string | selected profile ID |
**Expected Response:** void
**Gaps:** None

---

## Remaining Types

The following message names were identified but not fully documented.
Payload shapes are available in the block-comment bodies of the TypeAlias output files.
Emitter gaps have been fixed (TODO-025). context/, config/, and llm/ groups are now fully documented above.

### Core â† IDE/Webview (additional)
`devdata/log`, `history/share`, `mcp/reloadServer`,
`mcp/setServerEnabled`, `mcp/getPrompt`, `mcp/startAuthentication`, `mcp/removeAuthentication`,
`nextEdit/predict`, `nextEdit/reject`,
`nextEdit/accept`, `nextEdit/startChain`, `nextEdit/deleteChain`, `nextEdit/isChainAlive`,
`nextEdit/queue/getProcessedCount`, `nextEdit/queue/dequeueProcessed`,
`nextEdit/queue/processOne`, `nextEdit/queue/clear`, `nextEdit/queue/abort`,
`chatDescriber/describe`, `conversation/compact`,
`stats/getTokensPerDay`, `stats/getTokensPerModel`, `streamDiffLines`, `getDiffLines`,
`index/setPaused`, `index/forceReIndex`, `index/indexingProgressBarInitialized`,
`files/changed`, `files/opened`, `files/created`, `files/deleted`, `files/closed`,
`files/smallEdit`, `indexing/reindex`, `indexing/abort`, `indexing/setPaused`,
`docs/getSuggestedDocs`, `docs/initStatuses`, `docs/getDetails`, `docs/getIndexedPages`,
`addAutocompleteModel`, `tools/call`, `tools/evaluatePolicy`, `tools/preprocessArgs`,
`clipboardCache/add`, `process/markAsBackgrounded`, `process/isBackgrounded`,
`process/killTerminalProcess`, `models/fetch`
### IDE â† Webview/Core (additional)
`runCommand`, `getSearchResults`, `getFileResults`, `subprocess`, `getProblems`,
`getCurrentFile`, `getPinnedFiles`, `showLines`, `removeFile`, `showVirtualFile`,
`readRangeInFile`, `getDiff`, `getTerminalContents`, `getDebugLocals`,
`getTopLevelCallStackSources`, `getAvailableThreads`, `getTags`, `readSecrets`,
`writeSecrets`, `gotoDefinition`, `gotoTypeDefinition`, `getSignatureHelp`,
`getReferences`, `getDocumentSymbols`, `getFileStats`, `getGitRootPath`,
`listDir`, `getRepoName`, `reportError`, `closeSidebar`

### IDE â† Webview only (`ToIdeFromWebviewProtocol` additions)
`applyToFile`, `overwriteFile`, `showTutorial`, `showFile`, `toggleDevTools`, `reloadWindow`,
`focusEditor`, `toggleFullScreen`, `insertAtCursor`, `copyText`, `acceptDiff`, `rejectDiff`,
`edit/sendPrompt`, `edit/addCurrentSelection`, `edit/clearDecorations`, `session/share`,
`jetbrains/isOSREnabled`, `jetbrains/onLoad`, `jetbrains/getColors`, `vscode/openMoveRightMarkdown`

### Webview â† IDE only (`ToWebviewFromIdeProtocol` additions)
`setInactive`, `newSessionWithPrompt`, `userInput`, `focusContinueInput`,
`focusContinueInputWithoutClear`, `focusContinueInputWithNewSession`, `highlightedCode`,
`setCodeToEdit`, `navigateTo`, `addModel`, `focusContinueSessionId`, `newSession`,
`setTheme`, `setColors`, `setupApiKey`, `setupLocalConfig`, `incrementFtc`,
`openOnboardingCard`, `applyCodeFromChat`, `updateApplyState`, `exitEditMode`, `focusEdit`,
`addToChat`, `jetbrains/editorInsetRefresh`, `jetbrains/isOSREnabled`

### Webview â† IDE/Core (additional)
`indexing/statusUpdate`, `refreshSubmenuItems`, `didCloseFiles`, `isContinueInputFocused`,
`addContextItem`, `getWebviewHistoryLength`, `getCurrentSessionId`, `sessionUpdate`,
`toolCallPartialOutput`, `jetbrains/setColors`

### Messenger Implementation Classes
`InProcessMessenger`, `MessageIde`, `ReverseMessageIde` â€” all methods are TODO stubs
with unresolved TS type signatures. See `Protocol/Messenger/` for details.
