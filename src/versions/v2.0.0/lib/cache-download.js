/**
 * npm Package Cache Download Manager
 *
 * Implements cache-first download strategy for Continue npm packages.
 * Provides functionality to:
 * - Check local cache and validate with Step 8 integrity checks
 * - Download from npm registry if package missing or invalid
 * - Generate checksums for downloaded packages
 * - Handle network errors and timeouts gracefully
 *
 * This module is ESM (ES Modules) and uses only Node.js built-ins.
 * No external npm dependencies.
 *
 * @module src/versions/v2.0.0/lib/cache-download.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 8 (integrity utility), 12 (startup validation), 35 (download & verify)
 */

import https from 'https';
import fs from 'fs/promises';
import fsSync from 'fs';
import path from 'path';
import crypto from 'crypto';
import { validatePackageIntegrity } from './integrity.js';

// -------------------------------------------------------
// Custom Error Types
// -------------------------------------------------------

/**
 * Base error class for cache download failures.
 * @class CacheDownloadError
 * @extends Error
 */
class CacheDownloadError extends Error {
  constructor(message, statusCode = null, url = null) {
    super(message);
    this.name = 'CacheDownloadError';
    this.statusCode = statusCode;
    this.url = url;
  }
}

/**
 * Error class for network-related failures.
 * @class NetworkError
 * @extends CacheDownloadError
 */
class NetworkError extends CacheDownloadError {
  constructor(message, originalError) {
    super(message);
    this.name = 'NetworkError';
    this.originalError = originalError;
  }
}

// -------------------------------------------------------
// Logging Utility
// -------------------------------------------------------

/**
 * Log message to download log file with timestamp.
 * @param {string} message - Message to log
 * @param {string} logDir - Directory to write log file
 */
async function logToDownloadLog(message, logDir) {
  try {
    const timestamp = new Date().toISOString();
    const logPath = path.join(logDir, '.download-log');
    const logEntry = `[${timestamp}] ${message}\n`;

    await fs.appendFile(logPath, logEntry, 'utf-8');
  } catch (error) {
    // Silently fail if logging fails; don't block download process
    console.error(`Failed to write download log: ${error.message}`);
  }
}

// -------------------------------------------------------
// Main Export Functions
// -------------------------------------------------------

/**
 * Download package if not in cache, or return cached version if valid.
 *
 * Implements cache-first strategy:
 * 1. Check local cache using Step 8 validation
 * 2. If valid: return cached package (no download)
 * 3. If invalid/missing: download from npm registry
 * 4. Validate downloaded package
 * 5. Return result object
 *
 * @param {string} version - Version string (e.g., '2.0.0' or 'v2.0.0')
 * @param {string} cacheDir - Absolute path to cache directory (e.g., '.cache/npm-packages/v2.0.0')
 * @param {Object} options - Optional configuration
 *   @param {number} options.timeout - Download timeout in milliseconds (default: 60000)
 *   @param {number} options.maxRetries - Max retry attempts on checksum mismatch (default: 1)
 * @returns {Promise<Object>} Result object:
 *   {
 *     cached: boolean,           // true if returned from cache
 *     valid: boolean,            // true if package valid and ready to use
 *     packagePath: string,       // absolute path to .tgz file
 *     downloadTime: number,      // milliseconds (0 if cached)
 *     errors: string[]           // error messages (empty if successful)
 *   }
 *
 * @example
 * const result = await downloadPackageIfNeeded(
 *   '2.0.0',
 *   'E:\\GitRepos\\ContinueVS\\.cache\\npm-packages\\v2.0.0'
 * );
 *
 * if (result.valid) {
 *   console.log(`Ready: ${result.packagePath}`);
 *   console.log(`Cached: ${result.cached}`);
 * } else {
 *   result.errors.forEach(err => console.error(err));
 * }
 */
export async function downloadPackageIfNeeded(version, cacheDir, options = {}) {
  const timeout = options.timeout || 60000;
  const maxRetries = options.maxRetries || 1;
  const startTime = Date.now();

  const result = {
    cached: false,
    valid: false,
    packagePath: '',
    downloadTime: 0,
    errors: []
  };

  try {
    // Ensure cache directory exists
    try {
      await fs.mkdir(cacheDir, { recursive: true });
    } catch (error) {
      result.errors.push(`Cannot create cache directory: ${error.message}`);
      return result;
    }

    // Step 1: Check local cache using Step 8 validation
    await logToDownloadLog(`Checking local cache for version ${version}`, cacheDir);
    const validationResult = await validatePackageIntegrity(cacheDir, version);

    if (validationResult.valid) {
      // Cache hit: package is valid
      result.cached = true;
      result.valid = true;
      result.packagePath = validationResult.packagePath;
      result.downloadTime = 0;
      await logToDownloadLog(`Cache hit: ${validationResult.packagePath}`, cacheDir);
      return result;
    }

    // Step 2: Cache miss or invalid - download from npm registry
    await logToDownloadLog(`Cache miss or invalid. Downloading version ${version}...`, cacheDir);

    // Download with retry logic for checksum mismatch
    let downloadSuccess = false;
    let downloadAttempt = 0;

    while (downloadAttempt <= maxRetries && !downloadSuccess) {
      downloadAttempt++;
      const packageName = `continue-${version.toString().startsWith('v') ? version : `v${version}`}`;

      try {
        await logToDownloadLog(
          `Download attempt ${downloadAttempt}/${maxRetries + 1}: ${packageName}`,
          cacheDir
        );

        const downloadResult = await downloadPackageFromRegistry(
          packageName,
          version,
          cacheDir,
          { timeout }
        );

        if (!downloadResult.success) {
          result.errors.push(downloadResult.error);
          downloadAttempt = maxRetries + 1; // Exit retry loop
          break;
        }

        const packagePath = downloadResult.filePath;

        // Step 3: Generate checksum for downloaded file
        const checksumResult = await generateChecksum(packagePath);
        await logToDownloadLog(`Generated checksum: ${checksumResult.hash}`, cacheDir);

        // Step 4: Re-validate package using Step 8
        const revalidationResult = await validatePackageIntegrity(cacheDir, version);

        if (revalidationResult.valid) {
          result.valid = true;
          result.packagePath = packagePath;
          result.downloadTime = Date.now() - startTime;
          downloadSuccess = true;
          await logToDownloadLog(
            `Validation passed after download. Time: ${result.downloadTime}ms`,
            cacheDir
          );
        } else {
          // Validation failed: delete corrupted file and retry
          await logToDownloadLog(
            `Validation failed on attempt ${downloadAttempt}: ${revalidationResult.errors.join(', ')}`,
            cacheDir
          );

          try {
            await fs.unlink(packagePath);
            const checksumPath = `${packagePath}.sha256`;
            try {
              await fs.unlink(checksumPath);
            } catch {
              // Ignore if checksum file doesn't exist
            }
            await logToDownloadLog(`Cleaned up invalid package: ${packagePath}`, cacheDir);
          } catch (cleanupError) {
            await logToDownloadLog(
              `Failed to clean up: ${cleanupError.message}`,
              cacheDir
            );
          }

          if (downloadAttempt <= maxRetries) {
            await logToDownloadLog(`Retrying... (${maxRetries - downloadAttempt} retries remaining)`, cacheDir);
          }
        }
      } catch (downloadError) {
        result.errors.push(downloadError.message);
        downloadAttempt = maxRetries + 1; // Exit retry loop
      }
    }

    result.downloadTime = Date.now() - startTime;

  } catch (error) {
    result.errors.push(`Unexpected error: ${error.message}`);
    await logToDownloadLog(`Unexpected error: ${error.message}`, cacheDir).catch(() => {});
  }

  return result;
}

/**
 * Download package from npm registry.
 *
 * Fetches continue-{version}.tgz from npm registry using https.
 * Streams response to a temporary file, then renames on success.
 *
 * @param {string} packageName - Package name with version (e.g., 'continue-v2.0.0')
 * @param {string} version - Version string (e.g., '2.0.0' or 'v2.0.0')
 * @param {string} targetDir - Target directory to save .tgz file
 * @param {Object} options - Optional configuration
 *   @param {number} options.timeout - Download timeout in milliseconds (default: 60000)
 * @returns {Promise<Object>} Download result:
 *   {
 *     success: boolean,          // true if download completed
 *     filePath: string,          // absolute path to .tgz file (empty if failed)
 *     downloadTime: number,      // milliseconds
 *     error: string              // error message (empty if successful)
 *   }
 *
 * @example
 * const result = await downloadPackageFromRegistry(
 *   'continue-v2.0.0',
 *   '2.0.0',
 *   'E:\\cache\\npm-packages\\v2.0.0'
 * );
 *
 * if (result.success) {
 *   console.log(`Downloaded to: ${result.filePath}`);
 * } else {
 *   console.error(result.error);
 * }
 */
export async function downloadPackageFromRegistry(packageName, version, targetDir, options = {}) {
  const timeout = options.timeout || 60000;
  const startTime = Date.now();
  const versionTag = version.toString().startsWith('v') ? version : `v${version}`;

  const result = {
    success: false,
    filePath: '',
    downloadTime: 0,
    error: ''
  };

  // Build npm registry URL
  const registryUrl = `https://registry.npmjs.org/continue/-/continue-${versionTag}.tgz`;
  const fileName = `continue-${versionTag}.tgz`;
  const filePath = path.join(targetDir, fileName);
  const tempPath = `${filePath}.tmp`;

  return new Promise((resolve) => {
    https.get(registryUrl, { timeout }, (response) => {
      if (response.statusCode !== 200) {
        result.error = `HTTP ${response.statusCode} from npm registry: ${registryUrl}`;
        resolve(result);
        return;
      }

      const writeStream = fsSync.createWriteStream(tempPath);
      let receivedBytes = 0;

      response.on('data', (chunk) => {
        receivedBytes += chunk.length;
      });

      response.pipe(writeStream);

      writeStream.on('finish', async () => {
        try {
          // Verify file was written
          const stats = await fs.stat(tempPath);
          if (stats.size === 0) {
            result.error = 'Downloaded file is empty';
            await fs.unlink(tempPath).catch(() => {});
            resolve(result);
            return;
          }

          // Rename temp file to final location
          await fs.rename(tempPath, filePath);

          result.success = true;
          result.filePath = filePath;
          result.downloadTime = Date.now() - startTime;
          resolve(result);
        } catch (error) {
          result.error = `Failed to finalize download: ${error.message}`;
          await fs.unlink(tempPath).catch(() => {});
          resolve(result);
        }
      });

      writeStream.on('error', (error) => {
        result.error = `Write stream error: ${error.message}`;
        fs.unlink(tempPath).catch(() => {});
        resolve(result);
      });

    }).on('timeout', () => {
      result.error = `Download timeout after ${timeout}ms`;
      fs.unlink(tempPath).catch(() => {});
      resolve(result);
    }).on('error', (error) => {
      result.error = `Network error: ${error.message}`;
      fs.unlink(tempPath).catch(() => {});
      resolve(result);
    });
  });
}

/**
 * Generate SHA256 checksum for a package file and write .sha256 file.
 *
 * Computes SHA256 hash of the .tgz file and writes to {fileName}.sha256
 * in npm standard format: "{hash}  {filename}\n"
 *
 * @param {string} filePath - Absolute path to .tgz file
 * @returns {Promise<Object>} Checksum result:
 *   {
 *     hash: string,              // SHA256 hash in lowercase hex (64 characters)
 *     checksumPath: string       // absolute path to .sha256 file
 *   }
 *
 * @throws {CacheDownloadError} If file cannot be read or written
 *
 * @example
 * const result = await generateChecksum(
 *   'E:\\cache\\npm-packages\\v2.0.0\\continue-v2.0.0.tgz'
 * );
 * console.log(`Hash: ${result.hash}`);
 * console.log(`Written to: ${result.checksumPath}`);
 */
export async function generateChecksum(filePath) {
  const checksumPath = `${filePath}.sha256`;
  const fileName = path.basename(filePath);

  try {
    // Read file and compute SHA256
    const fileContent = await fs.readFile(filePath);
    const hash = crypto.createHash('sha256').update(fileContent).digest('hex');

    // Write .sha256 file in npm standard format
    const checksumContent = `${hash}  ${fileName}\n`;
    await fs.writeFile(checksumPath, checksumContent, 'utf-8');

    return {
      hash: hash.toLowerCase(),
      checksumPath
    };
  } catch (error) {
    throw new CacheDownloadError(
      `Failed to generate checksum for ${filePath}: ${error.message}`
    );
  }
}

// -------------------------------------------------------
// Error Export (for test access)
// -------------------------------------------------------

export { CacheDownloadError, NetworkError };
