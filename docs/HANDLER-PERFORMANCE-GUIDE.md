# Handler Performance Testing Guide (Step 98)

## 1. Performance Contract Definition

### Performance Tier SLAs

| Tier | Handlers | p50 | p95 | p99 | Timeout | Memory |
|------|----------|-----|-----|-----|---------|--------|
| **Fast** | search, code-lens, model-info, profiler, go-to-def | <5ms | <8ms | <10ms | 2000ms | <10MB |
| **Medium** | refactor, completion, hover, apply-edit, format, git, terminal, settings, snippet, workspace-reload | <15ms | <40ms | <50ms | 10000ms | <25MB |
| **Slow** | diff-viewer, test-explorer, debug-session, streaming, refactor-tests, project-info, sidebar, context-window, inline-msg, find-ref | <50ms | <200ms | <500ms | 30000ms | <50MB |

### Performance Rationale

- **Fast tier**: Single-request handlers with minimal I/O (symbol lookups, cache queries)
- **Medium tier**: Handler with light processing or external I/O (refactoring, formatting)
- **Slow tier**: Complex analysis or heavy I/O (diff generation, test discovery)
- **Timeout policies**: Aligned with visual feedback expectations (2s = instant, 10s = user-tolerable, 30s = background)
- **Memory SLAs**: Per-tier allocations account for fixture payload sizes and internal state

---

## 2. Test Environment Specifications

### Minimum System Requirements

- **CPU**: Intel i7-10700K or equivalent (8+ cores, consistent turbo boost)
- **RAM**: Minimum 16GB, no memory pressure (<80% utilization during tests)
- **Disk**: SSD with >10GB free space (HDD will fail streaming tests)
- **Network**: Stable connection, <50ms latency (for git/terminal tests)
- **OS**: Windows 10/11 build 21H2+ or macOS 12+
- **Node.js**: v18.x LTS with --expose-gc flag for memory tests

### Pre-Test Environment Validation

```bash
# Run environment check before establishing baseline
npm run test:performance -- --check-env

# Expected output:
# ✅ Node.js Version: v18.13.0
# ✅ CPU Cores: 8 cores
# ✅ Memory Pressure: 45.2% used (PASS)
# ✅ Disk Space: >10GB available
# ✅ Background Processes: <5
```

### Critical Checks Before Baseline

- [ ] No other CPU-intensive processes running
- [ ] System memory utilization <80%
- [ ] CPU frequency: nominal (no throttling)
- [ ] Disk I/O not saturated
- [ ] Network latency stable (<50ms)

---

## 3. Warm-Up Handling & JIT Effects

### Why Warm-Up Is Critical

Node.js/V8 exhibits three optimization phases:
1. **Interpretation** (first run): 100-200% slower than optimized
2. **Profiling** (runs 2-100): Collecting data for JIT compilation
3. **Optimization** (after run 100): Full JIT compilation applied

### Warm-Up Configuration

```javascript
// Default: 50 warm-up runs, then measure 1000 runs
await measureLatencyWithWarmup(handlerFn, payload, {
  runs: 1000,      // Actual measurement runs
  warmupRuns: 50,  // Discarded runs for JIT
  label: 'handler'
});
```

### Warm-Up Statistics

- **First 50 runs**: Discarded (JIT not complete)
- **Runs 51-1050**: Collected and analyzed
- **Variance check**: Coefficient of variation should be <20% after warm-up
- **Outlier handling**: Bottom/top 0.1% filtered to remove GC pauses

---

## 4. Baseline Persistence & Versioning

### Baseline Storage

**Location**: `~/.continue/baselines/`

**Filename Pattern**: `baseline-v{VERSION}-{DATE}T{TIME}.json`

**Example**: `baseline-v2.0.0-2024-01-15T10-30-00.json`

### Baseline File Structure

```json
{
  "version": "2.0.0",
  "schema": "1.0",
  "timestamp": 1705316400000,
  "environment": {
    "nodeVersion": "v18.13.0",
    "osVersion": "Windows 11 build 22621",
    "cpuModel": "Intel Core i7-10700K",
    "cpuCount": 8,
    "totalMemoryMB": 32768,
    "freeMemoryMB": 18432,
    "diskType": "SSD",
    "networkLatencyMs": 15
  },
  "systemChecks": {
    "cpuThrottlingDetected": false,
    "memoryPressureDetected": false
  },
  "handlers": {
    "search": {
      "tier": "fast",
      "latency": { "p50": 2.1, "p95": 4.3, "p99": 5.8, "mean": 2.3, "stdDev": 0.5 },
      "throughput": { "messagesPerSecond": 1200, "totalMessages": 1000 },
      "memory": { "deltaMB": 8.2, "leakDetected": false }
    }
    // ... 19 more handlers
  },
  "checksum": "abc123..."
}
```

### Baseline Lifecycle

1. **Save**: `npm run test:performance -- --save-baseline`
   - Generates filename with timestamp
   - Computes SHA256 checksum for integrity
   - Stores in ~/.continue/baselines/

2. **Load**: Auto-loads latest matching version for comparison

3. **Prune**: Keeps last 5 baselines per version, deletes older

4. **Validation**: Checksum verified on load

---

## 5. Regression Detection

### Regression Severity Levels

| Severity | p99 Regression | Throughput Drop | Memory Leak | Action |
|----------|----------------|-----------------|-------------|--------|
| **CRITICAL** | >50% | >30% | >20MB | Escalate immediately |
| **HIGH** | 25-50% | 20-30% | 10-20MB | Optimize before merge |
| **MEDIUM** | 10-25% | 10-20% | 5-10MB | Monitor, investigate |
| **LOW** | <10% | <10% | <5MB | Acceptable variance |

### Regression Workflow

```
1. Regression detected (e.g., p99 increased 30%)
   ↓
2. Measure with Step 96 profiler for root cause
   ↓
3. Identify bottleneck (CPU? I/O? Memory?)
   ↓
4. Optimize handler implementation
   ↓
5. Re-run performance tests (--compare-baseline)
   ↓
6. Verify improvement meets SLA
   ↓
7. Save new baseline if regression resolved
```

### Remediation Paths by Handler Type

**Latency Regression**:
- Measure with profiler to identify hot code paths
- Check for unbounded loops or recursive calls
- Optimize symbol lookups (add caching)
- Consider async boundaries for I/O

**Throughput Regression**:
- Check for concurrency limits (worker thread pool size)
- Verify no sequential batching where parallel possible
- Profile message queue overhead

**Memory Regression**:
- Enable memory snapshots before/after handler
- Check for circular references or unreleased event listeners
- Verify fixture cleanup between iterations
- Run with --expose-gc and force GC between runs

---

## 6. Running Performance Tests

### Basic Run (Complete Baseline)

```bash
# Run all tests, measure all 20 handlers, save baseline
npm run test:performance

# Output:
# [Suite 0] Handler Initialization & Teardown ... 4/4 ✅
# [Suite 1] Baseline Latency Measurement ... 15/15 ✅
# [Suite 2] Payload Scaling Analysis ... 6/6 ✅
# [Suite 3] Throughput ... 6/6 ✅
# [Suite 4] Memory Safety ... 6/6 ✅
# [Suite 5] Error Paths ... 4/4 ✅
# [Suite 6] Timeout Policy ... 4/4 ✅
# [Suite 7] C# Integration ... 3/3 ✅
# [Suite 8] Bidirectional ... 3/3 ✅
# [Suite 9] Comparative Analysis ... 5/5 ✅
#
# Results saved to: performance-reports/performance-report-2024-01-15T10-30-00.json
# Baseline saved to: ~/.continue/baselines/baseline-v2.0.0-2024-01-15T10-30-00.json
# Duration: 8m 42s
```

### Advanced Options

```bash
# Test fast tier only (1 minute)
npm run test:performance -- --filter=fast

# Test specific handlers
npm run test:performance -- --handlers=completion,refactor,git

# Compare to saved baseline
npm run test:performance -- --compare-baseline v2.0.0

# CI/CD mode: exit code reflects gate status
npm run test:performance -- --cicd

# Export reports in multiple formats
npm run test:performance -- --export-json report.json
npm run test:performance -- --export-markdown report.md
npm run test:performance -- --export-csv report.csv

# Check environment before baseline
npm run test:performance -- --check-env
```

---

## 7. CI/CD Integration

### GitHub Actions Example

```yaml
name: Performance Baseline

on:
  pull_request:
    branches: [main]

jobs:
  performance:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        node-version: [18.x]

    steps:
      - uses: actions/checkout@v3

      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: ${{ matrix.node-version }}

      - name: Install dependencies
        run: npm ci

      - name: Check environment
        run: npm run test:performance -- --check-env

      - name: Run performance tests
        run: npm run test:performance -- --cicd
        env:
          NODE_OPTIONS: --expose-gc

      - name: Load baseline (if exists)
        run: npm run test:performance -- --compare-baseline v2.0.0
        continue-on-error: true

      - name: Upload reports
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: performance-reports
          path: performance-reports/

      - name: Comment PR with results
        if: always()
        uses: actions/github-script@v6
        with:
          script: |
            const fs = require('fs');
            const report = JSON.parse(
              fs.readFileSync('performance-reports/performance-report.json', 'utf-8')
            );
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: `## Performance Test Results\n${report.summary}`
            });
```

### Gate Criteria (Part III Gate at Step 115)

**Performance tests pass if ALL of**:
- ✅ All handlers pass SLA (p99 within gate)
- ✅ No regressions >10% vs. baseline
- ✅ Throughput ≥100 msgs/sec per handler
- ✅ Memory within tier SLA
- ✅ All test suites passing (100+ tests)

**Exit Codes**:
- `0` = All handlers pass SLA, ready for merge
- `1` = SLA violations, requires optimization before merge

---

## 8. Interpreting Results

### Latency Percentile Meanings

| Percentile | Meaning | User Impact |
|-----------|---------|-------------|
| p50 | Median latency (typical user experience) | Normal case |
| p95 | 95th percentile (worst 5% of users) | Noticeable slowdown |
| p99 | 99th percentile (worst 1% of users) | Significant delay |

**Interpretation Example**:
```
completion handler:
  p50: 18.5ms (typical: user types, sees suggestion in 18ms)
  p95: 38.2ms (5% of time takes 38ms, visible lag)
  p99: 48.9ms (1% of time takes 49ms, user notices)
  Gate: p99Max=50ms → PASS (48.9 < 50)
```

### Variance Analysis

```
Coefficient of Variation (CV) = StdDev / Mean

If CV < 0.20 (20%): ✅ Stable, reproducible
If CV 0.20-0.35: ⚠️ Moderate variance, acceptable
If CV > 0.35: ❌ High variance, investigate system load
```

### Memory Leak Indicators

**Healthy Pattern** (no leak):
```
Iteration 1: ΔMB = 8.2MB
Iteration 2: ΔMB = 8.1MB (GC effective)
Iteration 3: ΔMB = 8.3MB
Average:     ΔMB = 8.2MB ✅ Stable
```

**Leak Pattern**:
```
Iteration 1: ΔMB = 8.2MB
Iteration 2: ΔMB = 18.5MB (growing!)
Iteration 3: ΔMB = 28.7MB (accumulating!)
Trend:       +20.5MB per iteration ❌ Leak detected
```

---

## 9. Performance Troubleshooting

### Common Issues & Fixes

| Symptom | Diagnosis | Fix |
|---------|-----------|-----|
| p99 spike on first run | Warm-up insufficient | Increase warmupRuns to 100 |
| Variance >20% | System load too high | Close apps, retry during idle |
| Memory growing | Possible leak | Enable profiler, add snapshots |
| Timeout violations | CPU throttling | Disable turbo boost, run again |
| Network latency high | Wifi interference | Use Ethernet for consistent results |

### Debugging Commands

```bash
# Run with verbose logging
npm run test:performance -- --verbose

# Measure specific handler
npm run test:performance -- --handlers=completion --runs=1000

# Memory profiling
node --expose-gc --inspect src/versions/v2.0.0/tests/handler-performance.test.mjs

# CPU profiling (Chrome DevTools)
node --inspect-brk src/versions/v2.0.0/tests/handler-performance.test.mjs
# Then open chrome://inspect
```

### Performance Troubleshooting Flowchart

```
Performance issue detected
    ↓
Run with profiler (Step 96)
    ↓
Identify hot code path
    ├→ CPU-bound? Optimize algorithm, reduce complexity
    ├→ I/O-bound? Add caching, parallelize, async boundaries
    ├→ Memory? Check fixtures, verify cleanup, add GC
    └→ Contention? Reduce lock time, increase parallelism
    ↓
Re-run performance tests
    ↓
Improvement? ✅ YES → Commit
         ↓ NO
    Update baseline expectations or escalate
```

---

## 10. Integration with Related Steps

### Step 96: Profiler Validation
- Measured p99 should match profiler output within ±5%
- If mismatch >5%: Investigate profiler accuracy or handler variance

### Step 97: Compliance Tests
- Use same fixtures from compliance test suite
- Add SMALL/MEDIUM/LARGE/BATCH payload variants
- Compliance tests validate correctness; performance tests validate speed

### Step 99: Stress Tests
- Uses this baseline as reference
- Adds concurrent load (50 concurrent handlers)
- Expects degradation <20% vs. baseline
- Validates circuit-breaker behavior (Step 108)

### Step 115: Part III Gate
- All handlers must pass performance SLAs
- Regression detection triggers optimization review
- CI/CD integration ensures gate is enforced

---

## 11. Quick Reference

**Environment**: Node.js 18.x, --expose-gc, Windows/Mac, SSD, 16GB+ RAM

**Baseline**: ~/.continue/baselines/baseline-v2.0.0-{DATE}.json

**Test Command**: `npm run test:performance`

**CI/CD Mode**: `npm run test:performance -- --cicd`

**Regression Check**: `npm run test:performance -- --compare-baseline v2.0.0`

**Expected Duration**: 5-10 minutes (1000 runs × 20 handlers)

**Success Criteria**: All 20 handlers pass SLAs, exit code 0, reports generated

---

**Version**: Step 98 v1.0  
**Last Updated**: 2024-01-15  
**Related Steps**: 96, 97, 99, 113, 115
