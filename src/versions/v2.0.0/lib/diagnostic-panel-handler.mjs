#!/usr/bin/env node

/**
 * Diagnostic Panel Handler for ContinueVS Bridge (Step 102)
 *
 * On-demand health snapshot and diagnostics aggregation handler.
 * Complements Step 101 (metrics dashboard) by providing snapshot-based
 * diagnostics (vs. continuous streaming) for troubleshooting and monitoring.
 *
 * Aggregates:
 * - Bridge health status (from Step 24: HealthCheckService)
 * - Per-handler performance metrics (from Step 96: ProfilerHandler)
 * - Recent error queue (from Step 25: BridgeLogger)
 * - Handler tier breakdown and statistics
 *
 * Features:
 * - Graceful degradation when metric sources unavailable
 * - Structured diagnostic report with severity levels
 * - Error codes: -32602 (invalid request), -32603 (aggregation failure)
 * - Response format: { health, handlers, errors, summary, timestamp }
 *
 * **Message Type**: bridge:getDiagnosticPanel
 * **Input**: BridgeMessage with { operation: "get-all" | "filter-tier" | "filter-handler-name", filter?: string }
 * **Output**: BridgeResponse containing diagnostic snapshot
 *
 * @module diagnostic-panel-handler
 * @version 1.0.0
 */

/**
 * Custom error class for diagnostic panel operations.
 */
export class DiagnosticPanelError extends Error {
  constructor(message, code = 'DIAGNOSTIC_PANEL_ERROR', details = null) {
    super(message);
    this.name = 'DiagnosticPanelError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Diagnostic severity levels.
 */
export const SeverityLevel = {
  CRITICAL: 'CRITICAL',
  WARNING: 'WARNING',
  INFO: 'INFO'
};

/**
 * Determines severity based on latency percentile.
 * @param {number} p99LatencyMs - P99 latency in milliseconds
 * @returns {string} SeverityLevel
 */
function determineSeverityFromLatency(p99LatencyMs) {
  if (p99LatencyMs > 500) return SeverityLevel.CRITICAL;
  if (p99LatencyMs > 200) return SeverityLevel.WARNING;
  return SeverityLevel.INFO;
}

/**
 * Creates the diagnostic panel handler with dependency injection.
 *
 * Factory function that creates a handler for bridge:getDiagnosticPanel.
 * Aggregates diagnostics from existing infrastructure non-invasively.
 * Gracefully degrades if any metric source is unavailable.
 *
 * @param {Object} config - Configuration object
 * @param {Object} [config.profilerHandler] - ProfilerHandler instance (Step 96), optional
 *   - If provided, must have aggregateMetrics() -> { handlers: [], summary: {} }
 * @param {Object} [config.healthCheckService] - HealthCheckService instance (Step 24), optional
 *   - If provided, must have getCurrentHealthStatus() or similar
 * @param {Object} [config.bridgeLogger] - BridgeLogger instance (Step 25), optional
 *   - If provided, must have getRecentErrors() -> Array of error objects
 * @param {Object} [config.telemetryCollector] - IBridgeTelemetryCollector instance, optional
 * @param {Object} [config.logger] - Optional logger for diagnostics
 * @returns {Function} Handler function (message, context) => Promise<Object>
 * @throws {DiagnosticPanelError} If configuration is invalid
 */
export function createDiagnosticPanelHandler({
  profilerHandler = null,
  healthCheckService = null,
  bridgeLogger = null,
  telemetryCollector = null,
  logger = null
} = {}) {

  // Validate configuration (all sources are optional for graceful degradation)
  if (typeof logger?.debug === 'function') {
    logger.debug('DiagnosticPanelHandler: Initializing with optional dependencies', {
      hasProfiler: !!profilerHandler,
      hasHealthCheck: !!healthCheckService,
      hasLogger: !!bridgeLogger
    });
  }

  /**
   * Main handler function invoked by dispatcher.
   * Aggregates diagnostic data and returns structured snapshot.
   *
   * @param {Object} message - Bridge message
   * @param {string} message.messageType - Must be 'bridge:getDiagnosticPanel'
   * @param {string} message.messageId - Unique request ID
   * @param {Object} message.data - Request payload
   * @param {string} [message.data.operation] - "get-all" (default), "filter-tier", "filter-handler-name"
   * @param {string} [message.data.filter] - Filter value for tier or handler name
   * @param {Object} context - Handler context
   * @returns {Promise<Object>} Diagnostic snapshot response
   */
  return async function diagnosticPanelHandler(message, context) {
    const startTime = performance.now();

    try {
      // Validate message envelope
      if (!message || !message.data) {
        throw new DiagnosticPanelError(
          'Invalid message envelope: missing data field',
          'INVALID_MESSAGE',
          { messageId: message?.messageId }
        );
      }

      const { operation = 'get-all', filter = null } = message.data;

      // Validate operation
      const validOperations = ['get-all', 'filter-tier', 'filter-handler-name'];
      if (!validOperations.includes(operation)) {
        throw new DiagnosticPanelError(
          `Invalid operation: ${operation}. Must be one of: ${validOperations.join(', ')}`,
          'INVALID_OPERATION',
          { messageId: message.messageId, operation }
        );
      }

      // Collect diagnostic data
      const diagnostic = await aggregateDiagnostics(
        operation,
        filter,
        profilerHandler,
        healthCheckService,
        bridgeLogger,
        logger
      );

      const latencyMs = performance.now() - startTime;

      // Log successful retrieval
      if (logger?.debug) {
        logger.debug('DiagnosticPanelHandler: Diagnostics aggregated successfully', {
          messageId: message.messageId,
          handlerCount: diagnostic.handlers?.length || 0,
          errorCount: diagnostic.errors?.length || 0,
          latencyMs: latencyMs.toFixed(2)
        });
      }

      // Record telemetry
      if (telemetryCollector?.recordEvent) {
        telemetryCollector.recordEvent('bridge:getDiagnosticPanel', {
          success: true,
          operation,
          latencyMs: latencyMs.toFixed(2),
          handlerCount: diagnostic.handlers?.length || 0
        });
      }

      return {
        success: true,
        messageId: message.messageId,
        data: diagnostic
      };

    } catch (error) {
      const latencyMs = performance.now() - startTime;

      // Log error
      if (logger?.error) {
        logger.error('DiagnosticPanelHandler: Failed to retrieve diagnostics', {
          messageId: message?.messageId,
          error: error.message,
          code: error.code,
          latencyMs: latencyMs.toFixed(2)
        });
      }

      // Record telemetry
      if (telemetryCollector?.recordEvent) {
        telemetryCollector.recordEvent('bridge:getDiagnosticPanel', {
          success: false,
          error: error.message,
          latencyMs: latencyMs.toFixed(2)
        });
      }

      // Return JSON-RPC error response
      return {
        success: false,
        messageId: message?.messageId,
        error: {
          code: -32603, // Internal error (aggregation failure)
          message: error.message,
          details: error.details
        }
      };
    }
  };
}

/**
 * Aggregates diagnostic data from multiple sources.
 *
 * Retrieves health status, handler metrics, and error queue.
 * Gracefully handles missing sources by omitting their fields.
 *
 * @param {string} operation - Filter operation
 * @param {string|null} filter - Filter value
 * @param {Object} profilerHandler - ProfilerHandler instance (optional)
 * @param {Object} healthCheckService - HealthCheckService instance (optional)
 * @param {Object} bridgeLogger - BridgeLogger instance (optional)
 * @param {Object} logger - Logger instance (optional)
 * @returns {Promise<Object>} Aggregated diagnostic snapshot
 */
async function aggregateDiagnostics(
  operation,
  filter,
  profilerHandler,
  healthCheckService,
  bridgeLogger,
  logger
) {
  const timestamp = new Date().toISOString();

  // Collect health status
  let healthStatus = null;
  try {
    if (healthCheckService) {
      const status = healthCheckService.getCurrentHealthStatus?.() ||
                     healthCheckService.getHealthStatus?.();
      healthStatus = {
        status: status?.state || 'unknown',
        reason: status?.reason || '',
        timestamp: status?.timestamp || timestamp,
        uptime: status?.uptime || null,
        lastCheckTime: status?.lastCheckTime || timestamp
      };
    } else {
      healthStatus = {
        status: 'unknown',
        reason: 'HealthCheckService not available',
        timestamp,
        uptime: null,
        lastCheckTime: timestamp
      };
    }
  } catch (err) {
    if (logger?.warn) {
      logger.warn('Failed to retrieve health status', { error: err.message });
    }
    healthStatus = {
      status: 'unknown',
      reason: `Error: ${err.message}`,
      timestamp,
      uptime: null,
      lastCheckTime: timestamp
    };
  }

  // Collect handler metrics
  let handlers = [];
  try {
    if (profilerHandler?.aggregateMetrics) {
      const metrics = profilerHandler.aggregateMetrics();
      handlers = metrics.handlers || [];
    }
  } catch (err) {
    if (logger?.warn) {
      logger.warn('Failed to retrieve handler metrics', { error: err.message });
    }
  }

  // Apply operation filters
  if (operation === 'filter-tier' && filter) {
    handlers = handlers.filter(h => h.tier === filter);
  } else if (operation === 'filter-handler-name' && filter) {
    handlers = handlers.filter(h => h.name?.includes(filter));
  }

  // Collect error queue
  let errors = [];
  try {
    if (bridgeLogger?.getRecentErrors) {
      const rawErrors = bridgeLogger.getRecentErrors();
      errors = (Array.isArray(rawErrors) ? rawErrors : [])
        .map(err => ({
          timestamp: err.timestamp || timestamp,
          severity: determineSeverityFromLatency(err.latencyMs || 0),
          message: err.message || String(err),
          context: err.context || null,
          handlerName: err.handlerName || null,
          code: err.code || null
        }))
        .slice(0, 100); // Max 100 entries
    }
  } catch (err) {
    if (logger?.warn) {
      logger.warn('Failed to retrieve error queue', { error: err.message });
    }
  }

  // Calculate summary statistics
  const summary = calculateSummary(healthStatus, handlers, errors);

  return {
    health: healthStatus,
    handlers: handlers.map(h => ({
      name: h.name,
      tier: h.tier || 'unknown',
      status: determineSeverityFromLatency(h.latency?.p99 || 0),
      latency: {
        p50: h.latency?.p50 || null,
        p95: h.latency?.p95 || null,
        p99: h.latency?.p99 || null
      },
      errorRate: h.errorRate || 0,
      throughput: h.throughput || 0,
      requestCount: h.requestCount || 0,
      timeoutCount: h.timeoutCount || 0,
      cacheHitRate: h.cacheHitRate || null
    })),
    errors: errors,
    summary: summary,
    timestamp: timestamp
  };
}

/**
 * Calculates summary statistics for the diagnostic snapshot.
 *
 * @param {Object} health - Health status object
 * @param {Array} handlers - Array of handler metrics
 * @param {Array} errors - Array of error objects
 * @returns {Object} Summary statistics
 */
function calculateSummary(health, handlers, errors) {
  const now = new Date();
  const uptime = health.uptime;

  // Calculate aggregate metrics
  const totalRequests = handlers.reduce((sum, h) => sum + (h.requestCount || 0), 0);
  const totalTimeouts = handlers.reduce((sum, h) => sum + (h.timeoutCount || 0), 0);
  const avgErrorRate = handlers.length > 0
    ? handlers.reduce((sum, h) => sum + (h.errorRate || 0), 0) / handlers.length
    : 0;
  const avgThroughput = handlers.length > 0
    ? handlers.reduce((sum, h) => sum + (h.throughput || 0), 0) / handlers.length
    : 0;
  const maxLatencyP99 = handlers.length > 0
    ? Math.max(...handlers.map(h => h.latency?.p99 || 0))
    : 0;

  // Determine overall health
  let overallHealth = 'healthy';
  if (health.status === 'error') {
    overallHealth = 'error';
  } else if (health.status === 'degraded' || totalTimeouts > 10 || maxLatencyP99 > 500) {
    overallHealth = 'degraded';
  }

  return {
    overallHealth,
    totalHandlers: handlers.length,
    totalRequests,
    totalTimeouts,
    errorCount: errors.length,
    avgErrorRate: avgErrorRate.toFixed(4),
    avgThroughput: avgThroughput.toFixed(2),
    maxLatencyP99Ms: maxLatencyP99,
    criticalHandlers: handlers.filter(h => (h.latency?.p99 || 0) > 500).length,
    warningHandlers: handlers.filter(h => (h.latency?.p99 || 0) > 200 && (h.latency?.p99 || 0) <= 500).length,
    uptime: uptime || null
  };
}

export default createDiagnosticPanelHandler;
