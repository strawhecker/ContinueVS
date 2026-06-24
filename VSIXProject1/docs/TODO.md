# TODO — Ordered Action List

One item per agent session. Commit after each item. Remove completed items. tools and docs moved to VSIXProject1.

---

### TODO-009
**Create IR model classes**
In `ContinueTranslator.Core/IR/`:
`TsFile.cs`, `TsClass.cs`, `TsInterface.cs`, `TsEnum.cs`, `TsFunction.cs`, `TsTypeRef.cs`
All POCOs — records or simple classes with properties only.
Reference: `docs/translator-design.md`, ADR-003

---

### TODO-010
**Create SourceFetcher.cs**
Downloads `https://github.com/continuedev/continue/archive/refs/tags/v{tag}.zip`
Extracts to a temp directory.
Returns list of `.ts` file paths after filtering out tests, vscode extension, gui.
Must be `partial`, under 400 lines.

---

### TODO-011
**Create parse.mjs**
Embedded resource in `ContinueTranslator.Core`.
Uses ts-morph to walk each `.ts` file and emit a JSON array of IR-compatible objects.
Covers: interfaces, classes, enums, functions, type aliases.

---

### TODO-012
**Create TsParser.cs**
Extracts `parse.mjs` to temp, runs `node parse.mjs <files>`, reads stdout JSON.
Deserializes to IR types.
Must be `partial`, under 400 lines.

---

### TODO-013
**Create mapping tables**
Files: `mappings/node-api.json`, `mappings/npm-packages.json`, `mappings/types.json`
Initial content as documented in `docs/translator-design.md`.

---

### TODO-014
**Create MappingEngine + map readers**
`Mapping/NodeApiMap.cs`, `Mapping/NpmPackageMap.cs`, `Mapping/TypeMap.cs`
`Mapping/MappingEngine.cs` — orchestrates all three over the IR.
All `partial`, under 400 lines each.

---

### TODO-015
**Create CsEmitter.cs**
Roslyn SyntaxFactory. Walks mapped IR. Emits `partial` classes under 400 lines.
Splits long classes by method into `ClassName.MethodGroup.cs` files.

---

### TODO-016
**Create ProjectEmitter.cs**
Generates `ContinueCore.csproj` with NuGet references discovered during mapping.

---

### TODO-017
**Wire CLI entry point**
`Program.cs` — parse `--tag` and `--out` args, call pipeline, report results.

---

### TODO-018
**Test run: Continue v2.0.0 core/protocol/**
Run translator targeting only `core/protocol/**` from tag `v2.0.0`.
Review generated C# protocol types.
Fix any parser or emitter gaps found.

---

## Phase 3 — Implement message handlers in VSIXProject1

### TODO-019
**Study protocol output**
After TODO-018: read the generated C# to understand all MessageType values.
Document each in a new `docs/protocol.md` file (message name, payload shape, expected response).

---

### TODO-020
**Create IMessageHandler interface and MessageDispatcher**
`VSIXProject1/Handlers/IMessageHandler.cs`
`VSIXProject1/Handlers/MessageDispatcher.cs`
Routes incoming WebView messages to the correct handler by MessageType.
Wire into `ContinueToolWindowControl.OnWebMessageReceived`.

---

*Further handler TODO items will be added after TODO-019 is complete
and the full protocol is known.*
