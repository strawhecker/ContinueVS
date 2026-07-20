/**
 * regression-detector.mjs
 * Step 98: Regression Detection & Analysis
 * 
 * Automated comparison of current measurements against baseline.
 * Detects regressions, calculates severity, provides remediation paths.
 */

import { PerformanceError } from './performance-test-framework.mjs';

/**
 * Regression detector: compare current vs baseline metrics
 */
export class RegressionDetector {
  constructor(options = {}) {
    this.tolerancePercent = options.tolerancePercent || 10;
    this.severityThresholds = options.severityThresholds || {
      p99Regression: 25,      // 25%+ = HIGH
      throughputDegradation: 20, // 20%+ = HIGH
      memoryLeak: 10          // 10MB+ = HIGH
    };
    this.logger = options.logger;
  }

  /**
   * Detect regressions: compare current vs baseline
   */
  detectRegressions(current, baseline) {
    if (!baseline || !baseline.handlers) {
      return {
        handlerRegressions: [],
        summary: {
          totalHandlers: Object.keys(current).length,
          passedHandlers: Object.keys(current).length,
          regressionCount: 0,
          criticalCount: 0,
          highCount: 0,
          mediumCount: 0,
          recommendation: 'PASS' // No baseline to compare
        },
        baselineUnavailable: true
      };
    }

    const handlerRegressions = [];
    const tierResults = { fast: true, medium: true, slow: true };

    for (const [handlerName, currentMetrics] of Object.entries(current)) {
      const baselineMetrics = baseline.handlers[handlerName];
      if (!baselineMetrics) {
        continue; // Handler not in baseline, skip
      }

      const regression = this._analyzeHandler(
        handlerName,
        currentMetrics,
        baselineMetrics
      );

      if (regression) {
        handlerRegressions.push(regression);
        if (regression.overallSeverity !== 'NONE' && regression.tier) {
          tierResults[regression.tier] = false;
        }
      }
    }

    const criticalCount = handlerRegressions.filter(r => r.overallSeverity === 'CRITICAL').length;
    const highCount = handlerRegressions.filter(r => r.overallSeverity === 'HIGH').length;
    const mediumCount = handlerRegressions.filter(r => r.overallSeverity === 'MEDIUM').length;
    const regressionCount = handlerRegressions.filter(r => r.overallSeverity !== 'NONE').length;

    return {
      handlerRegressions,
      summary: {
        totalHandlers: handlerRegressions.length,
        passedHandlers: handlerRegressions.filter(r => r.overallSeverity === 'NONE').length,
        regressionCount,
        criticalCount,
        highCount,
        mediumCount,
        tierStatus: tierResults,
        recommendation: this._getRecommendation(handlerRegressions, tierResults)
      },
      tierStatus: tierResults
    };
  }

  /**
   * Analyze single handler for regressions
   */
  _analyzeHandler(handlerName, current, baseline) {
    const currentLatency = current.latency || {};
    const baselineLatency = baseline.latency || {};

    const p99Regression = baselineLatency.p99
      ? ((currentLatency.p99 - baselineLatency.p99) / baselineLatency.p99) * 100
      : 0;

    const throughputRegression = baseline.throughput?.messagesPerSecond
      ? ((baseline.throughput.messagesPerSecond - (current.throughput?.messagesPerSecond || 0)) /
        baseline.throughput.messagesPerSecond) * 100
      : 0;

    const memoryDelta = (current.memory?.deltaMB || 0) - (baseline.memory?.deltaMB || 0);

    const p99Severity = this._getSeverity(
      p99Regression,
      this.severityThresholds.p99Regression
    );
    const throughputSeverity = this._getSeverity(
      throughputRegression,
      this.severityThresholds.throughputDegradation
    );
    const memorySeverity = memoryDelta > 0
      ? this._getMemorySeverity(memoryDelta, this.severityThresholds.memoryLeak)
      : 'NONE';

    const overallSeverity = this._getOverallSeverity([
      p99Severity,
      throughputSeverity,
      memorySeverity
    ]);

    return {
      handler: handlerName,
      tier: this._getHandlerTier(handlerName),
      p99: {
        baseline: baselineLatency.p99,
        current: currentLatency.p99,
        regressionPercent: p99Regression.toFixed(2),
        severity: p99Severity
      },
      throughput: {
        baseline: baseline.throughput?.messagesPerSecond,
        current: current.throughput?.messagesPerSecond,
        regressionPercent: throughputRegression.toFixed(2),
        severity: throughputSeverity
      },
      memory: {
        baseline: baseline.memory?.deltaMB,
        current: current.memory?.deltaMB,
        deltaPercent: baseline.memory?.deltaMB
          ? ((memoryDelta / baseline.memory.deltaMB) * 100).toFixed(2)
          : 0,
        severity: memorySeverity
      },
      overallSeverity,
      recommendation: this._getHandlerRecommendation(overallSeverity, p99Regression)
    };
  }

  /**
   * Get severity level: NONE, LOW, MEDIUM, HIGH, CRITICAL
   */
  _getSeverity(regression, threshold) {
    if (Math.abs(regression) < 10) return 'NONE';
    if (Math.abs(regression) < threshold * 0.5) return 'LOW';
    if (Math.abs(regression) < threshold * 0.75) return 'MEDIUM';
    if (Math.abs(regression) < threshold) return 'HIGH';
    return 'CRITICAL';
  }

  /**
   * Get memory-specific severity
   */
  _getMemorySeverity(deltaMB, threshold) {
    if (deltaMB < 5) return 'NONE';
    if (deltaMB < threshold * 0.5) return 'LOW';
    if (deltaMB < threshold) return 'MEDIUM';
    return 'HIGH';
  }

  /**
   * Calculate overall severity from component severities
   */
  _getOverallSeverity(severities) {
    const levels = { CRITICAL: 4, HIGH: 3, MEDIUM: 2, LOW: 1, NONE: 0 };
    const maxLevel = Math.max(...severities.map(s => levels[s] || 0));

    for (const [level, value] of Object.entries(levels)) {
      if (value === maxLevel) return level;
    }
    return 'NONE';
  }

  /**
   * Get handler tier: fast, medium, slow
   */
  _getHandlerTier(handlerName) {
    const fastHandlers = ['search', 'code-lens', 'model-info', 'profiler', 'go-to-def'];
    const mediumHandlers = ['refactor', 'completion', 'hover', 'apply-edit', 'format',
      'git', 'terminal', 'settings', 'snippet', 'workspace-reload'];
    const slowHandlers = ['diff-viewer', 'test-explorer', 'debug-session', 'streaming',
      'refactor-tests', 'project-info', 'sidebar', 'context-window', 'inline-msg', 'find-ref'];

    if (fastHandlers.includes(handlerName)) return 'fast';
    if (mediumHandlers.includes(handlerName)) return 'medium';
    if (slowHandlers.includes(handlerName)) return 'slow';
    return 'unknown';
  }

  /**
   * Get handler-specific recommendation
   */
  _getHandlerRecommendation(severity, p99Regression) {
    switch (severity) {
      case 'CRITICAL':
        return 'ESCALATE: Measure with profiler, investigate immediately';
      case 'HIGH':
        return `OPTIMIZE: p99 regression ${Math.abs(p99Regression).toFixed(1)}% detected`;
      case 'MEDIUM':
        return 'INVESTIGATE: Monitor for next release';
      case 'LOW':
        return 'MONITOR: Small variance, acceptable';
      default:
        return 'OK: Within tolerance';
    }
  }

  /**
   * Get recommendation for full test run
   */
  _getRecommendation(regressions, tierStatus) {
    const criticalCount = regressions.filter(r => r.overallSeverity === 'CRITICAL').length;
    const highCount = regressions.filter(r => r.overallSeverity === 'HIGH').length;

    if (criticalCount > 0) return 'CRITICAL';
    if (highCount > 2) return 'OPTIMIZE';
    if (highCount > 0) return 'INVESTIGATE';
    if (!tierStatus.fast || !tierStatus.medium) return 'WARN';
    return 'ACCEPTABLE';
  }
}

/**
 * Factory function
 */
export function createRegressionDetector(options = {}) {
  return new RegressionDetector(options);
}
