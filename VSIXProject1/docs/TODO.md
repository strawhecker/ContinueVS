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

---

TODO-022
Implement file read handlers
`VSIXProject1/Handlers/File/ReadFileHandler.cs`
`VSIXProject1/Handlers/File/FileExistsHandler.cs`
`VSIXProject1/Handlers/File/GetOpenFilesHandler.cs`
Uses System.IO for readFile and fileExists.
Uses VS DTE RunningDocumentTable for getOpenFiles.
Register all in ContinueToolWindowControl constructor.

---

TODO-023
Implement file write and navigation handlers
`VSIXProject1/Handlers/File/WriteFileHandler.cs`
`VSIXProject1/Handlers/File/SaveFileHandler.cs`
`VSIXProject1/Handlers/File/OpenFileHandler.cs`
`VSIXProject1/Handlers/Ide/OpenUrlHandler.cs`
`VSIXProject1/Handlers/Ide/GetBranchHandler.cs`
writeFile and saveFile use System.IO and VS DTE save.
openFile uses DTE.ItemOperations.OpenFile.
openUrl uses Process.Start.
getBranch runs git rev-parse --abbrev-ref HEAD as a subprocess.
Register all in ContinueToolWindowControl constructor.

---

TODO-024
Implement push-event senders to webview
`VSIXProject1/Handlers/Push/WebviewPusher.cs`
Add a WebviewPusher helper that wraps SendToGui on ContinueToolWindowControl.
Wire configUpdate to fire on tool window load with a minimal IdeSettings payload.
Wire indexProgress as a stub that sends a 100% complete payload immediately.
Wire didChangeActiveTextEditor to the VS RunningDocumentTable IVsRunningDocTableEvents3.OnAfterActiveDocChange event.

---

TODO-025
Remaining handlers from protocol.md
Remaining Types section Prerequisite: fix translator emitter gaps identified in output\review-018.md so payload shapes are fully resolved.
After fixing, implement the handlers listed under Remaining Types in docs/protocol.md.
Prioritise the context/, config/, and llm/ groups first as these are needed for core Continue functionality.

