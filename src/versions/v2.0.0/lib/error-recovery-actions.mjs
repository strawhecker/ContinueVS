#!/usr/bin/env node

/**
 * Error Recovery Actions (Step 74)
 *
 * Implements recovery strategies:
 * - State rollback: Revert partial changes if handler failed mid-execution
 * - Retry logic: Exponential backoff for transient errors (timeout only)
 * - Error escalation: Record telemetry alerts when error rate exceeds threshold
 *
 * @module src/versions/v2.0.0/lib/error-recovery-actions.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 74: ErrorRecoveryHook (orchestrates recovery actions)
 *   - Step 64: TimeoutManager (generates transient TimeoutError)
 */

import {
  TimeoutError,
  RecoveryActionError,
  AlertingError,
} from './error-types.mjs';
import {
  calculateBackoffDelay,
  isTimeoutError,
  shouldAlert,
  getErrorRate,
} from './error-recovery-helpers.mjs';

// ============================================================================
// STATE ROLLBACK ACTION
// ============================================================================

/**
 * Attempts to rollback handler state on failure.
 *
 * Handler must support rollback via onError(originalState) callback.
 * If not supported, silently degrades (fail-soft).
 *
 * @class RollbackAction
 */
export class RollbackAction {
  /**
   * @param {Object} handler - Handler instance
   * @param {*} [originalState] - State snapshot before handler execution
   * @param {*} [logger] - Logger instance (optional)
   */
  constructor(handler, originalState = null, logger = null) {
    this.handler = handler;
    this.originalState = originalState;
    this.logger = logger;
  }

  /**
   * Execute rollback.
   *
   * @throws {RecoveryActionError} If rollback fails
   * @returns {Promise<void>}
   */
  async execute() {
    try {
      // Check if handler supports rollback
      if (!this.handler || typeof this.handler.onError !== 'function') {
        // Silently skip if handler doesn't support rollback
        return;
      }

      // Invoke handler's rollback callback
      await this.handler.onError(this.originalState);

      if (this.logger) {
        this.logger.debug('Rollback successful for handler', {
          handlerName: this.handler.name || 'unknown',
        });
      }
    } catch (error) {
      // Wrap rollback failure
      throw new RecoveryActionError(
        'rollback',
        error,
        this.handler?.messageId || null
      );
    }
  }
}

/**
 * Factory function to create rollback action.
 *
 * @param {Object} handler
 * @param {*} [originalState]
 * @param {*} [logger]
 * @returns {RollbackAction}
 */
export function createRollbackAction(handler, originalState = null, logger = null) {
  return new RollbackAction(handler, originalState, logger);
}

// ============================================================================
// RETRY ACTION
// ============================================================================

/**
 * Retry policy configuration.
 *
 * @typedef {Object} RetryPolicy
 * @property {number} [maxAttempts=3] - Maximum retry attempts
 * @property {number} [baseDelay=100] - Base delay in milliseconds
 * @property {number} [maxDelay=5000] - Maximum delay cap in milliseconds
 * @property {Function} [shouldRetryFn] - Custom predicate for retry decision
 */

/**
 * Executes operation with retry logic for transient errors.
 *
 * Only retries TimeoutError; never retries validation or handler errors.
 * Uses exponential backoff between attempts.
 *
 * @class RetryAction
 */
export class RetryAction {
  /**
   * @param {Function} operation - Async operation to retry
   * @param {RetryPolicy} [policy] - Retry configuration
   * @param {*} [logger] - Logger instance (optional)
   */
  constructor(operation, policy = {}, logger = null) {
    if (typeof operation !== 'function') {
      throw new Error('operation must be a function');
    }

    this.operation = operation;
    this.policy = {
      maxAttempts: policy.maxAttempts || 3,
      baseDelay: policy.baseDelay || 100,
      maxDelay: policy.maxDelay || 5000,
      shouldRetryFn: policy.shouldRetryFn || ((err) => isTimeoutError(err)),
    };
    this.logger = logger;
  }

  /**
   * Execute operation with retry.
   *
   * @throws {Error} Last error after all retries exhausted
   * @returns {Promise<*>} Operation result
   */
  async execute() {
    let lastError = null;
    let retryCount = 0;

    for (retryCount = 0; retryCount < this.policy.maxAttempts; retryCount++) {
      try {
        return await this.operation();
      } catch (error) {
        lastError = error;

        // Check if error is retryable
        if (!this.policy.shouldRetryFn(error)) {
          // Non-transient error; stop retrying
          throw error;
        }

        // Calculate backoff delay
        if (retryCount < this.policy.maxAttempts - 1) {
          const delay = calculateBackoffDelay(
            retryCount,
            this.policy.baseDelay,
            this.policy.maxDelay
          );

          if (this.logger) {
            this.logger.debug(`Retry attempt ${retryCount + 1}/${this.policy.maxAttempts}`, {
              error: error.message,
              delayMs: delay,
            });
          }

          // Wait before retry
          await new Promise((resolve) => setTimeout(resolve, delay));
        }
      }
    }

    // All retries exhausted
    throw lastError;
  }

  /**
   * Get retry statistics.
   *
   * @returns {Object} { maxAttempts, baseDelay, maxDelay }
   */
  getPolicy() {
    return { ...this.policy };
  }
}

/**
 * Factory function to create retry action.
 *
 * @param {Function} operation
 * @param {RetryPolicy} [policy]
 * @param {*} [logger]
 * @returns {RetryAction}
 */
export function createRetryAction(operation, policy = {}, logger = null) {
  return new RetryAction(operation, policy, logger);
}

// ============================================================================
// ESCALATION / ALERTING ACTION
// ============================================================================

/**
 * Records error and checks if alert threshold exceeded.
 * Escalates to telemetry if error rate exceeds 1%.
 *
 * @class AlertingAction
 */
export class AlertingAction {
  /**
   * @param {Object} metrics - Error metrics collector (from Step 26)
   * @param {*} [logger] - Logger instance (optional)
   * @param {number} [alertThreshold=0.01] - Alert threshold (default 1%)
   */
  constructor(metrics, logger = null, alertThreshold = 0.01) {
    this.metrics = metrics;
    this.logger = logger;
    this.alertThreshold = alertThreshold;
  }

  /**
   * Record error and trigger alert if threshold exceeded.
   *
   * @param {Object} errorContext - Error details
   * @param {string} errorContext.errorType - Error type
   * @param {string} [errorContext.messageId] - Correlation ID
   * @param {string} [errorContext.handlerName] - Handler name
   * @throws {AlertingError} If alerting itself fails
   * @returns {Promise<Object>} Alert result { triggered, errorRate, errorCount, totalCount }
   */
  async execute(errorContext) {
    try {
      if (!this.metrics) {
        // Silently skip if metrics unavailable
        return {
          triggered: false,
          skipped: true,
          reason: 'metrics_unavailable',
        };
      }

      // Record error in metrics
      if (typeof this.metrics.recordError === 'function') {
        this.metrics.recordError(errorContext.errorType, errorContext.messageId);
      }

      // Check if alert threshold exceeded
      const errorRate = getErrorRate(this.metrics);
      const alertTriggered = shouldAlert(errorRate, this.alertThreshold);

      const result = {
        triggered: alertTriggered,
        errorRate,
        errorCount: this.metrics.errorCount || 0,
        totalCount: this.metrics.totalRequests || 0,
      };

      if (alertTriggered) {
        if (this.logger) {
          this.logger.warn('Error rate alert triggered', {
            errorRate: (errorRate * 100).toFixed(2) + '%',
            threshold: (this.alertThreshold * 100).toFixed(2) + '%',
            errorType: errorContext.errorType,
            handlerName: errorContext.handlerName,
            messageId: errorContext.messageId,
          });
        }
      }

      return result;
    } catch (error) {
      // Wrap alerting failure; don't throw (non-blocking)
      if (this.logger) {
        this.logger.error('Alerting failed', { error: error.message });
      }

      // Return degraded result instead of throwing
      return {
        triggered: false,
        skipped: true,
        reason: 'alerting_failed',
        error: error.message,
      };
    }
  }
}

/**
 * Factory function to create alerting action.
 *
 * @param {Object} metrics
 * @param {*} [logger]
 * @param {number} [threshold]
 * @returns {AlertingAction}
 */
export function createAlertingAction(metrics, logger = null, threshold = 0.01) {
  return new AlertingAction(metrics, logger, threshold);
}

// ============================================================================
// RECOVERY ORCHESTRATOR
// ============================================================================

/**
 * Coordinates recovery actions based on error type.
 *
 * Determines which recovery strategies to apply:
 * - Timeout: Retry + escalation
 * - Validation: Log + escalation
 * - Handler: Rollback (optional) + escalation
 * - Unknown: Log + escalation
 *
 * @class RecoveryOrchestrator
 */
export class RecoveryOrchestrator {
  /**
   * @param {Object} config - Configuration
   * @param {*} [config.logger] - Logger
   * @param {*} [config.metrics] - Metrics collector
   * @param {Object} [config.policies] - Recovery policies
   */
  constructor({ logger = null, metrics = null, policies = {} } = {}) {
    this.logger = logger;
    this.metrics = metrics;
    this.policies = {
      enableRollback: policies.enableRollback !== false, // Default enabled
      enableRetry: policies.enableRetry !== false, // Default enabled
      enableAlerting: policies.enableAlerting !== false, // Default enabled
      maxRetries: policies.maxRetries || 3,
      alertThreshold: policies.alertThreshold || 0.01,
    };
  }

  /**
   * Orchestrate recovery for error.
   *
   * @param {Error} error - Error to recover from
   * @param {Object} errorContext - Error context
   * @param {Function} [retryOperation] - Optional operation to retry
   * @param {Object} [handler] - Optional handler for rollback
   * @param {*} [originalState] - State for rollback
   * @returns {Promise<Object>} Recovery result { recovered, actions, details }
   */
  async orchestrate(
    error,
    errorContext,
    retryOperation = null,
    handler = null,
    originalState = null
  ) {
    const actions = [];
    const details = [];

    try {
      // Attempt rollback if configured and handler provided
      if (this.policies.enableRollback && handler && originalState) {
        try {
          const rollback = createRollbackAction(
            handler,
            originalState,
            this.logger
          );
          await rollback.execute();
          actions.push('rollback_success');
        } catch (rollbackError) {
          actions.push('rollback_failed');
          details.push(`Rollback failed: ${rollbackError.message}`);
          if (this.logger) {
            this.logger.error('Rollback failed', {
              error: rollbackError.message,
            });
          }
        }
      }

      // Attempt retry if configured and error is transient
      if (this.policies.enableRetry && isTimeoutError(error) && retryOperation) {
        try {
          const retry = createRetryAction(
            retryOperation,
            {
              maxAttempts: this.policies.maxRetries,
              baseDelay: 100,
              maxDelay: 5000,
            },
            this.logger
          );
          const result = await retry.execute();
          actions.push('retry_success');
          return {
            recovered: true,
            actions,
            details,
            result,
          };
        } catch (retryError) {
          actions.push('retry_exhausted');
          details.push(`Retry exhausted: ${retryError.message}`);
          if (this.logger) {
            this.logger.debug('Retry exhausted', {
              error: retryError.message,
            });
          }
        }
      }

      // Alert if threshold exceeded
      if (this.policies.enableAlerting) {
        try {
          const alerting = createAlertingAction(
            this.metrics,
            this.logger,
            this.policies.alertThreshold
          );
          const alertResult = await alerting.execute(errorContext);
          if (alertResult.triggered) {
            actions.push('alert_triggered');
          }
        } catch (alertError) {
          details.push(`Alerting failed: ${alertError.message}`);
        }
      }

      return {
        recovered: false, // Not fully recovered, but actions attempted
        actions,
        details,
      };
    } catch (orchestrationError) {
      // Orchestrator itself failed; return partial result
      return {
        recovered: false,
        actions: [...actions, 'orchestration_failed'],
        details: [...details, orchestrationError.message],
        error: orchestrationError,
      };
    }
  }
}

/**
 * Factory function to create recovery orchestrator.
 *
 * @param {Object} config
 * @returns {RecoveryOrchestrator}
 */
export function createRecoveryOrchestrator(config = {}) {
  return new RecoveryOrchestrator(config);
}
