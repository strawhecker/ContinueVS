#!/usr/bin/env node

/**
 * Test fixtures for ContinueConfigManager (Step 104)
 * 
 * Provides valid/invalid config examples and mock factories for tests.
 * 
 * **Exports**:
 * - validConfigs (array of valid config objects)
 * - invalidConfigs (array of invalid config objects with error details)
 * - createMockLogger (factory for mock logger)
 * - createMockMetrics (factory for mock metrics)
 */

/**
 * Valid configuration examples
 */
export const validConfigs = {
  empty: {
    models: []
  },

  singleModel: {
    models: [
      {
        title: 'GPT-4',
        provider: 'openai',
        model: 'gpt-4'
      }
    ]
  },

  multipleModels: {
    models: [
      {
        title: 'GPT-4',
        provider: 'openai',
        model: 'gpt-4',
        apiKey: 'sk-gpt4',
        apiBase: 'https://api.openai.com/v1'
      },
      {
        title: 'Claude-3-Opus',
        provider: 'anthropic',
        model: 'claude-3-opus-20240229',
        apiKey: 'sk-ant-claude3'
      },
      {
        title: 'Local-Llama',
        provider: 'local',
        model: 'llama2-7b'
      }
    ]
  },

  withOptionalFields: {
    models: [
      {
        title: 'Model with all fields',
        provider: 'openai',
        model: 'gpt-4',
        apiKey: 'sk-123',
        apiBase: 'https://api.openai.com'
      }
    ]
  },

  withoutOptionalFields: {
    models: [
      {
        title: 'Minimal model',
        provider: 'openai',
        model: 'gpt-4'
      }
    ]
  }
};

/**
 * Invalid configuration examples with error details
 */
export const invalidConfigs = {
  nullConfig: {
    value: null,
    expectedError: 'NULL_CONFIG',
    description: 'Null config'
  },

  undefinedConfig: {
    value: undefined,
    expectedError: 'NULL_CONFIG',
    description: 'Undefined config'
  },

  nullModels: {
    value: { models: null },
    expectedError: 'INVALID_MODELS_ARRAY',
    description: 'Null models array'
  },

  modelsNotArray: {
    value: { models: 'not_array' },
    expectedError: 'INVALID_MODELS_ARRAY',
    description: 'Models is string instead of array'
  },

  modelsAsObject: {
    value: { models: { title: 'Model' } },
    expectedError: 'INVALID_MODELS_ARRAY',
    description: 'Models is object instead of array'
  },

  emptyTitle: {
    value: {
      models: [
        {
          title: '',
          provider: 'openai',
          model: 'gpt-4'
        }
      ]
    },
    expectedError: 'MISSING_TITLE',
    description: 'Empty string title'
  },

  nullTitle: {
    value: {
      models: [
        {
          title: null,
          provider: 'openai',
          model: 'gpt-4'
        }
      ]
    },
    expectedError: 'MISSING_TITLE',
    description: 'Null title'
  },

  missingTitle: {
    value: {
      models: [
        {
          provider: 'openai',
          model: 'gpt-4'
        }
      ]
    },
    expectedError: 'MISSING_TITLE',
    description: 'Title field missing'
  },

  emptyProvider: {
    value: {
      models: [
        {
          title: 'GPT-4',
          provider: '',
          model: 'gpt-4'
        }
      ]
    },
    expectedError: 'MISSING_PROVIDER',
    description: 'Empty string provider'
  },

  missingProvider: {
    value: {
      models: [
        {
          title: 'GPT-4',
          model: 'gpt-4'
        }
      ]
    },
    expectedError: 'MISSING_PROVIDER',
    description: 'Provider field missing'
  },

  emptyModel: {
    value: {
      models: [
        {
          title: 'GPT-4',
          provider: 'openai',
          model: ''
        }
      ]
    },
    expectedError: 'MISSING_MODEL',
    description: 'Empty string model'
  },

  missingModel: {
    value: {
      models: [
        {
          title: 'GPT-4',
          provider: 'openai'
        }
      ]
    },
    expectedError: 'MISSING_MODEL',
    description: 'Model field missing'
  },

  duplicateTitles: {
    value: {
      models: [
        {
          title: 'GPT-4',
          provider: 'openai',
          model: 'gpt-4'
        },
        {
          title: 'GPT-4',
          provider: 'openai',
          model: 'gpt-4-32k'
        }
      ]
    },
    expectedError: 'DUPLICATE_TITLE',
    description: 'Duplicate model titles'
  },

  duplicateTitlesCaseInsensitive: {
    value: {
      models: [
        {
          title: 'GPT-4',
          provider: 'openai',
          model: 'gpt-4'
        },
        {
          title: 'gpt-4',
          provider: 'openai',
          model: 'gpt-4-turbo'
        }
      ]
    },
    expectedError: 'DUPLICATE_TITLE',
    description: 'Duplicate titles with different case'
  },

  invalidApiKeyType: {
    value: {
      models: [
        {
          title: 'GPT-4',
          provider: 'openai',
          model: 'gpt-4',
          apiKey: 12345
        }
      ]
    },
    expectedError: 'INVALID_APIKEY_TYPE',
    description: 'ApiKey is number instead of string'
  },

  invalidApiBaseType: {
    value: {
      models: [
        {
          title: 'GPT-4',
          provider: 'openai',
          model: 'gpt-4',
          apiBase: ['https://api.openai.com']
        }
      ]
    },
    expectedError: 'INVALID_APIBASE_TYPE',
    description: 'ApiBase is array instead of string'
  },

  modelNotObject: {
    value: {
      models: [
        'not_an_object'
      ]
    },
    expectedError: 'INVALID_MODEL',
    description: 'Model element is string instead of object'
  },

  malformedJson: {
    jsonString: '{ invalid json',
    expectedError: 'JSON_PARSE_ERROR',
    description: 'Malformed JSON'
  }
};

/**
 * Mock scenarios for merging and removal
 */
export const mergeScenarios = {
  addSingleModel: {
    existing: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' }
      ]
    },
    toMerge: [
      { title: 'Claude', provider: 'anthropic', model: 'claude-3' }
    ],
    expectedResult: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' },
        { title: 'Claude', provider: 'anthropic', model: 'claude-3' }
      ]
    }
  },

  updateSingleModel: {
    existing: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' }
      ]
    },
    toMerge: [
      { title: 'GPT-4', provider: 'openai', model: 'gpt-4-turbo' }
    ],
    expectedResult: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4-turbo' }
      ]
    }
  },

  addMultipleModels: {
    existing: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' }
      ]
    },
    toMerge: [
      { title: 'Claude', provider: 'anthropic', model: 'claude-3' },
      { title: 'Llama', provider: 'local', model: 'llama-7b' }
    ],
    expectedResult: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' },
        { title: 'Claude', provider: 'anthropic', model: 'claude-3' },
        { title: 'Llama', provider: 'local', model: 'llama-7b' }
      ]
    }
  },

  casInsensitiveUpdate: {
    existing: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' }
      ]
    },
    toMerge: [
      { title: 'gpt-4', provider: 'openai', model: 'gpt-4-32k' }
    ],
    expectedResult: {
      models: [
        { title: 'gpt-4', provider: 'openai', model: 'gpt-4-32k' }
      ]
    }
  },

  removeModel: {
    existing: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' },
        { title: 'Claude', provider: 'anthropic', model: 'claude-3' }
      ]
    },
    toRemove: ['GPT-4'],
    expectedResult: {
      models: [
        { title: 'Claude', provider: 'anthropic', model: 'claude-3' }
      ]
    }
  },

  removeMultipleModels: {
    existing: {
      models: [
        { title: 'GPT-4', provider: 'openai', model: 'gpt-4' },
        { title: 'Claude', provider: 'anthropic', model: 'claude-3' },
        { title: 'Llama', provider: 'local', model: 'llama-7b' }
      ]
    },
    toRemove: ['GPT-4', 'Llama'],
    expectedResult: {
      models: [
        { title: 'Claude', provider: 'anthropic', model: 'claude-3' }
      ]
    }
  }
};

/**
 * Creates a mock logger for testing
 * 
 * @param {boolean} captureOutput - If true, logs are captured in array
 * @returns {Object} Mock logger with log method
 */
export function createMockLogger(captureOutput = false) {
  const logs = [];

  return {
    log(level, message) {
      if (captureOutput) {
        logs.push({ level, message, timestamp: Date.now() });
      }
    },

    get captured() {
      return logs;
    },

    clear() {
      logs.length = 0;
    }
  };
}

/**
 * Creates a mock metrics collector for testing
 * 
 * @param {boolean} captureOutput - If true, metrics are captured in array
 * @returns {Object} Mock metrics with record method
 */
export function createMockMetrics(captureOutput = false) {
  const metrics = [];

  return {
    record(name, value) {
      if (captureOutput) {
        metrics.push({ name, value, timestamp: Date.now() });
      }
    },

    get captured() {
      return metrics;
    },

    clear() {
      metrics.length = 0;
    },

    getByName(name) {
      return metrics.filter(m => m.name === name);
    }
  };
}

/**
 * Creates a combined logger + metrics mock for integration testing
 * 
 * @returns {Object} Combined mock with both logger and metrics capabilities
 */
export function createMockBridgeContext() {
  return {
    logger: createMockLogger(true),
    metrics: createMockMetrics(true),

    clear() {
      this.logger.clear();
      this.metrics.clear();
    }
  };
}
