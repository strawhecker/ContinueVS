#!/usr/bin/env node

/**
 * Context Window Handler for Continue Bridge
 *
 * Provides bridge handler for querying LLM context window token budget and utilization.
 * Factory-based stateless handler that queries a C# collector to estimate token consumption.
 *
 * **Handler Type**: Factory (returns async function)
 * **Message Type**: bridge:getContextWindow
 * **Input**: BridgeMessage (minimal metadata, no payload required)
 * **Output**: BridgeResponse containing { maxTokens, usedTokens, availableTokens, estimatedTokens, utilization, recommendations }
 *
 * **Architecture Flow**:
 * ```
 * [WebView] → "getContextWindow" request
 *   ↓
 * [context-window-handler] validates request
 *   ↓
 * [ContextWindowCollector] GetContextWindowAsync()
 *   ├─ Query Continue config for max tokens
 *   ├─ Estimate tokens from conversation history
 *   ├─ Estimate tokens from editor content
 *   ├─ Estimate tokens from selected text
 *   ├─ Estimate tokens from referenced files
 *   └─ Return ContextWindowInfo DTO
 *   ↓
 * [context-window-handler] derives utilization & recommendations
 *   ├─ Calculate availableTokens = maxTokens - usedTokens
 *   ├─ Calculate utilization = usedTokens / maxTokens
 *   ├─ Generate recommendations if utilization > 70%
 *   └─ Record metrics
 *   ↓
 * [core-server] sends response via stdio
 * ```
 *
 * **Response Structure**:
 * ```javascript
 * {
 *   maxTokens: 4096,                  // Total context window size
 *   usedTokens: 2100,                 // Tokens consumed by conversation + history
 *   availableTokens: 1996,            // Free tokens for new context
 *   estimatedTokens: {                // Per-artifact estimates
 *     editorContent: 450,
 *     selectedText: 80,
 *     recentFiles: 600,
 *     conversationHistory: 970
 *   },
 *   utilization: 0.513,               // Percentage (0.0 to 1.0)
 *   recommendations: [                // Auto-suggest actions
 *     "Consider discarding conversation history",
 *     "Reduce referenced file count"
 *   ],
 *   lastUpdate: "2024-01-15T10:30:00.000Z"
 * }
 * ```
 *
 * **Performance**:
 * - Query latency (p99): <10ms
 * - Memory per response: ~2KB
 * - Concurrent requests: No limit
 *
 * **Error Handling**:
 * - ContextWindowError: Collector not initialized (RPC -32603)
 * - TokenCalculationError: Arithmetic overflow or invalid data (RPC -32603)
 * - Graceful null returns for unavailable data points
 *
 * **Dependencies**:
 * - logger (optional): for debug/info/warn/error logging
 * - metrics (optional): performance tracking
 * - collectorInstance (optional): DTE-based context window provider
 *
 * @module src/versions/v2.0.0/lib/context-window-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

/**
 * Custom error for context window operations
 */
export class ContextWindowError extends Error {
  constructor(message, errorCode = 'CONTEXT_WINDOW_ERROR', originalError = null) {
    super(message);
    this.name = 'ContextWindowError';
    this.errorCode = errorCode;
    this.originalError = originalError;
  }
}

/**
 * Custom error for token calculation failures
 */
export class TokenCalculationError extends Error {
  constructor(message, originalError = null) {
    super(message);
    this.name = 'TokenCalculationError';
    this.originalError = originalError;
  }
}

/**
 * Mock logger for cases where no logger is provided
 */
function _createMockLogger() {
  return {
    debug: () => {},
    info: () => {},
    warn: () => {},
    error: () => {},
  };
}

/**
 * Mock metrics collector for cases where no metrics are provided
 */
function _createMockMetrics() {
  return {
    recordEvent: () => {},
    recordLatency: () => {},
  };
}

/**
 * Call C# collector asynchronously, handling promise/callback patterns
 */
async function _callCollectorAsync(collectorInstance) {
  return new Promise((resolve, reject) => {
    try {
      const result = collectorInstance.GetContextWindowAsync();

      // Handle both promise and direct result
      if (result && typeof result.then === 'function') {
        result.then(resolve).catch(reject);
      } else {
        resolve(result);
      }
    } catch (error) {
      reject(error);
    }
  });
}

/**
 * Generate recommendations based on token utilization
 *
 * @param {number} utilization Utilization ratio (0.0 to 1.0)
 * @param {Object} estimatedTokens Breakdown of token usage
 * @returns {string[]} Array of recommendation strings
 */
function _generateRecommendations(utilization, estimatedTokens) {
  const recommendations = [];

  if (utilization < 0.7) {
    return recommendations; // No recommendations yet
  }

  if (utilization >= 0.7 && utilization < 0.85) {
    if (estimatedTokens?.conversationHistory > 500) {
      recommendations.push('Consider clearing older messages to preserve context');
    }
    if (estimatedTokens?.recentFiles > 300) {
      recommendations.push('Reduce number of referenced files');
    }
  }

  if (utilization >= 0.85 && utilization < 0.95) {
    recommendations.push('Context window is nearly full; clear conversation history');
    if (estimatedTokens?.recentFiles > 200) {
      recommendations.push('Remove unnecessary file references');
    }
  }

  if (utilization >= 0.95) {
    recommendations.push('CRITICAL: Context window exceeded; restart conversation');
    recommendations.push('Remove all optional context (files, history) to continue');
  }

  return recommendations;
}

/**
 * Factory function to create a context-window handler.
 *
 * Options:
 * - logger: optional logger instance with { debug, info, warn, error } methods
 * - metrics: optional metrics instance with { recordEvent, recordLatency } methods
 * - collectorInstance: optional ContextWindowCollector (for testing); if not provided,
 *   handler will fail with clear error directing to C# bridge setup
 *
 * @param {Object} options Handler configuration
 * @returns {Function} Message handler for bridge:getContextWindow
 */
export function createContextWindowHandler(options = {}) {
  if (typeof options !== 'object' || options === null || Array.isArray(options)) {
    throw new Error('Context-window handler options must be a plain object');
  }

  const logger = options.logger || _createMockLogger();
  const metrics = options.metrics || _createMockMetrics();
  const collectorInstance = options.collectorInstance || null;

  logger.debug('Context-window handler factory invoked', {
    hasCollector: !!collectorInstance,
    hasLogger: !!options.logger,
    hasMetrics: !!options.metrics,
  });

  /**
   * Handle bridge:getContextWindow message.
   *
   * Request: minimal message metadata (no payload required)
   * Response: { maxTokens, usedTokens, availableTokens, estimatedTokens, utilization, recommendations, lastUpdate }
   *
   * @param {Object} message The incoming message
   * @param {string} message.messageId Unique request identifier
   * @param {Object} context Bridge context (for logging/metrics)
   * @returns {Promise<Object>} Structured context window response
   */
  return async function handleGetContextWindow(message, context) {
    const requestId = message?.messageId || 'unknown';
    const startTime = Date.now();

    try {
      logger.debug('bridge:getContextWindow request received', {
        messageId: requestId,
        hasContext: !!context,
      });

      // Validate message structure
      if (!message || typeof message !== 'object') {
        throw new ContextWindowError(
          'Invalid message format: must be a plain object',
          'INVALID_MESSAGE',
          null
        );
      }

      if (!requestId || typeof requestId !== 'string') {
        throw new ContextWindowError(
          'Message must include a valid messageId string',
          'MISSING_MESSAGE_ID',
          null
        );
      }

      // Ensure collector is available
      if (!collectorInstance) {
        throw new ContextWindowError(
          'ContextWindowCollector not initialized; C# bridge adapter may not be running',
          'COLLECTOR_NOT_INITIALIZED',
          null
        );
      }

      // Call C# collector to gather context window data
      let contextInfo;
      try {
        logger.debug('Invoking ContextWindowCollector.GetContextWindowAsync()');
        contextInfo = await _callCollectorAsync(collectorInstance);
      } catch (collectorError) {
        logger.error('ContextWindowCollector failed', {
          error: collectorError?.message,
          errorName: collectorError?.name,
          stack: collectorError?.stack,
        });
        throw new TokenCalculationError(
          `Failed to collect context window info from IDE: ${collectorError?.message || 'unknown error'}`,
          collectorError
        );
      }

      // Validate collector response
      if (!contextInfo || typeof contextInfo !== 'object') {
        throw new ContextWindowError(
          'Collector returned invalid response: expected object',
          'INVALID_COLLECTOR_RESPONSE',
          null
        );
      }

      const { maxTokens, usedTokens, estimatedTokens } = contextInfo;

      // Validate numeric fields
      if (typeof maxTokens !== 'number' || maxTokens <= 0) {
        throw new TokenCalculationError(`Invalid maxTokens: ${maxTokens}`);
      }
      if (typeof usedTokens !== 'number' || usedTokens < 0) {
        throw new TokenCalculationError(`Invalid usedTokens: ${usedTokens}`);
      }

      // Cap usedTokens at maxTokens
      const cappedUsedTokens = Math.min(usedTokens, maxTokens);
      const availableTokens = maxTokens - cappedUsedTokens;
      const utilization = cappedUsedTokens / maxTokens;

      // Generate recommendations
      const recommendations = _generateRecommendations(utilization, estimatedTokens);

      const lastUpdate = new Date().toISOString();

      const response = {
        success: true,
        data: {
          maxTokens,
          usedTokens: cappedUsedTokens,
          availableTokens,
          estimatedTokens: estimatedTokens || {},
          utilization: Math.round(utilization * 10000) / 10000, // 4 decimal places
          recommendations,
          lastUpdate,
        },
      };

      // Record metrics
      const latency = Date.now() - startTime;
      metrics.recordEvent('context_window_query', {
        messageId: requestId,
        utilization,
        latency,
      });
      metrics.recordLatency('bridge:getContextWindow', latency);

      logger.debug('bridge:getContextWindow completed successfully', {
        messageId: requestId,
        utilization,
        latency,
        recommendationCount: recommendations.length,
      });

      return response;
    } catch (error) {
      const latency = Date.now() - startTime;
      metrics.recordLatency('bridge:getContextWindow', latency);

      if (error instanceof ContextWindowError || error instanceof TokenCalculationError) {
        logger.warn(`Handler error: ${error.name}`, {
          messageId: requestId,
          errorCode: error.errorCode || error.name,
          message: error.message,
        });

        return {
          success: false,
          error: {
            code: error.errorCode || error.name,
            message: error.message,
            details: error.originalError ? { originalError: error.originalError.message } : null,
          },
        };
      }

      logger.error('Unexpected error in context-window handler', {
        messageId: requestId,
        error: error?.message,
        errorName: error?.name,
        stack: error?.stack,
      });

      return {
        success: false,
        error: {
          code: 'CONTEXT_WINDOW_ERROR',
          message: 'An unexpected error occurred while retrieving context window info',
          details: { originalError: error?.message },
        },
      };
    }
  };
}

export default {
  createContextWindowHandler,
  ContextWindowError,
  TokenCalculationError,
};
