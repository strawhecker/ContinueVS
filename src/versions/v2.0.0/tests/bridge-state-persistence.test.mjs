/**
 * Bridge State Persistence Tests
 * 
 * 30 comprehensive test cases covering:
 * - Initialization & configuration (3 tests)
 * - Checkpoint creation (4 tests)
 * - Persistence (6 tests)
 * - Recovery (6 tests)
 * - Schema validation (4 tests)
 * - Performance gates (2 tests)
 * - Integration patterns (2 tests)
 * - Edge cases (3 tests)
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import fs from 'fs';
import path from 'path';
import os from 'os';
import { fileURLToPath } from 'url';
import {
  BridgeStateCheckpoint,
  BridgeStatePersistence,
  createBridgeStatePersistence
} from '../lib/bridge-state-persistence.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Test fixtures
const validCheckpoint = () => new BridgeStateCheckpoint({
  timestamp: new Date().toISOString(),
  phase: 'ready',
  handlers: {
    'refactor': { status: 'active', errorCount: 0, timeoutCount: 0 },
    'search': { status: 'active', errorCount: 1, timeoutCount: 0 },
    'hover': { status: 'idle', errorCount: 0, timeoutCount: 0 }
  },
  subscriptions: { count: 25, types: ['onEdit', 'onSave', 'onClose'] },
  pendingRequests: { count: 3 },
  uptime: 1234,
  bridgeVersion: '2.0.0'
});

const validCheckpointMinimal = () => new BridgeStateCheckpoint({
  timestamp: new Date().toISOString(),
  phase: 'bootstrap',
  handlers: {},
  subscriptions: { count: 0, types: [] },
  pendingRequests: { count: 0 },
  uptime: 0,
  bridgeVersion: '2.0.0'
});

const mockLogger = () => ({
  log: (level, msg) => {
    // Capture logs in tests if needed
  }
});

const mockMetrics = () => ({
  record: (name, value) => {
    // Capture metrics in tests if needed
  }
});

describe('BridgeStateCheckpoint', () => {
  describe('Initialization & Configuration', () => {
    // Test 1: Initialization with defaults
    it('should initialize with default values', () => {
      const checkpoint = new BridgeStateCheckpoint();
      expect(checkpoint).to.exist;
      expect(checkpoint.phase).to.equal('bootstrap');
      expect(checkpoint.handlers).to.deep.equal({});
      expect(checkpoint.subscriptions).to.deep.equal({ count: 0, types: [] });
      expect(checkpoint.timestamp).to.exist;
      expect(new Date(checkpoint.timestamp)).to.be.instanceof(Date);
    });

    // Test 2: Initialization with custom values
    it('should initialize with provided values', () => {
      const data = {
        phase: 'ready',
        handlers: { 'test': { status: 'active', errorCount: 0, timeoutCount: 0 } },
        uptime: 100
      };
      const checkpoint = new BridgeStateCheckpoint(data);
      expect(checkpoint.phase).to.equal('ready');
      expect(checkpoint.uptime).to.equal(100);
      expect(checkpoint.handlers).to.have.property('test');
    });

    // Test 3: Factory function creates valid instance
    it('should be created via factory function', () => {
      const checkpoint = validCheckpoint();
      expect(checkpoint).to.be.instanceof(BridgeStateCheckpoint);
      expect(checkpoint.validate()).to.be.true;
    });
  });

  describe('Checkpoint Creation', () => {
    // Test 4: Valid checkpoint structure
    it('should validate a valid checkpoint', () => {
      const checkpoint = validCheckpoint();
      expect(checkpoint.validate()).to.be.true;
    });

    // Test 5: Minimal valid checkpoint
    it('should validate minimal checkpoint with no handlers', () => {
      const checkpoint = validCheckpointMinimal();
      expect(checkpoint.validate()).to.be.true;
    });

    // Test 6: Invalid phase enum
    it('should reject invalid phase', () => {
      const checkpoint = new BridgeStateCheckpoint({
        timestamp: new Date().toISOString(),
        phase: 'invalid-phase'
      });
      expect(checkpoint.validate()).to.be.false;
    });

    // Test 7: Timestamp serialization
    it('should serialize and deserialize timestamp correctly', () => {
      const original = validCheckpoint();
      const json = original.toJSON();
      const restored = BridgeStateCheckpoint.fromJSON(json);
      expect(restored.timestamp).to.equal(original.timestamp);
    });
  });

  describe('Schema Validation', () => {
    // Test 8: Missing timestamp
    it('should reject checkpoint without timestamp', () => {
      const checkpoint = new BridgeStateCheckpoint({
        phase: 'ready',
        handlers: {},
        timestamp: null
      });
      expect(checkpoint.validate()).to.be.false;
    });

    // Test 9: Invalid handler status
    it('should reject invalid handler status', () => {
      const checkpoint = new BridgeStateCheckpoint({
        timestamp: new Date().toISOString(),
        phase: 'ready',
        handlers: {
          'test': { status: 'invalid-status', errorCount: 0, timeoutCount: 0 }
        }
      });
      expect(checkpoint.validate()).to.be.false;
    });

    // Test 10: Negative error count
    it('should reject negative error count', () => {
      const checkpoint = new BridgeStateCheckpoint({
        timestamp: new Date().toISOString(),
        phase: 'ready',
        handlers: {
          'test': { status: 'active', errorCount: -1, timeoutCount: 0 }
        }
      });
      expect(checkpoint.validate()).to.be.false;
    });

    // Test 11: Phase enumeration enforcement
    it('should accept all valid phase values', () => {
      const phases = ['bootstrap', 'connected', 'subscribed', 'ready', 'degraded'];
      for (const phase of phases) {
        const checkpoint = new BridgeStateCheckpoint({
          timestamp: new Date().toISOString(),
          phase
        });
        expect(checkpoint.validate(), `phase: ${phase}`).to.be.true;
      }
    });
  });

  describe('Staleness Check', () => {
    // Test 12: Recent checkpoint not stale
    it('should not mark recent checkpoint as stale', () => {
      const checkpoint = new BridgeStateCheckpoint({
        timestamp: new Date().toISOString(),
        phase: 'ready'
      });
      expect(checkpoint.isStale(7 * 24 * 60 * 60 * 1000)).to.be.false;
    });

    // Test 13: Old checkpoint marked stale
    it('should mark old checkpoint as stale', () => {
      const oldDate = new Date(Date.now() - 8 * 24 * 60 * 60 * 1000); // 8 days ago
      const checkpoint = new BridgeStateCheckpoint({
        timestamp: oldDate.toISOString(),
        phase: 'ready'
      });
      expect(checkpoint.isStale(7 * 24 * 60 * 60 * 1000)).to.be.true;
    });
  });
});

describe('BridgeStatePersistence', () => {
  let tempDir;
  let persistence;

  beforeEach(() => {
    tempDir = path.join(os.tmpdir(), `bridge-state-test-${Date.now()}`);
    fs.mkdirSync(tempDir, { recursive: true });
    persistence = new BridgeStatePersistence({
      stateDir: tempDir,
      stateFile: path.join(tempDir, 'bridge-state.json'),
      logger: mockLogger(),
      metrics: mockMetrics()
    });
  });

  afterEach(async () => {
    // Cleanup temp directory
    if (fs.existsSync(tempDir)) {
      fs.rmSync(tempDir, { recursive: true, force: true });
    }
  });

  describe('Persistence', () => {
    // Test 14: Save checkpoint to file
    it('should save checkpoint to file', async () => {
      const checkpoint = validCheckpoint();
      const result = await persistence.saveAsync(checkpoint);
      expect(result).to.be.true;
      expect(fs.existsSync(persistence.stateFile)).to.be.true;
    });

    // Test 15: Create directory if needed
    it('should create directory if it does not exist', async () => {
      const nonexistentDir = path.join(tempDir, 'nonexistent', 'nested');
      persistence.stateDir = nonexistentDir;
      persistence.stateFile = path.join(nonexistentDir, 'state.json');

      const checkpoint = validCheckpoint();
      const result = await persistence.saveAsync(checkpoint);
      expect(result).to.be.true;
      expect(fs.existsSync(nonexistentDir)).to.be.true;
    });

    // Test 16: Saved file is valid JSON
    it('should write valid JSON to file', async () => {
      const checkpoint = validCheckpoint();
      await persistence.saveAsync(checkpoint);

      const content = fs.readFileSync(persistence.stateFile, 'utf8');
      const parsed = JSON.parse(content); // Should not throw
      expect(parsed).to.have.property('timestamp');
      expect(parsed).to.have.property('phase');
      expect(parsed).to.have.property('handlers');
    });

    // Test 17: Atomic write (no partial files)
    it('should use atomic write (temp rename pattern)', async () => {
      const checkpoint = validCheckpoint();
      await persistence.saveAsync(checkpoint);

      const tempFile = `${persistence.stateFile}.tmp`;
      expect(fs.existsSync(tempFile)).to.be.false; // Temp should not remain
      expect(fs.existsSync(persistence.stateFile)).to.be.true; // Final should exist
    });

    // Test 18: Reject invalid checkpoint before save
    it('should reject invalid checkpoint and not save', async () => {
      const invalid = new BridgeStateCheckpoint({
        phase: 'invalid-phase' // Invalid
      });
      const result = await persistence.saveAsync(invalid);
      expect(result).to.be.false;
      expect(fs.existsSync(persistence.stateFile)).to.be.false;
    });

    // Test 19: Performance gate - save < 500ms
    it('should save in under 500ms', async () => {
      const checkpoint = validCheckpoint();
      const start = Date.now();
      await persistence.saveAsync(checkpoint);
      const duration = Date.now() - start;
      expect(duration).to.be.lessThan(500);
    });
  });

  describe('Recovery', () => {
    // Test 20: Load valid checkpoint from file
    it('should load valid checkpoint from file', async () => {
      const saved = validCheckpoint();
      await persistence.saveAsync(saved);

      const loaded = await persistence.loadAsync();
      expect(loaded).to.exist;
      expect(loaded.phase).to.equal('ready');
      expect(loaded.handlers).to.have.property('refactor');
    });

    // Test 21: Return null if file missing
    it('should return null if checkpoint file does not exist', async () => {
      const loaded = await persistence.loadAsync();
      expect(loaded).to.be.null;
    });

    // Test 22: Handle corrupted JSON gracefully
    it('should return null on corrupted JSON', async () => {
      fs.writeFileSync(persistence.stateFile, 'invalid json {[}', 'utf8');
      const loaded = await persistence.loadAsync();
      expect(loaded).to.be.null;
    });

    // Test 23: Validate loaded checkpoint
    it('should validate loaded checkpoint and discard invalid', async () => {
      const invalid = { phase: 'invalid-phase' };
      fs.writeFileSync(persistence.stateFile, JSON.stringify(invalid), 'utf8');
      const loaded = await persistence.loadAsync();
      expect(loaded).to.be.null;
    });

    // Test 24: Discard stale checkpoint
    it('should discard stale checkpoint (>7 days)', async () => {
      const oldCheckpoint = new BridgeStateCheckpoint({
        timestamp: new Date(Date.now() - 8 * 24 * 60 * 60 * 1000).toISOString(),
        phase: 'ready'
      });
      await persistence.saveAsync(oldCheckpoint);

      const loaded = await persistence.loadAsync();
      expect(loaded).to.be.null;
    });

    // Test 25: Performance gate - load < 200ms
    it('should load in under 200ms', async () => {
      const checkpoint = validCheckpoint();
      await persistence.saveAsync(checkpoint);

      const start = Date.now();
      await persistence.loadAsync();
      const duration = Date.now() - start;
      expect(duration).to.be.lessThan(200);
    });
  });

  describe('Integration Patterns', () => {
    // Test 26: Save with logger and metrics
    it('should log and record metrics on save', async () => {
      let logged = false;
      let metricsRecorded = false;

      const customPersistence = new BridgeStatePersistence({
        stateDir: tempDir,
        stateFile: path.join(tempDir, 'state.json'),
        logger: {
          log: () => { logged = true; }
        },
        metrics: {
          record: () => { metricsRecorded = true; }
        }
      });

      const checkpoint = validCheckpoint();
      await customPersistence.saveAsync(checkpoint);

      expect(logged).to.be.true;
      expect(metricsRecorded).to.be.true;
    });

    // Test 27: Load with logger on error
    it('should log errors on load failure', async () => {
      let errorLogged = false;

      const customPersistence = new BridgeStatePersistence({
        stateDir: tempDir,
        stateFile: path.join(tempDir, 'state.json'),
        logger: {
          log: (level, msg) => {
            if (level === 'warn' || level === 'error') {
              errorLogged = true;
            }
          }
        }
      });

      fs.writeFileSync(customPersistence.stateFile, 'invalid', 'utf8');
      await customPersistence.loadAsync();

      expect(errorLogged).to.be.true;
    });
  });

  describe('Edge Cases', () => {
    // Test 28: Large state object (many handlers)
    it('should handle large checkpoint with 50+ handlers', async () => {
      const largeCheckpoint = new BridgeStateCheckpoint({
        timestamp: new Date().toISOString(),
        phase: 'ready',
        handlers: Array.from({ length: 50 }, (_, i) => ({
          [`handler-${i}`]: { status: 'active', errorCount: i, timeoutCount: i }
        })).reduce((acc, obj) => Object.assign(acc, obj), {}),
        subscriptions: { count: 500, types: [] },
        pendingRequests: { count: 100 }
      });

      const result = await persistence.saveAsync(largeCheckpoint);
      expect(result).to.be.true;

      const loaded = await persistence.loadAsync();
      expect(loaded).to.exist;
      expect(Object.keys(loaded.handlers).length).to.equal(50);
    });

    // Test 29: Empty subscriptions
    it('should handle checkpoint with no subscriptions', async () => {
      const checkpoint = new BridgeStateCheckpoint({
        timestamp: new Date().toISOString(),
        phase: 'bootstrap',
        handlers: {},
        subscriptions: { count: 0, types: [] },
        pendingRequests: { count: 0 }
      });

      await persistence.saveAsync(checkpoint);
      const loaded = await persistence.loadAsync();
      expect(loaded).to.exist;
      expect(loaded.subscriptions.count).to.equal(0);
    });

    // Test 30: Zero pending requests
    it('should handle checkpoint with no pending requests', async () => {
      const checkpoint = new BridgeStateCheckpoint({
        timestamp: new Date().toISOString(),
        phase: 'ready',
        handlers: { 'test': { status: 'active', errorCount: 0, timeoutCount: 0 } },
        subscriptions: { count: 5, types: ['onEdit'] },
        pendingRequests: { count: 0 }
      });

      await persistence.saveAsync(checkpoint);
      const loaded = await persistence.loadAsync();
      expect(loaded).to.exist;
      expect(loaded.pendingRequests.count).to.equal(0);
    });
  });

  describe('Cleanup Operations', () => {
    // Extra: Delete checkpoint file
    it('should delete checkpoint file', async () => {
      const checkpoint = validCheckpoint();
      await persistence.saveAsync(checkpoint);
      expect(fs.existsSync(persistence.stateFile)).to.be.true;

      const result = await persistence.deleteAsync();
      expect(result).to.be.true;
      expect(fs.existsSync(persistence.stateFile)).to.be.false;
    });

    // Extra: Delete non-existent file gracefully
    it('should handle deletion of non-existent file gracefully', async () => {
      const result = await persistence.deleteAsync();
      expect(result).to.be.true; // No error thrown
    });
  });
});
