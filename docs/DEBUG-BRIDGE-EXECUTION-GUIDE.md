# Debug Guide: ContinueVS Bridge Execution Tracing (45 Steps: t1-t45)

## Overview

This guide provides comprehensive breakpoint locations and debugging workflows for all 45 execution steps across the ContinueVS bridge initialization pipeline.

**Three execution phases:**
- **Phase I (t1-t8)**: Package & Tool Window Initialization
- **Phase II (t9-t20)**: Handler Loop Registration  
- **Phase III (t21-t45)**: WebView2 Initialization & Bridge Operationality

---

## Quick Start

1. **Launch Debug**: Press `F5` in Visual Studio
2. **View Traces**: Debug → Windows → Output → Filter: "ContinueVS"
3. **Set Breakpoint**: Click margin at token line (see tables below)
4. **Trigger Init**: Open Continue tool window (View → Other Windows → Continue)
5. **Inspect**: Examine Locals/Watch; traces appear in Output pane

---

## Output Window Setup

1. Debug → Windows → Output (Ctrl+Alt+O)
2. Click dropdown → select "ContinueVS" pane
3. Each trace is a JSON object (one per line):
   ```json
   {"token":"t1.1","timestamp":"2026-07-23T14:32:45.156Z","component":"ContinueVSPackage","duration_ms":12}
   ```

---

## Phase I: Package & Tool Window Initialization (t1-t8)

### Overview
Initializes the VSIX package, creates the tool window pane, instantiates core services (Logger, Telemetry, Pusher, ConfigWatcher, EditorContextProvider).

### Token Map - Phase I

| Token | File | Line | Description | Breakpoint Focus |
|-------|------|------|-------------|-----------------|
| **t1** | ContinueVSPackage.cs | 48 | InitializeAsync entry point | Method entry, parameters |
| **t1.1** | ContinueVSPackage.cs | 56 | Thread switch (SwitchToMainThreadAsync) | Thread ID verification |
| **t1.2** | ContinueVSPackage.cs | 60 | Instance assignment | Package instance != null |
| **t1.3.1** | ContinueVSPackage.cs | 64 | VersionSelectorService creation | Service object instantiated |
| **t1.3.2** | ContinueVSPackage.cs | 68 | VersionManager creation | Manager callable |
| **t1.3.3** | ContinueVSPackage.cs | 72 | DowngradeWarningService creation | Service ready |
| **t1.3.4** | ContinueVSPackage.cs | 76 | BridgeLogger creation | Logger.WriteDebugAsync available |
| **t1.3.5** | ContinueVSPackage.cs | 81 | BridgeTelemetryCollector creation | TelemetryCollector instance |
| **t1.4** | ContinueVSPackage.cs | 85 | Options page retrieval | optionsPage != null |
| **t1.5** | ContinueVSPackage.cs | 109 | Command initialization loop | All 5 commands hooked |
| **t1.6** | ContinueVSPackage.cs | 125 | InitializeAsync completion | Exception state checked |
| **t2** | BridgeLogger.cs | 28 | BridgeLogger constructor | VS output window writer |
| **t3** | ContinueToolWindowPane.cs | TBD | Tool window pane creation | Pane initialized |
| **t4** | ContinueToolWindowControl.xaml.cs | 44 | Constructor entry | UserControl instantiated |
| **t5** | ContinueToolWindowControl.xaml.cs | 58 | MessageDispatcher registration | Dispatcher ready for handlers |
| **t6** | ContinueToolWindowControl.xaml.cs | 50 | WebviewPusher instantiation | Pusher created |
| **t7** | ContinueToolWindowControl.xaml.cs | 52 | WorkspaceConfigWatcher creation | Watcher ready |
| **t8** | ContinueToolWindowControl.xaml.cs | 54 | EditorContextProvider instantiation | Provider initialized |

### Phase I Debugging Scenario

```
1. Set breakpoint at t1 (ContinueVSPackage.cs:48)
2. F5 → experimental VS launches
3. Open Continue panel (View → Other Windows → Continue)
4. Breakpoint t1 hits → inspect:
   - Locals: this (package instance), cancellationToken
5. F10 to t1.1 → inspect thread ID changed
6. Continue F5 through t1.2-t1.6
7. At t1.6 → check exception == null (success)
8. Output pane shows:
   {"token":"t1","...} → t1.1 → t1.2 → ... → t1.6
```

---

## Phase II: Handler Loop Registration (t9-t20)

### Overview
MessageDispatcher registers 30+ handlers that process IDE commands and WebView messages. These handlers bridge the gap between VS IDE and Continue bridge.

### Token Map - Phase II (Select Key Handlers)

| Token | Handler | File | Description |
|-------|---------|------|-------------|
| **t9** | GetWorkspaceDirsHandler | ContinueToolWindowControl.xaml.cs | Handler loop start |
| **t10** | GetIdeInfoHandler | ContinueToolWindowControl.xaml.cs | IDE information  |
| **t11** | GetIdeSettingsHandler | ContinueToolWindowControl.xaml.cs | Settings retrieval |
| **t12** | GetUniqueIdHandler | ContinueToolWindowControl.xaml.cs | Unique ID generation |
| **t13** | IsTelemetryEnabledHandler | ContinueToolWindowControl.xaml.cs | Telemetry check |
| **t14** | IsWorkspaceRemoteHandler | ContinueToolWindowControl.xaml.cs | Remote workspace detection |
| **t15** | ReadFileHandler / FileExistsHandler | ContinueToolWindowControl.xaml.cs | File I/O handlers |
| **t16** | GetOpenFilesHandler / WriteFileHandler / SaveFileHandler / OpenFileHandler | ContinueToolWindowControl.xaml.cs | More file ops |
| **t17** | OpenUrlHandler / GetBranchHandler | ContinueToolWindowControl.xaml.cs | URL & Git handlers |
| **t18** | Context handlers (getContextItems, getSymbolsForFiles, loadSubmenuItems) | ContinueToolWindowControl.xaml.cs | Code context analysis |
| **t19** | Context docs handlers (addDocs, removeDocs, indexDocs) | ContinueToolWindowControl.xaml.cs | Documentation indexing |
| **t20** | Config handlers (addOpenAiKey, ideSettingsUpdate, deleteModel, etc.) | ContinueToolWindowControl.xaml.cs | Configuration management |

### Phase II Debugging Scenario

```
1. Set breakpoint after all _dispatcher.Register() calls (line ~90)
2. F5 → Open Continue panel
3. Breakpoint hits → inspect:
   - Locals: _dispatcher.Handlers should contain 30+ entries
4. In Watch, evaluate: _dispatcher.GetHandlerCount()
5. Open VS Output pane → filter "ContinueVS"
6. Make IDE request → traces show handler invocation (t9-t20)
```

---

## Phase III: WebView2 Initialization & Bridge (t21-t45)

### Overview
WPF control added to visual tree, Loaded event fires, WebView2 environment initialized, Continue bridge JavaScript injected, and operationality verified.

### Token Map - Phase III

| Token | File | Line | Description | Breakpoint Focus |
|-------|------|------|-------------|-----------------|
| **t21** | ContinueToolWindowControl.xaml.cs | ~100 | Constructor completion | All services initialized |
| **t22** | ContinueToolWindowControl.xaml.cs | 46 | InitializeComponent() | XAML loaded |
| **t23** | ContinueToolWindowControl.xaml.cs | (WPF event) | Control added to visual tree | OnLoaded event ready |
| **t24** | ContinueToolWindowControl.xaml.cs | (WPF routed) | Loaded event routed to handler | Handler method invoked |
| **t25** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | OnLoaded event triggered | Async task starting |
| **t26** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | OnLoaded async task started | WebView initialization begins |
| **t27** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | Guard check - WebView already initialized? | Early return if already done |
| **t28** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | GuiExtractor execution | HTML/CSS extracted from package |
| **t29** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | WebView2 element resource access | XAML WebView2 element found |
| **t30** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | CoreWebView2Environment creation | Environment initialized |
| **t31** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | VirtualHostNameMapping setup | Localhost mapping configured |
| **t32** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | CoreWebView2Controller initialization | Controller created |
| **t33** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | WebView2 element bounds set | Bounds/layout applied |
| **t34** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | CoreWebView2 reference obtained | _webView2.CoreWebView2 available |
| **t35** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | WebMessageReceived event handler registered | Message handler hooked |
| **t36** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | Bridge JavaScript injection | bridge.js injected into page |
| **t37** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | Navigation URL construction | URL built for Continue HTML |
| **t38** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | WebView2 navigation started | Navigate() called |
| **t39** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | WebView2 navigation completed | Page loaded |
| **t40** | ContinueToolWindowControl.xaml.cs | (OnLoaded) | WebView initialization flag set | _webViewInitialized = true |
| **t41** | Continue Bridge (JS) | (JavaScript) | Bridge global object verification | window.bridge exists |
| **t42** | Continue Bridge (JS) | (JavaScript) | Bridge SendMessage function test | bridge.SendMessage callable |
| **t43** | Continue Bridge (JS) | (JavaScript) | Bridge OnMessage function readiness | bridge.OnMessage registered |
| **t44** | WebviewPusher.cs | | First WebviewPusher.PushConfigUpdate call | Initial config pushed to WebView |
| **t45** | ContinueToolWindowControl.xaml.cs | (Final) | Full Bridge Operationality Confirmed | All systems ready |

### Phase III Debugging Scenario

```
1. Set breakpoint at t25 (OnLoaded event handler entry)
2. F5 → Open Continue panel (triggers OnLoaded)
3. Breakpoint t25 hits → inspect:
   - Locals: this (control), sender, e (routed event args)
4. Step F10 through t26 → t27 (guard check)
5. Continue F10 → t28-t34 (WebView environment setup)
6. At t35, inspect: _webView2.CoreWebView2 != null
7. Continue to t36 → JavaScript injection trace in Output pane
8. At t39 → page loaded, React GUI visible in WebView
9. At t40 → _webViewInitialized = true in Locals
10. Final step t45 → Output shows "Bridge operational"
```

---

## All 45 Tokens: Reference Table

| Token | Component | Status |
|-------|-----------|--------|
| t1-t8 | Package & Tool Window Init | Phase I |
| t9-t20 | Handler Loop Registration | Phase II |
| t21-t45 | WebView2 & Bridge Init | Phase III |

---

## Reading JSON Trace Output

Each line in Output pane is valid JSON:

```json
{
  "token": "t1.3.4",
  "timestamp": "2026-07-23T14:32:45.234Z",
  "component": "ContinueVSPackage",
  "duration_ms": 45,
  "metadata": {
    "service": "BridgeLogger",
    "status": "created"
  }
}
```

**Fields:**
- `token`: Step ID (t1, t1.1, ..., t45)
- `timestamp`: UTC creation time (ISO 8601)
- `component`: Code location (ContinueVSPackage, BridgeLogger, etc.)
- `duration_ms`: Elapsed milliseconds for scoped operations (null for immediate events)
- `metadata`: Contextual data (optional)

---

## Trace Export & Post-Mortem Analysis

### Export Traces

1. In Output pane, select all (Ctrl+A)
2. Copy (Ctrl+C)
3. Paste into file `E:\GitRepos\ContinueVS\trace-output.jsonl` (one JSON per line)

### Parse Traces

**Using jq (PowerShell):**
```powershell
cat trace-output.jsonl | ConvertFrom-Json | Select-Object token, duration_ms | Format-Table
```

**Using JSON viewer:**
- Paste each line into https://jsoncrack.com/
- Or use VS Code JSON extension

---

## Common Breakpoint Scenarios

### Scenario A: Package Initialization Failure
- Set breakpoint at t1.6 (exception handler)
- Run F5, observe exception details
- Check Output pane for t1.1-t1.5 traces
- Identify which service failed to initialize

### Scenario B: Handler Registration Issues
- Set breakpoint after t5 (line ~90 in constructor)
- Expand `_dispatcher` → inspect Handlers collection
- Count entries (should be ~30)
- Look for missing handlers in t9-t20 trace output

### Scenario C: WebView2 Initialization Hang
- Set breakpoint at t30 (CoreWebView2Environment)
- If timeout, check: GPU acceleration disabled? WebView2 Runtime installed?
- At t35, verify event handler properly registered
- At t39, check navigation URL is correct

### Scenario D: Bridge Message Delivery
- Set breakpoint in WebMessageReceived handler
- Run a Continue command in WebView GUI
- Inspect message JSON in debugger
- Verify t41-t43 traces show bridge readiness
- Check t44 for first config push

---

## Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| F5 doesn't start experimental VS | VSIX build failed | Rebuild: Ctrl+Shift+B → check for errors |
| Breakpoints not hit | Symbols stale | Clean bin/obj, rebuild, restart debug |
| Output pane shows no "ContinueVS" | Traces not written yet | Breakpoint must hit first; manually open Continue panel |
| Empty traces at t1 | InitializeAsync not called | Force open: View → Other Windows → Continue |
| WebView shows blank page | t39 navigation failed | Check t30-t37 traces; verify HTML file exists |
| Bridge.SendMessage not found | t36 JavaScript injection failed | Check t28 (GUI extraction), t35 (handler registration) |

---

## Integration with CI/CD

For automated testing:
1. Use `docs/DEBUG-T1-RUNTIME-GUIDE.md` for detailed t1 diagnostics
2. Enable trace collection in CI environment
3. Parse `trace-output.jsonl` to verify token sequencing
4. Flag missing tokens or excessive durations (>1s per phase)
5. Store traces for post-release debugging

---

## Next Steps

Once all 45 tokens are traced and validated:
1. Document any timing anomalies (slow handlers, hung WebView)
2. Profile memory usage across phases
3. Identify critical paths (t1 → t9 → t41 essential; others optional)
4. Plan optimizations (parallel initialization, lazy loading)
5. Reference this guide in production incident response

---

## File Locations

- **Main Instrumentation**: `src/VSIXProject1/ContinueVSPackage.cs` (t1-t8)
- **Tool Window**: `src/VSIXProject1/UI/ContinueToolWindowControl.xaml.cs` (t4-t20, t21-t39)
- **Tracer Infrastructure**: `src/VSIXProject1/Diagnostics/ExecutionTracer.cs`
- **Tests**: `src/VSIXProject1.Tests/Diagnostics/ExecutionTracerTests.cs`
- **This Guide**: `docs/DEBUG-BRIDGE-EXECUTION-GUIDE.md`
- **Phase I Detail**: `docs/DEBUG-T1-RUNTIME-GUIDE.md` (t1 only, comprehensive)

---

## Related Documentation

- Continue Bridge Architecture: `docs/BRIDGE-DEVELOPER-GUIDE.md`
- Performance Tuning: `docs/PERFORMANCE-TUNING-GUIDE.md`
- Session Context: `docs/session-context.md`
