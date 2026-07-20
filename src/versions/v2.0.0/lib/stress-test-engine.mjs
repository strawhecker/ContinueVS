#!/usr/bin/env node

/**
 * Handler Stress Test Engine
 *
 * Orchestrates concurrent load, error injection, sustained throughput,
 * and cascading failure scenarios for handler validation.
 *
 * @module src/versions/v2.0.0/lib/stress-test-engine.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 97: Compliance framework (baseline p99 <100ms)
 *   - Step 98: Performance tests (baseline throughput)
 *   - Step 99: Stress tests (this module, 4 scenarios)
 *   - Step 110: E2E scenarios (uses stress fixtures)
 *   - Step 112: Regression suite (uses stress results)
 *   - Step 115: Part III gate (stress test report required)
 *
 * Scenarios:
 *   1. High Concurrency: 50–100 parallel requests/handler → p99 <500ms
 *   2. Error Injection: Timeouts, protocol errors, missing deps → <5% error
 *   3. Sustained Load: 1000 msg/min × 30s → memory stable, no leaks
 *   4. Cascading Failures: One handler error → isolation for others
 */

/**
 * Metrics snapshot for a single request/handler execution.
 * @typedef {Object} ExecutionMetrics
 * @property {number} startTime - Performance.now() at request start
 * @property {number} endTime - Performance.now() at request completion
 * @property {number} latencyMs - endTime - startTime
 * @property {boolean} success - Handler returned success=true
 * @property {string} [errorType] - Error category (timeout, validation, protocol, etc.)
 * @property {string} [errorMessage] - Error details
 * @property {number} memoryBefore - Memory usage before handler (bytes)
 * @property {number} memoryAfter - Memory usage after handler (bytes)
 * @property {number} memoryDelta - memoryAfter - memoryBefore (bytes)
 */

/**
 * Metrics collection for a scenario run.
 * @typedef {Object} ScenarioMetrics
 * @property {string} scenarioName - 'concurrency', 'errorInjection', 'sustainedLoad', 'cascading'
 * @property {number} handlerCount - Number of handlers tested
 * @property {number} totalRequests - Total requests dispatched
 * @property {number} successCount - Successful responses
 * @property {number} errorCount - Failed responses
 * @property {number} errorRate - (errorCount / totalRequests) as percentage
 * @property {number} durationMs - Total scenario execution time
 * @property {number} throughput - Requests per second
 * @property {Object} latencyPercentiles - { p50, p95, p99, max }
 * @property {Object} memoryStats - { minDelta, maxDelta, avgDelta, peak }
 * @property {Object} handlerResults - Map<handlerName, { success: bool, details }>
 * @property {ExecutionMetrics[]} rawMetrics - All individual execution records
 */

/**
 * Error injection configuration.
 * @typedef {Object} ErrorInjectionConfig
 * @property {boolean} enabled - Enable error injection
 * @property {string[]} scenarios - Array of error types to inject
 * @property {number} injectionRate - [0–1.0] Fraction of requests to inject errors into
 */

/**
 * Stress test scenario configuration.
 * @typedef {Object} StressScenarioConfig
 * @property {number} concurrencyLevel - Number of parallel requests (concurrency scenario)
 * @property {number} requestsPerHandler - Total requests per handler (concurrency scenario)
 * @property {number} durationSeconds - Scenario duration (sustained load scenario)
 * @property {number} messagesPerSecond - Target throughput (sustained load scenario)
 * @property {ErrorInjectionConfig} errorInjection - Error injection settings
 * @property {boolean} measureMemory - Enable memory profiling
 * @property {boolean} captureRawMetrics - Capture all individual execution records
 */

/**
 * Stress Test Engine
 *
 * Coordinates scenario execution, metrics collection, and results aggregation.
 */
export class StressTestEngine {
  /**
   * @param {Object} config
   * @param {Map<string, Function>} config.handlers - Handler registry (messageType → handler function)
   * @param {Object} config.logger - Logger instance (or null for silent)
   * @param {Object} config.metrics - Metrics collector (or null for no collection)
   * @param {StressScenarioConfig} config.scenarioDefaults - Default scenario configuration
   */
  constructor({
    handlers = new Map(),
    logger = null,
    metrics = null,
    scenarioDefaults = {},
  } = {}) {
    this.handlers = handlers;
    this.logger = logger || this._createMockLogger();
    this.metrics = metrics || this._createMockMetrics();

    this.scenarioDefaults = {
      concurrencyLevel: 50,
      requestsPerHandler: 500,
      durationSeconds: 30,
      messagesPerSecond: 1000,
      errorInjection: {
        enabled: false,
        scenarios: ['timeout', 'protocol_error', 'missing_dependency'],
        injectionRate: 0.05,
      },
      measureMemory: true,
      captureRawMetrics: true,
      ...scenarioDefaults,
    };

    this.errorInjector = new ErrorInjector(this.logger);
  }

  // ========== SCENARIO RUNNERS ==========

  /**
   * High Concurrency Scenario
   *
   * Stress all handlers with parallel requests. Measures latency percentiles,
   * error rates, and memory pressure under concurrent load.
   *
   * @param {Object} config - Scenario overrides
   * @returns {Promise<ScenarioMetrics>}
   */
  async runConcurrencyScenario(config = {}) {
    const cfg = { ...this.scenarioDefaults, ...config };
    this.logger.info(
      `[CONCURRENCY] Starting: ${cfg.concurrencyLevel}x concurrency, ` +
      `${cfg.requestsPerHandler} requests/handler`
    );

    const scenarioMetrics = {
      scenarioName: 'concurrency',
      handlerCount: this.handlers.size,
      totalRequests: 0,
      successCount: 0,
      errorCount: 0,
      errorRate: 0,
      durationMs: 0,
      throughput: 0,
      latencyPercentiles: {},
      memoryStats: {},
      handlerResults: new Map(),
      rawMetrics: [],
    };

    const startTime = Date.now();
    const allMetrics = [];

    // Dispatch all handlers
    for (const [messageType, handler] of this.handlers) {
      const handlerMetrics = await this._runConcurrentHandler(
        messageType,
        handler,
        cfg
      );
      allMetrics.push(...handlerMetrics);
      scenarioMetrics.handlerResults.set(messageType, {
        success: handlerMetrics.every((m) => m.success),
        count: handlerMetrics.length,
      });
    }

    scenarioMetrics.durationMs = Date.now() - startTime;
    scenarioMetrics.totalRequests = allMetrics.length;
    scenarioMetrics.successCount = allMetrics.filter((m) => m.success).length;
    scenarioMetrics.errorCount = scenarioMetrics.totalRequests - scenarioMetrics.successCount;
    scenarioMetrics.errorRate = (scenarioMetrics.errorCount / scenarioMetrics.totalRequests) * 100;
    scenarioMetrics.throughput = (scenarioMetrics.totalRequests / scenarioMetrics.durationMs) * 1000;

    scenarioMetrics.latencyPercentiles = this._calculatePercentiles(
      allMetrics.map((m) => m.latencyMs)
    );
    scenarioMetrics.memoryStats = this._calculateMemoryStats(allMetrics);
    if (cfg.captureRawMetrics) {
      scenarioMetrics.rawMetrics = allMetrics;
    }

    this.logger.info(
      `[CONCURRENCY] Complete: ${scenarioMetrics.successCount}/${scenarioMetrics.totalRequests} success, ` +
      `p99=${scenarioMetrics.latencyPercentiles.p99}ms, error_rate=${scenarioMetrics.errorRate.toFixed(2)}%`
    );

    return scenarioMetrics;
  }

  /**
   * Error Injection Scenario
   *
   * Systematically inject failures (timeouts, protocol errors, missing deps)
   * and validate handler isolation and graceful degradation.
   *
   * @param {Object} config - Scenario overrides
   * @returns {Promise<ScenarioMetrics>}
   */
  async runErrorInjectionScenario(config = {}) {
    const cfg = { ...this.scenarioDefaults, errorInjection: { enabled: true, injectionRate: 0.5 }, ...config };
    this.logger.info(`[ERROR_INJECTION] Starting: injection_rate=${cfg.errorInjection.injectionRate}`);

    const scenarioMetrics = {
      scenarioName: 'errorInjection',
      handlerCount: this.handlers.size,
      totalRequests: 0,
      successCount: 0,
      errorCount: 0,
      errorRate: 0,
      durationMs: 0,
      throughput: 0,
      latencyPercentiles: {},
      memoryStats: {},
      handlerResults: new Map(),
      rawMetrics: [],
      errorBreakdown: {},
    };

    const startTime = Date.now();
    const allMetrics = [];

    // Run each handler with error injection
    for (const [messageType, handler] of this.handlers) {
      const handlerMetrics = await this._runErrorInjectedHandler(messageType, handler, cfg);
      allMetrics.push(...handlerMetrics);

      // Track error types
      const errorTypes = new Set();
      handlerMetrics.forEach((m) => {
        if (m.errorType) errorTypes.add(m.errorType);
      });
      scenarioMetrics.handlerResults.set(messageType, {
        success: handlerMetrics.filter((m) => m.success).length,
        errors: handlerMetrics.filter((m) => !m.success).length,
        errorTypes: Array.from(errorTypes),
      });
    }

    scenarioMetrics.durationMs = Date.now() - startTime;
    scenarioMetrics.totalRequests = allMetrics.length;
    scenarioMetrics.successCount = allMetrics.filter((m) => m.success).length;
    scenarioMetrics.errorCount = scenarioMetrics.totalRequests - scenarioMetrics.successCount;
    scenarioMetrics.errorRate = (scenarioMetrics.errorCount / scenarioMetrics.totalRequests) * 100;
    scenarioMetrics.throughput = (scenarioMetrics.totalRequests / scenarioMetrics.durationMs) * 1000;

    scenarioMetrics.latencyPercentiles = this._calculatePercentiles(
      allMetrics.map((m) => m.latencyMs)
    );

    // Breakdown by error type
    scenarioMetrics.errorBreakdown = {};
    allMetrics.forEach((m) => {
      if (m.errorType) {
        scenarioMetrics.errorBreakdown[m.errorType] =
          (scenarioMetrics.errorBreakdown[m.errorType] || 0) + 1;
      }
    });

    if (cfg.captureRawMetrics) {
      scenarioMetrics.rawMetrics = allMetrics;
    }

    this.logger.info(
      `[ERROR_INJECTION] Complete: ${scenarioMetrics.successCount}/${scenarioMetrics.totalRequests} success, ` +
      `error_rate=${scenarioMetrics.errorRate.toFixed(2)}%`
    );

    return scenarioMetrics;
  }

  /**
   * Sustained Load Scenario
   *
   * Run all handlers at target throughput (1000 msg/min) for extended duration.
   * Detect memory leaks, connection exhaustion, and degradation over time.
   *
   * @param {Object} config - Scenario overrides
   * @returns {Promise<ScenarioMetrics>}
   */
  async runSustainedLoadScenario(config = {}) {
    const cfg = { ...this.scenarioDefaults, ...config };
    this.logger.info(
      `[SUSTAINED_LOAD] Starting: ${cfg.messagesPerSecond} msg/s for ${cfg.durationSeconds}s`
    );

    const scenarioMetrics = {
      scenarioName: 'sustainedLoad',
      handlerCount: this.handlers.size,
      totalRequests: 0,
      successCount: 0,
      errorCount: 0,
      errorRate: 0,
      durationMs: 0,
      throughput: 0,
      latencyPercentiles: {},
      memoryStats: {},
      handlerResults: new Map(),
      rawMetrics: [],
      phaseBreakdown: [],
    };

    const startTime = Date.now();
    const allMetrics = [];
    const phaseLength = 10000; // 10s phases for memory tracking
    const phases = Math.ceil((cfg.durationSeconds * 1000) / phaseLength);

    for (let phase = 0; phase < phases; phase++) {
      const phaseMetrics = [];
      const phaseStart = Date.now();
      const messagesThisPhase = Math.floor(
        (cfg.messagesPerSecond / 1000) * phaseLength
      );

      // Distribute messages across handlers
      const messagesPerHandler = Math.ceil(messagesThisPhase / this.handlers.size);

      for (const [messageType, handler] of this.handlers) {
        for (let i = 0; i < messagesPerHandler; i++) {
          const metrics = await this._executeHandlerOnce(
            messageType,
            handler,
            cfg,
            false
          );
          phaseMetrics.push(metrics);
        }
      }

      const phaseElapsed = Date.now() - phaseStart;
      const phaseSuccessRate =
        (phaseMetrics.filter((m) => m.success).length / phaseMetrics.length) * 100;

      scenarioMetrics.phaseBreakdown.push({
        phase,
        messagesCount: phaseMetrics.length,
        successRate: phaseSuccessRate,
        elapsedMs: phaseElapsed,
        memoryAvgDelta: this._calculateMemoryStats(phaseMetrics).avgDelta,
      });

      allMetrics.push(...phaseMetrics);
    }

    scenarioMetrics.durationMs = Date.now() - startTime;
    scenarioMetrics.totalRequests = allMetrics.length;
    scenarioMetrics.successCount = allMetrics.filter((m) => m.success).length;
    scenarioMetrics.errorCount = scenarioMetrics.totalRequests - scenarioMetrics.successCount;
    scenarioMetrics.errorRate = (scenarioMetrics.errorCount / scenarioMetrics.totalRequests) * 100;
    scenarioMetrics.throughput = (scenarioMetrics.totalRequests / scenarioMetrics.durationMs) * 1000;

    scenarioMetrics.latencyPercentiles = this._calculatePercentiles(
      allMetrics.map((m) => m.latencyMs)
    );
    scenarioMetrics.memoryStats = this._calculateMemoryStats(allMetrics);

    if (cfg.captureRawMetrics) {
      scenarioMetrics.rawMetrics = allMetrics;
    }

    this.logger.info(
      `[SUSTAINED_LOAD] Complete: ${scenarioMetrics.successCount}/${scenarioMetrics.totalRequests} success, ` +
      `throughput=${scenarioMetrics.throughput.toFixed(2)} req/s, ` +
      `memory_avg_delta=${(scenarioMetrics.memoryStats.avgDelta / 1024).toFixed(2)}KB`
    );

    return scenarioMetrics;
  }

  /**
   * Cascading Failure Scenario
   *
   * Trigger one handler to fail and measure isolation. Verify other handlers
   * remain functional and uncontaminated.
   *
   * @param {Object} config - Scenario overrides
   * @returns {Promise<ScenarioMetrics>}
   */
  async runCascadingFailureScenario(config = {}) {
    const cfg = { ...this.scenarioDefaults, ...config };
    this.logger.info(`[CASCADING_FAILURE] Starting: isolation test for all handlers`);

    const scenarioMetrics = {
      scenarioName: 'cascading',
      handlerCount: this.handlers.size,
      totalRequests: 0,
      successCount: 0,
      errorCount: 0,
      errorRate: 0,
      durationMs: 0,
      throughput: 0,
      latencyPercentiles: {},
      memoryStats: {},
      handlerResults: new Map(),
      rawMetrics: [],
      isolationResults: {},
    };

    const startTime = Date.now();
    const allMetrics = [];

    // Identify a handler to fail and others to test
    const handlers = Array.from(this.handlers.entries());
    if (handlers.length < 2) {
      this.logger.warn(
        '[CASCADING_FAILURE] Skipped: need at least 2 handlers for isolation test'
      );
      return scenarioMetrics;
    }

    const [failingHandler, failingHandlerFn] = handlers[0];
    const otherHandlers = handlers.slice(1);

    // Phase 1: Run all handlers normally (baseline)
    this.logger.debug('[CASCADING_FAILURE] Phase 1: Baseline');
    for (const [messageType, handler] of handlers) {
      const phaseMetrics = await this._runConcurrentHandler(
        messageType,
        handler,
        { ...cfg, concurrencyLevel: 10, requestsPerHandler: 50 }
      );
      scenarioMetrics.handlerResults.set(messageType, {
        baselineSuccess: phaseMetrics.filter((m) => m.success).length,
        baselineTotal: phaseMetrics.length,
      });
      allMetrics.push(...phaseMetrics);
    }

    // Phase 2: Inject failure into one handler, run all
    this.logger.debug('[CASCADING_FAILURE] Phase 2: Inject failure');
    for (const [messageType, handler] of handlers) {
      const isFailingHandler = messageType === failingHandler;
      const phaseMetrics = await this._runConcurrentHandler(
        messageType,
        isFailingHandler ? this.errorInjector.createFailingHandler(handler) : handler,
        { ...cfg, concurrencyLevel: 10, requestsPerHandler: 50 }
      );
      const results = scenarioMetrics.handlerResults.get(messageType);
      if (isFailingHandler) {
        results.failurePhaseSuccess = phaseMetrics.filter((m) => m.success).length;
        results.failurePhaseTotal = phaseMetrics.length;
        results.isolated = true; // Marked as intentionally failing
      } else {
        results.failurePhaseSuccess = phaseMetrics.filter((m) => m.success).length;
        results.failurePhaseTotal = phaseMetrics.length;
        results.isolated = results.failurePhaseSuccess === results.failurePhaseTotal; // Unaffected?
      }
      allMetrics.push(...phaseMetrics);
    }

    scenarioMetrics.durationMs = Date.now() - startTime;
    scenarioMetrics.totalRequests = allMetrics.length;
    scenarioMetrics.successCount = allMetrics.filter((m) => m.success).length;
    scenarioMetrics.errorCount = scenarioMetrics.totalRequests - scenarioMetrics.successCount;
    scenarioMetrics.errorRate = (scenarioMetrics.errorCount / scenarioMetrics.totalRequests) * 100;
    scenarioMetrics.throughput = (scenarioMetrics.totalRequests / scenarioMetrics.durationMs) * 1000;

    scenarioMetrics.latencyPercentiles = this._calculatePercentiles(
      allMetrics.map((m) => m.latencyMs)
    );

    // Isolation analysis
    let isolatedCount = 0;
    scenarioMetrics.handlerResults.forEach((results, messageType) => {
      if (results.isolated) isolatedCount++;
    });
    scenarioMetrics.isolationResults = {
      totalHandlers: this.handlers.size,
      fullyIsolated: isolatedCount - 1, // Exclude intentionally failing handler
      isolationRate: ((isolatedCount - 1) / (this.handlers.size - 1)) * 100,
    };

    if (cfg.captureRawMetrics) {
      scenarioMetrics.rawMetrics = allMetrics;
    }

    this.logger.info(
      `[CASCADING_FAILURE] Complete: isolation_rate=${scenarioMetrics.isolationResults.isolationRate.toFixed(2)}%`
    );

    return scenarioMetrics;
  }

  // ========== INTERNAL HELPERS ==========

  /**
   * Run a single handler with concurrent requests.
   * @private
   */
  async _runConcurrentHandler(messageType, handler, config) {
    const metrics = [];
    const concurrencyLevel = config.concurrencyLevel || 50;
    const requestsPerHandler = config.requestsPerHandler || 500;
    const batchSize = concurrencyLevel;
    const totalBatches = Math.ceil(requestsPerHandler / batchSize);

    for (let batch = 0; batch < totalBatches; batch++) {
      const remaining = requestsPerHandler - batch * batchSize;
      const batchRequests = Math.min(batchSize, remaining);
      const promises = [];

      for (let i = 0; i < batchRequests; i++) {
        promises.push(
          this._executeHandlerOnce(messageType, handler, config, config.measureMemory)
        );
      }

      const batchMetrics = await Promise.all(promises);
      metrics.push(...batchMetrics);
    }

    return metrics;
  }

  /**
   * Run a single handler with error injection.
   * @private
   */
  async _runErrorInjectedHandler(messageType, handler, config) {
    const metrics = [];
    const requestsPerHandler = config.requestsPerHandler || 500;
    const injectionRate = config.errorInjection.injectionRate || 0.05;

    for (let i = 0; i < requestsPerHandler; i++) {
      const shouldInject = Math.random() < injectionRate;
      const finalHandler = shouldInject
        ? this.errorInjector.injectError(handler, config.errorInjection.scenarios)
        : handler;

      const metrics_ = await this._executeHandlerOnce(messageType, finalHandler, config, config.measureMemory);
      metrics.push(metrics_);
    }

    return metrics;
  }

  /**
   * Execute a handler once and capture metrics.
   * @private
   */
  async _executeHandlerOnce(messageType, handler, config, measureMemory) {
    const memBefore = measureMemory ? this._getMemoryUsage() : 0;
    const startTime = performance.now();
    let success = false;
    let errorType = null;
    let errorMessage = null;

    try {
      const result = await handler(
        { messageType, messageId: `stress-${Date.now()}-${Math.random()}`, data: {} },
        { logger: this.logger, metrics: this.metrics }
      );
      success = result.success === true;
      if (!success) {
        errorMessage = result.error || 'Unknown error';
        errorType = 'handler_error';
      }
    } catch (err) {
      success = false;
      errorType = err.name || 'exception';
      errorMessage = err.message;
    }

    const endTime = performance.now();
    const memAfter = measureMemory ? this._getMemoryUsage() : 0;

    return {
      startTime,
      endTime,
      latencyMs: endTime - startTime,
      success,
      errorType,
      errorMessage,
      memoryBefore: memBefore,
      memoryAfter: memAfter,
      memoryDelta: memAfter - memBefore,
    };
  }

  /**
   * Calculate latency percentiles from array of latencies (ms).
   * @private
   */
  _calculatePercentiles(latencies) {
    if (!latencies || latencies.length === 0) {
      return { p50: 0, p95: 0, p99: 0, max: 0 };
    }

    const sorted = latencies.slice().sort((a, b) => a - b);
    const len = sorted.length;

    return {
      p50: sorted[Math.floor(len * 0.5)],
      p95: sorted[Math.floor(len * 0.95)],
      p99: sorted[Math.floor(len * 0.99)],
      max: sorted[len - 1],
    };
  }

  /**
   * Calculate memory statistics from execution metrics.
   * @private
   */
  _calculateMemoryStats(metrics) {
    if (!metrics || metrics.length === 0) {
      return { minDelta: 0, maxDelta: 0, avgDelta: 0, peak: 0 };
    }

    const deltas = metrics.map((m) => m.memoryDelta);
    const min = Math.min(...deltas);
    const max = Math.max(...deltas);
    const avg = deltas.reduce((a, b) => a + b, 0) / deltas.length;

    return {
      minDelta: min,
      maxDelta: max,
      avgDelta: avg,
      peak: Math.max(...metrics.map((m) => m.memoryAfter)),
    };
  }

  /**
   * Get current memory usage in bytes.
   * @private
   */
  _getMemoryUsage() {
    if (typeof process !== 'undefined' && process.memoryUsage) {
      return process.memoryUsage().heapUsed;
    }
    return 0;
  }

  /**
   * Create mock logger.
   * @private
   */
  _createMockLogger() {
    return {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {},
    };
  }

  /**
   * Create mock metrics collector.
   * @private
   */
  _createMockMetrics() {
    return {
      record: () => {},
      increment: () => {},
    };
  }
}

/**
 * Error Injector
 *
 * Simulates failures (timeouts, protocol errors, missing deps) for testing.
 */
class ErrorInjector {
  constructor(logger = null) {
    this.logger = logger || {};
  }

  /**
   * Inject a random error into a handler.
   *
   * @param {Function} handler - Original handler
   * @param {string[]} scenarios - Error scenarios to inject
   * @returns {Function} Handler that may fail
   */
  injectError(handler, scenarios) {
    const scenario = scenarios[Math.floor(Math.random() * scenarios.length)];

    if (scenario === 'timeout') {
      return this.createTimeoutHandler(handler);
    } else if (scenario === 'protocol_error') {
      return this.createProtocolErrorHandler(handler);
    } else if (scenario === 'missing_dependency') {
      return this.createMissingDependencyHandler(handler);
    }

    return handler;
  }

  /**
   * Wrap handler to timeout after delay.
   * @private
   */
  createTimeoutHandler(handler, delayMs = 50) {
    return async (message, context) => {
      return Promise.race([
        handler(message, context),
        new Promise((_, reject) =>
          setTimeout(
            () => reject(new Error('Injected timeout error')),
            delayMs
          )
        ),
      ]).catch((err) => ({
        success: false,
        error: `Timeout: ${err.message}`,
      }));
    };
  }

  /**
   * Wrap handler to return malformed response.
   * @private
   */
  createProtocolErrorHandler(handler) {
    return async (message, context) => {
      try {
        const result = await handler(message, context);
        // Corrupt the response
        return { success: undefined, malformed: true };
      } catch (err) {
        return { success: false, error: 'Protocol error' };
      }
    };
  }

  /**
   * Wrap handler to simulate missing dependency.
   * @private
   */
  createMissingDependencyHandler(handler) {
    return async (message, context) => {
      // Simulate missing service/dependency
      return {
        success: false,
        error: 'Missing dependency: required service unavailable',
      };
    };
  }

  /**
   * Create a handler that always fails.
   * @private
   */
  createFailingHandler(handler) {
    return async (message, context) => ({
      success: false,
      error: 'Intentionally failing handler',
    });
  }
}

/**
 * Factory function to create a stress test engine.
 *
 * @param {Object} config - Configuration
 * @param {Map<string, Function>} config.handlers - Handler registry
 * @param {Object} config.logger - Logger instance
 * @param {Object} config.metrics - Metrics collector
 * @param {StressScenarioConfig} config.scenarioDefaults - Default configuration
 * @returns {StressTestEngine}
 */
export function createStressTestEngine(config) {
  return new StressTestEngine(config);
}

export { ErrorInjector };
