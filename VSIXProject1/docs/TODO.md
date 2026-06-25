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

TODO-031 — Wire GhostText autocomplete round-trip.
Add a pending-reply dictionary to ContinueToolWindowControl so any caller can await a response by messageId.
Make GhostTextController use ContinueVSPackage.Instance to get the tool window, send autocomplete/complete, await the reply, set _pendingText, and call RenderGhostText().
Complete both NotifyOutcome branches to send autocomplete/accept and autocomplete/cancel.
Register the three autocomplete message types in the dispatcher.

---

TODO-032 — Implement real llm/complete using System.Net.Http.HttpClient.
Read ~/.continue/config.json to find the first configured model's provider/apiKey/baseUrl.
Make a real HTTP POST to the provider endpoint.
Return the completion string.
This unblocks both inline autocomplete and chat.

---

TODO-033 — Implement llm/streamChat using the same HttpClient pattern as TODO-032 but streaming chunked responses back to the GUI incrementally via SendToGui.

