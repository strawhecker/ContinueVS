# Manual Test: Tool Window Initialization & Initial Configuration Push

Verify that after WebView2 content loads and the bridge is ready, the C# layer sends the initial settings/model configuration to the JavaScript UI, causing the spinner to stop and dropdowns to render with values.

## Context

**Step**: b22 (Initial Message Push)  
**Prerequisite**: b21 must be debugged and working (WebView2 ready, bridge injectable)  
**Related Handlers**: b16 (loadSettings), b17 (getModelInfo) must be registered  
**Performance Gate**: <500ms total latency

---

## Pre-Test Verification

- [ ] Step b21 debugged and working (WebView2 initialization complete)
- [ ] b16 (loadSettings handler) registered in MessageDispatcher
- [ ] b17 (getModelInfo handler) registered in MessageDispatcher
- [ ] Debug.WriteLine output capture enabled (Debug → Windows → Output)
- [ ] No active breakpoints from previous debugging sessions

---

## Test Procedure

### 1. Open Tool Window

1. Launch VS in debug mode: **F5**
2. Navigate to **Tools → ContinueVS → Show Continue Panel**
3. Observe the **Loading spinner** appears initially
4. **Expected**: Spinner stops within ~2 seconds, replaced by UI with dropdowns

**Verify**:
- [ ] Spinner appears on tool window open
- [ ] Spinner stops and disappears (UI becomes visible)
- [ ] No error toast/notification appears
- [ ] Tool window remains responsive (no hangs)

---

### 2. Verify [b22-*] Log Markers in Debug Output

1. Open **Debug → Windows → Output** pane
2. Set filter to search for: `[b22-`
3. Expected log sequence (in order):

```
[b22-PUSH-START] entry - IdeSettings serialization starting
[b22-CONFIG-SERIALIZED] IdeSettings object created
[b22-SCRIPT-INJECTED] calling SendToGui(configUpdate)
[b22-UI-RENDER] SendToGui completed in XXms
[b22-LATENCY-GATE-PASS] XXms within gate
```

**Verify**:
- [ ] **[b22-PUSH-START]** present
- [ ] **[b22-CONFIG-SERIALIZED]** appears after PUSH-START
- [ ] **[b22-SCRIPT-INJECTED]** present in sequence
- [ ] **[b22-LATENCY-GATE-PASS]** or **[b22-LATENCY-GATE-EXCEEDED]** appears (check for PASS, not EXCEEDED)
- [ ] **[b22-UI-RENDER]** shows elapsed milliseconds value (should be <500ms)

---

### 3. Verify UI Render Completion

After spinner stops, verify rendered UI elements:

- [ ] **Settings dropdown** appears and is populated with options
- [ ] **Model dropdown** appears and is populated with options
- [ ] Both dropdowns are clickable/interactive
- [ ] No "Loading..." placeholder text visible in dropdowns
- [ ] No error messages or warnings displayed

**Note**: Dropdowns should contain actual values from IdeSettings object. If empty, verify handlers (b16/b17) are registered.

---

### 4. Performance Gate Check

1. In Debug Output, locate the **[b22-UI-RENDER]** line
2. Extract the elapsed milliseconds value
3. Verify gate status

**Pass criteria**:
- [ ] See **[b22-LATENCY-GATE-PASS]** in logs (not EXCEEDED)
- [ ] Elapsed time < 500ms shown in log message
- [ ] Spinner stops visually within ~1-2 seconds

**Failure diagnostics**:
- If **[b22-LATENCY-GATE-EXCEEDED]** appears: Check SendToGui performance (b11-b13 steps)
- If latency very high (>1000ms): Profile handler execution or JavaScript execution time

---

### 5. Debugger Breakpoint Trace (Optional)

For manual step-through verification:

**Breakpoint Setup**:

| Breakpoint | File | Line | Purpose |
|-----------|------|------|---------|
| BP1 | WebviewPusher.cs | 28 (PushConfigUpdate entry) | Verify entry before serialization |
| BP2 | WorkspaceConfigWatcher.cs | 59 (pre-call marker) | Verify call site from config watcher |
| BP3 | WebviewPusher.cs | 45 (post-stopwatch.Stop) | Inspect elapsed time before gate check |

**Execution Steps**:

1. Set all 3 breakpoints
2. Launch debugger (**F5**), open tool window
3. At **BP1 hit**:
   - [ ] Verify `_control` is non-null
   - [ ] Single step through method to observe [b22-*] markers in output
4. At **BP2 hit** (if triggered from WorkspaceConfigWatcher):
   - [ ] Confirm `[b22-CALL-SITE]` marker visible in output
   - [ ] Note timing: bridge should be ready before this call
5. At **BP3 hit**:
   - [ ] Inspect `stopwatch.ElapsedMilliseconds` value in Locals pane
   - [ ] Verify value is < 500
   - [ ] Continue execution
6. After continuation:
   - [ ] Verify all [b22-*] markers in Debug Output
   - [ ] Verify UI dropdowns appear

---

## Pass Criteria

✅ **All of the following must be true**:

- [x] All [b22-*] markers present in sequence (PUSH-START → CONFIG-SERIALIZED → SCRIPT-INJECTED → LATENCY-GATE-PASS → UI-RENDER)
- [x] Latency gate: **PASS** (< 500ms) — NOT EXCEEDED
- [x] UI dropdowns rendered visibly (not spinning or loading)
- [x] No exceptions or error toasts in Debug Output
- [x] All 3 breakpoints (if set) hit on first push
- [x] Spinner stops within 1-2 seconds of tool window open

---

## Failure Diagnostics

| Symptom | Check | Potential Fix |
|---------|-------|---------------|
| Spinner still spinning after 3+ seconds | `[b22-LATENCY-GATE-EXCEEDED]` present? | Trace SendToGui performance (b11-b13); check handler exec time |
| No [b22-*] markers in Debug Output | JSON escaping issue in SendToGui? | Verify IdeSettings serializes to valid JSON |
| Dropdowns appear empty/loading | Handlers (b16/b17) registered but failing? | Trace OnWebMessageReceivedAsync (b12 step) for handler errors |
| Missing dropdowns entirely | UI HTML structure issue? | Check that HTML template includes dropdown elements |
| `[b22-LATENCY-GATE-EXCEEDED]` | Code path slow (serialization/JS injection) | Profile each [b22-*] marker; identify bottleneck |
| COMException in output | WebView2 lifecycle issue | Verify WebView2 initialization complete (b21) before this test |

---

## Verification Log Template

```
TEST: Tool Window Initialization & Initial Configuration Push
DATE: [DATE]
ENVIRONMENT: [VS VERSION, .NET VERSION]

PRE-TEST:
  [ ] b21 Debugged: [✓ / ✗]
  [ ] Handlers Registered: [✓ / ✗]
  [ ] Debug Output Enabled: [✓ / ✗]

TEST EXECUTION:
  Spinner Appeared: [✓ / ✗]
  Spinner Stopped Within 2s: [✓ / ✗]
  [b22-PUSH-START] Found: [✓ / ✗]
  [b22-CONFIG-SERIALIZED] Found: [✓ / ✗]
  [b22-SCRIPT-INJECTED] Found: [✓ / ✗]
  [b22-LATENCY-GATE-PASS] Found: [✓ / ✗]
  [b22-UI-RENDER] Elapsed Time: [___] ms
  Dropdowns Visible: [✓ / ✗]
  Dropdowns Populated: [✓ / ✗]

RESULT: [PASS / FAIL]
NOTES: [Additional observations, errors, or issues]
```

---

## Related Steps

- **b21**: WebView2 Initialization — must complete first
- **b16**: Settings Handler (loadSettings) — populates settings dropdown
- **b17**: Model Handler (getModelInfo) — populates model dropdown
- **b11**: Bridge Message Round-Trip — underlying mechanism (SendToGui)
- **b12**: Message Receiving Flow — handler dispatch
- **b13**: Response Serialization — JSON formatting
