#!/usr/bin/env node

/**
 * Diagnostics Collector Test Suite
 *
 * Comprehensive test coverage for DiagnosticsCollector (Step 54).
 * Tests: initialization, message registration, updates, queries, listeners, edge cases.
 *
 * @test Mocha + Chai
 * @author Bridge Architecture Team
 */

import { describe, it, before, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import {
  DiagnosticsCollector,
  DiagnosticsCollectorError,
  DiagnosticsValidationError
} from '../lib/diagnostics-collector.mjs';

/**
 * Test Fixtures
 */

function createValidDiagnostic(overrides = {}) {
  return {
    code: 'CS0001',
    message: 'Test diagnostic',
    severity: 'error',
    line: 0,
    column: 0,
    file: 'test.cs',
    ...overrides
  };
}

function createMultipleDiagnostics(count = 3) {
  const result = [];
  for (let i = 0; i < count; i++) {
    result.push(
      createValidDiagnostic({
        code: `CS000${i}`,
        message: `Diagnostic ${i}`,
        severity: ['error', 'warning', 'info'][i % 3],
        line: i,
        column: i * 2
      })
    );
  }
  return result;
}

function createMockServer() {
  const handlers = new Map();
  return {
    messageHandler: {
      on: (eventName, callback) => {
        handlers.set(eventName, callback);
      },
      emit: (eventName, message) => {
        if (handlers.has(eventName)) {
          handlers.get(eventName)(message);
        }
      }
    }
  };
}

/**
 * Test Suites
 */

describe('DiagnosticsCollector', () => {
  let collector;

  beforeEach(() => {
    collector = new DiagnosticsCollector();
  });

  afterEach(() => {
    if (collector) {
      collector.dispose();
    }
  });

  // ============= Suite 1: Initialization =============
  describe('Suite 1: Initialization', () => {
    it('should create instance with default options', () => {
      const instance = new DiagnosticsCollector();
      expect(instance).to.exist;
      expect(instance).to.be.instanceof(DiagnosticsCollector);
      instance.dispose();
    });

    it('should create instance with logger and metrics', () => {
      const mockLogger = {
        debug: () => {},
        info: () => {},
        warn: () => {},
        error: () => {}
      };
      const mockMetrics = {
        recordEvent: () => {}
      };
      const instance = new DiagnosticsCollector({ logger: mockLogger, metrics: mockMetrics });
      expect(instance).to.exist;
      instance.dispose();
    });

    it('should throw error if options is not an object', () => {
      expect(() => new DiagnosticsCollector('invalid')).to.throw(Error);
      expect(() => new DiagnosticsCollector([])).to.throw(Error);
      expect(() => new DiagnosticsCollector(null)).to.throw(Error);
    });
  });

  // ============= Suite 2: Message Handler Registration =============
  describe('Suite 2: Message Handler Registration', () => {
    it('should register message handlers successfully', async () => {
      const server = createMockServer();
      await collector.registerMessageHandlers(server);
      expect(server.messageHandler).to.exist;
    });

    it('should throw error if server is null', async () => {
      try {
        await collector.registerMessageHandlers(null);
        expect.fail('Should throw error');
      } catch (error) {
        expect(error).to.be.instanceof(DiagnosticsCollectorError);
        expect(error.operationType).to.equal('registration');
      }
    });

    it('should throw error if server.messageHandler.on is not a function', async () => {
      const server = { messageHandler: {} };
      try {
        await collector.registerMessageHandlers(server);
        expect.fail('Should throw error');
      } catch (error) {
        expect(error).to.be.instanceof(DiagnosticsCollectorError);
      }
    });
  });

  // ============= Suite 3: Diagnostics Updates =============
  describe('Suite 3: Diagnostics Updates', () => {
    it('should update diagnostics for a file', () => {
      const diags = createMultipleDiagnostics(2);
      collector.updateDiagnostics('test.cs', diags);
      expect(collector.hasDiagnostics('test.cs')).to.be.true;
    });

    it('should throw error if filepath is empty', () => {
      const diags = createMultipleDiagnostics(1);
      expect(() => collector.updateDiagnostics('', diags)).to.throw(DiagnosticsValidationError);
    });

    it('should throw error if diagnostics is not an array', () => {
      expect(() => collector.updateDiagnostics('test.cs', 'invalid')).to.throw(
        DiagnosticsValidationError
      );
    });

    it('should throw error if diagnostic is invalid', () => {
      const invalidDiag = { code: 'CS0001' }; // Missing required fields
      expect(() => collector.updateDiagnostics('test.cs', [invalidDiag])).to.throw(
        DiagnosticsValidationError
      );
    });
  });

  // ============= Suite 4: Query Methods =============
  describe('Suite 4: Query Methods', () => {
    beforeEach(() => {
      collector.updateDiagnostics('main.cs', createMultipleDiagnostics(3));
      collector.updateDiagnostics('utils.cs', createMultipleDiagnostics(2));
    });

    it('getDiagnosticsForFile should return diagnostics for a file', () => {
      const diags = collector.getDiagnosticsForFile('main.cs');
      expect(diags).to.be.an('array');
      expect(diags.length).to.equal(3);
    });

    it('getDiagnosticsForFile should return empty array if file not found', () => {
      const diags = collector.getDiagnosticsForFile('missing.cs');
      expect(diags).to.be.an('array');
      expect(diags.length).to.equal(0);
    });

    it('getDiagnosticsForFile should filter by severity', () => {
      const errors = collector.getDiagnosticsForFile('main.cs', 'error');
      expect(errors).to.be.an('array');
      expect(errors.every((d) => d.severity === 'error')).to.be.true;
    });

    it('getAllDiagnostics should return all diagnostics', () => {
      const all = collector.getAllDiagnostics();
      expect(all).to.be.instanceof(Map);
      expect(all.size).to.equal(2);
      expect(all.has('main.cs')).to.be.true;
      expect(all.has('utils.cs')).to.be.true;
    });

    it('getDiagnosticsBySeverity should filter all diagnostics by severity', () => {
      const errors = collector.getDiagnosticsBySeverity('error');
      expect(errors).to.be.instanceof(Map);
      // All diagnostics with 'error' severity should be included
      for (const [file, diags] of errors.entries()) {
        expect(diags.every((d) => d.severity === 'error')).to.be.true;
      }
    });

    it('getDiagnosticsBySeverity should throw error if severity is invalid', () => {
      expect(() => collector.getDiagnosticsBySeverity('invalid')).to.throw(
        DiagnosticsValidationError
      );
    });

    it('getDiagnosticsCount should return count for a file', () => {
      const count = collector.getDiagnosticsCount('main.cs');
      expect(count).to.equal(3);
    });

    it('getDiagnosticsCount should return 0 if file not found', () => {
      const count = collector.getDiagnosticsCount('missing.cs');
      expect(count).to.equal(0);
    });

    it('getDiagnosticsCount with no args should return total count', () => {
      const count = collector.getDiagnosticsCount();
      expect(count).to.equal(5);
    });

    it('hasDiagnostics should return true if file has diagnostics', () => {
      expect(collector.hasDiagnostics('main.cs')).to.be.true;
    });

    it('hasDiagnostics should return false if file not found', () => {
      expect(collector.hasDiagnostics('missing.cs')).to.be.false;
    });
  });

  // ============= Suite 5: Range Queries =============
  describe('Suite 5: Range Queries', () => {
    beforeEach(() => {
      const diags = [
        createValidDiagnostic({ line: 5, column: 10, endLine: 5, endColumn: 15 }),
        createValidDiagnostic({ line: 10, column: 0, endLine: 10, endColumn: 5 }),
        createValidDiagnostic({ line: 10, column: 8, code: 'CS0002' })
      ];
      collector.updateDiagnostics('test.cs', diags);
    });

    it('getDiagnosticsRange should return diagnostics at cursor position', () => {
      const diags = collector.getDiagnosticsRange('test.cs', 5, 10);
      expect(diags.length).to.be.greaterThan(0);
    });

    it('getDiagnosticsRange should return empty array if no overlap', () => {
      const diags = collector.getDiagnosticsRange('test.cs', 20, 0);
      expect(diags).to.be.an('array');
      expect(diags.length).to.equal(0);
    });

    it('getDiagnosticsRange should handle selection ranges', () => {
      const diags = collector.getDiagnosticsRange('test.cs', 10, 0, 10, 15);
      expect(diags.length).to.be.greaterThan(0);
    });

    it('getDiagnosticsRange should throw error if filepath is invalid', () => {
      expect(() => collector.getDiagnosticsRange('', 0, 0)).to.throw(
        DiagnosticsValidationError
      );
    });

    it('getDiagnosticsRange should throw error if line is invalid', () => {
      expect(() => collector.getDiagnosticsRange('test.cs', -1, 0)).to.throw(
        DiagnosticsValidationError
      );
    });
  });

  // ============= Suite 6: Listener Subscriptions =============
  describe('Suite 6: Listener Subscriptions', () => {
    it('should notify listeners on diagnostics change', (done) => {
      let notified = false;
      collector.onDiagnosticsChange((event) => {
        notified = true;
        expect(event.filepath).to.equal('test.cs');
        expect(event.diagnostics).to.be.an('array');
        expect(event.changeType).to.equal('update');
        done();
      });

      const diags = createMultipleDiagnostics(1);
      collector.updateDiagnostics('test.cs', diags);
    });

    it('should support multiple listeners', (done) => {
      let count = 0;
      const threshold = 2;

      const listener1 = () => {
        count++;
      };
      const listener2 = () => {
        count++;
        if (count === threshold) {
          done();
        }
      };

      collector.onDiagnosticsChange(listener1);
      collector.onDiagnosticsChange(listener2);

      const diags = createMultipleDiagnostics(1);
      collector.updateDiagnostics('test.cs', diags);
    });

    it('should allow unsubscribe via returned function', () => {
      let callCount = 0;
      const unsubscribe = collector.onDiagnosticsChange(() => {
        callCount++;
      });

      const diags = createMultipleDiagnostics(1);
      collector.updateDiagnostics('test.cs', diags);
      expect(callCount).to.equal(1);

      unsubscribe();
      collector.updateDiagnostics('test.cs', diags);
      expect(callCount).to.equal(1); // Should not be called again
    });

    it('should throw error if callback is not a function', () => {
      expect(() => collector.onDiagnosticsChange('invalid')).to.throw(TypeError);
    });
  });

  // ============= Suite 7: Message Handler Integration =============
  describe('Suite 7: Message Handler Integration', () => {
    it('should handle didOpenDiagnostics message', async () => {
      const server = createMockServer();
      await collector.registerMessageHandlers(server);

      const message = {
        data: {
          filepath: 'test.cs',
          diagnostics: createMultipleDiagnostics(2)
        }
      };

      server.messageHandler.emit('didOpenDiagnostics', message);
      expect(collector.hasDiagnostics('test.cs')).to.be.true;
      expect(collector.getDiagnosticsCount('test.cs')).to.equal(2);
    });

    it('should handle didUpdateDiagnostics message', async () => {
      const server = createMockServer();
      await collector.registerMessageHandlers(server);

      const message = {
        data: {
          filepath: 'test.cs',
          diagnostics: createMultipleDiagnostics(3)
        }
      };

      server.messageHandler.emit('didUpdateDiagnostics', message);
      expect(collector.getDiagnosticsCount('test.cs')).to.equal(3);
    });

    it('should handle didCloseDiagnostics message', async () => {
      const server = createMockServer();
      await collector.registerMessageHandlers(server);

      // First add diagnostics
      collector.updateDiagnostics('test.cs', createMultipleDiagnostics(2));
      expect(collector.hasDiagnostics('test.cs')).to.be.true;

      // Now close them
      const message = { data: { filepath: 'test.cs' } };
      server.messageHandler.emit('didCloseDiagnostics', message);
      expect(collector.hasDiagnostics('test.cs')).to.be.false;
    });
  });

  // ============= Suite 8: Cleanup & Disposal =============
  describe('Suite 8: Cleanup & Disposal', () => {
    it('should dispose and clear all diagnostics', () => {
      collector.updateDiagnostics('test1.cs', createMultipleDiagnostics(2));
      collector.updateDiagnostics('test2.cs', createMultipleDiagnostics(3));
      expect(collector.getDiagnosticsCount()).to.equal(5);

      collector.dispose();
      expect(collector.getDiagnosticsCount()).to.equal(0);
    });

    it('should dispose and clear all listeners', () => {
      let callCount = 0;
      collector.onDiagnosticsChange(() => {
        callCount++;
      });

      collector.updateDiagnostics('test.cs', createMultipleDiagnostics(1));
      expect(callCount).to.equal(1);

      collector.dispose();
      collector.updateDiagnostics('test2.cs', createMultipleDiagnostics(1));
      expect(callCount).to.equal(1); // Should not increase
    });
  });

  // ============= Suite 9: Edge Cases & Validation =============
  describe('Suite 9: Edge Cases & Validation', () => {
    it('should handle empty diagnostics array', () => {
      collector.updateDiagnostics('test.cs', []);
      expect(collector.hasDiagnostics('test.cs')).to.be.false;
    });

    it('should handle overlapping diagnostic ranges', () => {
      const diags = [
        createValidDiagnostic({ line: 5, column: 0, endLine: 5, endColumn: 20 }),
        createValidDiagnostic({
          line: 5,
          column: 10,
          endLine: 5,
          endColumn: 30,
          code: 'CS0002'
        })
      ];
      collector.updateDiagnostics('test.cs', diags);

      const overlap = collector.getDiagnosticsRange('test.cs', 5, 15);
      expect(overlap.length).to.equal(2);
    });

    it('should handle multi-line diagnostic ranges', () => {
      const diags = [
        createValidDiagnostic({ line: 5, column: 0, endLine: 10, endColumn: 0 })
      ];
      collector.updateDiagnostics('test.cs', diags);

      const found = collector.getDiagnosticsRange('test.cs', 7, 5);
      expect(found.length).to.equal(1);
    });

    it('should return copies, not references', () => {
      const original = createMultipleDiagnostics(1);
      collector.updateDiagnostics('test.cs', original);

      const retrieved = collector.getDiagnosticsForFile('test.cs');
      retrieved[0].message = 'Modified';

      const retrieved2 = collector.getDiagnosticsForFile('test.cs');
      expect(retrieved2[0].message).to.not.equal('Modified');
    });

    it('should validate endLine >= line', () => {
      const diags = [createValidDiagnostic({ endLine: 2 })];
      expect(() => collector.updateDiagnostics('test.cs', diags)).to.not.throw();
    });

    it('should handle invalid endLine', () => {
      const diags = [createValidDiagnostic({ endLine: -1 })];
      expect(() => collector.updateDiagnostics('test.cs', diags)).to.throw(
        DiagnosticsValidationError
      );
    });

    it('should handle multiple severity levels in one file', () => {
      const diags = [
        createValidDiagnostic({ severity: 'error', line: 0 }),
        createValidDiagnostic({ severity: 'warning', line: 1, code: 'CS0002' }),
        createValidDiagnostic({ severity: 'info', line: 2, code: 'CS0003' })
      ];
      collector.updateDiagnostics('test.cs', diags);

      const errors = collector.getDiagnosticsForFile('test.cs', 'error');
      const warnings = collector.getDiagnosticsForFile('test.cs', 'warning');
      const infos = collector.getDiagnosticsForFile('test.cs', 'info');

      expect(errors.length).to.equal(1);
      expect(warnings.length).to.equal(1);
      expect(infos.length).to.equal(1);
    });
  });

  // ============= Suite 10: Error Classes =============
  describe('Suite 10: Error Classes', () => {
    it('DiagnosticsCollectorError should have correct properties', () => {
      const error = new DiagnosticsCollectorError('Test error', 'test_op', new Error('Original'));
      expect(error).to.be.instanceof(Error);
      expect(error.name).to.equal('DiagnosticsCollectorError');
      expect(error.operationType).to.equal('test_op');
      expect(error.originalError).to.exist;
    });

    it('DiagnosticsValidationError should have correct properties', () => {
      const error = new DiagnosticsValidationError('testField', 'Invalid value', 42);
      expect(error).to.be.instanceof(Error);
      expect(error.name).to.equal('DiagnosticsValidationError');
      expect(error.fieldName).to.equal('testField');
      expect(error.value).to.equal(42);
    });
  });
});
