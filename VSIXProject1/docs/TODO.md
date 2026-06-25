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

