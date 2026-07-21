# Troubleshooting Guide

The bridge is a complex system integrating handler dispatch, WebView messaging, npm package management, and crash recovery. This guide provides symptom-based diagnosis and remediation for common production issues.

---

## Quick Lookup Table

Use this table to quickly identify your symptom and find initial remediation steps.

| Symptom | Error Code | Root Cause | Quick Fix | Full Guide |
|---------|------------|-----------|-----------|-----------|
| Handler doesn't respond | -32603 | Handler timeout or missing handler | Check Step 71 registry; increase timeout (Step 64) | Handler Failures |
| Invalid JSON-RPC error | -32600 | Malformed message envelope | Validate message structure (Step 73) | Compliance Violations |
| Bridge won't start | N/A | npm package missing or npm validation fails | Re-download packages (Step 35); verify checksums (Step 37) | Integration Problems |
| Slow handler response | N/A | p99 latency exceeds baseline | Profile with Step 96; compare to Step 112 baseline | Performance Degradation |
| Memory grows unbounded | N/A | Memory leak in handler context or cache | Run Step 99 sustained load; check Step 105 state | Performance Degradation |
| Bridge keeps restarting | N/A | Crash recovery loop (exponential backoff) | Check crash diagnostics (~/.continue/crash-diagnostics/) | Crash Recovery |
| Settings not applied | -32603 | Config file missing or invalid JSON | Create ~/.continue/config.json; validate schema (Step 104) | Configuration Issues |
| Cascading handler errors | -32603 | One handler failure triggers others | Check Step 74 error recovery; verify isolation | Handler Failures |
| Bridge in degraded mode | N/A | 2+ consecutive crashes triggered fallback | Identify crash root cause; restart with full handlers | Crash Recovery |
| WebView not responding | N/A | Message routing failure or bridge disconnected | Check Step 47 routing; restart bridge | Integration Problems |

---

## Issue Categories & Decision Trees

### A. Handler Failures (8–10 scenarios)

**Scenario A1: Handler not responding**
- **Symptom**: Handler invoked but no response within timeout period
- **Root Cause**: 
  1. Handler not registered in Step 71 dispatcher
  2. Handler timeout policy (Step 64) too short for handler complexity
  3. Handler execution blocked by slow operation or lock contention
  4. Message validation (Step 73) failing before handler invocation
- **Diagnosis Steps**:
  1. Check if handler is registered: `curl http://localhost:3000/bridge/handlers | jq '.[] | select(.name=="refactor")'`
  2. Verify handler timeout policy: Compare actual timeout (Step 71 registry) to complexity tier (fast/medium/slow)
  3. Profile handler latency: Run Step 96 profiler; check p99 latency per handler
  4. Check validation rules: Run message through Step 73 validator; verify envelope structure
- **Remediation**:
  - If handler not found: Re-register handler in Step 71 handler-registry.mjs
  - If timeout too short: Increase timeout policy (Step 64) for handler tier; verify with Step 98 baseline
  - If handler slow: Profile with Node.js --inspect (Step 96); optimize handler code or increase timeout
  - If validation fails: Fix message envelope structure; verify messageId, messageType, data fields

**Scenario A2: Handler returns error (-32603 InternalError)**
- **Symptom**: Handler invoked successfully but returns -32603 error with stack trace
- **Root Cause**:
  1. Handler code throws exception (logic error, null reference, invalid operation)
  2. Handler missing required dependency (Step 104 config, Step 105 state, external service)
  3. Handler violates JSON-RPC response schema (Step 63)
  4. Handler cascading failure from upstream handler or shared resource
- **Diagnosis Steps**:
  1. Check handler response schema: Review Step 63 bridge protocol adapter; verify success/error object structure
  2. Review handler code: Inspect handler file in src/versions/v2.0.0/lib/handlers/
  3. Check dependencies: Verify config file exists (Step 104), state file accessible (Step 105), external services responding
  4. Check crash recovery logs: Review ~/.continue/crash-diagnostics/ for related failures
- **Remediation**:
  - If logic error: Fix handler code; redeploy handler
  - If missing dependency: Create config file (Step 104), verify state persistence (Step 105), check external service availability
  - If schema violation: Review Step 63 adapter; ensure response wraps result in { success: true, result: ... } or { success: false, error: ... }
  - If cascading failure: Check Step 74 error recovery; verify handler isolation (Step 47 middleware)

**Scenario A3: Cascading errors across handlers**
- **Symptom**: One handler failure causes multiple other handlers to fail or hang
- **Root Cause**:
  1. Shared resource corrupted (cache, state file, message queue)
  2. Handler not isolated (Step 74 error recovery middleware not preventing propagation)
  3. Message queue saturation (Step 65 priority queue full) blocking subsequent handlers
  4. State persistence conflict (Step 105) from concurrent handler writes
- **Diagnosis Steps**:
  1. Check handler isolation: Review Step 74 error recovery; verify each handler wrapped in try-catch
  2. Check message queue state: Monitor Step 65 priority queue; verify queue not full
  3. Check shared state: Verify Step 105 state file not corrupted; review state persistence logs
  4. Check cascade log: Review core-server.js logs for error propagation pattern
- **Remediation**:
  - Clear message queue: Restart bridge (clears Step 65 priority queue)
  - Reset state file: Delete ~/.continue/bridge-state.json; restart bridge (Step 105 recovery)
  - Verify handler isolation: Check Step 74 error recovery middleware wraps all handlers
  - Isolate failing handler: Temporarily disable handler in Step 71 registry; re-enable after root cause fixed

---

### B. Performance Degradation (6–8 scenarios)

**Scenario B1: p99 latency regression exceeds baseline**
- **Symptom**: Handler response time slowly or suddenly increases; users notice sluggish behavior
- **Baseline Reference** (from Step 112):
  - **Fast tier** (editor state, hover, search): p99 <2,000 ms; critical if >3,000 ms (+50%)
  - **Medium tier** (refactor, complete, format): p99 <10,000 ms; critical if >15,000 ms (+50%)
  - **Slow tier** (debug, git, terminal): p99 <30,000 ms; critical if >45,000 ms (+50%)
- **Root Cause**:
  1. Handler performing slow I/O or CPU-intensive operation
  2. Timeout policy (Step 64) mismatch: too short → false timeout, too long → user waits unnecessarily
  3. Symbol cache thrashing: large workspace causing repeated cache regeneration
  4. Concurrent load spike: message queue saturation (Step 65)
  5. Middleware overhead: Steps 72–74 (logging, validation, error recovery) overhead
- **Diagnosis Steps**:
  1. Measure current p99 latency: Run Step 96 profiler; compare p99 to Step 112 baseline per handler
  2. Profile handler invocation: Run Node.js --inspect; attach debugger; measure handler execution time
  3. Check concurrent load: Monitor Step 99 concurrent stress test; verify p99 <500ms @100 parallel
  4. Check symbol cache: Review Step 53 symbol extractor logs; check cache hit/miss ratio
  5. Compare to Step 98 baseline: Run Step 98 performance test; verify current results vs. baseline
- **Remediation**:
  - If handler slow: Optimize handler code or increase timeout policy (Step 64) for tier
  - If symbol cache thrashing: Reload cache (Step 94 reload handler, scope='symbols')
  - If concurrent load: Increase priority queue size (Step 65 config); reduce user concurrency
  - If middleware overhead: Reduce logging verbosity (Step 72 logger levels); profile middleware (Steps 72–74)

**Scenario B2: Memory leak or unbounded growth**
- **Symptom**: Memory usage grows over time; bridge eventually crashes with OOM error
- **Baseline Reference** (from Step 99):
  - Peak memory: <50 MB (critical if >60 MB)
  - Average delta: <10 KB per 30 seconds
  - Sustained load: 100 concurrent requests for 5 minutes should not trigger OOM
- **Root Cause**:
  1. Circular references in handler context (object graph not garbage collected)
  2. Event listener not cleaned up (Step 51 subscriptions, Step 52 document changes)
  3. Document cache growing unbounded (Step 52 document provider)
  4. Message queue retention (Step 65) holding stale messages
  5. Diagnostics accumulation (Step 103 crash recovery) not truncated
- **Diagnosis Steps**:
  1. Run sustained load test: Execute Step 99 sustained load test; monitor memory growth trend
  2. Attach memory profiler: Run Node.js --inspect; use Chrome DevTools memory tab
  3. Check handler lifecycle: Review handler dispose() methods; verify cleanup on unsubscribe
  4. Check document cache: Monitor Step 52 document provider; verify cache size limits
  5. Check diagnostics files: Review ~/.continue/crash-diagnostics/ size; verify rotation policy
- **Remediation**:
  - Force garbage collection: Manually trigger gc() in profiler (Step 96)
  - Clear document cache: Call Step 94 reload handler with scope='documents'
  - Review handler lifecycle: Check dispose() methods in all 20 handlers (Step 76–87 handlers)
  - Truncate diagnostics: Delete old files in ~/.continue/crash-diagnostics/; verify rotation policy (Step 103)

**Scenario B3: Throughput drop (requests/sec below baseline)**
- **Symptom**: Bridge processes fewer requests per second; queue builds up
- **Baseline Reference** (from Step 98):
  - Target throughput: ~320 requests/sec (varies by handler mix)
  - Minimum acceptable: >300 requests/sec
  - Critical threshold: <150 requests/sec (-50%)
- **Root Cause**:
  1. Message queue saturation (Step 65 priority queue full) blocking new requests
  2. Middleware overhead (Steps 72–74) consuming CPU: logging, validation, error recovery
  3. Handler registration overhead (Step 71) from frequent lookups
  4. Concurrent handler contention on shared resources
  5. Validation overhead (Step 73) on large/complex message payloads
- **Diagnosis Steps**:
  1. Measure current throughput: Run Step 98 performance test; compare throughput (msg/sec) to baseline
  2. Check priority queue: Monitor Step 65 queue depth; verify queue size config
  3. Check middleware performance: Profile Steps 72–74; measure per-middleware latency
  4. Check handler registry performance: Review Step 71 lookup latency; verify caching
  5. Check validation overhead: Profile Step 73 validator on large payloads
- **Remediation**:
  - Increase queue size: Update Step 65 config; increase maxQueueSize
  - Disable verbose logging: Reduce log level (Step 72 logger) to 'warn' or 'error'
  - Profile middleware: Measure Steps 72–74 execution time; optimize bottleneck
  - Cache handler lookups: Review Step 71 registry; verify fast path caching

---

### C. Configuration Issues (5–6 scenarios)

**Scenario C1: Config file not found**
- **Symptom**: Bridge starts but settings not applied; users cannot configure model/API keys
- **Root Cause**:
  1. First run: User has not created ~/.continue/config.json
  2. Path mismatch: Bridge looking in wrong location (should be ~/.continue/config.json)
  3. Permissions: File exists but not readable by bridge process
  4. Corruption: File deleted unexpectedly (user action or system issue)
- **Diagnosis Steps**:
  1. Check file existence: `ls -la ~/.continue/config.json`
  2. Check permissions: `ls -la ~/.continue/` (should be user-readable and writable)
  3. Check file contents: `jq . ~/.continue/config.json` (should be valid JSON)
  4. Check bridge logs: Verify bridge looking in correct location (Step 104 config handler)
- **Remediation**:
  - Create default config: Call Step 95 bridge:applySettings handler with default models
  - Fix permissions: `chmod 755 ~/.continue/`; `chmod 644 ~/.continue/config.json`
  - Restore from backup: If config was corrupted, user may need to re-enter settings

**Scenario C2: State persistence failure**
- **Symptom**: Bridge loses state between restarts; cached data not recovered
- **Root Cause**:
  1. State file (~/.continue/bridge-state.json) corrupted or unreadable
  2. Permissions: File exists but not writable by bridge process
  3. Disk full: Bridge cannot write state file on shutdown
  4. Abnormal termination: Bridge crashed before writing state (data loss)
- **Diagnosis Steps**:
  1. Check file existence: `ls -la ~/.continue/bridge-state.json`
  2. Check file size: `wc -c ~/.continue/bridge-state.json` (should be >0 bytes)
  3. Check file validity: `jq . ~/.continue/bridge-state.json` (should be valid JSON)
  4. Check disk space: `df -h ~/.continue/` (should have >10MB free)
- **Remediation**:
  - Delete corrupt state: `rm ~/.continue/bridge-state.json`; restart bridge (Step 105 recovery)
  - Fix permissions: `chmod 755 ~/.continue/`; `chmod 644 ~/.continue/bridge-state.json`
  - Free disk space: Delete stale crash diagnostics (~/.continue/crash-diagnostics/); re-run bridge
  - Graceful recovery: Bridge detects corruption on startup (Step 105); auto-resets to defaults

---

### D. Crash Recovery (4–5 scenarios)

**Scenario D1: Bridge keeps restarting (auto-restart loop)**
- **Symptom**: Bridge process restarts repeatedly every few seconds; unable to stay running
- **Root Cause**:
  1. Handler crash (logic error, null reference, unhandled promise rejection)
  2. npm package validation fails on startup (Step 12 npm validation)
  3. Configuration invalid: bridge:applySettings handler fails (Step 104)
  4. Crash recovery exponential backoff (Step 103) hitting max retries
- **Diagnosis Steps**:
  1. Check crash diagnostics: Review ~/.continue/crash-diagnostics/ for recent crash reports (JSON files)
  2. Check bridge logs: Inspect core-server.js console output; look for stack traces
  3. Check npm validation: Run Step 12 npm validation manually; verify checksums (Step 37)
  4. Check recovery state: Review ~/.continue/crash-recovery.json for backoff attempts
- **Remediation**:
  - Identify crash handler: Check crash reports to identify which handler is crashing
  - Disable problematic handler: Temporarily disable handler in Step 71 registry (set enabled=false)
  - Verify npm packages: Re-download packages (Step 35); re-verify checksums (Step 37)
  - Reset recovery state: Delete ~/.continue/crash-recovery.json; restart bridge (clears backoff counter)

**Scenario D2: Bridge enters degraded mode**
- **Symptom**: Bridge running but only partial handlers available; some features disabled
- **Root Cause**:
  1. 2+ consecutive crashes detected (Step 103 strategy)
  2. Bridge auto-disabled problematic handlers to prevent infinite crash loop
  3. Degraded mode a safety net: bridge keeps running with reduced capability
- **Diagnosis Steps**:
  1. Check crash recovery logs: Review ~/.continue/crash-recovery.json for crash history
  2. Check disabled handlers: Call bridge:getHandlerStatus; verify which handlers are disabled
  3. Check recovery state: Review Step 103 crash recovery manager state
- **Remediation**:
  - Identify crash root cause: Check crash diagnostics for original crash handler
  - Fix root cause: Update problematic handler code; redeploy
  - Restart bridge with full handlers: Delete ~/.continue/crash-recovery.json; restart (Step 103 recovery)

**Scenario D3: Bridge won't exit cleanly**
- **Symptom**: Bridge process hangs on shutdown (graceful shutdown timeout exceeded)
- **Root Cause**:
  1. Handler not responding to cancellation signal (unresponsive long-running operation)
  2. Event listener not cleaned up; still waiting for event
  3. State persistence (Step 105) blocking on file I/O
  4. External service connection not closed
- **Diagnosis Steps**:
  1. Check shutdown logs: Review core-server.js logs for shutdown messages
  2. Check graceful shutdown timeout: Step 103 crash recovery uses 10s timeout
  3. Check handler cleanup: Review handler dispose() methods
- **Remediation**:
  - Force process kill: If bridge hangs >10s, send SIGKILL to process
  - Review handler lifecycle: Check dispose() methods; ensure cleanup completes
  - Clear state file: Delete ~/.continue/bridge-state.json; restart (forces clean state)

---

### E. Integration Problems (5–6 scenarios)

**Scenario E1: Bridge won't start**
- **Symptom**: `npm start` fails; bridge process exits immediately
- **Root Cause**:
  1. npm packages missing or version mismatch (Step 35 download)
  2. npm package validation fails (Step 12 integrity check)
  3. Continue npm package checksum mismatch (Step 37 verification)
  4. Core-server.js entry point fails to initialize (Step 13)
  5. Handler dispatcher fails on load (Step 14)
- **Diagnosis Steps**:
  1. Check npm packages: Verify `.cache/npm-packages/v2.0.0/` exists and contains files
  2. Run npm validation: Execute Step 12 npm validation manually; check output
  3. Verify checksums: Compare calculated checksum to Step 37 expected checksums
  4. Check console output: Review npm start console for error messages or stack traces
  5. Check core-server.js: Verify file exists at src/versions/v2.0.0/core-server.js
- **Remediation**:
  - Re-download packages: Delete .cache/npm-packages/v2.0.0/; run Step 35 download
  - Verify checksums: Run Step 37 checksum generation; compare to manifest (Step 4)
  - Check npm install: Run `npm install --prefix src/versions/v2.0.0` (install node_modules)
  - Verify core-server.js: Check file exists and has no syntax errors

**Scenario E2: WebView not responding**
- **Symptom**: Bridge running but IDE WebView doesn't receive messages; Continue sidebar appears frozen
- **Root Cause**:
  1. Message routing middleware (Step 47) not forwarding to WebView
  2. WebView injector (Step 43) failed to inject bridge bootstrap
  3. WebView message pusher (Step 44) disconnected from bridge
  4. Network/transport issue (Step 19–21 stdio transport) between IDE and bridge
- **Diagnosis Steps**:
  1. Check bridge logs: Review core-server.js for message routing errors
  2. Check WebView connection: Verify Step 43 injector ran successfully
  3. Check message pusher: Verify Step 44 message pusher connected to bridge
  4. Check IDE logs: Review Visual Studio output pane for transport errors
  5. Check transport: Verify Step 19–21 stdio transport active (check process handles)
- **Remediation**:
  - Restart bridge: Stop bridge; restart with `npm start`
  - Re-inject WebView: Reload Continue sidebar in IDE (may trigger Step 43 injector again)
  - Check IDE connection: Verify IDE has bridge process handle; check Windows process explorer
  - Reset transport: Kill all node processes; restart IDE and bridge

**Scenario E3: Message routing error (handler never invoked)**
- **Symptom**: User sends request via WebView; handler never receives message; timeout occurs
- **Root Cause**:
  1. Message routing middleware (Step 47) discarded message
  2. Handler not registered (Step 71)
  3. Message validation (Step 73) rejected message before routing
  4. Message type mismatch: WebView sent unsupported message type
- **Diagnosis Steps**:
  1. Check message routing logs: Enable Step 72 logging; trace message through middleware
  2. Check handler registry: Verify handler registered in Step 71 registry
  3. Check validation: Run message through Step 73 validator; verify envelope format
  4. Check message trace: Review core-server.js console for message arrival and routing
- **Remediation**:
  - Verify message format: Check WebView message format matches Step 62 message type definitions
  - Verify handler: Confirm handler in Step 71 registry and enabled
  - Re-register handler: Update Step 71 handler-registry.mjs; restart bridge
  - Enable tracing: Increase logging (Step 72) to trace message flow

---

## Step-by-Step Diagnostics

### Handler Diagnosis Flowchart

**Question 1**: Is handler registered?
```
curl http://localhost:3000/bridge/handlers | jq '.[] | select(.name=="refactor")'
```
- **YES**: Go to Question 2
- **NO**: Handler missing from Step 71 registry → Re-register handler in handler-registry.mjs

**Question 2**: Does message validate?
```
# Send test message with bridge:echo (should fail validation with -32602)
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{"messageId":"1"}'  # Missing messageType and data
```
- **PASS** (returns -32602 for invalid format): Go to Question 3
- **FAIL** (other error): Check Step 73 validation rules; verify envelope structure

**Question 3**: Does timeout enforce?
```
# Run Step 96 profiler; check p99 latency per handler
curl http://localhost:3000/bridge/profiler | jq '.handlers[] | {name, p99}'
```
- **PASS** (p99 <threshold from Step 112): Go to Question 4
- **FAIL** (p99 >threshold): Handler slow → Increase timeout (Step 64) or optimize handler

**Question 4**: Are metrics recorded?
```
# Check Step 109 aggregator; verify telemetry output
curl http://localhost:3000/bridge/metrics | jq '.handlers | length'
```
- **PASS** (metrics returned): Handler healthy; issue elsewhere
- **FAIL** (no metrics): Handler not invoked; check crash diagnostics

---

### Performance Diagnosis Flowchart

**Step 1**: Measure current latency
```
# Run Step 96 profiler; compare p99 to Step 112 baseline per handler tier
curl http://localhost:3000/bridge/profiler | jq '.handlers[] | {name, p50, p95, p99}'
```
- Compare p99 to tier baselines: Fast <2s, Medium <10s, Slow <30s
- If p99 >baseline: Continue to Step 2
- If p99 <baseline: Issue not latency-related

**Step 2**: Profile with Step 96 profiler
```
# Identify slowest handlers
curl http://localhost:3000/bridge/profiler | jq '.handlers | sort_by(.p99) | reverse | .[0:5]'
```
- Identify top 5 slowest handlers
- If multiple slow: Concurrent load issue; continue to Step 4
- If single slow: Handler optimization issue; continue to Step 3

**Step 3**: Check memory consumption
```
# Run Step 99 sustained load test; analyze growth trend
npm test -- --grep "sustained-load" --prefix src/versions/v2.0.0
```
- Monitor memory delta over 30s
- If >10KB/30s: Memory leak; continue to Scenario B2 remediation
- If <10KB/30s: Issue not memory

**Step 4**: Analyze error rate
```
# Compare current error rate to Step 99 baseline (<1% target)
curl http://localhost:3000/bridge/profiler | jq '.metrics | {totalRequests, totalErrors, errorRate}'
```
- If error rate >1%: Errors causing slowness; continue to Handler Failures section
- If error rate <1%: Error not primary issue

**Step 5**: Profile handler invocation
```
# Use Node.js --inspect; attach Chrome DevTools
node --inspect=9229 src/versions/v2.0.0/core-server.js
# Open chrome://inspect; record performance profile
```
- Measure handler execution time
- Compare to Step 98 baseline
- Identify bottleneck: I/O, CPU, or waiting on resource

---

## Common Errors & Remediation

### Compliance Violations (10+ examples from Step 97)

**Error**: -32600 InvalidRequest
- **Message**: "Invalid Request: missing required field 'messageType'"
- **Root Cause**: Message envelope missing required field (from Step 73 validation)
- **Solution**: Validate message structure; check Step 73 validation rules; ensure all fields present

**Error**: -32602 InvalidParams
- **Message**: "Invalid Params: 'timeout' must be positive integer, got 'abc'"
- **Root Cause**: Request parameter type mismatch or out-of-range (from Step 73)
- **Solution**: Validate params against Step 95/104 schema; verify type and range

**Error**: -32601 MethodNotFound (handler not registered)
- **Message**: "Handler 'refactor' not found in dispatcher registry"
- **Root Cause**: Step 71 registration missing or handler disabled
- **Solution**: Verify handler entry in Step 71 handler-registry.mjs; re-register if needed

**Error**: -32603 InternalError (handler execution failure)
- **Message**: "Handler error: TypeError: cannot read property 'name' of undefined"
- **Root Cause**: Handler code error (null reference, type mismatch, missing dependency)
- **Solution**: Check handler code; verify dependencies available; fix logic error

---

### Performance Gate Failures (6+ scenarios from Step 98–99)

**Gate**: p99 latency exceeds threshold
- **Symptom**: Handler response time >tier baseline (e.g., >2s for fast tier)
- **Cause**: Slow handler, concurrent load, or middleware overhead
- **Action**: Profile with Step 96; compare to Step 98 baseline; increase timeout (Step 64) or optimize

**Gate**: Memory growth >10KB/30s
- **Symptom**: Memory usage increasing during sustained load
- **Cause**: Potential memory leak (circular refs, uncleaned listeners, unbounded cache)
- **Action**: Run Step 99 sustained load; attach memory profiler (Node.js --inspect)

**Gate**: Throughput <target (e.g., <300 msg/sec vs. 320 baseline)
- **Symptom**: Bridge processes fewer requests per second
- **Cause**: Queue saturation, middleware overhead, validation bottleneck
- **Action**: Measure throughput (Step 98); profile middleware (Steps 72–74); tune queue (Step 65)

**Gate**: Error rate >1% absolute increase
- **Symptom**: Unintended errors appearing during normal operation
- **Cause**: Validation failures, timeout enforcement, or handler crashes
- **Action**: Run Step 99 error injection baseline; compare current to baseline; identify error source

---

### Regression Detection Alerts (from Step 112 thresholds)

**Alert Severity: CRITICAL** (>50% regression)
- **Latency**: p99 >50% higher than baseline
- **Action**: Release blocked; investigate immediately; compare Step 98 baseline
- **Example**: Fast tier baseline p99=1500ms → current p99=2250ms (50% increase) → critical

**Alert Severity: HIGH** (>25% regression)
- **Latency**: p99 >25% higher than baseline
- **Memory**: >20MB growth from baseline
- **Action**: Escalate for investigation before release; flag for review
- **Example**: Medium tier baseline p99=7000ms → current p99=8750ms (25% increase) → high

**Alert Severity: MEDIUM** (>15% regression)
- **Latency**: p99 >15% higher than baseline
- **Memory**: >10MB growth from baseline
- **Error Rate**: >2% absolute increase
- **Action**: Log for future optimization; monitor in production
- **Example**: Slow tier baseline p99=25000ms → current p99=28750ms (15% increase) → medium

---

## Reference Sections

### JSON-RPC Error Codes (from Step 73)

| Code | Name | Cause | Handler Response |
|------|------|-------|------------------|
| -32700 | ParseError | Malformed JSON on wire | Not typically returned; readline handles parsing |
| -32600 | InvalidRequest | Missing required envelope field (messageId, messageType, data) | Return -32600 with field name in error |
| -32602 | InvalidParams | Wrong param type or out-of-range | Return -32602 with field name and expected type |
| -32603 | InternalError | Handler execution failure (exception, crash, timeout) | Return -32603 with stack trace |
| -32000 to -32099 | Server Error (reserved) | Not used in current implementation | Not applicable |

### Timeout Policies (from Step 64)

| Tier | Default (ms) | Use Case | Handler Examples | Tuning |
|------|---|---|---|---|
| **Fast** | 2,000 | Simple queries, state reads | editor state, hover, settings load | Increase if timeout appears in logs >5% |
| **Medium** | 10,000 | Complex analysis, I/O | refactor, completion, format | Increase if p99 latency >8000ms |
| **Slow** | 30,000 | External integration | debug session, git ops, terminal | Increase if external service slow |

### Severity Thresholds (from Step 112 regression gates)

| Metric | Critical | High | Medium | Low |
|--------|----------|------|--------|-----|
| **Latency p99** | >50% regression | >25% regression | >15% regression | >10% regression |
| **Throughput** | >40% drop | >20% drop | >10% drop | >5% drop |
| **Memory** | >50MB | >20MB | >10MB | N/A |
| **Error Rate** | >10% abs | >5% abs | >2% abs | >1% abs |

### Handler Registry (from Step 71)

The bridge supports 20 handlers across 5 categories:

**Factory Handlers** (initialize resources):
- `bridge:applySettings` - Load model/API configuration
- `bridge:getEditorState` - Fetch current editor context
- `bridge:onEditorStateChange` - Subscribe to editor updates

**Bidirectional Handlers** (request-response):
- `bridge:refactor` - Apply refactor action
- `bridge:fixSuggestion` - Apply fix suggestion
- `bridge:applyEdit` - Apply text edit
- `bridge:formatDocument` - Format code

**Search & Navigation Handlers**:
- `bridge:search` - Search files/symbols
- `bridge:goToDefinition` - Navigate to definition
- `bridge:findReferences` - Find all references
- `bridge:codeCompletion` - Get completions
- `bridge:hoverInfo` - Get hover info

**Integration Handlers**:
- `bridge:git` - Git operations
- `bridge:terminal` - Terminal integration
- `bridge:filesystem` - File operations
- `bridge:projectInfo` - Project metadata

**Metadata Handlers**:
- `bridge:testExplorer` - Test discovery/execution
- `bridge:debugSession` - Debug session operations
- `bridge:inlineMessage` - Inline message display
- `bridge:sidebar` - Sidebar UI updates

Each handler has:
- **Timeout policy**: fast (2s), medium (10s), or slow (30s)
- **Stability tier**: core (required for bridge function) or experimental (optional)
- **Error mapping**: Handler-specific error codes

---

## When to Escalate

**Escalation Level 1: Self-Service**
- User can resolve using TROUBLESHOOTING-GUIDE.md decision trees
- Examples: Bridge won't start (check npm packages), handler timeout (increase timeout)

**Escalation Level 2: Support Team**
- Issue requires HANDLER-ERROR-CATALOG.mjs lookup + PERFORMANCE-TUNING-GUIDE.md
- Examples: Memory leak diagnosis, handler performance profiling
- Action: Gather diagnostics; consult error catalog; apply tuning guide

**Escalation Level 3: Engineering**
- Issue requires running Step 97–99 tests; reviewing profiler data (Step 96)
- Examples: Regression detected (compare to Step 112 baseline), cascading handler failures
- Action: Run compliance tests (Step 97); run performance tests (Step 98–99); review profiler

**Escalation Level 4: Engineering Deep-Dive**
- Issue requires code review, architecture analysis, or cross-team investigation
- Examples: Systematic crash loop, memory leak in Node.js core, transport layer issue
- Action: File issue with step-specific context + diagnostic bundle (~/.continue/crash-diagnostics/)

---

## Diagnostic Data to Collect

When escalating issues, collect:

1. **Bridge logs**: Core-server.js console output (save to file)
2. **Crash diagnostics**: ~/.continue/crash-diagnostics/ (all JSON files)
3. **State files**: ~/.continue/bridge-state.json, ~/.continue/crash-recovery.json (if exists)
4. **Performance metrics**: Output from Step 96 profiler (curl http://localhost:3000/bridge/profiler)
5. **Handler registry**: Output from `curl http://localhost:3000/bridge/handlers`
6. **Test results**: Output from Step 97–99 tests (compliance, performance, stress)
7. **System info**: OS version, IDE version, Node.js version (node --version)
8. **Reproduction steps**: Exact user actions that trigger issue

---

## Additional Resources

- **HANDLER-ERROR-CATALOG.mjs**: Programmatic error index (~/src/versions/v2.0.0/tests/mocks/handler-error-catalog.mjs)
- **PERFORMANCE-TUNING-GUIDE.md**: Operator's reference for optimization (~/docs/PERFORMANCE-TUNING-GUIDE.md)
- **TROUBLESHOOTING-INTEGRATION-CHECKLIST.md**: QA/Ops verification checklist (~/docs/TROUBLESHOOTING-INTEGRATION-CHECKLIST.md)
- **Step 112 Regression Guide**: HANDLER-REGRESSION-GUIDE.md (~/docs/HANDLER-REGRESSION-GUIDE.md)
- **Step 113 Manual Testing Guide**: MANUAL-TESTING-GUIDE.md (~/docs/MANUAL-TESTING-GUIDE.md)
