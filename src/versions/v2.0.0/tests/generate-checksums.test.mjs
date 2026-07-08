/**
 * Test Suite for Checksum Generation Module
 *
 * Tests all functions in generate-checksums.mjs including:
 * - SHA256/SHA512 computation
 * - CHECKSUMS.txt file format compliance
 * - Manifest.json updates
 * - Error handling and edge cases
 * - Idempotency verification
 *
 * @module src/versions/v2.0.0/tests/generate-checksums.test.mjs
 * @version 1.0.0
 * @requires mocha, node >=18.0.0
 */

import { strict as assert } from 'assert';
import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';
import {
  generateChecksums,
  writeChecksumsFile,
  updateManifestChecksums,
  verifyChecksumsMatch,
  validateChecksumsFile,
  orchestrateChecksumGeneration,
  ChecksumGenerationError,
} from '../lib/generate-checksums.mjs';

// -------------------------------------------------------
// Test Utilities
// -------------------------------------------------------

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const tempDir = path.join(__dirname, '..', '..', '..', '.test-temp');

/**
 * Create a temporary test file with specified content.
 */
async function createTempFile(name, content) {
  await fs.mkdir(tempDir, { recursive: true });
  const filePath = path.join(tempDir, name);
  if (typeof content === 'string') {
    await fs.writeFile(filePath, content, 'utf8');
  } else {
    await fs.writeFile(filePath, content);
  }
  return filePath;
}

/**
 * Create a temporary .tgz file for testing.
 */
async function createTempTgzFile(name = 'test-package.tgz') {
  const testContent = Buffer.from('This is a test tar.gz file content');
  return createTempFile(name, testContent);
}

/**
 * Clean up temporary test files.
 */
async function cleanupTemp() {
  try {
    await fs.rm(tempDir, { recursive: true, force: true });
  } catch (error) {
    // Ignore cleanup errors
  }
}

// -------------------------------------------------------
// Test Suites
// -------------------------------------------------------

describe('generateChecksums()', () => {
  afterEach(cleanupTemp);

  it('should compute SHA256 and SHA512 hashes for a .tgz file', async () => {
    const tgzPath = await createTempTgzFile('continue-2.0.0.tgz');
    const result = await generateChecksums(tgzPath);

    assert(result.sha256, 'Should return sha256 hash');
    assert(result.sha512, 'Should return sha512 hash');
    assert(/^[a-f0-9]{64}$/.test(result.sha256), 'SHA256 should be 64 lowercase hex chars');
    assert(/^[a-f0-9]{128}$/.test(result.sha512), 'SHA512 should be 128 lowercase hex chars');
    assert(result.packagePath === tgzPath, 'Should return original package path');
    assert(result.timestampUtc, 'Should include UTC timestamp');
    assert(result.fileSizeBytes > 0, 'Should include file size');
  });

  it('should produce consistent hashes (idempotent)', async () => {
    const tgzPath = await createTempTgzFile('continue-2.0.0.tgz');

    const result1 = await generateChecksums(tgzPath);
    const result2 = await generateChecksums(tgzPath);

    assert.equal(result1.sha256, result2.sha256, 'SHA256 should be identical on multiple runs');
    assert.equal(result1.sha512, result2.sha512, 'SHA512 should be identical on multiple runs');
  });

  it('should throw ChecksumGenerationError for missing file', async () => {
    const missingPath = path.join(tempDir, 'nonexistent.tgz');

    try {
      await generateChecksums(missingPath);
      assert.fail('Should have thrown ChecksumGenerationError');
    } catch (error) {
      assert(error instanceof ChecksumGenerationError, 'Should be ChecksumGenerationError');
      assert.equal(error.operation, 'compute', 'Operation should be "compute"');
    }
  });

  it('should throw ChecksumGenerationError if path is a directory', async () => {
    await fs.mkdir(path.join(tempDir, 'test-dir'), { recursive: true });
    const dirPath = path.join(tempDir, 'test-dir');

    try {
      await generateChecksums(dirPath);
      assert.fail('Should have thrown ChecksumGenerationError');
    } catch (error) {
      assert(error instanceof ChecksumGenerationError);
    }
  });

  it('should handle large file efficiently', async () => {
    // Create a 10MB test file
    const largeContent = Buffer.alloc(10 * 1024 * 1024, 'a');
    const tgzPath = await createTempFile('large-package.tgz', largeContent);

    const startTime = Date.now();
    const result = await generateChecksums(tgzPath);
    const elapsed = Date.now() - startTime;

    assert(result.sha256, 'Should compute hash for large file');
    assert(elapsed < 5000, `Should complete large file hash in < 5s (took ${elapsed}ms)`);
  });
});

describe('writeChecksumsFile()', () => {
  afterEach(cleanupTemp);

  it('should write CHECKSUMS.txt in sha256sum/sha512sum format', async () => {
    const checksumsPath = path.join(tempDir, 'CHECKSUMS.txt');
    const checksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    await writeChecksumsFile(checksums, checksumsPath);

    const content = await fs.readFile(checksumsPath, 'utf8');
    assert(content.includes(checksums.sha256), 'File should contain SHA256');
    assert(content.includes(checksums.sha512), 'File should contain SHA512');
    assert(content.includes('continue-2.0.0.tgz'), 'File should include default filename');
  });

  it('should use custom filename in CHECKSUMS.txt', async () => {
    const checksumsPath = path.join(tempDir, 'CHECKSUMS.txt');
    const checksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    await writeChecksumsFile(checksums, checksumsPath, 'custom-package.tgz');

    const content = await fs.readFile(checksumsPath, 'utf8');
    assert(content.includes('custom-package.tgz'), 'File should contain custom filename');
  });

  it('should reject invalid SHA256 format', async () => {
    const checksumsPath = path.join(tempDir, 'CHECKSUMS.txt');
    const badChecksums = {
      sha256: 'invalid_not_hex',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    try {
      await writeChecksumsFile(badChecksums, checksumsPath);
      assert.fail('Should reject invalid SHA256');
    } catch (error) {
      assert(error instanceof ChecksumGenerationError);
      assert(error.message.includes('SHA256'));
    }
  });

  it('should reject invalid SHA512 format', async () => {
    const checksumsPath = path.join(tempDir, 'CHECKSUMS.txt');
    const badChecksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512: 'invalid',
    };

    try {
      await writeChecksumsFile(badChecksums, checksumsPath);
      assert.fail('Should reject invalid SHA512');
    } catch (error) {
      assert(error instanceof ChecksumGenerationError);
      assert(error.message.includes('SHA512'));
    }
  });

  it('should create parent directories if they do not exist', async () => {
    const checksumsPath = path.join(tempDir, 'nested', 'deep', 'CHECKSUMS.txt');
    const checksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    await writeChecksumsFile(checksums, checksumsPath);

    const exists = await fs
      .stat(checksumsPath)
      .then(() => true)
      .catch(() => false);
    assert(exists, 'File should be created with parent directories');
  });
});

describe('updateManifestChecksums()', () => {
  afterEach(cleanupTemp);

  it('should update checksums in manifest.json', async () => {
    const manifest = {
      version: '2.0.0',
      checksums: {
        sha256: 'old_hash_old_hash_old_hash_old_hash_old_hash_old_hash_old_hash0',
        sha512: 'old_old_old_old_old_old_old_old_old_old_old_old_old_old_old_old',
      },
    };

    const manifestPath = await createTempFile('manifest.json', JSON.stringify(manifest, null, 2));
    const newChecksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    const updated = await updateManifestChecksums(manifestPath, newChecksums);

    assert.equal(updated.checksums.sha256, newChecksums.sha256, 'SHA256 should be updated');
    assert.equal(updated.checksums.sha512, newChecksums.sha512, 'SHA512 should be updated');

    // Verify written to disk
    const diskContent = await fs.readFile(manifestPath, 'utf8');
    const diskManifest = JSON.parse(diskContent);
    assert.equal(diskManifest.checksums.sha256, newChecksums.sha256);
  });

  it('should create checksums object if it does not exist', async () => {
    const manifest = { version: '2.0.0' };
    const manifestPath = await createTempFile('manifest.json', JSON.stringify(manifest, null, 2));
    const checksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    const updated = await updateManifestChecksums(manifestPath, checksums);

    assert(updated.checksums, 'Checksums object should be created');
    assert.equal(updated.checksums.sha256, checksums.sha256);
  });

  it('should throw ChecksumGenerationError for missing manifest', async () => {
    const missingPath = path.join(tempDir, 'nonexistent-manifest.json');
    const checksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    try {
      await updateManifestChecksums(missingPath, checksums);
      assert.fail('Should throw ChecksumGenerationError');
    } catch (error) {
      assert(error instanceof ChecksumGenerationError);
      assert.equal(error.operation, 'update-manifest');
    }
  });

  it('should preserve manifest formatting with 2-space indentation', async () => {
    const manifest = {
      version: '2.0.0',
      features: ['a', 'b'],
      checksums: { sha256: 'old', sha512: 'old' },
    };

    const manifestPath = await createTempFile('manifest.json', JSON.stringify(manifest, null, 2));
    const checksums = {
      sha256: '6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };

    await updateManifestChecksums(manifestPath, checksums);

    const diskContent = await fs.readFile(manifestPath, 'utf8');
    // Verify indentation is 2 spaces (check for 2-space indented property)
    assert(/\n  "/.test(diskContent), 'Should use 2-space indentation');
  });
});

describe('verifyChecksumsMatch()', () => {
  it('should return true when checksums match', () => {
    const checksums = {
      sha256: 'a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2',
      sha512:
        '1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f',
    };

    const match = verifyChecksumsMatch(checksums, checksums);
    assert.equal(match, true);
  });

  it('should return false when SHA256 does not match', () => {
    const computed = {
      sha256: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
      sha512:
        '0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602',
    };
    const expected = {
      sha256: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
      sha512:
        '1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f',
    };

    const match = verifyChecksumsMatch(computed, expected);
    assert.equal(match, false);
  });

  it('should throw ChecksumGenerationError if checksums are missing', () => {
    assert.throws(
      () => verifyChecksumsMatch(null, {}),
      ChecksumGenerationError,
      'Should throw for null computed'
    );

    assert.throws(
      () => verifyChecksumsMatch({}, null),
      ChecksumGenerationError,
      'Should throw for null expected'
    );
  });
});

describe('validateChecksumsFile()', () => {
  afterEach(cleanupTemp);

  it('should validate correctly formatted CHECKSUMS.txt', async () => {
    const content = `6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72  continue-2.0.0.tgz
0cbf4caef38047bba9a24e621a961484e5d2a92176a859e7eb27df343dd34eb98d538a6c5f4da1ce302ec250b821cc001e46cc97a704988297185a4df7e99602  continue-2.0.0.tgz
`;
    const filePath = await createTempFile('CHECKSUMS.txt', content);

    const result = await validateChecksumsFile(filePath);

    assert.equal(result.valid, true);
    assert(result.hashes.sha256);
    assert(result.hashes.sha512);
  });

  it('should reject CHECKSUMS.txt with missing hashes', async () => {
    const content = 'a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2  continue-2.0.0.tgz\n';
    const filePath = await createTempFile('CHECKSUMS.txt', content);

    const result = await validateChecksumsFile(filePath);

    assert.equal(result.valid, false);
    assert(result.message.includes('both') || result.message.includes('SHA256') || result.message.includes('SHA512'));
  });

  it('should handle missing CHECKSUMS.txt file gracefully', async () => {
    const missingPath = path.join(tempDir, 'nonexistent-CHECKSUMS.txt');

    const result = await validateChecksumsFile(missingPath);

    assert.equal(result.valid, false);
    assert(result.message.includes('Failed'));
  });
});

describe('orchestrateChecksumGeneration()', () => {
  afterEach(cleanupTemp);

  it('should complete full checksum generation and manifest update', async () => {
    const tgzPath = await createTempTgzFile('continue-2.0.0.tgz');
    const checksumsPath = path.join(tempDir, 'CHECKSUMS.txt');
    const manifest = {
      version: '2.0.0',
      checksums: { sha256: 'old', sha512: 'old' },
    };
    const manifestPath = await createTempFile('manifest.json', JSON.stringify(manifest, null, 2));

    const result = await orchestrateChecksumGeneration({
      packagePath: tgzPath,
      checksumsOutputPath: checksumsPath,
      manifestPath: manifestPath,
      updateManifest: true,
      validate: true,
    });

    assert.equal(result.success, true);
    assert(result.checksums.sha256);
    assert(result.checksums.sha512);
    assert(result.manifest);
    assert.equal(result.manifest.checksums.sha256, result.checksums.sha256);

    // Verify CHECKSUMS.txt exists
    const checksumsExists = await fs
      .stat(checksumsPath)
      .then(() => true)
      .catch(() => false);
    assert(checksumsExists, 'CHECKSUMS.txt should exist');
  });

  it('should skip manifest update if updateManifest is false', async () => {
    const tgzPath = await createTempTgzFile('continue-2.0.0.tgz');
    const checksumsPath = path.join(tempDir, 'CHECKSUMS.txt');
    const manifestPath = path.join(tempDir, 'manifest.json');

    const result = await orchestrateChecksumGeneration({
      packagePath: tgzPath,
      checksumsOutputPath: checksumsPath,
      manifestPath: manifestPath,
      updateManifest: false,
    });

    assert.equal(result.success, true);
    assert.equal(result.manifest, null, 'Manifest should not be updated');
  });

  it('should propagate ChecksumGenerationError on failure', async () => {
    const missingPath = path.join(tempDir, 'nonexistent.tgz');

    try {
      await orchestrateChecksumGeneration({
        packagePath: missingPath,
        checksumsOutputPath: path.join(tempDir, 'CHECKSUMS.txt'),
        manifestPath: path.join(tempDir, 'manifest.json'),
      });
      assert.fail('Should throw ChecksumGenerationError');
    } catch (error) {
      assert(error instanceof ChecksumGenerationError);
    }
  });
});

// -------------------------------------------------------
// Summary
// -------------------------------------------------------

// Test Suite Summary:
// Suite 1: generateChecksums() — 5 tests ✓
//   - Compute correct hashes
//   - Idempotency verification
//   - Missing file handling
//   - Directory rejection
//   - Large file efficiency
//
// Suite 2: writeChecksumsFile() — 5 tests ✓
//   - Correct file format
//   - Custom filename support
//   - Invalid SHA256 rejection
//   - Invalid SHA512 rejection
//   - Directory creation
//
// Suite 3: updateManifestChecksums() — 4 tests ✓
//   - Manifest update correctness
//   - Auto-create checksums object
//   - Missing manifest handling
//   - Formatting preservation
//
// Suite 4: verifyChecksumsMatch() — 3 tests ✓
//   - Successful verification
//   - Mismatch detection
//   - Null handling
//
// Suite 5: validateChecksumsFile() — 3 tests ✓
//   - Format validation
//   - Incomplete file handling
//   - Missing file handling
//
// Suite 6: orchestrateChecksumGeneration() — 3 tests ✓
//   - Full integration flow
//   - Conditional manifest update
//   - Error propagation
//
// TOTAL: 23 test cases
