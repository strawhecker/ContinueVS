/**
 * performance-report.mjs
 * Step 98: Performance Report Generation
 * 
 * Generate structured reports in JSON, Markdown, and CSV formats.
 * Includes CI/CD integration with exit codes.
 */

import fs from 'fs';
import path from 'path';

/**
 * Performance report container
 */
export class PerformanceReport {
  constructor(results = {}) {
    this.handlers = results.handlers || [];
    this.timestamp = results.timestamp || Date.now();
    this.environment = results.environment || {};
    this.regression = results.regression || null;
    this.regressions = results.regressions || [];
  }

  toJSON() {
    return {
      timestamp: this.timestamp,
      environment: this.environment,
      handlers: this.handlers,
      regression: this.regression,
      summary: this._generateSummary()
    };
  }

  toMarkdown() {
    const date = new Date(this.timestamp).toISOString();
    let md = `# Performance Baseline Report v2.0.0\n\n`;
    md += `**Generated**: ${date}\n`;
    md += `**Environment**: ${this.environment.osVersion || 'Unknown'}, ${this.environment.cpuModel || 'Unknown'}, ${this.environment.totalMemoryMB || 0}MB RAM\n\n`;

    const summary = this._generateSummary();
    md += `## Executive Summary\n`;
    md += `- **Total Handlers**: ${summary.totalHandlers}\n`;
    md += `- **Passed**: ${summary.passedHandlers}/${summary.totalHandlers}\n`;
    md += `- **Failed**: ${summary.failedCount}\n`;
    md += `- **Performance Budget**: ${summary.budgetUsed}% consumed\n\n`;

    md += `## Per-Handler Results\n\n`;
    md += `| Handler | Tier | p50 | p95 | p99 | Throughput | Memory | Gate |\n`;
    md += `|---------|------|-----|-----|-----|-----------|--------|------|\n`;

    for (const handler of this.handlers) {
      const p50 = handler.latency?.p50?.toFixed(2) || 'N/A';
      const p95 = handler.latency?.p95?.toFixed(2) || 'N/A';
      const p99 = handler.latency?.p99?.toFixed(2) || 'N/A';
      const tp = handler.throughput?.messagesPerSecond?.toFixed(0) || 'N/A';
      const mem = handler.memory?.deltaMB?.toFixed(1) || 'N/A';
      const gateStatus = handler.passed ? '✅ PASS' : '❌ FAIL';

      md += `| ${handler.name} | ${handler.tier} | ${p50}ms | ${p95}ms | ${p99}ms | ${tp} msg/s | ${mem}MB | ${gateStatus} |\n`;
    }

    md += `\n## Recommendations\n`;
    if (this.regression?.summary?.recommendation) {
      md += `- ${this.regression.summary.recommendation}\n`;
    } else {
      md += `- No immediate action required\n`;
    }

    const failedHandlers = this.handlers.filter(h => !h.passed);
    if (failedHandlers.length > 0) {
      md += `\n### Failed Handlers\n`;
      for (const handler of failedHandlers) {
        md += `- **${handler.name}**: p99 ${handler.latency?.p99?.toFixed(2)}ms (gate: ${handler.gate?.p99Max}ms)\n`;
      }
    }

    return md;
  }

  toCSV() {
    let csv = `Handler,Tier,p50,p95,p99,Throughput,Memory,Gate Status\n`;

    for (const handler of this.handlers) {
      const p50 = handler.latency?.p50 || '';
      const p95 = handler.latency?.p95 || '';
      const p99 = handler.latency?.p99 || '';
      const tp = handler.throughput?.messagesPerSecond || '';
      const mem = handler.memory?.deltaMB || '';
      const gateStatus = handler.passed ? 'PASS' : 'FAIL';

      csv += `${handler.name},${handler.tier},${p50},${p95},${p99},${tp},${mem},${gateStatus}\n`;
    }

    return csv;
  }

  toCICDSummary() {
    const failedCount = this.handlers.filter(h => !h.passed).length;
    const passed = failedCount === 0;

    return {
      passed,
      exitCode: passed ? 0 : 1,
      failedHandlers: this.handlers
        .filter(h => !h.passed)
        .map(h => `${h.name}: p99 ${h.latency?.p99?.toFixed(2)}ms (gate: ${h.gate?.p99Max}ms)`),
      gateStatus: passed ? 'PASS' : 'FAIL',
      summary: `${this.handlers.filter(h => h.passed).length}/${this.handlers.length} handlers pass SLA`,
      regressions: this.regression?.handlerRegressions?.slice(0, 5)
        .map(r => `${r.handler}: +${r.p99.regressionPercent}%`) || []
    };
  }

  _generateSummary() {
    const passedHandlers = this.handlers.filter(h => h.passed).length;
    const totalHandlers = this.handlers.length;
    const failedCount = totalHandlers - passedHandlers;

    return {
      totalHandlers,
      passedHandlers,
      failedCount,
      budgetUsed: Math.min(100, Math.round((failedCount / totalHandlers) * 100))
    };
  }
}

/**
 * Generate performance report from measurements
 */
export function generatePerformanceReport(measurements, metadata = {}) {
  const handlers = [];

  for (const [name, metrics] of Object.entries(measurements)) {
    handlers.push({
      name,
      tier: metadata.tiers?.[name] || 'unknown',
      latency: metrics.latency || {},
      throughput: metrics.throughput || {},
      memory: metrics.memory || {},
      passed: metrics.passed !== false,
      gate: metadata.gates?.[name]
    });
  }

  return new PerformanceReport({
    handlers,
    timestamp: Date.now(),
    environment: metadata.environment,
    regression: metadata.regression
  });
}

/**
 * Export report to file
 */
export async function exportReportToFile(report, format = 'json', filepath = null) {
  let content;
  let extension;

  switch (format.toLowerCase()) {
    case 'json':
      content = JSON.stringify(report.toJSON(), null, 2);
      extension = '.json';
      break;
    case 'markdown':
    case 'md':
      content = report.toMarkdown();
      extension = '.md';
      break;
    case 'csv':
      content = report.toCSV();
      extension = '.csv';
      break;
    default:
      throw new Error(`Unsupported format: ${format}`);
  }

  if (!filepath) {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').substring(0, 19);
    filepath = path.join(
      process.cwd(),
      'performance-reports',
      `performance-report-${timestamp}${extension}`
    );
  }

  // Ensure directory exists
  const dir = path.dirname(filepath);
  await fs.promises.mkdir(dir, { recursive: true });

  // Write file
  await fs.promises.writeFile(filepath, content, 'utf-8');

  return filepath;
}

/**
 * Generate CI/CD summary and exit code
 */
export function generateCICDSummary(report, gateConfig = {}) {
  const cicdSummary = report.toCICDSummary();

  return {
    passed: cicdSummary.passed,
    exitCode: cicdSummary.exitCode,
    gateStatus: cicdSummary.gateStatus,
    summary: cicdSummary.summary,
    failedHandlers: cicdSummary.failedHandlers,
    regressions: cicdSummary.regressions,
    failureReasons: cicdSummary.failedHandlers.length > 0
      ? ['Performance SLA violations detected']
      : [],
    recommendations: cicdSummary.gateStatus === 'FAIL'
      ? ['Review failed handlers', 'Run profiler for detailed analysis']
      : []
  };
}
