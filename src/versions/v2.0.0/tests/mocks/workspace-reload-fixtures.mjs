#!/usr/bin/env node

/**
 * Workspace-Reload Handler Test Fixtures & Mocks (Step 94)
 *
 * Provides reusable test fixtures, mock factories, and payload templates
 * for workspace-reload handler testing.
 *
 * Exports:
 * - Payload fixtures (valid and invalid)
 * - Mock cache instances with state tracking
 * - Mock logger/metrics
 * - Helper utilities for test setup
 *
 * @file src/versions/v2.0.0/tests/mocks/workspace-reload-fixtures.mjs
 */

import { ReloadScope } from '../../lib/workspace-reload-handler.mjs';

/**
 * Valid payload fixtures for all scope types.
 * @type {Object}
 */
export const validPayloads = {
  configScope: {
    data: { scope: ReloadScope.CONFIG },
  },
  symbolsScope: {
    data: { scope: ReloadScope.SYMBOLS },
  },
  diagnosticsScope: {
    data: { scope: ReloadScope.DIAGNOSTICS },
  },
  documentsScope: {
    data: { scope: ReloadScope.DOCUMENTS },
  },
  fullScope: {
    data: { scope: ReloadScope.FULL },
  },
  nullScope: {
    data: { scope: null },
  },
  undefinedScope: {
    data: { scope: undefined },
  },
  withFilePath: {
    data: { scope: ReloadScope.SYMBOLS, filePath: '/path/to/file.js' },
  },
  allScopesWithPath: {
    config: { data: { scope: ReloadScope.CONFIG, filePath: '/path/file.js' } },
    symbols: { data: { scope: ReloadScope.SYMBOLS, filePath: '/path/file.js' } },
    diagnostics: { data: { scope: ReloadScope.DIAGNOSTICS, filePath: '/path/file.js' } },
    documents: { data: { scope: ReloadScope.DOCUMENTS, filePath: '/path/file.js' } },
    full: { data: { scope: ReloadScope.FULL, filePath: '/path/file.js' } },
  },
};

/**
 * Invalid payload fixtures for validation testing.
 * @type {Object}
 */
export const invalidPayloads = {
  invalidScope: {
    data: { scope: 'invalid-scope' },
    expectedError: 'Invalid scope',
  },
  emptyStringScope: {
    data: { scope: '' },
    expectedError: 'Invalid scope',
  },
  numberScope: {
    data: { scope: 123 },
    expectedError: 'Invalid scope',
  },
  emptyFilePath: {
    data: { scope: ReloadScope.SYMBOLS, filePath: '' },
    expectedError: 'Invalid filePath',
  },
  whitespaceFilePath: {
    data: { scope: ReloadScope.SYMBOLS, filePath: '   ' },
    expectedError: 'Invalid filePath',
  },
  numberFilePath: {
    data: { scope: ReloadScope.SYMBOLS, filePath: 123 },
    expectedError: 'Invalid filePath',
  },
  arrayFilePath: {
    data: { scope: ReloadScope.SYMBOLS, filePath: ['/path'] },
    expectedError: 'Invalid filePath',
  },
  nullMessage: null,
  undefinedMessage: undefined,
  stringMessage: 'invalid',
};

/**
 * Factory for creating mock cache instances with state tracking.
 * @param {string} name - Cache name (for debugging)
 * @param {Object} options - Configuration options
 * @param {boolean} options.shouldFail - If true, clear operations throw errors
 * @param {number} options.delay - Delay before clear operation completes (ms)
 * @returns {Object} Mock cache instance
 */
export function createMockCache(name = 'mock', options = {}) {
  const { shouldFail = false, delay = 0 } = options;

  return {
    name,
    cleared: false,
    clearCount: 0,
    failCount: 0,
    clearCallArgs: [],
    clearCacheParams: [],

    clearCache: async function (filePath) {
      if (delay > 0) {
        await new Promise((resolve) => setTimeout(resolve, delay));
      }

      if (shouldFail) {
        this.failCount++;
        throw new Error(`${this.name}.clearCache failed (simulated)`);
      }

      this.cleared = true;
      this.clearCount++;
      this.clearCallArgs.push(filePath);
      this.clearCacheParams.push({ method: 'clearCache', filePath, timestamp: Date.now() });
    },

    clear: async function (filePath) {
      if (delay > 0) {
        await new Promise((resolve) => setTimeout(resolve, delay));
      }

      if (shouldFail) {
        this.failCount++;
        throw new Error(`${this.name}.clear failed (simulated)`);
      }

      this.cleared = true;
      this.clearCount++;
      this.clearCallArgs.push(filePath);
      this.clearCacheParams.push({ method: 'clear', filePath, timestamp: Date.now() });
    },

    clearAll: async function (filePath) {
      if (delay > 0) {
        await new Promise((resolve) => setTimeout(resolve, delay));
      }

      if (shouldFail) {
        this.failCount++;
        throw new Error(`${this.name}.clearAll failed (simulated)`);
      }

      this.cleared = true;
      this.clearCount++;
      this.clearCallArgs.push(filePath);
      this.clearCacheParams.push({ method: 'clearAll', filePath, timestamp: Date.now() });
    },

    reset: function () {
      this.cleared = false;
      this.clearCount = 0;
      this.failCount = 0;
      this.clearCallArgs = [];
      this.clearCacheParams = [];
    },

    getState: function () {
      return {
        name: this.name,
        cleared: this.cleared,
        clearCount: this.clearCount,
        failCount: this.failCount,
        callArgs: this.clearCallArgs,
        params: this.clearCacheParams,
      };
    },
  };
}

/**
 * Factory for creating mock metrics instances.
 * @param {Object} options - Configuration options
 * @returns {Object} Mock metrics instance
 */
export function createMockMetrics(options = {}) {
  return {
    events: [],

    recordWorkspaceReload: function (event) {
      this.events.push({
        ...event,
        timestamp: Date.now(),
      });
    },

    getLastEvent: function () {
      return this.events[this.events.length - 1];
    },

    getEvents: function (filter = null) {
      if (!filter) {
        return this.events;
      }

      return this.events.filter((event) => {
        for (const [key, value] of Object.entries(filter)) {
          if (event[key] !== value) {
            return false;
          }
        }
        return true;
      });
    },

    getEventCount: function (filter = null) {
      return this.getEvents(filter).length;
    },

    reset: function () {
      this.events = [];
    },
  };
}

/**
 * Factory for creating mock logger instances.
 * @param {Object} options - Configuration options
 * @returns {Object} Mock logger instance
 */
export function createMockLogger(options = {}) {
  return {
    logs: {
      info: [],
      warn: [],
      error: [],
      debug: [],
    },

    info: function (msg) {
      this.logs.info.push({
        level: 'info',
        message: msg,
        timestamp: Date.now(),
      });
    },

    warn: function (msg) {
      this.logs.warn.push({
        level: 'warn',
        message: msg,
        timestamp: Date.now(),
      });
    },

    error: function (msg) {
      this.logs.error.push({
        level: 'error',
        message: msg,
        timestamp: Date.now(),
      });
    },

    debug: function (msg) {
      this.logs.debug.push({
        level: 'debug',
        message: msg,
        timestamp: Date.now(),
      });
    },

    getLog: function (level) {
      return this.logs[level] || [];
    },

    getLogMessages: function (level) {
      return this.getLog(level).map((entry) => entry.message);
    },

    findLog: function (level, searchText) {
      return this.getLogMessages(level).find((msg) => msg.includes(searchText));
    },

    reset: function () {
      this.logs = {
        info: [],
        warn: [],
        error: [],
        debug: [],
      };
    },

    getState: function () {
      return {
        infoCount: this.logs.info.length,
        warnCount: this.logs.warn.length,
        errorCount: this.logs.error.length,
        debugCount: this.logs.debug.length,
        totalCount:
          this.logs.info.length +
          this.logs.warn.length +
          this.logs.error.length +
          this.logs.debug.length,
      };
    },
  };
}

/**
 * Factory for creating complete test context with all mocks.
 * @param {Object} overrides - Partial context to override defaults
 * @returns {Object} Complete test context
 */
export function createTestContext(overrides = {}) {
  return {
    symbolExtractor: overrides.symbolExtractor || createMockCache('symbols'),
    documentProvider: overrides.documentProvider || createMockCache('documents'),
    diagnosticsCollector:
      overrides.diagnosticsCollector || createMockCache('diagnostics'),
    logger: overrides.logger || createMockLogger(),
    metrics: overrides.metrics || createMockMetrics(),
    ...overrides,
  };
}

/**
 * Utility to verify cache state after reload.
 * @param {Object} cache - Mock cache instance
 * @param {Object} expectations - Expected state
 * @param {number} expectations.clearCount - Expected number of clears
 * @param {boolean} expectations.wasCleared - Expected cleared flag
 * @param {Array} expectations.callArgs - Expected call arguments
 * @returns {boolean} True if all expectations met
 */
export function verifyCacheState(cache, expectations = {}) {
  const {
    clearCount = null,
    wasCleared = null,
    callArgs = null,
  } = expectations;

  if (clearCount !== null && cache.clearCount !== clearCount) {
    throw new Error(
      `Cache clear count mismatch: expected ${clearCount}, got ${cache.clearCount}`
    );
  }

  if (wasCleared !== null && cache.cleared !== wasCleared) {
    throw new Error(
      `Cache cleared flag mismatch: expected ${wasCleared}, got ${cache.cleared}`
    );
  }

  if (callArgs !== null && JSON.stringify(cache.clearCallArgs) !== JSON.stringify(callArgs)) {
    throw new Error(
      `Cache call args mismatch: expected ${JSON.stringify(callArgs)}, got ${JSON.stringify(cache.clearCallArgs)}`
    );
  }

  return true;
}

/**
 * Utility to verify handler response.
 * @param {Object} response - Handler response object
 * @param {Object} expectations - Expected response structure
 * @param {boolean} expectations.success - Expected success flag
 * @param {Array} expectations.reloadedScopes - Expected reloaded scopes
 * @param {number} expectations.filesAffected - Expected files affected count
 * @param {boolean} expectations.cacheCleared - Expected cache cleared flag
 * @returns {boolean} True if all expectations met
 */
export function verifyResponse(response, expectations = {}) {
  const {
    success = null,
    reloadedScopes = null,
    filesAffected = null,
    cacheCleared = null,
  } = expectations;

  if (success !== null && response.success !== success) {
    throw new Error(
      `Response success mismatch: expected ${success}, got ${response.success}`
    );
  }

  if (!response.data) {
    if (response.error) {
      throw new Error(
        `Response is an error: ${response.error.code} - ${response.error.message}`
      );
    }
    throw new Error('Response has no data and no error');
  }

  if (reloadedScopes !== null) {
    const actualScopes = response.data.reloadedScopes || [];
    if (JSON.stringify(actualScopes) !== JSON.stringify(reloadedScopes)) {
      throw new Error(
        `Reloaded scopes mismatch: expected ${JSON.stringify(reloadedScopes)}, got ${JSON.stringify(actualScopes)}`
      );
    }
  }

  if (filesAffected !== null && response.data.filesAffected !== filesAffected) {
    throw new Error(
      `Files affected mismatch: expected ${filesAffected}, got ${response.data.filesAffected}`
    );
  }

  if (cacheCleared !== null && response.data.cacheCleared !== cacheCleared) {
    throw new Error(
      `Cache cleared flag mismatch: expected ${cacheCleared}, got ${response.data.cacheCleared}`
    );
  }

  return true;
}

/**
 * Utility to setup default test scenario.
 * @returns {Object} Pre-configured test scenario
 */
export function createDefaultScenario() {
  return {
    context: createTestContext(),
    payloads: validPayloads,
    invalidPayloads,
    caches: {
      symbolExtractor: createMockCache('symbols'),
      documentProvider: createMockCache('documents'),
      diagnosticsCollector: createMockCache('diagnostics'),
    },
    mocks: {
      logger: createMockLogger(),
      metrics: createMockMetrics(),
    },
    helpers: {
      verifyCacheState,
      verifyResponse,
    },
  };
}
