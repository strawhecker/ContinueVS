/**
 * Crash Recovery Manager
 * 
 * Main orchestrator for bridge crash recovery:
 * - Monitors bridge health using HealthCheckService
 * - Captures diagnostic state when crashes occur
 * - Persists recovery metadata
 * - Implements recovery strategies (auto-restart, graceful shutdown, degraded mode)
 * - Emits recovery events for external consumers (lifecycle manager, error recovery middleware)
 */

import {
  CrashRecoveryState,
  CrashMetadata,
  HandlerStateSnapshot,
  createCrashStateFromError,
} from './crash-recovery-state.mjs';
import {
  CrashDiagnosticsCollector,
  DiagnosticSnapshot,
} from './crash-diagnostics.mjs';
import { promises as fs } from 'fs';
import { resolve } from 'path';
import { homedir } from 'os';

/**
 * Error class for crash recovery manager operations
 */
export class CrashRecoveryManagerError extends Error {
  constructor(message, originalError = null) {
    super(message);
    this.name = 'CrashRecoveryManagerError';
    this.originalError = originalError;
  }
}

/**
 * Crash Recovery Manager - Main orchestrator
 */
export class CrashRecoveryManager {
  constructor({
    healthCheckService = null,
    logger = null,
    metrics = null,
  } = {}) {
    this.healthCheckService = healthCheckService;
    this.logger = logger;
    this.metrics = metrics;
    this.diagnosticsCollector = new CrashDiagnosticsCollector({ logger, metrics });
    this.recoveryStateFile = resolve(homedir(), '.continue', 'crash-recovery.json');
    this.recoveryState = new CrashRecoveryState();
    this.isInitialized = false;
    this.crashDetectTimeout = 5000; // 5 seconds
    this.persistenceTimeout = 1000; // 1 second
    this.recoveryTimeout = 10000; // 10 seconds
    this.recoveryListeners = [];
    this.healthCheckUnsubscriber = null;
  }

  /**
   * Initialize crash recovery manager
   */
  async initialize() {
    try {
      // Initialize diagnostics collector
      await this.diagnosticsCollector.initialize();

      // Load existing recovery state if available
      await this._loadRecoveryState();

      // Register health check monitoring if available
      if (this.healthCheckService && typeof this.healthCheckService.on === 'function') {
        this.healthCheckUnsubscriber = this.healthCheckService.on(
          'health-check-failed',
          (error) => this._onHealthCheckFailed(error)
        );
        this._logDebug('Health check monitoring registered');
      }

      this.isInitialized = true;
      this._logDebug('Crash recovery manager initialized');
      this._recordMetric('crash_recovery.initialized', 1);

      return true;
    } catch (error) {
      this._logError(`Initialization failed: ${error.message}`);
      throw new CrashRecoveryManagerError(
        `Failed to initialize crash recovery manager: ${error.message}`,
        error
      );
    }
  }

  /**
   * Dispose crash recovery manager and cleanup resources
   */
  async dispose() {
    try {
      // Unsubscribe from health check events
      if (this.healthCheckUnsubscriber && typeof this.healthCheckUnsubscriber === 'function') {
        this.healthCheckUnsubscriber();
      }

      // Clear recovery listeners
      this.recoveryListeners = [];

      this.isInitialized = false;
      this._logDebug('Crash recovery manager disposed');
      this._recordMetric('crash_recovery.disposed', 1);

      return true;
    } catch (error) {
      this._logError(`Disposal failed: ${error.message}`);
      throw new CrashRecoveryManagerError(
        `Failed to dispose crash recovery manager: ${error.message}`,
        error
      );
    }
  }

  /**
   * Handle health check failure (crash detection)
   */
  async _onHealthCheckFailed(error) {
    try {
      this._logDebug('Health check failure detected - initiating crash recovery');
      this._recordMetric('crash_recovery.health_check_failed', 1);

      const crashError = error || new Error('Health check failed - bridge unresponsive');
      this.recoveryState = createCrashStateFromError(crashError, {
        crashType: 'health_check_failure',
        bridgeVersion: process.env.BRIDGE_VERSION || 'unknown',
      });

      // Record crash metadata
      await this._captureCrashDiagnostics();

      // Execute recovery workflow
      await this._executeRecoveryWorkflow();
    } catch (error) {
      this._logError(`Crash handling failed: ${error.message}`);
    }
  }

  /**
   * Capture crash diagnostics
   */
  async _captureCrashDiagnostics() {
    try {
      const startTime = Date.now();

      const diagnosticResult = await this.diagnosticsCollector.captureAndPersist({
        bridgeVersion: process.env.BRIDGE_VERSION || 'unknown',
        handlerRegistry: null, // Would be injected from bridge
        bridgeLogger: this.logger,
        bridgeState: this.recoveryState.toJSON(),
        contextInfo: {
          userId: process.env.USER_ID || 'unknown',
          crashTime: new Date().toISOString(),
        },
      });

      const duration = Date.now() - startTime;
      if (duration > this.persistenceTimeout) {
        this._logDebug(`Warning: Diagnostics capture took ${duration}ms (threshold: ${this.persistenceTimeout}ms)`);
      }

      // Update recovery state with diagnostics path
      if (this.recoveryState.crashMetadata) {
        this.recoveryState.crashMetadata.diagnosticsPath = diagnosticResult.reportPath;
      }

      this._recordMetric('crash_recovery.diagnostics_captured', duration);
      this._logDebug(`Crash diagnostics captured in ${duration}ms`);

      return diagnosticResult;
    } catch (error) {
      this._logError(`Failed to capture crash diagnostics: ${error.message}`);
      // Continue recovery even if diagnostics fail
      return null;
    }
  }

  /**
   * Execute recovery workflow based on recovery state
   */
  async _executeRecoveryWorkflow() {
    try {
      const startTime = Date.now();

      // Record recovery attempt
      this.recoveryState.recordRecoveryAttempt();

      // Determine recovery strategy
      const strategy = this._determineRecoveryStrategy();
      this.recoveryState.recoveryStrategy = strategy;

      // Persist recovery state
      await this._persistRecoveryState();

      // Execute recovery action based on strategy
      let recoveryResult = null;
      switch (strategy) {
        case 'auto-restart':
          recoveryResult = await this._executeAutoRestart();
          break;
        case 'graceful-shutdown':
          recoveryResult = await this._executeGracefulShutdown();
          break;
        case 'degraded-mode':
          recoveryResult = await this._executeDegradedMode();
          break;
        default:
          this._logError(`Unknown recovery strategy: ${strategy}`);
          recoveryResult = { success: false, strategy };
      }

      const duration = Date.now() - startTime;
      if (duration > this.recoveryTimeout) {
        this._logDebug(`Warning: Recovery workflow took ${duration}ms (threshold: ${this.recoveryTimeout}ms)`);
      }

      // Emit recovery event
      this._emitRecoveryEvent({
        timestamp: Date.now(),
        strategy,
        success: recoveryResult.success,
        duration,
        reason: recoveryResult.reason || null,
      });

      this._recordMetric('crash_recovery.recovery_executed', 1);
      this._recordMetric('crash_recovery.recovery_duration_ms', duration);

      return recoveryResult;
    } catch (error) {
      this._logError(`Recovery workflow failed: ${error.message}`);
      this._recordMetric('crash_recovery.recovery_failed', 1);

      // Emit failure event
      this._emitRecoveryEvent({
        timestamp: Date.now(),
        strategy: this.recoveryState.recoveryStrategy,
        success: false,
        error: error.message,
      });

      throw new CrashRecoveryManagerError(
        `Recovery workflow failed: ${error.message}`,
        error
      );
    }
  }

  /**
   * Determine appropriate recovery strategy
   */
  _determineRecoveryStrategy() {
    // Check if should attempt recovery
    if (!this.recoveryState.shouldAttemptRecovery()) {
      return 'degraded-mode';
    }

    // Check retry count for escalation
    if (this.recoveryState.recoveryAttempts > 2) {
      return 'graceful-shutdown';
    }

    // Default to auto-restart for first attempts
    return 'auto-restart';
  }

  /**
   * Execute auto-restart recovery strategy
   */
  async _executeAutoRestart() {
    try {
      this._logDebug('Executing auto-restart recovery strategy');
      // Send signal to parent process to restart bridge
      process.emit('message', {
        type: 'bridge:request-restart',
        reason: 'crash-recovery',
        diagnosticsPath: this.recoveryState.crashMetadata?.diagnosticsPath,
      });
      return { success: true, strategy: 'auto-restart' };
    } catch (error) {
      this._logError(`Auto-restart failed: ${error.message}`);
      return { success: false, strategy: 'auto-restart', reason: error.message };
    }
  }

  /**
   * Execute graceful shutdown recovery strategy
   */
  async _executeGracefulShutdown() {
    try {
      this._logDebug('Executing graceful shutdown recovery strategy');
      // Send signal to parent process to perform graceful shutdown
      process.emit('message', {
        type: 'bridge:request-shutdown',
        reason: 'crash-recovery-shutdown',
        diagnosticsPath: this.recoveryState.crashMetadata?.diagnosticsPath,
      });
      return { success: true, strategy: 'graceful-shutdown' };
    } catch (error) {
      this._logError(`Graceful shutdown failed: ${error.message}`);
      return { success: false, strategy: 'graceful-shutdown', reason: error.message };
    }
  }

  /**
   * Execute degraded mode recovery strategy
   */
  async _executeDegradedMode() {
    try {
      this._logDebug('Executing degraded mode recovery strategy');
      // Signal to parent process that bridge is in degraded mode
      process.emit('message', {
        type: 'bridge:enter-degraded-mode',
        reason: 'crash-recovery-degraded',
        diagnosticsPath: this.recoveryState.crashMetadata?.diagnosticsPath,
      });
      return { success: true, strategy: 'degraded-mode' };
    } catch (error) {
      this._logError(`Degraded mode entry failed: ${error.message}`);
      return { success: false, strategy: 'degraded-mode', reason: error.message };
    }
  }

  /**
   * Register recovery event listener
   */
  onRecoveryEvent(callback) {
    if (typeof callback === 'function') {
      this.recoveryListeners.push(callback);
    }
  }

  /**
   * Emit recovery event to all listeners
   */
  _emitRecoveryEvent(event) {
    for (const listener of this.recoveryListeners) {
      try {
        listener(event);
      } catch (error) {
        this._logError(`Recovery listener error: ${error.message}`);
      }
    }
  }

  /**
   * Persist recovery state to file
   */
  async _persistRecoveryState() {
    try {
      const startTime = Date.now();
      this.recoveryState.validate();

      const dir = resolve(homedir(), '.continue');
      await fs.mkdir(dir, { recursive: true });

      await fs.writeFile(
        this.recoveryStateFile,
        JSON.stringify(this.recoveryState.toJSON(), null, 2),
        'utf-8'
      );

      const duration = Date.now() - startTime;
      if (duration > this.persistenceTimeout) {
        this._logDebug(`Warning: State persistence took ${duration}ms (threshold: ${this.persistenceTimeout}ms)`);
      }

      this._recordMetric('crash_recovery.state_persisted', 1);
    } catch (error) {
      this._logError(`Failed to persist recovery state: ${error.message}`);
      throw error;
    }
  }

  /**
   * Load recovery state from file
   */
  async _loadRecoveryState() {
    try {
      const data = await fs.readFile(this.recoveryStateFile, 'utf-8');
      const parsedData = JSON.parse(data);
      this.recoveryState = CrashRecoveryState.fromJSON(parsedData);
      this._logDebug('Recovery state loaded from file');
    } catch (error) {
      // File doesn't exist or is invalid - start fresh
      this.recoveryState = new CrashRecoveryState();
      this._logDebug('Starting with empty recovery state');
    }
  }

  /**
   * Reset recovery state after successful recovery
   */
  async resetRecoveryState() {
    try {
      this.recoveryState.resetRecoveryState();
      await this._persistRecoveryState();
      this._logDebug('Recovery state reset');
      this._recordMetric('crash_recovery.state_reset', 1);
      return true;
    } catch (error) {
      this._logError(`Failed to reset recovery state: ${error.message}`);
      throw new CrashRecoveryManagerError(
        `Failed to reset recovery state: ${error.message}`,
        error
      );
    }
  }

  /**
   * Get current recovery state
   */
  getRecoveryState() {
    return this.recoveryState;
  }

  /**
   * Log debug message
   */
  _logDebug(message) {
    if (this.logger && typeof this.logger.debug === 'function') {
      this.logger.debug(message);
    }
  }

  /**
   * Log error message
   */
  _logError(message) {
    if (this.logger && typeof this.logger.error === 'function') {
      this.logger.error(message);
    }
  }

  /**
   * Record metric
   */
  _recordMetric(name, value) {
    if (this.metrics && typeof this.metrics.record === 'function') {
      this.metrics.record(name, value);
    }
  }
}

/**
 * Factory function to create crash recovery manager
 */
export function createCrashRecoveryManager(options = {}) {
  return new CrashRecoveryManager(options);
}

/**
 * Factory function to create crash recovery handler for dispatcher
 */
export function createCrashRecoveryHandler(crashRecoveryManager) {
  return async (message, send) => {
    try {
      const recoveryState = crashRecoveryManager.getRecoveryState();
      send({
        jsonrpc: '2.0',
        id: message.id,
        result: {
          recoveryState: recoveryState.toJSON(),
          isRecoverable: recoveryState.isRecoverable(),
          shouldAttemptRecovery: recoveryState.shouldAttemptRecovery(),
        },
      });
    } catch (error) {
      send({
        jsonrpc: '2.0',
        id: message.id,
        error: {
          code: -32603,
          message: 'Internal error',
          data: { originalMessage: error.message },
        },
      });
    }
  };
}
