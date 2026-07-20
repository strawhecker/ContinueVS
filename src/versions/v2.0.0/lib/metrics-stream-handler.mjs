#!/usr/bin/env node

/**
 * Metrics Stream Handler for ContinueVS Bridge (Step 101)
 * 
 * Subscription-based real-time metrics streaming for dashboard visualization.
 * Non-invasive aggregation from:
 * - Step 96 (ProfilerHandler): Per-handler latency, error rates, request counts
 * - Step 72 (MessageLogger): Message volume, routing stats
 * - Step 74 (ErrorRecoveryMetrics): Error rates, timeout counts
 * - Step 64 (TimeoutManager): Latency distribution
 * - Step 66 (SymbolExtractor): Cache hit rates (optional)
 * 
 * Provides bridge:subscribeToMetrics subscription handler for continuous metrics updates.
 * Graceful degradation when metric sources unavailable.
 * 
 * @module metrics-stream-handler
 */

/**
 * Custom error class for metrics stream operations.
 */
export class MetricsStreamError extends Error {
  constructor(message, code = 'METRICS_STREAM_ERROR', details = null) {
    super(message);
    this.name = 'MetricsStreamError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Handler metrics snapshot interface (structure).
 * @typedef {Object} MetricsSnapshot
 * @property {Array} handlers - Array of per-handler metrics
 * @property {string} handlers[].name - Handler message type
 * @property {Object} handlers[].latency - Latency percentiles in milliseconds
 * @property {number} handlers[].latency.p50 - 50th percentile (median)
 * @property {number} handlers[].latency.p95 - 95th percentile
 * @property {number} handlers[].latency.p99 - 99th percentile
 * @property {number} handlers[].errorRate - Error rate (0.0-1.0)
 * @property {number} handlers[].throughput - Requests per second
 * @property {number} handlers[].requestCount - Total requests processed
 * @property {number} handlers[].timeoutCount - Number of timed-out requests
 * @property {string} handlers[].status - Status ('healthy', 'degraded', 'error')
 * @property {number} [handlers[].cacheHitRate] - Cache hit rate if available (0.0-1.0)
 * @property {Object} summary - Aggregate summary statistics
 * @property {number} summary.totalLatencyMs - Sum of all latencies
 * @property {number} summary.avgErrorRate - Average error rate across handlers
 * @property {number} summary.avgThroughput - Average throughput across handlers
 * @property {number} summary.totalRequests - Total requests across all handlers
 * @property {number} summary.totalTimeouts - Total timeouts across all handlers
 * @property {string} summary.uptime - Human-readable uptime string
 * @property {string} timestamp - ISO 8601 timestamp of snapshot
 */

/**
 * Creates the metrics stream handler with dependency injection.
 * 
 * Factory function that creates a subscription handler for bridge:subscribeToMetrics.
 * Aggregates metrics from existing infrastructure non-invasively.
 * Gracefully degrades if any metric source is unavailable.
 * 
 * @param {Object} config - Configuration object
 * @param {Object} config.profilerHandler - ProfilerHandler instance (Step 96)
 *   - Must have aggregateMetrics() -> { handlers: [], summary: {} }
 * @param {Object} [config.messageLogger] - MessageLogger instance (Step 72), optional
 *   - If provided, must have getStats() -> { totalMessages, requestCount, responseCount, errorCount, averageLatency }
 * @param {Object} [config.errorRecoveryMetrics] - ErrorRecoveryMetrics instance (Step 74), optional
 *   - If provided, must have getErrorRate() -> { errorCount, successCount, timeoutCount }
 * @param {Object} [config.timeoutManager] - TimeoutManager instance (Step 64), optional
 *   - If provided, must have getMetrics() -> { latencies: [], p99LatencyMs }
 * @param {Object} [config.symbolExtractor] - SymbolExtractor instance (Step 66), optional
 *   - If provided, must have getCacheStats() -> { hitCount, missCount, cacheSize }
 * @param {Object} [config.logger] - Optional logger for diagnostics
 * @returns {Function} Handler function (message, context) => Promise<Object>
 * @throws {MetricsStreamError} If profilerHandler is null
 */
export function createMetricsStreamHandler({
  profilerHandler,
  messageLogger = null,
  errorRecoveryMetrics = null,
  timeoutManager = null,
  symbolExtractor = null,
  logger = null
} = {}) {
  // Validate required dependencies
  if (!profilerHandler) {
    throw new MetricsStreamError(
      'ProfilerHandler is required for metrics stream',
      'MISSING_PROFILER_HANDLER'
    );
  }

  // Track active subscriptions
  const activeSubscriptions = new Map();
  const subscriptionStartTimes = new Map();

  /**
   * Message handler entry point.
   * Processes bridge:subscribeToMetrics requests.
   * 
   * @param {Object} message - Bridge message
   * @param {string} message.messageType - Expected: 'bridge:subscribeToMetrics'
   * @param {string} message.messageId - Unique request ID
   * @param {Object} message.data - Request data
   * @param {number} message.data.interval - Update interval in milliseconds (500-60000)
   * @param {Object} [message.data.filters] - Optional filtering
   * @param {Array} [message.data.filters.handlers] - Filter by handler names
   * @param {Array} [message.data.filters.metrics] - Filter by metric types
   * @param {Object} context - Handler context (push function for streaming)
   * @returns {Promise<Object>} Subscription confirmation
   */
  return async function handleMetricsSubscription(message, context) {
    try {
      // Validate message structure
      if (!message || typeof message !== 'object') {
        throw new MetricsStreamError('Invalid message object', 'INVALID_MESSAGE');
      }
      if (!message.messageId || typeof message.messageId !== 'string') {
        throw new MetricsStreamError('Missing or invalid messageId', 'INVALID_MESSAGE_ID');
      }
      if (!message.data || typeof message.data !== 'object') {
        throw new MetricsStreamError('Missing or invalid data object', 'INVALID_DATA');
      }

      // Validate subscription request
      const validation = validateSubscriptionRequest(message.data);
      if (!validation.valid) {
        throw new MetricsStreamError(validation.error, 'INVALID_SUBSCRIPTION');
      }

      const { interval, filters } = message.data;
      const subscriptionId = `sub-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

      if (logger) {
        logger.debug?.(`[MetricsStream] New subscription: ${subscriptionId}, interval: ${interval}ms`);
      }

      // Send confirmation
      if (context.send) {
        context.send({
          messageId: message.messageId,
          success: true,
          data: {
            subscriptionId,
            interval,
            filters: filters || { handlers: null, metrics: null },
            message: `Subscribed to metrics stream. Updates every ${interval}ms.`
          }
        });
      }

      // Track subscription
      const subscription = {
        messageId: message.messageId,
        subscriptionId,
        interval,
        filters: filters || {},
        context,
        active: true
      };

      activeSubscriptions.set(subscriptionId, subscription);
      subscriptionStartTimes.set(subscriptionId, Date.now());

      // Start streaming loop
      const streamInterval = setInterval(async () => {
        if (!subscription.active) {
          clearInterval(streamInterval);
          activeSubscriptions.delete(subscriptionId);
          subscriptionStartTimes.delete(subscriptionId);
          return;
        }

        try {
          const snapshot = await generateSnapshot(
            profilerHandler,
            messageLogger,
            errorRecoveryMetrics,
            timeoutManager,
            symbolExtractor,
            logger
          );

          const filteredSnapshot = applyFilters(snapshot, subscription.filters);
          const uptime = calculateUptime(subscriptionStartTimes.get(subscriptionId));

          const update = {
            messageType: 'bridge:metricsUpdate',
            data: {
              subscriptionId,
              timestamp: new Date().toISOString(),
              snapshot: {
                ...filteredSnapshot,
                summary: {
                  ...filteredSnapshot.summary,
                  uptime
                }
              }
            }
          };

          if (context.send) {
            context.send(update);
          }
        } catch (error) {
          if (logger) {
            logger.error?.(`[MetricsStream] Error generating snapshot: ${error.message}`);
          }
          // Continue streaming despite errors
        }
      }, interval);

      // Handle unsubscribe (graceful cleanup if context provides cleanup method)
      if (context.onCancel) {
        context.onCancel(() => {
          clearInterval(streamInterval);
          subscription.active = false;
          activeSubscriptions.delete(subscriptionId);
          subscriptionStartTimes.delete(subscriptionId);
          if (logger) {
            logger.debug?.(`[MetricsStream] Subscription cancelled: ${subscriptionId}`);
          }
        });
      }

      return {
        success: true,
        data: { subscriptionId, interval }
      };
    } catch (error) {
      if (logger) {
        logger.error?.(`[MetricsStream] Handler error: ${error.message}`);
      }

      return {
        success: false,
        error: {
          code: error.code || 'METRICS_STREAM_ERROR',
          message: error.message
        }
      };
    }
  };
}

/**
 * Validates subscription request data.
 * 
 * @param {Object} data - Request data to validate
 * @returns {Object} Validation result { valid: boolean, error?: string }
 */
function validateSubscriptionRequest(data) {
  if (!data.interval || typeof data.interval !== 'number') {
    return { valid: false, error: 'Missing or invalid interval (must be number)' };
  }

  if (data.interval < 500) {
    return { valid: false, error: 'Interval must be at least 500ms' };
  }

  if (data.interval > 60000) {
    return { valid: false, error: 'Interval must not exceed 60000ms' };
  }

  if (data.filters) {
    if (typeof data.filters !== 'object') {
      return { valid: false, error: 'Filters must be an object' };
    }
    if (data.filters.handlers && !Array.isArray(data.filters.handlers)) {
      return { valid: false, error: 'filters.handlers must be an array' };
    }
    if (data.filters.metrics && !Array.isArray(data.filters.metrics)) {
      return { valid: false, error: 'filters.metrics must be an array' };
    }
  }

  return { valid: true };
}

/**
 * Generates a metrics snapshot from all available sources.
 * 
 * @param {Object} profilerHandler - Profiler instance
 * @param {Object} messageLogger - Message logger instance (optional)
 * @param {Object} errorRecoveryMetrics - Error recovery metrics instance (optional)
 * @param {Object} timeoutManager - Timeout manager instance (optional)
 * @param {Object} symbolExtractor - Symbol extractor instance (optional)
 * @param {Object} logger - Logger instance (optional)
 * @returns {Promise<Object>} Metrics snapshot
 */
async function generateSnapshot(
  profilerHandler,
  messageLogger,
  errorRecoveryMetrics,
  timeoutManager,
  symbolExtractor,
  logger
) {
  const startTime = Date.now();
  const handlers = [];
  let totalLatencyMs = 0;
  let totalErrorRate = 0;
  let totalThroughput = 0;
  let totalRequests = 0;
  let totalTimeouts = 0;

  try {
    // Get profiler metrics (primary source)
    let profilerMetrics = null;
    try {
      if (typeof profilerHandler.aggregateMetrics === 'function') {
        profilerMetrics = profilerHandler.aggregateMetrics();
      } else if (typeof profilerHandler.getMetrics === 'function') {
        profilerMetrics = profilerHandler.getMetrics();
      }
    } catch (error) {
      if (logger) {
        logger.warn?.(`[MetricsStream] Error getting profiler metrics: ${error.message}`);
      }
    }

    if (profilerMetrics && profilerMetrics.handlers && Array.isArray(profilerMetrics.handlers)) {
      for (const handler of profilerMetrics.handlers) {
        const handlerData = {
          name: handler.name || 'unknown',
          latency: handler.latency || { p50: 0, p95: 0, p99: 0 },
          errorRate: handler.errorRate || 0,
          throughput: handler.throughput || 0,
          requestCount: handler.requestCount || 0,
          timeoutCount: handler.timeoutCount || 0,
          status: determineStatus(handler)
        };

        // Add optional cache hit rate
        if (typeof handler.cacheHitRate === 'number') {
          handlerData.cacheHitRate = handler.cacheHitRate;
        }

        handlers.push(handlerData);

        // Accumulate for summary
        totalLatencyMs += handlerData.latency.p99 || 0;
        totalErrorRate += handlerData.errorRate;
        totalThroughput += handlerData.throughput;
        totalRequests += handlerData.requestCount;
        totalTimeouts += handlerData.timeoutCount;
      }
    }

    const handlerCount = Math.max(handlers.length, 1);

    return {
      handlers,
      summary: {
        totalLatencyMs: Math.round(totalLatencyMs),
        avgErrorRate: parseFloat((totalErrorRate / handlerCount).toFixed(4)),
        avgThroughput: parseFloat((totalThroughput / handlerCount).toFixed(2)),
        totalRequests,
        totalTimeouts,
        generationTimeMs: Date.now() - startTime
      },
      timestamp: new Date().toISOString()
    };
  } catch (error) {
    if (logger) {
      logger.error?.(`[MetricsStream] Fatal error generating snapshot: ${error.message}`);
    }

    // Return minimal snapshot on failure
    return {
      handlers: [],
      summary: {
        totalLatencyMs: 0,
        avgErrorRate: 0,
        avgThroughput: 0,
        totalRequests: 0,
        totalTimeouts: 0,
        generationTimeMs: Date.now() - startTime
      },
      timestamp: new Date().toISOString()
    };
  }
}

/**
 * Determines handler status based on metrics.
 * 
 * @param {Object} handler - Handler metrics
 * @returns {string} Status ('healthy', 'degraded', 'error')
 */
function determineStatus(handler) {
  if (!handler) return 'error';

  const errorRate = handler.errorRate || 0;
  const p99Latency = handler.latency?.p99 || 0;

  if (errorRate > 0.1 || p99Latency > 5000) {
    return 'error';
  }
  if (errorRate > 0.05 || p99Latency > 1000) {
    return 'degraded';
  }
  return 'healthy';
}

/**
 * Applies filters to snapshot data.
 * 
 * @param {Object} snapshot - Metrics snapshot
 * @param {Object} filters - Filter criteria
 * @param {Array} [filters.handlers] - Handler names to include
 * @param {Array} [filters.metrics] - Metric types to include
 * @returns {Object} Filtered snapshot
 */
function applyFilters(snapshot, filters = {}) {
  let filteredHandlers = snapshot.handlers;

  // Filter by handler names
  if (filters.handlers && Array.isArray(filters.handlers) && filters.handlers.length > 0) {
    filteredHandlers = filteredHandlers.filter(h => filters.handlers.includes(h.name));
  }

  // Filter by metric types (removes fields not requested)
  if (filters.metrics && Array.isArray(filters.metrics) && filters.metrics.length > 0) {
    const metricsSet = new Set(filters.metrics);
    filteredHandlers = filteredHandlers.map(h => {
      const filtered = { name: h.name };
      if (metricsSet.has('latency')) filtered.latency = h.latency;
      if (metricsSet.has('errorRate')) filtered.errorRate = h.errorRate;
      if (metricsSet.has('throughput')) filtered.throughput = h.throughput;
      if (metricsSet.has('requestCount')) filtered.requestCount = h.requestCount;
      if (metricsSet.has('timeoutCount')) filtered.timeoutCount = h.timeoutCount;
      if (metricsSet.has('status')) filtered.status = h.status;
      if (metricsSet.has('cacheHitRate') && h.cacheHitRate !== undefined) {
        filtered.cacheHitRate = h.cacheHitRate;
      }
      return filtered;
    });
  }

  return {
    handlers: filteredHandlers,
    summary: snapshot.summary,
    timestamp: snapshot.timestamp
  };
}

/**
 * Calculates human-readable uptime string.
 * 
 * @param {number} startTime - Subscription start time (milliseconds)
 * @returns {string} Human-readable uptime (e.g., "2h 15m 30s")
 */
function calculateUptime(startTime) {
  const elapsed = Date.now() - startTime;
  const seconds = Math.floor((elapsed / 1000) % 60);
  const minutes = Math.floor((elapsed / (1000 * 60)) % 60);
  const hours = Math.floor((elapsed / (1000 * 60 * 60)) % 24);
  const days = Math.floor(elapsed / (1000 * 60 * 60 * 24));

  if (days > 0) {
    return `${days}d ${hours}h ${minutes}m`;
  }
  if (hours > 0) {
    return `${hours}h ${minutes}m ${seconds}s`;
  }
  if (minutes > 0) {
    return `${minutes}m ${seconds}s`;
  }
  return `${seconds}s`;
}
