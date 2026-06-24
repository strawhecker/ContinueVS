# ContinueVS — Continue AI Coding Assistant for Visual Studio

[Continue](https://github.com/continuedev/continue) is the leading open-source AI coding
assistant. This project brings it to **Visual Studio 2022 and 2026** as a native VSIX extension.

---

## Features

| Feature | Detail |
|---|---|
| **AI Chat panel** | `View -> Continue Chat` or `Ctrl+Shift+J` |
| **Inline ghost-text completions** | Appear as you type - press `Tab` to accept, `Esc` to dismiss |
| **Explain code** | Select code -> `Edit -> Continue -> Explain` |
| **Fix code** | Select code -> `Edit -> Continue -> Fix` (includes Error List context) |
| **Add comments** | Select code -> `Edit -> Continue -> Add Comment` |
| **Ask Continue** | Select code -> `Ctrl+Shift+M` to open chat with selection pre-filled |
| **Diff / apply** | Continue can propose file edits; VS diff view opens for review |
| **Model-agnostic** | Works with OpenAI, Anthropic, Ollama, Gemini, and any Continue provider |
| **Config-driven** | Same `~/.continue/config.json` as the VS Code extension |

---

## Building from source

### Prerequisites

- Visual Studio 2022 17.9+ or Visual Studio 2026 (Community / Professional / Enterprise)
- .NET SDK 10+ (for the SDK-style project tooling)
- **Visual Studio SDK** workload installed (`Visual Studio extension development`)

### Steps

```powershell
git clone https://github.com/strawhecker/ContinueVS
cd ContinueVS
dotnet build VSIXProject1\VSIXProject1\VSIXProject1.csproj
```

The output VSIX is written to:

    VSIXProject1\VSIXProject1\bin\Debug\net472\ContinueVS.vsix

Double-click the VSIX to install, or use **Extensions -> Manage Extensions** in VS.
Press **F5** in Visual Studio to launch an Experimental Instance with the extension loaded.

---

## First-run behaviour

On first launch ContinueVS automatically:

1. Downloads the Continue AI engine (~60 MB) from the VS Marketplace VSIX.
2. Extracts `continue-binary.exe` and the React GUI assets into `%APPDATA%\ContinueVS\`.
3. Starts the binary and connects via stdio IPC.

Progress is shown in the VS status bar. An internet connection is required on first run only.

To use a custom binary: **Tools -> Options -> Continue -> Binary path override**.

---

## Configuration

ContinueVS reads the same config file as the VS Code extension:

    ~/.continue/config.json

See https://docs.continue.dev/reference/config for the full schema.
ContinueVS watches this file for changes and reloads automatically.

---

## Architecture

    Visual Studio process
    ContinueVSPackage (AsyncPackage)
      ContinueBinaryManager   - launches continue-binary.exe as a child process
      ContinueClient          - stdio IPC: \r\n-delimited JSON on stdin/stdout
      IdeCallbackHandler      - answers ~40 IDE requests from the binary
      EditorContextProvider   - streams active-file / cursor context + didChangeActiveTextEditor
      GhostTextAdornment      - inline completion rendering (MEF ITextViewCreationListener)
      DiffApplier             - opens VS diff view for proposed edits
      ContinueToolWindowPane  - WebView2 hosting the Continue React chat UI

IPC wire format (matches upstream IpcMessenger.ts):
    {"messageType":"readFile","messageId":"<guid>","data":{"filepath":"C:\\src\\Foo.cs"}}\r\n

---

## Contributing

PRs welcome. Please open an issue first for significant changes.
The Continue core logic lives upstream at https://github.com/continuedev/continue.

---

## License

Apache 2.0 - see LICENSE.txt.
Upstream Continue is also Apache 2.0.
