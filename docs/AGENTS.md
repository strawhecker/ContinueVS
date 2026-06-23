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
| WebView2 tool window | EXISTS | `UI/ContinueToolWindowControl.xaml.cs` |
| Binary/IPC layer | EXISTS — WRONG | Must be deleted. No binary ships in the VSIX. |
| Message handlers | NOT YET | C# handlers to be written after translator runs |
| ContinueTranslator project | NOT YET | Separate solution, to be created |

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
| VS extension solution | `E:\DevStudio\VSIXProject1\VSIXProject1.slnx` |
| Extension project | `VSIXProject1\VSIXProject1.csproj` |
| Tool window control | `VSIXProject1\UI\ContinueToolWindowControl.xaml.cs` |
| GUI (after extraction) | `%APPDATA%\ContinueVS\gui\index.html` |
| Continue source (GitHub) | `https://github.com/continuedev/continue` |

## Constraints

- Target framework: **.NET Framework 4.7.2**
- Visual Studio SDK: **17.x**
- All non-POCO classes: **partial**, each file **< 400 lines** (see ADR-003)
- One TODO item per agent session — commit before continuing
