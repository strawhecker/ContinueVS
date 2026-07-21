/**
 * Crash Recovery State Model
 * 
 * Manages persistent state for bridge crash recovery, including:
 * - Recovery metadata (crash timestamp, type, last successful message)
 * - Handler state snapshots (active handlers, pending requests, cache state)
 * - Schema validation and migration for version upgrades
 */

/**
 * Error class for crash recovery state operations
 */
export class CrashRecoveryStateError extends Error {
  constructor(message, originalError = null) {
    super(message);
    this.name = 'CrashRecoveryStateError';
    this.originalError = originalError;
  }
}

/**
 * Represents a single crash event with metadata
 */
export class CrashMetadata {
  constructor({
    timestamp = Date.now(),
    crashType = 'unknown', // 'health_check_failure', 'process_exit', 'unresponsive', 'unknown'
    bridgeVersion = null,
    lastSuccessfulMessageId = null,
    errorTrace = null,
    diagnosticsPath = null,
  } = {}) {
    this.timestamp = timestamp;
    this.crashType = crashType;
    this.bridgeVersion = bridgeVersion;
    this.lastSuccessfulMessageId = lastSuccessfulMessageId;
    this.errorTrace = errorTrace;
    this.diagnosticsPath = diagnosticsPath;
  }

  /**
   * Validate crash metadata schema
   */
  validate() {
    if (!Number.isInteger(this.timestamp) || this.timestamp <= 0) {
      throw new CrashRecoveryStateError('Invalid timestamp: must be positive integer');
    }
    if (typeof this.crashType !== 'string' || this.crashType.length === 0) {
      throw new CrashRecoveryStateError('Invalid crashType: must be non-empty string');
    }
    if (this.bridgeVersion !== null && typeof this.bridgeVersion !== 'string') {
      throw new CrashRecoveryStateError('Invalid bridgeVersion: must be string or null');
    }
    if (this.lastSuccessfulMessageId !== null && typeof this.lastSuccessfulMessageId !== 'string') {
      throw new CrashRecoveryStateError('Invalid lastSuccessfulMessageId: must be string or null');
    }
    if (this.errorTrace !== null && typeof this.errorTrace !== 'string') {
      throw new CrashRecoveryStateError('Invalid errorTrace: must be string or null');
    }
    if (this.diagnosticsPath !== null && typeof this.diagnosticsPath !== 'string') {
      throw new CrashRecoveryStateError('Invalid diagnosticsPath: must be string or null');
    }
    return true;
  }

  /**
   * Convert to JSON-serializable object
   */
  toJSON() {
    return {
      timestamp: this.timestamp,
      crashType: this.crashType,
      bridgeVersion: this.bridgeVersion,
      lastSuccessfulMessageId: this.lastSuccessfulMessageId,
      errorTrace: this.errorTrace,
      diagnosticsPath: this.diagnosticsPath,
    };
  }

  /**
   * Create from JSON object
   */
  static fromJSON(data) {
    return new CrashMetadata(data);
  }
}

/**
 * Represents snapshot of a single handler's state
 */
export class HandlerStateSnapshot {
  constructor({
    handlerId = null,
    isActive = false,
    pendingRequestCount = 0,
    lastInvocationTime = null,
    cacheSize = 0,
    errorCount = 0,
  } = {}) {
    this.handlerId = handlerId;
    this.isActive = isActive;
    this.pendingRequestCount = pendingRequestCount;
    this.lastInvocationTime = lastInvocationTime;
    this.cacheSize = cacheSize;
    this.errorCount = errorCount;
  }

  /**
   * Validate handler state snapshot schema
   */
  validate() {
    if (this.handlerId !== null && typeof this.handlerId !== 'string') {
      throw new CrashRecoveryStateError('Invalid handlerId: must be string or null');
    }
    if (typeof this.isActive !== 'boolean') {
      throw new CrashRecoveryStateError('Invalid isActive: must be boolean');
    }
    if (!Number.isInteger(this.pendingRequestCount) || this.pendingRequestCount < 0) {
      throw new CrashRecoveryStateError('Invalid pendingRequestCount: must be non-negative integer');
    }
    if (this.lastInvocationTime !== null && !Number.isInteger(this.lastInvocationTime)) {
      throw new CrashRecoveryStateError('Invalid lastInvocationTime: must be integer or null');
    }
    if (!Number.isInteger(this.cacheSize) || this.cacheSize < 0) {
      throw new CrashRecoveryStateError('Invalid cacheSize: must be non-negative integer');
    }
    if (!Number.isInteger(this.errorCount) || this.errorCount < 0) {
      throw new CrashRecoveryStateError('Invalid errorCount: must be non-negative integer');
    }
    return true;
  }

  /**
   * Convert to JSON-serializable object
   */
  toJSON() {
    return {
      handlerId: this.handlerId,
      isActive: this.isActive,
      pendingRequestCount: this.pendingRequestCount,
      lastInvocationTime: this.lastInvocationTime,
      cacheSize: this.cacheSize,
      errorCount: this.errorCount,
    };
  }

  /**
   * Create from JSON object
   */
  static fromJSON(data) {
    return new HandlerStateSnapshot(data);
  }
}

/**
 * Complete recovery state model with crash metadata and handler snapshots
 */
export class CrashRecoveryState {
  constructor({
    schemaVersion = '1.0.0',
    crashMetadata = null,
    handlerSnapshots = [],
    recoveryStrategy = 'auto-restart', // 'auto-restart', 'graceful-shutdown', 'degraded-mode'
    recoveryAttempts = 0,
    lastRecoveryTime = null,
  } = {}) {
    this.schemaVersion = schemaVersion;
    this.crashMetadata = crashMetadata;
    this.handlerSnapshots = handlerSnapshots;
    this.recoveryStrategy = recoveryStrategy;
    this.recoveryAttempts = recoveryAttempts;
    this.lastRecoveryTime = lastRecoveryTime;
  }

  /**
   * Validate complete recovery state schema
   */
  validate() {
    if (typeof this.schemaVersion !== 'string') {
      throw new CrashRecoveryStateError('Invalid schemaVersion: must be string');
    }
    if (this.crashMetadata !== null && !(this.crashMetadata instanceof CrashMetadata)) {
      throw new CrashRecoveryStateError('Invalid crashMetadata: must be CrashMetadata instance or null');
    }
    if (this.crashMetadata !== null) {
      this.crashMetadata.validate();
    }
    if (!Array.isArray(this.handlerSnapshots)) {
      throw new CrashRecoveryStateError('Invalid handlerSnapshots: must be array');
    }
    for (const snapshot of this.handlerSnapshots) {
      if (!(snapshot instanceof HandlerStateSnapshot)) {
        throw new CrashRecoveryStateError('Invalid handlerSnapshots: all items must be HandlerStateSnapshot instances');
      }
      snapshot.validate();
    }
    const validStrategies = ['auto-restart', 'graceful-shutdown', 'degraded-mode'];
    if (!validStrategies.includes(this.recoveryStrategy)) {
      throw new CrashRecoveryStateError(`Invalid recoveryStrategy: must be one of ${validStrategies.join(', ')}`);
    }
    if (!Number.isInteger(this.recoveryAttempts) || this.recoveryAttempts < 0) {
      throw new CrashRecoveryStateError('Invalid recoveryAttempts: must be non-negative integer');
    }
    if (this.lastRecoveryTime !== null && !Number.isInteger(this.lastRecoveryTime)) {
      throw new CrashRecoveryStateError('Invalid lastRecoveryTime: must be integer or null');
    }
    return true;
  }

  /**
   * Add handler snapshot to state
   */
  addHandlerSnapshot(snapshot) {
    if (!(snapshot instanceof HandlerStateSnapshot)) {
      throw new CrashRecoveryStateError('Invalid snapshot: must be HandlerStateSnapshot instance');
    }
    snapshot.validate();
    this.handlerSnapshots.push(snapshot);
  }

  /**
   * Get handler snapshot by ID
   */
  getHandlerSnapshot(handlerId) {
    return this.handlerSnapshots.find(s => s.handlerId === handlerId) || null;
  }

  /**
   * Update recovery attempt counter
   */
  recordRecoveryAttempt() {
    this.recoveryAttempts += 1;
    this.lastRecoveryTime = Date.now();
  }

  /**
   * Reset recovery state after successful recovery
   */
  resetRecoveryState() {
    this.recoveryAttempts = 0;
    this.crashMetadata = null;
    this.handlerSnapshots = [];
    this.recoveryStrategy = 'auto-restart';
    this.lastRecoveryTime = null;
  }

  /**
   * Convert to JSON-serializable object
   */
  toJSON() {
    return {
      schemaVersion: this.schemaVersion,
      crashMetadata: this.crashMetadata ? this.crashMetadata.toJSON() : null,
      handlerSnapshots: this.handlerSnapshots.map(s => s.toJSON()),
      recoveryStrategy: this.recoveryStrategy,
      recoveryAttempts: this.recoveryAttempts,
      lastRecoveryTime: this.lastRecoveryTime,
    };
  }

  /**
   * Create from JSON object with validation
   */
  static fromJSON(data) {
    if (!data || typeof data !== 'object') {
      throw new CrashRecoveryStateError('Invalid data: must be non-null object');
    }

    const crashMetadata = data.crashMetadata 
      ? CrashMetadata.fromJSON(data.crashMetadata)
      : null;

    const handlerSnapshots = (data.handlerSnapshots || [])
      .map(s => HandlerStateSnapshot.fromJSON(s));

    return new CrashRecoveryState({
      schemaVersion: data.schemaVersion || '1.0.0',
      crashMetadata,
      handlerSnapshots,
      recoveryStrategy: data.recoveryStrategy || 'auto-restart',
      recoveryAttempts: data.recoveryAttempts || 0,
      lastRecoveryTime: data.lastRecoveryTime || null,
    });
  }

  /**
   * Check if state is recoverable (has crash metadata and handler snapshots)
   */
  isRecoverable() {
    return this.crashMetadata !== null && this.handlerSnapshots.length > 0;
  }

  /**
   * Get recovery predicate: determine if recovery should be attempted
   */
  shouldAttemptRecovery() {
    // Don't retry if already attempted too many times
    if (this.recoveryAttempts >= 5) {
      return false;
    }
    // Don't retry if no crash metadata available
    if (!this.crashMetadata) {
      return false;
    }
    // Allow recovery if crash is recent (within last 60 seconds)
    const timeSinceCrash = Date.now() - this.crashMetadata.timestamp;
    return timeSinceCrash < 60000;
  }

  /**
   * Migrate state from previous schema version
   */
  static migrateFromLegacy(legacyData, fromVersion = '0.9.0') {
    // Placeholder for future version migrations
    // Currently assumes legacy data is compatible with v1.0.0
    return CrashRecoveryState.fromJSON(legacyData);
  }
}

/**
 * Factory function to create empty recovery state
 */
export function createEmptyRecoveryState() {
  return new CrashRecoveryState();
}

/**
 * Factory function to create crash state from error
 */
export function createCrashStateFromError(error, {
  crashType = 'unknown',
  bridgeVersion = null,
  lastSuccessfulMessageId = null,
} = {}) {
  const crashMetadata = new CrashMetadata({
    timestamp: Date.now(),
    crashType,
    bridgeVersion,
    lastSuccessfulMessageId,
    errorTrace: error ? error.stack : null,
  });

  return new CrashRecoveryState({
    crashMetadata,
    handlerSnapshots: [],
    recoveryStrategy: 'auto-restart',
    recoveryAttempts: 0,
  });
}
