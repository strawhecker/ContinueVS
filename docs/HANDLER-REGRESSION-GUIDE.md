# Handler Regression Test Suite Guide

**Step 112: Regression Detection & Release Gating**

This guide explains how regression testing protects code quality before production release.

---

## Table of Contents

1. [Overview](#overview)
2. [Regression Model](#regression-model)
3. [Baseline Management](#baseline-management)
4. [Execution Instructions](#execution-instructions)
5. [Report Interpretation](#report-interpretation)
6. [CI/CD Integration](#cicd-integration)
7. [Troubleshooting](#troubleshooting)
8. [Related Documentation](#related-documentation)

---

## Overview

### Purpose

The regression test suite **detects performance degradation** by comparing current handler metrics against saved baselines. It blocks release if critical regressions are detected.

### Scope

- **20+ handlers** monitored for latency, throughput, memory, and error rates
- **3 handler tiers** (fast, medium, slow) with independent gates
- **4 regression classes** with automatic severity classification
- **Automated release gating** (PASS/BLOCKED decision)

### Success Criteria

✅ **All tiers pass** (fast, medium, slow)  
✅ **Zero critical regressions** (p99 >50% degradation)  
✅ **High regressions** require investigation & remediation  
✅ **Release approved** only when gates pass  

---

## Regression Model

### 4 Regression Classes

#### 1. Latency Regression (p50, p95, p99)

**Detection**: Handler response time increases beyond threshold.

| Metric | Critical | High | Medium | Low |
|--------|----------|------|--------|-----|
| p99 increase | >50% | >25% | >15% | >10% |
| p95 increase | >40% | >20% | >12% | >8% |
| p50 increase | >30% | >15% | >10% | >5% |

**Example**: `code-completion` p99 goes from 120ms → 200ms (+67%) = **CRITICAL**

#### 2. Throughput Degradation (messages/sec)

**Detection**: Handler processes fewer messages per second.

| Severity | Throughput Drop |
|----------|-----------------|
| CRITICAL | >40% |
| HIGH | >20% |
| MEDIUM | >10% |
| LOW | >5% |

**Example**: `search` throughput drops from 400 msg/s → 280 msg/s (-30%) = **HIGH**

#### 3. Memory Leak (heap + non-heap)

**Detection**: Process heap usage grows beyond safe threshold.

| Category | Critical | High | Medium |
|----------|----------|------|--------|
| Heap Delta | >50MB | >20MB | >10MB |
| Non-Heap Delta | >20MB | >10MB | >5MB |

**Example**: `refactor` heap grows by 25MB = **HIGH**

#### 4. Error Rate Spike

**Detection**: Handler error rate increases beyond tolerance.

| Severity | Error Rate Increase |
|----------|-------------------|
| CRITICAL | >10% absolute |
| HIGH | >5% absolute |
| MEDIUM | >2% absolute |
| LOW | >1% absolute |

**Example**: `git-integration` error rate increases from 1.5% → 7.5% (+6%) = **HIGH**

---

### Handler Tier Classification

#### Fast Tier (2-second SLA)

- `code-completion` (p99: <150ms)
- `search` (p99: <200ms)
- `go-to-definition` (p99: <200ms)

**Gate Rule**: All fast handlers must pass latency gate; no MEDIUM+ regressions.

#### Medium Tier (10-second SLA)

- `refactor` (p99: <1,500ms)
- `apply-edit` (p99: <1,500ms)
- `format-document` (p99: <1,500ms)

**Gate Rule**: All medium handlers must pass; no MEDIUM+ regressions.

#### Slow Tier (30-second SLA)

- `git-integration` (p99: <6,000ms)
- `terminal` (p99: <5,000ms)
- `file-system` (p99: <4,000ms)
- `project-info` (p99: <3,000ms)

**Gate Rule**: All slow handlers must pass; no MEDIUM+ regressions.

---

## Baseline Management

### Baseline Storage

Baselines are stored at: **`~/.continue/baselines/`**

**Filename Format**: `baseline-v{version}-{date}T{time}.json`

**Example**: `baseline-v2.0.0-2024-01-15T10-30-00.json`

### Baseline Structure

```json
{
  "version": "v2.0.0",
  "schema": "1.0",
  "timestamp": 1705325400000,
  "environment": {
    "os": "win32",
    "nodeVersion": "v18.17.0",
    "processor": "AMD64",
    "memory": 16384
  },
  "handlers": {
    "code-completion": {
      "latency": { "p50": 25, "p95": 85, "p99": 120 },
      "throughput": { "messagesPerSecond": 450 },
      "memory": { "heapUsed": 45, "heapTotal": 100, "external": 5 },
      "errorRate": 0.008,
      "tier": "fast"
    }
    // ... 19 more handlers
  },
  "checksum": "sha256..."
}
```

### Baseline Lifecycle

1. **Creation**: Run performance tests (Step 98) → save baseline
2. **Versioning**: Keep last 5 baselines; auto-prune older ones
3. **Comparison**: Current metrics compared against latest baseline
4. **Promotion**: After successful release, commit baseline to version control

---

## Execution Instructions

### Run Full Regression Test Suite

```bash
# Run all regression tests
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs --timeout 30000

# Expected output:
# ✓ Suite 1: Baseline Loading (5 tests)
# ✓ Suite 2: Metric Comparison (6 tests)
# ✓ Suite 3: Regression Classification (8 tests)
# ✓ Suite 4: Tier Validation (6 tests)
# ✓ Suite 5: Report Generation (8 tests)
# ✓ Suite 6: Integration & E2E (8 tests)
# ✓ Suite 7: Error Handling & Edge Cases (9 tests)
# 50+ passing
```

### Filter by Test Suite

```bash
# Run only baseline loading tests
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs --grep "Suite 1"

# Run only tier validation tests
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs --grep "Suite 4"

# Run only E2E tests
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs --grep "Suite 6"
```

### Filter by Tier

```bash
# Run fast tier regressions only
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs --grep "fast tier"

# Run medium tier regressions only
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs --grep "medium tier"

# Run slow tier regressions only
npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs --grep "slow tier"
```

### Custom Thresholds (Environment Variables)

```bash
# Override tolerance percentage (default: 10%)
REGRESSION_TOLERANCE_PCT=5 npx mocha ...

# Override p99 regression threshold (default: 25%)
REGRESSION_P99_THRESHOLD=20 npx mocha ...

# Override baseline version (default: v2.0.0)
REGRESSION_BASELINE_VERSION=v1.9.5 npx mocha ...

# Require pass (fail if any regressions)
REGRESSION_REQUIRE_PASS=true npx mocha ...

# Enable detailed logging
REGRESSION_ENABLE_LOGGING=true npx mocha ...
```

### C# Integration Tests

```bash
# Run C# regression comparison tests (xUnit)
dotnet test VSIXProject1.slnx --filter "Category=Regression"

# Run specific test class
dotnet test VSIXProject1.slnx --filter "FullyQualifiedName~RegressionComparisonTests"
```

---

## Report Interpretation

### JSON Report (Machine-Readable)

**Location**: Generated in-memory, typically persisted by CI/CD.

```json
{
  "format": "json",
  "timestamp": 1705325500000,
  "baseline": {
    "version": "v2.0.0",
    "timestamp": 1705325400000
  },
  "regressions": [
    {
      "handler": "code-completion",
      "tier": "fast",
      "severity": "HIGH",
      "metrics": {
        "latency": {
          "p99": {
            "baseline": 120,
            "current": 180,
            "deltaPercent": 50
          }
        }
      },
      "remediation": "Check symbol extraction cache, profile handler execution"
    }
  ],
  "tierStatus": {
    "fast": false,
    "medium": true,
    "slow": true,
    "allTiersPassed": false
  },
  "summary": {
    "totalHandlers": 20,
    "passedHandlers": 18,
    "regressionCount": 2,
    "criticalCount": 0,
    "highCount": 1,
    "mediumCount": 1,
    "releaseGate": "BLOCKED"
  },
  "decision": {
    "approved": false,
    "reason": "High regressions in fast tier"
  }
}
```

### Markdown Report (Human-Readable)

**Example**: Read full report generated by `formatMarkdownReport()`

Key sections:
1. **Executive Summary** — PASS/BLOCKED decision + stats
2. **Tier Status** — Per-tier gate results
3. **Regressions by Severity** — Grouped HIGH, MEDIUM, LOW
4. **Baseline Information** — Version, timestamp
5. **Recommendations** — Actions to unblock release

### Key Metrics Explained

| Metric | Meaning |
|--------|---------|
| `deltaPercent` | Percentage change from baseline (negative = improvement, positive = regression) |
| `p50` / `p95` / `p99` | Latency percentiles (50th, 95th, 99th) |
| `messagesPerSecond` | Throughput (handler capacity) |
| `heapUsed` | Current heap memory (MB) |
| `errorRate` | Percentage of failed invocations |

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Performance Regression Check

on: [pull_request]

jobs:
  regression-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Install Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'

      - name: Run Regression Tests
        run: npx mocha src/versions/v2.0.0/tests/handler-regression.test.mjs

      - name: Check Release Gate
        run: |
          node -e "
            import('./src/versions/v2.0.0/lib/regression-ci-integration.mjs')
              .then(m => m.checkReleaseGate({ comparisonResult: {...} }))
              .then(r => process.exit(r.exitCode));
          "

      - name: Generate Dashboard
        if: always()
        run: node scripts/regression-dashboard.mjs
```

### Environment Variables for CI/CD

```bash
# In GitHub Actions secrets or CI/CD configuration:
REGRESSION_TOLERANCE_PCT=10
REGRESSION_P99_THRESHOLD=25
REGRESSION_BASELINE_VERSION=v2.0.0
REGRESSION_REQUIRE_PASS=true
```

### Release Gate Decision

| Exit Code | Decision | Meaning |
|-----------|----------|---------|
| 0 | ✅ APPROVED | All gates passed, release ready |
| 1 | ❌ BLOCKED | Regressions detected, investigate required |

---

## Troubleshooting

### High False Positive Rate

**Symptom**: Regression detected on stable code.

**Diagnosis**:
- Check system load during test (CPU %, memory pressure)
- Verify baseline is from stable environment
- Compare against baseline date (old baseline may be outdated)

**Resolution**:
1. Increase tolerance: `REGRESSION_TOLERANCE_PCT=15`
2. Regenerate baseline from clean environment
3. Reduce noise with warmup iterations

### Baseline Corruption

**Symptom**: "Invalid baseline JSON" error.

**Diagnosis**:
- Baseline file corrupted or partially written
- Permission issue on `~/.continue/baselines/`

**Resolution**:
```bash
# Inspect baseline file
cat ~/.continue/baselines/baseline-v2.0.0-*.json | jq '.'

# Remove corrupted baseline
rm ~/.continue/baselines/baseline-v2.0.0-*.json

# Regenerate from performance tests
npm run test:performance
```

### Memory Regression on Clean Code

**Symptom**: Heap usage increased 15MB without code changes.

**Diagnosis**:
- Garbage collection not running between measurements
- External memory leak in dependency
- Baseline from low-memory environment

**Resolution**:
1. Force GC between measurements: `--expose-gc`
2. Profile handler: Use Node.js inspector (`node --inspect`)
3. Compare memory profiles: `node --prof handler.mjs`

### Tier Gate Failure

**Symptom**: "Fast tier FAILED" but individual handler tests pass.

**Diagnosis**:
- Handler has MEDIUM+ regression (not just LOW)
- Tier classification mismatch

**Resolution**:
1. Review detailed report: Check which handler failed in fast tier
2. Verify handler tier assignment: `fast`, `medium`, `slow`
3. Profile specific handler against baseline

### Performance Anomaly

**Symptom**: Metrics vary wildly between runs.

**Diagnosis**:
- System under load (background processes)
- Insufficient warmup iterations
- Node.js GC interference

**Resolution**:
1. Close background apps
2. Increase warmup: `--warmup-runs 50`
3. Use `--expose-gc` to control GC timing

---

## Related Documentation

- **Step 97**: Compliance test framework (baseline validation)
- **Step 98**: Performance test framework (baseline creation)
- **Step 99**: Stress test engine (memory/error metrics)
- **Step 110**: E2E scenario tests (integration baseline)
- **Step 111**: Cross-version compatibility (version progression)
- **Step 115**: Bridge feature parity matrix (release approval)

---

## Key Files

| File | Purpose |
|------|---------|
| `regression-comparison-engine.mjs` | Core detection logic |
| `regression-report-formatter.mjs` | Report generation |
| `handler-regression.test.mjs` | Test suite (50+ tests) |
| `regression-test-fixtures.mjs` | Sample baselines & scenarios |
| `regression-ci-integration.mjs` | CI/CD pipeline hooks |
| `RegressionComparisonTests.cs` | C# integration tests |

---

## Support & Escalation

**Questions?** See `docs/` directory for architecture details.

**Issues?** Check logs:
```bash
# View regression decision log
cat ~/.continue/release-decisions/release-decision-*.json
```

**Performance tuning?** Contact performance team → use `REGRESSION_TOLERANCE_PCT` and `REGRESSION_P99_THRESHOLD` to calibrate gates.
