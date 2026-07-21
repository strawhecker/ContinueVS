#!/usr/bin/env node

/**
 * regression-report-formatter.mjs
 * Step 112: Regression Test Suite - Report Formatting
 * 
 * Generates machine-readable (JSON) and human-readable (Markdown) regression reports.
 * Formats CI/CD summaries and actionable remediation paths.
 * 
 * @module regression-report-formatter
 */

/**
 * Format comparison result as JSON report (machine-readable).
 * @param {Object} comparisonResult - Result from RegressionComparisonEngine.compareMetrics()
 * @param {Object} options - Formatting options
 * @returns {Object} JSON report structure
 */
export function formatJSONReport(comparisonResult, options = {}) {
  const handlerRegressions = comparisonResult.handlerRegressions || [];
  const summary = comparisonResult.summary || {};
  const tierStatus = comparisonResult.tierStatus || {};

  const report = {
    format: 'json',
    version: '1.0',
    timestamp: Date.now(),
    isoTimestamp: new Date().toISOString(),
    baseline: {
      version: comparisonResult.baselineVersion || 'unknown',
      timestamp: comparisonResult.baselineTimestamp || null
    },
    current: {
      version: options.currentVersion || 'v2.0.0-candidate',
      timestamp: Date.now()
    },
    regressions: handlerRegressions.map(r => ({
      handler: r.handler,
      tier: r.tier,
      severity: r.overallSeverity,
      metrics: {
        latency: r.metrics.latency || null,
        throughput: r.metrics.throughput || null,
        memory: r.metrics.memory || null,
        errorRate: r.metrics.errorRate || null
      },
      remediation: generateRemediationPath(r)
    })),
    tierStatus: {
      fast: tierStatus.fast || false,
      medium: tierStatus.medium || false,
      slow: tierStatus.slow || false,
      allTiersPassed: tierStatus.fast && tierStatus.medium && tierStatus.slow
    },
    summary: {
      totalHandlers: summary.totalHandlers || 0,
      testedHandlers: summary.testedHandlers || 0,
      passedHandlers: summary.passedHandlers || 0,
      regressionCount: summary.regressionCount || 0,
      criticalCount: summary.criticalCount || 0,
      highCount: summary.highCount || 0,
      mediumCount: summary.mediumCount || 0,
      lowCount: summary.lowCount || 0,
      releaseGate: summary.releaseGate || 'UNKNOWN'
    },
    decision: {
      approved: (summary.criticalCount || 0) === 0 && tierStatus.fast && tierStatus.medium && tierStatus.slow,
      reason: generateDecisionReason(summary, tierStatus)
    }
  };

  return report;
}

/**
 * Format comparison result as Markdown report (human-readable).
 * @param {Object} comparisonResult - Result from RegressionComparisonEngine.compareMetrics()
 * @param {Object} options - Formatting options
 * @returns {string} Markdown report text
 */
export function formatMarkdownReport(comparisonResult, options = {}) {
  const handlerRegressions = comparisonResult.handlerRegressions || [];
  const summary = comparisonResult.summary || {};
  const tierStatus = comparisonResult.tierStatus || {};

  let md = '';

  // Header
  md += '# Regression Test Report\n\n';
  md += `**Generated**: ${new Date().toISOString()}\n\n`;

  // Executive Summary
  const approved = (summary.criticalCount || 0) === 0 && tierStatus.fast && tierStatus.medium && tierStatus.slow;
  const decision = approved ? '✅ **PASS** - Release Approved' : '❌ **BLOCKED** - Release Prohibited';
  md += `## Executive Summary\n\n`;
  md += `${decision}\n\n`;
  md += `| Metric | Value |\n`;
  md += `|--------|-------|\n`;
  md += `| Total Handlers | ${summary.totalHandlers || 0} |\n`;
  md += `| Tested Handlers | ${summary.testedHandlers || 0} |\n`;
  md += `| Passed | ${summary.passedHandlers || 0} |\n`;
  md += `| Regressions | ${summary.regressionCount || 0} |\n`;
  md += `| Critical | ${summary.criticalCount || 0} |\n`;
  md += `| High | ${summary.highCount || 0} |\n`;
  md += `| Medium | ${summary.mediumCount || 0} |\n`;
  md += `| Low | ${summary.lowCount || 0} |\n`;
  md += `| Release Gate | ${summary.releaseGate || 'UNKNOWN'} |\n\n`;

  // Tier Status
  md += `## Tier Status\n\n`;
  md += `| Tier | Status | Decision |\n`;
  md += `|------|--------|----------|\n`;
  md += `| Fast | ${tierStatus.fast ? '✅ PASS' : '❌ FAIL'} | ${tierStatus.fast ? 'Approved' : 'Blocked'} |\n`;
  md += `| Medium | ${tierStatus.medium ? '✅ PASS' : '❌ FAIL'} | ${tierStatus.medium ? 'Approved' : 'Blocked'} |\n`;
  md += `| Slow | ${tierStatus.slow ? '✅ PASS' : '❌ FAIL'} | ${tierStatus.slow ? 'Approved' : 'Blocked'} |\n\n`;

  // Regressions by Severity
  if (handlerRegressions.length > 0) {
    md += `## Regressions by Severity\n\n`;

    const byServerity = {
      CRITICAL: [],
      HIGH: [],
      MEDIUM: [],
      LOW: [],
      NONE: []
    };

    for (const r of handlerRegressions) {
      byServerity[r.overallSeverity].push(r);
    }

    for (const [severity, items] of Object.entries(byServerity)) {
      if (items.length > 0) {
        md += `### ${severity} (${items.length})\n\n`;
        md += `| Handler | Tier | Latency (p99) | Throughput | Memory | Error Rate | Remediation |\n`;
        md += `|---------|------|---------------|-----------|--------|------------|-------------|\n`;

        for (const r of items) {
          const p99Delta = r.metrics.latency?.p99?.deltaPercent?.toFixed(1) || 'N/A';
          const tpDelta = r.metrics.throughput?.deltaPercent?.toFixed(1) || 'N/A';
          const memDelta = r.metrics.memory?.heap?.delta?.toFixed(0) || 'N/A';
          const errDelta = r.metrics.errorRate?.deltaPercent?.toFixed(2) || 'N/A';

          md += `| ${r.handler} | ${r.tier} | ${p99Delta}% | ${tpDelta}% | ${memDelta}MB | ${errDelta}% | ${generateRemediationShort(r)} |\n`;
        }

        md += '\n';
      }
    }
  }

  // Baseline Info
  md += `## Baseline Information\n\n`;
  md += `| Field | Value |\n`;
  md += `|-------|-------|\n`;
  md += `| Baseline Version | ${comparisonResult.baselineVersion || 'unknown'} |\n`;
  md += `| Baseline Timestamp | ${new Date(comparisonResult.baselineTimestamp || 0).toISOString()} |\n`;
  md += `| Current Version | ${options.currentVersion || 'v2.0.0-candidate'} |\n`;
  md += `| Current Timestamp | ${new Date().toISOString()} |\n\n`;

  // Recommendations
  md += `## Recommendations\n\n`;
  if (approved) {
    md += `✅ All gates passed. Release candidate is ready for production deployment.\n`;
  } else {
    md += `❌ Release blocked due to performance regressions. Actions required:\n\n`;
    const criticals = handlerRegressions.filter(r => r.overallSeverity === 'CRITICAL');
    const highs = handlerRegressions.filter(r => r.overallSeverity === 'HIGH');
    const failedTiers = Object.entries(tierStatus).filter(([_, pass]) => !pass).map(([tier]) => tier);

    if (criticals.length > 0) {
      md += `1. **Investigate Critical Regressions**:\n`;
      for (const r of criticals) {
        md += `   - ${r.handler}: ${generateRemediationPath(r)}\n`;
      }
      md += `\n`;
    }

    if (highs.length > 0) {
      md += `2. **Review High-Priority Regressions**:\n`;
      for (const r of highs.slice(0, 3)) {
        md += `   - ${r.handler}: ${generateRemediationPath(r)}\n`;
      }
      md += `\n`;
    }

    if (failedTiers.length > 0) {
      md += `3. **Tier Gate Failures**: ${failedTiers.join(', ')} tier(s) exceeded thresholds\n\n`;
    }

    md += `4. **Next Steps**:\n`;
    md += `   - Profile affected handlers\n`;
    md += `   - Compare against baseline metrics\n`;
    md += `   - Apply fixes and re-run regression tests\n`;
  }

  return md;
}

/**
 * Format CI summary for pipeline dashboards.
 * @param {Object} report - JSON report from formatJSONReport()
 * @returns {Object} CI summary object
 */
export function formatCISummary(report) {
  return {
    exitCode: report.decision.approved ? 0 : 1,
    passed: report.decision.approved,
    reason: report.decision.reason,
    summary: report.summary,
    tierStatus: report.tierStatus,
    releaseGate: report.summary.releaseGate,
    criticalRegressions: report.summary.criticalCount,
    failedTiers: Object.entries(report.tierStatus)
      .filter(([_, pass]) => !pass)
      .map(([tier]) => tier)
  };
}

/**
 * Generate detailed remediation path for a regression.
 * @param {Object} regression - Handler regression object
 * @returns {string} Remediation recommendation text
 */
export function generateRemediationPath(regression) {
  const issues = [];

  // Latency issues
  if (regression.metrics.latency?.p99?.regression) {
    const delta = regression.metrics.latency.p99.deltaPercent.toFixed(1);
    issues.push(`Latency p99 regression (+${delta}%)`);
  }

  // Throughput issues
  if (regression.metrics.throughput?.regression) {
    const delta = Math.abs(regression.metrics.throughput.deltaPercent).toFixed(1);
    issues.push(`Throughput degradation (-${delta}%)`);
  }

  // Memory issues
  if (regression.metrics.memory?.heap?.regression) {
    const delta = regression.metrics.memory.heap.delta.toFixed(0);
    issues.push(`Memory leak (heap +${delta}MB)`);
  }

  // Error rate issues
  if (regression.metrics.errorRate?.regression) {
    const delta = regression.metrics.errorRate.deltaPercent.toFixed(2);
    issues.push(`Error rate spike (+${delta}%)`);
  }

  let remediation = `Address: ${issues.join(', ')}. `;

  // Add handler-specific recommendations
  switch (regression.handler) {
    case 'code-completion':
      remediation += 'Check symbol extraction cache, profile handler execution';
      break;
    case 'search':
      remediation += 'Verify search index integrity, check query optimization';
      break;
    case 'refactor':
      remediation += 'Profile AST transformation, check memory usage in transformation pipeline';
      break;
    case 'apply-edit':
      remediation += 'Verify document mutation performance, check buffer updates';
      break;
    default:
      remediation += 'Profile handler execution, compare against baseline';
  }

  return remediation;
}

/**
 * Generate short remediation text (for tables).
 * @param {Object} regression - Handler regression object
 * @returns {string} Short remediation text
 */
export function generateRemediationShort(regression) {
  if (regression.metrics.latency?.p99?.regression) {
    return 'Profile latency';
  }
  if (regression.metrics.throughput?.regression) {
    return 'Check throughput';
  }
  if (regression.metrics.memory?.heap?.regression) {
    return 'Memory leak';
  }
  if (regression.metrics.errorRate?.regression) {
    return 'Error spike';
  }
  return 'Review metrics';
}

/**
 * Generate decision reason based on summary and tier status.
 * @private
 */
function generateDecisionReason(summary, tierStatus) {
  const reasons = [];

  if ((summary.criticalCount || 0) > 0) {
    reasons.push(`${summary.criticalCount} critical regression(s) detected`);
  }

  if ((summary.highCount || 0) > 0 && !tierStatus.fast) {
    reasons.push('High regressions in fast tier');
  }
  if ((summary.highCount || 0) > 0 && !tierStatus.medium) {
    reasons.push('High regressions in medium tier');
  }
  if ((summary.highCount || 0) > 0 && !tierStatus.slow) {
    reasons.push('High regressions in slow tier');
  }

  if (reasons.length === 0) {
    return 'All performance gates passed, release approved';
  }

  return reasons.join('; ');
}

/**
 * Stringify JSON report for file persistence.
 * @param {Object} report - JSON report object
 * @param {number} [indent] - JSON indentation (default 2)
 * @returns {string} JSON string
 */
export function stringifyReport(report, indent = 2) {
  return JSON.stringify(report, null, indent);
}

/**
 * Parse JSON report from string.
 * @param {string} json - JSON string
 * @returns {Object} Parsed report object
 */
export function parseReport(json) {
  return JSON.parse(json);
}
