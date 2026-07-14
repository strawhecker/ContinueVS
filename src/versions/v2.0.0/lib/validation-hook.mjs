#!/usr/bin/env node

/**
 * Request/Response Validation Hook for Bridge Message Chain
 *
 * Provides JSON-RPC validation for bridge messages. Validates both:
 * - Custom Message envelope: { messageType, messageId, data }
 * - JSON-RPC payload within data: { method, params, id } or { result/error }
 *
 * Integrates into MiddlewareChain.validationHook (Step 47).
 *
 * @module src/versions/v2.0.0/lib/validation-hook.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 47: MiddlewareChain (provides hook registration)
 *   - Step 72: Message logging middleware
 *   - Step 74: Error recovery middleware
 */

/**
 * Custom error for validation failures.
 * Includes JSON-RPC error code and bridge message ID for correlation.
 */
export class ValidationError extends Error {
  constructor(code, message, messageId = null) {
    super(message);
    this.name = 'ValidationError';
    this.code = code;
    this.messageId = messageId;
  }
}

/**
 * Validates Message envelope structure.
 *
 * Checks:
 * - message is not null/undefined
 * - messageType exists and is non-empty string
 * - messageId exists and is non-empty string
 * - data exists and is an object
 *
 * @param {*} message - Message to validate
 * @returns {Object} { isValid: boolean, error?: string, code?: number }
 */
export function validateMessageEnvelope(message) {
  if (!message) {
    return {
      isValid: false,
      error: 'Message is null or undefined',
      code: -32700, // Parse error
    };
  }

  if (typeof message !== 'object') {
    return {
      isValid: false,
      error: 'Message must be an object',
      code: -32700,
    };
  }

  if (!message.messageType || typeof message.messageType !== 'string') {
    return {
      isValid: false,
      error: 'messageType is required and must be a non-empty string',
      code: -32600, // Invalid Request
    };
  }

  if (!message.messageId || typeof message.messageId !== 'string') {
    return {
      isValid: false,
      error: 'messageId is required and must be a non-empty string',
      code: -32600,
    };
  }

  if (!message.data || typeof message.data !== 'object') {
    return {
      isValid: false,
      error: 'data is required and must be an object',
      code: -32600,
    };
  }

  return { isValid: true };
}

/**
 * Validates JSON-RPC payload within message.data.
 *
 * For requests (isRequest=true):
 * - method: string, required
 * - params: object or array, optional
 * - id: string or number, optional (if missing, treated as notification)
 *
 * For responses (isRequest=false):
 * - Exactly one of result OR error must be present
 * - If error: must have code (number) and message (string)
 *
 * @param {*} data - Message.data to validate
 * @param {boolean} isRequest - True for request validation, false for response
 * @returns {Object} { isValid: boolean, error?: string, code?: number }
 */
export function validatePayload(data, isRequest = true) {
  if (!data || typeof data !== 'object') {
    return {
      isValid: false,
      error: 'Payload must be an object',
      code: -32600,
    };
  }

  if (isRequest) {
    return validateRequestPayload(data);
  } else {
    return validateResponsePayload(data);
  }
}

/**
 * Validates JSON-RPC request payload.
 *
 * @private
 * @param {Object} data - Request payload
 * @returns {Object} { isValid: boolean, error?: string, code?: number }
 */
function validateRequestPayload(data) {
  // method is required
  if (data.method === undefined || data.method === null) {
    return {
      isValid: false,
      error: 'Request method is required',
      code: -32600,
    };
  }

  if (typeof data.method !== 'string' || data.method.length === 0) {
    return {
      isValid: false,
      error: 'Request method must be a non-empty string',
      code: -32600,
    };
  }

  // params is optional, but if present must be object or array
  if (data.params !== undefined && data.params !== null) {
    const paramsType = typeof data.params;
    if (paramsType !== 'object') {
      return {
        isValid: false,
        error: `Request params must be an object or array, got ${paramsType}`,
        code: -32602, // Invalid params
      };
    }
  }

  // id is optional (notifications have no id)
  // If present, should be string or number
  if (data.id !== undefined && data.id !== null) {
    const idType = typeof data.id;
    if (idType !== 'string' && idType !== 'number') {
      return {
        isValid: false,
        error: `Request id must be string or number, got ${idType}`,
        code: -32600,
      };
    }
  }

  return { isValid: true };
}

/**
 * Validates JSON-RPC response payload.
 *
 * @private
 * @param {Object} data - Response payload
 * @returns {Object} { isValid: boolean, error?: string, code?: number }
 */
function validateResponsePayload(data) {
  const hasResult = data.hasOwnProperty('result');
  const hasError = data.hasOwnProperty('error');

  // XOR: exactly one of result or error
  if (!hasResult && !hasError) {
    return {
      isValid: false,
      error: 'Response must have either result or error field',
      code: -32603, // Internal error
    };
  }

  if (hasResult && hasError) {
    return {
      isValid: false,
      error: 'Response must have either result or error, not both',
      code: -32603,
    };
  }

  // If error present, validate structure
  if (hasError) {
    const error = data.error;
    if (!error || typeof error !== 'object') {
      return {
        isValid: false,
        error: 'Response error must be an object',
        code: -32603,
      };
    }

    if (!error.hasOwnProperty('code') || typeof error.code !== 'number') {
      return {
        isValid: false,
        error: 'Response error must have numeric code field',
        code: -32603,
      };
    }

    if (!error.hasOwnProperty('message') || typeof error.message !== 'string') {
      return {
        isValid: false,
        error: 'Response error must have string message field',
        code: -32603,
      };
    }
  }

  return { isValid: true };
}

/**
 * Builds structured error response following JSON-RPC spec.
 *
 * @param {Object} originalMessage - Original invalid message (or partial)
 * @param {number} errorCode - JSON-RPC error code
 * @param {string} errorMessage - Error description
 * @returns {Object} Error response in bridge Message format
 */
export function buildErrorResponse(originalMessage, errorCode, errorMessage) {
  return {
    messageType: 'rpc:error',
    messageId: originalMessage?.messageId || 'unknown',
    success: false,
    data: {
      error: {
        code: errorCode,
        message: errorMessage,
      },
      originalMessage: originalMessage || null,
    },
  };
}

/**
 * Factory function to create a validation hook for MiddlewareChain.
 *
 * The returned hook validates incoming messages before dispatch and returns
 * error responses for invalid messages without calling next().
 *
 * @param {Object} options - Hook configuration
 * @param {*} options.logger - Logger instance (optional)
 * @param {*} options.metrics - Metrics collector (optional)
 * @returns {Function} Hook function (message, next, context) => DispatchResult
 */
export function createValidationHook({ logger = null, metrics = null } = {}) {
  /**
   * Validation hook middleware.
   *
   * Signature: async (message, next, context) => DispatchResult
   *
   * @param {Object} message - Message to validate
   * @param {Function} next - Next middleware in chain
   * @param {Object} context - Middleware context
   * @returns {Promise<Object>} DispatchResult
   */
  async function validationHook(message, next, context) {
    const actualLogger = logger || context?.logger;
    const actualMetrics = metrics || context?.metrics;

    // Validate envelope
    const envelopeValidation = validateMessageEnvelope(message);
    if (!envelopeValidation.isValid) {
      const errorResponse = buildErrorResponse(
        message,
        envelopeValidation.code,
        envelopeValidation.error
      );

      if (actualLogger) {
        actualLogger.warn(
          `Validation failed (envelope): ${envelopeValidation.error} [messageId: ${message?.messageId}]`
        );
      }

      if (actualMetrics) {
        actualMetrics.recordValidationFailure('envelope', envelopeValidation.code);
      }

      return {
        handled: true,
        shouldRelay: true,
        response: errorResponse,
      };
    }

    // Detect if request or response by presence of 'method' field
    const isRequest = !!message.data.method;

    // Validate payload
    const payloadValidation = validatePayload(message.data, isRequest);
    if (!payloadValidation.isValid) {
      const errorResponse = buildErrorResponse(
        message,
        payloadValidation.code,
        payloadValidation.error
      );

      if (actualLogger) {
        actualLogger.warn(
          `Validation failed (payload): ${payloadValidation.error} [messageId: ${message.messageId}]`
        );
      }

      if (actualMetrics) {
        actualMetrics.recordValidationFailure('payload', payloadValidation.code);
      }

      return {
        handled: true,
        shouldRelay: true,
        response: errorResponse,
      };
    }

    // Valid message: pass to next middleware
    return await next(message);
  }

  return validationHook;
}

/**
 * Export validation hook as default for easy import.
 */
export default {
  ValidationError,
  validateMessageEnvelope,
  validatePayload,
  buildErrorResponse,
  createValidationHook,
};
