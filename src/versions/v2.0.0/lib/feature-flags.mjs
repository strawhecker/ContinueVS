#!/usr/bin/env node

/**
 * feature-flags.mjs
 *
 * Feature flags for bridge runtime configuration.
 *
 * **Purpose**: Centralized feature control that allows enabling/disabling experimental or
 * optional features without code changes. Features can be toggled via environment variables.
 *
 * **Usage**:
 * ```javascript
 * import { BRIDGE_MODE_ENABLED, TREE_SITTER_ENABLED } from './feature-flags.mjs';
 *
 * if (TREE_SITTER_ENABLED) {
 *   // Register optional tree-sitter handler
 * }
 * ```
 *
 * **Environment Variables**:
 * - `CONTINUE_BRIDGE_MODE` (default: 'true') — Enable/disable bridge mode
 * - `CONTINUE_TREE_SITTER` (default: 'false') — Enable/disable tree-sitter integration
 * - `CONTINUE_DEBUG_MODE` (default: 'false') — Enable/disable verbose debugging
 * - `CONTINUE_METRICS_ENABLED` (default: 'true') — Enable/disable metrics collection
 *
 * @module src/versions/v2.0.0/lib/feature-flags.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

/**
 * Parse boolean environment variable.
 *
 * @private
 * @param {string|undefined} value - Environment variable value
 * @param {boolean} defaultValue - Default if variable not set
 * @returns {boolean}
 */
function parseEnvBoolean(value, defaultValue) {
  if (value === undefined || value === null) {
    return defaultValue;
  }
  return value.toLowerCase() === 'true' || value === '1' || value === 'yes';
}

/**
 * Bridge mode enabled (Step 40).
 * When enabled, bridge processes messages from Continue through custom handlers.
 * When disabled, all messages relay directly to Continue without bridge intervention.
 *
 * @type {boolean}
 */
export const BRIDGE_MODE_ENABLED = parseEnvBoolean(
  process.env.CONTINUE_BRIDGE_MODE,
  true // Default: enabled
);

/**
 * Tree-sitter integration enabled (Step 80).
 * When enabled, optional tree-sitter handler is registered for AST analysis.
 * When disabled, tree-sitter handler is skipped; symbol extraction uses existing methods.
 *
 * **Notes**:
 * - tree-sitter npm package must be installed separately if enabled
 * - Does not block bridge initialization if unavailable
 * - Graceful degradation if tree-sitter module missing
 *
 * @type {boolean}
 */
export const TREE_SITTER_ENABLED = parseEnvBoolean(
  process.env.CONTINUE_TREE_SITTER,
  false // Default: disabled (opt-in feature post-GA)
);

/**
 * Debug mode enabled (internal development feature).
 * When enabled, additional logging and validation is performed.
 *
 * @type {boolean}
 */
export const DEBUG_MODE_ENABLED = parseEnvBoolean(
  process.env.CONTINUE_DEBUG_MODE,
  false // Default: disabled
);

/**
 * Metrics collection enabled.
 * When enabled, telemetry data is collected for performance analysis.
 *
 * @type {boolean}
 */
export const METRICS_ENABLED = parseEnvBoolean(
  process.env.CONTINUE_METRICS_ENABLED,
  true // Default: enabled
);

/**
 * Get all feature flags as object.
 *
 * @returns {Object} Feature flags object
 */
export function getAllFlags() {
  return {
    BRIDGE_MODE_ENABLED,
    TREE_SITTER_ENABLED,
    DEBUG_MODE_ENABLED,
    METRICS_ENABLED,
  };
}

/**
 * Log feature flags configuration (useful for debugging).
 *
 * @param {Object} logger - Logger instance
 * @returns {void}
 */
export function logFlagsConfiguration(logger) {
  if (!logger || typeof logger.log !== 'function') {
    return;
  }

  const flags = getAllFlags();
  logger.log('[feature-flags] Configuration loaded:');
  Object.entries(flags).forEach(([name, value]) => {
    logger.log(`  ${name}: ${value}`);
  });
}

/**
 * Check if a feature flag is enabled.
 *
 * @param {string} flagName - Flag name (e.g., 'TREE_SITTER_ENABLED')
 * @returns {boolean}
 */
export function isFlagEnabled(flagName) {
  return flags[flagName] === true;
}

/**
 * Private flags lookup table.
 *
 * @type {Object}
 */
const flags = {
  BRIDGE_MODE_ENABLED,
  TREE_SITTER_ENABLED,
  DEBUG_MODE_ENABLED,
  METRICS_ENABLED,
};
