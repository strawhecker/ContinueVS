# Troubleshooting Integration Checklist

A comprehensive verification checklist for QA/Ops teams to validate bridge health, diagnose issues, and sign off before release. Use this checklist before shipping, when troubleshooting production issues, or when verifying fixes.

---

## Pre-Troubleshooting Verification (Run First)

Before diving into detailed diagnostics, run these quick checks to establish baseline health.

### Component Status Checklist

Verify bridge infrastructure is in place:

- [ ] **Bridge process running**
  - **Windows**: Open Task Manager → find "node" process
  - **Linux/Mac**: Run `ps aux | grep "core-server.js"`
  - **Action**: If not running, start with `npm start --prefix src/versions/v2.0.0`

- [ ] **npm packages cached**
  - **Check**: `ls -la .cache/npm-packages/v2.0.0/`
  - **Expected**: Directory exists with v2.0.0 subdirectory
  - **Action**: If missing, run `npm run download-packages --prefix src/versions/v2.0.0` (Step 35)

- [ ] **Configuration file exists**
  - **Check**: `ls -la ~/.continue/config.json`
  - **Expected**: File exists and is readable
  - **Action**: If missing, create with bridge:applySettings handler (Step 95)

- [ ] **Handler registry loaded**
  - **Check**: Review core-server.js console output for "Registered 20 handlers" message
  - **Expected**: All 20 handlers loaded successfully
  - **Action**: If errors, check handler-registry.mjs (Step 71) syntax

---

### Quick Health Check (5-Minute Validation)

Run these commands in sequence to validate bridge is operational:

**Step 1: Verify bridge starts**
```bash
npm start --prefix src/versions/v2.0.0
# Expected output: "Bridge listening on :3000" or similar
# Wait 5 seconds for startup
# If error: Check Node.js version (should be 14+)
```

**Step 2: Check handler registration (20 handlers expected)**
```bash
curl http://localhost:3000/bridge/handlers | jq 'length'
# Expected: 20
# If <20: Some handlers failed to register (Step 71 issue)
# If error: Bridge not responding; restart bridge
```

**Step 3: Test validation middleware**
```bash
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{"messageId":"1","messageType":"bridge:echo"}'
# Expected: HTTP 200 with -32602 error code (invalid message format)
# If error: Validation middleware not running (Step 73 issue)
```

**Step 4: Check profiler metrics**
```bash
curl http://localhost:3000/bridge/profiler | jq '.handlers | length'
# Expected: 20 handlers with latency metrics
# If error: Profiler not running (Step 96 issue)
```

---

## Component-by-Component Diagnostics

Detailed diagnostics for each subsystem. Use when Quick Health Check fails or for deeper investigation.

---

### Step 104: Configuration Handler

Configuration file location: `~/.continue/config.json`

**Verification Steps**:

- [ ] **File exists**
  ```bash
  ls -la ~/.continue/config.json
  # Expected: -rw-r--r-- ... config.json
  # If "No such file": Config missing; user needs to create via bridge:applySettings
  ```

- [ ] **Valid JSON**
  ```bash
  jq . ~/.continue/config.json
  # Expected: Formatted JSON output
  # If parse error: File corrupted; user must recreate
  ```

- [ ] **Has models array**
  ```bash
  jq '.models | length' ~/.continue/config.json
  # Expected: >0 (at least one model configured)
  # If 0: User hasn't configured models yet
  ```

- [ ] **Readable permissions**
  ```bash
  ls -la ~/.continue/config.json
  # Expected: User can read and write (-rw-r--r-- or similar)
  # If permission denied: Fix with `chmod 644 ~/.continue/config.json`
  ```

**Remediation**:
```bash
# If config missing, create it via handler:
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{
    "messageId": "1",
    "messageType": "bridge:applySettings",
    "data": {
      "models": [
        {
          "title": "gpt-4",
          "provider": "openai",
          "model": "gpt-4"
        }
      ]
    }
  }'
```

---

### Step 105: State Persistence

State file location: `~/.continue/bridge-state.json`

**Verification Steps**:

- [ ] **Checkpoint file created on shutdown**
  ```bash
  # Start bridge, then stop with Ctrl+C
  # Check if file was created:
  ls -la ~/.continue/bridge-state.json
  # Expected: File created during shutdown
  # If missing: Step 105 not writing state
  ```

- [ ] **Recent timestamp**
  ```bash
  stat ~/.continue/bridge-state.json
  # Expected: Modify time within last hour
  # If old: Bridge hasn't written state recently; check for crashes
  ```

- [ ] **File validity**
  ```bash
  jq . ~/.continue/bridge-state.json
  # Expected: Valid JSON (can be large)
  # If parse error: File corrupted; needs reset
  ```

- [ ] **Recovery works**
  ```bash
  # Test recovery:
  rm ~/.continue/bridge-state.json  # Delete state
  npm start --prefix src/versions/v2.0.0  # Restart
  # Expected: Bridge starts normally; creates new state file on shutdown
  ```

**Remediation**:
```bash
# If state corrupted:
rm ~/.continue/bridge-state.json
# Restart bridge; it will auto-recover with fresh state
npm start --prefix src/versions/v2.0.0
```

---

### Step 103: Crash Recovery

Crash diagnostics directory: `~/.continue/crash-diagnostics/`

**Verification Steps**:

- [ ] **Diagnostics directory exists**
  ```bash
  ls -la ~/.continue/crash-diagnostics/
  # Expected: Directory with JSON files (if any crashes occurred)
  # If missing and bridge crashed: Step 103 not saving diagnostics
  ```

- [ ] **Recent crash reports (if applicable)**
  ```bash
  ls -la ~/.continue/crash-diagnostics/
  # Expected: Files sorted by time; newest first
  # Each file: crash-TIMESTAMP.json
  ```

- [ ] **Log parsing (if crashes present)**
  ```bash
  tail ~/.continue/crash-diagnostics/*.json | jq '.error'
  # Expected: JSON with error stack trace
  # Helps identify which handler crashed
  ```

- [ ] **Recovery state file**
  ```bash
  cat ~/.continue/crash-recovery.json
  # Expected: JSON with crash count and backoff state
  # If file contains high crash count: Bridge in recovery loop
  ```

**Remediation** (if in crash loop):
```bash
# Reset recovery counter:
rm ~/.continue/crash-recovery.json
# Restart bridge:
npm start --prefix src/versions/v2.0.0
# If still crashes: Check crash logs in crash-diagnostics/
```

---

### Step 71: Handler Registration

Handler registry file: `src/versions/v2.0.0/lib/handler-registry.mjs`

**Verification Steps**:

- [ ] **Registry loads successfully**
  ```bash
  # Check core-server.js logs for registration messages
  # Expected: "Handler 'refactor' registered" ×20
  # If errors: Check handler-registry.mjs for syntax errors
  ```

- [ ] **All 20 handlers present**
  ```bash
  curl http://localhost:3000/bridge/handlers | jq '.[] | .name'
  # Expected: 20 handler names (refactor, debug, git, etc.)
  # If <20: Some handlers failed to register
  ```

- [ ] **Per-handler entry verification**
  ```bash
  curl http://localhost:3000/bridge/handlers | jq '.[] | select(.name=="refactor")'
  # Expected: Handler entry with: name, timeout, stability
  # If null: Handler "refactor" not registered
  ```

- [ ] **Handler enabled status**
  ```bash
  curl http://localhost:3000/bridge/handlers | jq '.[] | {name, enabled}'
  # Expected: All .enabled=true (unless degraded mode active)
  # If false: Handler disabled by crash recovery (Step 103)
  ```

**Remediation**:
```bash
# If handler not registered:
# 1. Check handler-registry.mjs (Step 71) for syntax error
# 2. Verify handler file exists: src/versions/v2.0.0/lib/handlers/<name>-handler.mjs
# 3. Restart bridge after fix
npm start --prefix src/versions/v2.0.0
```

---

### Step 72–74: Middleware (Logging, Validation, Error Recovery)

**Verification Steps**:

- [ ] **Validation middleware runs**
  ```bash
  # Send invalid message (missing messageType)
  curl -X POST http://localhost:3000/bridge \
    -H "Content-Type: application/json" \
    -d '{"messageId":"1"}'
  # Expected: HTTP 200 with error response: {"error": ..., "code": -32602}
  # If request accepted: Validation not running (Step 73 issue)
  ```

- [ ] **Logging enabled**
  ```bash
  # Check core-server.js console output for "Received message" logs
  # Expected: Message received/response sent logs
  # If no logs: Logging middleware disabled (Step 72 issue)
  ```

- [ ] **Error recovery active**
  ```bash
  # Send message to non-existent handler
  curl -X POST http://localhost:3000/bridge \
    -H "Content-Type: application/json" \
    -d '{"messageId":"1","messageType":"bridge:nonexistent","data":{}}'
  # Expected: Graceful error response (-32601)
  # If bridge crashes: Error recovery not working (Step 74 issue)
  ```

- [ ] **Cascading failure prevented**
  ```bash
  # Send multiple requests while one handler errors
  # Expected: Other handlers continue responding
  # If cascade: Step 74 error recovery isolation failing
  ```

**Remediation**:
```bash
# If middleware not working:
# 1. Check core-server.js middleware registration
# 2. Verify Steps 72–74 files exist and have no syntax errors
# 3. Restart bridge
npm start --prefix src/versions/v2.0.0
```

---

### Step 97: Compliance Testing

Compliance test file: `src/versions/v2.0.0/tests/handler-compliance.test.mjs`

**Verification Steps**:

- [ ] **Test framework available**
  ```bash
  npx mocha src/versions/v2.0.0/tests/handler-compliance.test.mjs
  # Expected: Tests run; output shows pass/fail
  # If "file not found": Test file missing (Step 97 issue)
  ```

- [ ] **All handlers pass compliance**
  ```bash
  npx mocha src/versions/v2.0.0/tests/handler-compliance.test.mjs
  # Expected: "20 passing" (all handlers)
  # If failures: Specific handlers violate compliance rules
  # Action: Review failed handler; check Step 71 registration
  ```

- [ ] **Compliance fixtures available**
  ```bash
  ls -la src/versions/v2.0.0/tests/fixtures/handler-compliance-fixtures.mjs
  # Expected: File exists with test data
  # If missing: Fixtures not available; Step 97 incomplete
  ```

**Remediation** (if compliance fails):
```bash
# Review failing handler:
# 1. Check handler response schema (should have success/error fields)
# 2. Verify handler registered in Step 71 registry
# 3. Ensure handler implements required interface
# 4. Run compliance test again:
npx mocha src/versions/v2.0.0/tests/handler-compliance.test.mjs
```

---

### Step 112: Regression Baseline

Regression test file: `src/versions/v2.0.0/tests/handler-regression.test.mjs`

**Verification Steps**:

- [ ] **Baseline cached**
  ```bash
  ls -la ~/.continue/regression-baselines/
  # Expected: Directory with baseline JSON files
  # If missing: No baselines cached; Step 112 not generating baselines
  ```

- [ ] **Current metrics can be generated**
  ```bash
  npm test -- --grep "regression" --prefix src/versions/v2.0.0
  # Expected: Test runs; generates current metrics
  # If error: Regression tests not configured
  ```

- [ ] **Gate decision available**
  ```bash
  npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs
  # Expected: Test output shows gate decision (PASS/FAIL)
  # Example: "✓ Latency regression gate: PASS"
  # If FAIL: Performance regression detected; needs investigation
  ```

**Interpretation**:
```
GATE PASS: All metrics within regression threshold
GATE FAIL: One or more metrics exceed threshold (see TROUBLESHOOTING-GUIDE.md)
GATE WARN: Metrics approaching threshold; monitor
```

**Remediation** (if regression detected):
```bash
# 1. Review specific metric that regressed (latency, memory, throughput, error rate)
# 2. Consult PERFORMANCE-TUNING-GUIDE.md for tuning procedures
# 3. Profile with Step 96 profiler to identify bottleneck
# 4. Apply recommended fix
# 5. Re-run regression test to confirm improvement
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs
```

---

## Troubleshooting Workflow

A structured 6-step workflow to diagnose and resolve issues.

### Step 1: Identify Symptom

Determine what is not working:

**User Reports**:
- "Handler doesn't respond" → **Handler Failures** section (TROUBLESHOOTING-GUIDE.md)
- "Bridge is slow" → **Performance Degradation** section
- "Bridge keeps crashing" → **Crash Recovery** section
- "Settings not saving" → **Configuration Issues** section
- "Bridge won't start" → **Integration Problems** section

**Monitoring Alerts**:
- Performance regression (Step 112) → Performance Degradation
- Error rate spike → Handler Failures
- Memory leak → Performance Degradation
- Crash loop → Crash Recovery

### Step 2: Use Decision Tree

Navigate decision tree in TROUBLESHOOTING-GUIDE.md:

```
Is handler registered?
  → Yes: Does message validate?
     → Yes: Does timeout enforce?
        → Yes: Are metrics recorded?
           → Yes: Handler healthy
           → No: Check crash diagnostics
        → No: Increase timeout (Step 64)
     → No: Fix message schema (Step 73)
  → No: Re-register handler (Step 71)
```

**Output**: Root cause identified

### Step 3: Reference Error Catalog

Look up error code/message in HANDLER-ERROR-CATALOG.mjs:

```bash
# Example: Error code -32603
grep -r "32603" src/versions/v2.0.0/tests/mocks/handler-error-catalog.mjs

# Output shows:
# - Root cause: Handler execution timeout
# - Related steps: [64, 98]
# - Remediation: Increase timeout, profile with Step 96
```

**Output**: Recommended remediation steps

### Step 4: Verify Components

Check relevant component status using diagnostics from this checklist:

```
Error: Handler timeout
→ Check Step 64: Timeout policy appropriate?
→ Check Step 96: Profile handler latency
→ Check Step 112: Baseline exceeded?
```

**Output**: Components verified or issues identified

### Step 5: Apply Remediation

Execute recommended fix (from error catalog or tuning guide):

```bash
# Example: Increase handler timeout
# Step 71: handler-registry.mjs
{
  name: "refactor",
  timeoutPolicy: "slow"  # Increased from "medium"
}
# Restart bridge
npm start --prefix src/versions/v2.0.0
```

**Output**: Fix applied; bridge restarted if needed

### Step 6: Validate Fix

Confirm issue resolved:

```bash
# Re-run Quick Health Check:
curl http://localhost:3000/bridge/handlers | jq 'length'  # Should be 20
curl http://localhost:3000/bridge/profiler | jq '.handlers[] | {name, p99}' # Should show reasonable latency
curl http://localhost:3000/bridge \
  -d '{"messageId":"1","messageType":"bridge:refactor","data":{}}'  # Handler should respond
```

**Output**: Issue confirmed resolved OR escalate if persists

---

## Sign-Off Matrix (for QA/Ops)

Authorization matrix for issue sign-off and release gates.

| Issue Type | Dev Verification | QA Sign-Off | Ops Approval | Release Gate | Notes |
|---|---|---|---|---|---|
| Compliance violation | ✓ run Step 97 | ✓ verify all 20/20 pass | - | MUST PASS | Release blocked if compliance fails |
| Performance regression | ✓ profile with Step 96 | ✓ run Step 98–99 tests | ✓ compare to Step 112 baseline | MUST PASS | Release blocked if CRITICAL threshold exceeded |
| Crash recovery | ✓ fix handler code | ✓ run Step 99 crash gate | ✓ stress test in staging | MUST PASS | Release blocked if >5 consecutive crashes in test |
| Config/state corruption | ✓ implement migration | ✓ manual test recovery | ✓ backup/rollback plan | CONDITIONAL | QA must verify recovery process works |
| Handler timeout | ✓ adjust Step 64/71 | ✓ run Step 96 profiler | ✓ verify against baseline | SHOULD PASS | Can release if within acceptable margin |
| Memory leak | ✓ fix handler code | ✓ run Step 99 sustained | ✓ monitor production | MUST PASS | Release blocked if peak >50MB |
| Cascading failure | ✓ fix handler isolation | ✓ run Step 74 tests | ✓ verify isolation gate | MUST PASS | Release blocked if isolation <80% |

**Release Gate Decision**:
```
IF (compliance = PASS) AND
   (regression gate = PASS or WARN) AND
   (crash recovery = PASS) AND
   (memory peak <50MB)
THEN
   Release approved
ELSE
   Release blocked; escalate issues
```

---

## Escalation Path

Escalation levels for unresolved issues.

### Level 1: Self-Service (User)

**Symptoms the user can resolve**:
- Bridge won't start → Re-download npm packages (Step 35)
- Settings not applied → Create config file (Step 95)
- Handler timeout → User reduces concurrent requests

**Resources**: TROUBLESHOOTING-GUIDE.md decision trees

---

### Level 2: Support Team

**Symptoms support can resolve**:
- Memory leak (sustained load spike)
- Handler performance regression (compare to baseline)
- Crash recovery loop (provide diagnostics bundle)

**Resources**:
- HANDLER-ERROR-CATALOG.mjs lookup
- PERFORMANCE-TUNING-GUIDE.md for optimization
- Quick Health Check from this checklist

**Action**: Gather diagnostics; apply tuning guide; escalate if persists

---

### Level 3: Engineering

**Symptoms requiring engineering investigation**:
- Regression detected in Step 112 (compare baseline)
- Cascading handler failures (isolation gate failing)
- Intermittent crashes without clear root cause
- Memory leak not resolved by tuning

**Resources**:
- Run Step 97–99 tests (compliance, performance, stress)
- Review profiler data (Step 96) for bottlenecks
- Analyze crash diagnostics (~/.continue/crash-diagnostics/)

**Action**: Run diagnostic suite; profile with Node.js --inspect; code review

---

### Level 4: Escalation (Deep-Dive)

**Symptoms requiring architectural analysis**:
- Systematic memory leak in Node.js core
- Transport layer instability (Step 19–21)
- Cascading failures across multiple handlers
- Performance regression without clear cause

**Resources**:
- Access to source code and git history
- Performance benchmarking infrastructure
- Cross-team collaboration (IDE + bridge)

**Action**: Architecture review; performance deep-dive; potential code refactoring

---

## Diagnostic Data Collection

When escalating issues, collect this diagnostic bundle:

**Essential Files**:
1. **Bridge logs** (~2 KB)
   ```bash
   # Capture console output during issue
   npm start --prefix src/versions/v2.0.0 2>&1 | tee bridge.log
   ```

2. **Crash diagnostics** (~10–50 KB)
   ```bash
   # Copy all crash reports
   cp -r ~/.continue/crash-diagnostics/ ./diagnostics/
   ```

3. **State files** (~50 KB)
   ```bash
   # Snapshot current state
   cp ~/.continue/bridge-state.json ./bridge-state.json.bak
   cp ~/.continue/crash-recovery.json ./crash-recovery.json.bak
   ```

4. **Performance metrics** (~1 KB)
   ```bash
   # Capture profiler output
   curl http://localhost:3000/bridge/profiler > profiler.json
   ```

5. **Handler registry** (~1 KB)
   ```bash
   # Capture registered handlers
   curl http://localhost:3000/bridge/handlers > handlers.json
   ```

6. **Test results** (~5–10 KB)
   ```bash
   # Run compliance/performance/stress tests
   npm test -- --grep "compliance|performance|stress" --prefix src/versions/v2.0.0 > test-results.log
   ```

7. **System info** (~0.5 KB)
   ```bash
   # Environment details
   echo "OS: $(uname -a)" > system-info.txt
   echo "Node: $(node --version)" >> system-info.txt
   echo "npm: $(npm --version)" >> system-info.txt
   echo "IDE: Visual Studio $(code --version 2>/dev/null || echo 'N/A')" >> system-info.txt
   ```

8. **Reproduction steps** (~2 KB)
   ```
   Document exact user actions that trigger issue
   ```

**Diagnostic Bundle Structure**:
```
issue-YYYYMMDD-HHMMSS/
  bridge.log
  profiler.json
  handlers.json
  test-results.log
  system-info.txt
  bridge-state.json.bak
  crash-recovery.json.bak
  crash-diagnostics/
    crash-*.json
  REPRODUCTION.md
```

**Upload Bundle**:
- Attach to GitHub issue or support ticket
- Include escalation level (1–4) from Escalation Path
- Include REPRODUCTION.md with exact steps to reproduce

---

## Release Readiness Checklist (Final Gate)

Use this checklist before shipping a new version.

### Code Quality Gates

- [ ] All tests pass
  ```bash
  npm test --prefix src/versions/v2.0.0
  # Expected: All tests pass
  ```

- [ ] No linting errors
  ```bash
  npx eslint src/versions/v2.0.0/lib --fix
  # Expected: 0 errors (auto-fix or manual correction)
  ```

- [ ] No console errors at startup
  ```bash
  npm start --prefix src/versions/v2.0.0 2>&1 | grep -i "error"
  # Expected: No ERROR level messages
  ```

### Performance Gates

- [ ] Compliance test: 20/20 handlers pass (Step 97)
  ```bash
  npx mocha src/versions/v2.0.0/tests/handler-compliance.test.mjs
  # Expected: "20 passing"
  ```

- [ ] Performance test: Throughput >300 msg/sec (Step 98)
  ```bash
  npm test -- --grep "throughput" --prefix src/versions/v2.0.0
  # Expected: Throughput >300 msg/sec
  ```

- [ ] Stress test: Concurrent load p99 <500ms @100 parallel (Step 99)
  ```bash
  npm test -- --grep "concurrent-load" --prefix src/versions/v2.0.0
  # Expected: p99 <500ms
  ```

- [ ] Regression gate: All metrics within threshold (Step 112)
  ```bash
  npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs
  # Expected: All gates PASS
  ```

### Infrastructure Gates

- [ ] npm packages verified (Step 35–37)
  ```bash
  npm run verify-packages --prefix src/versions/v2.0.0
  # Expected: "All checksums verified"
  ```

- [ ] Bridge starts cleanly
  ```bash
  npm start --prefix src/versions/v2.0.0
  # Expected: "Bridge listening on :3000"
  ```

- [ ] All 20 handlers registered
  ```bash
  curl http://localhost:3000/bridge/handlers | jq 'length'
  # Expected: 20
  ```

### Documentation Gates

- [ ] TROUBLESHOOTING-GUIDE.md complete
  ```bash
  wc -l docs/TROUBLESHOOTING-GUIDE.md
  # Expected: >700 lines
  ```

- [ ] PERFORMANCE-TUNING-GUIDE.md complete
  ```bash
  wc -l docs/PERFORMANCE-TUNING-GUIDE.md
  # Expected: >350 lines
  ```

- [ ] Release notes updated
  ```bash
  cat docs/RELEASE-NOTES-v2.x.x.md | grep -q "Step 114"
  # Expected: Step 114 (troubleshooting) mentioned
  ```

### QA Sign-Off

- [ ] QA has reviewed TROUBLESHOOTING-INTEGRATION-CHECKLIST.md
- [ ] QA has verified all Quick Health Checks pass
- [ ] QA has reviewed component diagnostics (Step 104–112)
- [ ] QA has run troubleshooting workflow (Steps 1–6)
- [ ] QA has collected and reviewed diagnostic data for any issues
- [ ] QA has verified sign-off matrix (all gates cleared)
- [ ] QA signature: _________________ Date: _________________

### Ops Sign-Off

- [ ] Ops has reviewed runbooks (TROUBLESHOOTING-GUIDE.md, etc.)
- [ ] Ops has practiced escalation path (Levels 1–4)
- [ ] Ops has prepared monitoring/alerting for regression gates
- [ ] Ops has prepared rollback plan if issues arise
- [ ] Ops signature: _________________ Date: _________________

### Release Approval

**All Gates Clear**: ✓ APPROVED FOR RELEASE

**Issues Requiring Fix**:
- [ ] Compliance failures (must fix)
- [ ] Performance regression >HIGH threshold (must fix)
- [ ] Crash recovery failures (must fix)

**Hold for Investigation**:
- [ ] Performance regression >MEDIUM threshold (investigate before release)
- [ ] Memory approaching 50MB limit (monitor)

**Release Decision**:
```
Gate Status: [ ] PASS  [ ] FAIL  [ ] PASS WITH CONDITIONS

If FAIL: Do not release; fix issues and re-run gates
If PASS WITH CONDITIONS: Release with monitoring plan and rollback ready
If PASS: Release approved
```

**Signature**: ___________________ Date: ___________________

---

## Quick Reference

### Common Commands

```bash
# Start bridge
npm start --prefix src/versions/v2.0.0

# Run all tests
npm test --prefix src/versions/v2.0.0

# Run specific test suite
npm test -- --grep "compliance|performance|stress" --prefix src/versions/v2.0.0

# Get profiler metrics
curl http://localhost:3000/bridge/profiler

# Get handler registry
curl http://localhost:3000/bridge/handlers

# Test handler invocation
curl -X POST http://localhost:3000/bridge \
  -H "Content-Type: application/json" \
  -d '{"messageId":"1","messageType":"bridge:refactor","data":{}}'
```

### File Locations

| Component | Location |
|-----------|----------|
| Bridge entry | `src/versions/v2.0.0/core-server.js` |
| Handler registry | `src/versions/v2.0.0/lib/handler-registry.mjs` |
| Handlers | `src/versions/v2.0.0/lib/handlers/` |
| Config | `~/.continue/config.json` |
| State | `~/.continue/bridge-state.json` |
| Crash diagnostics | `~/.continue/crash-diagnostics/` |
| Tests | `src/versions/v2.0.0/tests/` |
| Baselines | `~/.continue/regression-baselines/` |

### Troubleshooting Resources

| Resource | Purpose | Location |
|----------|---------|----------|
| Symptom guide | Symptom → root cause → remediation | `docs/TROUBLESHOOTING-GUIDE.md` |
| Error catalog | Error code → details, related steps | `src/versions/v2.0.0/tests/mocks/handler-error-catalog.mjs` |
| Performance guide | Latency, memory, throughput tuning | `docs/PERFORMANCE-TUNING-GUIDE.md` |
| Regression guide | Performance baseline interpretation | `docs/HANDLER-REGRESSION-GUIDE.md` |
| Manual testing | Handler workflow validation | `docs/MANUAL-TESTING-GUIDE.md` |

