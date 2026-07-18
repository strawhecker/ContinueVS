#!/usr/bin/env node

/**
 * Bridge C#/Node.js boundary for ModelInfoCollector
 *
 * Provides factory functions to obtain a ModelInfoCollector instance,
 * handling both in-process (mock/test) and out-of-process (IPC) scenarios.
 *
 * **Architecture**:
 * - In-process (tests): Returns mock collector
 * - Out-of-process (production): Creates IPC proxy to C# collector
 * - Graceful degradation: Returns null if initialization fails
 *
 * **Usage**:
 * ```javascript
 * const collector = getModelInfoCollector();
 * const handler = createModelInfoHandler({ collector });
 * ```
 *
 * @module src/versions/v2.0.0/lib/model-info-collector-adapter.mjs
 * @version 1.0.0
 */

import { createMockModelInfoCollector } from '../tests/mocks/model-info-collector-mock.mjs';

// Environment flag to determine if running in test mode
const TEST_MODE = process.env.NODE_ENV === 'test' || process.env.BRIDGE_TEST_MODE === '1';

/**
 * Singleton cache for collector instance
 */
let _collectorInstance = null;
let _collectorInitialized = false;

/**
 * Gets or creates a ModelInfoCollector instance.
 *
 * In test mode: Returns mock collector
 * In production: Attempts to create IPC proxy to C# collector
 * On failure: Returns null (graceful degradation)
 *
 * @returns {Object|null} ModelInfoCollector instance or null if unavailable
 *
 * @example
 * const collector = getModelInfoCollector();
 * if (collector) {
 *   const currentModel = await collector.GetCurrentModelAsync();
 * }
 */
export function getModelInfoCollector() {
  // Return cached instance if already initialized
  if (_collectorInitialized) {
    return _collectorInstance;
  }

  _collectorInitialized = true;

  try {
    // In test mode, return mock collector
    if (TEST_MODE) {
      const scenario = process.env.BRIDGE_MOCK_SCENARIO || 'multi-provider';
      _collectorInstance = createMockModelInfoCollector(scenario);
      return _collectorInstance;
    }

    // In production, attempt to create IPC proxy
    _collectorInstance = _createIpcCollectorProxy();
    return _collectorInstance;
  } catch (error) {
    console.error(`Failed to initialize ModelInfoCollector: ${error.message}`);
    // Graceful degradation: return null
    return null;
  }
}

/**
 * Resets the collector cache (for testing only).
 *
 * @internal
 */
export function resetCollectorCache() {
  _collectorInstance = null;
  _collectorInitialized = false;
}

/**
 * Creates an IPC proxy to the C# ModelInfoCollector.
 *
 * This would be implemented to:
 * 1. Check if C# bridge process is running
 * 2. Create RPC calls to C# collector methods
 * 3. Handle async C# Task results
 * 4. Map C# DTOs to JavaScript objects
 *
 * **Note**: Current implementation returns null as placeholder.
 * Full IPC implementation would require:
 * - Reference to IDictionary<string, Delegate> or similar RPC registry
 * - JSON serialization/deserialization of C# DTO types
 * - Async/await wrapping for C# Task results
 *
 * @returns {Object|null} IPC proxy or null if not available
 * @internal
 */
function _createIpcCollectorProxy() {
  try {
    // Check if running in IPC context
    const rpcRegistry = global.__bridgeRpcRegistry;
    if (!rpcRegistry || !rpcRegistry.GetModelInfoCollector) {
      // IPC not available; graceful degradation
      return null;
    }

    // Get the C# collector instance from RPC registry
    const csharpCollector = rpcRegistry.GetModelInfoCollector();
    if (!csharpCollector) {
      return null;
    }

    // Create a proxy that adapts C# async patterns to Node.js promises
    return _createCollectorAdapter(csharpCollector);
  } catch (error) {
    console.error(`IPC collector initialization failed: ${error.message}`);
    return null;
  }
}

/**
 * Wraps a C# collector with Node.js-compatible async interface.
 *
 * Handles:
 * - C# Task<T> → JavaScript Promise<T>
 * - C# null references → JavaScript null
 * - DTO serialization/deserialization
 *
 * @param {Object} csharpCollector The raw C# collector instance
 * @returns {Object} Adapter with promise-based methods
 * @internal
 */
function _createCollectorAdapter(csharpCollector) {
  return {
    /**
     * Calls GetCurrentModelAsync on the C# collector
     */
    async GetCurrentModelAsync() {
      try {
        const result = await csharpCollector.GetCurrentModelAsync?.();
        return result ? _adaptModelInfoDto(result) : null;
      } catch (error) {
        throw new Error(`Failed to get current model: ${error.message}`);
      }
    },

    /**
     * Calls GetAvailableModelsAsync on the C# collector
     */
    async GetAvailableModelsAsync() {
      try {
        const result = await csharpCollector.GetAvailableModelsAsync?.();
        if (!result || !Array.isArray(result)) {
          return [];
        }
        return result.map(m => _adaptModelInfoDto(m));
      } catch (error) {
        console.error(`Failed to get available models: ${error.message}`);
        // Graceful degradation
        return [];
      }
    },

    /**
     * Calls GetModelCapabilitiesAsync on the C# collector
     */
    async GetModelCapabilitiesAsync(provider) {
      try {
        const result = await csharpCollector.GetModelCapabilitiesAsync?.(provider);
        return result || _getDefaultCapabilities();
      } catch (error) {
        console.error(`Failed to get capabilities: ${error.message}`);
        return _getDefaultCapabilities();
      }
    },

    /**
     * Calls GetTokenLimitsAsync on the C# collector
     */
    async GetTokenLimitsAsync(provider, model) {
      try {
        const result = await csharpCollector.GetTokenLimitsAsync?.(provider, model);
        return result || _getDefaultTokenLimits();
      } catch (error) {
        console.error(`Failed to get token limits: ${error.message}`);
        return _getDefaultTokenLimits();
      }
    }
  };
}

/**
 * Adapts a C# ModelInfoDto to JavaScript object.
 * Converts C# property names (PascalCase) to JavaScript (camelCase).
 *
 * @param {Object} csharpDto The C# ModelInfoDto instance
 * @returns {Object} Adapted JavaScript object
 * @internal
 */
function _adaptModelInfoDto(csharpDto) {
  if (!csharpDto) return null;

  return {
    provider: csharpDto.Provider || 'unknown',
    model: csharpDto.Model || 'unknown',
    title: csharpDto.Title || 'Unknown Model',
    apiBase: csharpDto.ApiBase || null,
    apiKey: csharpDto.ApiKey || null
  };
}

/**
 * Default model capabilities (fallback)
 * @internal
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
 * Default token limits (fallback)
 * @internal
 */
function _getDefaultTokenLimits() {
  return {
    maxInputTokens: 3072,
    maxOutputTokens: 1024,
    totalContextTokens: 4096
  };
}
