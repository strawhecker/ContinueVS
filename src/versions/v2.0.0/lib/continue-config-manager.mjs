#!/usr/bin/env node

/**
 * Continue Configuration Manager (Step 104)
 * 
 * Bridge-side configuration persistence layer for managing Continue SDK config files.
 * Handles reading, writing, validating, and merging Continue configuration (~/.continue/config.json)
 * independent of the settings-sync handler (Step 95).
 * 
 * **Architecture**: Separate from Step 95 (IDE ↔ Continue sync via handlers).
 * This manager focuses on bridge ↔ filesystem operations and validation.
 * 
 * **Dependencies**: None (Node.js fs/promises built-ins only)
 * **Async/await**: All file I/O non-blocking
 * 
 * **Export**: { ContinueConfigManager, ConfigError, ValidationError, FileIOError }
 * 
 * @module src/versions/v2.0.0/lib/continue-config-manager.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

import { promises as fs } from 'fs';
import { homedir } from 'os';
import { join, dirname } from 'path';

/**
 * Base error for configuration operations
 */
export class ConfigError extends Error {
  constructor(message, operation = 'unknown', code = 'CONFIG_ERROR', details = null) {
    super(message);
    this.name = 'ConfigError';
    this.operation = operation;
    this.code = code;
    this.details = details;
  }

  toString() {
    return `[${this.code}] ${this.operation}: ${this.message}`;
  }
}

/**
 * Validation error for invalid config schema
 */
export class ValidationError extends ConfigError {
  constructor(message, fieldPath = 'root', code = 'VALIDATION_ERROR', details = null) {
    super(message, 'validation', code, details);
    this.name = 'ValidationError';
    this.fieldPath = fieldPath;
  }

  toString() {
    return `[${this.code}] ${this.fieldPath}: ${this.message}`;
  }
}

/**
 * File I/O error for read/write failures
 */
export class FileIOError extends ConfigError {
  constructor(message, filePath, code = 'FILE_IO_ERROR', details = null) {
    super(message, 'file_io', code, details);
    this.name = 'FileIOError';
    this.filePath = filePath;
  }

  toString() {
    return `[${this.code}] ${this.filePath}: ${this.message}`;
  }
}

/**
 * Continue Configuration Manager — bridge-side config lifecycle operations
 * 
 * **Public API**:
 * - readConfig() — Read and parse ~/.continue/config.json
 * - writeConfig(config) — Write and serialize config file
 * - mergeModels(config, modelsToMerge) — Add/update models by title
 * - removeModels(config, modelTitles) — Remove models by title
 * - validateSchema(config) — Validate entire config schema
 * 
 * **Thread Safety**: Not applicable (Node.js single-threaded event loop)
 * **Graceful Degradation**: Missing file returns empty config; logger/metrics optional
 */
export class ContinueConfigManager {
  /**
   * Optional logger for diagnostics
   * @type {Object|null}
   */
  #logger;

  /**
   * Optional metrics collector for performance tracking
   * @type {Object|null}
   */
  #metrics;

  constructor(logger = null, metrics = null) {
    this.#logger = logger;
    this.#metrics = metrics;
  }

  /**
   * Reads and parses Continue config file (~/.continue/config.json).
   * 
   * Returns empty config if file not found (graceful degradation).
   * Throws ValidationError if JSON is valid but schema is invalid.
   * Throws FileIOError on read failures.
   * 
   * @returns {Promise<Object>} Deserialized config with validated structure
   * @throws {ConfigError} On file I/O or validation errors
   */
  async readConfig() {
    const startTime = performance.now();
    const configPath = this.#getConfigPath();

    try {
      let jsonContent;
      try {
        jsonContent = await fs.readFile(configPath, 'utf-8');
      } catch (err) {
        if (err.code === 'ENOENT') {
          this.#log('debug', `Config file not found at ${configPath}, returning empty config`);
          this.#recordMetric('config_read_not_found', Date.now() - startTime);
          return { models: [] };
        }
        throw new FileIOError(
          `Error reading config: ${err.message}`,
          configPath,
          'FILE_READ_ERROR',
          { originalError: err.code }
        );
      }

      let config;
      try {
        config = JSON.parse(jsonContent);
      } catch (parseErr) {
        throw new ConfigError(
          `Invalid JSON in config: ${parseErr.message}`,
          'parse',
          'JSON_PARSE_ERROR',
          { originalError: parseErr }
        );
      }

      // Validate schema
      this.validateSchema(config);

      this.#recordMetric('config_read_success', Date.now() - startTime);
      return config;
    } catch (err) {
      if (err instanceof ConfigError) {
        this.#log('error', `Config read failed: ${err.toString()}`);
        throw err;
      }
      throw new ConfigError(
        `Unexpected error reading config: ${err.message}`,
        'read',
        'UNKNOWN_ERROR'
      );
    }
  }

  /**
   * Writes and serializes config file (~/.continue/config.json).
   * 
   * Creates parent directory if not present.
   * Creates backup of existing file before overwriting.
   * Validates schema before writing.
   * 
   * @param {Object} config — Config to write
   * @throws {ConfigError} On validation or file I/O errors
   */
  async writeConfig(config) {
    const startTime = performance.now();

    if (!config) {
      throw new ValidationError('Config is required', 'root', 'NULL_CONFIG');
    }

    try {
      // Validate before write
      this.validateSchema(config);

      const configPath = this.#getConfigPath();
      const configDir = dirname(configPath);

      // Ensure directory exists
      try {
        await fs.mkdir(configDir, { recursive: true });
      } catch (mkdirErr) {
        throw new FileIOError(
          `Cannot create config directory: ${mkdirErr.message}`,
          configDir,
          'MKDIR_ERROR'
        );
      }

      // Backup existing file
      try {
        await fs.copyFile(configPath, `${configPath}.backup`);
      } catch (backupErr) {
        if (backupErr.code !== 'ENOENT') {
          this.#log('warn', `Failed to backup existing config: ${backupErr.message}`);
        }
      }

      // Write new config with 2-space indentation
      const jsonContent = JSON.stringify(config, null, 2);
      try {
        await fs.writeFile(configPath, jsonContent, 'utf-8');
      } catch (writeErr) {
        throw new FileIOError(
          `Cannot write config: ${writeErr.message}`,
          configPath,
          'FILE_WRITE_ERROR'
        );
      }

      this.#recordMetric('config_write_success', Date.now() - startTime);
      this.#log('info', `Config written successfully to ${configPath}`);
    } catch (err) {
      if (err instanceof ConfigError) {
        this.#log('error', `Config write failed: ${err.toString()}`);
        throw err;
      }
      throw new ConfigError(
        `Unexpected error writing config: ${err.message}`,
        'write',
        'UNKNOWN_ERROR'
      );
    }
  }

  /**
   * Merges models into config by title. Updates existing, adds new.
   * 
   * @param {Object} config — Config to merge into
   * @param {Array} modelsToMerge — Models to add/update
   * @returns {Promise<Object>} Merged config
   * @throws {ValidationError} On schema validation errors
   */
  async mergeModels(config, modelsToMerge) {
    const startTime = performance.now();

    if (!config) {
      throw new ValidationError('Config is required', 'root', 'NULL_CONFIG');
    }

    if (!modelsToMerge || modelsToMerge.length === 0) {
      return config;
    }

    try {
      const result = {
        models: [...config.models || []]
      };

      for (const modelToMerge of modelsToMerge) {
        this.#validateModel(modelToMerge);

        const existingIndex = result.models.findIndex(m =>
          m.title?.toLowerCase() === modelToMerge.title?.toLowerCase()
        );

        if (existingIndex >= 0) {
          result.models[existingIndex] = modelToMerge;
        } else {
          result.models.push(modelToMerge);
        }
      }

      this.validateSchema(result);
      this.#recordMetric('config_merge_success', Date.now() - startTime);
      return result;
    } catch (err) {
      if (err instanceof ConfigError) {
        throw err;
      }
      throw new ConfigError(
        `Unexpected error merging models: ${err.message}`,
        'merge',
        'UNKNOWN_ERROR'
      );
    }
  }

  /**
   * Removes models from config by title (case-insensitive).
   * 
   * @param {Object} config — Config to remove from
   * @param {Array} modelTitles — Titles to remove
   * @returns {Promise<Object>} Updated config
   * @throws {ValidationError} On schema validation errors
   */
  async removeModels(config, modelTitles) {
    if (!config) {
      throw new ValidationError('Config is required', 'root', 'NULL_CONFIG');
    }

    if (!modelTitles || modelTitles.length === 0) {
      return config;
    }

    try {
      const titlesToRemove = new Set(modelTitles.map(t => t.toLowerCase()));
      const result = {
        models: (config.models || []).filter(m =>
          !titlesToRemove.has(m.title?.toLowerCase())
        )
      };

      this.validateSchema(result);
      return result;
    } catch (err) {
      if (err instanceof ConfigError) {
        throw err;
      }
      throw new ConfigError(
        `Unexpected error removing models: ${err.message}`,
        'remove',
        'UNKNOWN_ERROR'
      );
    }
  }

  /**
   * Validates entire Continue config schema.
   * 
   * Checks: models array exists, all models have required fields,
   * all field types correct, no duplicate titles.
   * 
   * @param {Object} config — Config to validate
   * @throws {ValidationError} If schema is invalid
   */
  validateSchema(config) {
    if (!config) {
      throw new ValidationError('Config is null', 'root', 'NULL_CONFIG');
    }

    if (!Array.isArray(config.models)) {
      throw new ValidationError(
        'config.models must be an array',
        'models',
        'INVALID_MODELS_ARRAY'
      );
    }

    const seenTitles = new Set();
    for (let i = 0; i < config.models.length; i++) {
      const model = config.models[i];
      this.#validateModel(model, i);

      const titleLower = model.title?.toLowerCase();
      if (seenTitles.has(titleLower)) {
        throw new ValidationError(
          `Duplicate model title: '${model.title}' at index ${i}`,
          `models[${i}].title`,
          'DUPLICATE_TITLE'
        );
      }
      seenTitles.add(titleLower);
    }
  }

  /**
   * Validates individual model schema.
   * 
   * Checks: title (required, non-empty), provider (required, non-empty),
   * model (required, non-empty), apiKey (optional), apiBase (optional).
   * 
   * @private
   * @param {Object} model — Model to validate
   * @param {number} index — Array index for error reporting
   * @throws {ValidationError} If model schema is invalid
   */
  #validateModel(model, index = -1) {
    if (!model || typeof model !== 'object') {
      const fieldPath = index >= 0 ? `models[${index}]` : 'model';
      throw new ValidationError(
        'Model must be a non-null object',
        fieldPath,
        'INVALID_MODEL'
      );
    }

    const fieldPath = index >= 0 ? `models[${index}]` : 'model';

    if (!model.title || typeof model.title !== 'string' || model.title.trim() === '') {
      throw new ValidationError(
        'Model title is required (non-empty string)',
        `${fieldPath}.title`,
        'MISSING_TITLE'
      );
    }

    if (!model.provider || typeof model.provider !== 'string' || model.provider.trim() === '') {
      throw new ValidationError(
        'Model provider is required (non-empty string)',
        `${fieldPath}.provider`,
        'MISSING_PROVIDER'
      );
    }

    if (!model.model || typeof model.model !== 'string' || model.model.trim() === '') {
      throw new ValidationError(
        'Model field is required (non-empty string)',
        `${fieldPath}.model`,
        'MISSING_MODEL'
      );
    }

    if (model.apiKey !== undefined && model.apiKey !== null && typeof model.apiKey !== 'string') {
      throw new ValidationError(
        'Model apiKey must be a string or null',
        `${fieldPath}.apiKey`,
        'INVALID_APIKEY_TYPE'
      );
    }

    if (model.apiBase !== undefined && model.apiBase !== null && typeof model.apiBase !== 'string') {
      throw new ValidationError(
        'Model apiBase must be a string or null',
        `${fieldPath}.apiBase`,
        'INVALID_APIBASE_TYPE'
      );
    }
  }

  /**
   * Gets the full path to Continue configuration file (~/.continue/config.json).
   * 
   * @private
   * @returns {string} Full path to config file
   */
  #getConfigPath() {
    const home = homedir();
    return join(home, '.continue', 'config.json');
  }

  /**
   * Logs a message (graceful degradation if logger not provided).
   * 
   * @private
   * @param {string} level — Log level (debug, info, warn, error)
   * @param {string} message — Message to log
   */
  #log(level, message) {
    if (this.#logger && typeof this.#logger.log === 'function') {
      this.#logger.log(level, `[ContinueConfigManager] ${message}`);
    }
  }

  /**
   * Records a metric (graceful degradation if metrics not provided).
   * 
   * @private
   * @param {string} name — Metric name
   * @param {number} value — Metric value (typically duration in ms)
   */
  #recordMetric(name, value) {
    if (this.#metrics && typeof this.#metrics.record === 'function') {
      this.#metrics.record(name, value);
    }
  }
}

/**
 * Factory function to create a ContinueConfigManager instance.
 * 
 * @param {Object|null} logger — Optional bridge logger (has .log(level, message))
 * @param {Object|null} metrics — Optional metrics collector (has .record(name, value))
 * @returns {ContinueConfigManager} Manager instance
 */
export function createContinueConfigManager(logger = null, metrics = null) {
  return new ContinueConfigManager(logger, metrics);
}
