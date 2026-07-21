#!/usr/bin/env node

/**
 * regression-ci-integration.mjs
 * Step 112: Regression Test Suite - CI/CD Pipeline Integration
 * 
 * Exports functions for GitHub Actions and other CI/CD pipelines.
 * Provides release gating, dashboard generation, and decision recording.
 * 
 * @module regression-ci-integration
 */

import fs from 'fs';
import path from 'path';
import os from 'os';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * Check if release is approved (gate check).
 * @param {Object} options - Configuration options
 * @returns {Promise<Object>} { approved: boolean, reason: string, exitCode: number }
 */
export async function checkReleaseGate(options = {}) {
  try {
    const {
      comparisonResult = {},
      thresholds = {},
      baselineVersion = 'v2.0.0',
      requirePass = true,
      logger = null
    } = options;

    const summary = comparisonResult.summary || {};
    const tierStatus = comparisonResult.tierStatus || { fast: true, medium: true, slow: true };

    // Determine if release is approved
    const criticalCount = summary.criticalCount || 0;
    const tiersPassed = tierStatus.fast && tierStatus.medium && tierStatus.slow;

    const approved = criticalCount === 0 && tiersPassed;
    const exitCode = approved ? 0 : 1;

    const reason = approved
      ? 'All performance gates passed, release approved'
      : generateBlockReason(summary, tierStatus);

    logger?.log?.(`[Release Gate] Exit Code: ${exitCode}, Approved: ${approved}`);

    return {
      approved,
      passed: approved,
      reason,
      exitCode,
      summary,
      tierStatus,
      timestamp: Date.now()
    };
  } catch (error) {
    return {
      approved: false,
      passed: false,
      reason: `Error checking release gate: ${error.message}`,
      exitCode: 1,
      error: error.message,
      timestamp: Date.now()
    };
  }
}

/**
 * Generate CI/CD dashboard JSON.
 * @param {Object} report - JSON report from formatJSONReport()
 * @param {Object} options - Configuration options
 * @returns {Object} Dashboard object
 */
export function generateCIDashboard(report, options = {}) {
  const summary = report.summary || {};
  const tierStatus = report.tierStatus || {};

  const dashboard = {
    timestamp: Date.now(),
    isoTimestamp: new Date().toISOString(),
    buildStatus: summary.releaseGate === 'BLOCKED' ? 'FAILED' : 'PASSED',
    releaseGate: {
      approved: summary.releaseGate !== 'BLOCKED',
      decision: summary.releaseGate
    },
    summary: {
      totalHandlers: summary.totalHandlers || 0,
      testedHandlers: summary.testedHandlers || 0,
      passedHandlers: summary.passedHandlers || 0,
      regressions: {
        total: summary.regressionCount || 0,
        critical: summary.criticalCount || 0,
        high: summary.highCount || 0,
        medium: summary.mediumCount || 0,
        low: summary.lowCount || 0
      }
    },
    tierStatus: {
      fast: {
        status: tierStatus.fast ? 'PASS' : 'FAIL',
        passed: tierStatus.fast
      },
      medium: {
        status: tierStatus.medium ? 'PASS' : 'FAIL',
        passed: tierStatus.medium
      },
      slow: {
        status: tierStatus.slow ? 'PASS' : 'FAIL',
        passed: tierStatus.slow
      },
      allPassed: tierStatus.fast && tierStatus.medium && tierStatus.slow
    },
    topRegressions: getTopRegressions(report.regressions || [], 5),
    failedTiers: Object.entries(tierStatus)
      .filter(([_, pass]) => !pass)
      .map(([tier]) => tier),
    metrics: {
      baselineVersion: report.baseline?.version || 'unknown',
      baselineTimestamp: report.baseline?.timestamp || null,
      currentVersion: report.current?.version || 'v2.0.0-candidate',
      currentTimestamp: report.current?.timestamp || null
    }
  };

  return dashboard;
}

/**
 * Record release decision for audit trail.
 * @param {Object} decision - Release decision object
 * @param {Object} options - Configuration options
 * @returns {Promise<void>}
 */
export async function recordReleaseDecision(decision, options = {}) {
  try {
    const {
      decisionFile = null,
      logger = null
    } = options;

    const home = process.env.USERPROFILE || process.env.HOME || os.homedir();
    const decisionDir = path.join(home, '.continue', 'release-decisions');

    // Create directory if needed
    await fs.promises.mkdir(decisionDir, { recursive: true });

    // Generate decision filename
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const filename = `release-decision-${timestamp}.json`;
    const filepath = decisionFile || path.join(decisionDir, filename);

    // Prepare decision record
    const record = {
      timestamp: Date.now(),
      isoTimestamp: new Date().toISOString(),
      decision: decision.approved || decision.passed ? 'APPROVED' : 'BLOCKED',
      reason: decision.reason,
      exitCode: decision.exitCode,
      summary: decision.summary,
      tierStatus: decision.tierStatus,
      environment: {
        node: process.version,
        platform: process.platform,
        arch: process.arch
      }
    };

    // Write to file
    const content = JSON.stringify(record, null, 2);
    await fs.promises.writeFile(filepath, content, 'utf-8');

    logger?.log?.(`[Release Decision] Recorded to ${filepath}`);

    return { filepath, record };
  } catch (error) {
    throw new Error(`Failed to record release decision: ${error.message}`);
  }
}

/**
 * Generate GitHub Actions summary.
 * @param {Object} report - JSON report from formatJSONReport()
 * @param {Object} options - Configuration options
 * @returns {string} GitHub Actions summary markdown
 */
export function generateGitHubSummary(report, options = {}) {
  const summary = report.summary || {};
  const tierStatus = report.tierStatus || {};
  const approved = summary.releaseGate !== 'BLOCKED';

  let md = '';

  md += approved ? '✅ **Release Approved**' : '❌ **Release Blocked**';
  md += '\n\n';

  md += '### Performance Regression Test Results\n\n';
  md += `- **Status**: ${summary.releaseGate || 'UNKNOWN'}\n`;
  md += `- **Total Handlers**: ${summary.totalHandlers || 0}\n`;
  md += `- **Regressions**: ${summary.regressionCount || 0}\n`;
  md += `- **Critical**: ${summary.criticalCount || 0}\n`;
  md += `- **High**: ${summary.highCount || 0}\n`;
  md += `- **Medium**: ${summary.mediumCount || 0}\n`;
  md += `- **Low**: ${summary.lowCount || 0}\n\n`;

  md += '### Tier Status\n\n';
  md += `- Fast Tier: ${tierStatus.fast ? '✅ PASS' : '❌ FAIL'}\n`;
  md += `- Medium Tier: ${tierStatus.medium ? '✅ PASS' : '❌ FAIL'}\n`;
  md += `- Slow Tier: ${tierStatus.slow ? '✅ PASS' : '❌ FAIL'}\n\n`;

  if (!approved) {
    const failures = (report.regressions || []).filter(r => r.severity !== 'NONE' && r.severity !== 'LOW');
    if (failures.length > 0) {
      md += '### Key Regressions\n\n';
      for (const regression of failures.slice(0, 5)) {
        md += `- **${regression.handler}** (${regression.severity}): ${regression.remediation}\n`;
      }
      md += '\n';
    }
  }

  return md;
}

/**
 * Get environment variable configuration.
 * @returns {Object} Configuration from environment variables
 */
export function getEnvironmentConfig() {
  return {
    tolerancePercent: parseInt(process.env.REGRESSION_TOLERANCE_PCT || '10', 10),
    p99Threshold: parseInt(process.env.REGRESSION_P99_THRESHOLD || '25', 10),
    baselineVersion: process.env.REGRESSION_BASELINE_VERSION || 'v2.0.0',
    requirePass: (process.env.REGRESSION_REQUIRE_PASS || 'true').toLowerCase() === 'true',
    enableLogging: (process.env.REGRESSION_ENABLE_LOGGING || 'false').toLowerCase() === 'true'
  };
}

/**
 * Export release gate check for CI/CD scripts.
 * Usage in pipeline:
 *   const { checkReleaseGate } = await import('./regression-ci-integration.mjs');
 *   const result = await checkReleaseGate({ comparisonResult: ... });
 *   process.exit(result.exitCode);
 * @returns {Object} Release gate API
 */
export const releaseGateAPI = {
  check: checkReleaseGate,
  dashboard: generateCIDashboard,
  record: recordReleaseDecision,
  summary: generateGitHubSummary,
  config: getEnvironmentConfig
};

/**
 * Helper: Get top regressions by severity.
 * @private
 */
function getTopRegressions(regressions, limit = 5) {
  const sorted = [...(regressions || [])]
    .sort((a, b) => {
      const severityOrder = { CRITICAL: 0, HIGH: 1, MEDIUM: 2, LOW: 3, NONE: 4 };
      return (severityOrder[a.severity] || 4) - (severityOrder[b.severity] || 4);
    })
    .slice(0, limit);

  return sorted.map(r => ({
    handler: r.handler,
    severity: r.severity,
    tier: r.tier,
    details: r.remediation
  }));
}

/**
 * Helper: Generate block reason.
 * @private
 */
function generateBlockReason(summary, tierStatus) {
  const reasons = [];

  if ((summary.criticalCount || 0) > 0) {
    reasons.push(`${summary.criticalCount} critical regression(s)`);
  }

  const failedTiers = Object.entries(tierStatus)
    .filter(([_, pass]) => !pass)
    .map(([tier]) => tier);

  if (failedTiers.length > 0) {
    reasons.push(`${failedTiers.join(', ')} tier(s) failed`);
  }

  if (reasons.length === 0) {
    reasons.push('Performance regression threshold exceeded');
  }

  return 'Release blocked: ' + reasons.join('; ');
}

/**
 * Export default for convenient access.
 */
export default {
  checkReleaseGate,
  generateCIDashboard,
  recordReleaseDecision,
  generateGitHubSummary,
  getEnvironmentConfig,
  releaseGateAPI
};
