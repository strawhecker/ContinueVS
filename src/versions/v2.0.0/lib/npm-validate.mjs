/**
 * npm Package Startup Validation Module
 *
 * Orchestrates validation of npm packages at bridge startup.
 * Uses the integrity utility (Step 8) to verify checksums and manifests.
 *
 * Provides three main exports:
 * 1. validateIntegrity() - Full validation with logging and recovery suggestions
 * 2. checkPackageIntegrity() - Quick synchronous integrity check
 * 3. performHealthCheck() - Diagnostic utility for troubleshooting
 *
 * @module src/versions/v2.0.0/lib/npm-validate.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 8 (integrity utility), 11 (cache download), 31 (npm tests), 45 (lifecycle manager)
 */

import { validatePackageIntegrity } from './integrity.js';
import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';

// -------------------------------------------------------
// Constants & Configuration
// -------------------------------------------------------

const DEFAULT_TIMEOUT_MS = 30000; // 30 seconds
const CACHE_BASE_DIR = '.cache/npm-packages';

/**
 * Error message templates with recovery instructions.
 */
const ERROR_MESSAGES = {
  PACKAGE_NOT_FOUND: {
    message: 'npm package not found in cache',
    recovery: 'Run: npm run initialize (or delete .cache folder and re-run npm install)'
  },
  CHECKSUM_MISMATCH: {
    message: 'package checksum does not match (file corrupted)',
    recovery: 'Delete cache and re-run: rm -r .cache && npm run initialize'
  },
  MANIFEST_INVALID: {
    message: 'manifest.json is missing or invalid',
    recovery: 'Cache may be incomplete. Run: npm run initialize'
  },
  VALIDATION_TIMEOUT: {
    message: 'validation exceeded timeout (30 seconds)',
    recovery: 'Check network and disk performance, then retry'
  },
  CACHE_INACCESSIBLE: {
    message: 'cache directory is not readable or accessible',
    recovery: 'Check file permissions: chmod -R u+rw .cache'
  },
  UNEXPECTED_ERROR: {
    message: 'unexpected validation error',
    recovery: 'Check logs and retry. If problem persists, delete cache and reinstall'
  }
};

// -------------------------------------------------------
// Helper Functions
// -------------------------------------------------------

/**
 * Resolve the cache directory for a given version.
 * Handles both relative (from cwd) and absolute paths.
 *
 * @param {string} version - Version string (e.g., 'v2.0.0' or '2.0.0')
 * @returns {string} Absolute path to version cache directory
 */
function resolveCacheDirectory(version) {
  const versionTag = version.toString().startsWith('v') ? version : `v${version}`;
  const cwd = process.cwd();
  return path.resolve(cwd, CACHE_BASE_DIR, versionTag);
}

/**
 * Format timestamp as ISO string for logging.
 *
 * @returns {string} ISO 8601 timestamp
 */
function getCurrentTimestamp() {
  return new Date().toISOString();
}

/**
 * Log a message with timestamp and severity.
 *
 * @param {string} level - 'info', 'warn', 'error'
 * @param {string} message - Log message
 */
function logMessage(level, message) {
  const timestamp = getCurrentTimestamp();
  const prefix = `[${timestamp}] [${level.toUpperCase()}]`;
  console.error(`${prefix} ${message}`);
}

/**
 * Format error with recovery suggestion.
 *
 * @param {string} errorKey - Key from ERROR_MESSAGES
 * @param {string} [details] - Additional error details
 * @returns {Object} Formatted error object
 */
function formatError(errorKey, details) {
  const template = ERROR_MESSAGES[errorKey] || ERROR_MESSAGES.UNEXPECTED_ERROR;
  return {
    message: template.message,
    details: details || '',
    recovery: template.recovery
  };
}

// -------------------------------------------------------
// Main Validation Functions (Exported)
// -------------------------------------------------------

/**
 * Validate npm package integrity at startup.
 *
 * This is the main entry point for startup validation. It orchestrates
 * checksum and manifest verification, logs results, and provides recovery
 * suggestions if validation fails.
 *
 * Called by: npm run validate, core-server.js startup, Step 45 (lifecycle manager)
 *
 * @param {Object} options - Configuration options
 * @param {string} [options.version='2.0.0'] - Package version to validate
 * @param {string} [options.versionDir] - Override cache directory (for testing)
 * @param {number} [options.timeout=30000] - Validation timeout in milliseconds
 * @returns {Promise<Object>} Validation result:
 *   {
 *     valid: boolean,
 *     version: string,
 *     versionDir: string,
 *     timestamp: string,
 *     checksumValid: boolean,
 *     manifestValid: boolean,
 *     metadata: Object | null,
 *     errors: Array<string>,
 *     recoverySteps: Array<string>
 *   }
 *
 * @example
 * // From npm script:
 * // npm run validate
 *
 * // Programmatic usage:
 * const result = await validateIntegrity({ version: '2.0.0' });
 * if (!result.valid) {
 *   console.error(result.errors[0]);
 *   console.info(result.recoverySteps[0]);
 *   process.exit(1);
 * }
 */
export async function validateIntegrity(options = {}) {
  const {
    version = '2.0.0',
    versionDir = resolveCacheDirectory(options.version || '2.0.0'),
    timeout = DEFAULT_TIMEOUT_MS
  } = options;

  const startTime = Date.now();
  const result = {
    valid: false,
    version,
    versionDir,
    timestamp: getCurrentTimestamp(),
    checksumValid: false,
    manifestValid: false,
    metadata: null,
    errors: [],
    recoverySteps: []
  };

  try {
    logMessage('info', `Validating npm package v${version} at ${versionDir}`);

    // Check timeout
    if (Date.now() - startTime > timeout) {
      const error = formatError('VALIDATION_TIMEOUT');
      result.errors.push(error.message);
      result.recoverySteps.push(error.recovery);
      logMessage('error', `${error.message}: ${error.recovery}`);
      return result;
    }

    // Validate package integrity using Step 8 utility
    const validationResult = await validatePackageIntegrity(versionDir, version);

    // Copy validation results
    result.checksumValid = validationResult.checksumValid;
    result.manifestValid = validationResult.manifestValid;
    result.valid = validationResult.valid;
    result.metadata = validationResult.metadata;

    // Map errors to recovery steps
    if (validationResult.errors.length > 0) {
      validationResult.errors.forEach(error => {
        result.errors.push(error);

        // Determine recovery suggestion based on error content
        if (error.includes('checksum') || error.includes('Checksum')) {
          result.recoverySteps.push(ERROR_MESSAGES.CHECKSUM_MISMATCH.recovery);
        } else if (error.includes('Manifest') || error.includes('manifest')) {
          result.recoverySteps.push(ERROR_MESSAGES.MANIFEST_INVALID.recovery);
        } else if (error.includes('not found') || error.includes('ENOENT')) {
          result.recoverySteps.push(ERROR_MESSAGES.PACKAGE_NOT_FOUND.recovery);
        } else {
          result.recoverySteps.push(ERROR_MESSAGES.UNEXPECTED_ERROR.recovery);
        }
      });
    }

    // Log results
    if (result.valid) {
      logMessage('info', `✅ Package valid (${result.metadata?.continueVersion || 'unknown'} Continue)`);
      logMessage('info', `   Checksum: ✓ | Manifest: ✓ | Release: ${result.metadata?.releaseDate || 'unknown'}`);
    } else {
      logMessage('error', `❌ Package validation failed:`);
      result.errors.forEach((err, i) => {
        logMessage('error', `   [${i + 1}] ${err}`);
      });
      result.recoverySteps.forEach((step, i) => {
        logMessage('info', `   Recovery [${i + 1}]: ${step}`);
      });
    }

    const duration = Date.now() - startTime;
    logMessage('info', `Validation completed in ${duration}ms`);

    return result;

  } catch (error) {
    const err = formatError('UNEXPECTED_ERROR', error.message);
    result.errors.push(`${err.message}: ${err.details}`);
    result.recoverySteps.push(err.recovery);
    logMessage('error', `${err.message}: ${error.message}`);
    logMessage('info', `Recovery: ${err.recovery}`);
    return result;
  }
}

/**
 * Quick integrity check for focused validation.
 *
 * Returns true/false without detailed logging. Used by tests (Step 31)
 * and quick startup checks that don't need full orchestration.
 *
 * @param {string} [version='2.0.0'] - Package version to validate
 * @param {string} [versionDir] - Override cache directory (for testing)
 * @returns {Promise<boolean>} True if valid, false otherwise
 *
 * @example
 * if (await checkPackageIntegrity()) {
 *   // Start server
 * } else {
 *   console.error('Package validation failed');
 *   process.exit(1);
 * }
 */
export async function checkPackageIntegrity(version = '2.0.0', versionDir) {
  try {
    const cacheDir = versionDir || resolveCacheDirectory(version);
    const result = await validatePackageIntegrity(cacheDir, version);
    return result.valid;
  } catch (error) {
    logMessage('error', `Integrity check failed: ${error.message}`);
    return false;
  }
}

/**
 * Perform diagnostic health check of the cache.
 *
 * Verifies:
 * - Cache directory exists and is readable
 * - All required files are present
 * - manifest.json is valid JSON
 * - Basic file permissions
 *
 * Called by: npm run health-check, diagnostics, troubleshooting
 *
 * @param {string} [version='2.0.0'] - Package version to check
 * @param {string} [versionDir] - Override cache directory (for testing)
 * @returns {Promise<Object>} Health check result:
 *   {
 *     healthy: boolean,
 *     version: string,
 *     cacheDir: string,
 *     timestamp: string,
 *     files: {
 *       package: { exists: boolean, size: number },
 *       checksum: { exists: boolean, readable: boolean },
 *       manifest: { exists: boolean, valid: boolean }
 *     },
 *     diagnostics: string[]
 *   }
 *
 * @example
 * const health = await performHealthCheck('2.0.0');
 * if (!health.healthy) {
 *   health.diagnostics.forEach(msg => console.log(msg));
 * }
 */
export async function performHealthCheck(version = '2.0.0', versionDir) {
  const cacheDir = versionDir || resolveCacheDirectory(version);
  const versionTag = version.toString().startsWith('v') ? version : `v${version}`;

  const result = {
    healthy: true,
    version,
    cacheDir,
    timestamp: getCurrentTimestamp(),
    files: {
      package: { exists: false, size: 0, readable: false },
      checksum: { exists: false, readable: false },
      manifest: { exists: false, valid: false }
    },
    diagnostics: []
  };

  try {
    logMessage('info', `Starting health check for v${version} at ${cacheDir}`);

    // Check directory accessibility
    try {
      await fs.access(cacheDir);
    } catch (error) {
      result.healthy = false;
      result.diagnostics.push(`❌ Cache directory not accessible: ${cacheDir}`);
      logMessage('error', `Cache directory not accessible: ${error.message}`);
      return result;
    }

    // Check package file
    const packageName = `continue-${versionTag}.tgz`;
    const packagePath = path.join(cacheDir, packageName);
    try {
      const stats = await fs.stat(packagePath);
      result.files.package.exists = true;
      result.files.package.size = stats.size;
      result.files.package.readable = (stats.mode & 0o400) !== 0;
      result.diagnostics.push(`✅ Package file exists (${stats.size} bytes)`);
    } catch (error) {
      result.healthy = false;
      result.diagnostics.push(`❌ Package file missing: ${packageName}`);
    }

    // Check checksum file
    const checksumName = `${packageName}.sha256`;
    const checksumPath = path.join(cacheDir, checksumName);
    try {
      await fs.access(checksumPath);
      result.files.checksum.exists = true;
      result.files.checksum.readable = true;
      result.diagnostics.push(`✅ Checksum file exists`);
    } catch (error) {
      result.healthy = false;
      result.diagnostics.push(`❌ Checksum file missing: ${checksumName}`);
    }

    // Check manifest file
    const manifestName = `manifest-${versionTag}.json`;
    const manifestPath = path.join(cacheDir, manifestName);
    try {
      const content = await fs.readFile(manifestPath, 'utf-8');
      const manifest = JSON.parse(content);
      result.files.manifest.exists = true;
      result.files.manifest.valid = true;
      result.diagnostics.push(`✅ Manifest valid (Continue ${manifest.continueVersion})`);
    } catch (error) {
      if (error.code === 'ENOENT') {
        result.healthy = false;
        result.diagnostics.push(`❌ Manifest file missing: ${manifestName}`);
      } else {
        result.healthy = false;
        result.diagnostics.push(`❌ Manifest invalid or unreadable: ${error.message}`);
      }
    }

    // List all files in directory
    try {
      const files = await fs.readdir(cacheDir);
      result.diagnostics.push(`📁 Cache contents (${files.length} items): ${files.join(', ')}`);
    } catch (error) {
      result.diagnostics.push(`⚠️ Could not list cache contents: ${error.message}`);
    }

    // Log diagnostics
    logMessage('info', `Health check ${result.healthy ? '✅ PASSED' : '❌ FAILED'}:`);
    result.diagnostics.forEach(msg => {
      logMessage('info', `   ${msg}`);
    });

    return result;

  } catch (error) {
    result.healthy = false;
    result.diagnostics.push(`❌ Health check error: ${error.message}`);
    logMessage('error', `Health check error: ${error.message}`);
    return result;
  }
}

// -------------------------------------------------------
// CLI Entry Points (for npm scripts)
// -------------------------------------------------------

/**
 * Entry point for `npm run validate`
 * Validates package integrity and exits with appropriate code.
 */
if (import.meta.url === `file://${process.argv[1]}`) {
  const result = await validateIntegrity();
  process.exit(result.valid ? 0 : 1);
}
