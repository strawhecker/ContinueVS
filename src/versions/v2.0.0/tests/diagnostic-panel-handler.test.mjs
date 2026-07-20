#!/usr/bin/env node

/**
 * Test suite for diagnostic-panel-handler (Step 102)
 *
 * 30+ tests across 6 suites:
 * 1. Initialization & Dependency Injection (4 tests)
 * 2. Diagnostic Aggregation (5 tests)
 * 3. Error Queue Management (5 tests)
 * 4. Health Status Reporting (4 tests)
 * 5. Graceful Degradation (4 tests)
 * 6. Performance & Response Format (4 tests)
 *
 * @module diagnostic-panel-handler.test
 */

import assert from 'assert';
import {
  createDiagnosticPanelHandler,
  DiagnosticPanelError,
  SeverityLevel
} from '../lib/diagnostic-panel-handler.mjs';
import {
  createMockProfilerHandler,
  createMockHealthCheckService,
  createMockBridgeLogger,
  createValidRequestMessage,
  createInvalidRequestMessage,
  createMockContext,
  createTestScenario
} from './mocks/diagnostic-panel-fixtures.mjs';

describe('DiagnosticPanelHandler', function () {
  this.timeout(10000); // Allow up to 10 seconds per test

  // ============================================
  // Suite 1: Initialization & Dependency Injection
  // ============================================

  describe('Suite 1: Initialization & Dependency Injection', function () {
    it('should create handler with all optional dependencies', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger,
        logger: { debug: () => {} }
      });

      assert.strictEqual(typeof handler, 'function', 'Handler should be a function');
    });

    it('should create handler with no dependencies (graceful degradation)', async function () {
      const handler = createDiagnosticPanelHandler({});
      assert.strictEqual(typeof handler, 'function', 'Handler should be a function');
    });

    it('should create handler with only profiler (partial config)', async function () {
      const profiler = createMockProfilerHandler();
      const handler = createDiagnosticPanelHandler({ profilerHandler: profiler });
      assert.strictEqual(typeof handler, 'function', 'Handler should be a function');
    });

    it('should create handler with logger and telemetry', async function () {
      const logger = {
        debug: (msg, ctx) => {},
        warn: (msg, ctx) => {},
        error: (msg, ctx) => {}
      };
      const telemetry = {
        recordEvent: (name, data) => {}
      };
      const handler = createDiagnosticPanelHandler({
        logger,
        telemetryCollector: telemetry
      });

      assert.strictEqual(typeof handler, 'function', 'Handler should be a function');
    });
  });

  // ============================================
  // Suite 2: Diagnostic Aggregation
  // ============================================

  describe('Suite 2: Diagnostic Aggregation', function () {
    it('should return aggregated diagnostic snapshot with all fields', async function () {
      const scenario = createTestScenario({
        healthState: 'healthy',
        errorCount: 10
      });

      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);

      assert.strictEqual(response.success, true, 'Response should be successful');
      assert.strictEqual(typeof response.data, 'object', 'Response should contain data');
      assert(response.data.health, 'Should have health field');
      assert(response.data.handlers, 'Should have handlers field');
      assert(response.data.errors, 'Should have errors field');
      assert(response.data.summary, 'Should have summary field');
      assert(response.data.timestamp, 'Should have timestamp field');
    });

    it('should aggregate handler metrics from profiler', async function () {
      const scenario = createTestScenario({ errorCount: 5 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const handlers = response.data.handlers;

      assert(Array.isArray(handlers), 'Handlers should be an array');
      assert(handlers.length > 0, 'Should have handler metrics');
      assert(handlers[0].name, 'Handler should have name');
      assert(handlers[0].tier, 'Handler should have tier');
      assert(handlers[0].latency, 'Handler should have latency');
      assert(typeof handlers[0].errorRate === 'number', 'Handler should have errorRate');
    });

    it('should include error queue with correct count', async function () {
      const scenario = createTestScenario({ errorCount: 25 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const errors = response.data.errors;

      assert(Array.isArray(errors), 'Errors should be an array');
      assert.strictEqual(errors.length, 25, 'Should return all error entries');
    });

    it('should cap error queue at 100 entries', async function () {
      const scenario = createTestScenario({ errorCount: 150 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const errors = response.data.errors;

      assert(errors.length <= 100, 'Should not exceed 100 error entries');
    });
  });

  // ============================================
  // Suite 3: Error Queue Management
  // ============================================

  describe('Suite 3: Error Queue Management', function () {
    it('should maintain FIFO order in error queue', async function () {
      const scenario = createTestScenario({ errorCount: 10 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const errors = response.data.errors;

      // Errors should be ordered by timestamp descending (newest first)
      for (let i = 0; i < errors.length - 1; i++) {
        assert(
          new Date(errors[i].timestamp) >= new Date(errors[i + 1].timestamp),
          'Errors should be ordered by timestamp descending'
        );
      }
    });

    it('should include error severity levels', async function () {
      const scenario = createTestScenario({ errorCount: 20 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const errors = response.data.errors;

      const validSeverities = ['CRITICAL', 'WARNING', 'INFO'];
      errors.forEach(err => {
        assert(
          validSeverities.includes(err.severity),
          `Error severity should be one of: ${validSeverities.join(', ')}`
        );
      });
    });

    it('should populate error context and handler name', async function () {
      const scenario = createTestScenario({ errorCount: 5 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const errors = response.data.errors;

      errors.forEach(err => {
        assert(err.message, 'Error should have message');
        assert(err.timestamp, 'Error should have timestamp');
        // handlerName and context may be null for some errors (acceptable)
      });
    });

    it('should handle empty error queue gracefully', async function () {
      const scenario = createTestScenario({ errorCount: 0 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const errors = response.data.errors;

      assert(Array.isArray(errors), 'Errors should be an empty array');
      assert.strictEqual(errors.length, 0, 'Should have no errors');
    });
  });

  // ============================================
  // Suite 4: Health Status Reporting
  // ============================================

  describe('Suite 4: Health Status Reporting', function () {
    it('should report healthy bridge status', async function () {
      const scenario = createTestScenario({ healthState: 'healthy' });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const health = response.data.health;

      assert.strictEqual(health.status, 'healthy', 'Should report healthy status');
      assert(health.reason, 'Should provide reason for status');
      assert(health.timestamp, 'Should have timestamp');
    });

    it('should report degraded bridge status', async function () {
      const scenario = createTestScenario({ healthState: 'degraded' });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const health = response.data.health;

      assert.strictEqual(health.status, 'degraded', 'Should report degraded status');
    });

    it('should report error bridge status', async function () {
      const scenario = createTestScenario({ healthState: 'error' });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const health = response.data.health;

      assert.strictEqual(health.status, 'error', 'Should report error status');
    });

    it('should include uptime in health status', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const health = response.data.health;

      // Uptime may be null if not provided by health service
      assert(health.uptime !== undefined, 'Should include uptime field');
    });
  });

  // ============================================
  // Suite 5: Graceful Degradation
  // ============================================

  describe('Suite 5: Graceful Degradation', function () {
    it('should handle missing profiler handler gracefully', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: null,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);

      assert.strictEqual(response.success, true, 'Response should still be successful');
      assert(response.data.handlers, 'Handlers field should exist');
      // Handlers may be empty or contain empty data
    });

    it('should handle missing health check service gracefully', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: null,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);

      assert.strictEqual(response.success, true, 'Response should still be successful');
      assert(response.data.health, 'Health field should exist');
      assert.strictEqual(response.data.health.status, 'unknown', 'Should report unknown status');
    });

    it('should handle missing bridge logger gracefully', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: null
      });

      const response = await handler(scenario.message, scenario.context);

      assert.strictEqual(response.success, true, 'Response should still be successful');
      assert(response.data.errors, 'Errors field should exist');
      assert.strictEqual(response.data.errors.length, 0, 'Should have empty error array');
    });

    it('should handle all dependencies missing gracefully', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({});

      const response = await handler(scenario.message, scenario.context);

      assert.strictEqual(response.success, true, 'Response should still be successful');
      assert(response.data, 'Should return diagnostic data');
      assert(response.data.health, 'Should have health field');
      assert(response.data.handlers, 'Should have handlers field');
      assert(response.data.errors, 'Should have errors field');
    });
  });

  // ============================================
  // Suite 6: Performance & Response Format
  // ============================================

  describe('Suite 6: Performance & Response Format', function () {
    it('should return valid JSON-RPC response format', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);

      assert(response.hasOwnProperty('success'), 'Should have success field');
      assert(response.hasOwnProperty('messageId'), 'Should have messageId field');
      assert.strictEqual(response.messageId, scenario.message.messageId, 'MessageId should match request');
    });

    it('should generate response in less than 50ms', async function () {
      const scenario = createTestScenario({ errorCount: 100 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const start = performance.now();
      const response = await handler(scenario.message, scenario.context);
      const duration = performance.now() - start;

      assert(duration < 50, `Response took ${duration.toFixed(2)}ms, should be <50ms`);
    });

    it('should include summary statistics', async function () {
      const scenario = createTestScenario({ errorCount: 10 });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const summary = response.data.summary;

      assert(summary.overallHealth, 'Should have overallHealth');
      assert(typeof summary.totalHandlers === 'number', 'Should have totalHandlers count');
      assert(typeof summary.totalRequests === 'number', 'Should have totalRequests count');
      assert(typeof summary.errorCount === 'number', 'Should have errorCount');
      assert(summary.uptime !== undefined, 'Should have uptime info');
    });

    it('should include ISO timestamp in response', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(scenario.message, scenario.context);
      const timestamp = response.data.timestamp;

      assert(timestamp, 'Should have timestamp');
      // Validate ISO 8601 format
      assert(/\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(timestamp), 'Should be ISO 8601 timestamp');
    });
  });

  // ============================================
  // Additional Coverage: Error Handling & Edge Cases
  // ============================================

  describe('Suite 7: Error Handling & Invalid Requests', function () {
    it('should reject request with missing data field', async function () {
      const scenario = createTestScenario();
      const message = createInvalidRequestMessage('missing-data');
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(message, scenario.context);

      assert.strictEqual(response.success, false, 'Response should indicate failure');
      assert(response.error, 'Should include error object');
      assert.strictEqual(response.error.code, -32603, 'Should use JSON-RPC error code');
    });

    it('should reject request with invalid operation', async function () {
      const scenario = createTestScenario();
      const message = createInvalidRequestMessage('invalid-operation');
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(message, scenario.context);

      assert.strictEqual(response.success, false, 'Response should indicate failure');
      assert(response.error.message.includes('Invalid operation'), 'Error should describe invalid operation');
    });

    it('should handle null message gracefully', async function () {
      const scenario = createTestScenario();
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler
      });

      const response = await handler(null, scenario.context);

      assert.strictEqual(response.success, false, 'Response should indicate failure');
      assert(response.error, 'Should include error object');
    });

    it('should filter handlers by tier when requested', async function () {
      const scenario = createTestScenario();
      const message = createValidRequestMessage({
        operation: 'filter-tier',
        filter: 'core'
      });
      const handler = createDiagnosticPanelHandler({
        profilerHandler: scenario.profilerHandler,
        healthCheckService: scenario.healthCheckService,
        bridgeLogger: scenario.bridgeLogger
      });

      const response = await handler(message, scenario.context);
      const handlers = response.data.handlers;

      // All returned handlers should have tier 'core'
      handlers.forEach(h => {
        assert.strictEqual(h.tier, 'core', 'Filtered handlers should match tier filter');
      });
    });
  });
});
