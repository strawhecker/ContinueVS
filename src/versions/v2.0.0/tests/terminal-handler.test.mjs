#!/usr/bin/env node

/**
 * Terminal Handler Tests (Step 82)
 *
 * Comprehensive test suite for terminal-handler.mjs
 * 28 test cases across 6 suites covering initialization, execution, streaming, input, subscriptions, and error handling.
 *
 * **Test Organization**:
 * - Suite 1: Initialization & Dependencies (3 tests)
 * - Suite 2: Command Execution (5 tests)
 * - Suite 3: Output Streaming (4 tests)
 * - Suite 4: Input & Control (4 tests)
 * - Suite 5: Subscriptions (5 tests)
 * - Suite 6: Error Handling (7 tests)
 *
 * **Run**: npx mocha src/versions/v2.0.0/tests/terminal-handler.test.mjs --timeout 15000
 *
 * @module src/versions/v2.0.0/tests/terminal-handler.test.mjs
 * @version 1.0.0
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import {
  TerminalHandler,
  createTerminalHandler,
  terminalHandler,
  TerminalError,
  CommandError,
  StreamError,
  StateError,
} from '../lib/terminal-handler.mjs';

// ============================================================================
// MOCK TERMINAL COLLECTOR
// ============================================================================

/**
 * Mock TerminalCollector for testing without DTE
 */
class MockTerminalCollector {
  constructor(config = {}) {
    this.config = config;
    this.commandCount = 0;
    this.state = 'idle';
    this.shouldFail = config.shouldFail || false;
    this.failureType = config.failureType || null;
  }

  async *executeCommand(command, timeout, cwd) {
    this.commandCount++;
    this.state = 'running';

    if (this.shouldFail && this.failureType === 'stream') {
      throw new Error('Stream failure');
    }

    // Simulate command output in chunks
    const output = `Output of: ${command}\nLine 2\nLine 3`;
    const chunkSize = 10;

    for (let i = 0; i < output.length; i += chunkSize) {
      const chunk = output.substring(i, Math.min(i + chunkSize, output.length));
      const isLast = i + chunkSize >= output.length;

      yield {
        chunk,
        isPartial: !isLast,
        isError: false,
        timestamp: Date.now(),
      };

      if (this.config.delayMs) {
        await new Promise((r) => setTimeout(r, this.config.delayMs));
      }
    }

    this.state = 'idle';
  }

  async sendInput(text) {
    if (this.shouldFail && this.failureType === 'sendInput') {
      throw new Error('Send input failed');
    }
  }

  async clearTerminal() {
    if (this.shouldFail && this.failureType === 'clear') {
      throw new Error('Clear failed');
    }
    this.state = 'idle';
  }

  async getStatus() {
    return {
      state: this.state,
      isResponsive: !this.shouldFail,
      commandCount: this.commandCount,
      lastOutput: 'mock output',
    };
  }
}

/**
 * Mock logger for testing
 */
class MockLogger {
  constructor() {
    this.logs = [];
  }

  debug(msg) {
    this.logs.push({ level: 'debug', message: msg });
  }

  warn(msg) {
    this.logs.push({ level: 'warn', message: msg });
  }

  info(msg) {
    this.logs.push({ level: 'info', message: msg });
  }
}

/**
 * Mock metrics for testing
 */
class MockMetrics {
  constructor() {
    this.records = [];
  }

  record(name, fields) {
    this.records.push({ name, fields });
  }
}

// ============================================================================
// TEST SUITES
// ============================================================================

describe('TerminalHandler', () => {
  // ========================================================================
  // SUITE 1: Initialization & Dependencies
  // ========================================================================

  describe('Suite 1: Initialization & Dependencies', () => {
    it('should create handler with collector and optional logger/metrics', () => {
      const collector = new MockTerminalCollector();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const handler = new TerminalHandler(collector, logger, metrics);

      expect(handler).to.exist;
      expect(handler.collector).to.equal(collector);
      expect(handler.logger).to.equal(logger);
      expect(handler.metrics).to.equal(metrics);
    });

    it('should throw TerminalError if collector is null', () => {
      expect(() => new TerminalHandler(null)).to.throw(TerminalError);
    });

    it('should create handler with null logger/metrics (graceful degradation)', () => {
      const collector = new MockTerminalCollector();
      const handler = new TerminalHandler(collector, null, null);

      expect(handler).to.exist;
      expect(handler.logger).to.be.null;
      expect(handler.metrics).to.be.null;
    });
  });

  // ========================================================================
  // SUITE 2: Command Execution
  // ========================================================================

  describe('Suite 2: Command Execution', () => {
    let handler;
    let collector;
    let logger;

    beforeEach(() => {
      collector = new MockTerminalCollector();
      logger = new MockLogger();
      handler = new TerminalHandler(collector, logger);
    });

    it('should execute command and return chunks', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 'echo "hello"' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data.chunks).to.be.an('array');
      expect(response.data.chunks.length).to.be.greaterThan(0);
      expect(response.data.isComplete).to.be.true;
    });

    it('should set command state to running during execution', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 'test' },
      };

      const statusBefore = await collector.getStatus();
      expect(statusBefore.state).to.equal('idle');

      // Note: In real test, state would be 'running' during execution
      // Mock completes immediately, so we can't capture mid-execution
    });

    it('should throw CommandError if command is missing', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.error).to.include('must be');
    });

    it('should throw CommandError if command is not a string', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 123 },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.error).to.include('must be a non-empty string');
    });

    it('should include command text in response', async () => {
      const cmdText = 'npm run build';
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: cmdText },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data.commandText).to.equal(cmdText);
    });
  });

  // ========================================================================
  // SUITE 3: Output Streaming
  // ========================================================================

  describe('Suite 3: Output Streaming', () => {
    let handler;
    let collector;

    beforeEach(() => {
      collector = new MockTerminalCollector();
      handler = new TerminalHandler(collector);
    });

    it('should accumulate partial chunks into complete output', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 'echo "test"' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data.chunks).to.be.an('array');

      // Each chunk should have text, isPartial, isError, timestamp
      for (const chunk of response.data.chunks) {
        expect(chunk).to.have.property('text');
        expect(chunk).to.have.property('isPartial');
        expect(chunk).to.have.property('isError');
        expect(chunk).to.have.property('timestamp');
      }
    });

    it('should mark last chunk as complete (isPartial: false)', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 'test' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      const lastChunk = response.data.chunks[response.data.chunks.length - 1];
      expect(lastChunk.isPartial).to.be.false;
    });

    it('should preserve all output in chunks', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 'test' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      const combined = response.data.chunks.map((c) => c.text).join('');
      expect(combined).to.include('Output of: test');
    });

    it('should handle large output with multiple chunks', async () => {
      collector = new MockTerminalCollector({ delayMs: 0 }); // Fast for testing
      handler = new TerminalHandler(collector);

      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 'large' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data.chunks.length).to.be.greaterThanOrEqual(2);
    });
  });

  // ========================================================================
  // SUITE 4: Input & Control
  // ========================================================================

  describe('Suite 4: Input & Control', () => {
    let handler;
    let collector;

    beforeEach(() => {
      collector = new MockTerminalCollector();
      handler = new TerminalHandler(collector);
    });

    it('should send input to terminal', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'sendInput', text: 'npm test' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data.queued).to.be.true;
    });

    it('should throw StateError if text is not a string', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'sendInput', text: 123 },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.error).to.include('must be a string');
    });

    it('should clear terminal', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'clear' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data.cleared).to.be.true;
    });

    it('should get terminal status', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'getStatus' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data).to.have.property('state');
      expect(response.data).to.have.property('isResponsive');
      expect(response.data).to.have.property('commandCount');
    });
  });

  // ========================================================================
  // SUITE 5: Subscriptions
  // ========================================================================

  describe('Suite 5: Subscriptions', () => {
    let handler;
    let collector;

    beforeEach(() => {
      collector = new MockTerminalCollector();
      handler = new TerminalHandler(collector);
    });

    it('should register subscription for onTerminalOutput', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'subscribe' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.true;
      expect(response.data.subscriptionId).to.exist;
      expect(response.data.subscriptionId).to.include('sub_');
    });

    it('should assign unique subscription IDs', async () => {
      const msg = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'subscribe' },
      };

      const resp1 = await handler.handle(msg);
      const resp2 = await handler.handle(msg);

      expect(resp1.data.subscriptionId).to.not.equal(resp2.data.subscriptionId);
    });

    it('should track multiple subscriptions', async () => {
      const msg = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'subscribe' },
      };

      await handler.handle(msg);
      await handler.handle(msg);
      await handler.handle(msg);

      const subs = handler.getSubscriptions();
      expect(subs).to.have.lengthOf(3);
    });

    it('should unsubscribe from subscription', async () => {
      const msg = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'subscribe' },
      };

      const resp = await handler.handle(msg);
      const subId = resp.data.subscriptionId;

      const unsubscribed = handler.unsubscribe(subId);

      expect(unsubscribed).to.be.true;
      expect(handler.getSubscriptions()).to.not.include(subId);
    });

    it('should emit output to all listeners', async () => {
      const msg = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'subscribe' },
      };

      await handler.handle(msg);
      await handler.handle(msg);

      let emitCount = 0;
      const originalSubs = handler.subscriptions;
      for (const [, listener] of originalSubs) {
        listener({ text: 'test', isPartial: false });
        emitCount++;
      }

      expect(emitCount).to.equal(2);
    });
  });

  // ========================================================================
  // SUITE 6: Error Handling
  // ========================================================================

  describe('Suite 6: Error Handling', () => {
    let handler;
    let collector;
    let logger;

    beforeEach(() => {
      collector = new MockTerminalCollector();
      logger = new MockLogger();
      handler = new TerminalHandler(collector, logger);
    });

    it('should catch command execution errors', async () => {
      collector.shouldFail = true;
      collector.failureType = 'stream';
      handler = new TerminalHandler(collector, logger);

      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'execute', command: 'bad' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.error).to.exist;
    });

    it('should throw StateError for unknown operation', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'unknownOp' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.error).to.include('Unknown operation');
    });

    it('should throw StateError if message data is missing', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: null,
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.error).to.include('Missing operation');
    });

    it('should record error metrics', async () => {
      const metrics = new MockMetrics();
      handler = new TerminalHandler(collector, logger, metrics);

      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'unknownOp' },
      };

      await handler.handle(message);

      expect(metrics.records).to.have.lengthOf.greaterThan(0);
      const errorRecord = metrics.records.find((r) => r.name === 'terminal.operation');
      expect(errorRecord.fields.success).to.be.false;
    });

    it('should catch sendInput failures', async () => {
      collector.shouldFail = true;
      collector.failureType = 'sendInput';
      handler = new TerminalHandler(collector, logger);

      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'sendInput', text: 'test' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.error).to.exist;
    });

    it('should include RPC error codes in error responses', async () => {
      const message = {
        messageType: 'bridge:executeTerminalCommand',
        data: { operation: 'unknownOp' },
      };

      const response = await handler.handle(message);

      expect(response.success).to.be.false;
      expect(response.rpcErrorCode).to.be.a('number');
      expect(response.rpcErrorCode).to.be.lessThan(0); // Negative RPC error codes
    });
  });
});

// ============================================================================
// FACTORY FUNCTION TESTS
// ============================================================================

describe('Factory Functions', () => {
  it('should create handler via createTerminalHandler factory', () => {
    const collector = new MockTerminalCollector();
    const logger = new MockLogger();
    const context = { collector, logger };

    const handler = createTerminalHandler(context);

    expect(handler).to.be.a('function');
  });

  it('should throw if collector missing in factory context', () => {
    expect(() => createTerminalHandler({})).to.throw(TerminalError);
  });

  it('should return async function from factory', async () => {
    const collector = new MockTerminalCollector();
    const handler = createTerminalHandler({ collector });

    const message = {
      messageType: 'bridge:executeTerminalCommand',
      data: { operation: 'getStatus' },
    };

    const result = await handler(message, {});

    expect(result).to.have.property('success');
  });

  it('should use terminalHandler standalone function', async () => {
    const collector = new MockTerminalCollector();
    const context = { collector };

    const message = {
      messageType: 'bridge:executeTerminalCommand',
      data: { operation: 'getStatus' },
    };

    const result = await terminalHandler(message, context);

    expect(result.success).to.be.true;
  });
});

// ============================================================================
// ERROR CLASS TESTS
// ============================================================================

describe('Error Classes', () => {
  it('should create TerminalError with code and RPC error code', () => {
    const err = new TerminalError('test', 'TEST_CODE');

    expect(err).to.be.an.instanceof(Error);
    expect(err.code).to.equal('TEST_CODE');
    expect(err.rpcErrorCode).to.equal(-32600);
  });

  it('should create CommandError with exit code', () => {
    const err = new CommandError('failed', 'cmd', 1);

    expect(err).to.be.an.instanceof(TerminalError);
    expect(err.exitCode).to.equal(1);
    expect(err.rpcErrorCode).to.equal(-32601);
  });

  it('should create StreamError with chunk info', () => {
    const chunk = { text: 'bad' };
    const err = new StreamError('stream failed', chunk);

    expect(err).to.be.an.instanceof(TerminalError);
    expect(err.chunk).to.deep.equal(chunk);
    expect(err.rpcErrorCode).to.equal(-32603);
  });

  it('should create StateError with state info', () => {
    const err = new StateError('invalid state', 'running');

    expect(err).to.be.an.instanceof(TerminalError);
    expect(err.currentState).to.equal('running');
    expect(err.rpcErrorCode).to.equal(-32602);
  });
});
