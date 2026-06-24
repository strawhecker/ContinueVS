# ADR-005 — GUI Extraction from Continue VSIX

## Status
Accepted

## Context
The Continue React GUI (`extension/gui/`) ships inside the VS Code VSIX package.
The VSIX is a ZIP archive. The GUI is a static SPA (HTML + JS + CSS) with no runtime
dependency on VS Code or Node.js.

The GUI must be extracted to a local path before WebView2 can load it.

## What is in the VSIX (confirmed from directory listing)
- `extension/gui/index.html` — main entry point
- `extension/gui/assets/` — JS bundles, CSS
- `extension/gui/fonts/` — Inter, JetBrainsMono
- `extension/gui/logos/` — provider logos
- `extension/gui/textmate-syntaxes/` — syntax highlighting grammars

There is **no** `continue-binary.exe` or any standalone executable in the VSIX.

## Decision
On first run (or when `gui/index.html` is missing), extract only the `extension/gui/`
subtree from the VSIX to `%APPDATA%\ContinueVS\gui\`.

The VSIX to extract from is either:
- A **local file path** configured via Tools → Options → Continue → Local VSIX Path
- Or **downloaded** from the VS Marketplace as a fallback

Binary download/launch logic is **removed entirely** (see ADR-001).

## Extraction path mapping
```
VSIX:  extension/gui/assets/index.js
Disk:  %APPDATA%\ContinueVS\gui\assets\index.js

VSIX:  extension/gui/index.html
Disk:  %APPDATA%\ContinueVS\gui\index.html
```

## Consequences
- `BinaryDownloader.cs` is deleted and replaced with a focused `GuiExtractor.cs`.
- `ContinueBinaryManager.cs` is deleted entirely.
- `ContinueToolWindowControl` navigates to `gui/index.html` directly after extraction.
- The local VSIX path option remains in `ContinueOptionsPage` for offline/dev use.
