/**
 * Checksum Generation Utility for npm Packages
 *
 * Computes SHA256 and SHA512 cryptographic hashes for the Continue npm package
 * and manages checksum files and manifest updates.
 *
 * This module is ESM (ES Modules) and uses only Node.js built-ins.
 * No external npm dependencies.
 *
 * @module src/versions/v2.0.0/lib/generate-checksums.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 31 (integrity module), 35 (package download), 36 (package validation), 37 (checksum generation)
 */

import fs from 'fs/promises';
import path from 'path';
import crypto from 'crypto';
import { fileURLToPath } from 'url';

// -------------------------------------------------------
// Custom Error Types
// -------------------------------------------------------

/**
 * Error class for checksum generation failures.
 * @class ChecksumGenerationError
 * @extends Error
 */
class ChecksumGenerationError extends Error {
  constructor(message, operation = 'unknown', context = {}) {
    super(message);
    this.name = 'ChecksumGenerationError';
    this.operation = operation;
    this.context = context;
  }
}

// -------------------------------------------------------
// Core Functions
// -------------------------------------------------------

/**
 * Compute SHA256 and SHA512 hashes for a package file.
 *
 * @async
 * @param {string} packagePath - Absolute path to the .tgz file
 * @param {Object} options - Configuration options
 * @param {boolean} options.throwOnMissing - Throw if file not found (default: true)
 * @returns {Promise<{sha256: string, sha512: string, packagePath: string, timestampUtc: string, fileSizeBytes: number}>}
 * @throws {ChecksumGenerationError} If file is unreadable or operation fails
 *
 * @example
 * const result = await generateChecksums('/path/to/continue-2.0.0.tgz');
 * console.log(result.sha256); // "a1b2c3d4..."
 */
export async function generateChecksums(packagePath, options = {}) {
  const { throwOnMissing = true } = options;

  try {
    // Verify file exists and is readable
    const stats = await fs.stat(packagePath);
    if (!stats.isFile()) {
      throw new Error(`Path is not a file: ${packagePath}`);
    }

    // Read file and compute hashes
    const fileBuffer = await fs.readFile(packagePath);

    const sha256 = crypto
      .createHash('sha256')
      .update(fileBuffer)
      .digest('hex')
      .toLowerCase();

    const sha512 = crypto
      .createHash('sha512')
      .update(fileBuffer)
      .digest('hex')
      .toLowerCase();

    return {
      sha256,
      sha512,
      packagePath,
      timestampUtc: new Date().toISOString(),
      fileSizeBytes: stats.size,
    };
  } catch (error) {
    if (error.code === 'ENOENT' && !throwOnMissing) {
      return null;
    }
    throw new ChecksumGenerationError(
      `Failed to compute checksums for ${packagePath}: ${error.message}`,
      'compute',
      { packagePath, originalError: error.message }
    );
  }
}

/**
 * Write checksums to a file in sha256sum/sha512sum compatible format.
 *
 * Format:
 *   <hash>  <filename>
 *   <hash>  <filename>
 *
 * @async
 * @param {Object} checksums - Checksums object with sha256 and sha512 fields
 * @param {string} checksums.sha256 - SHA256 hash (lowercase hex)
 * @param {string} checksums.sha512 - SHA512 hash (lowercase hex)
 * @param {string} outputPath - Absolute path where CHECKSUMS.txt will be written
 * @param {string} packageFileName - Filename to write in checksums file (default: 'continue-2.0.0.tgz')
 * @returns {Promise<string>} The path to the written file
 * @throws {ChecksumGenerationError} If write operation fails
 *
 * @example
 * await writeChecksumsFile(
 *   { sha256: 'abc...', sha512: 'def...' },
 *   '/cache/CHECKSUMS.txt'
 * );
 */
export async function writeChecksumsFile(
  checksums,
  outputPath,
  packageFileName = 'continue-2.0.0.tgz'
) {
  try {
    // Validate checksums structure
    if (!checksums.sha256 || !checksums.sha512) {
      throw new Error('Checksums must contain both sha256 and sha512 fields');
    }

    // Validate hex format (64 chars for SHA256, 128 for SHA512)
    if (!/^[a-f0-9]{64}$/.test(checksums.sha256)) {
      throw new Error(
        `Invalid SHA256 format: must be 64 lowercase hex characters, got "${checksums.sha256}"`
      );
    }

    if (!/^[a-f0-9]{128}$/.test(checksums.sha512)) {
      throw new Error(
        `Invalid SHA512 format: must be 128 lowercase hex characters, got "${checksums.sha512}"`
      );
    }

    // Build file content in sha256sum/sha512sum format
    const content = `${checksums.sha256}  ${packageFileName}\n${checksums.sha512}  ${packageFileName}\n`;

    // Ensure output directory exists
    const outputDir = path.dirname(outputPath);
    await fs.mkdir(outputDir, { recursive: true });

    // Write file
    await fs.writeFile(outputPath, content, 'utf8');

    return outputPath;
  } catch (error) {
    throw new ChecksumGenerationError(
      `Failed to write checksums file ${outputPath}: ${error.message}`,
      'write',
      { outputPath, packageFileName, originalError: error.message }
    );
  }
}

/**
 * Update manifest.json with computed checksums.
 *
 * Reads the manifest, updates the checksums object with sha256 and sha512 values,
 * and writes the updated manifest back to disk.
 *
 * @async
 * @param {string} manifestPath - Absolute path to manifest.json
 * @param {Object} checksums - Checksums object with sha256 and sha512 fields
 * @returns {Promise<Object>} The updated manifest object
 * @throws {ChecksumGenerationError} If read/parse/write operations fail
 *
 * @example
 * const updated = await updateManifestChecksums(
 *   '/path/to/manifest.json',
 *   { sha256: 'abc...', sha512: 'def...' }
 * );
 * console.log(updated.checksums.sha256); // 'abc...'
 */
export async function updateManifestChecksums(manifestPath, checksums) {
  try {
    // Read current manifest
    const manifestContent = await fs.readFile(manifestPath, 'utf8');
    const manifest = JSON.parse(manifestContent);

    // Ensure checksums object exists
    if (!manifest.checksums) {
      manifest.checksums = {};
    }

    // Update checksums
    manifest.checksums.sha256 = checksums.sha256;
    manifest.checksums.sha512 = checksums.sha512;

    // Write updated manifest (with 2-space indentation to match existing format)
    await fs.writeFile(manifestPath, JSON.stringify(manifest, null, 2) + '\n', 'utf8');

    return manifest;
  } catch (error) {
    throw new ChecksumGenerationError(
      `Failed to update manifest ${manifestPath}: ${error.message}`,
      'update-manifest',
      { manifestPath, originalError: error.message }
    );
  }
}

/**
 * Verify computed checksums match expected values.
 *
 * @param {Object} computed - Computed checksums { sha256, sha512 }
 * @param {Object} expected - Expected checksums { sha256, sha512 }
 * @returns {boolean} true if checksums match, false otherwise
 * @throws {ChecksumGenerationError} If comparison fails
 *
 * @example
 * const match = verifyChecksumsMatch(
 *   { sha256: 'abc...', sha512: 'def...' },
 *   { sha256: 'abc...', sha512: 'def...' }
 * );
 * console.log(match); // true
 */
export function verifyChecksumsMatch(computed, expected) {
  try {
    if (!computed || !expected) {
      throw new Error('Both computed and expected checksums must be provided');
    }

    const sha256Match = computed.sha256 === expected.sha256;
    const sha512Match = computed.sha512 === expected.sha512;

    return sha256Match && sha512Match;
  } catch (error) {
    throw new ChecksumGenerationError(
      `Failed to verify checksums: ${error.message}`,
      'verify',
      { originalError: error.message }
    );
  }
}

/**
 * Validate checksum file format (CHECKSUMS.txt).
 *
 * Verifies the file contains properly formatted SHA256 and SHA512 hashes
 * in the standard format: "<hash>  <filename>"
 *
 * @async
 * @param {string} checksumsFilePath - Path to CHECKSUMS.txt
 * @returns {Promise<{valid: boolean, hashes: {sha256: string, sha512: string}, message: string}>}
 *
 * @example
 * const result = await validateChecksumsFile('/cache/CHECKSUMS.txt');
 * if (result.valid) {
 *   console.log(result.hashes.sha256);
 * }
 */
export async function validateChecksumsFile(checksumsFilePath) {
  try {
    const content = await fs.readFile(checksumsFilePath, 'utf8');
    const lines = content
      .trim()
      .split('\n')
      .filter((line) => line.length > 0);

    if (lines.length < 2) {
      return {
        valid: false,
        hashes: null,
        message: 'CHECKSUMS.txt must contain at least 2 lines (SHA256 and SHA512)',
      };
    }

    const hashes = {};
    for (const line of lines) {
      const parts = line.split(/\s{2,}/);
      if (parts.length !== 2) {
        return {
          valid: false,
          hashes: null,
          message: `Invalid line format in CHECKSUMS.txt: "${line}"`,
        };
      }

      const [hash, filename] = parts;
      if (/^[a-f0-9]{64}$/.test(hash)) {
        hashes.sha256 = hash;
      } else if (/^[a-f0-9]{128}$/.test(hash)) {
        hashes.sha512 = hash;
      }
    }

    if (!hashes.sha256 || !hashes.sha512) {
      return {
        valid: false,
        hashes: null,
        message: 'CHECKSUMS.txt must contain both SHA256 (64 chars) and SHA512 (128 chars) hashes',
      };
    }

    return {
      valid: true,
      hashes,
      message: 'CHECKSUMS.txt format is valid',
    };
  } catch (error) {
    return {
      valid: false,
      hashes: null,
      message: `Failed to validate CHECKSUMS.txt: ${error.message}`,
    };
  }
}

// -------------------------------------------------------
// Orchestrator Function
// -------------------------------------------------------

/**
 * Complete orchestration: compute hashes, write files, and update manifest.
 *
 * @async
 * @param {Object} config - Configuration object
 * @param {string} config.packagePath - Path to the .tgz file
 * @param {string} config.checksumsOutputPath - Path where CHECKSUMS.txt will be written
 * @param {string} config.manifestPath - Path to manifest.json
 * @param {boolean} config.updateManifest - Whether to update manifest.json (default: true)
 * @param {boolean} config.validate - Whether to validate after generation (default: true)
 * @returns {Promise<{success: boolean, checksums: Object, checksumsFile: string, manifest: Object, message: string}>}
 * @throws {ChecksumGenerationError} On any step failure
 *
 * @example
 * const result = await orchestrateChecksumGeneration({
 *   packagePath: '/cache/continue-2.0.0.tgz',
 *   checksumsOutputPath: '/cache/CHECKSUMS.txt',
 *   manifestPath: '/src/versions/v2.0.0/manifest.json'
 * });
 */
export async function orchestrateChecksumGeneration(config) {
  const {
    packagePath,
    checksumsOutputPath,
    manifestPath,
    updateManifest = true,
    validate = true,
  } = config;

  try {
    // Step 1: Compute hashes
    const checksums = await generateChecksums(packagePath);

    // Step 2: Write CHECKSUMS.txt
    await writeChecksumsFile(checksums, checksumsOutputPath);

    // Step 3: Update manifest (if requested)
    let manifest = null;
    if (updateManifest) {
      manifest = await updateManifestChecksums(manifestPath, checksums);
    }

    // Step 4: Validate (if requested)
    if (validate) {
      const validation = await validateChecksumsFile(checksumsOutputPath);
      if (!validation.valid) {
        throw new ChecksumGenerationError(validation.message, 'validate');
      }
    }

    return {
      success: true,
      checksums,
      checksumsFile: checksumsOutputPath,
      manifest,
      message: 'Checksum generation completed successfully',
    };
  } catch (error) {
    if (error instanceof ChecksumGenerationError) {
      throw error;
    }
    throw new ChecksumGenerationError(
      `Orchestration failed: ${error.message}`,
      'orchestration',
      { config, originalError: error.message }
    );
  }
}

// -------------------------------------------------------
// Exports
// -------------------------------------------------------

export { ChecksumGenerationError };
