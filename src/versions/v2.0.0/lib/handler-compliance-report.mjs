#!/usr/bin/env node

/**
 * Handler Compliance Report Generator
 *
 * Generates structured compliance reports from test results for audit trail
 * and CI/CD integration.
 *
 * @module src/versions/v2.0.0/lib/handler-compliance-report.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Usage:
 *   const report = generateComplianceReport(testResults);
 *   console.log(JSON.stringify(report, null, 2));
 */

/**
 * Generate compliance report from test results
 *
 * @param {Array} testResults - Array of test result objects
 * @param {Object} options - Report options {format: 'json'|'md', includeTimeline: boolean}
 * @returns {Object|string} - Compliance report (JSON or markdown)
 */
export function generateComplianceReport(testResults = [], options = {}) {
  const format = options.format || 'json';
  const includeTimeline = options.includeTimeline !== false;

  // Group results by handler
  const handlerResults = groupResultsByHandler(testResults);

  // Compute summary statistics
  const summary = computeSummary(handlerResults);

  // Analyze per-handler status
  const handlers = analyzeHandlers(handlerResults);

  // Generate recommendations
  const recommendations = generateRecommendations(handlers);

  // Build report
  const report = {
    metadata: {
      version: '1.0.0',
      generatedAt: new Date().toISOString(),
      format: 'Handler Compliance Report',
    },
    summary,
    handlers,
    recommendations,
  };

  if (includeTimeline) {
    report.timeline = {
      startTime: new Date().toISOString(),
      endTime: new Date().toISOString(),
      duration: 'See individual test execution times',
    };
  }

  // Format output
  if (format === 'md') {
    return reportToMarkdown(report);
  }

  return report;
}

/**
 * Group test results by handler name
 */
function groupResultsByHandler(testResults) {
  const grouped = {};

  for (const result of testResults) {
    const handlerName = result.handlerName || 'unknown';
    if (!grouped[handlerName]) {
      grouped[handlerName] = [];
    }
    grouped[handlerName].push(result);
  }

  return grouped;
}

/**
 * Compute summary statistics
 */
function computeSummary(handlerResults) {
  const summary = {
    totalHandlers: Object.keys(handlerResults).length,
    passed: 0,
    failed: 0,
    partialPass: 0,
    totalTests: 0,
    totalPassed: 0,
    totalFailed: 0,
    passRate: 0,
    warnings: [],
  };

  for (const [, results] of Object.entries(handlerResults)) {
    const passed = results.filter((r) => r.passed).length;
    const failed = results.filter((r) => !r.passed && r.error).length;
    const total = results.length;

    summary.totalTests += total;
    summary.totalPassed += passed;
    summary.totalFailed += failed;

    if (failed > 0) {
      summary.failed++;
    } else if (passed === total) {
      summary.passed++;
    } else {
      summary.partialPass++;
    }
  }

  if (summary.totalTests > 0) {
    summary.passRate = Math.round(
      (summary.totalPassed / summary.totalTests) * 100
    );
  }

  return summary;
}

/**
 * Analyze per-handler status
 */
function analyzeHandlers(handlerResults) {
  const handlers = [];

  for (const [handlerName, results] of Object.entries(handlerResults)) {
    const passed = results.filter((r) => r.passed).length;
    const failed = results.filter((r) => !r.passed && r.error).length;
    const total = results.length;
    const passRate = total > 0 ? Math.round((passed / total) * 100) : 0;

    let status = 'pass';
    if (failed > 0) {
      status = 'fail';
    } else if (passed < total) {
      status = 'partialPass';
    }

    const handlerReport = {
      name: handlerName,
      status,
      testsPassed: passed,
      testsFailed: failed,
      testsTotal: total,
      passRate,
      tests: results.map((r) => ({
        requirement: r.requirement,
        passed: r.passed,
        error: r.error || null,
        warning: r.warning || null,
      })),
      warnings: results
        .filter((r) => r.warning)
        .map((r) => ({ requirement: r.requirement, warning: r.warning })),
    };

    handlers.push(handlerReport);
  }

  // Sort by name for consistent output
  handlers.sort((a, b) => a.name.localeCompare(b.name));

  return handlers;
}

/**
 * Generate recommendations for failing handlers
 */
function generateRecommendations(handlers) {
  const recommendations = [];

  for (const handler of handlers) {
    if (handler.status === 'fail') {
      const failedTests = handler.tests.filter((t) => !t.passed);

      recommendations.push({
        handlerName: handler.name,
        severity: 'high',
        action: 'Fix failing compliance tests',
        details: {
          failingTests: failedTests.length,
          tests: failedTests.map((t) => ({
            requirement: t.requirement,
            error: t.error,
          })),
          nextSteps: [
            'Review failing test requirements',
            'Check handler implementation against contract',
            'Verify message schema matches expected format',
            'Check error codes are JSON-RPC standard',
            'Ensure middleware integration is correct',
          ],
        },
      });
    } else if (handler.status === 'partialPass') {
      recommendations.push({
        handlerName: handler.name,
        severity: 'medium',
        action: 'Review partial pass tests',
        details: {
          passRate: handler.passRate,
          nextSteps: [
            'Investigate failing test cases',
            'Ensure consistent contract compliance',
          ],
        },
      });
    } else if (handler.warnings.length > 0) {
      recommendations.push({
        handlerName: handler.name,
        severity: 'low',
        action: 'Address compliance warnings',
        details: {
          warnings: handler.warnings,
          nextSteps: ['Review warning details for potential improvements'],
        },
      });
    }
  }

  return recommendations;
}

/**
 * Convert report to markdown format
 */
function reportToMarkdown(report) {
  let md = '# Handler Compliance Report\n\n';

  md += `**Generated**: ${report.metadata.generatedAt}\n\n`;

  // Summary section
  md += '## Summary\n\n';
  md += `| Metric | Value |\n`;
  md += `|--------|-------|\n`;
  md += `| Total Handlers | ${report.summary.totalHandlers} |\n`;
  md += `| Passed | ${report.summary.passed} |\n`;
  md += `| Failed | ${report.summary.failed} |\n`;
  md += `| Partial Pass | ${report.summary.partialPass} |\n`;
  md += `| Total Tests | ${report.summary.totalTests} |\n`;
  md += `| Pass Rate | ${report.summary.passRate}% |\n\n`;

  // Per-handler section
  md += '## Handler Status\n\n';
  md += `| Handler | Status | Tests Passed | Pass Rate |\n`;
  md += `|---------|--------|--------------|----------|\n`;

  for (const handler of report.handlers) {
    const statusEmoji =
      handler.status === 'pass' ? '✅' : handler.status === 'fail' ? '❌' : '⚠️';
    md += `| ${handler.name} | ${statusEmoji} ${handler.status} | ${handler.testsPassed}/${handler.testsTotal} | ${handler.passRate}% |\n`;
  }

  md += '\n';

  // Recommendations section
  if (report.recommendations.length > 0) {
    md += '## Recommendations\n\n';

    for (const rec of report.recommendations) {
      const severityBadge =
        rec.severity === 'high' ? '🔴' : rec.severity === 'medium' ? '🟡' : '🟢';
      md += `### ${severityBadge} ${rec.handlerName}\n\n`;
      md += `**Action**: ${rec.action}\n\n`;

      if (rec.details.failingTests) {
        md += `**Failing Tests**: ${rec.details.failingTests}\n\n`;
      }

      if (rec.details.nextSteps) {
        md += '**Next Steps**:\n';
        for (const step of rec.details.nextSteps) {
          md += `- ${step}\n`;
        }
        md += '\n';
      }
    }
  }

  return md;
}

/**
 * Export report to JSON file
 */
export async function exportReportToFile(report, filePath) {
  const fs = await import('fs').then((m) => m.promises);
  await fs.writeFile(filePath, JSON.stringify(report, null, 2), 'utf8');
}

/**
 * Create a summary report for CI/CD integration
 */
export function createCICDSummary(report) {
  return {
    status: report.summary.failed === 0 ? 'pass' : 'fail',
    totalHandlers: report.summary.totalHandlers,
    passedHandlers: report.summary.passed,
    failedHandlers: report.summary.failed,
    passRate: report.summary.passRate,
    criticalIssues: report.recommendations
      .filter((r) => r.severity === 'high')
      .map((r) => r.handlerName),
  };
}

export default {
  generateComplianceReport,
  exportReportToFile,
  createCICDSummary,
};
