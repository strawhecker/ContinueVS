#!/usr/bin/env node

/**
 * regression-test-fixtures.mjs
 * Step 112: Regression Test Suite - Test Fixtures
 * 
 * Sample baselines, synthetic regression scenarios, and expected reports.
 * Used for unit testing and regression detection validation.
 * 
 * @module regression-test-fixtures
 */

/**
 * Get baseline v2.0.0 (all handlers healthy).
 * @returns {Object} Baseline object
 */
export function getBaselineV200() {
  return {
    version: 'v2.0.0',
    schema: '1.0',
    timestamp: Date.now() - 86400000, // 1 day ago
    environment: {
      os: 'win32',
      nodeVersion: 'v18.17.0',
      processor: 'AMD64',
      memory: 16384
    },
    systemChecks: {
      diskSpace: 'ok',
      cpuLoad: 'normal',
      memoryUsage: '45%'
    },
    handlers: {
      'code-completion': {
        latency: { p50: 25, p95: 85, p99: 120 },
        throughput: { messagesPerSecond: 450 },
        memory: { heapUsed: 45, heapTotal: 100, external: 5 },
        errorRate: 0.008,
        tier: 'fast'
      },
      'search': {
        latency: { p50: 30, p95: 95, p99: 140 },
        throughput: { messagesPerSecond: 400 },
        memory: { heapUsed: 50, heapTotal: 100, external: 6 },
        errorRate: 0.010,
        tier: 'fast'
      },
      'go-to-definition': {
        latency: { p50: 35, p95: 110, p99: 160 },
        throughput: { messagesPerSecond: 380 },
        memory: { heapUsed: 55, heapTotal: 100, external: 7 },
        errorRate: 0.012,
        tier: 'fast'
      },
      'refactor': {
        latency: { p50: 200, p95: 800, p99: 1200 },
        throughput: { messagesPerSecond: 120 },
        memory: { heapUsed: 120, heapTotal: 200, external: 15 },
        errorRate: 0.005,
        tier: 'medium'
      },
      'apply-edit': {
        latency: { p50: 150, p95: 700, p99: 1100 },
        throughput: { messagesPerSecond: 150 },
        memory: { heapUsed: 100, heapTotal: 150, external: 12 },
        errorRate: 0.004,
        tier: 'medium'
      },
      'format-document': {
        latency: { p50: 180, p95: 750, p99: 1150 },
        throughput: { messagesPerSecond: 125 },
        memory: { heapUsed: 115, heapTotal: 180, external: 14 },
        errorRate: 0.006,
        tier: 'medium'
      },
      'git-integration': {
        latency: { p50: 300, p95: 2500, p99: 5000 },
        throughput: { messagesPerSecond: 50 },
        memory: { heapUsed: 200, heapTotal: 300, external: 25 },
        errorRate: 0.015,
        tier: 'slow'
      },
      'terminal': {
        latency: { p50: 250, p95: 2000, p99: 4500 },
        throughput: { messagesPerSecond: 60 },
        memory: { heapUsed: 180, heapTotal: 280, external: 22 },
        errorRate: 0.020,
        tier: 'slow'
      },
      'file-system': {
        latency: { p50: 200, p95: 1500, p99: 3500 },
        throughput: { messagesPerSecond: 80 },
        memory: { heapUsed: 160, heapTotal: 250, external: 20 },
        errorRate: 0.018,
        tier: 'slow'
      },
      'project-info': {
        latency: { p50: 150, p95: 1000, p99: 2500 },
        throughput: { messagesPerSecond: 100 },
        memory: { heapUsed: 140, heapTotal: 220, external: 18 },
        errorRate: 0.010,
        tier: 'slow'
      }
    },
    checksum: 'abc123baseline'
  };
}

/**
 * Get baseline v1.9.5 (legacy version for cross-version testing).
 * @returns {Object} Baseline object
 */
export function getBaselineV195() {
  return {
    version: 'v1.9.5',
    schema: '1.0',
    timestamp: Date.now() - 172800000, // 2 days ago
    environment: {
      os: 'win32',
      nodeVersion: 'v16.14.0',
      processor: 'AMD64',
      memory: 8192
    },
    systemChecks: {
      diskSpace: 'ok',
      cpuLoad: 'normal',
      memoryUsage: '60%'
    },
    handlers: {
      'code-completion': {
        latency: { p50: 40, p95: 120, p99: 200 },
        throughput: { messagesPerSecond: 300 },
        memory: { heapUsed: 70, heapTotal: 150, external: 10 },
        errorRate: 0.025,
        tier: 'fast'
      },
      'search': {
        latency: { p50: 50, p95: 140, p99: 220 },
        throughput: { messagesPerSecond: 250 },
        memory: { heapUsed: 80, heapTotal: 160, external: 12 },
        errorRate: 0.030,
        tier: 'fast'
      }
    },
    checksum: 'abc123old'
  };
}

/**
 * Get synthetic regression scenarios for testing.
 * @returns {Object} Map of scenario name to metrics snapshot
 */
export function getSyntheticRegressionScenarios() {
  const baseline = getBaselineV200();

  return {
    // Scenario 1: Latency regression only (p99 +30%)
    latencyRegression: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'code-completion': {
          ...baseline.handlers['code-completion'],
          latency: { p50: 26, p95: 90, p99: 156 } // p99 +30%
        }
      }
    },

    // Scenario 2: Throughput regression (msgs/sec -25%)
    throughputRegression: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'search': {
          ...baseline.handlers['search'],
          throughput: { messagesPerSecond: 300 } // -25%
        }
      }
    },

    // Scenario 3: Memory regression (heap +15MB)
    memoryRegression: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'refactor': {
          ...baseline.handlers['refactor'],
          memory: { heapUsed: 135, heapTotal: 215, external: 15 } // +15MB heap
        }
      }
    },

    // Scenario 4: Error rate spike (errors +5%)
    errorRateRegression: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'git-integration': {
          ...baseline.handlers['git-integration'],
          errorRate: 0.065 // +5% absolute
        }
      }
    },

    // Scenario 5: Mixed regressions (latency + memory + error)
    mixedRegressions: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'apply-edit': {
          ...baseline.handlers['apply-edit'],
          latency: { p50: 165, p95: 800, p99: 1400 }, // p99 +27%
          memory: { heapUsed: 120, heapTotal: 160, external: 12 }, // +20MB heap
          errorRate: 0.014 // +1% absolute
        },
        'terminal': {
          ...baseline.handlers['terminal'],
          latency: { p50: 300, p95: 2500, p99: 6000 } // p99 +33%
        }
      }
    },

    // Scenario 6: Critical cascading (multiple handlers fail)
    criticalCascading: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'code-completion': {
          ...baseline.handlers['code-completion'],
          latency: { p50: 50, p95: 200, p99: 300 } // p99 +150% CRITICAL
        },
        'search': {
          ...baseline.handlers['search'],
          latency: { p50: 60, p95: 220, p99: 350 }, // p99 +150% CRITICAL
          errorRate: 0.150 // +14% absolute CRITICAL
        }
      }
    },

    // Scenario 7: Fast tier only affected
    fastTierRegression: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'code-completion': {
          ...baseline.handlers['code-completion'],
          latency: { p50: 40, p95: 150, p99: 200 } // p99 +67%
        },
        'search': {
          ...baseline.handlers['search'],
          latency: { p50: 45, p95: 160, p99: 210 } // p99 +50%
        }
      }
    },

    // Scenario 8: Medium tier only affected
    mediumTierRegression: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'refactor': {
          ...baseline.handlers['refactor'],
          latency: { p50: 280, p95: 1200, p99: 1800 } // p99 +50%
        }
      }
    },

    // Scenario 9: Slow tier only affected
    slowTierRegression: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'git-integration': {
          ...baseline.handlers['git-integration'],
          latency: { p50: 450, p95: 4000, p99: 8000 } // p99 +60%
        }
      }
    },

    // Scenario 10: All tiers pass (new baseline acceptable)
    allPass: {
      ...baseline,
      handlers: {
        ...baseline.handlers,
        'code-completion': {
          ...baseline.handlers['code-completion'],
          latency: { p50: 26, p95: 87, p99: 123 } // <10% all percentiles
        }
      }
    }
  };
}

/**
 * Get mock comparison result for scenario.
 * @param {string} scenario - Scenario name
 * @returns {Object} Comparison result (pre-computed)
 */
export function getMockComparisonResult(scenario) {
  const scenarios = getSyntheticRegressionScenarios();
  const baseline = getBaselineV200();
  const current = scenarios[scenario];

  if (!current) {
    throw new Error(`Unknown scenario: ${scenario}`);
  }

  // Simplified mock result (real engine would compute this)
  return {
    handlerRegressions: [],
    summary: {
      totalHandlers: Object.keys(current.handlers).length,
      testedHandlers: Object.keys(current.handlers).length,
      passedHandlers: 0,
      regressionCount: 0,
      criticalCount: 0,
      highCount: 0,
      mediumCount: 0,
      lowCount: 0,
      releaseGate: scenario === 'allPass' ? 'PASSED' : 'BLOCKED'
    },
    tierStatus: {
      fast: scenario !== 'fastTierRegression' && scenario !== 'criticalCascading',
      medium: scenario !== 'mediumTierRegression' && scenario !== 'mixedRegressions',
      slow: scenario !== 'slowTierRegression'
    },
    baselineVersion: baseline.version,
    baselineTimestamp: baseline.timestamp
  };
}

/**
 * Get mock logger for testing.
 * @returns {Object} Logger with log function
 */
export function getMockLogger() {
  const logs = [];

  return {
    log: (msg) => {
      logs.push({ timestamp: Date.now(), message: msg });
    },
    getLogs: () => logs,
    clear: () => { logs.length = 0; }
  };
}

/**
 * Get mock metrics object for testing.
 * @returns {Object} Metrics with common properties
 */
export function getMockMetrics() {
  return {
    latency: { p50: 100, p95: 500, p99: 1000 },
    throughput: { messagesPerSecond: 200 },
    memory: { heapUsed: 100, heapTotal: 200, external: 10 },
    errorRate: 0.01,
    tier: 'medium'
  };
}

/**
 * Create test context builder.
 * @returns {Object} Builder object
 */
export function createTestContextBuilder() {
  return {
    baseline: getBaselineV200(),
    current: null,
    scenarios: getSyntheticRegressionScenarios(),
    logger: getMockLogger(),

    withScenario(name) {
      this.current = this.scenarios[name];
      return this;
    },

    withBaseline(baseline) {
      this.baseline = baseline;
      return this;
    },

    build() {
      return {
        baseline: this.baseline,
        current: this.current,
        logger: this.logger
      };
    }
  };
}

/**
 * Create verification utility for test assertions.
 * @returns {Object} Verification utilities
 */
export function createVerificationUtility() {
  return {
    isValidRegression(regression) {
      return regression && regression.handler && regression.overallSeverity;
    },

    isValidReport(report) {
      return report && report.summary && report.tierStatus && report.decision;
    },

    isValidJSONReport(report) {
      return this.isValidReport(report) && report.regressions && Array.isArray(report.regressions);
    },

    assertSeverity(regression, expectedSeverity) {
      if (regression.overallSeverity !== expectedSeverity) {
        throw new Error(
          `Expected ${expectedSeverity} but got ${regression.overallSeverity}`
        );
      }
    },

    assertRegressionCount(summary, expected) {
      if (summary.regressionCount !== expected) {
        throw new Error(
          `Expected ${expected} regressions but got ${summary.regressionCount}`
        );
      }
    },

    assertTierStatus(tierStatus, tier, expectedPass) {
      if (tierStatus[tier] !== expectedPass) {
        throw new Error(
          `Expected ${tier} tier to be ${expectedPass ? 'PASS' : 'FAIL'}`
        );
      }
    }
  };
}
