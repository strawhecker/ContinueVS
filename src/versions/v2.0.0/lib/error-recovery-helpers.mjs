#!/usr/bin/env node

/**
 * Error Recovery Helper Functions (Step 74)
 *
 * Provides utility functions for:
 * - Error classification (type identification, error code extraction)
 * - Response building (JSON-RPC error response construction)
 * - Telemetry utilities (error rate calculation, backoff delays)
 * - Guard clauses (null checks, circular reference detection)
 *
 * @module src/versions/v2.0.0/lib/error-recovery-helpers.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 74: ErrorRecoveryHook (primary consumer)
 *   - Step 25: IBridgeLogger (integrates with logging)
 *   - Step 26: IBridgeTelemetryCollector (integrates with metrics)
 */

import {
  ErrorRecoveryError,
  ValidationError,
  TimeoutError,
  HandlerError,
  RecoveryActionError,
  AlertingError,
  UnknownError,
  getErrorCode,
  getErrorMessageId,
  isRecoverableError,
} from './error-types.mjs';

// ============================================================================
// ERROR CLASSIFICATION HELPERS
// ============================================================================

/**
 * Classify error by type and determine JSON-RPC error code.
 *
 * @param {Error} error - Error to classify
 * @returns {Object} Classification result:
 *   {
 *     type: 'validation' | 'timeout' | 'handler' | 'recovery' | 'alerting' | 'unknown',
 *     code: number,
 *     isRecoverable: boolean,
 *     message: string
 *   }
 */
export function classifyError(error) {
  if (!error) {
    return {
      type: 'unknown',
      code: -32603,
      isRecoverable: false,
      message: 'Error is null or undefined',
    };
  }

  if (error instanceof ValidationError) {
    return {
      type: 'validation',
      code: error.code,
      isRecoverable: false,
      message: error.message,
    };
  }

  if (error instanceof TimeoutError) {
    return {
      type: 'timeout',
      code: error.code,
      isRecoverable: error.isTransient,
      message: error.message,
    };
  }

  if (error instanceof HandlerError) {
    return {
      type: 'handler',
      code: error.code,
      isRecoverable: false,
      message: error.message,
    };
  }

  if (error instanceof RecoveryActionError) {
    return {
      type: 'recovery',
      code: error.code,
      isRecoverable: false,
      message: error.message,
    };
  }

  if (error instanceof AlertingError) {
    return {
      type: 'alerting',
      code: error.code,
      isRecoverable: false,
      message: error.message,
    };
  }

  if (error instanceof ErrorRecoveryError) {
    return {
      type: 'recoveryError',
      code: error.code,
      isRecoverable: false,
      message: error.message,
    };
  }

  // Unknown/unexpected error
  return {
    type: 'unknown',
    code: -32603,
    isRecoverable: false,
    message: error.message || 'Unknown error',
  };
}

/**
 * Check if error is a validation error.
 *
 * @param {Error} error
 * @returns {boolean}
 */
export function isValidationError(error) {
  return error instanceof ValidationError;
}

/**
 * Check if error is a timeout error.
 *
 * @param {Error} error
 * @returns {boolean}
 */
export function isTimeoutError(error) {
  return error instanceof TimeoutError;
}

/**
 * Check if error is a handler error.
 *
 * @param {Error} error
 * @returns {boolean}
 */
export function isHandlerError(error) {
  return error instanceof HandlerError;
}

/**
 * Check if error is recoverable (transient).
 *
 * @param {Error} error
 * @returns {boolean}
 */
export function isRecoverable(error) {
  return isRecoverableError(error);
}

// ============================================================================
// RESPONSE BUILDING HELPERS
// ============================================================================

/**
 * Build JSON-RPC error response from validation error.
 *
 * @param {ValidationError|Error} error
 * @param {string} messageId
 * @returns {Object} { code, message, data? }
 */
export function buildValidationErrorResponse(error, messageId) {
  const classification = classifyError(error);
  return {
    code: classification.code || -32600,
    message: classification.message || 'Invalid request',
    data: {
      operation: error.operation || 'validation',
      messageId,
      field: error.field || undefined,
      timestamp: new Date().toISOString(),
    },
  };
}

/**
 * Build JSON-RPC error response from timeout error.
 *
 * @param {TimeoutError|string} errorOrMessage - Error or timeout message
 * @param {number} timeoutMs - Timeout duration in milliseconds
 * @param {string} messageId
 * @returns {Object} { code, message, data }
 */
export function buildTimeoutErrorResponse(errorOrMessage, timeoutMs, messageId) {
  const message =
    typeof errorOrMessage === 'string'
      ? errorOrMessage
      : errorOrMessage?.message || `Request timeout after ${timeoutMs}ms`;

  return {
    code: -32603,
    message,
    data: {
      operation: 'timeout',
      messageId,
      timeoutMs,
      isTransient: true,
      timestamp: new Date().toISOString(),
    },
  };
}

/**
 * Build JSON-RPC error response from handler error.
 *
 * @param {HandlerError|Error} error
 * @param {string} messageId
 * @param {boolean} [includeStack=false] - Include stack trace (debug mode)
 * @returns {Object} { code, message, data? }
 */
export function buildHandlerErrorResponse(error, messageId, includeStack = false) {
  const classification = classifyError(error);
  const response = {
    code: classification.code || -32603,
    message: classification.message || 'Handler execution failed',
    data: {
      operation: error.operation || 'handler',
      messageId,
      handlerName: error.handlerName || 'unknown',
      timestamp: new Date().toISOString(),
    },
  };

  if (includeStack && error.stack) {
    response.data.stack = sanitizeStackTrace(error.stack);
  }

  return response;
}

/**
 * Build JSON-RPC error response from unknown error.
 *
 * @param {Error} error
 * @param {string} messageId
 * @param {boolean} [includeStack=false] - Include stack trace (debug mode)
 * @returns {Object} { code, message, data }
 */
export function buildUnknownErrorResponse(error, messageId, includeStack = false) {
  const classification = classifyError(error);
  const response = {
    code: classification.code || -32603,
    message: classification.message || 'Internal error',
    data: {
      operation: 'unknown',
      messageId,
      timestamp: new Date().toISOString(),
    },
  };

  if (includeStack && error?.stack) {
    response.data.stack = sanitizeStackTrace(error.stack);
  }

  return response;
}

/**
 * Generic error response builder (delegates to specific builders).
 *
 * @param {Error} error
 * @param {string} messageId
 * @param {boolean} [includeStack=false]
 * @returns {Object} JSON-RPC error response
 */
export function buildErrorResponse(error, messageId, includeStack = false) {
  const classification = classifyError(error);

  switch (classification.type) {
    case 'validation':
      return buildValidationErrorResponse(error, messageId);

    case 'timeout':
      const timeoutMs =
        error instanceof TimeoutError
          ? error.timeoutMs
          : parseInt(error.message?.match(/(\d+)/)?.[0] || '0');
      return buildTimeoutErrorResponse(error, timeoutMs, messageId);

    case 'handler':
      return buildHandlerErrorResponse(error, messageId, includeStack);

    case 'recovery':
    case 'alerting':
    case 'unknown':
    default:
      return buildUnknownErrorResponse(error, messageId, includeStack);
  }
}

// ============================================================================
// TELEMETRY HELPERS
// ============================================================================

/**
 * Calculate error rate from metrics.
 *
 * @param {Object} metrics - Metrics object
 * @returns {number} Error rate (0.0 - 1.0), or NaN if no data
 */
export function getErrorRate(metrics) {
  if (!metrics || !metrics.totalRequests || metrics.totalRequests === 0) {
    return 0;
  }
  const errorCount = metrics.errorCount || 0;
  return errorCount / metrics.totalRequests;
}

/**
 * Determine if error rate exceeds alert threshold.
 *
 * @param {number} errorRate - Current error rate (0-1)
 * @param {number} [threshold=0.01] - Alert threshold (default 1%)
 * @returns {boolean}
 */
export function shouldAlert(errorRate, threshold = 0.01) {
  return errorRate > threshold;
}

/**
 * Calculate exponential backoff delay for retry attempts.
 *
 * @param {number} retryCount - Current retry attempt (0-indexed)
 * @param {number} [baseDelay=100] - Base delay in milliseconds
 * @param {number} [maxDelay=5000] - Maximum delay cap in milliseconds
 * @returns {number} Delay in milliseconds
 */
export function calculateBackoffDelay(retryCount, baseDelay = 100, maxDelay = 5000) {
  const exponentialDelay = baseDelay * Math.pow(2, retryCount);
  return Math.min(exponentialDelay, maxDelay);
}

/**
 * Format error for structured logging.
 *
 * @param {Error} error
 * @param {string} [messageId]
 * @param {boolean} [includeStack=false]
 * @returns {string} Formatted error string
 */
export function formatErrorForLogging(error, messageId, includeStack = false) {
  const classification = classifyError(error);
  const parts = [
    `[${classification.type.toUpperCase()}] ${classification.message}`,
  ];

  if (messageId) {
    parts.push(`messageId=${messageId}`);
  }

  if (error instanceof ErrorRecoveryError && error.operation) {
    parts.push(`operation=${error.operation}`);
  }

  if (includeStack && error?.stack) {
    parts.push(`\nStack: ${sanitizeStackTrace(error.stack)}`);
  }

  return parts.join(' | ');
}

// ============================================================================
// GUARD CLAUSES
// ============================================================================

/**
 * Guard: Reject null or undefined message.
 *
 * @param {*} message
 * @throws {ValidationError}
 */
export function guardAgainstNullMessage(message) {
  if (message === null || message === undefined) {
    throw new ValidationError('Message is null or undefined', 'message');
  }
}

/**
 * Guard: Reject non-object message.
 *
 * @param {*} message
 * @throws {ValidationError}
 */
export function guardAgainstNonObjectMessage(message) {
  if (typeof message !== 'object') {
    throw new ValidationError('Message must be an object', 'type');
  }
}

/**
 * Guard: Sanitize error to prevent circular references in JSON serialization.
 *
 * @param {Error} error
 * @returns {Object} Sanitized error object
 */
export function sanitizeErrorForSerialization(error) {
  if (!error) {
    return { message: 'Unknown error' };
  }

  const sanitized = {
    name: error.name || 'Error',
    message: error.message || 'No message',
    code:
      error.code ||
      (error instanceof ErrorRecoveryError
        ? error.code
        : -32603),
  };

  if (error.operation) {
    sanitized.operation = error.operation;
  }

  if (error.messageId) {
    sanitized.messageId = error.messageId;
  }

  // Avoid circular reference: don't include originalError
  return sanitized;
}

/**
 * Guard: Sanitize stack trace to remove PII and unnecessary frames.
 *
 * @param {string} stack
 * @returns {string} Sanitized stack trace
 */
export function sanitizeStackTrace(stack) {
  if (!stack) {
    return '';
  }

  return stack
    .split('\n')
    .map((line) => {
      // Remove file paths that might contain usernames
      return line.replace(/([A-Z]:\\[^:]+\\|\/[\w]+\/[\w]+\/)/g, '[path]/');
    })
    .join('\n')
    .slice(0, 500); // Cap stack trace length
}

/**
 * Check if object has circular reference.
 *
 * @param {*} obj
 * @param {Set} [visited] - Internal tracking
 * @returns {boolean}
 */
export function hasCircularReference(obj, visited = new Set()) {
  if (obj === null || typeof obj !== 'object') {
    return false;
  }

  if (visited.has(obj)) {
    return true; // Circular reference detected
  }

  visited.add(obj);

  for (const key in obj) {
    if (obj.hasOwnProperty(key)) {
      if (hasCircularReference(obj[key], visited)) {
        return true;
      }
    }
  }

  visited.delete(obj);
  return false;
}

// ============================================================================
// UTILITY HELPERS
// ============================================================================

/**
 * Extract correlation ID from message.
 *
 * @param {Object} message
 * @returns {string|null}
 */
export function getMessageIdFromMessage(message) {
  if (!message) {
    return null;
  }

  return message.messageId || null;
}

/**
 * Extract handler name from error context.
 *
 * @param {Error} error
 * @returns {string|null}
 */
export function getHandlerNameFromError(error) {
  if (error instanceof HandlerError) {
    return error.handlerName;
  }

  if (error instanceof ErrorRecoveryError && error.operation) {
    // Extract handler name from operation like "handler_getEditorState"
    const match = error.operation.match(/^handler_(.+)$/);
    if (match) {
      return match[1];
    }
  }

  return null;
}

/**
 * Create error context object for logging/telemetry.
 *
 * @param {Error} error
 * @param {Object} message
 * @returns {Object}
 */
export function createErrorContext(error, message) {
  const classification = classifyError(error);
  return {
    errorType: classification.type,
    errorCode: classification.code,
    message: classification.message,
    messageId: getMessageIdFromMessage(message),
    handlerName: getHandlerNameFromError(error),
    timestamp: new Date().toISOString(),
    isRecoverable: classification.isRecoverable,
  };
}
