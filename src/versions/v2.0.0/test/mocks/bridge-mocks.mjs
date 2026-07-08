/**
 * bridge-mocks.mjs
 * 
 * Mock factories and stubs for bridge component testing.
 * Provides mocks for stdio transport, process, server, and message protocol.
 * 
 * Usage:
 *   import {
 *     mockStdioTransport,
 *     mockContinueProcess,
 *     mockBridgeServer,
 *     createJsonRpcMessage,
 *   } from './mocks/bridge-mocks.mjs';
 *   
 *   const transport = mockStdioTransport();
 *   const process = mockContinueProcess();
 *   const server = mockBridgeServer();
 */

import { EventEmitter } from 'events';
import { Duplex } from 'stream';

/**
 * Creates a mock stdio transport (Duplex stream).
 * @param {Object} options - Configuration
 * @param {string[]} [options.lines=[]] - Initial stdout lines to return
 * @returns {Object} Mock transport object with stdin/stdout/stderr
 */
export function mockStdioTransport(options = {}) {
  const { lines = [] } = options;

  // Create duplex streams for input/output
  const stdin = new Duplex({
    write(chunk, encoding, callback) {
      callback();
    },
    read() {},
  });

  const stdout = new Duplex({
    write(chunk, encoding, callback) {
      callback();
    },
    read() {},
  });

  // Push initial lines
  for (const line of lines) {
    stdout.push(`${line}\n`);
  }

  const stderr = new Duplex({
    write(chunk, encoding, callback) {
      callback();
    },
    read() {},
  });

  return {
    stdin,
    stdout,
    stderr,
    write: (data) => stdin.push(data),
    send: async (message) => stdout.push(JSON.stringify(message) + '\n'),
    close: async () => {
      stdin.end();
      stdout.end();
      stderr.end();
    },
  };
}

/**
 * Creates a mock child_process.spawn() result.
 * @param {Object} options - Configuration
 * @param {number} [options.pid=1234] - Process ID
 * @param {number} [options.exitCode=0] - Exit code
 * @returns {Object} Mock ChildProcess object
 */
export function mockContinueProcess(options = {}) {
  const {
    pid = 1234,
    exitCode = 0,
  } = options;

  const emitter = new EventEmitter();
  const transport = mockStdioTransport();

  return {
    pid,
    exitCode,
    stdin: transport.stdin,
    stdout: transport.stdout,
    stderr: transport.stderr,
    killed: false,
    _killed: false,

    // EventEmitter methods
    on: (event, listener) => emitter.on(event, listener),
    once: (event, listener) => emitter.once(event, listener),
    removeListener: (event, listener) => emitter.removeListener(event, listener),
    emit: (event, ...args) => emitter.emit(event, ...args),

    // Process methods
    kill: function(signal = 'SIGTERM') {
      this.killed = true;
      this._killed = true;
      this.exitCode = 1;
      emitter.emit('exit', 1, signal);
    },

    send: async function(data) {
      if (this.killed) {
        throw new Error('Process already killed');
      }
      await transport.send(data);
    },

    disconnect: function() {
      emitter.emit('disconnect');
    },

    unref: function() {
      // No-op
    },

    ref: function() {
      // No-op
    },

    // Helper for tests
    simulateMessage: async function(message) {
      transport.stdout.push(JSON.stringify(message) + '\n');
    },

    simulateExit: function(code = 0) {
      this.exitCode = code;
      emitter.emit('exit', code, null);
    },

    simulateStderr: function(message) {
      transport.stderr.push(message + '\n');
    },
  };
}

/**
 * Creates a mock bridge server (EventEmitter with request/response protocol).
 * @param {Object} options - Configuration
 * @returns {Object} Mock server object
 */
export function mockBridgeServer(options = {}) {
  const emitter = new EventEmitter();
  const handlers = new Map();

  return {
    // EventEmitter methods
    on: (event, listener) => emitter.on(event, listener),
    once: (event, listener) => emitter.once(event, listener),
    removeListener: (event, listener) => emitter.removeListener(event, listener),
    emit: (event, ...args) => emitter.emit(event, ...args),

    // Server state
    isRunning: false,
    isHealthy: true,
    connectionCount: 0,

    // Server methods
    start: async function() {
      this.isRunning = true;
      emitter.emit('started');
    },

    stop: async function() {
      this.isRunning = false;
      emitter.emit('stopped');
    },

    // Request/response handling
    registerHandler: function(method, handler) {
      handlers.set(method, handler);
    },

    getHandler: function(method) {
      return handlers.get(method);
    },

    handleRequest: async function(message) {
      const { id, method, params } = message;
      const handler = handlers.get(method);

      if (!handler) {
        return createJsonRpcError(id, -32601, `Method not found: ${method}`);
      }

      try {
        const result = await handler(params);
        return createJsonRpcResponse(id, result);
      } catch (err) {
        return createJsonRpcError(id, -32603, err.message || 'Internal server error');
      }
    },

    // Health check
    setHealth: function(healthy) {
      this.isHealthy = healthy;
      emitter.emit('health-changed', healthy);
    },

    // Connection simulation
    simulateConnection: function() {
      this.connectionCount++;
      emitter.emit('connection');
    },

    simulateDisconnection: function() {
      this.connectionCount = Math.max(0, this.connectionCount - 1);
      emitter.emit('disconnection');
    },
  };
}

/**
 * Creates a JSON-RPC 2.0 request message.
 * @param {number|string} id - Request ID
 * @param {string} method - RPC method name
 * @param {Object} params - RPC parameters
 * @returns {Object} JSON-RPC request
 */
export function createJsonRpcRequest(id, method, params = {}) {
  return {
    jsonrpc: '2.0',
    id,
    method,
    params,
  };
}

/**
 * Creates a JSON-RPC 2.0 response message.
 * @param {number|string} id - Request ID
 * @param {any} result - Response result
 * @returns {Object} JSON-RPC response
 */
export function createJsonRpcResponse(id, result) {
  return {
    jsonrpc: '2.0',
    id,
    result,
  };
}

/**
 * Creates a JSON-RPC 2.0 error response.
 * @param {number|string} id - Request ID
 * @param {number} code - Error code
 * @param {string} message - Error message
 * @param {any} data - Optional error data
 * @returns {Object} JSON-RPC error response
 */
export function createJsonRpcError(id, code, message, data = null) {
  const error = {
    code,
    message,
  };
  if (data !== null) {
    error.data = data;
  }
  return {
    jsonrpc: '2.0',
    id,
    error,
  };
}

/**
 * Creates a JSON-RPC 2.0 notification (no response expected).
 * @param {string} method - Notification method name
 * @param {Object} params - Notification parameters
 * @returns {Object} JSON-RPC notification
 */
export function createJsonRpcNotification(method, params = {}) {
  return {
    jsonrpc: '2.0',
    method,
    params,
  };
}

/**
 * Validates a message against JSON-RPC 2.0 format.
 * @param {Object} message - Message to validate
 * @returns {boolean} True if valid JSON-RPC message
 */
export function isValidJsonRpc(message) {
  if (!message || typeof message !== 'object') {
    return false;
  }
  if (message.jsonrpc !== '2.0') {
    return false;
  }
  // Either request/response/error, must have method for request, result/error for response
  if (message.method) {
    return typeof message.method === 'string';
  }
  if ('result' in message || 'error' in message) {
    return true;
  }
  return false;
}

/**
 * Creates a mock bridge configuration object.
 * @param {Object} options - Configuration options
 * @returns {Object} Mock configuration
 */
export function mockBridgeConfig(options = {}) {
  return {
    version: options.version || '2.0.0',
    debugMode: options.debugMode || false,
    enableTelemetry: options.enableTelemetry !== false,
    logLevel: options.logLevel || 'info',
    timeout: options.timeout || 5000,
    retryCount: options.retryCount || 3,
    ...options,
  };
}

export default {
  mockStdioTransport,
  mockContinueProcess,
  mockBridgeServer,
  createJsonRpcRequest,
  createJsonRpcResponse,
  createJsonRpcError,
  createJsonRpcNotification,
  isValidJsonRpc,
  mockBridgeConfig,
};
