# ContinueVS — LLM Session Context

Paste this file at the start of any new agent session to restore full project context without replaying chat history.
Last updated: 2026-07-01.

---

## What this project is

ContinueVS is a Visual Studio 2022/2026 VSIX extension that ports the open-source Continue AI coding assistant (continuedev/continue, TypeScript) to Visual Studio. It is a translated C# reimplementation, not a wrapper. ContinueTranslator reads annotated TypeScript source from a local fork clone and emits C# — signatures today, method bodies as Gap 1/2/3 work is completed (TODO-044–050). The VSIX consumes the promoted output from Generated/.

## Repo

- Local path: E:\GitRepos\ContinueVS\
- Remote: https://github.com/strawhecker/ContinueVS
- Branch: main
- IDE: Visual Studio Enterprise 2026 (18.6.1)
- Shell: powershell.exe

## Target frameworks

- src/VSIXProject1: net472 (.NET Framework 4.7.2)
- src/tools/ContinueTranslator.*: .NET 10

## Current repo layout

```
ContinueVS/
├── src/
│   ├── VSIXProject1/                ← VSIX (net472)
│   │   ├── Generated/               ← translator .cs output — committed as source, not binary
│   │   ├── gui/                     ← vendored Continue web UI (HTML/JS)
│   │   ├── Handlers/                ← IMessageHandler implementations
│   │   ├── Editor/                  ← ghost text adornment
│   │   ├── IPC/                     ← ContinueProtocol.cs
│   │   ├── Settings/                ← ContinueOptionsPage, WorkspaceConfigWatcher
│   │   ├── UI/                      ← ContinueToolWindowControl (WebView2 host)
│   │   ├── Binary/                  ← vendored node binary
│   │   ├── Commands/
│   │   ├── ContinueVSPackage.cs
│   │   ├── ContinueGuids.cs
│   │   ├── ContinueCommands.vsct
│   │   ├── source.extension.vsixmanifest
│   │   └── VSIXProject1.csproj
│   ├── mappings/                    ← JSON data files copied to bin/mappings/ at build time
│   │   ├── node-api.json            ← Node.js built-in → .NET type mappings
│   │   ├── npm-packages.json        ← npm package → .NET namespace mappings
│   │   ├── types.json               ← TS primitive → C# primitive mappings
│   │   └── callsites.json           ← Node.js call expressions → .NET call expressions (TODO-044)
│   ├── output/                      ← last translator run output (ContinueCore.csproj + stubs)
│   └── tools/
│       ├── ContinueTranslator.Cli/
│       ├── ContinueTranslator.Core/
│       │   ├── Emission/            ← CsEmitter partials (Classes, Functions, Helpers, Interfaces, Enums)
│       │   │   ├── CsEmitter.Expressions.cs   ← Gap 2, TODO-048 (not yet created)
│       │   │   └── CsEmitter.Statements.cs    ← Gap 2, TODO-049 (not yet created)
│       │   ├── IR/                  ← TsFile, TsClass, TsMethod, TsFunction, TsTypeRef …
│       │   │   ├── TsStatement.cs   ← Gap 2, TODO-047 (not yet created)
│       │   │   └── TsExpression.cs  ← Gap 2, TODO-047 (not yet created)
│       │   ├── Mapping/             ← MappingEngine, TypeMap, NodeApiMap, NpmPackageMap
│       │   │   └── CallSiteMap.cs   ← Gap 3, TODO-044 (not yet created)
│       │   ├── Parsing/             ← TsParser.cs, parse.mjs (embedded resource)
│       │   └── Sync/                ← TODO-051 (not yet created)
│       │       ├── GeneratedFolderSync.cs
│       │       └── SyncResult.cs
│       └── ContinueTranslator.Tests/  ← currently empty; tests added in TODO-053
├── docs/
│   ├── TODO.md                      ← master ordered TODO for both projects
│   ├── session-context.md           ← this file
│   ├── translator-design.md
│   ├── architecture.md
│   ├── protocol.md
│   └── AGENTS.md
├── adr/
├── .gitignore                       ← bin/ obj/ out/ excluded; Generated/ must NOT be excluded
├── ContinueVS.slnx
└── README.md
```

## ContinueVS.slnx (current)

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/VSIXProject1/VSIXProject1.csproj" />
    <Project Path="src/tools/ContinueTranslator.Cli/ContinueTranslator.Cli.csproj" />
    <Project Path="src/tools/ContinueTranslator.Core/ContinueTranslator.Core.csproj" />
    <Project Path="src/tools/ContinueTranslator.Tests/ContinueTranslator.Tests.csproj" />
  </Folder>
</Solution>
```

## NuGet packages (VSIXProject1)

- Microsoft.VisualStudio.SDK 17.14.40265
- Microsoft.VSSDK.BuildTools 18.5.40034
- Microsoft.Web.WebView2 1.0.4022.49
- Newtonsoft.Json 13.0.4
- Framework references: System.ComponentModel.Composition, System.Net.Http

## Key design decisions

1. ContinueTranslator belongs in this solution — it is a build-time code generator whose only consumer is this VSIX. Keeping it separate would split TODO tracking and break cross-project navigation.

2. bin/ and obj/ are excluded from git (reproducible, machine-specific). Generated .cs files from ContinueTranslator are source — commit them under src/VSIXProject1/Generated/ so the VSIX builds without running the translator locally.

3. TODO items for both projects are tracked in one file (docs/TODO.md) in dependency order, tagged [VSIX] or [Trans]. One item per agent session; remove on completion.

4. Always use git mv for tracked file moves. Visual Studio must be closed before any git mv of the project root (permission denied otherwise).

5. @ct: cookies in the TypeScript fork source annotate bodies as well as signatures — the translator is intended to translate method bodies, not only signatures. Current gaps (body walking, statement/expression IR, call-site map) are tracked as TODO-044–050. Syntax: @ct:map=, @ct:ignore, @ct:rename=, @ct:nuget=.

6. output/ is a staging area — raw translator output for inspection. Files are promoted to VSIXProject1/Generated/ by GeneratedFolderSync (TODO-051/052) when they are clean (no // TODO stubs, no raw TS type leaks). A .translator-manifest.json in Generated/ tracks SHA-256 hashes so hand-edited files are never overwritten by a subsequent translator run.

7. CsEmitter is currently stateless (all helpers are static). TODO-047 adds a CallSiteMap constructor parameter — the only instance field CsEmitter will ever need. PipelineRunner constructs it with the loaded CallSiteMap.

8. The fork contains two concrete implementations of the IDE interface: FileSystemIde (filesystem.ts) calls System.IO.* directly; MessageIde (messageIde.ts) routes every call through a _request("messageName", payload) messenger delegate. These are not alternatives — both are required. FileSystemIde is pure System.IO work; MessageIde is a proxy whose bodies follow a single mechanical pattern and should be fully auto-translated once Gap 1/2 body walking is in place.

9. **PipelineRunner cleanup (NEW):** When the translator CLI is invoked with `--out`, `--generated`, or `--rejected-dir` parameters, those directories are recursively deleted (if they exist) at startup, **before** the translation pipeline runs. This ensures each translation run starts with a clean slate and avoids stale files from previous runs. Additionally, the optional `--post-translate-clean-generated` flag deletes the Generated/ folder **after** translation completes, useful for testing scenarios to prevent folder pollution during CI/local test runs. Warnings are logged if cleanup fails, but the pipeline continues.

## Fork workflow

```
cd <fork-clone-path>
git fetch upstream
git merge upstream/vX.Y.Z
git push origin
```

## TODO list (ordered, dependency-aware — remove items as completed)

TODO-053 [Trans] — Tests: Add xUnit tests to ContinueTranslator.Tests (currently empty) — CallSiteMap, GeneratedFolderSync, statement/expression emitter round-trip. No VS SDK dependency.

TODO-054 [Trans] — Re-run translator after 038–053. Implement remaining // TODO stubs in FileSystemIde.cs using System.IO.*. Promote via --generated.

TODO-055 [Trans] — Re-run translator after 038–053. MessageIde.cs _request-pattern methods should auto-translate. Implement remaining VS SDK stubs directly in Generated/MessageIde.cs; manifest protects them. Depends on 053 and 037.

---

## How to use this file

Attach this file as context at the start of a new agent session and state which TODO item you are working on. The agent has everything needed to proceed without re-deriving decisions.
