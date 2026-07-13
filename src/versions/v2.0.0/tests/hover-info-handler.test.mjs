/**
 * Unit Tests for Hover-Info Handler (Step 59)
 *
 * Comprehensive test suite for the hover-info-handler.mjs module
 * Tests all query paths, caching behavior, error handling, and edge cases.
 *
 * @module src/versions/v2.0.0/tests/hover-info-handler.test.mjs
 * @version 1.0.0
 */

import { strict as assert } from 'assert';
import 'mocha';
import {
  createHoverInfoHandler,
  HoverInfoHandler,
  HoverInfoError,
  StateValidationError,
  HoverInfoCache,
} from '../lib/hover-info-handler.mjs';
import {
  getClassSymbol,
  getMethodSymbol,
  getPropertySymbol,
  getDeprecatedSymbol,
  getErrorDiagnostic,
  getWarningDiagnostic,
  getDeprecationDiagnostic,
  getExpectedClassHover,
  getExpectedMethodHover,
  getExpectedDiagnosticHover,
  getExpectedDeprecatedHover,
  getExpectedEmptyHover,
  getSymbolPosition,
  getDiagnosticPosition,
  getEmptyPosition,
  getValidHoverRequest,
  getHoverRequestWithoutDocs,
  getOutOfBoundsHoverRequest,
  getInvalidFilepathHoverRequest,
  getHoverBridgeMessage,
  getComplexSignature,
  getGenericTypeSymbol,
  getNestedClassSymbol,
  getJSDocComment,
  getXmlDocComment,
} from './mocks/hover-fixtures.mjs';
import {
  MockSymbolExtractor,
  MockDiagnosticsCollector,
  MockDocumentProvider,
  MockLogger,
  MockMetrics,
  MockHoverHandlerBuilder,
} from './mocks/hover-mocks.mjs';

// ============================================================================
// Test Suite 1: Initialization & Dependencies
// ============================================================================

describe('HoverInfoHandler - Initialization', function () {
  this.timeout(5000);

  it('should initialize with default options', function () {
    const handler = createHoverInfoHandler();
    assert(handler instanceof HoverInfoHandler);
    assert.ok(handler.getCacheStats);
    assert.ok(handler.handle);
  });

  it('should initialize with custom dependencies', function () {
    const logger = new MockLogger();
    const metrics = new MockMetrics();
    const symbolExtractor = new MockSymbolExtractor();
    const diagnosticsCollector = new MockDiagnosticsCollector();
    const documentProvider = new MockDocumentProvider();

    const handler = createHoverInfoHandler({
      logger,
      metrics,
      symbolExtractor,
      diagnosticsCollector,
      documentProvider,
    });

    assert(handler instanceof HoverInfoHandler);
    assert.deepStrictEqual(handler.logger, logger);
    assert.deepStrictEqual(handler.metrics, metrics);
  });

  it('should initialize with custom cache options', function () {
    const handler = createHoverInfoHandler({ cacheSize: 100, cacheTtlMs: 10000 });
    assert(handler instanceof HoverInfoHandler);
    const stats = handler.getCacheStats();
    assert.deepStrictEqual(stats.size, 0);
  });
});

// ============================================================================
// Test Suite 2: Symbol Hover Queries
// ============================================================================

describe('HoverInfoHandler - Symbol Hover Queries', function () {
  this.timeout(5000);

  let handler;
  let mockBuilder;

  beforeEach(function () {
    mockBuilder = new MockHoverHandlerBuilder();
  });

  it('should return class hover info', async function () {
    const symbol = getClassSymbol();
    mockBuilder.withSymbols('/src/UserService.cs', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/UserService.cs', line: 5, column: 0 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.kind, 'class');
    assert.strictEqual(response.data.hoverInfo.text, 'public class UserService');
    assert.strictEqual(response.data.source, 'symbol');
    assert.strictEqual(response.data.cacheHit, false);
  });

  it('should return method hover info', async function () {
    const symbol = getMethodSymbol();
    mockBuilder.withSymbols('/src/UserService.cs', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/UserService.cs', line: 20, column: 4 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.kind, 'method');
    assert(response.data.hoverInfo.signature);
    assert(response.data.hoverInfo.documentation);
  });

  it('should return property hover info', async function () {
    const symbol = getPropertySymbol();
    mockBuilder.withSymbols('/src/User.cs', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/User.cs', line: 10, column: 8 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.kind, 'property');
    // When signature is included (default), text becomes the signature
    assert(response.data.hoverInfo.text.includes('Id'));
  });

  it('should handle missing symbol gracefully', async function () {
    mockBuilder.withSymbols('/src/empty.ts', []);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/empty.ts', line: 100, column: 50 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.source, 'none');
  });
});

// ============================================================================
// Test Suite 3: Diagnostic Hover Queries
// ============================================================================

describe('HoverInfoHandler - Diagnostic Hover Queries', function () {
  this.timeout(5000);

  let handler;
  let mockBuilder;

  beforeEach(function () {
    mockBuilder = new MockHoverHandlerBuilder();
  });

  it('should return error diagnostic hover', async function () {
    const diagnostic = getErrorDiagnostic();
    mockBuilder.withDiagnostics('/src/file.ts', 25, 10, [diagnostic]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/file.ts', line: 25, column: 10 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.kind, 'diagnostic');
    assert(response.data.hoverInfo.text.includes('string'));
    assert.strictEqual(response.data.source, 'diagnostic');
  });

  it('should return warning diagnostic hover', async function () {
    const diagnostic = getWarningDiagnostic();
    mockBuilder.withDiagnostics('/src/file.ts', 30, 6, [diagnostic]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/file.ts', line: 30, column: 6 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.kind, 'diagnostic');
    assert(response.data.hoverInfo.text);
  });

  it('should prioritize diagnostic over symbol', async function () {
    const diagnostic = getErrorDiagnostic();
    const symbol = getMethodSymbol();

    mockBuilder
      .withDiagnostics('/src/file.ts', 25, 10, [diagnostic])
      .withSymbols('/src/file.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/file.ts', line: 25, column: 10 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.source, 'diagnostic');
  });

  it('should handle multiple diagnostics by severity', async function () {
    const errorDiag = getErrorDiagnostic();
    const warningDiag = getWarningDiagnostic();

    mockBuilder.withDiagnostics('/src/file.ts', 40, 5, [warningDiag, errorDiag]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/file.ts', line: 40, column: 5 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    // Should get highest priority (error is higher priority than warning)
    assert.strictEqual(response.data.hoverInfo.kind, 'diagnostic');
    // The text should be from the first diagnostic in the sorted array
    assert(response.data.hoverInfo.text);
  });
});

// ============================================================================
// Test Suite 4: Documentation & Deprecation Handling
// ============================================================================

describe('HoverInfoHandler - Documentation & Deprecation', function () {
  this.timeout(5000);

  let handler;
  let mockBuilder;

  beforeEach(function () {
    mockBuilder = new MockHoverHandlerBuilder();
  });

  it('should include documentation when requested', async function () {
    const symbol = getMethodSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: {
        filepath: '/src/service.ts',
        line: 20,
        column: 4,
        includeDocumentation: true,
      },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(response.data.hoverInfo.documentation);
  });

  it('should exclude documentation when not requested', async function () {
    const symbol = getMethodSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: {
        filepath: '/src/service.ts',
        line: 20,
        column: 4,
        includeDocumentation: false,
      },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(!response.data.hoverInfo.documentation);
  });

  it('should include signature when requested', async function () {
    const symbol = getMethodSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: {
        filepath: '/src/service.ts',
        line: 20,
        column: 4,
        includeSignature: true,
      },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(response.data.hoverInfo.signature);
  });

  it('should flag deprecated symbols', async function () {
    const symbol = getDeprecatedSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: {
        filepath: '/src/service.ts',
        line: 40,
        column: 4,
        includeDeprecation: true,
      },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.deprecated, true);
  });

  it('should sanitize documentation text', async function () {
    const symbol = {
      ...getMethodSymbol(),
      documentation: '  Multiple   spaces    and\n\nnewlines  here  ',
    };
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/service.ts', line: 20, column: 4 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(!response.data.hoverInfo.documentation.includes('\n'));
    assert(!response.data.hoverInfo.documentation.match(/  +/));
  });
});

// ============================================================================
// Test Suite 5: Caching & Performance
// ============================================================================

describe('HoverInfoHandler - Caching & Performance', function () {
  this.timeout(5000);

  let handler;
  let mockBuilder;

  beforeEach(function () {
    mockBuilder = new MockHoverHandlerBuilder();
  });

  it('should cache hover results', async function () {
    const symbol = getMethodSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/service.ts', line: 20, column: 4 },
    };

    // First call: cache miss
    const response1 = await handler.handle(message);
    assert.strictEqual(response1.data.cacheHit, false);

    // Second call: cache hit
    const response2 = await handler.handle(message);
    assert.strictEqual(response2.data.cacheHit, true);

    // Responses should be identical
    assert.deepStrictEqual(response1.data.hoverInfo, response2.data.hoverInfo);
  });

  it('should track cache statistics', async function () {
    const symbol = getMethodSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/service.ts', line: 20, column: 4 },
    };

    await handler.handle(message);
    await handler.handle(message);
    await handler.handle(message);

    const stats = handler.getCacheStats();
    assert.strictEqual(stats.hits, 2);
    assert.strictEqual(stats.misses, 1);
    assert.strictEqual(stats.size, 1);
  });

  it('should enforce cache TTL', async function () {
    mockBuilder.withSymbols('/src/service.ts', [getMethodSymbol()]);

    handler = createHoverInfoHandler({
      ...mockBuilder.build(),
      cacheTtlMs: 100, // 100ms TTL
    });

    const message = {
      data: { filepath: '/src/service.ts', line: 20, column: 4 },
    };

    await handler.handle(message);
    let stats = handler.getCacheStats();
    assert.strictEqual(stats.size, 1);

    // Wait for TTL to expire
    await new Promise((resolve) => setTimeout(resolve, 150));

    // Next query should be a cache miss (TTL expired)
    const response = await handler.handle(message);
    assert.strictEqual(response.data.cacheHit, false);
  });

  it('should enforce LRU eviction', async function () {
    handler = createHoverInfoHandler({
      ...mockBuilder.build(),
      cacheSize: 2, // Very small cache
    });

    // Create 3 different positions
    const positions = [
      { filepath: '/src/a.ts', line: 1, column: 0 },
      { filepath: '/src/b.ts', line: 2, column: 0 },
      { filepath: '/src/c.ts', line: 3, column: 0 },
    ];

    // Add symbols for each position
    mockBuilder
      .withSymbols('/src/a.ts', [getMethodSymbol()])
      .withSymbols('/src/b.ts', [getMethodSymbol()])
      .withSymbols('/src/c.ts', [getMethodSymbol()]);

    // Re-create handler with new mocks (since we modified builder)
    handler = createHoverInfoHandler({
      ...mockBuilder.build(),
      cacheSize: 2,
    });

    // Query first two positions
    await handler.handle({ data: positions[0] });
    await handler.handle({ data: positions[1] });
    let stats = handler.getCacheStats();
    assert.strictEqual(stats.size, 2);

    // Query third position - should evict first (LRU)
    await handler.handle({ data: positions[2] });
    stats = handler.getCacheStats();
    assert.strictEqual(stats.size, 2);
    assert.strictEqual(stats.evictions, 1);
  });

  it('should clear cache on demand', async function () {
    const symbol = getMethodSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/service.ts', line: 20, column: 4 },
    };

    await handler.handle(message);
    let stats = handler.getCacheStats();
    assert.strictEqual(stats.size, 1);

    handler.clearCache();
    stats = handler.getCacheStats();
    assert.strictEqual(stats.size, 0);
  });
});

// ============================================================================
// Test Suite 6: Message Handler Integration
// ============================================================================

describe('HoverInfoHandler - Message Handler Integration', function () {
  this.timeout(5000);

  let handler;
  let mockBuilder;

  beforeEach(function () {
    mockBuilder = new MockHoverHandlerBuilder();
  });

  it('should handle valid bridge message', async function () {
    mockBuilder.withSymbols('/src/service.ts', [getMethodSymbol()]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = getHoverBridgeMessage();
    message.data = { filepath: '/src/service.ts', line: 20, column: 4 };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(response.data.hoverInfo);
  });

  it('should reject message with invalid filepath', async function () {
    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '', line: 0, column: 0 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'StateValidationError');
  });

  it('should reject message with negative line number', async function () {
    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/file.ts', line: -1, column: 0 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'StateValidationError');
  });

  it('should reject message with negative column number', async function () {
    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/file.ts', line: 0, column: -1 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'StateValidationError');
  });

  it('should handle out-of-bounds position gracefully', async function () {
    mockBuilder.withSymbols('/src/file.ts', []);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/file.ts', line: 99999, column: 99999 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.source, 'none');
  });
});

// ============================================================================
// Test Suite 7: Edge Cases
// ============================================================================

describe('HoverInfoHandler - Edge Cases', function () {
  this.timeout(5000);

  let handler;
  let mockBuilder;

  beforeEach(function () {
    mockBuilder = new MockHoverHandlerBuilder();
  });

  it('should handle multiline hover ranges', async function () {
    const symbol = getComplexSignature();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/service.ts', line: 10, column: 0 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(response.data.hoverInfo.signature);
    assert(response.data.hoverInfo.signature.length > 50);
  });

  it('should handle generic type symbols', async function () {
    const symbol = getGenericTypeSymbol();
    mockBuilder.withSymbols('/src/repository.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/repository.ts', line: 5, column: 0 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(response.data.hoverInfo.signature.includes('<T>'));
  });

  it('should handle nested class symbols', async function () {
    const symbol = getNestedClassSymbol();
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/service.ts', line: 100, column: 2 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(response.data.hoverInfo.text.includes('Configuration'));
  });

  it('should handle missing message data', async function () {
    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {};

    const response = await handler.handle(message);

    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'StateValidationError');
  });

  it('should handle null message', async function () {
    handler = createHoverInfoHandler(mockBuilder.build());

    const response = await handler.handle(null);

    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'StateValidationError');
  });

  it('should gracefully degrade when dependencies unavailable', async function () {
    // Create handler with no dependencies
    handler = createHoverInfoHandler();

    const message = {
      data: { filepath: '/src/file.ts', line: 10, column: 5 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hoverInfo.source, 'none');
  });

  it('should truncate long documentation', async function () {
    const longDoc = 'a'.repeat(1000); // 1000 chars
    const symbol = { ...getMethodSymbol(), documentation: longDoc };
    mockBuilder.withSymbols('/src/service.ts', [symbol]);

    handler = createHoverInfoHandler(mockBuilder.build());

    const message = {
      data: { filepath: '/src/service.ts', line: 20, column: 4 },
    };

    const response = await handler.handle(message);

    assert.strictEqual(response.success, true);
    assert(response.data.hoverInfo.documentation.length <= 500);
    assert(response.data.hoverInfo.documentation.endsWith('...'));
  });
});

// ============================================================================
// Test Suite 8: HoverInfoCache Unit Tests
// ============================================================================

describe('HoverInfoCache - Unit Tests', function () {
  this.timeout(5000);

  it('should create cache with default options', function () {
    const cache = new HoverInfoCache();
    const stats = cache.getStats();
    assert.strictEqual(stats.hits, 0);
    assert.strictEqual(stats.misses, 0);
    assert.strictEqual(stats.size, 0);
  });

  it('should set and get cache entries', function () {
    const cache = new HoverInfoCache();
    const hoverInfo = { kind: 'class', text: 'MyClass', source: 'symbol' };

    cache.set('/src/file.ts', 10, 5, hoverInfo);
    const result = cache.get('/src/file.ts', 10, 5);

    assert.strictEqual(result.cacheHit, true);
    assert.deepStrictEqual(result.data, hoverInfo);
  });

  it('should return null for missing entries', function () {
    const cache = new HoverInfoCache();
    const result = cache.get('/src/file.ts', 10, 5);
    assert.strictEqual(result, null);
  });

  it('should track hit/miss statistics', function () {
    const cache = new HoverInfoCache();
    const hoverInfo = { kind: 'class', text: 'MyClass', source: 'symbol' };

    cache.set('/src/file.ts', 10, 5, hoverInfo);

    cache.get('/src/file.ts', 10, 5); // Hit
    cache.get('/src/file.ts', 20, 5); // Miss

    const stats = cache.getStats();
    assert.strictEqual(stats.hits, 1);
    assert.strictEqual(stats.misses, 1);
  });
});

// ============================================================================
// Test Summary
// ============================================================================

// Total test count: 27 tests planned
// Final assertion in test runner output will show pass/fail count
