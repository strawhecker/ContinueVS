# TODO — Ordered Action List

One item per agent session. Commit after each item. Remove completed items. tools and docs moved to VSIXProject1.

## Fork workflow

Source is read from a **local clone of a personal fork** of continuedev/continue.
To update to a newer upstream version:
```
cd <fork-clone-path>
git fetch upstream
git merge upstream/vX.Y.Z
# resolve conflicts — @ct: cookies on stable lines survive automatically
git push origin
```
Translation annotations (`@ct:` cookies) are placed directly in the fork's TS source and
are version-controlled alongside the code they annotate.

Cookie syntax:
```
// @ct:map=System.Net.Http.HttpClient
// @ct:ignore
// @ct:rename=GetFileContents
// @ct:nuget=System.Net.Http
```

---

## Phase 2 — Create ContinueTranslator solution

### TODO-010
**Create SourceFetcher.cs**
Accepts a local fork clone path and a git tag or branch name.
Runs `git checkout <tag-or-branch>` in the clone before scanning.
Returns list of `.ts` file paths after filtering out `*.test.ts`, `extensions/vscode/**`, `gui/**`.
Must be `partial`, under 400 lines.

---

### TODO-011
**Create IR records and parse.mjs**
First: replace stubs in `ContinueTranslator.Core/IR/` with `sealed record` types.
All records include a `string[] Cookies` property for `@ct:` annotations.
Property shapes derived from what ts-morph exposes for each TypeScript construct.

Then: implement `parse.mjs` as the embedded resource in `ContinueTranslator.Core/Parsing/`.
Uses ts-morph to walk each `.ts` file and emit a JSON array of IR-compatible objects.
Covers: interfaces, classes, enums, functions, type aliases.
For each node, scan leading comments for `@ct:` prefix and emit them in the `cookies` array.

---

### TODO-012
**Create TsParser.cs**
Extracts `parse.mjs` to temp, runs `node parse.mjs <files>`, reads stdout JSON.
Deserializes to IR types including the `Cookies` field.
Must be `partial`, under 400 lines.

---

### TODO-014
**Create MappingEngine + map readers**
`Mapping/NodeApiMap.cs`, `Mapping/NpmPackageMap.cs`, `Mapping/TypeMap.cs`
`Mapping/MappingEngine.cs` — orchestrates all three over the IR.
`@ct:` cookies take precedence over mapping tables.
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
`Program.cs` — parse args, call pipeline, report results.
- `--repo`  path to local clone of forked continue repo (required)
- `--tag`   git tag or branch to check out before scanning (required)
- `--out`   output directory (required)

---

### TODO-018
**Test run: Continue v2.0.0 core/protocol/**
Prerequisite: personal fork of continuedev/continue cloned locally with tag `v2.0.0` present.
Run: `dotnet run -- --repo <fork-clone-path> --tag v2.0.0 --out output/`
Scope: `core/protocol/**` only.
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
