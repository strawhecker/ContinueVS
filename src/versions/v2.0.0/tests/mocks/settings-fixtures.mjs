#!/usr/bin/env node

/**
 * Settings Fixtures and Mocks (Step 95)
 *
 * Provides test fixtures, mock implementations, and helper functions
 * for settings-sync handler testing.
 *
 * @module src/versions/v2.0.0/tests/mocks/settings-fixtures.mjs
 */

/**
 * Valid settings with all required and optional fields
 */
export const VALID_SETTINGS_FULL = {
  model: 'gpt-4',
  provider: 'openai',
  temperature: 0.7,
  contextWindow: 8192,
  maxTokens: 2048,
  systemPrompt: 'You are a helpful coding assistant.',
  endpoint: 'https://api.openai.com/v1/chat/completions',
};

/**
 * Valid settings with only required fields
 */
export const VALID_SETTINGS_MINIMAL = {
  model: 'claude-3-opus',
  provider: 'anthropic',
};

/**
 * Valid settings with different model
 */
export const VALID_SETTINGS_ALT_MODEL = {
  model: 'llama-2-70b',
  provider: 'local',
  temperature: 0.5,
  contextWindow: 4096,
  maxTokens: 1024,
};

/**
 * Valid settings with different provider
 */
export const VALID_SETTINGS_ALT_PROVIDER = {
  model: 'mistral-large',
  provider: 'mistral',
  temperature: 0.8,
  endpoint: 'https://api.mistral.ai/v1/chat/completions',
};

/**
 * Invalid settings: missing required model field
 */
export const INVALID_SETTINGS_MISSING_MODEL = {
  provider: 'openai',
  temperature: 0.7,
};

/**
 * Invalid settings: missing required provider field
 */
export const INVALID_SETTINGS_MISSING_PROVIDER = {
  model: 'gpt-4',
  temperature: 0.7,
};

/**
 * Invalid settings: temperature out of range (too high)
 */
export const INVALID_SETTINGS_TEMP_HIGH = {
  model: 'gpt-4',
  provider: 'openai',
  temperature: 1.5,
};

/**
 * Invalid settings: temperature out of range (negative)
 */
export const INVALID_SETTINGS_TEMP_LOW = {
  model: 'gpt-4',
  provider: 'openai',
  temperature: -0.1,
};

/**
 * Invalid settings: contextWindow too small
 */
export const INVALID_SETTINGS_CTX_SMALL = {
  model: 'gpt-4',
  provider: 'openai',
  contextWindow: 100,
};

/**
 * Invalid settings: contextWindow too large
 */
export const INVALID_SETTINGS_CTX_LARGE = {
  model: 'gpt-4',
  provider: 'openai',
  contextWindow: 300000,
};

/**
 * Invalid settings: maxTokens out of range
 */
export const INVALID_SETTINGS_TOKENS_INVALID = {
  model: 'gpt-4',
  provider: 'openai',
  maxTokens: 10000,
};

/**
 * Invalid settings: wrong type for model field
 */
export const INVALID_SETTINGS_MODEL_WRONG_TYPE = {
  model: 123,
  provider: 'openai',
};

/**
 * Invalid settings: wrong type for temperature field
 */
export const INVALID_SETTINGS_TEMP_WRONG_TYPE = {
  model: 'gpt-4',
  provider: 'openai',
  temperature: 'high',
};

/**
 * Invalid settings: systemPrompt exceeds max length
 */
export const INVALID_SETTINGS_PROMPT_TOO_LONG = {
  model: 'gpt-4',
  provider: 'openai',
  systemPrompt: 'a'.repeat(10001),
};

/**
 * Invalid settings: unknown field
 */
export const INVALID_SETTINGS_UNKNOWN_FIELD = {
  model: 'gpt-4',
  provider: 'openai',
  unknownField: 'should-fail',
};

/**
 * Empty settings object
 */
export const EMPTY_SETTINGS = {};

/**
 * Null settings
 */
export const NULL_SETTINGS = null;

/**
 * Settings as non-object (array)
 */
export const INVALID_SETTINGS_ARRAY = ['gpt-4', 'openai'];

/**
 * Settings as non-object (string)
 */
export const INVALID_SETTINGS_STRING = 'invalid';

/**
 * Mock SettingsCollector for testing
 */
export function createMockSettingsCollector(initialSettings = null) {
  const state = {
    settings: initialSettings || { ...VALID_SETTINGS_MINIMAL },
    readCount: 0,
    writeCount: 0,
    writeHistory: [],
    throwOnRead: null,
    throwOnWrite: null,
  };

  return {
    readSettings: async () => {
      state.readCount++;
      if (state.throwOnRead) {
        throw state.throwOnRead;
      }
      return { ...state.settings };
    },

    writeSettings: async (newSettings) => {
      state.writeCount++;
      if (state.throwOnWrite) {
        throw state.throwOnWrite;
      }
      state.writeHistory.push({ timestamp: Date.now(), settings: { ...newSettings } });
      state.settings = { ...newSettings };
    },

    getState: () => state,
    setThrowOnRead: (err) => {
      state.throwOnRead = err;
    },
    setThrowOnWrite: (err) => {
      state.throwOnWrite = err;
    },
  };
}

/**
 * Mock Logger for testing
 */
export function createMockLogger() {
  const state = {
    logs: {
      info: [],
      warn: [],
      error: [],
    },
  };

  return {
    info: (msg, data) => {
      state.logs.info.push({ msg, data, timestamp: Date.now() });
    },

    warn: (msg, data) => {
      state.logs.warn.push({ msg, data, timestamp: Date.now() });
    },

    error: (msg, data) => {
      state.logs.error.push({ msg, data, timestamp: Date.now() });
    },

    getLogs: () => state.logs,
    getLastInfo: () => state.logs.info[state.logs.info.length - 1],
    getLastWarn: () => state.logs.warn[state.logs.warn.length - 1],
    getLastError: () => state.logs.error[state.logs.error.length - 1],
    clear: () => {
      state.logs.info = [];
      state.logs.warn = [];
      state.logs.error = [];
    },
  };
}

/**
 * Mock Metrics Collector for testing
 */
export function createMockMetrics() {
  const state = {
    events: [],
  };

  return {
    recordSettingsLoad: (event) => {
      state.events.push({ type: 'load', ...event, timestamp: Date.now() });
    },

    recordSettingsApply: (event) => {
      state.events.push({ type: 'apply', ...event, timestamp: Date.now() });
    },

    getEvents: () => state.events,
    getLastEvent: () => state.events[state.events.length - 1],
    getEventsByType: (type) => state.events.filter((e) => e.type === type),
    clear: () => {
      state.events = [];
    },
  };
}

/**
 * Creates a test message object
 */
export function createTestMessage(payload = {}) {
  return {
    type: 'request',
    id: Math.floor(Math.random() * 100000),
    method: 'bridge:loadSettings',
    payload,
  };
}

/**
 * Creates a test message for applySettings
 */
export function createApplySettingsMessage(settings) {
  return {
    type: 'request',
    id: Math.floor(Math.random() * 100000),
    method: 'bridge:applySettings',
    payload: { settings },
  };
}

export default {
  VALID_SETTINGS_FULL,
  VALID_SETTINGS_MINIMAL,
  VALID_SETTINGS_ALT_MODEL,
  VALID_SETTINGS_ALT_PROVIDER,
  INVALID_SETTINGS_MISSING_MODEL,
  INVALID_SETTINGS_MISSING_PROVIDER,
  INVALID_SETTINGS_TEMP_HIGH,
  INVALID_SETTINGS_TEMP_LOW,
  INVALID_SETTINGS_CTX_SMALL,
  INVALID_SETTINGS_CTX_LARGE,
  INVALID_SETTINGS_TOKENS_INVALID,
  INVALID_SETTINGS_MODEL_WRONG_TYPE,
  INVALID_SETTINGS_TEMP_WRONG_TYPE,
  INVALID_SETTINGS_PROMPT_TOO_LONG,
  INVALID_SETTINGS_UNKNOWN_FIELD,
  EMPTY_SETTINGS,
  NULL_SETTINGS,
  INVALID_SETTINGS_ARRAY,
  INVALID_SETTINGS_STRING,
  createMockSettingsCollector,
  createMockLogger,
  createMockMetrics,
  createTestMessage,
  createApplySettingsMessage,
};
