#!/usr/bin/env node

/**
 * Cross-Version Manifest Compatibility Tests (Step 111)
 *
 * Validates that manifest files conform to the schema and properly declare
 * feature compatibility, platform support, and npm package metadata.
 *
 * Test Coverage:
 * - Suite 1: Manifest schema validation (4 tests)
 * - Suite 2: Feature compatibility matrix (4 tests)
 * - Suite 3: Platform/Node version compatibility (4 tests)
 * - Suite 4: NPM package metadata (3 tests)
 * Total: 15 tests
 *
 * @module src/versions/v2.0.0/tests/cross-version-manifest-compatibility.test.mjs
 * @version 1.0.0
 */

import { strict as assert } from 'assert';
import { describe, it, beforeEach } from 'mocha';
import Ajv from 'ajv';
import { readFileSync } from 'fs';
import { resolve } from 'path';

// ===== FIXTURES & SETUP =====

const schemaPath = resolve(process.cwd(), 'src/versions/manifest.schema.json');
const manifestPath = resolve(process.cwd(), 'src/versions/v2.0.0/manifest.json');

let schema;
let manifest;
let ajv;
let validate;

beforeEach(() => {
  schema = JSON.parse(readFileSync(schemaPath, 'utf-8'));
  manifest = JSON.parse(readFileSync(manifestPath, 'utf-8'));
  ajv = new Ajv({ strict: false, allErrors: true });
  validate = ajv.compile(schema);
});

// ===== SUITE 1: Manifest Schema Validation =====

describe('Cross-Version Manifest Compatibility - Schema Validation', function () {
  this.timeout(3000);

  it('should have v2.0.0 manifest conform to schema', () => {
    const valid = validate(manifest);
    if (!valid) {
      console.error('Schema validation errors:', validate.errors);
    }
    assert.ok(valid, 'Manifest should conform to schema');
  });

  it('should have valid semantic version format', () => {
    const versionPattern = /^\d+\.\d+\.\d+$/;
    assert.match(manifest.version, versionPattern, 'Version must be X.Y.Z format');
  });

  it('should have all required fields', () => {
    const required = ['version', 'releaseDate', 'npmPackage', 'checksums', 'compatibility'];
    for (const field of required) {
      assert.ok(manifest.hasOwnProperty(field), `Manifest must have ${field} field`);
    }
  });

  it('should have no additional properties', () => {
    const allowed = ['version', 'releaseDate', 'npmPackage', 'checksums', 'compatibility', 'features', 'dependencies'];
    const extra = Object.keys(manifest).filter(key => !allowed.includes(key));
    assert.strictEqual(extra.length, 0, `No additional properties allowed, found: ${extra.join(', ')}`);
  });
});

// ===== SUITE 2: Feature Compatibility Matrix =====

describe('Cross-Version Manifest Compatibility - Feature Matrix', function () {
  this.timeout(3000);

  it('should declare stable features', () => {
    assert.ok(manifest.features, 'Manifest must have features object');
    assert.ok(Array.isArray(manifest.features.stable), 'Features.stable must be array');
    assert.ok(manifest.features.stable.length > 0, 'Must declare at least one stable feature');

    const expectedStable = ['coreEditorIntegration', 'diagnosticsCollection', 'goToDefinition', 'findReferences', 'codeCompletion', 'search'];
    for (const feature of expectedStable) {
      assert.ok(manifest.features.stable.includes(feature), `Feature ${feature} should be stable`);
    }
  });

  it('should declare experimental features', () => {
    assert.ok(Array.isArray(manifest.features.experimental), 'Features.experimental must be array');
    assert.ok(manifest.features.experimental.length > 0, 'Must declare at least one experimental feature');
    assert.ok(manifest.features.experimental.includes('advancedSymbolSearch') || manifest.features.experimental.includes('webviewMessaging'), 
      'Experimental features should include known experimental features');
  });

  it('should have deprecated array (even if empty)', () => {
    assert.ok(Array.isArray(manifest.features.deprecated), 'Features.deprecated must be array');
  });

  it('should not have duplicate features across categories', () => {
    const stable = new Set(manifest.features.stable);
    const experimental = new Set(manifest.features.experimental);
    const deprecated = new Set(manifest.features.deprecated);

    const stableExperimental = [...stable].filter(f => experimental.has(f));
    const stableDeprecated = [...stable].filter(f => deprecated.has(f));

    assert.strictEqual(stableExperimental.length, 0, 'Stable and experimental features must not overlap');
    assert.strictEqual(stableDeprecated.length, 0, 'Stable and deprecated features must not overlap');
  });
});

// ===== SUITE 3: Platform & Node Version Compatibility =====

describe('Cross-Version Manifest Compatibility - Platform/Node Version', function () {
  this.timeout(3000);

  it('should declare win32 platform', () => {
    assert.ok(Array.isArray(manifest.compatibility.platforms), 'Compatibility.platforms must be array');
    assert.ok(manifest.compatibility.platforms.includes('win32'), 'Must support win32 platform');
  });

  it('should declare Node.js version 18 or higher', () => {
    assert.ok(Array.isArray(manifest.compatibility.nodeVersions), 'Compatibility.nodeVersions must be array');
    assert.ok(manifest.compatibility.nodeVersions.length > 0, 'Must declare Node versions');

    const hasNode18Plus = manifest.compatibility.nodeVersions.some(v => {
      const major = parseInt(v.split('.')[0], 10);
      return major >= 18;
    });
    assert.ok(hasNode18Plus, 'Must support Node.js 18.x or higher');
  });

  it('should have valid VS Code version range', () => {
    assert.ok(Array.isArray(manifest.compatibility.vsCodeVersions), 'Compatibility.vsCodeVersions must be array');
    assert.ok(manifest.compatibility.vsCodeVersions.length > 0, 'Must declare VS Code versions');

    const versionPattern = /^\d+\.\d+\.\d+$/;
    for (const version of manifest.compatibility.vsCodeVersions) {
      assert.match(version, versionPattern, `VS Code version ${version} must be X.Y.Z format`);
    }
  });

  it('should have consistent version ordering', () => {
    const nodeVersions = manifest.compatibility.nodeVersions
      .map(v => {
        const parts = v.split('.');
        return parseInt(parts[0], 10) * 100 + (parseInt(parts[1], 10) || 0);
      });

    for (let i = 1; i < nodeVersions.length; i++) {
      assert.ok(nodeVersions[i] > nodeVersions[i - 1], 'Node versions should be in ascending order');
    }
  });
});

// ===== SUITE 4: NPM Package Metadata =====

describe('Cross-Version Manifest Compatibility - NPM Package Metadata', function () {
  this.timeout(3000);

  it('should have valid tarball URL', () => {
    const { tarballUrl } = manifest.npmPackage;
    const urlPattern = /^https?:\/\/.+\.tgz$/;
    assert.match(tarballUrl, urlPattern, 'Tarball URL must be HTTPS and end with .tgz');
  });

  it('should have valid checksum hashes', () => {
    const { sha256, sha512 } = manifest.checksums;

    // Validate SHA256 (64 hex chars)
    const sha256Pattern = /^[a-f0-9]{64}$/;
    assert.match(sha256, sha256Pattern, 'SHA256 must be 64 lowercase hex characters');

    // Validate SHA512 if present (128 hex chars)
    if (sha512) {
      const sha512Pattern = /^[a-f0-9]{128}$/;
      assert.match(sha512, sha512Pattern, 'SHA512 must be 128 lowercase hex characters');
    }
  });

  it('should declare npm package metadata', () => {
    const { name, version, tarballUrl, registry } = manifest.npmPackage;

    assert.strictEqual(name, 'continue', 'Package name should be "continue"');
    assert.strictEqual(version, '2.0.0', 'Package version should match manifest version');
    assert.ok(tarballUrl, 'Tarball URL must be specified');
    assert.ok(registry, 'Registry must be specified');
  });
});

// ===== EXPORT FOR TEST RUNNER =====

export { validate };
