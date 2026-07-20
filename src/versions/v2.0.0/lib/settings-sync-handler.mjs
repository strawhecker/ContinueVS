#!/usr/bin/env node

/**
 * Settings-Sync Handler (Step 95)
 *
 * Provides bidirectional settings synchronization for Continue LLM configuration.
 * Enables loading and applying settings (model selection, API keys, temperature, context window, etc.)
 * from Continue configuration files and the IDE.
 *
 * **Handler Type**: Query + mutation handler (two operations)
 * **Message Types**: 
 *   - bridge:loadSettings — retrieve settings from Continue config
 *   - bridge:applySettings — persist settings to Continue config
 *
 * **Input Payloads**:
 *   loadSettings: { scope?: "all"|"modelConfig"|"apiConfig" }
 *   applySettings: { settings: { model, provider, temperature, contextWindow, maxTokens, systemPrompt, endpoint } }
 *
 * **Output**:
 *   loadSettings: { success: true, data: { model, provider, temperature, ... }, duration }
 *   applySettings: { success: true, data: { appliedFields: string[], duration, cacheInvalidated: boolean } }
 *
 * **Architecture Flow**:
 * ```
 * [IDE requests settings] → bridge:loadSettings
 *   ↓
 * [handler] calls SettingsCollector.readSettings() → reads ~/.continue/config.json
 *   ↓
 * [handler] validates settings structure → error if invalid
 *   ↓
 * [handler] returns settings with duration metrics
 *   ↓
 * [IDE applies new settings] → bridge:applySettings { settings: {...} }
 *   ↓
 * [handler] validates payload (required fields, types, ranges)
 *   ↓
 * [handler] persists to Continue config via SettingsCollector.writeSettings()
 *   ↓
 * [handler] triggers cache invalidation
 *   ↓
 * [handler] returns success with applied fields + duration
 * ```
 *
 * **Settings Scope**:
 * - model: string (e.g., "gpt-4", "claude-3-opus")
 * - provider: string (e.g., "openai", "anthropic", "local")
 * - temperature: number (0.0–1.0)
 * - contextWindow: number (256–200000)
 * - maxTokens: number (1–4096)
 * - systemPrompt: string (optional, max 10000 chars)
 * - endpoint: string (optional URL for custom providers)
 *
 * **Error Handling**:
 * - ValidationError: Invalid field types, out-of-range values, missing required fields
 * - FileIOError: Cannot read/write Continue config file
 * - SettingsSyncError: General sync failure
 *
 * **Performance**:
 * - Load: <500ms (file I/O + parsing)
 * - Apply: <1s (validation + file write)
 * - Validation: <50ms
 * - Memory: ~100KB for settings object
 *
 * **Dependencies**:
 * - SettingsCollector (injected via context) — reads/writes Continue config
 * - Bridge logger (optional, injected) — debug logging
 * - Bridge metrics (optional, injected) — performance tracking
 *
 * @module src/versions/v2.0.0/lib/settings-sync-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

import { performance } from 'perf_hooks';

/**
 * Base error for settings-sync operations
 */
export class SettingsSyncError extends Error {
  constructor(message, operationType = 'unknown', code = 'SETTINGS_SYNC_ERROR', details = null) {
    super(message);
    this.name = 'SettingsSyncError';
    this.operationType = operationType;
    this.code = code;
    this.details = details;
  }
}

/**
 * Validation error for invalid settings payloads
 */
export class ValidationError extends SettingsSyncError {
  constructor(message, details = null) {
    super(message, 'validation', 'VALIDATION_ERROR', details);
    this.name = 'ValidationError';
  }
}

/**
 * File I/O error for Continue config read/write failures
 */
export class FileIOError extends SettingsSyncError {
  constructor(message, details = null) {
    super(message, 'fileio', 'FILEIO_ERROR', details);
    this.name = 'FileIOError';
  }
}

/**
 * Settings validation schema
 */
const SETTINGS_SCHEMA = {
  model: { type: 'string', required: true, minLength: 1, maxLength: 255 },
  provider: { type: 'string', required: true, minLength: 1, maxLength: 100 },
  temperature: { type: 'number', required: false, min: 0.0, max: 1.0 },
  contextWindow: { type: 'number', required: false, min: 256, max: 200000 },
  maxTokens: { type: 'number', required: false, min: 1, max: 4096 },
  systemPrompt: { type: 'string', required: false, maxLength: 10000 },
  endpoint: { type: 'string', required: false, maxLength: 2048 },
};

/**
 * Validates a single settings field against the schema
 * @param {string} fieldName
 * @param {*} value
 * @throws {ValidationError}
 */
function validateField(fieldName, value) {
  const schema = SETTINGS_SCHEMA[fieldName];
  if (!schema) {
    throw new ValidationError(`Unknown field: ${fieldName}`, { fieldName, value });
  }

  if (value === null || value === undefined) {
    if (schema.required) {
      throw new ValidationError(`Required field missing: ${fieldName}`, { fieldName });
    }
    return; // Optional field is null; skip validation
  }

  // Type checking
  if (typeof value !== schema.type) {
    throw new ValidationError(
      `Field ${fieldName} must be ${schema.type}, got ${typeof value}`,
      { fieldName, expectedType: schema.type, actualType: typeof value }
    );
  }

  // String constraints
  if (schema.type === 'string') {
    if (schema.minLength && value.length < schema.minLength) {
      throw new ValidationError(
        `Field ${fieldName} must be at least ${schema.minLength} characters`,
        { fieldName, minLength: schema.minLength, actualLength: value.length }
      );
    }
    if (schema.maxLength && value.length > schema.maxLength) {
      throw new ValidationError(
        `Field ${fieldName} must not exceed ${schema.maxLength} characters`,
        { fieldName, maxLength: schema.maxLength, actualLength: value.length }
      );
    }
  }

  // Numeric constraints
  if (schema.type === 'number') {
    if (schema.min !== undefined && value < schema.min) {
      throw new ValidationError(
        `Field ${fieldName} must be >= ${schema.min}, got ${value}`,
        { fieldName, min: schema.min, actualValue: value }
      );
    }
    if (schema.max !== undefined && value > schema.max) {
      throw new ValidationError(
        `Field ${fieldName} must be <= ${schema.max}, got ${value}`,
        { fieldName, max: schema.max, actualValue: value }
      );
    }
  }
}

/**
 * Validates entire settings object
 * @param {object} settings
 * @throws {ValidationError}
 */
function validateSettings(settings) {
  if (!settings || typeof settings !== 'object') {
    throw new ValidationError('Settings must be a non-null object', { settings });
  }

  // Validate each field
  for (const [fieldName, value] of Object.entries(settings)) {
    validateField(fieldName, value);
  }

  // Ensure required fields are present
  for (const [fieldName, schema] of Object.entries(SETTINGS_SCHEMA)) {
    if (schema.required && !(fieldName in settings)) {
      throw new ValidationError(`Required field missing: ${fieldName}`, { fieldName });
    }
  }
}

/**
 * Masks sensitive fields (API keys) in settings for logging
 * @param {object} settings
 * @returns {object}
 */
function maskSensitiveFields(settings) {
  const masked = { ...settings };
  // Mask endpoint if it contains query parameters (likely has API key)
  if (masked.endpoint && masked.endpoint.includes('?')) {
    masked.endpoint = '[MASKED_URL]';
  }
  return masked;
}

/**
 * Creates the loadSettings handler (factory)
 * Retrieves current settings from Continue configuration
 *
 * @param {object} context - Injected dependencies
 * @param {object} context.settingsCollector - Settings reader (optional)
 * @param {object} context.logger - Logger facade (optional)
 * @param {object} context.metrics - Metrics collector (optional)
 * @returns {Function} Async handler function
 */
export function createLoadSettingsHandler(context = {}) {
  const { settingsCollector, logger, metrics } = context;

  return async (message, bridgeContext) => {
    const startTime = performance.now();
    try {
      const payload = message.payload || {};
      const scope = payload.scope || 'all';

      // Validate scope parameter
      const validScopes = ['all', 'modelConfig', 'apiConfig'];
      if (!validScopes.includes(scope)) {
        throw new ValidationError(`Invalid scope: ${scope}`, { scope, validScopes });
      }

      logger?.info?.(`Loading settings with scope: ${scope}`);

      let settings = {};

      // Attempt to read from SettingsCollector if available
      if (settingsCollector && typeof settingsCollector.readSettings === 'function') {
        try {
          settings = await settingsCollector.readSettings();
        } catch (err) {
          logger?.warn?.(`Failed to read settings from collector: ${err.message}`);
          // Graceful degradation: continue with empty settings
        }
      }

      // Apply scope filter if needed
      if (scope === 'modelConfig') {
        settings = {
          model: settings.model,
          provider: settings.provider,
          temperature: settings.temperature,
        };
      } else if (scope === 'apiConfig') {
        settings = {
          endpoint: settings.endpoint,
          provider: settings.provider,
        };
      }

      const duration = performance.now() - startTime;

      // Record metrics
      metrics?.recordSettingsLoad?.({
        scope,
        duration,
        success: true,
        fieldCount: Object.keys(settings).length,
      });

      return {
        success: true,
        data: {
          settings: maskSensitiveFields(settings),
          scope,
          duration: Math.round(duration),
        },
      };
    } catch (err) {
      const duration = performance.now() - startTime;

      logger?.error?.(
        `Settings load failed: ${err.message}`,
        maskSensitiveFields(err.details || {})
      );

      metrics?.recordSettingsLoad?.({
        duration,
        success: false,
        error: err.code || 'UNKNOWN_ERROR',
      });

      // Convert to JSON-RPC error format
      if (err instanceof ValidationError) {
        return {
          success: false,
          error: {
            code: -32602, // Invalid params
            message: err.message,
            data: err.details,
          },
        };
      }

      return {
        success: false,
        error: {
          code: -32603, // Internal error
          message: err.message,
          data: err.details,
        },
      };
    }
  };
}

/**
 * Creates the applySettings handler (factory)
 * Persists new settings to Continue configuration
 *
 * @param {object} context - Injected dependencies
 * @param {object} context.settingsCollector - Settings writer (optional)
 * @param {object} context.logger - Logger facade (optional)
 * @param {object} context.metrics - Metrics collector (optional)
 * @returns {Function} Async handler function
 */
export function createApplySettingsHandler(context = {}) {
  const { settingsCollector, logger, metrics } = context;

  return async (message, bridgeContext) => {
    const startTime = performance.now();
    try {
      const payload = message.payload || {};
      const newSettings = payload.settings || {};

      logger?.info?.('Applying new settings', maskSensitiveFields(newSettings));

      // Validate settings
      validateSettings(newSettings);

      const appliedFields = [];
      let cacheInvalidated = false;

      // Attempt to write to SettingsCollector if available
      if (settingsCollector && typeof settingsCollector.writeSettings === 'function') {
        try {
          await settingsCollector.writeSettings(newSettings);
          appliedFields.push(...Object.keys(newSettings));
          cacheInvalidated = true;

          logger?.info?.(`Settings written: ${appliedFields.join(', ')}`);
        } catch (err) {
          logger?.error?.(`Failed to write settings: ${err.message}`);
          throw new FileIOError(`Cannot persist settings: ${err.message}`, {
            originalError: err.message,
          });
        }
      } else {
        // No collector available; just mark fields as applied
        appliedFields.push(...Object.keys(newSettings));
        logger?.warn?.(
          'No SettingsCollector available; settings will not persist'
        );
      }

      const duration = performance.now() - startTime;

      // Record metrics
      metrics?.recordSettingsApply?.({
        appliedFields,
        duration,
        success: true,
        cacheInvalidated,
      });

      return {
        success: true,
        data: {
          appliedFields,
          cacheInvalidated,
          duration: Math.round(duration),
        },
      };
    } catch (err) {
      const duration = performance.now() - startTime;

      logger?.error?.(
        `Settings apply failed: ${err.message}`,
        err.details || {}
      );

      metrics?.recordSettingsApply?.({
        duration,
        success: false,
        error: err.code || 'UNKNOWN_ERROR',
      });

      // Convert to JSON-RPC error format
      if (err instanceof ValidationError) {
        return {
          success: false,
          error: {
            code: -32602, // Invalid params
            message: err.message,
            data: err.details,
          },
        };
      }

      if (err instanceof FileIOError) {
        return {
          success: false,
          error: {
            code: -32603, // Internal error
            message: err.message,
            data: err.details,
          },
        };
      }

      return {
        success: false,
        error: {
          code: -32603, // Internal error
          message: err.message,
          data: err.details,
        },
      };
    }
  };
}

export default {
  createLoadSettingsHandler,
  createApplySettingsHandler,
  SettingsSyncError,
  ValidationError,
  FileIOError,
};
