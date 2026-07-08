/**
 * Unit Tests for Version Upgrade Validation Module (Step 32)
 *
 * Comprehensive test suite covering all upgrade path scenarios, breaking changes detection,
 * feature parity validation, downgrade prevention, and error handling.
 *
 * Test Structure: 8 suites, 23 test cases
 * - validateUpgradePath: 3 tests (valid paths, invalid versions, missing manifests)
 * - checkBreakingChanges: 4 tests (feature removals, API changes, deprecations, none)
 * - validateFeatureParity: 3 tests (parity present, missing features, edge cases)
 * - getUpgradeRisks: 4 tests (experimental features, deprecated features, no risks, mixed)
 * - shouldBlockDowngrade: 3 tests (prevent downgrade, same version, allow forward)
 * - generateUpgradeReport: 3 tests (happy path, with risks, formatting)
 * - simulateUpgrade: 2 tests (dry-run validation, error recovery)
 * - Edge Cases: 1 test (corrupted metadata)
 *
 * @module src/versions/v2.0.0/tests/version-upgrade.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 32 (version upgrade), 10 (downgrade warning), 31 (integrity tests)
 */

import { strict as assert } from 'assert';
import { describe, it, beforeEach, afterEach } from 'mocha';
import * as upgrade from '../lib/version-upgrade.js';
import {
  getManifestV195,
  getManifestV200,
  getManifestV210,
  getCorruptedManifest
} from './mocks/manifest-mock.mjs';

// ===== TEST SETUP & FIXTURES =====

describe('Version Upgrade Validation Module (Step 32)', function () {
  this.timeout(5000);

  let manifestRegistry;
  let v195Manifest;
  let v200Manifest;
  let v210Manifest;

  beforeEach(function () {
    v195Manifest = getManifestV195();
    v200Manifest = getManifestV200();
    v210Manifest = getManifestV210();

    manifestRegistry = {
      '1.9.5': v195Manifest,
      '2.0.0': v200Manifest,
      '2.1.0': v210Manifest
    };
  });

  afterEach(function () {
    manifestRegistry = null;
  });

  // ===== SUITE 1: validateUpgradePath() =====

  describe('validateUpgradePath()', function () {
    it('should validate a valid upgrade path (1.9.5 → 2.0.0)', function () {
      const result = upgrade.validateUpgradePath('1.9.5', '2.0.0', manifestRegistry);

      assert.strictEqual(result.valid, true, 'Upgrade should be valid');
      assert.strictEqual(result.errors.length, 0, 'No errors for valid path');
    });

    it('should reject invalid version format', function () {
      const result = upgrade.validateUpgradePath('1.9', '2.0.0', manifestRegistry);

      assert.strictEqual(result.valid, false, 'Invalid version should fail');
      assert(result.errors.length > 0, 'Should have error messages');
      assert(
        result.errors[0].toLowerCase().includes('version'),
        'Error should mention version'
      );
    });

    it('should reject when target manifest is missing', function () {
      const result = upgrade.validateUpgradePath('1.9.5', '3.0.0', manifestRegistry);

      assert.strictEqual(result.valid, false, 'Missing manifest should fail');
      assert(result.errors[0].includes('manifest'), 'Error should mention manifest');
    });
  });

  // ===== SUITE 2: checkBreakingChanges() =====

  describe('checkBreakingChanges()', function () {
    it('should detect breaking changes (v1.9.5 → v2.0.0)', function () {
      const result = upgrade.checkBreakingChanges(
        '1.9.5',
        '2.0.0',
        v195Manifest,
        v200Manifest
      );

      assert.strictEqual(
        result.hasBreakingChanges,
        true,
        'Should detect breaking changes'
      );
      assert(result.changes.length > 0, 'Should list breaking changes');

      const hasFeatureRemovals = result.changes.some(
        c => c.type === 'BREAKING_CHANGE' || c.type === 'FEATURE_REMOVAL'
      );
      assert(hasFeatureRemovals, 'Should detect feature removals');
    });

    it('should identify deprecated features being removed', function () {
      const result = upgrade.checkBreakingChanges(
        '1.9.5',
        '2.0.0',
        v195Manifest,
        v200Manifest
      );

      const deprecationRemovals = result.changes.filter(
        c => c.type === 'DEPRECATION_REMOVED'
      );
      assert(
        deprecationRemovals.length > 0,
        'Should detect deprecated feature removals'
      );
    });

    it('should flag major version transitions', function () {
      const result = upgrade.checkBreakingChanges(
        '1.9.5',
        '2.0.0',
        v195Manifest,
        v200Manifest
      );

      const majorChange = result.changes.find(c => c.type === 'MAJOR_VERSION_CHANGE');
      assert(majorChange, 'Should flag major version change');
      assert(majorChange.severity === 'HIGH', 'Major version should be HIGH severity');
    });

    it('should report no breaking changes for minor upgrade', function () {
      const result = upgrade.checkBreakingChanges(
        '2.0.0',
        '2.1.0',
        v200Manifest,
        v210Manifest
      );

      // v2.0.0 → v2.1.0 is a minor version bump, should have no breaking changes
      // (though may have warnings about new experimental features)
      const breakingChanges = result.changes.filter(
        c =>
          c.type === 'BREAKING_CHANGE' ||
          c.type === 'FEATURE_REMOVAL' ||
          c.type === 'MAJOR_VERSION_CHANGE'
      );
      assert(
        breakingChanges.length === 0,
        'Minor version upgrade should have no breaking changes'
      );
    });
  });

  // ===== SUITE 3: validateFeatureParity() =====

  describe('validateFeatureParity()', function () {
    it('should validate feature parity when all stable features are preserved', function () {
      const result = upgrade.validateFeatureParity(
        '2.0.0',
        '2.1.0',
        v200Manifest,
        v210Manifest
      );

      assert.strictEqual(result.hasParity, true, 'Should have parity');
      assert.strictEqual(result.missingFeatures.length, 0, 'No missing features');
    });

    it('should report missing features when parity is broken', function () {
      // Create a manifest that removes a feature
      const brokenManifest = {
        ...v200Manifest,
        features: {
          ...v200Manifest.features,
          stable: ['coreEditorIntegration'] // Remove most features
        }
      };

      const result = upgrade.validateFeatureParity(
        '1.9.5',
        '2.0.0',
        v195Manifest,
        brokenManifest
      );

      assert.strictEqual(result.hasParity, false, 'Should detect missing parity');
      assert(result.missingFeatures.length > 0, 'Should list missing features');
    });

    it('should handle missing manifests gracefully', function () {
      const result = upgrade.validateFeatureParity(
        '1.9.5',
        '2.0.0',
        null,
        v200Manifest
      );

      assert.strictEqual(result.hasParity, false, 'Should fail with null manifest');
      assert(result.missingFeatures.length > 0, 'Should report error in missing features');
    });
  });

  // ===== SUITE 4: getUpgradeRisks() =====

  describe('getUpgradeRisks()', function () {
    it('should identify experimental features as info-level risks', function () {
      const result = upgrade.getUpgradeRisks('1.9.5', '2.0.0', v200Manifest);

      assert.strictEqual(result.hasRisks, true, 'Should have risks');
      const expRisks = result.risks.filter(r => r.type === 'EXPERIMENTAL_FEATURES');
      assert(expRisks.length > 0, 'Should identify experimental features');
      assert(expRisks[0].level === 'INFO', 'Experimental features should be INFO level');
    });

    it('should flag deprecated features present in target version', function () {
      const manifestWithDeprecated = {
        ...v200Manifest,
        features: {
          ...v200Manifest.features,
          deprecated: ['oldFeature1', 'oldFeature2']
        }
      };

      const result = upgrade.getUpgradeRisks('1.9.5', '2.0.0', manifestWithDeprecated);

      const depRisks = result.risks.filter(r => r.type === 'DEPRECATED_FEATURES');
      assert(depRisks.length > 0, 'Should flag deprecated features');
      assert(depRisks[0].level === 'WARNING', 'Deprecated features should be WARNING level');
    });

    it('should report no risks when upgrade is clean', function () {
      const cleanManifest = {
        ...v200Manifest,
        features: {
          stable: v200Manifest.features.stable,
          experimental: [],
          deprecated: []
        },
        compatibility: {
          nodeVersions: ['18.0.0', '20.0.0'],
          vsCodeVersions: ['1.80.0']
        }
      };

      const result = upgrade.getUpgradeRisks('1.9.5', '2.0.0', cleanManifest);

      assert.strictEqual(result.hasRisks, false, 'Should have no risks');
      assert.strictEqual(result.risks.length, 0, 'Risk list should be empty');
    });

    it('should identify mixed risk levels', function () {
      const mixedManifest = {
        ...v200Manifest,
        features: {
          stable: v200Manifest.features.stable,
          experimental: ['newFeature1', 'newFeature2'],
          deprecated: ['oldFeature1']
        }
      };

      const result = upgrade.getUpgradeRisks('1.9.5', '2.0.0', mixedManifest);

      assert.strictEqual(result.hasRisks, true, 'Should detect mixed risks');
      assert(result.risks.length >= 2, 'Should list multiple risk types');
      const hasInfo = result.risks.some(r => r.level === 'INFO');
      const hasWarning = result.risks.some(r => r.level === 'WARNING');
      assert(hasInfo && hasWarning, 'Should have both INFO and WARNING levels');
    });
  });

  // ===== SUITE 5: shouldBlockDowngrade() =====

  describe('shouldBlockDowngrade()', function () {
    it('should block downgrade from 2.0.0 to 1.9.5', function () {
      const shouldBlock = upgrade.shouldBlockDowngrade('2.0.0', '1.9.5');

      assert.strictEqual(shouldBlock, true, 'Should block downgrade');
    });

    it('should allow same version (no-op)', function () {
      const shouldBlock = upgrade.shouldBlockDowngrade('2.0.0', '2.0.0');

      assert.strictEqual(shouldBlock, false, 'Should not block same version');
    });

    it('should allow forward upgrade from 1.9.5 to 2.0.0', function () {
      const shouldBlock = upgrade.shouldBlockDowngrade('1.9.5', '2.0.0');

      assert.strictEqual(shouldBlock, false, 'Should allow forward upgrade');
    });
  });

  // ===== SUITE 6: generateUpgradeReport() =====

  describe('generateUpgradeReport()', function () {
    it('should generate report for clean upgrade', function () {
      const report = upgrade.generateUpgradeReport('1.9.5', '2.0.0', [], []);

      assert(report.summary, 'Should have summary');
      assert(report.summary.includes('1.9.5'), 'Summary should include from version');
      assert(report.summary.includes('2.0.0'), 'Summary should include to version');
      assert.strictEqual(report.sections.breakingChanges.length, 0, 'No breaking changes');
      assert.strictEqual(report.sections.risks.length, 0, 'No risks');
      assert(
        report.sections.recommendations.some(r => r.includes('Safe')),
        'Should recommend safe upgrade'
      );
    });

    it('should report breaking changes in upgrade report', function () {
      const breakingChanges = [
        {
          type: 'BREAKING_CHANGE',
          severity: 'HIGH',
          description: 'API changed'
        }
      ];

      const report = upgrade.generateUpgradeReport(
        '1.9.5',
        '2.0.0',
        breakingChanges,
        []
      );

      assert.strictEqual(report.sections.breakingChanges.length, 1, 'Should list changes');
      assert(
        report.sections.recommendations.some(r =>
          r.includes('breaking changes')
        ),
        'Should warn about breaking changes'
      );
    });

    it('should format report with risk levels', function () {
      const risks = [
        { level: 'WARNING', type: 'DEPRECATED_FEATURES' },
        { level: 'INFO', type: 'EXPERIMENTAL_FEATURES' }
      ];

      const report = upgrade.generateUpgradeReport('1.9.5', '2.0.0', [], risks);

      assert.strictEqual(report.sections.risks.length, 2, 'Should list risks');
      assert(
        report.sections.recommendations.some(r => r.includes('caution')),
        'Should recommend caution for warnings'
      );
    });
  });

  // ===== SUITE 7: simulateUpgrade() =====

  describe('simulateUpgrade()', function () {
    it('should simulate valid upgrade without side effects (dry-run)', async function () {
      const result = await upgrade.simulateUpgrade('1.9.5', '2.0.0', {
        manifestRegistry: manifestRegistry,
        dryRun: true
      });

      assert.strictEqual(result.success, true, 'Simulation should succeed');
      assert.strictEqual(result.errors.length, 0, 'No errors in dry-run');
      assert.strictEqual(result.simulationResults.dryRun, true, 'Should be marked as dry-run');
      assert(result.simulationResults.report, 'Should generate report');
    });

    it('should recover from upgrade errors gracefully', async function () {
      const result = await upgrade.simulateUpgrade('1.9.5', '3.0.0', {
        manifestRegistry: manifestRegistry,
        dryRun: true
      });

      assert.strictEqual(result.success, false, 'Should fail for missing target');
      assert(result.errors.length > 0, 'Should report errors');
    });
  });

  // ===== SUITE 8: EDGE CASES =====

  describe('Edge Cases & Error Handling', function () {
    it('should handle corrupted manifests gracefully', function () {
      const corrupted = getCorruptedManifest();

      const parityResult = upgrade.validateFeatureParity(
        '1.9.5',
        '2.0.0',
        v195Manifest,
        corrupted
      );

      // Should fail or report missing features due to corrupted manifest
      assert(
        parityResult.hasParity === false || parityResult.missingFeatures.length > 0,
        'Should handle corrupted manifest'
      );
    });

    it('should validate version parsing edge cases', function () {
      // Test empty version
      const emptyResult = upgrade.validateUpgradePath('', '2.0.0', manifestRegistry);
      assert.strictEqual(emptyResult.valid, false, 'Should reject empty version');
      assert(emptyResult.errors.length > 0, 'Should provide error message for empty version');

      // Test undefined version
      const result = upgrade.validateUpgradePath(undefined, '2.0.0', manifestRegistry);
      assert.strictEqual(result.valid, false, 'Should reject undefined version');
    });

    it('should handle missing version data in validateUpgradePath', function () {
      const result = upgrade.validateUpgradePath('1.9.5', '2.0.0', {});

      assert.strictEqual(result.valid, false, 'Should fail with empty registry');
      assert(result.errors.length > 0, 'Should report error');
    });

    it('should compare versions correctly across patch/minor/major', function () {
      // Test patch
      const v1 = upgrade.validateUpgradePath('1.9.4', '1.9.5', manifestRegistry);
      assert(v1.errors[0]?.includes('manifest') || v1.valid, 'Patch upgrade should validate');

      // Test minor
      const v2 = upgrade.validateUpgradePath('1.8.0', '1.9.5', manifestRegistry);
      assert(v2.errors[0]?.includes('manifest') || v2.valid, 'Minor upgrade should validate');

      // Test major
      const v3 = upgrade.validateUpgradePath('1.9.5', '2.0.0', manifestRegistry);
      assert.strictEqual(v3.valid, true, 'Major upgrade should validate');
    });
  });
});
