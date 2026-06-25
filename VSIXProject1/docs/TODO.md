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

## Repo structure note

The translator (`ContinueTranslator`) is a build-time code generator whose sole output feeds this VSIX. It is architecturally equivalent to a source generator and belongs in the same solution. Merge target:

```
solution root (ContinueVS/)
├── VSIXProject1/                ← VSIX
├── tools/
│   └── ContinueTranslator/      ← subtree merge: git subtree add --prefix=tools/ContinueTranslator <translator-remote> main
├── ContinueVS.sln               ← add translator .csproj here, set build order
└── docs/
    └── TODO.md                  ← single master TODO (this file)
```

TODO items for both projects are listed below in dependency order. Each session: one item, commit, remove.

---

TODO-034 [VSIX] — Fix ContinueVSPackage.cs Dispose method: the base.Dispose(disposing); line and the three closing braces that follow it are all indented at 20 spaces instead of the correct 8/4/0. Reformat to correct indentation so the file is structurally valid.

---

TODO-035 [VSIX] — Honor ContinueOptionsPage in GhostTextController. At the start of RequestCompletionAsync, read the options page via ContinueVSPackage.Instance?.GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage and return early if EnableInlineCompletions is false. Replace the hard-coded 150 in OnBufferChanged with options?.DebounceDelayMs ?? 150.

---

TODO-038 [Trans] — Fix translator emitter: map Task<void> → Task everywhere in output (TypeScript Promise<void> → Task).

---

TODO-039 [Trans] — Fix translator emitter: map TypeScript union return types (T | null) → nullable C# reference types (T?) in method signatures.

---

TODO-037 [VSIX] — Surface LLM errors to the user. In LlmCompleteHandler and LlmStreamChatHandler, catch HttpRequestException specifically and call _control.SendToGui("showToast", new { message = "Continue: LLM request failed — " + ex.Message, type = "error" }) before returning the empty reply, so the user sees actionable feedback in the GUI.

---

TODO-040 [Trans] — Fix translator emitter: drop TypeScript inline object/index-signature parameter types (e.g., { [key: string]: string }) and emit a named Dictionary<string, string> or record instead.

---

TODO-041 [Trans] — Fix translator emitter: convert TypeScript arrow-function property declarations (e.g., showToast { get; init; } with a function type) into proper C# Func<> or event members.

---

TODO-036 [VSIX] — Make AutocompleteCompleteHandler produce real completions. Extract the AutocompleteInput from message.Data, build a prompt string from filepath and pos, call ContinueConfigReader.FindModel("") then LlmHttpClient.CompleteAsync, and reply with a string[] containing the single result. This closes the ghost text round-trip. Depends on LlmHttpClient being stable (037 done).

---

TODO-042 [Trans] — Once emitter fixes (038–041) produce compilable output, implement FileSystemIde.cs methods (the 45-stub file) using System.IO.* — these are pure file-system operations with no VS SDK dependency.

---

TODO-043 [Trans] — Implement MessageIde.cs methods (44 stubs) by routing each IDE protocol call through the VSIX message dispatcher to the VS SDK (DTE, IVsRunningDocTable, etc.). Depends on 042 and the 036 dispatcher.

