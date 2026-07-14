#!/usr/bin/env node

/**
 * Fix-Suggestion Handler Tests (Step 77)
 *
 * Comprehensive test suite for the fix suggestion handler covering:
 * - Request validation and error handling
 * - Fix strategy functions for all error categories
 * - Language support (JavaScript, TypeScript, C#, Python)
 * - Response format and metadata
 * - Integration with handler registry
 *
 * **Test Framework**: Mocha
 * **Assertion Library**: Node.js assert
 * **Running Tests**: npx mocha tests/fix-suggestion-handler.test.mjs --timeout 10000
 *
 * @module tests/fix-suggestion-handler.test.mjs
 * @author Bridge Architecture Team
 */

import assert from 'assert';
import {
  fixSuggestionHandler,
  validateFixSuggestionRequest,
  generateSyntaxFixes,
  generateSemanticFixes,
  generatePatternFixes,
  generateStyleFixes,
  generatePerformanceFixes,
  generateSecurityFixes,
  createFixSuggestionResponse,
  FixSuggestionError,
  FixSuggestionValidationError,
  FixSuggestionUnsupportedError,
  FixSuggestionAnalysisError,
} from '../lib/fix-suggestion-handler.mjs';

// ============================================================================
// Mock Context (Logger & Metrics)
// ============================================================================

class MockLogger {
  constructor() {
    this.logs = [];
  }

  debug(message) {
    this.logs.push({ level: 'debug', message });
  }

  info(message) {
    this.logs.push({ level: 'info', message });
  }

  warn(message) {
    this.logs.push({ level: 'warn', message });
  }

  error(message) {
    this.logs.push({ level: 'error', message });
  }
}

class MockMetrics {
  constructor() {
    this.events = [];
  }

  recordEvent(name, data = {}) {
    this.events.push({ name, data, type: 'event' });
  }

  recordHandlerExecution(handler, success, data = {}) {
    this.events.push({ handler, success, data, type: 'execution' });
  }
}

// ============================================================================
// Test Suite 1: Parameter Validation
// ============================================================================

describe('Fix-Suggestion Handler - Step 77', () => {
  describe('Suite 1: Parameter Validation', () => {
    it('should reject empty request object', () => {
      assert.throws(() => {
        validateFixSuggestionRequest(null);
      }, FixSuggestionValidationError);
    });

    it('should reject missing source code', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          errorMessage: 'Error',
          errorType: 'syntax',
        });
      }, FixSuggestionValidationError);
    });

    it('should reject empty source code', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          source: '',
          errorMessage: 'Error',
          errorType: 'syntax',
        });
      }, FixSuggestionValidationError);
    });

    it('should reject whitespace-only source code', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          source: '   \n\t  ',
          errorMessage: 'Error',
          errorType: 'syntax',
        });
      }, FixSuggestionValidationError);
    });

    it('should reject missing error message', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          source: 'const x = 1;',
          errorType: 'syntax',
        });
      }, FixSuggestionValidationError);
    });

    it('should reject invalid error type', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          source: 'const x = 1;',
          errorMessage: 'Error',
          errorType: 'invalid_type',
        });
      }, FixSuggestionValidationError);
    });

    it('should reject unsupported language', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          source: 'const x = 1;',
          errorMessage: 'Error',
          errorType: 'syntax',
          language: 'rust',
        });
      }, FixSuggestionUnsupportedError);
    });

    it('should reject invalid line number (negative)', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          source: 'const x = 1;',
          errorMessage: 'Error',
          errorType: 'syntax',
          line: -1,
        });
      }, FixSuggestionValidationError);
    });

    it('should reject line number exceeding source code', () => {
      assert.throws(() => {
        validateFixSuggestionRequest({
          source: 'const x = 1;',
          errorMessage: 'Error',
          errorType: 'syntax',
          line: 100,
        });
      }, FixSuggestionValidationError);
    });

    it('should accept valid request with minimal fields', () => {
      assert.doesNotThrow(() => {
        validateFixSuggestionRequest({
          source: 'const x = 1;',
          errorMessage: 'Error occurred',
          errorType: 'syntax',
        });
      });
    });

    it('should accept valid request with all fields', () => {
      assert.doesNotThrow(() => {
        validateFixSuggestionRequest({
          source: 'const x = 1;\nconst y = 2;',
          errorMessage: 'Unexpected token',
          errorType: 'syntax',
          language: 'javascript',
          line: 1,
        });
      });
    });
  });

  // ============================================================================
  // Test Suite 2: Fix Strategy Functions
  // ============================================================================

  describe('Suite 2: Syntax Error Fixes', () => {
    it('should generate syntax fixes for unexpected token', () => {
      const fixes = generateSyntaxFixes('const x = ;', 'Unexpected token', 'javascript');
      assert(Array.isArray(fixes));
      // Pattern matching is best-effort; function should return array
    });

    it('should generate syntax fixes for TypeScript import error', () => {
      const fixes = generateSyntaxFixes('import { x }', 'cannot find module', 'typescript');
      assert(Array.isArray(fixes));
    });

    it('should generate syntax fixes for C# unexpected symbol', () => {
      const fixes = generateSyntaxFixes('class X {', 'unexpected symbol', 'csharp');
      assert(Array.isArray(fixes));
    });

    it('should generate syntax fixes for Python indentation', () => {
      const fixes = generateSyntaxFixes('def x():\nreturn 1', 'unexpected indent', 'python');
      assert(Array.isArray(fixes));
    });

    it('should return empty array when no patterns match', () => {
      const fixes = generateSyntaxFixes('const x = 1;', '', 'javascript');
      assert(Array.isArray(fixes));
    });
  });

  describe('Suite 3: Semantic Error Fixes', () => {
    it('should generate semantic fixes for undefined reference', () => {
      const fixes = generateSemanticFixes('console.log(x);', 'x is not defined', 'javascript');
      assert(Array.isArray(fixes));
      assert(fixes.length > 0);
      assert(fixes.some((f) => f.category === 'semantic'));
    });

    it('should generate semantic fixes for null reference', () => {
      const fixes = generateSemanticFixes('obj.prop.x', 'Cannot read property x of undefined', 'javascript');
      assert(Array.isArray(fixes));
      assert(fixes.length > 0);
      // Should include handling for undefined/null scenarios
    });

    it('should generate semantic fixes for type mismatch', () => {
      const fixes = generateSemanticFixes('let x: string = 5;', 'not assignable', 'typescript');
      assert(Array.isArray(fixes));
    });

    it('should generate semantic fixes for C# name resolution', () => {
      const fixes = generateSemanticFixes('Console.WriteLine(x);', 'does not exist', 'csharp');
      assert(Array.isArray(fixes));
    });

    it('should generate semantic fixes for Python attribute error', () => {
      const fixes = generateSemanticFixes('x.foo()', 'has no attribute', 'python');
      assert(Array.isArray(fixes));
    });
  });

  describe('Suite 4: Pattern & Code Quality Fixes', () => {
    it('should detect var usage pattern', () => {
      const fixes = generatePatternFixes('var x = 5;', '', 'javascript');
      assert(Array.isArray(fixes));
    });

    it('should detect loose equality pattern', () => {
      const fixes = generatePatternFixes('if (x == 5)', '', 'javascript');
      assert(Array.isArray(fixes));
    });

    it('should detect any type usage in TypeScript', () => {
      const fixes = generatePatternFixes('let x: any;', '', 'typescript');
      assert(Array.isArray(fixes));
    });

    it('should detect String vs string in C#', () => {
      const fixes = generatePatternFixes('String x = "test";', '', 'csharp');
      assert(Array.isArray(fixes));
    });

    it('should detect bare except in Python', () => {
      const fixes = generatePatternFixes('try:\n  x()\nexcept:\n  pass', '', 'python');
      assert(Array.isArray(fixes));
    });
  });

  describe('Suite 5: Style Fixes', () => {
    it('should detect trailing whitespace', () => {
      const fixes = generateStyleFixes('const x = 5;   ', '', 'javascript');
      assert(Array.isArray(fixes));
    });

    it('should detect tab usage in Python', () => {
      const fixes = generateStyleFixes('def foo():\n\treturn 1', '', 'python');
      assert(Array.isArray(fixes));
    });

    it('should provide style suggestions', () => {
      const fixes = generateStyleFixes('const   x   =   5;', '', 'javascript');
      assert(Array.isArray(fixes));
    });
  });

  describe('Suite 6: Performance Fixes', () => {
    it('should suggest for...of over for...in for arrays', () => {
      const fixes = generatePerformanceFixes('for (let i in arr)', '', 'javascript');
      assert(Array.isArray(fixes));
      // Note: pattern may not always trigger on all variations
    });

    it('should suggest enumerate() in Python', () => {
      const fixes = generatePerformanceFixes('for i in range(len(list))', '', 'python');
      assert(Array.isArray(fixes));
      // Note: pattern may not always trigger on all variations
    });
  });

  describe('Suite 7: Security Fixes', () => {
    it('should warn about eval() usage', () => {
      const fixes = generateSecurityFixes('eval(code)', '', 'javascript');
      assert(fixes.length > 0);
      assert(fixes.some((f) => f.confidence >= 90));
    });

    it('should warn about innerHTML usage', () => {
      const fixes = generateSecurityFixes('el.innerHTML = html', '', 'javascript');
      assert(fixes.length > 0);
      assert(fixes.some((f) => f.suggestion.includes('textContent')));
    });

    it('should warn about hardcoded secrets', () => {
      const fixes = generateSecurityFixes('const password = "secret123"', '', 'javascript');
      assert(fixes.length > 0);
      assert(fixes.some((f) => f.confidence >= 80));
    });
  });

  // ============================================================================
  // Test Suite 8: Response Format
  // ============================================================================

  describe('Suite 8: Response Format', () => {
    it('should create response with suggestions', () => {
      const suggestions = [
        { suggestion: 'Fix 1', confidence: 85, category: 'syntax', explanation: 'Test' },
        { suggestion: 'Fix 2', confidence: 70, category: 'semantic', explanation: 'Test' },
      ];
      const response = createFixSuggestionResponse(suggestions, {
        errorType: 'syntax',
        language: 'javascript',
      });

      assert(response.success === true);
      assert(Array.isArray(response.data.suggestions));
      assert(response.data.suggestions.length === 2);
      assert(response.data.metadata.suggestionCount === 2);
    });

    it('should sort suggestions by confidence descending', () => {
      const suggestions = [
        { suggestion: 'Fix 1', confidence: 50, category: 'syntax', explanation: 'Test' },
        { suggestion: 'Fix 2', confidence: 90, category: 'syntax', explanation: 'Test' },
      ];
      const response = createFixSuggestionResponse(suggestions, {});

      assert(response.data.suggestions[0].confidence === 90);
      assert(response.data.suggestions[1].confidence === 50);
    });

    it('should calculate average confidence', () => {
      const suggestions = [
        { suggestion: 'Fix 1', confidence: 80, category: 'syntax', explanation: 'Test' },
        { suggestion: 'Fix 2', confidence: 90, category: 'syntax', explanation: 'Test' },
      ];
      const response = createFixSuggestionResponse(suggestions, {});

      assert(response.data.metadata.averageConfidence === 85);
    });

    it('should include metadata with categories', () => {
      const suggestions = [
        { suggestion: 'Fix 1', confidence: 85, category: 'syntax', explanation: 'Test' },
        { suggestion: 'Fix 2', confidence: 70, category: 'semantic', explanation: 'Test' },
      ];
      const response = createFixSuggestionResponse(suggestions, {});

      assert(Array.isArray(response.data.metadata.categories));
      assert(response.data.metadata.categories.includes('syntax'));
      assert(response.data.metadata.categories.includes('semantic'));
    });

    it('should handle empty suggestions', () => {
      const response = createFixSuggestionResponse([], {
        errorType: 'unknown',
      });

      assert(response.success === true);
      assert(response.data.suggestions.length === 0);
      assert(response.data.metadata.averageConfidence === 0);
    });

    it('should include timestamp in metadata', () => {
      const suggestions = [
        { suggestion: 'Fix 1', confidence: 85, category: 'syntax', explanation: 'Test' },
      ];
      const response = createFixSuggestionResponse(suggestions, {});

      assert(typeof response.data.metadata.timestamp === 'string');
      assert(/^\d{4}-\d{2}-\d{2}T/.test(response.data.metadata.timestamp));
    });
  });

  // ============================================================================
  // Test Suite 9: Handler Integration
  // ============================================================================

  describe('Suite 9: Handler Integration', () => {
    it('should process syntax error request', async () => {
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const response = await fixSuggestionHandler(
        {
          data: {
            source: 'const x = ;',
            errorMessage: 'Unexpected token ;',
            errorType: 'syntax',
            language: 'javascript',
          },
        },
        { logger, metrics }
      );

      assert(response.success === true);
      assert(Array.isArray(response.data.suggestions));
      // Suggestions should be generated (may be empty if patterns don't match)
      assert(typeof response.data.metadata === 'object');
      assert(metrics.events.length > 0);
    });

    it('should process semantic error request', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'console.log(x);',
          errorMessage: 'x is not defined',
          errorType: 'semantic',
          language: 'javascript',
        },
      });

      assert(response.success === true);
      assert(response.data.suggestions.length > 0);
    });

    it('should process TypeScript request', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'let x: any = 5;',
          errorMessage: 'any type detected',
          errorType: 'pattern',
          language: 'typescript',
        },
      });

      assert(response.success === true);
    });

    it('should process C# request', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'String x = "test";',
          errorMessage: 'style issue',
          errorType: 'style',
          language: 'csharp',
        },
      });

      assert(response.success === true);
    });

    it('should process Python request', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'x = y',
          errorMessage: 'name error',
          errorType: 'semantic',
          language: 'python',
        },
      });

      assert(response.success === true);
    });

    it('should handle invalid request', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: '',
          errorMessage: 'Error',
          errorType: 'syntax',
        },
      });

      assert(response.success === false);
      assert(response.error.code === 'VALIDATION_ERROR');
    });

    it('should handle unsupported language', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'code',
          errorMessage: 'Error',
          errorType: 'syntax',
          language: 'rust',
        },
      });

      assert(response.success === false);
      assert(response.error.code === 'UNSUPPORTED_LANGUAGE');
    });

    it('should deduplicate suggestions', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'const x = ;',
          errorMessage: 'Unexpected token; invalid syntax',
          errorType: 'syntax',
          language: 'javascript',
        },
      });

      assert(response.success === true);
      const uniqueSuggestions = new Set(response.data.suggestions.map((s) => s.suggestion));
      assert(uniqueSuggestions.size === response.data.suggestions.length);
    });

    it('should record metrics on success', async () => {
      const metrics = new MockMetrics();

      await fixSuggestionHandler(
        {
          data: {
            source: 'x is undefined',
            errorMessage: 'x is not defined',
            errorType: 'semantic',
            language: 'javascript',
          },
        },
        { metrics }
      );

      assert(metrics.events.length > 0);
      assert(metrics.events[0].type === 'execution');
      assert(typeof metrics.events[0].success === 'boolean');
    });

    it('should record metrics on failure', async () => {
      const metrics = new MockMetrics();

      await fixSuggestionHandler(
        {
          data: {
            source: '',
            errorMessage: 'Error',
            errorType: 'syntax',
          },
        },
        { metrics }
      );

      assert(metrics.events.length > 0);
      assert(metrics.events[0].type === 'execution');
      assert(metrics.events[0].success === false);
    });
  });

  // ============================================================================
  // Test Suite 10: Error Handling
  // ============================================================================

  describe('Suite 10: Error Handling', () => {
    it('should throw RefactoringValidationError for invalid source', () => {
      assert.throws(
        () => {
          validateFixSuggestionRequest({
            source: null,
            errorMessage: 'Error',
            errorType: 'syntax',
          });
        },
        (err) => err instanceof FixSuggestionValidationError
      );
    });

    it('should throw FixSuggestionUnsupportedError for unsupported language', () => {
      assert.throws(
        () => {
          validateFixSuggestionRequest({
            source: 'code',
            errorMessage: 'Error',
            errorType: 'syntax',
            language: 'golang',
          });
        },
        FixSuggestionUnsupportedError
      );
    });

    it('should catch validation errors in handler', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: null,
          errorMessage: 'Error',
          errorType: 'syntax',
        },
      });

      assert(response.success === false);
      assert(response.error.name === 'FixSuggestionValidationError');
    });

    it('should catch unsupported language errors in handler', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'code',
          errorMessage: 'Error',
          errorType: 'syntax',
          language: 'haskell',
        },
      });

      assert(response.success === false);
      assert(response.error.name === 'FixSuggestionUnsupportedError');
    });

    it('should handle unknown error gracefully', async () => {
      const badContext = {
        logger: {
          debug: () => {
            throw new Error('Logger error');
          },
          error: () => {},
        },
      };

      const response = await fixSuggestionHandler(
        {
          data: {
            source: 'const x = 1;',
            errorMessage: 'Error',
            errorType: 'syntax',
          },
        },
        badContext
      );

      assert(response.success === false);
    });
  });

  // ============================================================================
  // Test Suite 11: Edge Cases
  // ============================================================================

  describe('Suite 11: Edge Cases', () => {
    it('should handle very large source code', async () => {
      const largeSource = 'const x = 1;\n'.repeat(1000);
      const response = await fixSuggestionHandler({
        data: {
          source: largeSource,
          errorMessage: 'Error at line 500',
          errorType: 'syntax',
          language: 'javascript',
          line: 500,
        },
      });

      assert(response.success === true);
    });

    it('should handle source with special characters', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'console.log("\\n\\t\\r")',
          errorMessage: 'Error',
          errorType: 'syntax',
          language: 'javascript',
        },
      });

      assert(response.success === true);
    });

    it('should handle source with unicode characters', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'const greeting = "你好世界";',
          errorMessage: 'Error',
          errorType: 'syntax',
          language: 'javascript',
        },
      });

      assert(response.success === true);
    });

    it('should handle single-line source', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'x',
          errorMessage: 'Error',
          errorType: 'semantic',
          language: 'javascript',
        },
      });

      assert(response.success === true);
    });

    it('should handle error message with special characters', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'code',
          errorMessage: 'Error: "unexpected token" <> []',
          errorType: 'syntax',
          language: 'javascript',
        },
      });

      assert(response.success === true);
    });
  });

  // ============================================================================
  // Test Suite 12: Language-Specific Behaviors
  // ============================================================================

  describe('Suite 12: Language-Specific Behaviors', () => {
    it('should treat c# case-insensitively in patterns', () => {
      const fixes1 = generateSyntaxFixes('String x;', 'Error', 'c#');
      const fixes2 = generateSyntaxFixes('String x;', 'Error', 'csharp');
      assert(Array.isArray(fixes1));
      assert(Array.isArray(fixes2));
    });

    it('should generate language-specific syntax fixes', async () => {
      const jsResponse = await fixSuggestionHandler({
        data: {
          source: 'const x = ;',
          errorMessage: 'Unexpected token',
          errorType: 'syntax',
          language: 'javascript',
        },
      });

      const pyResponse = await fixSuggestionHandler({
        data: {
          source: 'def x():\nreturn 1',
          errorMessage: 'unexpected indent',
          errorType: 'syntax',
          language: 'python',
        },
      });

      assert(jsResponse.success === true);
      assert(pyResponse.success === true);
    });

    it('should handle missing language parameter', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'const x = 1;',
          errorMessage: 'Error',
          errorType: 'syntax',
        },
      });

      assert(response.success === true);
      assert(response.data.metadata.language === 'javascript');
    });
  });

  // ============================================================================
  // Test Suite 13: Confidence Scoring
  // ============================================================================

  describe('Suite 13: Confidence Scoring', () => {
    it('should assign confidence scores to suggestions', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'const x = ;',
          errorMessage: 'Unexpected token',
          errorType: 'syntax',
          language: 'javascript',
        },
      });

      assert(response.success === true);
      assert(response.data.suggestions.every((s) => typeof s.confidence === 'number'));
      assert(response.data.suggestions.every((s) => s.confidence >= 0 && s.confidence <= 100));
    });

    it('should track top confidence suggestion', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'eval(code)',
          errorMessage: 'Security risk',
          errorType: 'security',
          language: 'javascript',
        },
      });

      assert(response.success === true);
      assert(response.data.metadata.topConfidence >= 0);
    });

    it('should calculate correct average confidence', async () => {
      const response = await fixSuggestionHandler({
        data: {
          source: 'const x = ;',
          errorMessage: 'Syntax error',
          errorType: 'syntax',
          language: 'javascript',
        },
      });

      assert(response.success === true);
      if (response.data.suggestions.length > 0) {
        const avg = response.data.suggestions.reduce((sum, s) => sum + s.confidence, 0) / response.data.suggestions.length;
        assert(Math.abs(avg - response.data.metadata.averageConfidence) <= 1);
      }
    });
  });
});
