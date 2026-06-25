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

TODO-027 — Wire WorkspaceConfigWatcher and add getCurrentFile handler.
Complete OnConfigChanged to call _pusher.PushConfigUpdate().
Call Start() from ContinueToolWindowControl.NavigateAsync.
Create Handlers\Ide\GetCurrentFileHandler.cs that uses EditorContextProvider data (DTE active document) to reply with filepath, contents, and cursor position.

---

TODO-028 — Implement applyToFile, acceptDiff, rejectDiff handlers using VS editor APIs (IVsTextManager / DTE).

---

TODO-029 — Update AGENTS.md current state table and architecture.md to reflect what is actually built and what remains.
