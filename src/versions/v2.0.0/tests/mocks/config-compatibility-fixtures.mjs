#!/usr/bin/env node

/**
 * Configuration Compatibility Test Fixtures (Step 111)
 *
 * Provides factory functions and test data for configuration persistence,
 * settings-sync handler integration, and cross-platform compatibility.
 *
 * @module src/versions/v2.0.0/tests/mocks/config-compatibility-fixtures.mjs
 * @version 1.0.0
 */

import { homedir } from 'os';
import { resolve } from 'path';

/**
 * Get configuration test scenarios (valid and invalid)
 * @returns {Object} Object with valid and invalid config scenarios
 */
export function getConfigScenarios() {
  const validConfigs = [
    {
      name: 'Minimal valid config',
      config: {
        version: '2.0.0',
        models: [
          {
            name: 'gpt-4',
            provider: 'openai',
            apiKey: '[MASKED]'
          }
        ]
      }
    },
    {
      name: 'Config with multiple models',
      config: {
        version: '2.0.0',
        models: [
          {
            name: 'gpt-4',
            provider: 'openai',
            apiKey: '[MASKED]'
          },
          {
            name: 'claude-3',
            provider: 'anthropic',
            apiKey: '[MASKED]'
          }
        ]
      }
    },
    {
      name: 'Config with settings',
      config: {
        version: '2.0.0',
        models: [
          {
            name: 'gpt-4',
            provider: 'openai',
            apiKey: '[MASKED]'
          }
        ],
        settings: {
          theme: 'dark',
          language: 'en',
          autoSave: true
        }
      }
    },
    {
      name: 'Config with advanced settings',
      config: {
        version: '2.0.0',
        models: [
          {
            name: 'gpt-4',
            provider: 'openai',
            apiKey: '[MASKED]'
          }
        ],
        settings: {
          theme: 'dark',
          autoSave: true,
          codeCompletionEnabled: true,
          refactoringEnabled: true,
          maxContextTokens: 4000,
          temperature: 0.7
        }
      }
    }
  ];

  const invalidConfigs = [
    {
      name: 'Missing version field',
      config: {
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }
        ]
      },
      error: 'Missing required field: version'
    },
    {
      name: 'Missing models array',
      config: {
        version: '2.0.0'
      },
      error: 'Missing required field: models'
    },
    {
      name: 'Empty models array',
      config: {
        version: '2.0.0',
        models: []
      },
      error: 'Models array must contain at least one model'
    },
    {
      name: 'Model missing required fields',
      config: {
        version: '2.0.0',
        models: [
          {
            name: 'gpt-4'
            // missing provider and apiKey
          }
        ]
      },
      error: 'Model missing required fields: provider, apiKey'
    },
    {
      name: 'Duplicate model names',
      config: {
        version: '2.0.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' },
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }
        ]
      },
      error: 'Duplicate model name: gpt-4'
    },
    {
      name: 'Invalid version format',
      config: {
        version: '2.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }
        ]
      },
      error: 'Invalid version format (expected X.Y.Z)'
    }
  ];

  return {
    valid: validConfigs,
    invalid: invalidConfigs
  };
}

/**
 * Get settings-sync handler message scenarios
 * @returns {Array} Array of handler message scenarios
 */
export function getSettingsSyncMessages() {
  return [
    {
      name: 'Load settings request',
      message: {
        messageId: 'load-settings-1',
        messageType: 'bridge:loadSettings',
        data: null
      },
      expectedResponse: {
        settings: 'object',
        models: 'array',
        timestamp: 'string'
      }
    },
    {
      name: 'Apply settings request',
      message: {
        messageId: 'apply-settings-1',
        messageType: 'bridge:applySettings',
        data: {
          theme: 'dark',
          autoSave: true,
          temperature: 0.8
        }
      },
      expectedResponse: {
        success: 'boolean',
        message: 'string'
      }
    },
    {
      name: 'Add model to settings',
      message: {
        messageId: 'add-model-1',
        messageType: 'bridge:applySettings',
        data: {
          action: 'addModel',
          model: {
            name: 'gpt-4-turbo',
            provider: 'openai',
            apiKey: '[MASKED]'
          }
        }
      },
      expectedResponse: {
        success: 'boolean',
        models: 'array'
      }
    },
    {
      name: 'Update model in settings',
      message: {
        messageId: 'update-model-1',
        messageType: 'bridge:applySettings',
        data: {
          action: 'updateModel',
          modelName: 'gpt-4',
          updates: {
            temperature: 0.9
          }
        }
      },
      expectedResponse: {
        success: 'boolean',
        model: 'object'
      }
    },
    {
      name: 'Remove model from settings',
      message: {
        messageId: 'remove-model-1',
        messageType: 'bridge:applySettings',
        data: {
          action: 'removeModel',
          modelName: 'gpt-4'
        }
      },
      expectedResponse: {
        success: 'boolean',
        message: 'string'
      }
    }
  ];
}

/**
 * Get config round-trip scenarios (write → restart → read)
 * @returns {Array} Array of round-trip test scenarios
 */
export function getConfigRoundTrips() {
  return [
    {
      name: 'Simple round-trip',
      writeConfig: {
        version: '2.0.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }
        ]
      },
      expectedReadConfig: {
        version: '2.0.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }
        ]
      },
      verifyFields: ['version', 'models']
    },
    {
      name: 'Round-trip with settings',
      writeConfig: {
        version: '2.0.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }
        ],
        settings: {
          theme: 'dark',
          autoSave: true
        }
      },
      expectedReadConfig: {
        version: '2.0.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }
        ],
        settings: {
          theme: 'dark',
          autoSave: true
        }
      },
      verifyFields: ['version', 'models', 'settings']
    },
    {
      name: 'Round-trip with complex settings',
      writeConfig: {
        version: '2.0.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' },
          { name: 'claude-3', provider: 'anthropic', apiKey: '[MASKED]' }
        ],
        settings: {
          theme: 'dark',
          autoSave: true,
          maxContextTokens: 4000,
          codeCompletionEnabled: true,
          refactoringEnabled: true,
          features: {
            advancedSearch: false,
            gitIntegration: true
          }
        }
      },
      expectedReadConfig: {
        version: '2.0.0',
        models: [
          { name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' },
          { name: 'claude-3', provider: 'anthropic', apiKey: '[MASKED]' }
        ],
        settings: {
          theme: 'dark',
          autoSave: true,
          maxContextTokens: 4000,
          codeCompletionEnabled: true,
          refactoringEnabled: true,
          features: {
            advancedSearch: false,
            gitIntegration: true
          }
        }
      },
      verifyFields: ['version', 'models', 'settings.maxContextTokens', 'settings.features']
    }
  ];
}

/**
 * Get cross-platform config path scenarios
 * @returns {Object} Platform-specific config paths
 */
export function getCrossPlatformConfigPaths() {
  const homeDir = homedir();

  return {
    windows: {
      platform: 'win32',
      configDir: resolve(homeDir, '.continue'),
      configFile: resolve(homeDir, '.continue', 'config.json'),
      backupDir: resolve(homeDir, '.continue', 'backups'),
      description: 'Windows config directory'
    },
    unix: {
      platform: 'linux',
      configDir: resolve(homeDir, '.continue'),
      configFile: resolve(homeDir, '.continue', 'config.json'),
      backupDir: resolve(homeDir, '.continue', 'backups'),
      description: 'Unix/Linux config directory'
    },
    macos: {
      platform: 'darwin',
      configDir: resolve(homeDir, '.continue'),
      configFile: resolve(homeDir, '.continue', 'config.json'),
      backupDir: resolve(homeDir, '.continue', 'backups'),
      description: 'macOS config directory'
    }
  };
}

/**
 * Get config persistence test cases (backup, atomic write, etc.)
 * @returns {Array} Array of persistence test scenarios
 */
export function getConfigPersistenceScenarios() {
  return [
    {
      name: 'Atomic write creates backup',
      operation: 'write',
      config: {
        version: '2.0.0',
        models: [{ name: 'gpt-4', provider: 'openai', apiKey: '[MASKED]' }]
      },
      expectations: {
        configWritten: true,
        backupCreated: true,
        backupContainsPreviousVersion: true
      }
    },
    {
      name: 'Read returns latest config',
      operation: 'read',
      expectations: {
        configExists: true,
        configValid: true,
        version: '2.0.0'
      }
    },
    {
      name: 'Handle missing config gracefully',
      operation: 'read',
      configExists: false,
      expectations: {
        returnsDefaults: true,
        errorHandled: true
      }
    },
    {
      name: 'Corrupted config detected',
      operation: 'read',
      corruptedConfig: true,
      expectations: {
        corruptionDetected: true,
        backupRestored: true
      }
    },
    {
      name: 'Sensitive data masked in logs',
      operation: 'write',
      config: {
        version: '2.0.0',
        models: [
          {
            name: 'gpt-4',
            provider: 'openai',
            apiKey: 'sk-1234567890abcdef'
          }
        ]
      },
      expectations: {
        apiKeyMasked: true,
        logContains: '[MASKED]',
        apiKeyNotInLogs: true
      }
    }
  ];
}

/**
 * Factory: Create a complete config with all fields
 * @param {Object} overrides - Fields to override
 * @returns {Object} Complete config object
 */
export function createCompleteConfig(overrides = {}) {
  const defaults = {
    version: '2.0.0',
    models: [
      {
        name: 'gpt-4',
        provider: 'openai',
        apiKey: '[MASKED]'
      }
    ],
    settings: {
      theme: 'dark',
      autoSave: true,
      maxContextTokens: 4000,
      temperature: 0.7
    }
  };

  return {
    ...defaults,
    ...overrides
  };
}

/**
 * Factory: Create settings-sync handler message
 * @param {string} action - Action type (load, apply, addModel, etc.)
 * @param {Object} data - Message data
 * @returns {Object} Handler message
 */
export function createSettingsSyncMessage(action, data = null) {
  const messageType = action === 'load' ? 'bridge:loadSettings' : 'bridge:applySettings';

  return {
    messageId: `settings-${action}-${Date.now()}`,
    messageType,
    data: action === 'load' ? null : data
  };
}

export default {
  getConfigScenarios,
  getSettingsSyncMessages,
  getConfigRoundTrips,
  getCrossPlatformConfigPaths,
  getConfigPersistenceScenarios,
  createCompleteConfig,
  createSettingsSyncMessage
};
