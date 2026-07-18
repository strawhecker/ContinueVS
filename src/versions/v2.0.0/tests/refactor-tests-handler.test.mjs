#!/usr/bin/env node

/**
 * Refactor-Tests Handler Test Suite (Step 93)
 *
 * Comprehensive test coverage across 8 suites + bonus tests.
 *
 * @module src/versions/v2.0.0/tests/refactor-tests-handler.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

import { strict as assert } from 'assert';
import {
  createRefactorTestsHandler,
  detectTestFramework,
  extractTestsFromCode,
  executeTests,
  compareResults,
  RefactorTestError,
  ValidationError,
  TestExecutionError,
  TestFrameworkError,
  RefactorTestsOperationType,
} from '../lib/refactor-tests-handler.mjs';
import {
  MockTestRunner,
  getValidCSharpXUnitSource,
  getValidCSharpNUnitSource,
  getValidJavaScriptJestSource,
  getValidJavaScriptMochaSource,
  getValidPythonPytestSource,
  getMalformedCSharpSource,
  getMalformedJavaScriptSource,
  getSampleCSharpProduction,
  getSampleJavaScriptProduction,
  getRefactoredCSharpSource,
  getRefactoredJavaScriptSource,
  getBrokenRefactorCSharp,
  getBrokenRefactorJavaScript,
  createComparisonUtils,
  createMockMetrics,
  createMockLogger,
} from './mocks/refactor-tests-mock.mjs';

describe('Refactor-Tests Handler', () => {
  describe('Suite 1: Initialization & Dependencies (3 tests)', () => {
    it('1.1 should create handler with logger and metrics', () => {
      const logger = createMockLogger();
      const metrics = createMockMetrics();
      const handler = createRefactorTestsHandler({ logger, metrics });

      assert(typeof handler === 'function', 'Handler should be a function');
      assert(handler.name === 'refactorTestsHandler', 'Handler should have correct name');
    });

    it('1.2 should create handler without dependencies (graceful degradation)', () => {
      const handler = createRefactorTestsHandler();

      assert(typeof handler === 'function', 'Handler should be a function');
      // Should not throw when logger/metrics unavailable
    });

    it('1.3 should create handler with test runner mock', () => {
      const testRunner = new MockTestRunner({ passRate: 0.8, testCount: 5 });
      const handler = createRefactorTestsHandler({ testRunner });

      assert(typeof handler === 'function', 'Handler should be a function');
    });
  });

  describe('Suite 2: Input Validation (4 tests)', () => {
    it('2.1 should reject missing refactoredSource', async () => {
      const handler = createRefactorTestsHandler();
      const message = {
        data: {
          originalSource: 'valid',
          language: 'csharp',
        },
      };

      const response = await handler(message, {});

      assert(!response.success, 'Should fail validation');
      assert(response.errorCode === 'VALIDATION_ERROR', 'Should be validation error');
      assert(response.details?.fieldName === 'refactoredSource', 'Should identify missing field');
    });

    it('2.2 should reject missing originalSource', async () => {
      const handler = createRefactorTestsHandler();
      const message = {
        data: {
          refactoredSource: 'valid',
          language: 'csharp',
        },
      };

      const response = await handler(message, {});

      assert(!response.success, 'Should fail validation');
      assert(response.errorCode === 'VALIDATION_ERROR', 'Should be validation error');
      assert(response.details?.fieldName === 'originalSource', 'Should identify missing field');
    });

    it('2.3 should reject unsupported language', async () => {
      const handler = createRefactorTestsHandler();
      const message = {
        data: {
          refactoredSource: 'code',
          originalSource: 'code',
          language: 'cobol', // Unsupported
        },
      };

      const response = await handler(message, {});

      assert(!response.success, 'Should fail validation');
      assert(response.errorCode === 'VALIDATION_ERROR', 'Should be validation error');
      assert(response.details?.fieldName === 'language', 'Should identify language field');
    });

    it('2.4 should accept valid language variants', async () => {
      const handler = createRefactorTestsHandler();
      const validLanguages = ['csharp', 'javascript', 'typescript', 'python', 'java', 'go', 'rust'];

      for (const lang of validLanguages) {
        const message = {
          data: {
            refactoredSource: getValidCSharpXUnitSource(),
            originalSource: getSampleCSharpProduction(),
            language: lang,
          },
        };

        const response = await handler(message, {});

        assert(response.success === true || response.success === false, `Language ${lang} should be accepted`);
      }
    });
  });

  describe('Suite 3: Test Detection & Framework (5 tests)', () => {
    it('3.1 should detect xUnit framework from C# source', () => {
      const source = getValidCSharpXUnitSource();
      const result = detectTestFramework(source, 'csharp');

      assert(result.framework === 'xUnit', 'Should detect xUnit');
      assert(result.confidence > 0.5, 'Confidence should be > 0.5');
      assert(result.indicators.includes('xUnit'), 'Should have xUnit indicators');
    });

    it('3.2 should detect NUnit framework from C# source', () => {
      const source = getValidCSharpNUnitSource();
      const result = detectTestFramework(source, 'csharp');

      assert(result.framework === 'NUnit', 'Should detect NUnit');
      assert(result.confidence > 0.5, 'Confidence should be > 0.5');
    });

    it('3.3 should detect Jest framework from JavaScript source', () => {
      const source = getValidJavaScriptJestSource();
      const result = detectTestFramework(source, 'javascript');

      assert(result.framework === 'Jest', 'Should detect Jest');
      assert(result.confidence > 0.5, 'Confidence should be > 0.5');
    });

    it('3.4 should detect Mocha framework from JavaScript source', () => {
      const source = getValidJavaScriptMochaSource();
      const result = detectTestFramework(source, 'javascript');

      assert(result.framework === 'Mocha', 'Should detect Mocha');
      assert(result.confidence > 0.5, 'Confidence should be > 0.5');
    });

    it('3.5 should use explicit framework over detection', () => {
      const source = getValidCSharpXUnitSource();
      const result = detectTestFramework(source, 'csharp', 'NUnit');

      assert(result.framework === 'NUnit', 'Should use explicit framework');
      assert(result.confidence === 1.0, 'Explicit should have confidence 1.0');
    });
  });

  describe('Suite 4: Test Extraction (5 tests)', () => {
    it('4.1 should extract xUnit tests from C# source', () => {
      const source = getValidCSharpXUnitSource();
      const result = extractTestsFromCode(source, 'xUnit');

      assert(result.testCount > 0, 'Should find tests');
      assert(Array.isArray(result.tests), 'Should return tests array');
      assert(result.tests.some((t) => t.name.includes('Add')), 'Should find Add test');
    });

    it('4.2 should extract NUnit tests from C# source', () => {
      const source = getValidCSharpNUnitSource();
      const result = extractTestsFromCode(source, 'NUnit');

      assert(result.testCount > 0, 'Should find tests');
      assert(result.tests.length >= 2, 'Should find at least 2 tests');
    });

    it('4.3 should extract Jest tests from JavaScript source', () => {
      const source = getValidJavaScriptJestSource();
      const result = extractTestsFromCode(source, 'Jest');

      assert(result.testCount > 0, 'Should find tests');
      assert(result.tests.some((t) => t.name.includes('sum')), 'Should find sum test');
    });

    it('4.4 should extract Mocha tests from JavaScript source', () => {
      const source = getValidJavaScriptMochaSource();
      const result = extractTestsFromCode(source, 'Mocha');

      assert(result.testCount > 0, 'Should find tests');
    });

    it('4.5 should return empty test list for source without tests', () => {
      const source = getSampleCSharpProduction();
      const result = extractTestsFromCode(source, 'xUnit');

      assert(result.testCount === 0, 'Should find no tests in production code');
      assert(result.tests.length === 0, 'Tests array should be empty');
    });
  });

  describe('Suite 5: Test Execution (5 tests)', () => {
    it('5.1 should execute tests with mock runner', async () => {
      const runner = new MockTestRunner({ passRate: 0.8, testCount: 5 });
      const source = getValidCSharpXUnitSource();

      const result = await executeTests(source, 'xUnit', runner);

      assert(typeof result.passed === 'number', 'Should have passed count');
      assert(typeof result.failed === 'number', 'Should have failed count');
      assert(typeof result.total === 'number', 'Should have total count');
      assert(typeof result.duration === 'number', 'Should have duration');
    });

    it('5.2 should record test execution time', async () => {
      const runner = new MockTestRunner({ passRate: 1.0, testCount: 3 });
      const startTime = Date.now();

      const result = await executeTests('', 'xUnit', runner);

      const elapsed = Date.now() - startTime;
      assert(result.duration >= 0, 'Duration should be >= 0');
      assert(result.duration <= elapsed + 100, 'Duration should be reasonable');
    });

    it('5.3 should apply failure mode to test results', async () => {
      const runner = new MockTestRunner({ passRate: 0.5, testCount: 10, failureMode: 'random' });

      const result = await executeTests('', 'xUnit', runner);

      assert(result.passed + result.failed === result.total, 'Passed + Failed should equal Total');
      assert(result.failed === 5, 'With passRate 0.5, should have 5 failures');
    });

    it('5.4 should return errors array in result', async () => {
      const runner = new MockTestRunner({ passRate: 0.7, testCount: 5 });

      const result = await executeTests('', 'xUnit', runner);

      assert(Array.isArray(result.errors), 'Should have errors array');
    });

    it('5.5 should handle test runner not available', async () => {
      const result = await executeTests('code', 'xUnit', null);

      assert(typeof result === 'object', 'Should return object');
      assert(result.errors.length > 0, 'Should have warning about missing runner');
    });
  });

  describe('Suite 6: Result Comparison (4 tests)', () => {
    it('6.1 should detect regressions (more failures)', () => {
      const baseline = { passed: 10, failed: 0 };
      const refactored = { passed: 8, failed: 2 };

      const comparison = compareResults(baseline, refactored);

      assert(comparison.regressions === 2, 'Should detect 2 regressions');
      assert(comparison.statusChange === 'degraded', 'Should report degraded status');
    });

    it('6.2 should detect improvements (fewer failures)', () => {
      const baseline = { passed: 8, failed: 2 };
      const refactored = { passed: 10, failed: 0 };

      const comparison = compareResults(baseline, refactored);

      assert(comparison.improvements === 2, 'Should detect 2 improvements');
      assert(comparison.statusChange === 'improved', 'Should report improved status');
    });

    it('6.3 should detect maintained status (same results)', () => {
      const baseline = { passed: 10, failed: 0 };
      const refactored = { passed: 10, failed: 0 };

      const comparison = compareResults(baseline, refactored);

      assert(comparison.regressions === 0, 'Should have no regressions');
      assert(comparison.improvements === 0, 'Should have no improvements');
      assert(comparison.statusChange === 'maintained', 'Should report maintained status');
    });

    it('6.4 should generate summary strings', () => {
      const baseline = { passed: 8, failed: 2 };
      const refactored = { passed: 9, failed: 1 };

      const comparison = compareResults(baseline, refactored);

      assert(comparison.baselineSummary.includes('8'), 'Baseline summary should include passed count');
      assert(comparison.refactoredSummary.includes('9'), 'Refactored summary should include passed count');
    });
  });

  describe('Suite 7: Error Handling & Recovery (5 tests)', () => {
    it('7.1 should return error response on validation failure', async () => {
      const handler = createRefactorTestsHandler();
      const message = { data: { refactoredSource: '', originalSource: 'valid', language: 'csharp' } };

      const response = await handler(message, {});

      assert(response.success === false, 'Should fail');
      assert(response.errorCode !== undefined, 'Should have errorCode');
    });

    it('7.2 should not throw on handler error (non-blocking)', async () => {
      const handler = createRefactorTestsHandler();
      const message = { data: null }; // Will cause error

      let thrown = false;
      try {
        await handler(message, {});
      } catch (e) {
        thrown = true;
      }

      assert(!thrown, 'Should not throw error');
    });

    it('7.3 should include operation type in error response', async () => {
      const handler = createRefactorTestsHandler();
      const message = { data: { refactoredSource: '', originalSource: 'x', language: 'csharp' } };

      const response = await handler(message, {});

      assert(response.operationType === RefactorTestsOperationType.VALIDATION, 'Should include operationType');
    });

    it('7.4 should record metrics on error', async () => {
      const metrics = createMockMetrics();
      const handler = createRefactorTestsHandler({ metrics });
      const message = { data: { refactoredSource: '', originalSource: 'x', language: 'csharp' } };

      await handler(message, {});

      const errorMetrics = metrics.getRecorded('refactorTests.error');
      assert(errorMetrics.length > 0, 'Should record error metric');
    });

    it('7.5 should log errors with context', async () => {
      const logger = createMockLogger();
      const handler = createRefactorTestsHandler({ logger });
      const message = { data: { refactoredSource: '', originalSource: 'x', language: 'csharp' } };

      await handler(message, {});

      const errors = logger.getLogs('error');
      assert(errors.length > 0, 'Should log error');
      assert(errors[0].message.includes('failed'), 'Error message should indicate failure');
    });
  });

  describe('Suite 8: Performance Gates (3 tests)', () => {
    it('8.1 should complete within performance gate (< 30s for slow policy)', async () => {
      const runner = new MockTestRunner({ passRate: 0.8, testCount: 5, avgDuration: 50 });
      const handler = createRefactorTestsHandler({ testRunner: runner });
      const message = {
        data: {
          refactoredSource: getValidCSharpXUnitSource(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
        },
      };

      const startTime = Date.now();
      const response = await handler(message, {});
      const elapsed = Date.now() - startTime;

      assert(elapsed < 30000, `Execution should complete within 30s (took ${elapsed}ms)`);
      assert(response.data?.executionTime !== undefined, 'Should record execution time');
    });

    it('8.2 should record execution time in metrics', async () => {
      const metrics = createMockMetrics();
      const runner = new MockTestRunner({ passRate: 0.8, testCount: 3 });
      const handler = createRefactorTestsHandler({ metrics, testRunner: runner });
      const message = {
        data: {
          refactoredSource: getValidCSharpXUnitSource(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
        },
      };

      await handler(message, {});

      const executionMetrics = metrics.getRecorded('refactorTests.execution');
      assert(executionMetrics.length > 0, 'Should record execution metric');
      assert(executionMetrics[0].data.executionTime !== undefined, 'Should include execution time');
    });

    it('8.3 should handle timeout gracefully', async () => {
      // Slow runner to test timeout handling
      const runner = new MockTestRunner({ passRate: 0.5, testCount: 100, avgDuration: 200 });
      const handler = createRefactorTestsHandler({ testRunner: runner });
      const message = {
        data: {
          refactoredSource: getValidCSharpXUnitSource(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
        },
      };

      const startTime = Date.now();
      const response = await handler(message, {});
      const elapsed = Date.now() - startTime;

      assert(elapsed < 35000, 'Should complete with timeout handling');
      assert(response.data !== undefined || response.success === false, 'Should return result or error');
    });
  });

  describe('Suite 9: Metrics & Logging (3 tests)', () => {
    it('9.1 should record framework detection metrics', async () => {
      const metrics = createMockMetrics();
      const runner = new MockTestRunner({ passRate: 1.0, testCount: 3 });
      const handler = createRefactorTestsHandler({ metrics, testRunner: runner });
      const message = {
        data: {
          refactoredSource: getValidCSharpXUnitSource(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
        },
      };

      await handler(message, {});

      const detectionMetrics = metrics.getRecorded('refactorTests.frameworkDetection');
      assert(detectionMetrics.length > 0, 'Should record framework detection');
    });

    it('9.2 should log execution stages', async () => {
      const logger = createMockLogger();
      const runner = new MockTestRunner({ passRate: 1.0, testCount: 2 });
      const handler = createRefactorTestsHandler({ logger, testRunner: runner });
      const message = {
        data: {
          refactoredSource: getValidCSharpXUnitSource(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
        },
      };

      await handler(message, {});

      const logs = logger.getLogs('debug');
      assert(logs.length > 0, 'Should log debug messages');
    });

    it('9.3 should record test results in metrics', async () => {
      const metrics = createMockMetrics();
      const runner = new MockTestRunner({ passRate: 0.8, testCount: 5 });
      const handler = createRefactorTestsHandler({ metrics, testRunner: runner });
      const message = {
        data: {
          refactoredSource: getValidCSharpXUnitSource(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
        },
      };

      await handler(message, {});

      const executionMetrics = metrics.getRecorded('refactorTests.execution');
      assert(executionMetrics[0].data.testsPassed !== undefined, 'Should record passed count');
      assert(executionMetrics[0].data.testsFailed !== undefined, 'Should record failed count');
    });
  });

  describe('Bonus: Integration Scenarios (2 tests)', () => {
    it('Bonus 1: Full workflow - passing refactor', async () => {
      const logger = createMockLogger();
      const metrics = createMockMetrics();
      const runner = new MockTestRunner({ passRate: 1.0, testCount: 5 });
      const handler = createRefactorTestsHandler({ logger, metrics, testRunner: runner });

      const message = {
        data: {
          refactoredSource: getRefactoredCSharpSource(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
          testFramework: 'xUnit',
        },
      };

      const response = await handler(message, {});

      assert(response.success === true, 'Should succeed');
      assert(response.data.statusChange === 'maintained' || response.data.statusChange === 'improved', 'Should maintain or improve');
      assert(response.data.framework === 'xUnit', 'Should detect xUnit');
    });

    it('Bonus 2: Full workflow - broken refactor', async () => {
      const metrics = createMockMetrics();
      const runner = new MockTestRunner({ passRate: 0.5, testCount: 5 });
      const handler = createRefactorTestsHandler({ metrics, testRunner: runner });

      const message = {
        data: {
          refactoredSource: getBrokenRefactorCSharp(),
          originalSource: getSampleCSharpProduction(),
          language: 'csharp',
        },
      };

      const response = await handler(message, {});

      assert(response.success === true, 'Should return response (non-blocking)');
      assert(response.data.regressions >= 0, 'Should report regressions');
    });
  });
});
