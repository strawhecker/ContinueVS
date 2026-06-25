# ContinueVS — LLM Agent Entry Point

Read this file first when starting a new chat thread on this project.

## What this project is

A Visual Studio 2022+ extension (VSIX) that embeds the **Continue AI coding assistant**.
Continue is normally a VS Code extension. This project ports it to Visual Studio.

## The one insight that drives everything

The Continue GUI (`extension/gui/index.html`) is a **plain React SPA**.
It communicates with its backend solely via JSON messages.
It has **zero dependency on VS Code**.

This extension hosts the GUI in **WebView2** and handles all messages in **C#**.
C# is the full replacement for the VS Code Extension Host.

## Current state of the codebase

| Area | Status | Notes |
|---|---|---|
| WebView2 tool window | COMPLETE | UI/ContinueToolWindowControl.xaml.cs — navigates directly to GUI, no binary wait |
| Binary/IPC layer | REMOVED | Replaced by MessageDispatcher + C# handlers. Binary/GuiExtractor.cs retained for GUI asset extraction only |
| Message handlers | COMPLETE | 40+ handlers across Handlers/Config, Handlers/Context, Handlers/File, Handlers/Ide, Handlers/Llm, Handlers/Push |
| EditorContextProvider | COMPLETE | Wired into ContinueToolWindowControl; pushes currentFile and didChangeActiveTextEditor on editor changes |
| WorkspaceConfigWatcher | COMPLETE | Wired into ContinueToolWindowControl; pushes configUpdate when ~/.continue/config.json changes |
| ContinueTranslator project | EXISTS | Separate solution at E:\DevStudio\ContinueTranslator\ — re-run against new Continue release tags to regenerate protocol types |

## Files to read for context

| File | When to read it |
|---|---|
| `docs/architecture.md` | Understanding the full system design |
| `docs/TODO.md` | Finding the next work item |
| `docs/translator-design.md` | Working on the ContinueTranslator project |
| `docs/adr/` | Understanding why a decision was made |

## Key paths

| Thing | Path |
|---|---|
| Extension project | `E:\GitRepos\ContinueVS\VSIXProject1\VSIXProject1.csproj` |
| Tool window control | `VSIXProject1\UI\ContinueToolWindowControl.xaml.cs` |
| GUI (after extraction) | `%APPDATA%\ContinueVS\gui\index.html` |
| Continue source (GitHub) | `https://github.com/continuedev/continue` |

## Constraints

- Target framework: **.NET Framework 4.7.2**
- Visual Studio SDK: **17.x**
- All non-POCO classes: **partial**, each file **< 400 lines** (see ADR-003)
- One TODO item per agent session — commit before continuing
