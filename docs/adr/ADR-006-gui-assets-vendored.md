# ADR-006 — GUI Assets Vendored into Repository

## Status
Accepted

## Context
ADR-005 established that the Continue React GUI (`extension/gui/`) is extracted from
a Continue VSIX and served via WebView2. It defined two VSIX sources:

1. A local file path configured by the user (Tools -> Options).
2. A fallback download from the VS Code Marketplace (`latest`).

ADR-001 established that C# replaces the VS Code Extension Host and handles all GUI
messages directly. Those C# handlers are written against a **specific version** of the
Continue message protocol.

The Marketplace fallback in ADR-005 is therefore unsafe: it can silently pull a newer
VSIX whose GUI emits message shapes that the C# handlers have never seen. There is no
compile-time or startup guard against this mismatch -- it produces silent runtime failure.

The `localVsixPath` option was implicitly acknowledging the version-pinning requirement.
This ADR makes that pin explicit and moves it to build time.

## Decision
The `extension/gui/` subtree from a **pinned, known-good Continue VSIX** is extracted
once by a developer and checked into the repository under `VSIXProject1/gui/`.

- All files under `VSIXProject1/gui/` are marked as VSIX content and ship inside the
  ContinueVS `.vsix` package.
- `GuiExtractor.cs` is simplified: it only copies from the in-package `gui/` folder to
  `%APPDATA%\ContinueVS\gui\`. No `HttpClient`. No Marketplace URL.
- The `localVsixPath` option in `ContinueOptionsPage` is **removed** (it was a
  workaround for the problem this ADR solves).
- The Marketplace fallback constant and `DownloadVsixAsync` method in `GuiExtractor.cs`
  are **deleted**.

## Upgrading to a new Continue version
When the project targets a new Continue release:

1. Download that release's VSIX from the Marketplace or GitHub releases.
2. Extract its `extension/gui/` subtree, replacing the contents of `VSIXProject1/gui/`.
3. Update the C# message handlers to match any protocol changes in that release.
4. Commit steps 2 and 3 together so the repo is never in a split state.

The folder name `VSIXProject1/gui/` never changes. Only its contents are replaced.

## Consequences
- **Version coherence is enforced**: the GUI and the C# handlers always match because
  they are updated in the same commit.
- **No internet access required** at install time or first run.
- **Reproducible installs**: every user gets exactly the GUI the developer tested against.
- **VSIX size increases** by approximately 5-15 MB (JS bundles, fonts, syntax grammars).
  This is acceptable for a developer tool.
- `GuiExtractor.cs` becomes simpler: copy logic replaces download logic.
- ADR-005 remains valid for the extraction path mapping and AppData target location.
  This ADR supersedes only the VSIX-source decision within ADR-005.
