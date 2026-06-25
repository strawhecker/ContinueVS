# ContinueVS — LLM Session Context

Paste this file at the start of any new agent session to restore full project context without replaying chat history.
Last updated: 2026-06-25.

---

## What this project is

ContinueVS is a Visual Studio 2022/2026 VSIX extension that ports the open-source Continue AI coding assistant (continuedev/continue, TypeScript) to Visual Studio. It is a translated C# reimplementation, not a wrapper. A tool called ContinueTranslator reads annotated TypeScript source and emits C# stubs. The VSIX consumes those stubs.

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
│   ├── output/                      ← last translator run output (ContinueCore.csproj + stubs)
│   └── tools/
│       ├── ContinueTranslator.Cli/
│       ├── ContinueTranslator.Core/
│       └── ContinueTranslator.Tests/
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

5. @ct: cookies in the TypeScript fork source survive upstream merges because they are placed on stable lines. Syntax: @ct:map=, @ct:ignore, @ct:rename=, @ct:nuget=.

## Fork workflow

```
cd <fork-clone-path>
git fetch upstream
git merge upstream/vX.Y.Z
git push origin
```

## TODO list (ordered, dependency-aware — remove items as completed)

TODO-034 [VSIX] — Fix ContinueVSPackage.cs Dispose method: base.Dispose(disposing); and three closing braces are indented at 20 spaces. Correct to 8/4/0.

TODO-035 [VSIX] — Honor ContinueOptionsPage in GhostTextController. In RequestCompletionAsync: read options via ContinueVSPackage.Instance?.GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage, return early if EnableInlineCompletions is false. Replace hard-coded 150 in OnBufferChanged with options?.DebounceDelayMs ?? 150.

TODO-038 [Trans] — Emitter: map TypeScript Promise<void> to Task (not Task<void>).

TODO-039 [Trans] — Emitter: map T | null to T? in method signatures.

TODO-037 [VSIX] — Surface LLM errors. In LlmCompleteHandler and LlmStreamChatHandler catch HttpRequestException and call _control.SendToGui("showToast", new { message = "Continue: LLM request failed — " + ex.Message, type = "error" }) before returning empty reply.

TODO-040 [Trans] — Emitter: replace inline object/index-signature parameter types ({ [key: string]: string }) with Dictionary<string, string>.

TODO-041 [Trans] — Emitter: convert TypeScript arrow-function property declarations to C# Func<> or event members.

TODO-036 [VSIX] — AutocompleteCompleteHandler: extract AutocompleteInput from message.Data, build prompt from filepath+pos, call ContinueConfigReader.FindModel("") then LlmHttpClient.CompleteAsync, reply string[] with single result. Depends on 037.

TODO-042 [Trans] — Implement FileSystemIde.cs (45 stubs) using System.IO.*. No VS SDK dependency. Depends on 038-041 emitter fixes producing compilable output.

TODO-043 [Trans] — Implement MessageIde.cs (44 stubs) routing IDE protocol calls through VSIX message dispatcher to VS SDK (DTE, IVsRunningDocTable, etc.). Depends on 042 and 036.

---

## How to use this file

Attach this file as context at the start of a new agent session and state which TODO item you are working on. The agent has everything needed to proceed without re-deriving decisions.
