#!/usr/bin/env node

/**
 * ContinueVS Bridge - core-server.js
 * 
 * Entry point for the npm-based Continue bridge.
 * 
 * Architecture:
 * 
 *   IDE (Visual Studio, C#)
 *        ↓ [stdio Parent Process]
 *        ↓
 *   core-server.js (Node.js)
 *        ├─ Validates npm package integrity (Step 12)
 *        ├─ Spawns `continue` binary (child process)
 *        └─ Relays line-delimited JSON-RPC between IDE and Continue
 *             ↓ [stdin/stdout pipes]
 *             ↓
 *        Continue Process
 *             ├─ Plugin SDK server
 *             └─ Chat/autocomplete logic
 * 
 * Message Protocol:
 *   Line-delimited JSON with shape:
 *   {
 *     "messageType": "string (e.g., 'ping', 'getEditorState', 'onEditorStateChange')",
 *     "messageId": "string (correlation UUID)",
 *     "data": { /* payload (TBD per message type) */ }
 *   }
 * 
 * Lifecycle:
 *   1. Parse CLI args (--version, --health-check, --log-level, --log-dir)
 *   2. Initialize logger and health check (dependency injection stubs)
 *   3. Validate npm package integrity
 *   4. Create logs directory
 *   5. Spawn Continue binary (child process)
 *   6. Establish stdio relay
 *   7. Wait for graceful shutdown (SIGTERM, SIGINT) or crash
 *   8. Cleanup and exit
 * 
 * Error Recovery:
 *   - Continue process crashes → Restart with exponential backoff
 *   - Backoff: 100ms → 500ms → 2000ms (max 3 retries)
 *   - After 3 failures → Report to IDE and stop respawning
 * 
 * Step Dependencies:
 *   - Step 2: package.json (ESM, scripts, dependencies)
 *   - Step 12: npm package validation (integrity checks)
 *   - Step 24: health check service (placeholder)
 *   - Step 25: logger facade (placeholder)
 *   - Step 26: telemetry collector (placeholder)
 *   - Step 27+: unit tests
 */

import { spawn } from 'child_process';
import { createInterface } from 'readline';
import { createWriteStream } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join, resolve } from 'path';
import { existsSync, mkdirSync } from 'fs';
import process from 'process';
import { randomUUID } from 'crypto';

// Step 14: Handler Dispatcher for message routing
import HandlerDispatcher from './lib/handler-dispatcher.js';

// Step 71: Handler registration orchestrator
import { registerAllHandlersWithDispatcher } from './lib/register-handlers.mjs';

// ============================================================================
// Configuration & Constants
// ============================================================================

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const BRIDGE_VERSION = '2.0.0';
const CONTINUE_PACKAGE_VERSION = '2.0.5';

// Exponential backoff configuration
const RESTART_CONFIG = {
  maxRetries: 3,
  backoffMs: [100, 500, 2000], // Delays for attempt 1, 2, 3
};

// Message protocol constants
const MESSAGE_PROTOCOL_VERSION = '1.0';

// ============================================================================
// Logger Facade (Placeholder for Step 25)
// ============================================================================

/**
 * Placeholder logger that will be replaced by Step 25 implementation.
 * For now, logs to console and optionally to file.
 */
class BridgeLogger {
  constructor(logDir = null, logLevel = 'info') {
    this.logLevel = logLevel;
    this.logDir = logDir;
    this.fileHandle = null;
    this.levels = { debug: 0, info: 1, warn: 2, error: 3 };
    this.currentLevel = this.levels[logLevel] || 1;

    if (logDir && existsSync(logDir)) {
      try {
        const logFile = join(logDir, `bridge.log`);
        this.fileHandle = createWriteStream(logFile, { flags: 'a' });
      } catch (err) {
        console.error(`[LOGGER] Failed to open log file: ${err.message}`);
      }
    }
  }

  _log(level, message, data = null) {
    const levelNum = this.levels[level] || 1;
    if (levelNum < this.currentLevel) return;

    const timestamp = new Date().toISOString();
    const prefix = `[${timestamp}] [${level.toUpperCase()}]`;
    const msg = data ? `${message} ${JSON.stringify(data)}` : message;
    const output = `${prefix} ${msg}`;

    console.error(output);
    if (this.fileHandle) {
      this.fileHandle.write(output + '\n');
    }
  }

  debug(message, data) { this._log('debug', message, data); }
  info(message, data) { this._log('info', message, data); }
  warn(message, data) { this._log('warn', message, data); }
  error(message, data) { this._log('error', message, data); }

  close() {
    if (this.fileHandle) {
      this.fileHandle.end();
    }
  }
}

// ============================================================================
// Health Check Service (Placeholder for Step 24)
// ============================================================================

/**
 * Placeholder health check that will be replaced by Step 24 implementation.
 */
class HealthCheckService {
  constructor(logger) {
    this.logger = logger;
    this.lastCheck = null;
    this.status = 'initializing';
  }

  async performHealthCheck() {
    this.lastCheck = new Date();
    this.status = 'healthy';
    this.logger.debug('Health check performed');
    return { status: this.status, timestamp: this.lastCheck };
  }

  getStatus() {
    return {
      status: this.status,
      lastCheck: this.lastCheck,
    };
  }
}

// ============================================================================
// BridgeServer Class
// ============================================================================

/**
 * Main bridge server that manages:
 *   - Continue process lifecycle
 *   - stdio message relay (line-delimited JSON)
 *   - Error recovery with exponential backoff
 *   - Graceful shutdown
 */
class BridgeServer {
  constructor(config) {
    this.config = config;
    this.logger = config.logger;
    this.healthCheck = config.healthCheck;

    this.continueProcess = null;
    this.stdinLineReader = null;
    this.restartCount = 0;
    this.lastRestartTime = null;
    this.isShuttingDown = false;
    this.messageCount = 0;

    // Metrics (for Step 26 telemetry)
    this.metrics = {
      messagesFromContinue: 0,
      messagesToContinue: 0,
      errors: 0,
      restarts: 0,
      startTime: Date.now(),
    };

    // Step 14: Initialize handler dispatcher for bridge message routing
    this.dispatcher = new HandlerDispatcher({
      logger: this.logger,
      metrics: null, // Step 26 will inject metrics
      server: this,
    });
  }

  /**
   * Start the bridge server:
   *   1. Validate npm packages
   *   2. Create logs directory
   *   3. Spawn Continue process
   *   4. Establish stdio relay
   *   5. Setup signal handlers
   */
  async start() {
    try {
      this.logger.info('Starting ContinueVS Bridge', {
        version: BRIDGE_VERSION,
        continueVersion: CONTINUE_PACKAGE_VERSION,
      });

      // Step 12: Validate npm package integrity (delegate)
      await this._validateNpmPackages();

      // Create logs directory if not exists
      const logsDir = this.config.logsDir;
      if (!existsSync(logsDir)) {
        mkdirSync(logsDir, { recursive: true });
        this.logger.info('Created logs directory', { path: logsDir });
      }

      // Step 71: Register all handlers with dispatcher (before spawning Continue)
      const registrationResult = await registerAllHandlersWithDispatcher(this);
      if (!registrationResult.success) {
        this.logger.warn('Handler registration completed with errors', {
          count: registrationResult.count,
          errorCount: registrationResult.errors.length,
          duration: registrationResult.duration,
        });
      }

      // Spawn Continue process
      await this._spawnContinue();

      // Setup signal handlers for graceful shutdown
      this._setupSignalHandlers();

      this.logger.info('Bridge server started successfully');
      this.healthCheck.status = 'healthy';
    } catch (err) {
      this.logger.error('Failed to start bridge server', { error: err.message });
      throw err;
    }
  }

  /**
   * Stop the bridge server and cleanup resources.
   */
  async stop() {
    if (this.isShuttingDown) return;
    this.isShuttingDown = true;

    this.logger.info('Stopping bridge server');

    if (this.continueProcess) {
      return new Promise((resolve) => {
        this.continueProcess.on('exit', () => {
          this._cleanup();
          resolve();
        });

        // Graceful termination: SIGTERM with 5-second timeout
        this.continueProcess.kill('SIGTERM');
        setTimeout(() => {
          if (!this.continueProcess.killed) {
            this.logger.warn('Forcing Continue process termination');
            this.continueProcess.kill('SIGKILL');
          }
          this._cleanup();
          resolve();
        }, 5000);
      });
    } else {
      this._cleanup();
    }
  }

  /**
   * Send a message to the Continue process.
   * @param {Object} message - Message object with messageType, messageId, data
   */
  sendMessage(message) {
    if (!this.continueProcess || this.continueProcess.killed) {
      this.logger.warn('Attempted to send message to dead Continue process');
      return;
    }

    const json = JSON.stringify(message) + '\n';
    this.continueProcess.stdin.write(json, 'utf-8', (err) => {
      if (err) {
        this.logger.error('Failed to send message to Continue', { error: err.message });
        this.metrics.errors++;
      } else {
        this.metrics.messagesToContinue++;
      }
    });
  }

  /**
   * Get current metrics (for Step 26 telemetry).
   */
  getMetrics() {
    return {
      ...this.metrics,
      uptime: Date.now() - this.metrics.startTime,
      restartCount: this.restartCount,
    };
  }

  /**
   * Get health status (for Step 24).
   */
  getHealthStatus() {
    return {
      ...this.healthCheck.getStatus(),
      metrics: this.getMetrics(),
    };
  }

  /**
   * Register a handler with the dispatcher (used by Step 71).
   * 
   * @param {string} messageType - Message type to handle (e.g., "bridge:getEditorState")
   * @param {Function} handler - Handler function (async)
   * @throws {Error} If handler already registered for this type
   */
  registerHandler(messageType, handler) {
    this.dispatcher.register(messageType, handler);
  }

  /**
   * Get dispatcher diagnostics (for debugging/telemetry).
   * 
   * @returns {Object} Diagnostics including handler count and list
   */
  getDispatcherDiagnostics() {
    return this.dispatcher.getDiagnostics();
  }

  /**
   * Dispatch a message through the handler registry.
   * Used by Step 15 (handler adapter) to route IDE input.
   * 
   * @param {Object} message - Message to dispatch
   * @returns {Promise<Object>} Dispatch result with routing decision
   */
  async dispatchMessage(message) {
    return this.dispatcher.dispatch(message);
  }

  // ========================================================================
  // Private Methods
  // ========================================================================

  /**
   * Validate npm package integrity (delegates to Step 12 utility).
   * For now, this is a stub.
   */
  async _validateNpmPackages() {
    // TODO (Step 12): Load and run npm-validate.mjs
    this.logger.debug('Validating npm packages (stub for Step 12)');
    // Example: const validator = await import('./lib/npm-validate.mjs');
    // await validator.checkPackageIntegrity();
  }

  /**
   * Spawn the Continue binary as a child process.
   */
  async _spawnContinue() {
    return new Promise((resolve, reject) => {
      try {
        const continueBin = join(__dirname, 'node_modules', '.bin', 'continue');

        this.logger.debug('Spawning Continue process', { binary: continueBin });

        this.continueProcess = spawn(continueBin, [], {
          stdio: ['pipe', 'pipe', 'pipe'],
          cwd: __dirname,
          env: {
            ...process.env,
            NODE_ENV: 'production',
            BRIDGE_VERSION: BRIDGE_VERSION,
          },
        });

        // Setup stdio relay
        this._setupStdioRelay();

        // Error handling
        this.continueProcess.on('error', (err) => {
          this.logger.error('Continue process error', { error: err.message });
          this.metrics.errors++;
          reject(err);
        });

        // Exit handling
        this.continueProcess.on('exit', (code, signal) => {
          this.logger.warn('Continue process exited', { code, signal });
          if (!this.isShuttingDown) {
            this._attemptRestart();
          }
        });

        // Give process 2 seconds to start before resolving
        setTimeout(() => resolve(), 2000);
      } catch (err) {
        reject(err);
      }
    });
  }

  /**
   * Setup stdio relay:
   *   - Read line-delimited JSON from Continue stdout
   *   - Parse and relay to IDE stdout
   *   - Capture stderr for logging
   */
  _setupStdioRelay() {
    // Setup readline for Continue stdout (line-delimited JSON)
    this.stdinLineReader = createInterface({
      input: this.continueProcess.stdout,
      crlfDelay: Infinity,
    });

    this.stdinLineReader.on('line', (line) => {
      this._onContinueOutput(line);
    });

    this.stdinLineReader.on('error', (err) => {
      this.logger.error('Error reading Continue stdout', { error: err.message });
      this.metrics.errors++;
    });

    // Capture stderr
    this.continueProcess.stderr.on('data', (chunk) => {
      this._onContinueError(chunk);
    });
  }

  /**
   * Handle output from Continue process.
   * @param {string} line - Line of JSON from Continue
   */
  _onContinueOutput(line) {
    if (!line.trim()) return;

    try {
      const message = JSON.parse(line);

      // Validate message shape
      if (!message.messageType || !message.messageId) {
        this.logger.warn('Invalid message from Continue: missing messageType or messageId');
        this.metrics.errors++;
        return;
      }

      this.metrics.messagesFromContinue++;

      // Relay to IDE via parent stdout (line-delimited JSON)
      console.log(JSON.stringify(message));

      this.logger.debug('Relayed message from Continue to IDE', {
        messageType: message.messageType,
        messageId: message.messageId,
      });
    } catch (err) {
      this.logger.error('Failed to parse Continue output as JSON', {
        error: err.message,
        line: line.substring(0, 100),
      });
      this.metrics.errors++;
    }
  }

  /**
   * Handle stderr from Continue process.
   * @param {Buffer} chunk - Chunk of stderr data
   */
  _onContinueError(chunk) {
    const text = chunk.toString('utf-8').trim();
    if (text) {
      this.logger.warn('Continue stderr', { text: text.substring(0, 200) });
    }
  }

  /**
   * Attempt to restart Continue process with exponential backoff.
   */
  async _attemptRestart() {
    if (this.isShuttingDown || this.restartCount >= RESTART_CONFIG.maxRetries) {
      this.logger.error('Max restart retries exceeded; giving up', {
        restartCount: this.restartCount,
      });
      this.healthCheck.status = 'failed';
      return;
    }

    const backoffMs = RESTART_CONFIG.backoffMs[this.restartCount];
    this.restartCount++;

    this.logger.info('Attempting Continue process restart', {
      attempt: this.restartCount,
      backoffMs,
    });

    await new Promise((resolve) => setTimeout(resolve, backoffMs));

    try {
      await this._spawnContinue();
      this.metrics.restarts++;
      this.logger.info('Continue process restarted successfully');
    } catch (err) {
      this.logger.error('Failed to restart Continue process', { error: err.message });
      await this._attemptRestart(); // Recursive retry
    }
  }

  /**
   * Setup signal handlers for graceful shutdown.
   */
  _setupSignalHandlers() {
    const signals = ['SIGTERM', 'SIGINT'];

    signals.forEach((signal) => {
      process.on(signal, async () => {
        this.logger.info(`Received ${signal}; initiating graceful shutdown`);
        await this.stop();
        process.exit(0);
      });
    });

    process.on('uncaughtException', (err) => {
      this.logger.error('Uncaught exception', { error: err.message, stack: err.stack });
      this.stop().then(() => process.exit(1));
    });
  }

  /**
   * Cleanup resources.
   */
  _cleanup() {
    this.logger.info('Cleaning up resources');

    if (this.stdinLineReader) {
      this.stdinLineReader.close();
      this.stdinLineReader = null;
    }

    if (this.continueProcess && !this.continueProcess.killed) {
      this.continueProcess.kill();
    }
  }
}

// ============================================================================
// CLI Argument Parsing
// ============================================================================

/**
 * Parse command-line arguments.
 * Supported:
 *   --version            : Print bridge version
 *   --health-check       : Run health check and exit
 *   --log-level <level>  : Set log level (debug|info|warn|error)
 *   --log-dir <path>     : Override log directory
 */
function parseArgs(args) {
  const config = {
    version: false,
    healthCheck: false,
    logLevel: 'info',
    logsDir: join(__dirname, 'logs'),
  };

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];

    if (arg === '--version') {
      config.version = true;
    } else if (arg === '--health-check') {
      config.healthCheck = true;
    } else if (arg === '--log-level' && i + 1 < args.length) {
      config.logLevel = args[++i];
    } else if (arg === '--log-dir' && i + 1 < args.length) {
      config.logsDir = resolve(args[++i]);
    }
  }

  return config;
}

// ============================================================================
// Main Entry Point
// ============================================================================

/**
 * Main async entry point.
 */
async function main() {
  const args = process.argv.slice(2);
  const config = parseArgs(args);

  // Handle --version flag
  if (config.version) {
    console.log(`ContinueVS Bridge v${BRIDGE_VERSION}`);
    process.exit(0);
  }

  // Initialize logger
  const logger = new BridgeLogger(config.logsDir, config.logLevel);
  const healthCheck = new HealthCheckService(logger);

  // Handle --health-check flag
  if (config.healthCheck) {
    const health = await healthCheck.performHealthCheck();
    console.log(JSON.stringify(health));
    logger.close();
    process.exit(0);
  }

  // Create server instance
  const serverConfig = {
    logger,
    healthCheck,
    logsDir: config.logsDir,
  };

  const server = new BridgeServer(serverConfig);

  try {
    await server.start();
    // Server runs until SIGTERM/SIGINT or crash
  } catch (err) {
    logger.error('Fatal error', { error: err.message });
    await server.stop();
    logger.close();
    process.exit(1);
  }
}

// Run main
main().catch((err) => {
  console.error('Fatal error in main:', err);
  process.exit(1);
});
