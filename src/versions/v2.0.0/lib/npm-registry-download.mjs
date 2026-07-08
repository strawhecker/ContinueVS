/**
 * npm Registry Package Downloader with Cache Fallback
 *
 * Implements a registry-first download strategy for Continue npm package:
 * 1. Attempt download from npm registry
 * 2. Validate downloaded package against manifest checksum
 * 3. Fall back to cached package if registry unavailable
 * 4. Validate cached package before returning
 * 5. Log all operations with timestamps
 *
 * Network Errors Handled:
 * - Connection timeout
 * - DNS resolution failure
 * - HTTP error codes (404, 403, 500, etc.)
 * - Incomplete/corrupted download
 * - Registry unavailable
 *
 * Uses Node.js built-ins only (https, fs, path, crypto).
 * No external npm dependencies.
 *
 * @module src/versions/v2.0.0/lib/npm-registry-download.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 11 (cache download), 35 (download & verify), 37 (checksums)
 */

import https from 'https';
import fs from 'fs/promises';
import fsSync from 'fs';
import path from 'path';
import crypto from 'crypto';

// -------------------------------------------------------
// Custom Error Types
// -------------------------------------------------------

/**
 * Base error for download failures.
 */
class DownloadError extends Error {
  constructor(message, details = {}) {
    super(message);
    this.name = 'DownloadError';
    this.details = details;
  }
}

/**
 * Network-related download errors (retryable).
 */
class NetworkError extends DownloadError {
  constructor(message, details = {}) {
    super(message, details);
    this.name = 'NetworkError';
    this.retryable = true;
  }
}

/**
 * Checksum validation errors (not retryable).
 */
class ChecksumError extends DownloadError {
  constructor(message, expected, computed) {
    super(message, { expected, computed });
    this.name = 'ChecksumError';
    this.retryable = false;
  }
}

/**
 * Cache access errors.
 */
class CacheError extends DownloadError {
  constructor(message, details = {}) {
    super(message, details);
    this.name = 'CacheError';
    this.retryable = false;
  }
}

// -------------------------------------------------------
// Logging Utilities
// -------------------------------------------------------

/**
 * Log message to download log file.
 *
 * @param {string} message - Message to log
 * @param {string} cacheDir - Cache directory path
 * @param {Object} options - Logging options
 * @param {boolean} options.silent - Suppress console output
 */
async function logToDownloadLog(message, cacheDir, options = {}) {
  const timestamp = new Date().toISOString();
  const logEntry = `[${timestamp}] ${message}\n`;
  const logPath = path.join(cacheDir, 'download.log');

  try {
    await fs.appendFile(logPath, logEntry);
    if (!options.silent) {
      console.log(`${timestamp} ${message}`);
    }
  } catch (error) {
    // Log file write failed, but don't block download
    if (!options.silent) {
      console.error(`⚠️  Failed to write log: ${error.message}`);
    }
  }
}

/**
 * Compute SHA256 hash of a file.
 *
 * @param {string} filePath - Path to file
 * @returns {Promise<string>} SHA256 hash (lowercase hex)
 */
async function computeSHA256(filePath) {
  const fileBuffer = await fs.readFile(filePath);
  return crypto.createHash('sha256').update(fileBuffer).digest('hex').toLowerCase();
}

/**
 * Load manifest.json and extract expected checksum.
 *
 * @param {string} manifestPath - Path to manifest.json
 * @returns {Promise<Object>} { version, expectedSha256 }
 * @throws {Error} If manifest missing or invalid
 */
async function loadManifestChecksum(manifestPath) {
  try {
    const content = await fs.readFile(manifestPath, 'utf-8');
    const manifest = JSON.parse(content);

    if (!manifest.checksums || !manifest.checksums.sha256) {
      throw new Error('Manifest missing checksums.sha256');
    }

    return {
      version: manifest.version,
      expectedSha256: manifest.checksums.sha256
    };
  } catch (error) {
    throw new CacheError(
      `Failed to load manifest: ${error.message}`,
      { manifestPath }
    );
  }
}

// -------------------------------------------------------
// Registry Download Functions
// -------------------------------------------------------

/**
 * Download package from npm registry with retry logic.
 *
 * @param {string} version - Package version (e.g., '2.0.0' or 'v2.0.0')
 * @param {string} targetDir - Target directory for .tgz
 * @param {Object} options - Download options
 * @param {number} options.timeout - Request timeout (ms, default 60000)
 * @param {number} options.maxRetries - Retry attempts (default 3)
 * @returns {Promise<Object>} { success, filePath, hash, downloadTime, error, fallbackUsed }
 */
async function downloadFromRegistry(version, targetDir, options = {}) {
  const timeout = options.timeout || 60000;
  const maxRetries = options.maxRetries || 3;
  const startTime = Date.now();

  const result = {
    success: false,
    filePath: '',
    hash: '',
    downloadTime: 0,
    error: '',
    fallbackUsed: false
  };

  const versionTag = version.toString().startsWith('v') ? version : `v${version}`;
  const fileName = `continue-${versionTag}.tgz`;
  const filePath = path.join(targetDir, fileName);
  const tempPath = `${filePath}.tmp`;

  const registryUrl = `https://registry.npmjs.org/continue/-/continue-${versionTag}.tgz`;

  // Ensure target directory exists
  try {
    await fs.mkdir(targetDir, { recursive: true });
  } catch (error) {
    result.error = `Failed to create cache directory: ${error.message}`;
    return result;
  }

  let downloadAttempt = 0;
  let lastError = null;

  while (downloadAttempt < maxRetries) {
    downloadAttempt++;

    try {
      await logToDownloadLog(
        `Registry download attempt ${downloadAttempt}/${maxRetries}: ${registryUrl}`,
        targetDir,
        { silent: true }
      );

      // Perform HTTPS download
      const downloadResult = await new Promise((resolve) => {
        https.get(registryUrl, { timeout }, (response) => {
          if (response.statusCode !== 200) {
            const error = new NetworkError(
              `HTTP ${response.statusCode} from npm registry`,
              { statusCode: response.statusCode, url: registryUrl }
            );
            resolve({ error });
            return;
          }

          const writeStream = fsSync.createWriteStream(tempPath);
          let receivedBytes = 0;

          response.on('data', (chunk) => {
            receivedBytes += chunk.length;
          });

          response.pipe(writeStream);

          writeStream.on('finish', () => {
            resolve({ success: true, receivedBytes });
          });

          writeStream.on('error', (error) => {
            resolve({ error: new NetworkError(`Write failed: ${error.message}`) });
          });

        }).on('timeout', () => {
          resolve({ error: new NetworkError(`Download timeout after ${timeout}ms`) });
        }).on('error', (error) => {
          resolve({ error: new NetworkError(`Connection failed: ${error.message}`) });
        });
      });

      if (downloadResult.error) {
        lastError = downloadResult.error;
        if (downloadAttempt < maxRetries) {
          await logToDownloadLog(
            `Retry scheduled (${maxRetries - downloadAttempt} remaining): ${lastError.message}`,
            targetDir,
            { silent: true }
          );
          continue;
        }
        throw lastError;
      }

      // Download succeeded, verify file
      const stats = await fs.stat(tempPath);
      if (stats.size === 0) {
        await fs.unlink(tempPath).catch(() => {});
        throw new DownloadError('Downloaded file is empty');
      }

      // Finalize download
      await fs.rename(tempPath, filePath);
      result.success = true;
      result.filePath = filePath;
      result.hash = await computeSHA256(filePath);
      result.downloadTime = Date.now() - startTime;

      await logToDownloadLog(
        `✅ Registry download successful: ${fileName} (${stats.size} bytes, SHA256: ${result.hash})`,
        targetDir,
        { silent: true }
      );

      return result;

    } catch (error) {
      lastError = error;
      await fs.unlink(tempPath).catch(() => {});

      if (!error.retryable || downloadAttempt >= maxRetries) {
        result.error = error.message;
        return result;
      }
    }
  }

  result.error = lastError?.message || 'Download failed: unknown error';
  return result;
}

// -------------------------------------------------------
// Cache Fallback Functions
// -------------------------------------------------------

/**
 * Check cache directory for existing package.
 *
 * @param {string} version - Package version
 * @param {string} cacheDir - Cache directory path
 * @returns {Promise<Object>} { exists, filePath, hash }
 */
async function checkCacheForPackage(version, cacheDir) {
  const versionTag = version.toString().startsWith('v') ? version : `v${version}`;
  const fileName = `continue-${versionTag}.tgz`;
  const filePath = path.join(cacheDir, fileName);

  try {
    const stats = await fs.stat(filePath);
    if (stats.isFile() && stats.size > 0) {
      const hash = await computeSHA256(filePath);
      return { exists: true, filePath, hash, size: stats.size };
    }
  } catch (error) {
    // File doesn't exist or can't be read
  }

  return { exists: false, filePath: '', hash: '', size: 0 };
}

/**
 * Validate cached package against manifest checksum.
 *
 * @param {string} cachedPath - Path to cached .tgz
 * @param {string} expectedHash - Expected SHA256 from manifest
 * @returns {Promise<Object>} { valid, computedHash, error }
 */
async function validateCachedPackage(cachedPath, expectedHash) {
  try {
    const computedHash = await computeSHA256(cachedPath);
    const valid = computedHash === expectedHash;

    return {
      valid,
      computedHash,
      expectedHash,
      error: valid ? null : `Hash mismatch: expected ${expectedHash}, got ${computedHash}`
    };
  } catch (error) {
    return {
      valid: false,
      computedHash: '',
      expectedHash,
      error: `Failed to validate cache: ${error.message}`
    };
  }
}

// -------------------------------------------------------
// Main Public API
// -------------------------------------------------------

/**
 * Download Continue npm package with registry-first, fallback-to-cache strategy.
 *
 * Process:
 * 1. Attempt registry download
 * 2. If successful, validate checksum
 * 3. If registry fails, check cache directory
 * 4. Validate cached package
 * 5. Return path to valid package or throw error
 *
 * @param {string} version - Package version (e.g., '2.0.0' or 'v2.0.0')
 * @param {string} cacheDir - Cache directory path (e.g., '.cache/npm-packages/v2.0.0')
 * @param {string} manifestPath - Path to manifest.json (for checksum validation)
 * @param {Object} options - Download options
 * @param {number} options.timeout - Request timeout (ms, default 60000)
 * @param {number} options.maxRetries - Retry attempts (default 3)
 * @param {boolean} options.dryRun - Don't actually download, only check cache
 * @returns {Promise<Object>} Result object:
 *   {
 *     success: boolean,
 *     packagePath: string,              // Path to valid .tgz
 *     packageHash: string,              // SHA256 of downloaded/cached package
 *     downloadedFromRegistry: boolean,  // True if fresh download, false if from cache
 *     fallbackUsed: boolean,            // True if fallback to cache was necessary
 *     totalTime: number,                // Total time in milliseconds
 *     message: string,                  // Human-readable summary
 *     error: string | null              // Error message if failed
 *   }
 *
 * @throws {Error} If both registry and cache fail
 *
 * @example
 * const result = await downloadWithFallback(
 *   '2.0.0',
 *   '.cache/npm-packages/v2.0.0',
 *   'src/versions/v2.0.0/manifest.json'
 * );
 *
 * if (result.success) {
 *   console.log(`✅ Package ready: ${result.packagePath}`);
 *   if (result.fallbackUsed) {
 *     console.log('   (Used cached version due to registry unavailability)');
 *   }
 * } else {
 *   console.error(`❌ ${result.error}`);
 * }
 */
export async function downloadWithFallback(version, cacheDir, manifestPath, options = {}) {
  const startTime = Date.now();
  const result = {
    success: false,
    packagePath: '',
    packageHash: '',
    downloadedFromRegistry: false,
    fallbackUsed: false,
    totalTime: 0,
    message: '',
    error: null
  };

  try {
    // Step 1: Load expected checksum from manifest
    const manifestData = await loadManifestChecksum(manifestPath);
    const expectedHash = manifestData.expectedSha256;

    // Step 2: Dry-run mode (check cache only)
    if (options.dryRun) {
      const cacheCheck = await checkCacheForPackage(version, cacheDir);
      if (!cacheCheck.exists) {
        result.error = 'Dry-run mode: package not in cache';
        return result;
      }

      const validation = await validateCachedPackage(cacheCheck.filePath, expectedHash);
      if (!validation.valid) {
        result.error = `Dry-run mode: cached package invalid - ${validation.error}`;
        return result;
      }

      result.success = true;
      result.packagePath = cacheCheck.filePath;
      result.packageHash = cacheCheck.hash;
      result.downloadedFromRegistry = false;
      result.fallbackUsed = false;
      result.message = '✅ Dry-run: cached package is valid';
      result.totalTime = Date.now() - startTime;
      return result;
    }

    // Step 3: Attempt registry download
    await logToDownloadLog(
      `Starting download for version ${version}...`,
      cacheDir,
      { silent: true }
    );

    const downloadResult = await downloadFromRegistry(version, cacheDir, {
      timeout: options.timeout || 60000,
      maxRetries: options.maxRetries || 3
    });

    // Step 4: If registry download succeeded, validate checksum
    if (downloadResult.success) {
      const validation = await validateCachedPackage(downloadResult.filePath, expectedHash);

      if (validation.valid) {
        result.success = true;
        result.packagePath = downloadResult.filePath;
        result.packageHash = downloadResult.hash;
        result.downloadedFromRegistry = true;
        result.fallbackUsed = false;
        result.message = `✅ Registry download successful (${downloadResult.downloadTime}ms)`;
        result.totalTime = Date.now() - startTime;

        await logToDownloadLog(
          `✅ Download complete: ${result.message}`,
          cacheDir,
          { silent: true }
        );

        return result;
      }

      // Checksum mismatch on registry download - log and fall back
      await logToDownloadLog(
        `⚠️  Registry download checksum mismatch - falling back to cache`,
        cacheDir,
        { silent: true }
      );
    }

    // Step 5: Registry failed or checksum mismatch - try cache fallback
    await logToDownloadLog(
      `Registry unavailable or invalid - checking cache...`,
      cacheDir,
      { silent: true }
    );

    const cacheCheck = await checkCacheForPackage(version, cacheDir);

    if (!cacheCheck.exists) {
      result.error = (
        `Package not available: registry download failed and no cached version found. ` +
        `Details: ${downloadResult.error || 'unknown error'}`
      );
      await logToDownloadLog(`❌ ${result.error}`, cacheDir, { silent: true });
      return result;
    }

    // Step 6: Validate cached package
    const cacheValidation = await validateCachedPackage(cacheCheck.filePath, expectedHash);

    if (!cacheValidation.valid) {
      result.error = (
        `Cached package is corrupted or invalid (expected ${expectedHash}, ` +
        `got ${cacheValidation.computedHash}). Original error: ${downloadResult.error}`
      );
      await logToDownloadLog(`❌ ${result.error}`, cacheDir, { silent: true });
      return result;
    }

    // Step 7: Cache fallback succeeded
    result.success = true;
    result.packagePath = cacheCheck.filePath;
    result.packageHash = cacheCheck.hash;
    result.downloadedFromRegistry = false;
    result.fallbackUsed = true;
    result.message = (
      `⚠️  Using cached version (${cacheCheck.size} bytes) - ` +
      `registry unavailable: ${downloadResult.error}`
    );
    result.totalTime = Date.now() - startTime;

    await logToDownloadLog(
      `✅ Fallback succeeded: using cached package`,
      cacheDir,
      { silent: true }
    );

    return result;

  } catch (error) {
    result.error = error instanceof DownloadError
      ? error.message
      : `Unexpected error: ${error.message}`;

    await logToDownloadLog(
      `❌ Error: ${result.error}`,
      cacheDir,
      { silent: true }
    ).catch(() => {});

    return result;
  }
}

/**
 * Check if a package is available (registry or cache).
 *
 * Quick availability check without actual download.
 *
 * @param {string} version - Package version
 * @param {string} cacheDir - Cache directory
 * @returns {Promise<boolean>} True if package available
 */
export async function isPackageAvailable(version, cacheDir) {
  const check = await checkCacheForPackage(version, cacheDir);
  return check.exists;
}

// -------------------------------------------------------
// Error Exports (for testing)
// -------------------------------------------------------

export { DownloadError, NetworkError, ChecksumError, CacheError };
