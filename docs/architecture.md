# Architecture

## Two Projects

### 1. VSIXProject1 — The Visual Studio Extension
Hosts the Continue GUI in a WebView2 tool window.
C# handles all messages the GUI sends, replacing the VS Code Extension Host entirely.

### 2. ContinueTranslator — The Code Generator (separate solution)
Reads Continue's TypeScript source from GitHub at a specific release tag.
Translates it to C# source code and emits a .csproj.
Re-run per Continue release to stay current.
See `docs/translator-design.md` for full design.

---

## Message Flow (the core of everything)

```
Continue React GUI
  running in WebView2
        |
        |  window.chrome.webview.postMessage(json)   [GUI → C#]
        |  window.continueVS.onMessage(json)          [C# → GUI]
        |
  C# MessageDispatcher
  (to be built — routes by MessageType)
        |
        ├── LlmHandler         HttpClient → OpenAI / Anthropic / Ollama
        ├── EditorHandler      VS SDK DTE → active file, selection, open docs
        ├── FileHandler        System.IO → read/write workspace files
        ├── ConfigHandler      ~/.continue/config.json reader
        └── DiffHandler        VS editor APIs → apply code changes
```

---

## What is being removed from VSIXProject1

These were built on the false assumption that a standalone binary exists in the VSIX.
The VS Code VSIX ships no executable — only Node.js extension code that requires VS Code.

| File | Reason for removal |
|---|---|
| `Binary/ContinueBinaryManager.cs` | No binary to manage |
| `Binary/BinaryDownloader.cs` | Nothing to download (binary doesn't exist) |
| `IPC/ContinueClient.cs` | stdio IPC to nonexistent process |
| `IPC/IdeCallbackHandler.cs` | Wired to ContinueClient |
| `Editor/DiffApplier.cs` | Wired to ContinueClient |
| `UI/StatusBarManager.cs` | Wired to ContinueClient |

---

## What is kept and extended

| File | What changes |
|---|---|
| `UI/ContinueToolWindowControl.xaml.cs` | Remove binary wait; navigate directly to GUI |
| `ContinueVSPackage.cs` | Remove binary/client wiring; add MessageDispatcher init |
| `IPC/ContinueProtocol.cs` | Keep — Message DTO shape is still correct |
| `Settings/ContinueOptionsPage.cs` | Keep — LLM config options will expand |
| `Settings/WorkspaceConfigWatcher.cs` | Keep — watches ~/.continue/config.json |
| `Editor/EditorContextProvider.cs` | Keep — provides VS editor context |

---

## Continue GUI assumptions

The GUI posts messages using `window.chrome.webview.postMessage`.
WebView2 natively supports this API — no shim required.
The GUI reads responses via a global `window.continueVS.onMessage(json)` callback.
This callback must be injected by the C# host before the page loads.

---

## Staying current with Continue releases

1. Run `ContinueTranslator` against the new GitHub tag
2. `git diff` the generated C# output
3. Review changes — only delta needs human attention
4. Copy updated files into VSIXProject1
5. Implement any new message handler types discovered
