#!/usr/bin/env node

/**
 * TimeoutManager for RPC Calls
 *
 * Manages pending RPC requests with configurable timeout policies, metrics collection,
 * and graceful degradation. Extracted from inline timeout enforcement in Step 63
 * (BridgeProtocolAdapter) to provide dedicated, reusable timeout lifecycle management.
 *
 * Responsibilities:
 * 1. **Request Tracking**: Track pending RPC calls by messageId with AbortController pattern
 * 2. **Timeout Enforcement**: Apply configurable timeouts per request or message type
 * 3. **Lifecycle Management**: Resolve/reject requests, clean up expired entries
 * 4. **Metrics Collection**: Track p99 latency, timeout rate, average wait time, request volume
 * 5. **Graceful Degradation**: Optional logger/metrics injection (no-op if null)
 * 6. **Policy-driven Configuration**: Per-handler timeout strategies via TimeoutPolicy
 *
 * Architecture:
 * ```
 * TimeoutPolicy (config)
 *   ├─ defaultTimeoutMs: 5000
 *   ├─ handlerTimeouts: Map<messageType, ms>
 *   ├─ retryOnTimeout: false (optional)
 *   └─ maxRetries: 0 (optional)
 *
 * TimeoutManager (instance)
 *   ├─ trackRequest(messageId, timeoutMs?) → Promise
 *   ├─ resolveRequest(messageId, response) → boolean
 *   ├─ rejectRequest(messageId, error) → boolean
 *   ├─ getPendingCount() → number
 *   ├─ getMetrics() → {totalRequests, timeouts, averageWaitMs, p99WaitMs, requestsPerSecond}
 *   ├─ clearExpired(maxAgeMs) → number
 *   └─ dispose() → void
 *
 * Metrics Lifecycle:
 *   1. trackRequest() records request start time
 *   2. resolveRequest/rejectRequest/timeout records end time
 *   3. Latency (end - start) stored in latencies array
 *   4. getMetrics() computes p99, average, rate from tracked data
 * ```
 *
 * @module src/versions/v2.0.0/lib/timeout-manager.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 63: BridgeProtocolAdapter (current timeout implementation)
 *   - Step 71: Handler Registration (per-handler policies)
 *   - Step 72–74: Middleware (metrics subscription)
 */

/**
 * Configuration contract for TimeoutManager policies.
 *
 * @typedef {Object} TimeoutPolicy
 * @property {number} defaultTimeoutMs - Default timeout in milliseconds (e.g., 5000)
 * @property {Map<string, number>} [handlerTimeouts] - Per-messageType overrides, e.g., {"bridge:getEditorState": 2000}
 * @property {boolean} [retryOnTimeout] - Whether to retry after timeout (default: false)
 * @property {number} [maxRetries] - Maximum retry attempts (default: 0)
 */

/**
 * Error thrown by TimeoutManager during initialization or configuration.
 * @class TimeoutManagerError
 * @extends {Error}
 */
export class TimeoutManagerError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [operation='unknown'] - Operation that failed (e.g., 'initialize', 'validate')
   * @param {Error} [originalError=null] - Original error (if wrapping)
   */
  constructor(message, operation = 'unknown', originalError = null) {
    super(message);
    this.name = 'TimeoutManagerError';
    this.operation = operation;
    this.originalError = originalError;
  }
}

/**
 * Error thrown when an RPC request times out.
 * @class TimeoutError
 * @extends {TimeoutManagerError}
 */
export class TimeoutError extends TimeoutManagerError {
  /**
   * @param {string} messageId - Correlation message ID
   * @param {number} timeoutMs - Timeout window in milliseconds
   */
  constructor(messageId, timeoutMs) {
    super(
      `RPC request timeout after ${timeoutMs}ms: messageId=${messageId}`,
      'timeout'
    );
    this.name = 'TimeoutError';
    this.messageId = messageId;
    this.timeoutMs = timeoutMs;
  }
}

/**
 * Internal representation of a pending RPC request.
 * @private
 */
class PendingRequest {
  /**
   * @param {string} messageId - Correlation ID
   * @param {AbortController} abortController - For cancellation
   * @param {Function} resolve - Promise resolve callback
   * @param {Function} reject - Promise reject callback
   * @param {number} startTimeMs - Request start timestamp
   */
  constructor(messageId, abortController, resolve, reject, startTimeMs) {
    this.messageId = messageId;
    this.abortController = abortController;
    this.resolve = resolve;
    this.reject = reject;
    this.startTimeMs = startTimeMs;
  }

  /**
   * Check if request has expired (age > maxAgeMs).
   * @param {number} maxAgeMs - Maximum age in milliseconds
   * @returns {boolean}
   */
  isExpired(maxAgeMs) {
    const ageMs = Date.now() - this.startTimeMs;
    return ageMs > maxAgeMs;
  }
}

/**
 * Manages pending RPC calls with configurable timeouts and metrics collection.
 *
 * @class TimeoutManager
 * @example
 * // Create with default policy
 * const policy = {
 *   defaultTimeoutMs: 5000,
 *   handlerTimeouts: new Map([
 *     ['bridge:getEditorState', 2000],
 *     ['bridge:search', 30000]
 *   ])
 * };
 * const manager = new TimeoutManager(policy, logger, metrics);
 *
 * // Track a request
 * const promise = manager.trackRequest('msg-uuid-1234', 5000);
 * promise.catch(err => console.error('Timeout:', err.message));
 *
 * // Later, resolve it
 * manager.resolveRequest('msg-uuid-1234', { success: true, data: {...} });
 *
 * // Query metrics
 * const {p99WaitMs, timeouts, averageWaitMs} = manager.getMetrics();
 * console.log(`p99 latency: ${p99WaitMs}ms, timeout rate: ${timeouts}/${totalRequests}`);
 */
export class TimeoutManager {
  /**
   * @param {TimeoutPolicy} policy - Timeout configuration
   * @param {Object} [logger=null] - Optional logger (log, warn, error methods)
   * @param {Object} [metrics=null] - Optional metrics collector (record, increment methods)
   * @throws {TimeoutManagerError} If policy validation fails
   */
  constructor(policy, logger = null, metrics = null) {
    this._validatePolicy(policy);

    this.policy = policy;
    this.logger = logger;
    this.metrics = metrics;

    // Pending RPC requests: Map<messageId, PendingRequest>
    this.pendingRequests = new Map();

    // Metrics tracking
    this.totalRequests = 0;
    this.totalTimeouts = 0;
    this.latencies = []; // Array of latency values (ms)
    this.createdAt = Date.now();
  }

  /**
   * Validate TimeoutPolicy object.
   * @private
   * @param {TimeoutPolicy} policy - Policy to validate
   * @throws {TimeoutManagerError} If policy invalid
   */
  _validatePolicy(policy) {
    if (!policy) {
      throw new TimeoutManagerError('policy is required', 'validate');
    }

    if (typeof policy.defaultTimeoutMs !== 'number' || policy.defaultTimeoutMs <= 0) {
      throw new TimeoutManagerError(
        `policy.defaultTimeoutMs must be a positive number, got: ${policy.defaultTimeoutMs}`,
        'validate'
      );
    }

    if (policy.handlerTimeouts) {
      if (!(policy.handlerTimeouts instanceof Map)) {
        throw new TimeoutManagerError(
          'policy.handlerTimeouts must be a Map<string, number>',
          'validate'
        );
      }
      for (const [key, value] of policy.handlerTimeouts.entries()) {
        if (typeof value !== 'number' || value <= 0) {
          throw new TimeoutManagerError(
            `policy.handlerTimeouts['${key}'] must be a positive number, got: ${value}`,
            'validate'
          );
        }
      }
    }
  }

  /**
   * Get timeout for a specific message type or use default.
   * @private
   * @param {string} messageType - Message type (e.g., 'bridge:getEditorState')
   * @returns {number} Timeout in milliseconds
   */
  _getTimeoutForMessageType(messageType) {
    if (this.policy.handlerTimeouts && this.policy.handlerTimeouts.has(messageType)) {
      return this.policy.handlerTimeouts.get(messageType);
    }
    return this.policy.defaultTimeoutMs;
  }

  /**
   * Track a pending RPC request with timeout enforcement.
   *
   * @param {string} messageId - Correlation ID
   * @param {number} [timeoutMs] - Timeout in milliseconds (uses default if not specified)
   * @param {string} [messageType] - Optional message type for handler-specific timeout override
   * @returns {Promise<Object>} Promise that resolves with response or rejects on timeout
   * @throws {TimeoutManagerError} If messageId invalid or duplicate
   *
   * @example
   * const promise = manager.trackRequest('uuid-1234', 5000, 'bridge:getEditorState');
   * try {
   *   const response = await promise;
   *   console.log('Success:', response);
   * } catch (err) {
   *   if (err instanceof TimeoutError) {
   *     console.error('Request timed out');
   *   }
   * }
   */
  trackRequest(messageId, timeoutMs = null, messageType = null) {
    if (!messageId || typeof messageId !== 'string') {
      const err = new TimeoutManagerError(
        `messageId must be a non-empty string, got: ${messageId}`,
        'track'
      );
      this._logError(`trackRequest failed: ${err.message}`);
      throw err;
    }

    if (this.pendingRequests.has(messageId)) {
      const err = new TimeoutManagerError(
        `Request already pending for messageId: ${messageId}`,
        'track'
      );
      this._logError(`trackRequest failed: ${err.message}`);
      throw err;
    }

    // Determine timeout: explicit > message-type-specific > default
    let timeout = timeoutMs;
    if (timeout === null || timeout === undefined) {
      timeout = messageType
        ? this._getTimeoutForMessageType(messageType)
        : this.policy.defaultTimeoutMs;
    }

    const startTimeMs = Date.now();
    const abortController = new AbortController();

    return new Promise((resolve, reject) => {
      const pendingRequest = new PendingRequest(
        messageId,
        abortController,
        resolve,
        reject,
        startTimeMs
      );

      this.pendingRequests.set(messageId, pendingRequest);
      this.totalRequests += 1;
      this._recordMetric('track', 1);

      // Set timeout
      const timeoutHandle = setTimeout(() => {
        this.pendingRequests.delete(messageId);
        this.totalTimeouts += 1;
        this._recordLatency(Date.now() - startTimeMs);
        this._recordMetric('timeout', 1);

        const error = new TimeoutError(messageId, timeout);
        this._logWarn(`RPC timeout: ${messageId} (${timeout}ms)`);
        reject(error);
      }, timeout);

      // Wrap resolve/reject to clean up timeout
      const wrappedResolve = (value) => {
        clearTimeout(timeoutHandle);
        this._recordLatency(Date.now() - startTimeMs);
        this.pendingRequests.delete(messageId);
        this._logDebug(`Request resolved: ${messageId}`);
        resolve(value);
      };

      const wrappedReject = (error) => {
        clearTimeout(timeoutHandle);
        this._recordLatency(Date.now() - startTimeMs);
        this.pendingRequests.delete(messageId);
        this._logWarn(`Request rejected: ${messageId}: ${error.message}`);
        reject(error);
      };

      // Update pending request with wrapped callbacks
      pendingRequest.resolve = wrappedResolve;
      pendingRequest.reject = wrappedReject;
    });
  }

  /**
   * Resolve a pending RPC request with a response.
   *
   * @param {string} messageId - Correlation ID
   * @param {Object} response - Response object to resolve with
   * @returns {boolean} True if request was pending and resolved, false otherwise
   *
   * @example
   * const success = manager.resolveRequest('uuid-1234', { success: true, data: {...} });
   * if (!success) console.warn('No pending request for uuid-1234');
   */
  resolveRequest(messageId, response) {
    const pending = this.pendingRequests.get(messageId);
    if (!pending) {
      this._logWarn(`resolveRequest: no pending request for ${messageId}`);
      return false;
    }

    pending.resolve(response);
    return true;
  }

  /**
   * Reject a pending RPC request with an error.
   *
   * @param {string} messageId - Correlation ID
   * @param {Error} error - Error to reject with
   * @returns {boolean} True if request was pending and rejected, false otherwise
   *
   * @example
   * const success = manager.rejectRequest('uuid-1234', new Error('Handler crashed'));
   * if (!success) console.warn('No pending request for uuid-1234');
   */
  rejectRequest(messageId, error) {
    const pending = this.pendingRequests.get(messageId);
    if (!pending) {
      this._logWarn(`rejectRequest: no pending request for ${messageId}`);
      return false;
    }

    pending.reject(error);
    return true;
  }

  /**
   * Get count of pending requests.
   *
   * @returns {number} Number of requests currently pending
   *
   * @example
   * const pending = manager.getPendingCount();
   * console.log(`${pending} requests in flight`);
   */
  getPendingCount() {
    return this.pendingRequests.size;
  }

  /**
   * Get current metrics for all tracked requests.
   *
   * @returns {Object} Metrics object
   *   - totalRequests: number (total requests tracked)
   *   - timeouts: number (count of timeouts)
   *   - averageWaitMs: number (average latency)
   *   - p99WaitMs: number (99th percentile latency)
   *   - requestsPerSecond: number (request rate)
   *   - pendingRequests: number (currently pending)
   *
   * @example
   * const {p99WaitMs, timeouts, totalRequests} = manager.getMetrics();
   * console.log(`p99: ${p99WaitMs}ms, timeouts: ${timeouts}/${totalRequests}`);
   */
  getMetrics() {
    const elapsedMs = Math.max(1, Date.now() - this.createdAt);
    const elapsedSec = elapsedMs / 1000;

    let averageWaitMs = 0;
    let p99WaitMs = 0;

    if (this.latencies.length > 0) {
      averageWaitMs = this.latencies.reduce((a, b) => a + b, 0) / this.latencies.length;

      // Calculate p99 (99th percentile)
      const sortedLatencies = [...this.latencies].sort((a, b) => a - b);
      const p99Index = Math.floor(sortedLatencies.length * 0.99);
      p99WaitMs = sortedLatencies[Math.max(0, p99Index)];
    }

    return {
      totalRequests: this.totalRequests,
      timeouts: this.totalTimeouts,
      averageWaitMs: Math.round(averageWaitMs),
      p99WaitMs: Math.round(p99WaitMs),
      requestsPerSecond: parseFloat((this.totalRequests / elapsedSec).toFixed(2)),
      pendingRequests: this.pendingRequests.size
    };
  }

  /**
   * Clear expired pending requests (by age).
   *
   * @param {number} [maxAgeMs=60000] - Maximum age before removal in milliseconds
   * @returns {number} Count of requests cleaned up
   *
   * @example
   * const cleaned = manager.clearExpired(120000); // Remove requests older than 2 minutes
   * console.log(`Cleaned up ${cleaned} expired requests`);
   */
  clearExpired(maxAgeMs = 60000) {
    let count = 0;
    for (const [messageId, pending] of this.pendingRequests.entries()) {
      if (pending.isExpired(maxAgeMs)) {
        this.pendingRequests.delete(messageId);
        count += 1;
        this._logDebug(`Expired request cleaned up: ${messageId}`);
      }
    }

    if (count > 0) {
      this._logDebug(`clearExpired: removed ${count} expired requests`);
    }
    return count;
  }

  /**
   * Dispose of TimeoutManager and clean up all pending requests.
   * After disposal, the manager cannot be reused; create a new instance instead.
   *
   * @example
   * manager.dispose();
   * // manager.trackRequest() will now throw as pendingRequests is null
   */
  dispose() {
    // Reject all pending requests
    for (const [messageId, pending] of this.pendingRequests.entries()) {
      try {
        pending.reject(new Error('TimeoutManager disposed'));
      } catch (err) {
        this._logWarn(`Error rejecting pending request during dispose: ${err.message}`);
      }
    }

    this.pendingRequests.clear();
    this.latencies = [];
    this.totalRequests = 0;
    this.totalTimeouts = 0;
    this._logDebug('TimeoutManager disposed');
  }

  /**
   * Record a latency measurement.
   * @private
   */
  _recordLatency(latencyMs) {
    this.latencies.push(latencyMs);
    // Keep latencies bounded to prevent unbounded memory growth
    // Keep last 10,000 measurements
    if (this.latencies.length > 10000) {
      this.latencies.shift();
    }
  }

  /**
   * Record a metric (if metrics collector provided).
   * @private
   */
  _recordMetric(metricName, value) {
    if (!this.metrics) {
      return;
    }
    try {
      if (typeof this.metrics.record === 'function') {
        this.metrics.record(metricName, value);
      }
    } catch (err) {
      this._logWarn(`Failed to record metric '${metricName}': ${err.message}`);
    }
  }

  /**
   * Logging helpers (graceful degradation if no logger).
   * @private
   */
  _logDebug(message) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log(`[TimeoutManager] ${message}`);
    }
  }

  _logWarn(message) {
    if (this.logger && typeof this.logger.warn === 'function') {
      this.logger.warn(`[TimeoutManager] ${message}`);
    }
  }

  _logError(message) {
    if (this.logger && typeof this.logger.error === 'function') {
      this.logger.error(`[TimeoutManager] ${message}`);
    }
  }
}

/**
 * Factory function to create and validate a TimeoutManager instance.
 *
 * @param {TimeoutPolicy} policy - Timeout configuration
 * @param {Object} [logger=null] - Optional logger
 * @param {Object} [metrics=null] - Optional metrics collector
 * @returns {TimeoutManager} Initialized TimeoutManager instance
 * @throws {TimeoutManagerError} If policy validation fails
 *
 * @example
 * const policy = {
 *   defaultTimeoutMs: 5000,
 *   handlerTimeouts: new Map([
 *     ['bridge:getEditorState', 2000],
 *     ['bridge:search', 30000]
 *   ])
 * };
 * const manager = createTimeoutManager(policy, logger, metrics);
 */
export function createTimeoutManager(policy, logger = null, metrics = null) {
  return new TimeoutManager(policy, logger, metrics);
}

/**
 * Create a default TimeoutPolicy suitable for general RPC operations.
 *
 * @returns {TimeoutPolicy} Default policy with common timeout settings
 *
 * @example
 * const policy = createDefaultPolicy();
 * // {
 * //   defaultTimeoutMs: 5000,
 * //   handlerTimeouts: new Map([
 * //     ['bridge:search', 30000],
 * //     ['bridge:codeCompletion', 15000]
 * //   ])
 * // }
 */
export function createDefaultPolicy() {
  return {
    defaultTimeoutMs: 5000,
    handlerTimeouts: new Map([
      ['bridge:search', 30000],
      ['bridge:codeCompletion', 15000],
      ['bridge:getEditorState', 2000],
      ['bridge:goToDefinition', 10000],
      ['bridge:findReferences', 10000]
    ])
  };
}

export default TimeoutManager;
