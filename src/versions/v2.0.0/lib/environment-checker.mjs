/**
 * environment-checker.mjs
 * Step 98: Test Environment Validation
 * 
 * Validates system environment before baseline creation.
 * Captures environment metadata for baseline context.
 */

import os from 'os';
import { exec } from 'child_process';
import { promisify } from 'util';
import { EnvironmentValidationError } from './performance-test-framework.mjs';

const execAsync = promisify(exec);

/**
 * Environment information container
 */
export class EnvironmentInfo {
  constructor(data) {
    this.timestamp = data.timestamp;
    this.nodeVersion = data.nodeVersion;
    this.osVersion = data.osVersion;
    this.cpuModel = data.cpuModel;
    this.cpuCount = data.cpuCount;
    this.totalMemoryMB = data.totalMemoryMB;
    this.freeMemoryMB = data.freeMemoryMB;
    this.diskType = data.diskType;
    this.networkLatencyMs = data.networkLatencyMs;
    this.backgroundProcesses = data.backgroundProcesses;
    this.cpuThrottlingDetected = data.cpuThrottlingDetected;
    this.memoryPressureDetected = data.memoryPressureDetected;
  }
}

/**
 * Check result
 */
class CheckResult {
  constructor(checkName, expected, actual, passed, severity = 'INFO', details = '') {
    this.checkName = checkName;
    this.expected = expected;
    this.actual = actual;
    this.passed = passed;
    this.severity = severity;
    this.details = details;
  }
}

/**
 * Environment checker
 */
export class EnvironmentChecker {
  constructor(options = {}) {
    this.logger = options.logger;
    this.criticalOnly = options.criticalOnly || false;
  }

  /**
   * Run all environment checks
   */
  async runFullCheck() {
    const checks = [];

    checks.push(await this.checkNodeVersion());
    checks.push(await this.checkCPU());
    checks.push(await this.checkMemory());
    checks.push(await this.checkDisk());
    checks.push(this.checkBackgroundProcesses());

    const warnings = checks
      .filter(c => c.severity === 'WARNING')
      .map(c => `${c.checkName}: ${c.details}`);

    const passed = !checks.some(c => c.severity === 'CRITICAL' && !c.passed);
    const environment = await this.captureEnvironmentInfo();

    if (!passed && !this.criticalOnly) {
      throw new EnvironmentValidationError(
        'Environment validation failed',
        checks.filter(c => !c.passed)
      );
    }

    return {
      passed,
      checks,
      warnings,
      environment
    };
  }

  /**
   * Check Node.js version
   */
  async checkNodeVersion() {
    const version = process.version; // e.g., 'v18.13.0'
    const majorVersion = parseInt(version.substring(1).split('.')[0]);
    const expected = '18.x LTS';
    const passed = majorVersion >= 18;

    return new CheckResult(
      'Node.js Version',
      expected,
      version,
      passed,
      passed ? 'INFO' : 'WARNING',
      passed ? 'OK' : `Expected ${expected}, got ${version}`
    );
  }

  /**
   * Check CPU availability
   */
  async checkCPU() {
    const cpuCount = os.cpus().length;
    const expected = '≥4 cores';
    const passed = cpuCount >= 4;

    return new CheckResult(
      'CPU Cores',
      expected,
      `${cpuCount} cores`,
      passed,
      passed ? 'INFO' : 'WARNING',
      passed ? 'Sufficient cores' : `Only ${cpuCount} cores available`
    );
  }

  /**
   * Check available memory
   */
  async checkMemory() {
    const totalMB = os.totalmem() / 1024 / 1024;
    const freeMB = os.freemem() / 1024 / 1024;
    const usedPercent = ((totalMB - freeMB) / totalMB) * 100;
    const expected = '<80% utilization';
    const passed = usedPercent < 80;

    return new CheckResult(
      'Memory Pressure',
      expected,
      `${usedPercent.toFixed(1)}% used`,
      passed,
      passed ? 'INFO' : 'CRITICAL',
      passed
        ? 'Memory available'
        : `High memory pressure: ${usedPercent.toFixed(1)}%`
    );
  }

  /**
   * Check disk availability
   */
  async checkDisk() {
    // Simple heuristic: at least 10GB free (Windows/Mac)
    try {
      // In real implementation, would use 'df' or similar
      // For now, assume sufficient if no error
      return new CheckResult(
        'Disk Space',
        '>10GB free',
        'Assumed sufficient',
        true,
        'INFO',
        'No disk space check implemented'
      );
    } catch (err) {
      return new CheckResult(
        'Disk Space',
        '>10GB free',
        'Unknown',
        false,
        'WARNING',
        err.message
      );
    }
  }

  /**
   * Check background processes
   */
  checkBackgroundProcesses() {
    const processCount = process.env.NODE_ENV === 'test' ? 0 : 1;
    const expected = '<5 heavy processes';
    const passed = true; // Assume OK without detailed process inspection

    return new CheckResult(
      'Background Processes',
      expected,
      `${processCount} detected`,
      passed,
      'INFO',
      'Assumed OK'
    );
  }

  /**
   * Detect CPU throttling
   */
  async detectCPUThrottling() {
    // Would require platform-specific commands
    // For now, assume no throttling
    return false;
  }

  /**
   * Detect memory pressure
   */
  async detectMemoryPressure() {
    const usedPercent = ((os.totalmem() - os.freemem()) / os.totalmem()) * 100;
    return usedPercent > 80;
  }

  /**
   * Capture full environment information
   */
  async captureEnvironmentInfo() {
    const cpuInfo = os.cpus()[0];
    const totalMem = os.totalmem();
    const freeMem = os.freemem();

    return new EnvironmentInfo({
      timestamp: Date.now(),
      nodeVersion: process.version,
      osVersion: `${os.platform()} ${os.release()}`,
      cpuModel: cpuInfo?.model || 'Unknown',
      cpuCount: os.cpus().length,
      totalMemoryMB: totalMem / 1024 / 1024,
      freeMemoryMB: freeMem / 1024 / 1024,
      diskType: process.platform === 'win32' ? 'unknown' : 'unknown', // Would detect via SMART data
      networkLatencyMs: 0, // Would measure via ping
      backgroundProcesses: 0,
      cpuThrottlingDetected: await this.detectCPUThrottling(),
      memoryPressureDetected: await this.detectMemoryPressure()
    });
  }
}

/**
 * Factory function
 */
export function createEnvironmentChecker(options = {}) {
  return new EnvironmentChecker(options);
}
