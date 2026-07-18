#!/usr/bin/env node

/**
 * Refactor-Tests Mock & Fixtures (Step 93)
 *
 * Provides mock test runner, source code fixtures, and comparison utilities
 * for testing the refactor-tests-handler in isolation.
 *
 * @module src/versions/v2.0.0/tests/mocks/refactor-tests-mock.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

/**
 * Mock test runner with configurable pass/fail rates
 *
 * @class MockTestRunner
 */
export class MockTestRunner {
  /**
   * @param {Object} config - Configuration
   * @param {number} [config.passRate=0.8] - Fraction of tests that pass (0.0-1.0)
   * @param {number} [config.testCount=10] - Number of tests to simulate
   * @param {number} [config.avgDuration=100] - Average duration per test (ms)
   * @param {number} [config.failureMode='random'] - 'random', 'first', 'last', 'none'
   */
  constructor({ passRate = 0.8, testCount = 10, avgDuration = 100, failureMode = 'random' } = {}) {
    this.passRate = Math.max(0, Math.min(1, passRate));
    this.testCount = Math.max(1, testCount);
    this.avgDuration = Math.max(10, avgDuration);
    this.failureMode = failureMode;
  }

  /**
   * Simulates test execution
   *
   * @param {string} sourceCode - Source code (unused in mock)
   * @param {string} framework - Test framework (unused in mock)
   * @returns {Promise<Object>} Simulated test results
   */
  async run(sourceCode, framework) {
    const startTime = Date.now();
    const passCount = Math.floor(this.testCount * this.passRate);
    let failCount = this.testCount - passCount;

    // Apply failure mode
    if (this.failureMode === 'none') {
      failCount = 0;
    }

    // Simulate execution delay
    const delayMs = this.avgDuration + Math.random() * 50 - 25;
    await new Promise((resolve) => setTimeout(resolve, delayMs));

    const errors = failCount > 0 ? [{ message: `${failCount} test(s) failed`, severity: 'error' }] : [];

    return {
      passed: passCount,
      failed: failCount,
      total: this.testCount,
      duration: Date.now() - startTime,
      errors,
    };
  }
}

/**
 * Sample C# xUnit test source (valid)
 *
 * @returns {string}
 */
export function getValidCSharpXUnitSource() {
  return `using Xunit;

namespace TestProject
{
  public class CalculatorTests
  {
    private Calculator _calculator;

    public CalculatorTests()
    {
      _calculator = new Calculator();
    }

    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
      var result = _calculator.Add(2, 3);
      Assert.Equal(5, result);
    }

    [Fact]
    public void Subtract_TwoNumbers_ReturnsDifference()
    {
      var result = _calculator.Subtract(5, 3);
      Assert.Equal(2, result);
    }

    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(0, 0, 0)]
    public void Add_MultipleInputs_CorrectSum(int a, int b, int expected)
    {
      var result = _calculator.Add(a, b);
      Assert.Equal(expected, result);
    }
  }
}`;
}

/**
 * Sample C# NUnit test source (valid)
 *
 * @returns {string}
 */
export function getValidCSharpNUnitSource() {
  return `using NUnit.Framework;

namespace TestProject
{
  [TestFixture]
  public class StringTests
  {
    [Test]
    public void ToUpper_LowerString_ReturnsUpper()
    {
      var input = "hello";
      var result = input.ToUpper();
      Assert.That(result, Is.EqualTo("HELLO"));
    }

    [Test]
    public void Contains_StringPresent_ReturnsTrue()
    {
      var input = "hello world";
      Assert.That(input, Does.Contain("world"));
    }

    [Test]
    public void Length_NonEmptyString_ReturnsCorrectLength()
    {
      var input = "test";
      Assert.That(input.Length, Is.EqualTo(4));
    }
  }
}`;
}

/**
 * Sample JavaScript Jest test source (valid)
 *
 * @returns {string}
 */
export function getValidJavaScriptJestSource() {
  return `const math = require('./math');

describe('Math Operations', () => {
  describe('add', () => {
    it('should return sum of two numbers', () => {
      expect(math.add(2, 3)).toBe(5);
    });

    it('should handle negative numbers', () => {
      expect(math.add(-2, -3)).toBe(-5);
    });
  });

  describe('multiply', () => {
    it('should return product of two numbers', () => {
      expect(math.multiply(2, 3)).toBe(6);
    });

    it('should return zero when multiplying by zero', () => {
      expect(math.multiply(5, 0)).toBe(0);
    });
  });
});`;
}

/**
 * Sample JavaScript Mocha test source (valid)
 *
 * @returns {string}
 */
export function getValidJavaScriptMochaSource() {
  return `const chai = require('chai');
const expect = chai.expect;
const utils = require('./utils');

describe('String Utils', () => {
  describe('trim', () => {
    it('should remove leading and trailing spaces', () => {
      expect(utils.trim('  hello  ')).to.equal('hello');
    });

    it('should handle empty strings', () => {
      expect(utils.trim('')).to.equal('');
    });
  });

  describe('uppercase', () => {
    it('should convert string to uppercase', () => {
      expect(utils.uppercase('hello')).to.equal('HELLO');
    });
  });
});`;
}

/**
 * Sample Python Pytest source (valid)
 *
 * @returns {string}
 */
export function getValidPythonPytestSource() {
  return `import pytest
from calculator import Calculator

class TestCalculator:
  @pytest.fixture
  def calc(self):
    return Calculator()

  def test_add(self, calc):
    assert calc.add(2, 3) == 5

  def test_subtract(self, calc):
    assert calc.subtract(5, 3) == 2

  @pytest.mark.parametrize('a,b,expected', [
    (2, 3, 5),
    (0, 0, 0),
    (-1, 1, 0),
  ])
  def test_add_multiple(self, calc, a, b, expected):
    assert calc.add(a, b) == expected`;
}

/**
 * Malformed C# test source (invalid syntax)
 *
 * @returns {string}
 */
export function getMalformedCSharpSource() {
  return `using Xunit;

namespace TestProject
{
  public class BrokenTests
  {
    [Fact]
    public void BrokenTest()
    {
      var result = this.DoSomething(
      // Missing closing paren
      Assert.True(result);
    }

    [Fact]
    public void AnotherBroken
    // Missing method body
  }
}`;
}

/**
 * Malformed JavaScript test source (invalid syntax)
 *
 * @returns {string}
 */
export function getMalformedJavaScriptSource() {
  return `describe('Broken Tests', () => {
  it('should fail due to syntax error', () => {
    const x = {
      // Missing closing brace
    expect(x).toBeDefined();
  });

  it('missing closing paren'
    // Incomplete
});`;
}

/**
 * Sample C# source code (production code, no tests)
 *
 * @returns {string}
 */
export function getSampleCSharpProduction() {
  return `namespace Calculator
{
  public class Calculator
  {
    public int Add(int a, int b)
    {
      return a + b;
    }

    public int Subtract(int a, int b)
    {
      return a - b;
    }

    public int Multiply(int a, int b)
    {
      return a * b;
    }

    public int Divide(int a, int b)
    {
      if (b == 0) throw new ArgumentException("Division by zero");
      return a / b;
    }
  }
}`;
}

/**
 * Sample JavaScript source code (production code, no tests)
 *
 * @returns {string}
 */
export function getSampleJavaScriptProduction() {
  return `function add(a, b) {
  return a + b;
}

function subtract(a, b) {
  return a - b;
}

function multiply(a, b) {
  return a * b;
}

function divide(a, b) {
  if (b === 0) throw new Error('Division by zero');
  return a / b;
}

module.exports = { add, subtract, multiply, divide };`;
}

/**
 * Refactored C# source code (optimized, with same interface)
 *
 * @returns {string}
 */
export function getRefactoredCSharpSource() {
  return `namespace Calculator
{
  public class Calculator
  {
    // Refactored: Added caching and validation
    private Dictionary<string, int> _resultCache = new();

    public int Add(int a, int b)
    {
      var key = $"add_{a}_{b}";
      if (_resultCache.TryGetValue(key, out var cached))
        return cached;

      var result = checked(a + b);
      _resultCache[key] = result;
      return result;
    }

    public int Subtract(int a, int b) => a - b;

    public int Multiply(int a, int b) => a * b;

    public int Divide(int a, int b)
    {
      if (b == 0) throw new ArgumentException("Division by zero");
      return a / b;
    }
  }
}`;
}

/**
 * Refactored JavaScript source code (optimized, with same interface)
 *
 * @returns {string}
 */
export function getRefactoredJavaScriptSource() {
  return `// Refactored: Added memoization
const cache = {};

function memoize(fn, key) {
  if (cache[key] !== undefined) return cache[key];
  const result = fn();
  cache[key] = result;
  return result;
}

function add(a, b) {
  return memoize(() => a + b, \`add_\${a}_\${b}\`);
}

function subtract(a, b) {
  return a - b;
}

function multiply(a, b) {
  return a * b;
}

function divide(a, b) {
  if (b === 0) throw new Error('Division by zero');
  return a / b;
}

module.exports = { add, subtract, multiply, divide };`;
}

/**
 * Test fixture: C# source with regression (broken refactor)
 *
 * @returns {string}
 */
export function getBrokenRefactorCSharp() {
  return `namespace Calculator
{
  public class Calculator
  {
    // BUG: Subtract logic is broken
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a + b; // WRONG: Should be a - b
    public int Multiply(int a, int b) => a * b;

    public int Divide(int a, int b)
    {
      if (b == 0) throw new ArgumentException("Division by zero");
      return a / b;
    }
  }
}`;
}

/**
 * Test fixture: JavaScript source with regression (broken refactor)
 *
 * @returns {string}
 */
export function getBrokenRefactorJavaScript() {
  return `// BUG: Subtract logic is broken
function add(a, b) {
  return a + b;
}

function subtract(a, b) {
  return a + b; // WRONG: Should be a - b
}

function multiply(a, b) {
  return a * b;
}

function divide(a, b) {
  if (b === 0) throw new Error('Division by zero');
  return a / b;
}

module.exports = { add, subtract, multiply, divide };`;
}

/**
 * Creates comparison utilities for test results
 *
 * @returns {Object}
 */
export function createComparisonUtils() {
  return {
    /**
     * Simulates a passing baseline (all tests pass)
     */
    passingBaseline() {
      return { passed: 5, failed: 0, total: 5, duration: 150, errors: [] };
    },

    /**
     * Simulates a partially failing baseline (some tests fail)
     */
    partiallyFailingBaseline() {
      return { passed: 3, failed: 2, total: 5, duration: 180, errors: [{ message: '2 tests failed', severity: 'error' }] };
    },

    /**
     * Simulates a fully failing baseline
     */
    fullyFailingBaseline() {
      return { passed: 0, failed: 5, total: 5, duration: 200, errors: [{ message: '5 tests failed', severity: 'error' }] };
    },

    /**
     * Simulates improved refactored results
     */
    improvedRefactored() {
      return { passed: 5, failed: 0, total: 5, duration: 120, errors: [] };
    },

    /**
     * Simulates regressed refactored results
     */
    regressedRefactored() {
      return { passed: 2, failed: 3, total: 5, duration: 150, errors: [{ message: '3 tests failed', severity: 'error' }] };
    },

    /**
     * Simulates maintained refactored results
     */
    maintainedRefactored() {
      return { passed: 3, failed: 2, total: 5, duration: 160, errors: [{ message: '2 tests failed', severity: 'error' }] };
    },
  };
}

/**
 * Creates mock metrics collector
 *
 * @returns {Object}
 */
export function createMockMetrics() {
  const recorded = [];

  return {
    recordMetric(name, data) {
      recorded.push({ name, data, timestamp: Date.now() });
    },

    getRecorded(name) {
      return recorded.filter((m) => m.name === name);
    },

    getAllRecorded() {
      return recorded;
    },

    clear() {
      recorded.length = 0;
    },
  };
}

/**
 * Creates mock logger
 *
 * @returns {Object}
 */
export function createMockLogger() {
  const logs = { debug: [], info: [], warn: [], error: [] };

  return {
    debug(message, data) {
      logs.debug.push({ message, data, timestamp: Date.now() });
    },

    info(message, data) {
      logs.info.push({ message, data, timestamp: Date.now() });
    },

    warn(message, data) {
      logs.warn.push({ message, data, timestamp: Date.now() });
    },

    error(message, data) {
      logs.error.push({ message, data, timestamp: Date.now() });
    },

    getLogs(level) {
      return level ? logs[level] : logs;
    },

    clear() {
      Object.keys(logs).forEach((level) => (logs[level].length = 0));
    },
  };
}

export default {
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
};
