# ADR-001 — C# Replaces the VS Code Extension Host

## Status
Accepted

## Context
Continue is a VS Code extension. Its backend (`extension/out/extension.js`) runs inside
VS Code's Extension Host — a Node.js process that requires VS Code's Electron main process
via IPC. The `vscode` module injected by the Extension Host has no standalone equivalent.

The Continue GUI (`extension/gui/index.html`) is a React SPA with no VS Code dependency.
It communicates with its backend via JSON messages only.

WebView2 (used in this VS extension) natively supports `window.chrome.webview.postMessage`,
which is the same API the GUI uses to send messages.

## Decision
C# code inside VSIXProject1 is the **complete replacement** for the VS Code Extension Host.

- The GUI runs in WebView2 unchanged.
- All JSON messages from the GUI are received and handled by C# code.
- C# calls VS APIs (DTE, IVsTextView) where the extension would have called `vscode.*`.
- C# calls LLM HTTP endpoints where the extension would have called its LLM clients.
- No subprocess is spawned. No IPC pipe is needed.

## Consequences
- The entire `Binary/` and `IPC/` layer in VSIXProject1 must be deleted.
- A C# `MessageDispatcher` and per-message-type handlers must be written.
- The Continue message protocol must be understood and implemented in C#.
