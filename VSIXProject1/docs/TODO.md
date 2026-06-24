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

### TODO-017
**Wire CLI entry point**
`Program.cs` — parse args, call pipeline, report results.
- `--repo`  path to local clone of forked continue repo (required)
- `--tag`   git tag or branch to check out before scanning (required)
- `--out`   output directory (required)

---

### TODO-018
**Test run: Continue v2.0.0 core/protocol/**
Prerequisite: personal fork of continuedev/continue cloned locally with tag `v2.0.0` present.
Run: `dotnet run -- --repo <fork-clone-path> --tag v2.0.0 --out output/`
Scope: `core/protocol/**` only.
Review generated C# protocol types.
Fix any parser or emitter gaps found.

---

## Phase 3 — Implement message handlers in VSIXProject1

### TODO-019
**Study protocol output**
After TODO-018: read the generated C# to understand all MessageType values.
Document each in a new `docs/protocol.md` file (message name, payload shape, expected response).

---

### TODO-020
**Create IMessageHandler interface and MessageDispatcher**
`VSIXProject1/Handlers/IMessageHandler.cs`
`VSIXProject1/Handlers/MessageDispatcher.cs`
Routes incoming WebView messages to the correct handler by MessageType.
Wire into `ContinueToolWindowControl.OnWebMessageReceived`.

---

*Further handler TODO items will be added after TODO-019 is complete
and the full protocol is known.*
