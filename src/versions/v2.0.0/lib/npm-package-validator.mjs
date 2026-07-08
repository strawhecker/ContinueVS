/**
 * npm Package Content Validator
 *
 * Validates the internal structure of the Continue npm package (.tgz archive).
 * Ensures all required files and modules are present and properly structured.
 *
 * Validates:
 * - Archive integrity (valid tar format)
 * - Package metadata (package.json structure)
 * - Entry point existence (lib/core-server.js)
 * - Feature implementations (matching manifest declarations)
 * - Manifest consistency (features vs. actual files)
 *
 * This module is ESM (ES Modules) and uses only Node.js built-ins.
 * No external npm dependencies.
 *
 * @module src/versions/v2.0.0/lib/npm-package-validator.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 35 (package download), 37 (checksum generation), 12 (startup validation)
 */

import fs from 'fs/promises';
import { createReadStream } from 'fs';
import path from 'path';
import { Readable } from 'stream';

// -------------------------------------------------------
// Custom Error Types
// -------------------------------------------------------

/**
 * Base error class for package validation failures.
 * @class PackageValidationError
 * @extends Error
 */
class PackageValidationError extends Error {
  constructor(message, details = {}) {
    super(message);
    this.name = 'PackageValidationError';
    this.details = details;
  }
}

/**
 * Error class for archive-related failures.
 * @class ArchiveError
 * @extends PackageValidationError
 */
class ArchiveError extends PackageValidationError {
  constructor(message, details = {}) {
    super(message, details);
    this.name = 'ArchiveError';
  }
}

/**
 * Error class for metadata validation failures.
 * @class MetadataError
 * @extends PackageValidationError
 */
class MetadataError extends PackageValidationError {
  constructor(message, details = {}) {
    super(message, details);
    this.name = 'MetadataError';
  }
}

// -------------------------------------------------------
// Feature-to-File Mappings
// -------------------------------------------------------

const FEATURE_FILE_MAPPINGS = {
  coreEditorIntegration: ['lib/core-server.js', 'lib/handler-dispatcher.js'],
  diagnosticsCollection: ['lib/handlers/diagnostics-handler.js'],
  goToDefinition: ['lib/handlers/goto-definition-handler.js'],
  findReferences: ['lib/handlers/find-references-handler.js'],
  codeCompletion: ['lib/handlers/completion-handler.js'],
  search: ['lib/handlers/search-handler.js'],
  advancedSymbolSearch: ['lib/handlers/symbol-search-handler.js'],
  webviewMessaging: ['lib/handlers/webview-handler.js']
};

const REQUIRED_FILES = [
  'package.json',
  'lib/core-server.js',
  'lib/handler-dispatcher.js'
];

// -------------------------------------------------------
// Helper Functions
// -------------------------------------------------------

/**
 * Parses a simple tar header and extracts filename, size, and type.
 * Reads exactly 512 bytes of a tar record.
 * @param {Buffer} block - 512-byte tar header block
 * @returns {Object|null} {name, size, type, isFile, isDir} or null if not a valid header
 */
function parseTarHeader(block) {
  if (block.length < 512) return null;

  // Tar filename is at offset 0-99
  const name = block.toString('utf8', 0, 100).trim();
  if (!name) return null;

  // Tar file type flag at offset 156
  const typeFlag = String.fromCharCode(block[156]);

  // Size in octal at offset 124-135
  const sizeStr = block.toString('utf8', 124, 135).trim();
  let size = 0;
  try {
    size = parseInt(sizeStr, 8);
  } catch {
    return null;
  }

  return {
    name: name.replace(/^\.\//, ''), // Normalize ./prefix
    size,
    type: typeFlag,
    isFile: typeFlag === '0' || typeFlag === '',
    isDir: typeFlag === '5'
  };
}

/**
 * Reads entire tar archive and extracts file list without permanent extraction.
 * Returns a map of {filename -> {size, isFile, isDir}}.
 * @param {string} tgzPath - Path to .tgz file
 * @returns {Promise<Map>} File entries from tar archive
 * @throws {ArchiveError} If tar format is invalid or file cannot be read
 */
async function readTarEntries(tgzPath) {
  const zlib = await import('zlib');
  const entries = new Map();

  return new Promise((resolve, reject) => {
    const stream = createReadStream(tgzPath);
    const gunzip = zlib.createGunzip();
    let buffer = Buffer.alloc(0);

    stream.on('error', (err) => {
      reject(new ArchiveError(`Failed to read archive: ${err.message}`, { originalError: err.message }));
    });

    gunzip.on('error', (err) => {
      reject(new ArchiveError(`Failed to decompress archive: ${err.message}`, { originalError: err.message }));
    });

    gunzip.on('data', (chunk) => {
      buffer = Buffer.concat([buffer, chunk]);

      // Process complete 512-byte blocks
      while (buffer.length >= 512) {
        const block = buffer.subarray(0, 512);
        buffer = buffer.subarray(512);

        const header = parseTarHeader(block);
        if (!header) continue; // End of archive or padding

        if (header.name && (header.isFile || header.isDir)) {
          entries.set(header.name, {
            size: header.size,
            isFile: header.isFile,
            isDir: header.isDir
          });
        }

        // Skip data blocks (align to 512-byte boundary)
        const dataBlocks = Math.ceil(header.size / 512);
        const skipBytes = dataBlocks * 512;
        while (buffer.length < skipBytes && stream.readable) {
          // Wait for more data
          break;
        }
        if (buffer.length >= skipBytes) {
          buffer = buffer.subarray(skipBytes);
        }
      }
    });

    gunzip.on('end', () => {
      resolve(entries);
    });

    stream.pipe(gunzip);
  });
}

/**
 * Validates package.json structure and required fields.
 * @param {Map} tarEntries - Tar file entries
 * @returns {Promise<Object>} Parsed package.json metadata
 * @throws {MetadataError} If package.json is missing or invalid
 */
async function validatePackageJson(tarEntries) {
  if (!tarEntries.has('package/package.json')) {
    throw new MetadataError('package.json not found in archive', {
      expected: 'package/package.json',
      found: Array.from(tarEntries.keys()).filter(f => f.includes('package.json'))
    });
  }

  const entry = tarEntries.get('package/package.json');
  if (!entry.isFile) {
    throw new MetadataError('package.json is not a regular file', { entry });
  }

  // For validation without extraction, we check that it exists and is readable
  // Full JSON parsing would require extracting the file
  return {
    path: 'package/package.json',
    exists: true,
    isFile: true
  };
}

/**
 * Validates core entry point existence.
 * @param {Map} tarEntries - Tar file entries
 * @returns {Object} Entry point validation result
 * @throws {MetadataError} If core-server.js is missing
 */
function validateEntryPoint(tarEntries) {
  const expectedPath = 'package/lib/core-server.js';

  if (!tarEntries.has(expectedPath)) {
    const found = Array.from(tarEntries.keys()).filter(f => f.includes('core-server'));
    throw new MetadataError('lib/core-server.js not found in archive', {
      expected: expectedPath,
      found
    });
  }

  const entry = tarEntries.get(expectedPath);
  if (!entry.isFile) {
    throw new MetadataError('core-server.js is not a regular file', { entry });
  }

  return {
    path: expectedPath,
    exists: true,
    isFile: true,
    size: entry.size
  };
}

/**
 * Validates that required files are present.
 * @param {Map} tarEntries - Tar file entries
 * @returns {Object} Validation result with missing/present files
 * @throws {MetadataError} If required files are missing
 */
function validateRequiredFiles(tarEntries) {
  const missing = [];
  const present = [];

  for (const required of REQUIRED_FILES) {
    const tarPath = `package/${required}`;
    if (tarEntries.has(tarPath)) {
      present.push(required);
    } else {
      missing.push(required);
    }
  }

  if (missing.length > 0) {
    throw new MetadataError('Required files missing from archive', {
      missing,
      present
    });
  }

  return {
    required: REQUIRED_FILES,
    missing: [],
    present
  };
}

/**
 * Validates that feature implementations are present (for stable features).
 * Warnings only for experimental features.
 * @param {Map} tarEntries - Tar file entries
 * @param {Object} manifest - Manifest object with feature list
 * @returns {Object} Validation result with warnings and errors
 */
function validateFeatureImplementations(tarEntries, manifest) {
  const errors = [];
  const warnings = [];

  if (manifest.features) {
    // Check stable features (must exist)
    if (manifest.features.stable) {
      for (const feature of manifest.features.stable) {
        const files = FEATURE_FILE_MAPPINGS[feature] || [];
        const missing = [];

        for (const file of files) {
          const tarPath = `package/${file}`;
          if (!tarEntries.has(tarPath)) {
            missing.push(file);
          }
        }

        if (missing.length > 0) {
          errors.push(`Stable feature '${feature}' missing implementation: ${missing.join(', ')}`);
        }
      }
    }

    // Check experimental features (warning if missing, not error)
    if (manifest.features.experimental) {
      for (const feature of manifest.features.experimental) {
        const files = FEATURE_FILE_MAPPINGS[feature] || [];
        const missing = [];

        for (const file of files) {
          const tarPath = `package/${file}`;
          if (!tarEntries.has(tarPath)) {
            missing.push(file);
          }
        }

        if (missing.length > 0) {
          warnings.push(`Experimental feature '${feature}' not present: ${missing.join(', ')}`);
        }
      }
    }
  }

  return {
    errors,
    warnings,
    featuresMissing: errors.length > 0 ? true : false
  };
}

/**
 * Loads and parses manifest.json.
 * @param {string} manifestPath - Path to manifest.json
 * @returns {Promise<Object>} Parsed manifest
 * @throws {MetadataError} If manifest cannot be read or parsed
 */
async function loadManifest(manifestPath) {
  try {
    const content = await fs.readFile(manifestPath, 'utf8');
    return JSON.parse(content);
  } catch (err) {
    throw new MetadataError(`Failed to load manifest: ${err.message}`, {
      path: manifestPath,
      originalError: err.message
    });
  }
}

/**
 * Generates a structured validation report.
 * @param {Object} results - Validation results from all checks
 * @returns {Object} Comprehensive report
 */
function generateValidationReport(results) {
  const allErrors = [];
  const allWarnings = [];

  if (results.archiveError) {
    allErrors.push(results.archiveError);
  }
  if (results.packageJsonError) {
    allErrors.push(results.packageJsonError);
  }
  if (results.entryPointError) {
    allErrors.push(results.entryPointError);
  }
  if (results.requiredFilesError) {
    allErrors.push(results.requiredFilesError);
  }
  if (results.featureErrors && results.featureErrors.length > 0) {
    allErrors.push(...results.featureErrors);
  }
  if (results.featureWarnings && results.featureWarnings.length > 0) {
    allWarnings.push(...results.featureWarnings);
  }

  const valid = allErrors.length === 0;

  return {
    valid,
    packagePath: results.packagePath,
    manifestPath: results.manifestPath,
    archiveValid: !results.archiveError,
    metadataValid: !results.packageJsonError,
    entryPointValid: !results.entryPointError,
    requiredFilesValid: !results.requiredFilesError,
    featuresValid: !results.featureErrors || results.featureErrors.length === 0,
    fileCount: results.fileCount || 0,
    errors: allErrors,
    warnings: allWarnings,
    timestamp: new Date().toISOString(),
    summary: {
      requiredFiles: results.requiredFilesPresent || REQUIRED_FILES,
      entriesChecked: results.fileCount || 0,
      validationDuration: results.duration || 0
    }
  };
}

// -------------------------------------------------------
// Main Orchestrator
// -------------------------------------------------------

/**
 * Main validation orchestrator. Validates all aspects of the npm package.
 *
 * @param {string} packagePath - Absolute path to .tgz file
 * @param {string} manifestPath - Absolute path to manifest.json
 * @returns {Promise<Object>} Validation report
 *
 * @throws {PackageValidationError} If package cannot be validated
 *
 * @example
 * const result = await validatePackageContents(
 *   'E:\\cache\\npm-packages\\v2.0.0\\continue-v2.0.0.tgz',
 *   'E:\\src\\versions\\v2.0.0\\manifest.json'
 * );
 *
 * if (result.valid) {
 *   console.log(`✅ Package validated: ${result.fileCount} files`);
 * } else {
 *   result.errors.forEach(err => console.error(`❌ ${err}`));
 * }
 */
export async function validatePackageContents(packagePath, manifestPath) {
  const startTime = performance.now();
  const results = {
    packagePath,
    manifestPath
  };

  try {
    // Step 1: Verify package file exists
    try {
      await fs.stat(packagePath);
    } catch (err) {
      throw new ArchiveError(`Package file not found: ${packagePath}`, {
        path: packagePath,
        originalError: err.message
      });
    }

    // Step 2: Load manifest
    const manifest = await loadManifest(manifestPath);

    // Step 3: Read tar entries
    let tarEntries;
    try {
      tarEntries = await readTarEntries(packagePath);
    } catch (err) {
      if (err instanceof ArchiveError) {
        throw err;
      }
      throw new ArchiveError(`Failed to read tar entries: ${err.message}`, {
        originalError: err.message
      });
    }

    results.fileCount = tarEntries.size;

    // Step 4: Validate package.json
    try {
      const pkgJsonResult = await validatePackageJson(tarEntries);
      results.packageJsonResult = pkgJsonResult;
    } catch (err) {
      results.packageJsonError = err.message;
    }

    // Step 5: Validate entry point
    try {
      const entryPointResult = validateEntryPoint(tarEntries);
      results.entryPointResult = entryPointResult;
    } catch (err) {
      results.entryPointError = err.message;
    }

    // Step 6: Validate required files
    try {
      const requiredResult = validateRequiredFiles(tarEntries);
      results.requiredFilesResult = requiredResult;
      results.requiredFilesPresent = requiredResult.present;
    } catch (err) {
      results.requiredFilesError = err.message;
    }

    // Step 7: Validate feature implementations
    try {
      const featureResult = validateFeatureImplementations(tarEntries, manifest);
      results.featureErrors = featureResult.errors;
      results.featureWarnings = featureResult.warnings;
    } catch (err) {
      results.featureErrors = [err.message];
    }

    results.duration = Math.round(performance.now() - startTime);

    // Generate final report
    return generateValidationReport(results);
  } catch (err) {
    results.duration = Math.round(performance.now() - startTime);

    // If it's one of our custom errors, re-throw
    if (err instanceof PackageValidationError) {
      throw err;
    }

    // Otherwise, wrap it
    throw new PackageValidationError(`Validation failed: ${err.message}`, {
      originalError: err.message,
      stack: err.stack
    });
  }
}

/**
 * Quick validation check for use in startup sequence.
 * Returns boolean instead of throwing.
 *
 * @param {string} packagePath - Path to .tgz file
 * @param {string} manifestPath - Path to manifest.json
 * @returns {Promise<boolean>} True if valid, false otherwise
 */
export async function quickValidatePackage(packagePath, manifestPath) {
  try {
    const result = await validatePackageContents(packagePath, manifestPath);
    return result.valid;
  } catch {
    return false;
  }
}

// -------------------------------------------------------
// Exports
// -------------------------------------------------------

export {
  PackageValidationError,
  ArchiveError,
  MetadataError
};
