/**
 * performance-test-framework.mjs
 * Step 98: Handler Performance Test Framework
 * 
 * Core measurement validator with latency/throughput/memory utilities.
 * Supports warm-up handling, percentile calculation, and SLA validation.
 */

import { performance } from 'perf_hooks';
import { createHash } from 'crypto';

/**
 * Custom error classes for performance testing
 */
export class PerformanceError extends Error {
  constructor(message, context = {}) {
    super(message);
    this.name = 'PerformanceError';
    this.context = context;
  }
}

export class GateViolationError extends PerformanceError {
  constructor(violations, gate) {
    super(`Performance gate violations detected for ${gate.handlerName}`, {
      violations,
      gate
    });
    this.name = 'GateViolationError';
    this.violations = violations;
    this.gate = gate;
  }
}

export class EnvironmentValidationError extends PerformanceError {
  constructor(message, failedChecks = []) {
    super(message, { failedChecks });
    this.name = 'EnvironmentValidationError';
    this.failedChecks = failedChecks;
  }
}

/**
 * Performance gate definition with SLA thresholds
 */
export class PerformanceGate {
  constructor(config) {
    this.tier = config.tier; // 'fast' | 'medium' | 'slow'
    this.handlerName = config.handlerName;
    this.p50Max = config.p50Max;
    this.p95Max = config.p95Max;
    this.p99Max = config.p99Max;
    this.timeoutPolicyMs = config.timeoutPolicyMs;
    this.memoryMaxMB = config.memoryMaxMB;
    this.minThroughput = config.minThroughput; // msgs/sec
  }

  validate(percentiles) {
    const violations = [];

    if (percentiles.p50 > this.p50Max) {
      violations.push({
        metric: 'p50',
        measured: percentiles.p50,
        limit: this.p50Max,
        excess: percentiles.p50 - this.p50Max
      });
    }

    if (percentiles.p95 > this.p95Max) {
      violations.push({
        metric: 'p95',
        measured: percentiles.p95,
        limit: this.p95Max,
        excess: percentiles.p95 - this.p95Max
      });
    }

    if (percentiles.p99 > this.p99Max) {
      violations.push({
        metric: 'p99',
        measured: percentiles.p99,
        limit: this.p99Max,
        excess: percentiles.p99 - this.p99Max
      });
    }

    return {
      passed: violations.length === 0,
      violations,
      severityScore: this._calculateSeverity(violations)
    };
  }

  _calculateSeverity(violations) {
    if (violations.length === 0) return 0;
    const maxExcess = Math.max(...violations.map(v => v.excess / v.limit));
    return Math.min(100, maxExcess * 100);
  }

  getDescription() {
    return `${this.handlerName} (${this.tier}): p50=${this.p50Max}ms, p95=${this.p95Max}ms, p99=${this.p99Max}ms`;
  }
}

/**
 * Core performance validator for latency/throughput/memory measurement
 */
export class PerformanceValidator {
  constructor(options = {}) {
    this.logger = options.logger;
    this.metrics = options.metrics;
  }

  /**
   * Measure latency with warm-up phase
   * Discards first N runs for JIT compilation and V8 optimization
   */
  async measureLatencyWithWarmup(handlerFn, testPayload, options = {}) {
    const { runs = 1000, warmupRuns = 50, label = 'handler' } = options;

    this.logger?.log(`[${label}] Starting warm-up: ${warmupRuns} runs`);

    // Warm-up phase: discard results
    for (let i = 0; i < warmupRuns; i++) {
      await handlerFn(testPayload);
    }

    this.logger?.log(`[${label}] Warm-up complete, measuring ${runs} runs`);

    const latencies = [];

    // Measurement phase
    for (let i = 0; i < runs; i++) {
      const start = process.hrtime.bigint();
      await handlerFn(testPayload);
      const end = process.hrtime.bigint();
      const durationMs = Number(end - start) / 1_000_000;
      latencies.push(durationMs);
    }

    const percentiles = this.calculatePercentiles(latencies);

    return {
      latencies,
      percentiles,
      warmupStats: { runs: warmupRuns, skipped: true },
      metadata: { runsAfterWarmup: runs, timestamp: Date.now(), label }
    };
  }

  /**
   * Calculate latency percentiles with optional outlier filtering
   * Handles JIT compilation variance and system interrupts
   */
  calculatePercentiles(latencies, options = {}) {
    const { filterOutliers = true, filterThreshold = 0.001 } = options;

    let values = [...latencies].sort((a, b) => a - b);
    const originalCount = values.length;

    if (filterOutliers) {
      const lowIdx = Math.floor(values.length * filterThreshold);
      const highIdx = Math.ceil(values.length * (1 - filterThreshold));
      values = values.slice(lowIdx, highIdx);
    }

    const filteredCount = values.length;
    const outlierCount = originalCount - filteredCount;

    const sum = values.reduce((a, b) => a + b, 0);
    const mean = sum / values.length;

    const variance = values.reduce((sum, v) => sum + Math.pow(v - mean, 2), 0) / values.length;
    const stdDev = Math.sqrt(variance);

    return {
      p50: this._percentile(values, 0.50),
      p95: this._percentile(values, 0.95),
      p99: this._percentile(values, 0.99),
      min: Math.min(...values),
      max: Math.max(...values),
      mean,
      stdDev,
      outlierCount,
      filteredCount,
      coefficientOfVariation: stdDev / mean
    };
  }

  /**
   * Measure throughput: messages per second under sustained load
   * Sequential batches to avoid concurrency complexity
   */
  async measureThroughput(handlerFn, testPayload, options = {}) {
    const { batches = 10, batchSize = 10 } = options;
    const totalMessages = batches * batchSize;

    this.logger?.log(`Measuring throughput: ${batches} batches × ${batchSize} msgs/batch`);

    const start = Date.now();

    for (let i = 0; i < batches; i++) {
      for (let j = 0; j < batchSize; j++) {
        await handlerFn(testPayload);
      }
    }

    const duration = (Date.now() - start) / 1000; // Convert to seconds
    const messagesPerSecond = totalMessages / duration;

    return {
      messagesPerSecond,
      totalMessages,
      duration,
      batches,
      batchSize,
      variance: 0 // Variance not computed for sequential
    };
  }

  /**
   * Measure memory usage with GC control
   * Forces GC before/after and periodically during iterations
   */
  async measureMemory(handlerFn, testPayload, options = {}) {
    const { iterations = 100, forceGC = true, periodicGC = true } = options;

    // Force GC before measurement
    if (forceGC && global.gc) {
      global.gc();
      await new Promise(resolve => setTimeout(resolve, 100)); // Let GC settle
    }

    const before = process.memoryUsage().heapUsed;

    for (let i = 0; i < iterations; i++) {
      await handlerFn(testPayload);
      if (periodicGC && i % 10 === 0 && global.gc) {
        global.gc();
      }
    }

    // Force GC after measurement
    if (forceGC && global.gc) {
      global.gc();
      await new Promise(resolve => setTimeout(resolve, 100));
    }

    const after = process.memoryUsage().heapUsed;

    const deltaMB = (after - before) / 1024 / 1024;

    return {
      deltaMB,
      before: before / 1024 / 1024,
      after: after / 1024 / 1024,
      leakDetected: deltaMB > 50,
      periodGCCount: Math.floor(iterations / 10)
    };
  }

  /**
   * Measure latency across multiple payload sizes
   * Analyze scaling behavior (linear, sublinear, etc.)
   */
  async measureScaling(handlerFn, payloadSizes, options = {}) {
    const { runs = 100, label = 'handler' } = options;
    const results = new Map();

    for (const size of payloadSizes) {
      this.logger?.log(`Measuring ${label} scaling for ${size.label} (${size.sizeKB}KB)`);

      const measurement = await this.measureLatencyWithWarmup(
        handlerFn,
        size.payload || size,
        { runs, warmupRuns: 20, label: `${label}-${size.label}` }
      );

      results.set(size.label, {
        latencies: measurement.latencies,
        percentiles: measurement.percentiles,
        sizeKB: size.sizeKB
      });
    }

    return results;
  }

  /**
   * Validate measured percentiles against performance gate
   */
  validateGate(percentiles, gate) {
    return gate.validate(percentiles);
  }

  /**
   * Internal: Calculate percentile value from sorted array
   */
  _percentile(sortedValues, percentile) {
    if (sortedValues.length === 0) return 0;
    const index = percentile * (sortedValues.length - 1);
    const lower = Math.floor(index);
    const upper = Math.ceil(index);
    const weight = index % 1;

    if (lower === upper) {
      return sortedValues[lower];
    }

    return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
  }
}

/**
 * Factory: Create default performance gates for all handlers
 */
export function createDefaultGates() {
  const gates = new Map();

  // Fast tier handlers (p99 < 10ms)
  const fastHandlers = ['search', 'code-lens', 'model-info', 'profiler', 'go-to-def'];
  for (const name of fastHandlers) {
    gates.set(name, new PerformanceGate({
      tier: 'fast',
      handlerName: name,
      p50Max: 5,
      p95Max: 8,
      p99Max: 10,
      timeoutPolicyMs: 2000,
      memoryMaxMB: 10,
      minThroughput: 100
    }));
  }

  // Medium tier handlers (p99 < 50ms)
  const mediumHandlers = ['refactor', 'completion', 'hover', 'apply-edit', 'format',
    'git', 'terminal', 'settings', 'snippet', 'workspace-reload'];
  for (const name of mediumHandlers) {
    gates.set(name, new PerformanceGate({
      tier: 'medium',
      handlerName: name,
      p50Max: 15,
      p95Max: 40,
      p99Max: 50,
      timeoutPolicyMs: 10000,
      memoryMaxMB: 25,
      minThroughput: 100
    }));
  }

  // Slow tier handlers (p99 < 500ms)
  const slowHandlers = ['diff-viewer', 'test-explorer', 'debug-session', 'streaming',
    'refactor-tests', 'project-info', 'sidebar', 'context-window', 'inline-msg', 'find-ref'];
  for (const name of slowHandlers) {
    gates.set(name, new PerformanceGate({
      tier: 'slow',
      handlerName: name,
      p50Max: 50,
      p95Max: 200,
      p99Max: 500,
      timeoutPolicyMs: 30000,
      memoryMaxMB: 50,
      minThroughput: 100
    }));
  }

  return gates;
}

/**
 * Factory: Create performance validator instance
 */
export function createPerformanceValidator(options = {}) {
  return new PerformanceValidator(options);
}
