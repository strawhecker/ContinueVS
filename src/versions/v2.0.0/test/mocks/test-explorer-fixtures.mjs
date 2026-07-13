#!/usr/bin/env node

/**
 * Test-Explorer-Handler Mock Fixtures and Builders
 *
 * Provides reusable mock data factories for test setup across multiple test suites.
 * Follows the builder pattern for fluent test configuration.
 *
 * @module src/versions/v2.0.0/tests/mocks/test-explorer-fixtures.mjs
 */

/**
 * Mock C# test class symbol
 */
export function getCSharpTestClass() {
  return {
    name: 'MathServiceTests',
    kind: 'class',
    line: 5,
    column: 0,
    endLine: 50,
    endColumn: 1,
    attributes: ['[TestFixture]'],
    children: [
      {
        name: 'TestAddition',
        kind: 'method',
        line: 8,
        column: 4,
        endLine: 15,
        endColumn: 5,
        attributes: ['[Fact]'],
      },
      {
        name: 'TestDivisionByZero',
        kind: 'method',
        line: 17,
        column: 4,
        endLine: 25,
        endColumn: 5,
        attributes: ['[Theory]'],
      },
    ],
  };
}

/**
 * Mock C# test method
 */
export function getCSharpTestMethod(name = 'TestExample', attributes = ['[Fact]']) {
  return {
    name,
    kind: 'method',
    line: 10,
    column: 4,
    endLine: 20,
    endColumn: 5,
    attributes,
  };
}

/**
 * Mock TypeScript describe block
 */
export function getTypeScriptDescribeBlock() {
  return {
    name: 'Calculator Tests',
    kind: 'suite',
    line: 5,
    column: 0,
    endLine: 30,
    endColumn: 1,
    attributes: ['describe'],
    children: [
      {
        name: 'should add two numbers',
        kind: 'test',
        line: 6,
        column: 2,
        endLine: 10,
        endColumn: 3,
        attributes: ['it'],
      },
      {
        name: 'should throw on divide by zero',
        kind: 'test',
        line: 12,
        column: 2,
        endLine: 16,
        endColumn: 3,
        attributes: ['it'],
      },
    ],
  };
}

/**
 * Mock TypeScript individual test
 */
export function getTypeScriptItTest(name = 'should work correctly', attributes = ['it']) {
  return {
    name,
    kind: 'test',
    line: 6,
    column: 2,
    endLine: 10,
    endColumn: 3,
    attributes,
  };
}

/**
 * Mock test source code with test markers
 */
export function getTestSourceCode(language = 'csharp') {
  if (language === 'csharp') {
    return `using Xunit;

namespace MyApp.Tests
{
    [TestFixture]
    public class CalculatorTests
    {
        [Fact]
        public void TestAddition()
        {
            var calc = new Calculator();
            Assert.Equal(5, calc.Add(2, 3));
        }

        [Theory]
        [InlineData(10, 5, 2)]
        [InlineData(20, 4, 5)]
        public void TestDivision(int dividend, int divisor, int expected)
        {
            var calc = new Calculator();
            Assert.Equal(expected, calc.Divide(dividend, divisor));
        }

        [Fact(Skip = "Not ready")]
        public void TestSkipped()
        {
            Assert.True(false);
        }
    }
}`;
  } else if (language === 'typescript' || language === 'javascript') {
    return `import { describe, it, expect } from '@jest/globals';
import { Calculator } from '../calculator';

describe('Calculator Tests', () => {
  let calculator;

  beforeEach(() => {
    calculator = new Calculator();
  });

  it('should add two numbers', () => {
    expect(calculator.add(2, 3)).toBe(5);
  });

  it('should divide correctly', () => {
    expect(calculator.divide(10, 5)).toBe(2);
  });

  describe('nested suite', () => {
    it('should work in nested context', () => {
      expect(true).toBe(true);
    });
  });
});`;
  }
  return '';
}

/**
 * Mock test diagnostics (failure/error)
 */
export function getTestDiagnostics(testId = 'test-1', state = 'failed') {
  if (state === 'failed') {
    return [
      {
        id: testId,
        severity: 'error',
        message: 'Assertion failed: expected 5 but got 4',
        filepath: '/path/to/test.cs',
        line: 12,
        column: 20,
      },
    ];
  } else if (state === 'skipped') {
    return [
      {
        id: testId,
        severity: 'info',
        message: 'Test skipped',
        filepath: '/path/to/test.cs',
        line: 25,
        column: 0,
      },
    ];
  }
  return [];
}

/**
 * Valid test explorer request fixtures
 */
export function getValidTestExplorerRequest(scope = 'workspace') {
  const requests = {
    file: {
      scope: 'file',
      filepath: '/path/to/test.cs',
      includeResults: true,
      includeTimings: true,
    },
    project: {
      scope: 'project',
      projectPath: '/path/to/project',
      includeResults: true,
      includeTimings: true,
    },
    workspace: {
      scope: 'workspace',
      includeResults: true,
      includeTimings: true,
    },
  };
  return requests[scope] || requests.workspace;
}

/**
 * Invalid test explorer request (for error testing)
 */
export function getInvalidTestExplorerRequest(type = 'badScope') {
  const requests = {
    badScope: { scope: 'invalid', filepath: '/path/to/test.cs' },
    missingFilepath: { scope: 'file' },
    nullMessage: null,
    emptyData: { data: null },
  };
  return requests[type] || requests.badScope;
}

/**
 * Expected test explorer response
 */
export function getExpectedTestResponse() {
  return {
    success: true,
    data: {
      tests: [
        {
          id: '/path/to/test.cs:8:4',
          name: 'TestAddition',
          kind: 'test',
          filepath: '/path/to/test.cs',
          range: {
            start: { line: 8, column: 4 },
            end: { line: 15, column: 5 },
          },
          attributes: ['[Fact]'],
          tags: [],
          state: 'unknown',
        },
        {
          id: '/path/to/test.cs:17:4',
          name: 'TestDivision',
          kind: 'test',
          filepath: '/path/to/test.cs',
          range: {
            start: { line: 17, column: 4 },
            end: { line: 23, column: 5 },
          },
          attributes: ['[Theory]'],
          tags: [],
          state: 'unknown',
        },
      ],
      summary: {
        total: 2,
        passed: 0,
        failed: 0,
        skipped: 0,
        executionTime: 0,
      },
      scope: 'workspace',
      cacheHit: false,
      queryTime: 45,
    },
  };
}

/**
 * Test discovery event fixture
 */
export function getTestDiscoveredEvent(count = 5) {
  const tests = [];
  for (let i = 0; i < count; i++) {
    tests.push({
      id: `test-${i}`,
      name: `Test${i}`,
      kind: 'test',
      filepath: `/path/to/test${i}.cs`,
      range: { start: { line: i * 5, column: 0 }, end: { line: i * 5 + 5, column: 0 } },
      attributes: ['[Fact]'],
      tags: [],
      state: 'unknown',
    });
  }
  return {
    tests,
    discoveredAt: Date.now(),
  };
}

/**
 * Test execution event fixture
 */
export function getTestExecutionEvent(testCount = 3) {
  const testIds = [];
  for (let i = 0; i < testCount; i++) {
    testIds.push(`test-${i}`);
  }
  return {
    testIds,
    startedAt: Date.now(),
  };
}

/**
 * Test results event fixture
 */
export function getTestResultsEvent() {
  return {
    results: [
      {
        id: 'test-0',
        state: 'passed',
        duration: 125,
      },
      {
        id: 'test-1',
        state: 'failed',
        duration: 230,
        error: 'Expected 5 but got 4',
      },
      {
        id: 'test-2',
        state: 'skipped',
        duration: 0,
      },
    ],
    completedAt: Date.now(),
  };
}

/**
 * Out-of-bounds test explorer request (for error testing)
 */
export function getOutOfBoundsRequest() {
  return {
    scope: 'file',
    filepath: '',
    line: -1,
    column: -1,
  };
}

/**
 * Cache statistics fixture
 */
export function getCacheStatsFixture() {
  return {
    hits: 150,
    misses: 30,
    evictions: 5,
    ttlExpiries: 2,
    size: 42,
  };
}

/**
 * Multiple test files fixture
 */
export function getMultipleTestFilesFixture() {
  return [
    {
      filepath: '/src/tests/unit/math.test.cs',
      language: 'csharp',
      testCount: 3,
      passedCount: 3,
    },
    {
      filepath: '/src/tests/integration/api.test.ts',
      language: 'typescript',
      testCount: 5,
      passedCount: 3,
      failedCount: 2,
    },
    {
      filepath: '/src/tests/e2e/ui.test.js',
      language: 'javascript',
      testCount: 2,
      passedCount: 1,
      skippedCount: 1,
    },
  ];
}

/**
 * Test suite with nested tests fixture
 */
export function getNestedTestSuiteFixture() {
  return {
    id: 'suite-root',
    name: 'Root Suite',
    kind: 'suite',
    filepath: '/path/to/nested.test.ts',
    range: { start: { line: 1, column: 0 }, end: { line: 50, column: 0 } },
    attributes: ['describe'],
    tags: [],
    state: 'unknown',
    children: [
      {
        id: 'suite-1',
        name: 'Feature A',
        kind: 'suite',
        filepath: '/path/to/nested.test.ts',
        range: { start: { line: 5, column: 2 }, end: { line: 20, column: 2 } },
        attributes: ['describe'],
        tags: [],
        state: 'unknown',
        children: [
          {
            id: 'test-1-1',
            name: 'should work',
            kind: 'test',
            filepath: '/path/to/nested.test.ts',
            range: { start: { line: 6, column: 4 }, end: { line: 10, column: 4 } },
            attributes: ['it'],
            tags: [],
            state: 'passed',
          },
          {
            id: 'test-1-2',
            name: 'should fail gracefully',
            kind: 'test',
            filepath: '/path/to/nested.test.ts',
            range: { start: { line: 12, column: 4 }, end: { line: 18, column: 4 } },
            attributes: ['it'],
            tags: [],
            state: 'failed',
            error: 'Expected true but got false',
          },
        ],
      },
      {
        id: 'suite-2',
        name: 'Feature B',
        kind: 'suite',
        filepath: '/path/to/nested.test.ts',
        range: { start: { line: 22, column: 2 }, end: { line: 40, column: 2 } },
        attributes: ['describe'],
        tags: [],
        state: 'unknown',
        children: [
          {
            id: 'test-2-1',
            name: 'should skip',
            kind: 'test',
            filepath: '/path/to/nested.test.ts',
            range: { start: { line: 24, column: 4 }, end: { line: 28, column: 4 } },
            attributes: ['it.skip'],
            tags: ['skipped'],
            state: 'skipped',
          },
        ],
      },
    ],
  };
}

/**
 * Large test count fixture (for performance testing)
 */
export function getLargeTestCountFixture(count = 1000) {
  const tests = [];
  for (let i = 0; i < count; i++) {
    tests.push({
      id: `test-${i}`,
      name: `Test${i}`,
      kind: 'test',
      filepath: `/path/to/test${Math.floor(i / 100)}.cs`,
      range: {
        start: { line: (i % 100) * 5, column: 0 },
        end: { line: (i % 100) * 5 + 5, column: 0 },
      },
      attributes: ['[Fact]'],
      tags: [],
      state: i % 3 === 0 ? 'passed' : i % 3 === 1 ? 'failed' : 'unknown',
      duration: Math.random() * 500,
    });
  }
  return tests;
}

/**
 * Test tags fixture (for filtering/sorting)
 */
export function getTestsWithTagsFixture() {
  return [
    {
      id: 'test-1',
      name: 'SlowIntegrationTest',
      kind: 'test',
      filepath: '/path/to/test.cs',
      range: { start: { line: 1, column: 0 }, end: { line: 10, column: 0 } },
      attributes: ['[Fact]', '[Slow]', '[Integration]'],
      tags: ['slow', 'integration'],
      state: 'unknown',
      duration: 5000,
    },
    {
      id: 'test-2',
      name: 'FastUnitTest',
      kind: 'test',
      filepath: '/path/to/test.cs',
      range: { start: { line: 12, column: 0 }, end: { line: 20, column: 0 } },
      attributes: ['[Fact]', '[Unit]'],
      tags: ['unit'],
      state: 'unknown',
      duration: 50,
    },
  ];
}

/**
 * Mixed language tests fixture (C# and TypeScript)
 */
export function getMixedLanguageTestsFixture() {
  return {
    csharp: [
      {
        id: 'cs-test-1',
        name: 'TestCSharpFeature',
        kind: 'test',
        filepath: '/src/tests/feature.test.cs',
        range: { start: { line: 10, column: 0 }, end: { line: 20, column: 0 } },
        attributes: ['[Fact]'],
        tags: [],
        state: 'passed',
      },
    ],
    typescript: [
      {
        id: 'ts-test-1',
        name: 'should test TypeScript feature',
        kind: 'test',
        filepath: '/src/tests/feature.test.ts',
        range: { start: { line: 8, column: 0 }, end: { line: 15, column: 0 } },
        attributes: ['it'],
        tags: [],
        state: 'passed',
      },
    ],
  };
}

/**
 * Malformed test attributes fixture (for error handling)
 */
export function getMalformedTestAttributesFixture() {
  return {
    incompleteAttribute: {
      id: 'test-bad-1',
      name: 'TestWithBadAttribute',
      kind: 'test',
      filepath: '/path/to/test.cs',
      range: { start: { line: 5, column: 0 }, end: { line: 10, column: 0 } },
      attributes: ['[Fact'], // Missing closing bracket
      tags: [],
      state: 'unknown',
    },
    nullAttribute: {
      id: 'test-bad-2',
      name: 'TestWithNull',
      kind: 'test',
      filepath: '/path/to/test.cs',
      range: { start: { line: 12, column: 0 }, end: { line: 18, column: 0 } },
      attributes: null, // Should be array
      tags: [],
      state: 'unknown',
    },
    emptyAttribute: {
      id: 'test-bad-3',
      name: 'TestWithEmpty',
      kind: 'test',
      filepath: '/path/to/test.cs',
      range: { start: { line: 20, column: 0 }, end: { line: 25, column: 0 } },
      attributes: [], // Empty attributes
      tags: [],
      state: 'unknown',
    },
  };
}

/**
 * Concurrent query fixture (for stress testing)
 */
export function getConcurrentQueryFixture(queryCount = 10) {
  const queries = [];
  for (let i = 0; i < queryCount; i++) {
    queries.push({
      scope: i % 3 === 0 ? 'file' : i % 3 === 1 ? 'project' : 'workspace',
      filepath: i % 3 === 0 ? `/path/to/test${i}.cs` : undefined,
      projectPath: i % 3 === 1 ? `/project${i}` : undefined,
    });
  }
  return queries;
}
