/**
 * Profiler Integration Handler for ContinueVS Bridge (Step 96)
 * 
 * Non-invasive aggregation of real-time metrics from existing infrastructure:
 * - TimeoutManager (Step 64): Latency percentiles, timeout tracking
 * - MessageLogger (Step 72): Message volume, routing stats
 * - ErrorRecoveryMetrics (Step 74): Error rates, recovery stats
 * - SymbolExtractor (Step 66): Cache hit rates
 * 
 * Provides bridge:getProfilerData handler for health diagnostics and compliance testing.
 * Real-time snapshots only (no persistent history; Step 101 handles dashboard).
 * Graceful degradation for missing metrics collectors.
 * 
 * @module profiler-integration
 */

/**
 * Custom error class for profiler operations.
 */
export class ProfilerError extends Error {
  constructor(message, code = 'PROFILER_ERROR', details = null) {
    super(message);
    this.name = 'ProfilerError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Handler metrics interface (structure).
 * @typedef {Object} HandlerMetrics
 * @property {string} name - Handler message type
 * @property {Object} latency - Latency percentiles in milliseconds
 * @property {number} latency.p50 - 50th percentile (median)
 * @property {number} latency.p95 - 95th percentile
 * @property {number} latency.p99 - 99th percentile
 * @property {number} errorRate - Error rate (0.0-1.0)
 * @property {number} requestCount - Total requests processed
 * @property {number} [cacheHitRate] - Cache hit rate if available (0.0-1.0)
 * @property {number} timeoutCount - Number of timed-out requests
 */

/**
 * Profiler report interface (structure).
 * @typedef {Object} ProfilerReport
 * @property {HandlerMetrics[]} handlers - Array of per-handler metrics
 * @property {string} timestamp - ISO 8601 timestamp of report generation
 * @property {Object} summary - Aggregate summary statistics
 * @property {string} summary.slowestHandler - Handler with highest p99 latency
 * @property {number} summary.maxP99 - Highest p99 latency value
 * @property {string} summary.highestErrorRate - Handler with highest error rate
 * @property {number} summary.maxErrorRate - Highest error rate value
 * @property {number} summary.totalRequests - Total requests across all handlers
 * @property {number} summary.totalTimeouts - Total timeouts across all handlers
 * @property {number} summary.generationTimeMs - Milliseconds to generate this report
 */

/**
 * Creates the profiler handler with dependency injection.
 * 
 * Factory function that creates a message handler for bridge:getProfilerData.
 * Aggregates metrics from existing infrastructure non-invasively.
 * Gracefully degrades if any metric source is unavailable.
 * 
 * @param {Object} timeoutManager - TimeoutManager instance (Step 64)
 *   - Must have getMetrics() -> { pendingRequests, completedRequests, totalTimeouts, latencies: [], averageLatencyMs, p99LatencyMs }
 * @param {Object} messageLogger - MessageLogger instance (Step 72)
 *   - Must have getStats() -> { totalMessages, requestCount, responseCount, errorCount, averageLatency }
 * @param {Object} errorRecoveryMetrics - ErrorRecoveryMetrics instance (Step 74)
 *   - Must have getErrorRate() -> { errorCount, successCount, timeoutCount }
 * @param {Object} symbolExtractor - SymbolExtractor instance (Step 66), optional
 *   - If provided, must have getCacheStats() -> { hitCount, missCount, cacheSize }
 * @param {Object} [logger] - Optional logger for diagnostics
 * @param {Object} [metrics] - Optional metrics collector
 * @returns {Function} Handler function (message, context) => Promise<Object>
 * @throws {ProfilerError} If required parameters (timeoutManager, messageLogger, errorRecoveryMetrics) are null
 */
export function createProfilerHandler(
  timeoutManager,
  messageLogger,
  errorRecoveryMetrics,
  symbolExtractor,
  logger = null,
  metrics = null
) {
  // Validate required dependencies
  if (!timeoutManager) {
    throw new ProfilerError(
      'TimeoutManager is required for profiler',
      'MISSING_TIMEOUT_MANAGER'
    );
  }
  if (!messageLogger) {
    throw new ProfilerError(
      'MessageLogger is required for profiler',
      'MISSING_MESSAGE_LOGGER'
    );
  }
  if (!errorRecoveryMetrics) {
    throw new ProfilerError(
      'ErrorRecoveryMetrics is required for profiler',
      'MISSING_ERROR_RECOVERY_METRICS'
    );
  }

  /**
   * Message handler entry point.
   * Processes bridge:getProfilerData requests.
   * 
   * @param {Object} message - Bridge message
   * @param {string} message.messageType - Expected: 'bridge:getProfilerData'
   * @param {string} message.messageId - Unique request ID
   * @param {Object} [message.data] - Request data (unused for profiler)
   * @param {Object} context - Handler context
   * @returns {Promise<Object>} Profiler report with handler metrics
   */
  return async function handleProfilerRequest(message, context) {
    const startTime = Date.now();

    try {
      // Validate message structure
      if (!message || typeof message !== 'object') {
        throw new ProfilerError('Invalid message object', 'INVALID_MESSAGE');
      }
      if (!message.messageId || typeof message.messageId !== 'string') {
        throw new ProfilerError('Missing or invalid messageId', 'INVALID_MESSAGE_ID');
      }

      // Log request
      if (logger) {
        logger.debug?.(`[Profiler] Processing request: ${message.messageId}`);
      }

      // Aggregate metrics from all sources
      const aggregatedMetrics = aggregateMetrics(
        timeoutManager,
        messageLogger,
        errorRecoveryMetrics,
        symbolExtractor,
        logger
      );

      // Build report
      const report = buildReport(aggregatedMetrics);
      const generationTime = Date.now() - startTime;
      report.summary.generationTimeMs = generationTime;

      // Validate performance gate
      if (generationTime > 20) {
        if (logger) {
          logger.warn?.(
            `[Profiler] Report generation exceeded 20ms gate: ${generationTime}ms`
          );
        }
      }

      // Record metrics
      if (metrics) {
        metrics.recordEvent?.('profiler_report_generated', {
          handlerCount: report.handlers.length,
          generationTimeMs: generationTime,
          totalRequests: report.summary.totalRequests,
        });
      }

      return {
        success: true,
        data: report,
        timestamp: new Date().toISOString(),
      };
    } catch (error) {
      // Log error
      if (logger) {
        logger.error?.(
          `[Profiler] Error generating report: ${error.message}`,
          error
        );
      }

      // Record error metric
      if (metrics) {
        metrics.recordEvent?.('profiler_error', {
          errorCode: error.code || 'UNKNOWN',
          message: error.message,
        });
      }

      // Return error response (JSON-RPC compliant)
      return {
        success: false,
        error: {
          code: -32603, // Internal error
          message: 'Failed to generate profiler report',
          data: {
            details: error.message,
            code: error.code,
          },
        },
      };
    }
  };
}

/**
 * Aggregates metrics from all available sources.
 * 
 * Non-invasive read-only aggregation. Gracefully handles missing data
 * from any metric source and returns best-effort results.
 * Throws if all metric sources fail to provide data.
 * 
 * @param {Object} timeoutManager - TimeoutManager instance
 * @param {Object} messageLogger - MessageLogger instance
 * @param {Object} errorRecoveryMetrics - ErrorRecoveryMetrics instance
 * @param {Object} symbolExtractor - SymbolExtractor instance (optional)
 * @param {Object} logger - Logger instance (optional)
 * @returns {Object} Aggregated metrics structure
 * @throws {ProfilerError} If all metric sources fail and no data available
 * @private
 */
function aggregateMetrics(
  timeoutManager,
  messageLogger,
  errorRecoveryMetrics,
  symbolExtractor,
  logger
) {
  const aggregated = {
    handlers: [],
    totalRequests: 0,
    totalTimeouts: 0,
    totalErrors: 0,
  };

  let sourcesFailed = [];

  try {
    // Extract TimeoutManager metrics
    let tmMetrics = {};
    if (timeoutManager && typeof timeoutManager.getMetrics === 'function') {
      try {
        tmMetrics = timeoutManager.getMetrics();
      } catch (error) {
        sourcesFailed.push('TimeoutManager');
        if (logger) {
          logger.warn?.(`[Profiler] Failed to get TimeoutManager metrics: ${error.message}`);
        }
      }
    }

    // Extract MessageLogger stats
    let msgStats = {};
    if (messageLogger && typeof messageLogger.getStats === 'function') {
      try {
        msgStats = messageLogger.getStats();
      } catch (error) {
        sourcesFailed.push('MessageLogger');
        if (logger) {
          logger.warn?.(`[Profiler] Failed to get MessageLogger stats: ${error.message}`);
        }
      }
    }

    // Extract ErrorRecoveryMetrics data
    let errMetrics = {};
    if (errorRecoveryMetrics && typeof errorRecoveryMetrics.getErrorRate === 'function') {
      try {
        errMetrics = errorRecoveryMetrics.getErrorRate();
      } catch (error) {
        sourcesFailed.push('ErrorRecoveryMetrics');
        if (logger) {
          logger.warn?.(`[Profiler] Failed to get ErrorRecoveryMetrics: ${error.message}`);
        }
      }
    }

    // If all sources failed, throw error
    if (sourcesFailed.length === 3) {
      throw new ProfilerError(
        `All metric sources failed: ${sourcesFailed.join(', ')}`,
        'ALL_SOURCES_FAILED',
        { failedSources: sourcesFailed }
      );
    }

    // Build per-handler metrics from aggregated data
    // Note: In a real integration, handlers would register their own metrics.
    // For Step 96 (optional), we provide a simplified aggregation model.
    if (tmMetrics.latencies && Array.isArray(tmMetrics.latencies)) {
      const percentiles = calculatePercentiles(tmMetrics.latencies);

      aggregated.handlers.push({
        name: 'aggregate',
        latency: {
          p50: percentiles.p50,
          p95: percentiles.p95,
          p99: percentiles.p99,
        },
        errorRate: errMetrics.errorCount
          ? errMetrics.errorCount / (errMetrics.errorCount + errMetrics.successCount || 1)
          : 0,
        requestCount: tmMetrics.completedRequests || msgStats.requestCount || 0,
        timeoutCount: errMetrics.timeoutCount || tmMetrics.totalTimeouts || 0,
      });

      aggregated.totalRequests = tmMetrics.completedRequests || 0;
      aggregated.totalTimeouts = tmMetrics.totalTimeouts || 0;
      aggregated.totalErrors = errMetrics.errorCount || 0;
    }

    // Add cache hit rate if SymbolExtractor available
    if (symbolExtractor && typeof symbolExtractor.getCacheStats === 'function') {
      try {
        const cacheStats = symbolExtractor.getCacheStats();
        if (aggregated.handlers.length > 0 && cacheStats) {
          const hitRate = cacheStats.hitCount
            ? cacheStats.hitCount / (cacheStats.hitCount + cacheStats.missCount || 1)
            : 0;
          aggregated.handlers[0].cacheHitRate = hitRate;
        }
      } catch (error) {
        if (logger) {
          logger.warn?.(`[Profiler] Failed to get cache stats: ${error.message}`);
        }
      }
    }
  } catch (error) {
    if (logger) {
      logger.error?.(`[Profiler] Aggregation failed: ${error.message}`, error);
    }
    throw error;
  }

  return aggregated;
}

/**
 * Calculates percentiles from a sorted or unsorted latency array.
 * 
 * Implements linear interpolation for non-integer indices.
 * Handles edge cases: empty array, single value, duplicates.
 * 
 * @param {number[]} latencies - Array of latency values in milliseconds
 * @returns {Object} Percentile object: { p50, p95, p99 }
 * @private
 */
function calculatePercentiles(latencies) {
  if (!latencies || latencies.length === 0) {
    return { p50: 0, p95: 0, p99: 0 };
  }

  // Sort in ascending order
  const sorted = [...latencies].sort((a, b) => a - b);
  const length = sorted.length;

  // Calculate index for each percentile (0-based, with interpolation)
  const getPercentile = (percentile) => {
    if (length === 1) return sorted[0];
    const index = (percentile / 100) * (length - 1);
    const lower = Math.floor(index);
    const upper = Math.ceil(index);
    const weight = index % 1;

    if (lower === upper) {
      return sorted[lower];
    }

    return sorted[lower] * (1 - weight) + sorted[upper] * weight;
  };

  return {
    p50: Math.round(getPercentile(50) * 100) / 100,
    p95: Math.round(getPercentile(95) * 100) / 100,
    p99: Math.round(getPercentile(99) * 100) / 100,
  };
}

/**
 * Builds a complete profiler report from aggregated metrics.
 * 
 * Includes summary statistics, handler-specific metrics, and metadata.
 * Graceful handling of empty or partial data.
 * 
 * @param {Object} aggregated - Aggregated metrics from aggregateMetrics()
 * @returns {ProfilerReport} Complete profiler report
 * @private
 */
function buildReport(aggregated) {
  const handlers = aggregated.handlers || [];

  // Compute summary statistics
  let slowestHandler = 'N/A';
  let maxP99 = 0;
  let highestErrorRate = 'N/A';
  let maxErrorRate = 0;

  handlers.forEach((handler) => {
    if (handler.latency?.p99 > maxP99) {
      maxP99 = handler.latency.p99;
      slowestHandler = handler.name;
    }
    if (handler.errorRate > maxErrorRate) {
      maxErrorRate = handler.errorRate;
      highestErrorRate = handler.name;
    }
  });

  return {
    handlers: handlers.map((h) => ({
      name: h.name,
      latency: h.latency || { p50: 0, p95: 0, p99: 0 },
      errorRate: h.errorRate || 0,
      requestCount: h.requestCount || 0,
      timeoutCount: h.timeoutCount || 0,
      ...(h.cacheHitRate !== undefined && { cacheHitRate: h.cacheHitRate }),
    })),
    timestamp: new Date().toISOString(),
    summary: {
      slowestHandler,
      maxP99,
      highestErrorRate,
      maxErrorRate: Math.round(maxErrorRate * 10000) / 10000,
      totalRequests: aggregated.totalRequests || 0,
      totalTimeouts: aggregated.totalTimeouts || 0,
      totalErrors: aggregated.totalErrors || 0,
    },
  };
}

export default createProfilerHandler;
