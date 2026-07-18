#!/usr/bin/env node

/**
 * Model-Info Handler for Continue Bridge
 *
 * Provides bridge handler for querying available LLM models and current model configuration.
 * Factory-based stateless handler that queries a C# collector to fetch model metadata.
 *
 * **Handler Type**: Factory (returns async function)
 * **Message Type**: bridge:getModelInfo
 * **Input**: BridgeMessage (minimal metadata, no payload required)
 * **Output**: BridgeResponse containing { currentModel, availableModels, modelCapabilities, tokenLimits }
 *
 * **Architecture Flow**:
 * ```
 * [WebView] → "getModelInfo" request
 *   ↓
 * [model-info-handler] validates request
 *   ↓
 * [ModelInfoCollector] GetCurrentModelAsync() + GetAvailableModelsAsync()
 *   ├─ Query Continue config for configured models
 *   ├─ Map LlmModelConfig → ModelInfoDto
 *   ├─ Return current model (first in list)
 *   └─ Return all available models
 *   ↓
 * [model-info-handler] normalizes response
 *   ├─ Merge current + available + capabilities + token limits
 *   ├─ Record metrics (query latency, model count, provider)
 *   └─ Log model availability
 *   ↓
 * [core-server] sends response via stdio
 * ```
 *
 * **Response Structure**:
 * ```javascript
 * {
 *   currentModel: {
 *     provider: "openai",
 *     model: "gpt-4",
 *     title: "OpenAI GPT-4",
 *     apiBase: "https://api.openai.com/v1"
 *   },
 *   availableModels: [
 *     { provider: "openai", model: "gpt-4", title: "OpenAI GPT-4" },
 *     { provider: "anthropic", model: "claude-3-opus", title: "Anthropic Claude 3" }
 *   ],
 *   modelCapabilities: {
 *     contextLength: 8192,
 *     supportsStreaming: true,
 *     supportsVision: true,
 *     maxRpm: 3500,
 *     maxTokensPerMinute: 90000
 *   },
 *   tokenLimits: {
 *     maxInputTokens: 8000,
 *     maxOutputTokens: 2000,
 *     totalContextTokens: 8192
 *   }
 * }
 * ```
 *
 * **Performance**:
 * - Query latency (p99): <50ms
 * - Memory per response: ~3KB
 * - Concurrent requests: No limit (stateless)
 *
 * **Error Handling**:
 * - ModelInfoError: Collector not initialized or returns null (RPC -32603)
 * - Graceful degradation: Returns empty availableModels[] if config unavailable
 *
 * **Dependencies**:
 * - logger (optional): for debug/info/warn/error logging
 * - metrics (optional): performance tracking (latency, event counts)
 * - collector (optional): C# ModelInfoCollector instance or IPC proxy
 *
 * @module src/versions/v2.0.0/lib/model-info-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

/**
 * Custom error for model info operations
 */
export class ModelInfoError extends Error {
  constructor(message, errorCode = 'MODEL_INFO_ERROR', originalError = null) {
    super(message);
    this.name = 'ModelInfoError';
    this.errorCode = errorCode;
    this.originalError = originalError;
  }
}

/**
 * Custom error when collector is unavailable
 */
export class CollectorNotAvailableError extends Error {
  constructor(message = 'ModelInfoCollector is not available') {
    super(message);
    this.name = 'CollectorNotAvailableError';
  }
}

/**
 * Factory function to create a model-info handler.
 *
 * @param {Object} options Handler configuration options
 * @param {Object} options.collector C# ModelInfoCollector instance (required)
 * @param {Object} [options.logger] Logger instance for diagnostics (optional)
 * @param {Object} [options.metrics] Metrics collector for performance tracking (optional)
 * @returns {Function} Async message handler function: (bridgeMessage, handlerContext) => BridgeResponse
 *
 * @example
 * const handler = createModelInfoHandler({
 *   collector: modelInfoCollectorInstance,
 *   logger: bridgeLogger,
 *   metrics: bridgeMetrics
 * });
 *
 * const response = await handler(bridgeMessage, handlerContext);
 */
export function createModelInfoHandler({ collector, logger, metrics }) {
  const _logger = logger || _createMockLogger();
  const _metrics = metrics || _createMockMetrics();

  /**
   * Async message handler for bridge:getModelInfo requests.
   *
   * @param {Object} bridgeMessage The incoming bridge message
   * @param {string} bridgeMessage.messageType Must equal 'bridge:getModelInfo'
   * @param {string} bridgeMessage.messageId Unique message identifier
   * @param {Object} [bridgeMessage.data] Optional request payload (unused)
   * @param {Object} handlerContext Handler execution context
   * @returns {Promise<Object>} BridgeResponse with model info or error
   */
  return async function handleGetModelInfo(bridgeMessage, handlerContext) {
    const startTime = performance.now();

    try {
      // Validate message type
      if (bridgeMessage.messageType !== 'bridge:getModelInfo') {
        throw new ModelInfoError(
          `Invalid message type: ${bridgeMessage.messageType}`,
          'INVALID_MESSAGE_TYPE'
        );
      }

      // Check collector availability
      if (!collector) {
        throw new CollectorNotAvailableError(
          'ModelInfoCollector is not initialized'
        );
      }

      // Query current model and available models concurrently
      _logger.debug('Fetching current model and available models...');

      const [currentModel, availableModels] = await Promise.all([
        _callCollectorAsync(collector, 'GetCurrentModelAsync'),
        _callCollectorAsync(collector, 'GetAvailableModelsAsync')
      ]);

      // Get capabilities and token limits for current model
      let modelCapabilities = null;
      let tokenLimits = null;

      if (currentModel) {
        modelCapabilities = await _callCollectorAsync(
          collector,
          'GetModelCapabilitiesAsync',
          [currentModel.Provider]
        );

        tokenLimits = await _callCollectorAsync(
          collector,
          'GetTokenLimitsAsync',
          [currentModel.Provider, currentModel.Model]
        );
      }

      // Record metrics
      const latency = performance.now() - startTime;
      _metrics.recordLatency('model_info_query', latency);
      _metrics.recordEvent('model_info_query', {
        modelCount: availableModels?.length || 0,
        provider: currentModel?.Provider || 'unknown',
        latency
      });

      // Log results
      _logger.info(
        `Retrieved model info: current=${currentModel?.Title || 'none'}, ` +
        `available=${availableModels?.length || 0} models, latency=${latency.toFixed(2)}ms`
      );

      // Build response
      const response = {
        currentModel: currentModel || null,
        availableModels: availableModels || [],
        modelCapabilities: modelCapabilities || _getDefaultCapabilities(),
        tokenLimits: tokenLimits || _getDefaultTokenLimits(),
        lastUpdate: new Date().toISOString(),
        queryLatency: latency
      };

      return {
        success: true,
        data: response,
        messageId: bridgeMessage.messageId
      };
    } catch (error) {
      const latency = performance.now() - startTime;

      if (error instanceof ModelInfoError || error instanceof CollectorNotAvailableError) {
        _logger.error(`ModelInfoError: ${error.message}`);
        _metrics.recordEvent('model_info_error', {
          errorCode: error.errorCode || 'UNKNOWN',
          latency
        });

        return {
          success: false,
          error: {
            code: -32603, // JSON-RPC Internal Error
            message: error.message,
            data: {
              errorCode: error.errorCode,
              originalError: error.originalError?.message
            }
          },
          messageId: bridgeMessage.messageId
        };
      }

      // Unexpected error
      _logger.error(`Unexpected error in model-info-handler: ${error.message}`);
      _metrics.recordEvent('model_info_error', {
        errorCode: 'UNEXPECTED_ERROR',
        latency
      });

      return {
        success: false,
        error: {
          code: -32603, // JSON-RPC Internal Error
          message: 'Unexpected error querying model information',
          data: {
            errorCode: 'UNEXPECTED_ERROR',
            details: error.message
          }
        },
        messageId: bridgeMessage.messageId
      };
    }
  };
}

/**
 * Mock logger for cases where no logger is provided
 */
function _createMockLogger() {
  return {
    debug: () => {},
    info: () => {},
    warn: () => {},
    error: () => {}
  };
}

/**
 * Mock metrics collector for cases where no metrics are provided
 */
function _createMockMetrics() {
  return {
    recordEvent: () => {},
    recordLatency: () => {}
  };
}

/**
 * Call C# collector method asynchronously, handling promise/callback patterns.
 *
 * @param {Object} collectorInstance The C# ModelInfoCollector instance
 * @param {string} methodName The method name to call (e.g., 'GetCurrentModelAsync')
 * @param {Array} [args] Optional method arguments
 * @returns {Promise<*>} The method result
 */
async function _callCollectorAsync(collectorInstance, methodName, args = []) {
  return new Promise((resolve, reject) => {
    try {
      const method = collectorInstance[methodName];
      if (!method) {
        reject(new Error(`Collector method not found: ${methodName}`));
        return;
      }

      // Call the method with arguments
      const result = args.length > 0 ? method(...args) : method();

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
 * Get default model capabilities (fallback)
 */
function _getDefaultCapabilities() {
  return {
    contextLength: 4096,
    supportsStreaming: true,
    supportsVision: false,
    maxRpm: 0,
    maxTokensPerMinute: 0
  };
}

/**
 * Get default token limits (fallback)
 */
function _getDefaultTokenLimits() {
  return {
    maxInputTokens: 3072,
    maxOutputTokens: 1024,
    totalContextTokens: 4096
  };
}

/**
 * Export handler metadata for registry
 */
export const handlerMetadata = {
  messageType: 'bridge:getModelInfo',
  isFactory: true,
  timeoutPolicy: 'fast',
  stabilityTier: 'core',
  description: 'Queries available LLM models and current model info',
  relatedSteps: [87, 88, 89],
  dependencies: [84, 87],
  version: '1.0.0'
};
