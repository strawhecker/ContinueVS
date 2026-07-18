#!/usr/bin/env node

/**
 * Mock fixtures for model-info-handler tests
 *
 * Provides factory functions to create mock ModelInfoCollector instances
 * with spy capabilities for unit and integration testing.
 *
 * Usage in tests:
 * ```javascript
 * import { createMockModelInfoCollector } from './mocks/model-info-collector-mock.mjs';
 *
 * const mockCollector = createMockModelInfoCollector('openai-only');
 * const handler = createModelInfoHandler({ collector: mockCollector });
 * const response = await handler(bridgeMessage, context);
 * ```
 *
 * @module src/versions/v2.0.0/tests/mocks/model-info-collector-mock.mjs
 * @version 1.0.0
 */

/**
 * Test scenario: Single OpenAI model configured
 */
const FIXTURE_OPENAI_ONLY = {
  currentModel: {
    provider: 'openai',
    model: 'gpt-4',
    title: 'OpenAI GPT-4',
    apiBase: 'https://api.openai.com/v1',
    apiKey: null
  },
  availableModels: [
    {
      provider: 'openai',
      model: 'gpt-4',
      title: 'OpenAI GPT-4',
      apiBase: 'https://api.openai.com/v1',
      apiKey: null
    }
  ],
  capabilities: {
    contextLength: 8192,
    supportsStreaming: true,
    supportsVision: true,
    maxRpm: 3500,
    maxTokensPerMinute: 90000
  },
  tokenLimits: {
    maxInputTokens: 8000,
    maxOutputTokens: 2000,
    totalContextTokens: 8192
  }
};

/**
 * Test scenario: OpenAI + Anthropic models configured
 */
const FIXTURE_MULTI_PROVIDER = {
  currentModel: {
    provider: 'openai',
    model: 'gpt-4',
    title: 'OpenAI GPT-4',
    apiBase: 'https://api.openai.com/v1',
    apiKey: null
  },
  availableModels: [
    {
      provider: 'openai',
      model: 'gpt-4',
      title: 'OpenAI GPT-4',
      apiBase: 'https://api.openai.com/v1',
      apiKey: null
    },
    {
      provider: 'anthropic',
      model: 'claude-3-opus',
      title: 'Anthropic Claude 3 Opus',
      apiBase: 'https://api.anthropic.com',
      apiKey: '<redacted>'
    }
  ],
  capabilities: {
    contextLength: 8192,
    supportsStreaming: true,
    supportsVision: true,
    maxRpm: 3500,
    maxTokensPerMinute: 90000
  },
  tokenLimits: {
    maxInputTokens: 8000,
    maxOutputTokens: 2000,
    totalContextTokens: 8192
  }
};

/**
 * Test scenario: No models configured (empty config)
 */
const FIXTURE_NO_MODELS = {
  currentModel: null,
  availableModels: [],
  capabilities: {
    contextLength: 4096,
    supportsStreaming: true,
    supportsVision: false,
    maxRpm: 0,
    maxTokensPerMinute: 0
  },
  tokenLimits: {
    maxInputTokens: 3072,
    maxOutputTokens: 1024,
    totalContextTokens: 4096
  }
};

/**
 * Creates a mock ModelInfoCollector instance for testing.
 *
 * @param {string} [scenario='openai-only'] Test scenario name
 *   - 'openai-only': Single OpenAI GPT-4 model
 *   - 'multi-provider': OpenAI + Anthropic models
 *   - 'no-models': Empty config (no models configured)
 * @returns {Object} Mock collector with GetCurrentModelAsync, GetAvailableModelsAsync, etc.
 *
 * @example
 * const mockCollector = createMockModelInfoCollector('multi-provider');
 * const currentModel = await mockCollector.GetCurrentModelAsync();
 * // → { provider: 'openai', model: 'gpt-4', ... }
 */
export function createMockModelInfoCollector(scenario = 'openai-only') {
  let fixture;

  switch (scenario) {
    case 'openai-only':
      fixture = FIXTURE_OPENAI_ONLY;
      break;
    case 'multi-provider':
      fixture = FIXTURE_MULTI_PROVIDER;
      break;
    case 'no-models':
      fixture = FIXTURE_NO_MODELS;
      break;
    default:
      throw new Error(`Unknown scenario: ${scenario}`);
  }

  // Create a mock collector with spy capabilities
  const mock = {
    scenario,
    fixture,

    // Call counters for spy verification
    callCounts: {
      getCurrentModel: 0,
      getAvailableModels: 0,
      getModelCapabilities: 0,
      getTokenLimits: 0
    },

    // Last arguments for assertion verification
    lastCallArgs: {
      getCurrentModel: null,
      getAvailableModels: null,
      getModelCapabilities: null,
      getTokenLimits: null
    },

    // Error injection
    errorToThrow: null,

    /**
     * Returns the current active model
     */
    async GetCurrentModelAsync() {
      this.callCounts.getCurrentModel++;
      this.lastCallArgs.getCurrentModel = {};

      if (this.errorToThrow) {
        throw this.errorToThrow;
      }

      return fixture.currentModel;
    },

    /**
     * Returns all available configured models
     */
    async GetAvailableModelsAsync() {
      this.callCounts.getAvailableModels++;
      this.lastCallArgs.getAvailableModels = {};

      if (this.errorToThrow) {
        throw this.errorToThrow;
      }

      return fixture.availableModels;
    },

    /**
     * Returns capabilities for a specific provider
     */
    async GetModelCapabilitiesAsync(provider) {
      this.callCounts.getModelCapabilities++;
      this.lastCallArgs.getModelCapabilities = { provider };

      if (this.errorToThrow) {
        throw this.errorToThrow;
      }

      // Return provider-specific capabilities or default
      if (provider === 'anthropic') {
        return {
          contextLength: 100000,
          supportsStreaming: true,
          supportsVision: true,
          maxRpm: 50,
          maxTokensPerMinute: 40000
        };
      }

      return fixture.capabilities;
    },

    /**
     * Returns token limits for a specific model
     */
    async GetTokenLimitsAsync(provider, model) {
      this.callCounts.getTokenLimits++;
      this.lastCallArgs.getTokenLimits = { provider, model };

      if (this.errorToThrow) {
        throw this.errorToThrow;
      }

      // Return model-specific limits
      if (provider === 'openai' && model?.includes('turbo')) {
        return {
          maxInputTokens: 128000,
          maxOutputTokens: 4096,
          totalContextTokens: 128000
        };
      }

      return fixture.tokenLimits;
    },

    /**
     * Test helper: Check if method was called
     */
    wasCalled(methodName) {
      const key = `${methodName[0].toLowerCase()}${methodName.slice(1)}`;
      return this.callCounts[key] > 0;
    },

    /**
     * Test helper: Get call count for method
     */
    getCallCount(methodName) {
      const key = `${methodName[0].toLowerCase()}${methodName.slice(1)}`;
      return this.callCounts[key] || 0;
    },

    /**
     * Test helper: Get last arguments for method
     */
    getLastCallArgs(methodName) {
      const key = `${methodName[0].toLowerCase()}${methodName.slice(1)}`;
      return this.lastCallArgs[key] || null;
    },

    /**
     * Test helper: Reset call counts and arguments
     */
    resetCallTracker() {
      for (const key in this.callCounts) {
        this.callCounts[key] = 0;
      }
      for (const key in this.lastCallArgs) {
        this.lastCallArgs[key] = null;
      }
    },

    /**
     * Test helper: Configure error injection
     */
    throwError(error) {
      this.errorToThrow = error;
      return this;
    },

    /**
     * Test helper: Clear error injection
     */
    clearError() {
      this.errorToThrow = null;
      return this;
    }
  };

  return mock;
}

/**
 * Creates a mock logger for testing
 *
 * @returns {Object} Mock logger with spy capabilities
 */
export function createMockLogger() {
  return {
    logs: [],

    debug(message) {
      this.logs.push({ level: 'debug', message });
    },

    info(message) {
      this.logs.push({ level: 'info', message });
    },

    warn(message) {
      this.logs.push({ level: 'warn', message });
    },

    error(message) {
      this.logs.push({ level: 'error', message });
    },

    getLogs(level) {
      if (!level) return this.logs;
      return this.logs.filter(log => log.level === level);
    },

    hasLog(level, messageSubstring) {
      return this.logs.some(
        log => log.level === level && log.message.includes(messageSubstring)
      );
    },

    clear() {
      this.logs = [];
    }
  };
}

/**
 * Creates a mock metrics collector for testing
 *
 * @returns {Object} Mock metrics with spy capabilities
 */
export function createMockMetrics() {
  return {
    events: [],
    latencies: [],

    recordEvent(eventName, data) {
      this.events.push({ eventName, data, timestamp: Date.now() });
    },

    recordLatency(operation, latency) {
      this.latencies.push({ operation, latency, timestamp: Date.now() });
    },

    getEvents(eventName) {
      if (!eventName) return this.events;
      return this.events.filter(e => e.eventName === eventName);
    },

    getLatencies(operation) {
      if (!operation) return this.latencies;
      return this.latencies.filter(l => l.operation === operation);
    },

    getAverageLatency(operation) {
      const latencies = this.getLatencies(operation);
      if (latencies.length === 0) return 0;
      const sum = latencies.reduce((acc, l) => acc + l.latency, 0);
      return sum / latencies.length;
    },

    clear() {
      this.events = [];
      this.latencies = [];
    }
  };
}

/**
 * Export all fixtures for direct access
 */
export const fixtures = {
  FIXTURE_OPENAI_ONLY,
  FIXTURE_MULTI_PROVIDER,
  FIXTURE_NO_MODELS
};
