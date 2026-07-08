/**
 * Version Upgrade Validation Module (Step 32)
 *
 * Provides utilities to validate npm package upgrade paths, detect breaking changes,
 * check feature compatibility, and prevent unsafe downgrades.
 *
 * Core Functions:
 * - validateUpgradePath() — verifies upgrade is safe
 * - checkBreakingChanges() — identifies incompatibilities
 * - validateFeatureParity() — ensures stable features preserved
 * - getUpgradeRisks() — flags experimental/deprecated features
 * - shouldBlockDowngrade() — prevents unsafe downgrades
 * - generateUpgradeReport() — human-readable summary
 * - simulateUpgrade() — dry-run validation
 *
 * @module src/versions/v2.0.0/lib/version-upgrade.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 10 (downgrade warning), 31 (integrity tests), 35 (npm download)
 */

// ===== CUSTOM ERROR CLASSES =====

/**
 * Thrown when upgrade path is invalid or unsafe.
 */
export class UpgradeError extends Error {
  constructor(message, details = {}) {
    super(message);
    this.name = 'UpgradeError';
    this.details = details;
  }
}

/**
 * Thrown when downgrade is attempted.
 */
export class DowngradeBlockedError extends Error {
  constructor(currentVersion, targetVersion, reason = '') {
    super(`Downgrade blocked: ${currentVersion} → ${targetVersion}. ${reason}`);
    this.name = 'DowngradeBlockedError';
    this.currentVersion = currentVersion;
    this.targetVersion = targetVersion;
  }
}

/**
 * Thrown when breaking changes would break stability.
 */
export class BreakingChangeError extends Error {
  constructor(message, breakingChanges = []) {
    super(message);
    this.name = 'BreakingChangeError';
    this.breakingChanges = breakingChanges;
  }
}

// ===== HELPER FUNCTIONS =====

/**
 * Parses semantic version string into components.
 * @param {string} version - Semantic version (e.g., "1.9.5")
 * @returns {{major: number, minor: number, patch: number}} version components
 */
function parseVersion(version) {
  if (!version || typeof version !== 'string') {
    throw new UpgradeError(`Invalid version format: ${version}`, { version });
  }

  const parts = version.split('.');
  if (parts.length < 3) {
    throw new UpgradeError(`Version must have 3 components: ${version}`, { version });
  }

  const major = parseInt(parts[0], 10);
  const minor = parseInt(parts[1], 10);
  const patch = parseInt(parts[2], 10);

  if (isNaN(major) || isNaN(minor) || isNaN(patch)) {
    throw new UpgradeError(`Version components must be numeric: ${version}`, { version });
  }

  return { major, minor, patch };
}

/**
 * Compares two semantic versions.
 * @param {string} v1 - First version
 * @param {string} v2 - Second version
 * @returns {number} -1 if v1 < v2, 0 if equal, 1 if v1 > v2
 */
function compareVersions(v1, v2) {
  const p1 = parseVersion(v1);
  const p2 = parseVersion(v2);

  if (p1.major !== p2.major) return p1.major < p2.major ? -1 : 1;
  if (p1.minor !== p2.minor) return p1.minor < p2.minor ? -1 : 1;
  if (p1.patch !== p2.patch) return p1.patch < p2.patch ? -1 : 1;
  return 0;
}

// ===== CORE VALIDATION FUNCTIONS =====

/**
 * Validates that an upgrade path from one version to another is safe.
 *
 * @param {string} fromVersion - Source version (e.g., "1.9.5")
 * @param {string} toVersion - Target version (e.g., "2.0.0")
 * @param {Object} manifestRegistry - Map of version -> manifest object
 * @returns {Object} { valid: boolean, errors: string[], reason?: string }
 */
export function validateUpgradePath(fromVersion, toVersion, manifestRegistry = {}) {
  try {
    if (!fromVersion || !toVersion) {
      return {
        valid: false,
        errors: ['fromVersion and toVersion are required'],
        reason: 'Missing version parameters'
      };
    }

    const cmp = compareVersions(fromVersion, toVersion);
    if (cmp === 0) {
      return {
        valid: false,
        errors: ['Source and target versions are identical'],
        reason: 'No upgrade needed'
      };
    }

    if (cmp > 0) {
      return {
        valid: false,
        errors: [`Cannot upgrade from ${fromVersion} to ${toVersion} (downgrade)`],
        reason: 'Downgrade not allowed'
      };
    }

    // Check manifests exist
    if (!manifestRegistry[toVersion]) {
      return {
        valid: false,
        errors: [`Target version ${toVersion} manifest not found`],
        reason: 'Missing target manifest'
      };
    }

    const toManifest = manifestRegistry[toVersion];
    if (!toManifest.version || !toManifest.features) {
      return {
        valid: false,
        errors: [`Corrupted manifest for ${toVersion}`],
        reason: 'Invalid manifest schema'
      };
    }

    return {
      valid: true,
      errors: []
    };
  } catch (err) {
    return {
      valid: false,
      errors: [err.message],
      reason: 'Validation exception'
    };
  }
}

/**
 * Detects breaking changes between two versions.
 *
 * @param {string} fromVersion - Source version
 * @param {string} toVersion - Target version
 * @param {Object} fromManifest - Source manifest
 * @param {Object} toManifest - Target manifest
 * @returns {Object} { hasBreakingChanges: boolean, changes: Array<Object> }
 */
export function checkBreakingChanges(fromVersion, toVersion, fromManifest, toManifest) {
  const changes = [];

  if (!fromManifest || !toManifest) {
    return {
      hasBreakingChanges: true,
      changes: [{ type: 'ERROR', description: 'Missing manifest(s)' }]
    };
  }

  // Check for removed stable features
  const fromStable = fromManifest.features?.stable || [];
  const toStable = toManifest.features?.stable || [];

  for (const feature of fromStable) {
    if (!toStable.includes(feature)) {
      // Check if it's been replaced
      const replacement = toManifest.breakingChanges?.find(
        bc => bc.feature === feature
      );

      if (replacement) {
        changes.push({
          type: 'BREAKING_CHANGE',
          severity: 'HIGH',
          feature: feature,
          replacement: replacement.replacement,
          description: replacement.description
        });
      } else {
        changes.push({
          type: 'FEATURE_REMOVAL',
          severity: 'CRITICAL',
          feature: feature,
          description: `Feature removed without replacement`
        });
      }
    }
  }

  // Check for deprecated features still in use
  const fromDeprecated = fromManifest.features?.deprecated || [];
  const toDeprecated = toManifest.features?.deprecated || [];

  for (const feature of fromDeprecated) {
    if (!toDeprecated.includes(feature)) {
      changes.push({
        type: 'DEPRECATION_REMOVED',
        severity: 'MEDIUM',
        feature: feature,
        description: `Deprecated feature removed in ${toVersion}`
      });
    }
  }

  // Check for significant version jumps (major version change)
  const fromParsed = parseVersion(fromVersion);
  const toParsed = parseVersion(toVersion);

  if (fromParsed.major !== toParsed.major) {
    changes.push({
      type: 'MAJOR_VERSION_CHANGE',
      severity: 'HIGH',
      description: `Major version change: ${fromVersion} → ${toVersion}`
    });
  }

  return {
    hasBreakingChanges: changes.length > 0,
    changes: changes
  };
}

/**
 * Validates that all stable features from source version are present in target.
 *
 * @param {string} fromVersion - Source version
 * @param {string} toVersion - Target version
 * @param {Object} fromManifest - Source manifest
 * @param {Object} toManifest - Target manifest
 * @returns {Object} { hasParity: boolean, missingFeatures: string[] }
 */
export function validateFeatureParity(fromVersion, toVersion, fromManifest, toManifest) {
  const missingFeatures = [];

  if (!fromManifest || !toManifest) {
    return {
      hasParity: false,
      missingFeatures: ['Cannot validate: missing manifest(s)']
    };
  }

  const fromStable = fromManifest.features?.stable || [];
  const toStable = toManifest.features?.stable || [];
  const toBreakingChanges = toManifest.breakingChanges || [];

  for (const feature of fromStable) {
    const hasFeature = toStable.includes(feature);
    const hasReplacement = toBreakingChanges.some(bc => bc.feature === feature);

    if (!hasFeature && !hasReplacement) {
      missingFeatures.push(feature);
    }
  }

  return {
    hasParity: missingFeatures.length === 0,
    missingFeatures: missingFeatures
  };
}

/**
 * Identifies upgrade risks: experimental features that may be unstable.
 *
 * @param {string} fromVersion - Source version
 * @param {string} toVersion - Target version
 * @param {Object} toManifest - Target manifest
 * @returns {Object} { hasRisks: boolean, risks: Array<Object> }
 */
export function getUpgradeRisks(fromVersion, toVersion, toManifest) {
  const risks = [];

  if (!toManifest) {
    return {
      hasRisks: true,
      risks: [{ level: 'CRITICAL', description: 'Missing target manifest' }]
    };
  }

  const experimental = toManifest.features?.experimental || [];
  if (experimental.length > 0) {
    risks.push({
      level: 'INFO',
      type: 'EXPERIMENTAL_FEATURES',
      features: experimental,
      description: `${experimental.length} experimental feature(s) available`
    });
  }

  const deprecated = toManifest.features?.deprecated || [];
  if (deprecated.length > 0) {
    risks.push({
      level: 'WARNING',
      type: 'DEPRECATED_FEATURES',
      features: deprecated,
      description: `${deprecated.length} deprecated feature(s) still present`
    });
  }

  const nodeReqs = toManifest.compatibility?.nodeVersions || [];
  if (nodeReqs.length === 0) {
    risks.push({
      level: 'WARNING',
      type: 'MISSING_NODE_REQUIREMENTS',
      description: 'Node version requirements not specified'
    });
  }

  return {
    hasRisks: risks.length > 0,
    risks: risks
  };
}

/**
 * Determines whether a downgrade should be blocked.
 *
 * @param {string} currentVersion - Currently installed version
 * @param {string} targetVersion - Proposed target version
 * @returns {boolean} true if downgrade should be blocked
 */
export function shouldBlockDowngrade(currentVersion, targetVersion) {
  if (!currentVersion || !targetVersion) {
    return true;
  }

  try {
    const cmp = compareVersions(currentVersion, targetVersion);
    // Block if target is less than current (downgrade)
    return cmp > 0;
  } catch {
    return true; // Block on error
  }
}

/**
 * Generates a human-readable upgrade report.
 *
 * @param {string} fromVersion - Source version
 * @param {string} toVersion - Target version
 * @param {Array} changesList - Breaking changes from checkBreakingChanges()
 * @param {Array} risksList - Risks from getUpgradeRisks()
 * @returns {Object} { summary: string, sections: Object }
 */
export function generateUpgradeReport(fromVersion, toVersion, changesList = [], risksList = []) {
  const report = {
    summary: `Upgrade from v${fromVersion} to v${toVersion}`,
    sections: {
      overview: {
        from: fromVersion,
        to: toVersion,
        hasBreakingChanges: changesList.length > 0,
        hasRisks: risksList.length > 0
      },
      breakingChanges: [],
      risks: [],
      recommendations: []
    }
  };

  if (changesList.length > 0) {
    report.sections.breakingChanges = changesList.map(change => ({
      type: change.type,
      severity: change.severity,
      description: change.description,
      details: change
    }));

    report.sections.recommendations.push(
      'Review breaking changes before upgrading'
    );
  }

  if (risksList.length > 0) {
    report.sections.risks = risksList.map(risk => ({
      level: risk.level,
      type: risk.type,
      description: risk.description
    }));

    if (risksList.some(r => r.level === 'CRITICAL')) {
      report.sections.recommendations.push('CRITICAL RISKS DETECTED: Do not upgrade');
    } else if (risksList.some(r => r.level === 'WARNING')) {
      report.sections.recommendations.push('Proceed with caution: warnings present');
    }
  }

  if (changesList.length === 0 && risksList.length === 0) {
    report.sections.recommendations.push('Safe to upgrade');
  }

  return report;
}

/**
 * Simulates an upgrade without making permanent changes.
 *
 * @param {string} fromVersion - Source version
 * @param {string} toVersion - Target version
 * @param {Object} options - { manifestRegistry?, dryRun?: boolean }
 * @returns {Promise<Object>} { success: boolean, errors: string[], simulationResults: Object }
 */
export async function simulateUpgrade(fromVersion, toVersion, options = {}) {
  try {
    const { manifestRegistry = {}, dryRun = true } = options;

    const validationResult = validateUpgradePath(fromVersion, toVersion, manifestRegistry);
    if (!validationResult.valid) {
      return {
        success: false,
        errors: validationResult.errors,
        simulationResults: {
          stage: 'validation',
          dryRun: dryRun
        }
      };
    }

    const fromManifest = manifestRegistry[fromVersion];
    const toManifest = manifestRegistry[toVersion];

    const breakingChangesResult = checkBreakingChanges(
      fromVersion,
      toVersion,
      fromManifest,
      toManifest
    );

    const featureParityResult = validateFeatureParity(
      fromVersion,
      toVersion,
      fromManifest,
      toManifest
    );

    const risksResult = getUpgradeRisks(fromVersion, toVersion, toManifest);

    return {
      success: true,
      errors: [],
      simulationResults: {
        stage: 'complete',
        dryRun: dryRun,
        breakingChanges: breakingChangesResult,
        featureParity: featureParityResult,
        risks: risksResult,
        report: generateUpgradeReport(
          fromVersion,
          toVersion,
          breakingChangesResult.changes,
          risksResult.risks
        )
      }
    };
  } catch (err) {
    return {
      success: false,
      errors: [err.message],
      simulationResults: {
        stage: 'error',
        exception: err.name
      }
    };
  }
}

export default {
  // Error classes
  UpgradeError,
  DowngradeBlockedError,
  BreakingChangeError,

  // Core functions
  validateUpgradePath,
  checkBreakingChanges,
  validateFeatureParity,
  getUpgradeRisks,
  shouldBlockDowngrade,
  generateUpgradeReport,
  simulateUpgrade,

  // Helpers
  parseVersion,
  compareVersions
};
