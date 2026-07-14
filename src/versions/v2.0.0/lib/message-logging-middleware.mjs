#!/usr/bin/env node

/**
 * Message Logging Middleware for Bridge Protocol
 *
 * Wraps the MiddlewareChain (Step 47) to capture inbound/outbound messages,
 * track latency per message type, and integrate with IBridgeLogger + IBridgeTelemetryCollector
 * for structured logging and metrics aggregation.
 *
 * @module src/versions/v2.0.0/lib/message-logging-middleware.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Architecture:
 *   WebView ─→ BridgeMessage ─→ MessageLoggingMiddleware ─→ MiddlewareChain ─→ Dispatcher
 *                              ↓
 *                         IBridgeLogger (log capture)
 *                         IBridgeTelemetryCollector (metrics)
 *
 * Related Steps:
 *   - Step 47: MiddlewareChain (foundation for hook registration)
 *   - Step 63: BridgeProtocolAdapter (message normalization)
 *   - Step 64: TimeoutManager (latency tracking foundation)
 *   - Step 71: HandlerDispatcher (uses logging middleware)
 *   - Step 73: Request/response validation (follows in chain)
 *   - Step 74: Error recovery middleware (follows in chain)
 */

/**
 * Error class for logging middleware failures.
 */
export class LoggingMiddlewareError extends Error {
  /**
   * @param {string} operationType - Operation that failed (e.g., 'inboundLogging', 'outboundLogging')
   * @param {string} message - Error message
   * @param {Error} [originalError] - Root cause exception
   */
  constructor(operationType, message, originalError = null) {
    super(`Logging middleware error [${operationType}]: ${message}`);
    this.name = 'LoggingMiddlewareError';
    this.operationType = operationType;
    this.originalError = originalError;
  }
}

/**
 * Message Logging Middleware.
 *
 * Wraps MiddlewareChain execution to capture inbound/outbound messages,
 * track latency per message type, and record metrics for performance monitoring.
 *
 * Features:
 *   - Inbound logging: messageType, messageId, timestamp, payload size
 *   - Outbound logging: response status, latency (ms), response size, handler name
 *   - Latency histogram: fast (<50ms), normal (50-500ms), slow (>500ms)
 *   - Metrics aggregation: total messages, error rate, average latency, p95/p99
 *   - Graceful degradation: no-op if logger/telemetry null
 */
export class MessageLoggingMiddleware {
  /**
   * @param {Object} config - Configuration
   * @param {*} config.middlewareChain - MiddlewareChain instance (from Step 47)
   * @param {*} [config.logger] - Logger instance (Step 25 IBridgeLogger or null)
   * @param {*} [config.metrics] - Metrics collector (Step 26 IBridgeTelemetryCollector or null)
   * @param {Object} [config.config] - Logging configuration
   * @param {boolean} [config.config.enableDetailedLogging=false] - Log full payloads
   * @param {boolean} [config.config.includePayloads=false] - Include message data in logs
   * @param {number} [config.config.sampleRate=1.0] - Log only N% of messages (0-1)
   * @param {number} [config.config.metricsWindow=1000] - Bounded latency history size
   */
  constructor({
    middlewareChain,
    logger = null,
    metrics = null,
    config = {},
  } = {}) {
    if (!middlewareChain) {
      throw new Error('middlewareChain is required');
    }

    this.middlewareChain = middlewareChain;
    this.logger = logger || this._createMockLogger();
    this.metrics = metrics || this._createMockMetrics();

    // Configuration
    this.config = {
      enableDetailedLogging: config.enableDetailedLogging ?? false,
      includePayloads: config.includePayloads ?? false,
      sampleRate: config.sampleRate ?? 1.0,
      metricsWindow: config.metricsWindow ?? 1000,
    };

    // Validate configuration
    if (this.config.sampleRate < 0 || this.config.sampleRate > 1) {
      throw new Error('sampleRate must be between 0 and 1');
    }

    // Initialize metrics state
    this.metricsState = {
      inbound: {
        total: 0,
        byType: {},
      },
      outbound: {
        successCount: 0,
        errorCount: 0,
        latencies: [], // Bounded array for p95/p99 calculation
      },
      latency: {
        fast: 0,      // < 50ms
        normal: 0,    // 50-500ms
        slow: 0,      // > 500ms
      },
      errors: {
        total: 0,
        byType: {},
      },
    };

    this.logger.debug('MessageLoggingMiddleware initialized', {
      sampleRate: this.config.sampleRate,
      metricsWindow: this.config.metricsWindow,
    });
  }

  /**
   * Create mock logger for graceful degradation.
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
   * Create mock metrics for graceful degradation.
   * @private
   */
  _createMockMetrics() {
    return {
      recordMetric: () => {},
      recordError: () => {},
    };
  }

  /**
   * Should this message be sampled for logging?
   * @private
   * @returns {boolean}
   */
  _shouldSampleMessage() {
    if (this.config.sampleRate >= 1.0) return true;
    return Math.random() < this.config.sampleRate;
  }

  /**
   * Log inbound message metadata.
   * @private
   */
  _logInbound(message) {
    try {
      if (!this._shouldSampleMessage()) return;

      const logData = {
        messageType: message?.messageType,
        messageId: message?.messageId,
        timestamp: new Date().toISOString(),
        payloadSize: JSON.stringify(message?.data || {}).length,
      };

      if (this.config.enableDetailedLogging && this.config.includePayloads) {
        logData.data = message?.data;
      }

      this.logger.info('Inbound message', logData);

      // Update inbound metrics
      this.metricsState.inbound.total += 1;
      const messageType = message?.messageType || 'unknown';
      this.metricsState.inbound.byType[messageType] =
        (this.metricsState.inbound.byType[messageType] || 0) + 1;

      // Record metric
      this.metrics.recordMetric?.('message.inbound', 1, {
        messageType,
      });
    } catch (err) {
      this.logger.error('Failed to log inbound message', {
        error: err.message,
      });
    }
  }

  /**
   * Log outbound message (response) with latency.
   * @private
   */
  _logOutbound(message, response, latencyMs, handlerName) {
    try {
      if (!this._shouldSampleMessage()) return;

      const isSuccess = response?.success === true;
      const logLevel = isSuccess ? 'info' : 'warn';

      const logData = {
        messageType: message?.messageType,
        messageId: message?.messageId,
        handlerName,
        status: isSuccess ? 'success' : 'error',
        latencyMs,
        responseSize: JSON.stringify(response || {}).length,
      };

      if (!isSuccess && response?.error) {
        logData.error = response.error;
      }

      if (this.config.enableDetailedLogging && this.config.includePayloads) {
        logData.response = response;
      }

      this.logger[logLevel]('Outbound message', logData);

      // Update outbound metrics
      if (isSuccess) {
        this.metricsState.outbound.successCount += 1;
      } else {
        this.metricsState.outbound.errorCount += 1;
      }

      // Track latency
      this.metricsState.outbound.latencies.push(latencyMs);
      if (this.metricsState.outbound.latencies.length > this.config.metricsWindow) {
        this.metricsState.outbound.latencies.shift();
      }

      // Categorize latency
      if (latencyMs < 50) {
        this.metricsState.latency.fast += 1;
      } else if (latencyMs <= 500) {
        this.metricsState.latency.normal += 1;
      } else {
        this.metricsState.latency.slow += 1;
      }

      // Record metrics
      this.metrics.recordMetric?.('message.outbound.latency', latencyMs, {
        messageType: message?.messageType,
        status: isSuccess ? 'success' : 'error',
      });

      this.metrics.recordMetric?.('message.outbound', 1, {
        messageType: message?.messageType,
        status: isSuccess ? 'success' : 'error',
      });
    } catch (err) {
      this.logger.error('Failed to log outbound message', {
        error: err.message,
      });
    }
  }

  /**
   * Log error from handler execution.
   * @private
   */
  _logError(message, error, handlerName) {
    try {
      const errorType = error?.constructor?.name || 'UnknownError';

      this.logger.error('Handler execution error', {
        messageType: message?.messageType,
        messageId: message?.messageId,
        handlerName,
        errorType,
        errorMessage: error?.message,
      });

      // Update error metrics
      this.metricsState.errors.total += 1;
      this.metricsState.errors.byType[errorType] =
        (this.metricsState.errors.byType[errorType] || 0) + 1;

      // Record metric
      this.metrics.recordMetric?.('message.error', 1, {
        messageType: message?.messageType,
        errorType,
      });
    } catch (err) {
      this.logger.error('Failed to log error', {
        error: err.message,
      });
    }
  }

  /**
   * Execute message through MiddlewareChain with logging.
   *
   * @param {Object} message - Message to process
   * @param {*} dispatcher - HandlerDispatcher instance
   * @param {Object} [context] - Execution context
   * @returns {Promise<Object>} DispatchResult with logging side-effects
   */
  async executeWithLogging(message, dispatcher, context = {}) {
    const startTime = performance.now();
    const handlerName = message?.messageType || 'unknown';

    // Log inbound
    this._logInbound(message);

    try {
      // Execute through middleware chain
      const result = await this.middlewareChain.execute(message, dispatcher, context);

      // Calculate latency
      const latencyMs = Math.round(performance.now() - startTime);

      // Log outbound success
      this._logOutbound(message, result?.response, latencyMs, handlerName);

      return result;
    } catch (error) {
      // Calculate latency
      const latencyMs = Math.round(performance.now() - startTime);

      // Log error
      this._logError(message, error, handlerName);

      // Record error latency
      this.metricsState.outbound.errorCount += 1;
      this.metricsState.outbound.latencies.push(latencyMs);
      if (this.metricsState.outbound.latencies.length > this.config.metricsWindow) {
        this.metricsState.outbound.latencies.shift();
      }

      // Re-throw to allow caller to handle
      throw error;
    }
  }

  /**
   * Calculate p-th percentile from latencies.
   * @private
   */
  _percentile(p) {
    const sorted = [...this.metricsState.outbound.latencies].sort((a, b) => a - b);
    if (sorted.length === 0) return 0;
    const idx = Math.ceil((p / 100) * sorted.length) - 1;
    return sorted[Math.max(0, idx)];
  }

  /**
   * Get aggregated metrics snapshot.
   *
   * @returns {Object} Metrics structure:
   *   {
   *     inbound: { total, byType },
   *     outbound: { successCount, errorCount, averageLatency, p95Latency, p99Latency },
   *     latency: { fast, normal, slow },
   *     errors: { total, byType },
   *     summary: { errorRate, avgLatencyCategory }
   *   }
   */
  getMetrics() {
    const avgLatency =
      this.metricsState.outbound.latencies.length > 0
        ? this.metricsState.outbound.latencies.reduce((a, b) => a + b, 0) /
          this.metricsState.outbound.latencies.length
        : 0;

    const totalOutbound =
      this.metricsState.outbound.successCount + this.metricsState.outbound.errorCount;
    const errorRate = totalOutbound > 0 ? this.metricsState.outbound.errorCount / totalOutbound : 0;

    let avgLatencyCategory = 'unknown';
    if (avgLatency < 50) avgLatencyCategory = 'fast';
    else if (avgLatency <= 500) avgLatencyCategory = 'normal';
    else avgLatencyCategory = 'slow';

    return {
      inbound: {
        total: this.metricsState.inbound.total,
        byType: { ...this.metricsState.inbound.byType },
      },
      outbound: {
        successCount: this.metricsState.outbound.successCount,
        errorCount: this.metricsState.outbound.errorCount,
        averageLatency: Math.round(avgLatency * 100) / 100,
        p95Latency: this._percentile(95),
        p99Latency: this._percentile(99),
      },
      latency: {
        fast: this.metricsState.latency.fast,
        normal: this.metricsState.latency.normal,
        slow: this.metricsState.latency.slow,
      },
      errors: {
        total: this.metricsState.errors.total,
        byType: { ...this.metricsState.errors.byType },
      },
      summary: {
        errorRate: Math.round(errorRate * 10000) / 100,
        avgLatencyCategory,
      },
    };
  }

  /**
   * Reset all metrics to initial state.
   */
  resetMetrics() {
    this.metricsState = {
      inbound: {
        total: 0,
        byType: {},
      },
      outbound: {
        successCount: 0,
        errorCount: 0,
        latencies: [],
      },
      latency: {
        fast: 0,
        normal: 0,
        slow: 0,
      },
      errors: {
        total: 0,
        byType: {},
      },
    };

    this.logger.debug('Metrics reset');
  }

  /**
   * Dispose and cleanup.
   */
  dispose() {
    // No-op for now; reserved for future cleanup
    this.logger.debug('MessageLoggingMiddleware disposed');
  }
}

/**
 * Factory function to create MessageLoggingMiddleware.
 *
 * @param {Object} config - Configuration (see MessageLoggingMiddleware constructor)
 * @returns {MessageLoggingMiddleware}
 */
export function createMessageLoggingMiddleware(config) {
  return new MessageLoggingMiddleware(config);
}
