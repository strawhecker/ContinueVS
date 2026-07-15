#!/usr/bin/env node

/**
 * Mock Terminal Collector for Node.js Tests (Step 82)
 *
 * Provides realistic mock of C# TerminalCollector for isolated testing
 * of terminal-handler.mjs without DTE dependencies.
 *
 * Features:
 * - Async generator support for output streaming
 * - Configurable latency for realistic simulation
 * - Partial output chunking
 * - Error injection for failure scenarios
 * - Command execution simulation
 *
 * @module src/versions/v2.0.0/tests/mocks/terminal-collector-mock.mjs
 * @version 1.0.0
 */

/**
 * Mock terminal output
 * @typedef {Object} MockTerminalOutput
 * @property {string} chunk - Output text
 * @property {boolean} isPartial - True if more output expected
 * @property {boolean} isError - True if error output
 * @property {number} timestamp - When generated
 */

/**
 * Mock terminal status
 * @typedef {Object} MockTerminalStatus
 * @property {string} state - 'idle' | 'busy' | 'running' | 'error'
 * @property {boolean} isResponsive - Terminal accessible
 * @property {number} commandCount - Total commands executed
 * @property {string} lastOutput - Last output chunk
 */

/**
 * Factory function to create mock TerminalCollector
 *
 * @param {Object} config - Configuration object
 * @param {number} config.delayMs - Delay between chunks (default: 10)
 * @param {number} config.chunkSize - Characters per chunk (default: 100)
 * @param {boolean} config.shouldFail - Inject failures (default: false)
 * @param {string} config.failureType - Type of failure: 'execute'|'stream'|'sendInput'|'clear'
 * @param {string} config.failurePoint - When to fail: 'start'|'middle'|'end'
 * @returns {Object} Mock collector with async generators
 */
export function createMockTerminalCollector(config = {}) {
  const {
    delayMs = 10,
    chunkSize = 100,
    shouldFail = false,
    failureType = null,
    failurePoint = 'middle',
  } = config;

  let state = 'idle';
  let commandCount = 0;
  let lastOutput = null;
  let isResponsive = true;

  /**
   * Mock executeCommand with async generator
   *
   * @async
   * @generator
   * @param {string} command - Command text
   * @param {number} timeoutMs - Timeout (ignored in mock)
   * @param {string} workingDirectory - Working dir (ignored)
   * @yields {MockTerminalOutput} Output chunks
   */
  async function* executeCommand(command, timeoutMs, workingDirectory) {
    if (shouldFail && failureType === 'execute' && failurePoint === 'start') {
      throw new Error('Command execution failed at start');
    }

    state = 'running';

    try {
      const output = `Output of command: ${command}\nLine 2: Working directory context\nLine 3: Execution complete`;

      let chunkCount = 0;
      const totalChunks = Math.ceil(output.length / chunkSize);

      for (let i = 0; i < output.length; i += chunkSize) {
        if (
          shouldFail &&
          failureType === 'stream' &&
          failurePoint === 'middle' &&
          chunkCount === Math.floor(totalChunks / 2)
        ) {
          throw new Error('Stream error at middle chunk');
        }

        const chunk = output.substring(i, Math.min(i + chunkSize, output.length));
        const isLast = i + chunkSize >= output.length;

        const output_obj = {
          chunk,
          isPartial: !isLast,
          isError: false,
          timestamp: Date.now(),
        };

        lastOutput = chunk;
        yield output_obj;

        chunkCount++;

        if (delayMs > 0) {
          await new Promise((resolve) => setTimeout(resolve, delayMs));
        }

        if (
          shouldFail &&
          failureType === 'stream' &&
          failurePoint === 'end' &&
          isLast
        ) {
          throw new Error('Stream error at end chunk');
        }
      }

      commandCount++;
      state = 'idle';
    } catch (error) {
      state = 'error';
      isResponsive = false;
      throw error;
    }
  }

  /**
   * Mock sendInput (non-blocking)
   *
   * @async
   * @param {string} text - Input text
   */
  async function sendInput(text) {
    if (shouldFail && failureType === 'sendInput') {
      throw new Error('Send input failed');
    }

    lastOutput = text;
    // Non-blocking, just queue it
  }

  /**
   * Mock clearTerminal
   *
   * @async
   */
  async function clearTerminal() {
    if (shouldFail && failureType === 'clear') {
      throw new Error('Clear terminal failed');
    }

    lastOutput = null;
    state = 'idle';
  }

  /**
   * Mock getStatus
   *
   * @async
   * @returns {Promise<MockTerminalStatus>}
   */
  async function getStatus() {
    return {
      state,
      isResponsive,
      commandCount,
      lastOutput,
    };
  }

  return {
    executeCommand,
    sendInput,
    clearTerminal,
    getStatus,
    // Helper methods for testing
    _getState: () => state,
    _setResponsive: (val) => {
      isResponsive = val;
    },
  };
}

/**
 * Factory for creating mock logger
 *
 * @returns {Object} Mock logger with debug, warn, info methods
 */
export function createMockLogger() {
  const logs = [];

  return {
    debug: (msg) => logs.push({ level: 'debug', message: msg }),
    warn: (msg) => logs.push({ level: 'warn', message: msg }),
    info: (msg) => logs.push({ level: 'info', message: msg }),
    error: (msg) => logs.push({ level: 'error', message: msg }),
    getLogs: () => [...logs],
    clear: () => logs.splice(0, logs.length),
  };
}

/**
 * Factory for creating mock metrics collector
 *
 * @returns {Object} Mock metrics with record method
 */
export function createMockMetrics() {
  const records = [];

  return {
    record: (name, fields) => records.push({ name, fields }),
    getRecords: () => [...records],
    getRecordsByName: (name) => records.filter((r) => r.name === name),
    clear: () => records.splice(0, records.length),
  };
}

/**
 * Helper: Simulate command execution with controlled output
 *
 * @param {string} command - Command name
 * @param {number} outputLines - Number of output lines to generate
 * @returns {string} Simulated command output
 */
export function simulateCommandOutput(command, outputLines = 3) {
  const lines = [
    `Executing: ${command}`,
    `Output line 1: ${new Date().toISOString()}`,
  ];

  for (let i = 2; i < outputLines; i++) {
    lines.push(`Output line ${i}: Data row ${i}`);
  }

  lines.push('Command completed successfully');
  return lines.join('\n');
}

/**
 * Helper: Generate mock context for handler testing
 *
 * @param {Object} config - Configuration
 * @returns {Object} Handler context with collector, logger, metrics
 */
export function createMockContext(config = {}) {
  return {
    collector: createMockTerminalCollector(config),
    logger: createMockLogger(),
    metrics: createMockMetrics(),
  };
}

/**
 * Helper: Create context with injection failures
 * Useful for testing error scenarios
 *
 * @param {string} failureMode - 'noCollector'|'noLogger'|'noMetrics'
 * @returns {Object} Context with specified failures
 */
export function createFailingContext(failureMode = 'noCollector') {
  switch (failureMode) {
    case 'noCollector':
      return {
        collector: null,
        logger: createMockLogger(),
        metrics: createMockMetrics(),
      };
    case 'noLogger':
      return {
        collector: createMockTerminalCollector(),
        logger: null,
        metrics: createMockMetrics(),
      };
    case 'noMetrics':
      return {
        collector: createMockTerminalCollector(),
        logger: createMockLogger(),
        metrics: null,
      };
    default:
      return createMockContext();
  }
}

/**
 * Helper: Create context with specific error scenario
 *
 * @param {string} scenario - 'commandTimeout'|'streamError'|'inputError'|'clearError'
 * @returns {Object} Context configured for scenario
 */
export function createScenarioContext(scenario) {
  const failureMap = {
    commandTimeout: { shouldFail: true, failureType: 'stream', failurePoint: 'middle' },
    streamError: { shouldFail: true, failureType: 'stream', failurePoint: 'middle' },
    inputError: { shouldFail: true, failureType: 'sendInput' },
    clearError: { shouldFail: true, failureType: 'clear' },
    slowOutput: { delayMs: 50, chunkSize: 20 },
    largeOutput: { delayMs: 5, chunkSize: 500 },
  };

  const config = failureMap[scenario] || {};

  return {
    collector: createMockTerminalCollector(config),
    logger: createMockLogger(),
    metrics: createMockMetrics(),
  };
}

/**
 * Helper: Collect all chunks from streaming response
 *
 * @async
 * @param {AsyncIterable} stream - Async iterable of chunks
 * @returns {Promise<Array>} All chunks
 */
export async function collectChunks(stream) {
  const chunks = [];
  for await (const chunk of stream) {
    chunks.push(chunk);
  }
  return chunks;
}

/**
 * Helper: Assert all chunks have required properties
 *
 * @param {Array} chunks - Chunks to validate
 * @throws {AssertionError} If validation fails
 */
export function validateChunks(chunks) {
  if (!Array.isArray(chunks)) {
    throw new Error('chunks must be an array');
  }

  for (const chunk of chunks) {
    if (typeof chunk.text !== 'string') {
      throw new Error(`Chunk text must be string, got ${typeof chunk.text}`);
    }
    if (typeof chunk.isPartial !== 'boolean') {
      throw new Error(`Chunk isPartial must be boolean, got ${typeof chunk.isPartial}`);
    }
    if (typeof chunk.timestamp !== 'number') {
      throw new Error(`Chunk timestamp must be number, got ${typeof chunk.timestamp}`);
    }
  }

  return true;
}
