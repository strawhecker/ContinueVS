# TODO — Ordered Action List

One item per agent session. Commit after each item. Remove completed items.

---

## Phase 1 — Clean up VSIXProject1

### TODO-005
**Vendor Continue GUI assets and simplify GuiExtractor.cs**

Step 1 — Vendor the assets (manual, done once by developer):
- Identify the pinned Continue release to target (start with the latest stable tag on
  https://github.com/continuedev/continue/releases).
- Download that release's VSIX from the Marketplace or GitHub releases page.
- Extract the `extension/gui/` subtree from the VSIX (it is a ZIP) into
  `VSIXProject1/gui/`, preserving relative paths.
- In `VSIXProject1.csproj`, add a glob `<Content>` item for all files under `gui/`
  with `<IncludeInVSIX>true</IncludeInVSIX>`.
- Record the pinned Continue version tag in `docs/adr/ADR-006-gui-assets-vendored.md`
  under a new **Pinned version** section.

Step 2 — Simplify GuiExtractor.cs (code change):
- Delete `MarketplaceUrl` constant.
- Delete `DownloadVsixAsync` method.
- Replace the VSIX-source resolution logic with a direct reference to the bundled
  `gui/` folder: resolve it as
  `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui")`.
- `EnsureExtractedAsync` copies from that folder to `%APPDATA%\ContinueVS\gui\`
  (skip if `IndexHtmlPath` already exists — sentinel check unchanged).
- Remove the `localVsixPath` parameter entirely (no longer needed).
- File must remain `partial`, under 400 lines.

Reference: ADR-005, ADR-006

---

### TODO-006
**Simplify ContinueToolWindowControl.xaml.cs**
Remove: binary wait (`OnBinaryReady`, `BinaryManager.Ready` subscription)
Remove: `ContinueClient` message bridge wiring
Add: call `GuiExtractor.EnsureExtractedAsync()` on load, then navigate to
`GuiExtractor.IndexHtmlPath`
Keep: `NavigateAsync`, `LoadingPanel`, `WebView`
Must be `partial`, under 400 lines.
Reference: ADR-001, ADR-005, ADR-006

---

### TODO-007
**Build and smoke test**
Run in debugger. Experimental VS instance opens.
Press Ctrl+Shift+J. Tool window opens.
Loading panel shows briefly, then WebView shows Continue GUI.
No exceptions in Output window.

---

## Phase 2 — Create ContinueTranslator solution

### TODO-008
**Create ContinueTranslator solution**
Location: `E:\DevStudio\ContinueTranslator\`
Projects: `ContinueTranslator.Cli` (.NET 8), `ContinueTranslator.Core` (.NET 8),
`ContinueTranslator.Tests` (xUnit .NET 8)
Reference: `docs/translator-design.md`

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
