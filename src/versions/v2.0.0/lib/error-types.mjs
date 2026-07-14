#!/usr/bin/env node

/**
 * Error Type Hierarchy for Error Recovery Middleware (Step 74)
 *
 * Defines custom error classes used throughout the error recovery pipeline:
 * - ErrorRecoveryError: Base class for all recovery-related errors
 * - ValidationError: Validation failures (envelope or JSON-RPC payload)
 * - TimeoutError: RPC deadline exceeded
 * - HandlerError: Unhandled exceptions from handler execution
 * - RecoveryActionError: Failure during rollback/retry/escalation
 * - AlertingError: Failure during telemetry reporting
 * - UnknownError: Catch-all for unexpected exceptions
 *
 * @module src/versions/v2.0.0/lib/error-types.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 47: MiddlewareChain (uses these for error handling)
 *   - Step 64: TimeoutManager (throws TimeoutError)
 *   - Step 73: ValidationHook (throws ValidationError)
 *   - Step 74: ErrorRecoveryHook (catches all, wraps in JSON-RPC response)
 */

/**
 * Base error class for error recovery operations.
 *
 * All error recovery errors extend this base to provide consistent:
 * - JSON-RPC error code (-32600, -32603, etc.)
 * - Operation context (which step/handler failed)
 * - Original error chain (rootCause exception)
 * - Message correlation (messageId for tracing)
 *
 * @class ErrorRecoveryError
 * @extends Error
 */
export class ErrorRecoveryError extends Error {
  /**
   * @param {number} code - JSON-RPC error code (-32700 to -32000)
   * @param {string} message - Error message
   * @param {string} operation - Operation that failed (e.g., 'requestValidation', 'handlerExecution')
   * @param {Error} [originalError] - Root cause exception (if wrapping another error)
   * @param {string} [messageId] - Correlation ID for request tracking
   */
  constructor(code, message, operation, originalError = null, messageId = null) {
    super(message);
    this.name = 'ErrorRecoveryError';
    this.code = code; // JSON-RPC error code
    this.operation = operation; // Context of failure
    this.originalError = originalError; // Root cause chain
    this.messageId = messageId; // Correlation ID
    this.timestamp = new Date().toISOString();

    // Capture stack trace at construction time
    if (Error.captureStackTrace) {
      Error.captureStackTrace(this, this.constructor);
    }
  }

  /**
   * Build JSON-RPC error object from this error.
   * @returns {Object} { code, message, data? }
   */
  toJsonRpcError() {
    return {
      code: this.code,
      message: this.message,
      data: {
        operation: this.operation,
        messageId: this.messageId,
        timestamp: this.timestamp,
        // Include stack trace only in debug mode (controlled by caller)
      },
    };
  }
}

/**
 * Validation failure error.
 *
 * Thrown when envelope or JSON-RPC payload validation fails.
 * JSON-RPC code: -32600 (Invalid Request)
 *
 * @class ValidationError
 * @extends ErrorRecoveryError
 */
export class ValidationError extends ErrorRecoveryError {
  /**
   * @param {string} message - Validation error message
   * @param {string} [field] - Field that failed validation (e.g., 'messageType', 'messageId')
   * @param {string} [messageId] - Correlation ID
   */
  constructor(message, field = null, messageId = null) {
    super(
      -32600, // JSON-RPC Invalid Request code
      message,
      'requestValidation',
      null,
      messageId
    );
    this.name = 'ValidationError';
    this.field = field; // Which field failed (optional)
  }

  toJsonRpcError() {
    const base = super.toJsonRpcError();
    if (this.field) {
      base.data.field = this.field;
    }
    return base;
  }
}

/**
 * Timeout error.
 *
 * Thrown when RPC call exceeds deadline (from Step 64 TimeoutManager).
 * JSON-RPC code: -32603 (Internal Error)
 *
 * @class TimeoutError
 * @extends ErrorRecoveryError
 */
export class TimeoutError extends ErrorRecoveryError {
  /**
   * @param {number} timeoutMs - Timeout duration in milliseconds
   * @param {string} [operation] - Operation that timed out (e.g., 'getEditorState', 'search')
   * @param {string} [messageId] - Correlation ID
   */
  constructor(timeoutMs, operation = 'rpcCall', messageId = null) {
    super(
      -32603, // JSON-RPC Internal Error code
      `Request timeout after ${timeoutMs}ms`,
      `timeout_${operation}`,
      null,
      messageId
    );
    this.name = 'TimeoutError';
    this.timeoutMs = timeoutMs;
    this.isTransient = true; // Retry candidates
  }

  toJsonRpcError() {
    const base = super.toJsonRpcError();
    base.data.timeoutMs = this.timeoutMs;
    base.data.isTransient = this.isTransient;
    return base;
  }
}

/**
 * Handler execution error.
 *
 * Thrown when a handler throws an unhandled exception.
 * Wraps the original exception for correlation and tracing.
 * JSON-RPC code: -32603 (Internal Error)
 *
 * @class HandlerError
 * @extends ErrorRecoveryError
 */
export class HandlerError extends ErrorRecoveryError {
  /**
   * @param {Error} originalError - Exception thrown by handler
   * @param {string} handlerName - Name of handler that failed (e.g., 'getEditorState')
   * @param {string} [messageId] - Correlation ID
   */
  constructor(originalError, handlerName, messageId = null) {
    const message = `Handler '${handlerName}' failed: ${originalError?.message || 'Unknown error'}`;
    super(
      -32603, // JSON-RPC Internal Error code
      message,
      `handler_${handlerName}`,
      originalError,
      messageId
    );
    this.name = 'HandlerError';
    this.handlerName = handlerName;
    this.isTransient = false; // Don't retry handler errors
  }

  toJsonRpcError() {
    const base = super.toJsonRpcError();
    base.data.handlerName = this.handlerName;
    base.data.isTransient = this.isTransient;
    // Stack trace added by caller if logger level = debug
    return base;
  }
}

/**
 * Recovery action failure error.
 *
 * Thrown when a recovery action (rollback, retry, escalation) fails.
 * Indicates the recovery mechanism itself encountered an error.
 * JSON-RPC code: -32000 (Server Error)
 *
 * @class RecoveryActionError
 * @extends ErrorRecoveryError
 */
export class RecoveryActionError extends ErrorRecoveryError {
  /**
   * @param {string} actionType - Type of action that failed ('rollback', 'retry', 'escalation')
   * @param {Error} originalError - Exception thrown during recovery
   * @param {string} [messageId] - Correlation ID
   */
  constructor(actionType, originalError, messageId = null) {
    const message = `Recovery action '${actionType}' failed: ${originalError?.message || 'Unknown error'}`;
    super(
      -32000, // JSON-RPC Server Error code
      message,
      `recovery_${actionType}`,
      originalError,
      messageId
    );
    this.name = 'RecoveryActionError';
    this.actionType = actionType;
  }

  toJsonRpcError() {
    const base = super.toJsonRpcError();
    base.data.actionType = this.actionType;
    return base;
  }
}

/**
 * Alerting/telemetry error.
 *
 * Thrown when telemetry recording or alerting fails.
 * Non-blocking; middleware continues despite telemetry failures.
 * JSON-RPC code: -32000 (Server Error)
 *
 * @class AlertingError
 * @extends ErrorRecoveryError
 */
export class AlertingError extends ErrorRecoveryError {
  /**
   * @param {string} alertType - Type of alert that failed ('errorRateAlert', 'stackTraceLogging')
   * @param {Error} originalError - Exception during alerting
   * @param {string} [messageId] - Correlation ID
   */
  constructor(alertType, originalError, messageId = null) {
    const message = `Alerting failed for '${alertType}': ${originalError?.message || 'Unknown error'}`;
    super(
      -32000, // JSON-RPC Server Error code
      message,
      `alerting_${alertType}`,
      originalError,
      messageId
    );
    this.name = 'AlertingError';
    this.alertType = alertType;
    this.isNonBlocking = true; // Middleware continues despite this error
  }

  toJsonRpcError() {
    const base = super.toJsonRpcError();
    base.data.alertType = this.alertType;
    base.data.isNonBlocking = this.isNonBlocking;
    return base;
  }
}

/**
 * Unknown/unexpected error.
 *
 * Catch-all for exceptions that don't fit other categories.
 * Preserves original error chain for debugging.
 * JSON-RPC code: -32603 (Internal Error)
 *
 * @class UnknownError
 * @extends ErrorRecoveryError
 */
export class UnknownError extends ErrorRecoveryError {
  /**
   * @param {Error} originalError - Unexpected exception
   * @param {string} [context] - Where the error occurred (e.g., 'middlewareChainExecution')
   * @param {string} [messageId] - Correlation ID
   */
  constructor(originalError, context = 'unknown', messageId = null) {
    const message = `Unexpected error in ${context}: ${originalError?.message || 'Unknown error'}`;
    super(
      -32603, // JSON-RPC Internal Error code
      message,
      `unknown_${context}`,
      originalError,
      messageId
    );
    this.name = 'UnknownError';
    this.context = context;
  }

  toJsonRpcError() {
    const base = super.toJsonRpcError();
    base.data.context = this.context;
    return base;
  }
}

/**
 * Helper function to determine if an error is recoverable.
 *
 * @param {Error} error - Error to check
 * @returns {boolean} True if error is transient and should be retried
 */
export function isRecoverableError(error) {
  if (error instanceof TimeoutError) {
    return error.isTransient === true;
  }
  if (error instanceof HandlerError) {
    return error.isTransient === false; // Don't retry handler errors
  }
  if (error instanceof ValidationError) {
    return false; // Never retry validation errors
  }
  return false; // Unknown errors not recoverable
}

/**
 * Helper function to extract JSON-RPC error code from any error.
 *
 * @param {Error} error - Error to examine
 * @returns {number} JSON-RPC error code (-32700 to -32000)
 */
export function getErrorCode(error) {
  if (error instanceof ErrorRecoveryError) {
    return error.code;
  }
  // Default to Internal Error for unknown exceptions
  return -32603;
}

/**
 * Helper function to extract correlation ID from error.
 *
 * @param {Error} error - Error to examine
 * @returns {string|null} messageId if present
 */
export function getErrorMessageId(error) {
  if (error instanceof ErrorRecoveryError) {
    return error.messageId || null;
  }
  return null;
}
