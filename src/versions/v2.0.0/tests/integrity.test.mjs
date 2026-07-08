/**
 * Unit Tests for npm Package Integrity Validation Utility (Step 8)
 *
 * Tests the integrity.js module with real file fixtures and crypto operations.
 * Covers SHA256 computation, manifest validation, checksum parsing, and error handling.
 *
 * This is NOT an integration test; it provides low-level unit coverage of the
 * integrity utility itself (separate from npm-validate.test.mjs which tests
 * npm startup validation).
 *
 * @module src/versions/v2.0.0/tests/integrity.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 8 (integrity utility), 12 (npm validation), 31 (npm package tests)
 */

import { strict as assert } from 'assert';
import { describe, it, beforeEach, afterEach } from 'mocha';
import * as fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';
import crypto from 'crypto';

// Import the integrity module under test
import * as integrity from '../lib/integrity.js';

// -------------------------------------------------------
// Test Configuration & Helpers
// -------------------------------------------------------

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const TEMP_TEST_DIR = path.join(__dirname, '.temp-integrity-tests');

/**
 * Helper to compute SHA256 of a buffer
 */
function computeHash(buffer) {
  return crypto.createHash('sha256').update(buffer).digest('hex');
}

/**
 * Helper to create a fake package file with known content
 */
async function createFakePackage(dir, filename, content = 'fake-continue-package') {
  const filePath = path.join(dir, filename);
  await fs.writeFile(filePath, content);
  return filePath;
}

/**
 * Helper to create a checksum file
 */
async function createChecksumFile(dir, filename, hash) {
  const checksumPath = path.join(dir, `${filename}.sha256`);
  await fs.writeFile(checksumPath, `${hash}  ${filename}`);
  return checksumPath;
}

/**
 * Helper to create a manifest.json file with versioned naming
 * Follows the pattern expected by validatePackageIntegrity: manifest-v{version}.json
 */
async function createManifest(dir, version, manifestObj) {
  const versionTag = version.toString().startsWith('v') ? version : `v${version}`;
  const manifestPath = path.join(dir, `manifest-${versionTag}.json`);
  await fs.writeFile(manifestPath, JSON.stringify(manifestObj, null, 2));
  return manifestPath;
}

// -------------------------------------------------------
// Global Test Setup & Teardown
// -------------------------------------------------------

describe('integrity.js - npm Package Integrity Validation Utility', function () {
  this.timeout(10000); // 10 second timeout for all tests (crypto operations can be slow)

  beforeEach(async function () {
    // Create temporary test directory
    try {
      await fs.mkdir(TEMP_TEST_DIR, { recursive: true });
    } catch (err) {
      // Directory may already exist
    }
  });

  afterEach(async function () {
    // Clean up temporary test directory
    try {
      await fs.rm(TEMP_TEST_DIR, { recursive: true, force: true });
    } catch (err) {
      // Ignore cleanup errors
    }
  });

  // -------------------------------------------------------
  // Test Suite 1: computeSHA256() - Hash Computation
  // -------------------------------------------------------

  describe('computeSHA256() - Hash Computation', function () {
    it('should compute correct SHA256 hash of a file', async function () {
      const content = 'test-package-content';
      const filePath = await createFakePackage(TEMP_TEST_DIR, 'test.tgz', content);

      const hash = await integrity.computeSHA256(filePath);

      const expectedHash = computeHash(Buffer.from(content));
      assert.strictEqual(hash, expectedHash, 'Hash should match expected value');
      assert.strictEqual(hash.length, 64, 'SHA256 hash should be 64 hex characters');
    });

    it('should return lowercase hash', async function () {
      const filePath = await createFakePackage(TEMP_TEST_DIR, 'test.tgz', 'content');
      const hash = await integrity.computeSHA256(filePath);

      assert.strictEqual(hash, hash.toLowerCase(), 'Hash should be lowercase');
    });

    it('should throw IntegrityError for missing file', async function () {
      const filePath = path.join(TEMP_TEST_DIR, 'nonexistent.tgz');

      try {
        await integrity.computeSHA256(filePath);
        assert.fail('Should have thrown IntegrityError');
      } catch (err) {
        assert.strictEqual(err.name, 'IntegrityError', 'Should throw IntegrityError');
        assert(err.message.includes('Failed to compute SHA256'), 'Error message should be descriptive');
      }
    });

    it('should produce consistent hashes for same content', async function () {
      const filePath = await createFakePackage(TEMP_TEST_DIR, 'test.tgz', 'consistent-content');

      const hash1 = await integrity.computeSHA256(filePath);
      const hash2 = await integrity.computeSHA256(filePath);

      assert.strictEqual(hash1, hash2, 'Same file should produce same hash');
    });
  });

  // -------------------------------------------------------
  // Test Suite 2: validateChecksumFormat() - Format Validation
  // -------------------------------------------------------

  describe('validateChecksumFormat() - Checksum Format Validation', function () {
    it('should accept valid 64-character hex checksum', async function () {
      const validHash = 'a'.repeat(64); // 64 lowercase hex chars
      const result = integrity.validateChecksumFormat(validHash);
      assert.strictEqual(result, true, 'Valid hash should be accepted');
    });

    it('should accept mixed case hex (normalize to lowercase)', async function () {
      const mixedCaseHash = 'AbCdEf' + 'a'.repeat(58);
      const result = integrity.validateChecksumFormat(mixedCaseHash);
      assert.strictEqual(result, true, 'Mixed case hash should be accepted');
    });

    it('should reject non-string input', async function () {
      const result = integrity.validateChecksumFormat(12345);
      assert.strictEqual(result, false, 'Non-string should be rejected');
    });

    it('should reject null/undefined', async function () {
      assert.strictEqual(integrity.validateChecksumFormat(null), false);
      assert.strictEqual(integrity.validateChecksumFormat(undefined), false);
    });

    it('should reject hash that is too short', async function () {
      const tooShortHash = 'a'.repeat(63);
      const result = integrity.validateChecksumFormat(tooShortHash);
      assert.strictEqual(result, false, 'Hash shorter than 64 chars should be rejected');
    });

    it('should reject hash that is too long', async function () {
      const tooLongHash = 'a'.repeat(65);
      const result = integrity.validateChecksumFormat(tooLongHash);
      assert.strictEqual(result, false, 'Hash longer than 64 chars should be rejected');
    });

    it('should reject hash with invalid hex characters', async function () {
      const invalidHexHash = 'g'.repeat(64); // 'g' is not a valid hex character
      const result = integrity.validateChecksumFormat(invalidHexHash);
      assert.strictEqual(result, false, 'Invalid hex characters should be rejected');
    });
  });

  // -------------------------------------------------------
  // Test Suite 3: parseChecksumFile() - Checksum Parsing
  // -------------------------------------------------------

  describe('parseChecksumFile() - Checksum File Parsing', function () {
    it('should parse valid checksum file', async function () {
      const hash = 'a'.repeat(64);
      const filename = 'continue-v2.0.0.tgz';
      await createChecksumFile(TEMP_TEST_DIR, filename, hash);
      const checksumPath = path.join(TEMP_TEST_DIR, `${filename}.sha256`);

      const result = await integrity.parseChecksumFile(checksumPath);

      assert.strictEqual(result.hash, hash, 'Hash should match');
      assert.strictEqual(result.filename, filename, 'Filename should match');
    });

    it('should handle multiple spaces between hash and filename', async function () {
      const hash = 'b'.repeat(64);
      const filename = 'continue-v2.0.0.tgz';
      const checksumPath = path.join(TEMP_TEST_DIR, `${filename}.sha256`);
      await fs.writeFile(checksumPath, `${hash}    ${filename}`); // Multiple spaces

      const result = await integrity.parseChecksumFile(checksumPath);

      assert.strictEqual(result.hash, hash);
      assert.strictEqual(result.filename, filename);
    });

    it('should normalize hash to lowercase', async function () {
      const hash = 'ABCDEF' + 'a'.repeat(58);
      const filename = 'test.tgz';
      const checksumPath = path.join(TEMP_TEST_DIR, `${filename}.sha256`);
      await fs.writeFile(checksumPath, `${hash}  ${filename}`);

      const result = await integrity.parseChecksumFile(checksumPath);

      assert.strictEqual(result.hash, hash.toLowerCase(), 'Hash should be normalized to lowercase');
    });

    it('should throw IntegrityError for missing file', async function () {
      const checksumPath = path.join(TEMP_TEST_DIR, 'nonexistent.sha256');

      try {
        await integrity.parseChecksumFile(checksumPath);
        assert.fail('Should have thrown IntegrityError');
      } catch (err) {
        assert.strictEqual(err.name, 'IntegrityError', 'Should throw IntegrityError');
      }
    });

    it('should throw IntegrityError for malformed checksum file', async function () {
      const checksumPath = path.join(TEMP_TEST_DIR, 'malformed.sha256');
      await fs.writeFile(checksumPath, 'not-a-valid-checksum-format');

      try {
        await integrity.parseChecksumFile(checksumPath);
        assert.fail('Should have thrown IntegrityError');
      } catch (err) {
        assert.strictEqual(err.name, 'IntegrityError');
      }
    });

    it('should throw IntegrityError for invalid hash format', async function () {
      const checksumPath = path.join(TEMP_TEST_DIR, 'invalid.sha256');
      const invalidHash = 'invalid_hash'; // Too short, invalid chars
      await fs.writeFile(checksumPath, `${invalidHash}  test.tgz`);

      try {
        await integrity.parseChecksumFile(checksumPath);
        assert.fail('Should have thrown IntegrityError');
      } catch (err) {
        assert.strictEqual(err.name, 'IntegrityError');
      }
    });
  });

  // -------------------------------------------------------
  // Test Suite 4: validateManifestStructure() - Manifest Validation
  // -------------------------------------------------------

  describe('validateManifestStructure() - Manifest Validation', function () {
    const validManifest = {
      version: '2.0.0',
      continueVersion: '0.4.x',
      releaseDate: '2024-01-15',
      status: 'stable',
      checksums: {
        sha256: 'a'.repeat(64)
      }
    };

    it('should validate manifest with all required fields', async function () {
      // Should not throw
      assert.doesNotThrow(() => {
        integrity.validateManifestStructure(validManifest, '2.0.0');
      }, 'Valid manifest should not throw');
    });

    it('should handle version with leading "v"', async function () {
      const manifestWithV = { ...validManifest, version: 'v2.0.0' };

      assert.doesNotThrow(() => {
        integrity.validateManifestStructure(manifestWithV, '2.0.0');
      }, 'Should accept "v" prefix and normalize');
    });

    it('should throw ManifestError for missing required field', async function () {
      const invalidManifest = { ...validManifest };
      delete invalidManifest.continueVersion;

      try {
        integrity.validateManifestStructure(invalidManifest, '2.0.0');
        assert.fail('Should have thrown ManifestError');
      } catch (err) {
        assert.strictEqual(err.name, 'ManifestError');
        assert(err.message.includes('continueVersion'), 'Should mention missing field');
      }
    });

    it('should throw ManifestError for version mismatch', async function () {
      try {
        integrity.validateManifestStructure(validManifest, '1.0.0');
        assert.fail('Should have thrown ManifestError');
      } catch (err) {
        assert.strictEqual(err.name, 'ManifestError');
        assert(err.message.includes('Version mismatch'), 'Should mention version mismatch');
      }
    });

    it('should throw ManifestError if checksums is missing', async function () {
      const invalidManifest = { ...validManifest };
      delete invalidManifest.checksums;

      try {
        integrity.validateManifestStructure(invalidManifest, '2.0.0');
        assert.fail('Should have thrown ManifestError');
      } catch (err) {
        assert.strictEqual(err.name, 'ManifestError');
      }
    });

    it('should throw ManifestError if sha256 is missing from checksums', async function () {
      const invalidManifest = {
        ...validManifest,
        checksums: { md5: 'abc123' } // Missing sha256
      };

      try {
        integrity.validateManifestStructure(invalidManifest, '2.0.0');
        assert.fail('Should have thrown ManifestError');
      } catch (err) {
        assert.strictEqual(err.name, 'ManifestError');
        assert(err.message.includes('sha256'), 'Should mention sha256 requirement');
      }
    });

    it('should throw ManifestError for null required field', async function () {
      const invalidManifest = { ...validManifest, releaseDate: null };

      try {
        integrity.validateManifestStructure(invalidManifest, '2.0.0');
        assert.fail('Should have thrown ManifestError');
      } catch (err) {
        assert.strictEqual(err.name, 'ManifestError');
      }
    });
  });

  // -------------------------------------------------------
  // Test Suite 5: validatePackageChecksum() - Checksum Verification
  // -------------------------------------------------------

  describe('validatePackageChecksum() - Checksum Verification', function () {
    it('should verify matching checksum', async function () {
      const content = 'test-package-v2.0.0';
      const packagePath = await createFakePackage(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', content);
      const hash = computeHash(Buffer.from(content));
      const checksumPath = await createChecksumFile(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', hash);

      const result = await integrity.validatePackageChecksum(packagePath, checksumPath);

      assert.strictEqual(result.valid, true, 'Checksum should be valid');
      assert.strictEqual(result.error, null, 'No error for matching checksum');
    });

    it('should return error for mismatched checksum', async function () {
      const content = 'test-package';
      const packagePath = await createFakePackage(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', content);
      const wrongHash = 'b'.repeat(64); // Deliberately wrong hash
      const checksumPath = await createChecksumFile(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', wrongHash);

      const result = await integrity.validatePackageChecksum(packagePath, checksumPath);

      assert.strictEqual(result.valid, false, 'Checksum should be invalid');
      assert(result.error, 'Should have error message');
      assert(result.error.includes('Checksum mismatch'), 'Error should mention mismatch');
      assert(result.expectedHash, 'Should have expected hash');
      assert(result.computedHash, 'Should have computed hash');
    });

    it('should return error if .sha256 file is missing', async function () {
      const packagePath = await createFakePackage(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', 'content');
      const checksumPath = path.join(TEMP_TEST_DIR, 'continue-v2.0.0.tgz.sha256');
      // Do NOT create the .sha256 file

      const result = await integrity.validatePackageChecksum(packagePath, checksumPath);

      assert.strictEqual(result.valid, false, 'Should be invalid');
      assert(result.error, 'Should have error message');
      assert(result.error.includes('sha256'), 'Should mention missing checksum file');
    });
  });

  // -------------------------------------------------------
  // Test Suite 6: validatePackageIntegrity() - Full Integration
  // -------------------------------------------------------

  describe('validatePackageIntegrity() - Full Integration', function () {
    it('should validate package with all checks passing', async function () {
      const content = 'continue-v2.0.0-package';
      const packagePath = await createFakePackage(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', content);
      const hash = computeHash(Buffer.from(content));
      await createChecksumFile(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', hash);

      const manifest = {
        version: '2.0.0',
        continueVersion: '0.4.x',
        releaseDate: '2024-01-15',
        status: 'stable',
        checksums: { sha256: hash }
      };
      await createManifest(TEMP_TEST_DIR, '2.0.0', manifest);

      const result = await integrity.validatePackageIntegrity(TEMP_TEST_DIR, '2.0.0');

      assert.strictEqual(result.valid, true, 'Package should be valid');
      assert.strictEqual(result.version, '2.0.0', 'Version should match');
      assert.strictEqual(result.checksumValid, true, 'Checksum should be valid');
      assert.strictEqual(result.manifestValid, true, 'Manifest should be valid');
      assert(result.metadata, 'Metadata should be populated');
      assert.strictEqual(result.errors.length, 0, 'No errors for valid package');
    });

    it('should handle invalid checksum in full validation', async function () {
      const content = 'continue-v2.0.0-package';
      const packagePath = await createFakePackage(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', content);
      const wrongHash = 'b'.repeat(64);
      await createChecksumFile(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', wrongHash);

      const manifest = {
        version: '2.0.0',
        continueVersion: '0.4.x',
        releaseDate: '2024-01-15',
        status: 'stable',
        checksums: { sha256: wrongHash }
      };
      await createManifest(TEMP_TEST_DIR, '2.0.0', manifest);

      const result = await integrity.validatePackageIntegrity(TEMP_TEST_DIR, '2.0.0');

      assert.strictEqual(result.valid, false, 'Package should be invalid');
      assert.strictEqual(result.checksumValid, false, 'Checksum should be invalid');
      assert(result.errors.length > 0, 'Should have error messages');
    });

    it('should return versionDir in result', async function () {
      const content = 'continue-v2.0.0';
      await createFakePackage(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', content);
      const hash = computeHash(Buffer.from(content));
      await createChecksumFile(TEMP_TEST_DIR, 'continue-v2.0.0.tgz', hash);

      const manifest = {
        version: '2.0.0',
        continueVersion: '0.4.x',
        releaseDate: '2024-01-15',
        status: 'stable',
        checksums: { sha256: hash }
      };
      await createManifest(TEMP_TEST_DIR, '2.0.0', manifest);

      const result = await integrity.validatePackageIntegrity(TEMP_TEST_DIR, '2.0.0');

      assert.strictEqual(result.versionDir, TEMP_TEST_DIR, 'Should return versionDir');
    });
  });

  // -------------------------------------------------------
  // Test Suite 7: Error Classes
  // -------------------------------------------------------

  describe('Error Classes', function () {
    it('should create IntegrityError with proper name and message', async function () {
      const err = new integrity.IntegrityError('Test error');
      assert.strictEqual(err.name, 'IntegrityError');
      assert.strictEqual(err.message, 'Test error');
      assert(err instanceof Error, 'Should extend Error');
    });

    it('should create ChecksumError with expected and computed hashes', async function () {
      const expectedHash = 'a'.repeat(64);
      const computedHash = 'b'.repeat(64);
      const err = new integrity.ChecksumError('Checksum mismatch', expectedHash, computedHash);

      assert.strictEqual(err.name, 'ChecksumError');
      assert.strictEqual(err.expected, expectedHash);
      assert.strictEqual(err.computed, computedHash);
      assert(err instanceof integrity.IntegrityError, 'Should extend IntegrityError');
    });

    it('should create ManifestError with proper inheritance', async function () {
      const err = new integrity.ManifestError('Invalid manifest');
      assert.strictEqual(err.name, 'ManifestError');
      assert(err instanceof integrity.IntegrityError, 'Should extend IntegrityError');
      assert(err instanceof Error, 'Should extend Error');
    });
  });

  // -------------------------------------------------------
  // Test Suite 8: Edge Cases
  // -------------------------------------------------------

  describe('Edge Cases', function () {
    it('should handle empty file', async function () {
      const filePath = await createFakePackage(TEMP_TEST_DIR, 'empty.tgz', '');
      const hash = await integrity.computeSHA256(filePath);

      // Empty string should have a valid SHA256
      assert.strictEqual(hash.length, 64, 'Hash should be 64 chars even for empty file');
      assert.strictEqual(integrity.validateChecksumFormat(hash), true, 'Hash should be valid');
    });

    it('should handle large file content', async function () {
      const largeContent = 'x'.repeat(1024 * 1024); // 1MB
      const filePath = await createFakePackage(TEMP_TEST_DIR, 'large.tgz', largeContent);

      const hash = await integrity.computeSHA256(filePath);

      assert.strictEqual(hash.length, 64, 'Hash should be 64 chars');
      assert.strictEqual(integrity.validateChecksumFormat(hash), true, 'Hash should be valid');
    });

    it('should handle special characters in filenames', async function () {
      const filename = 'continue-v2.0.0-alpha+build.123.tgz';
      const filePath = await createFakePackage(TEMP_TEST_DIR, filename, 'content');

      const hash = await integrity.computeSHA256(filePath);
      assert.strictEqual(hash.length, 64, 'Should compute hash for filename with special chars');
    });

    it('should handle manifest with extra fields', async function () {
      const manifest = {
        version: '2.0.0',
        continueVersion: '0.4.x',
        releaseDate: '2024-01-15',
        status: 'stable',
        checksums: { sha256: 'a'.repeat(64) },
        extraField: 'should-be-ignored',
        metadata: { custom: 'data' }
      };

      assert.doesNotThrow(() => {
        integrity.validateManifestStructure(manifest, '2.0.0');
      }, 'Should allow extra fields in manifest');
    });
  });
});
