#!/usr/bin/env node

/**
 * Integration tests for model-info-handler.mjs
 *
 * Tests handler factory, message validation, collector interaction,
 * response normalization, error handling, and metrics recording.
 *
 * @module src/versions/v2.0.0/tests/model-info-handler.test.mjs
 */

import { strict as assert } from 'assert';
import {
  createModelInfoHandler,
  ModelInfoError,
  CollectorNotAvailableError
} from '../lib/model-info-handler.mjs';
import {
  createMockModelInfoCollector,
  createMockLogger,
  createMockMetrics
} from './mocks/model-info-collector-mock.mjs';

// Test utilities
const describe = globalThis.describe || function (name, fn) { console.log(`\n${name}`); fn(); };
const it = globalThis.it || function (name, fn) { console.log(`  ${name}`); return fn(); };
const beforeEach = globalThis.beforeEach || function (fn) { return fn(); };

let mockCollector;
let mockLogger;
let mockMetrics;
let handler;

beforeEach(() => {
  mockCollector = createMockModelInfoCollector('multi-provider');
  mockLogger = createMockLogger();
  mockMetrics = createMockMetrics();
  handler = createModelInfoHandler({
    collector: mockCollector,
    logger: mockLogger,
    metrics: mockMetrics
  });
});

// ===== Suite 1: Factory & Initialization (3 tests) =====

describe('Suite 1: Factory & Initialization', () => {
  it('should create handler successfully', () => {
    const h = createModelInfoHandler({ collector: mockCollector });
    assert(typeof h === 'function', 'Handler should be a function');
  });

  it('should accept all optional parameters', () => {
    const h = createModelInfoHandler({
      collector: mockCollector,
      logger: mockLogger,
      metrics: mockMetrics
    });
    assert(typeof h === 'function');
  });

  it('should degrade gracefully without collector', async () => {
    const h = createModelInfoHandler({ collector: null });
    const response = await h(
      { messageType: 'bridge:getModelInfo', messageId: 'test-1' },
      {}
    );
    assert(response.success === false);
    assert(response.error.code === -32603);
  });
});

// ===== Suite 2: Message Handling (4 tests) =====

describe('Suite 2: Message Handling', () => {
  it('should handle valid bridge:getModelInfo request', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-1', data: {} },
      {}
    );
    assert(response.success === true);
    assert(response.messageId === 'test-1');
  });

  it('should return current model in response', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-2', data: {} },
      {}
    );
    assert(response.data.currentModel !== null);
    assert(response.data.currentModel.provider === 'openai');
    assert(response.data.currentModel.model === 'gpt-4');
  });

  it('should return available models array', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-3', data: {} },
      {}
    );
    assert(Array.isArray(response.data.availableModels));
    assert(response.data.availableModels.length > 0);
  });

  it('should include token limits and capabilities', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-4', data: {} },
      {}
    );
    assert(response.data.modelCapabilities !== null);
    assert(response.data.modelCapabilities.contextLength > 0);
    assert(response.data.tokenLimits !== null);
    assert(response.data.tokenLimits.maxInputTokens > 0);
  });
});

// ===== Suite 3: Multiple Models (3 tests) =====

describe('Suite 3: Multiple Models', () => {
  it('should return multiple available models', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-5', data: {} },
      {}
    );
    assert(response.data.availableModels.length >= 2);
    assert(response.data.availableModels.some(m => m.provider === 'openai'));
    assert(response.data.availableModels.some(m => m.provider === 'anthropic'));
  });

  it('should have current model in available models', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-6', data: {} },
      {}
    );
    const current = response.data.currentModel;
    const available = response.data.availableModels;
    const found = available.some(
      m => m.provider === current.provider && m.model === current.model
    );
    assert(found, 'Current model should be in available models list');
  });

  it('should not cross-contaminate models', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-7', data: {} },
      {}
    );
    const models = response.data.availableModels;
    models.forEach((m, i) => {
      assert(m.provider !== undefined, `Model ${i} missing provider`);
      assert(m.model !== undefined, `Model ${i} missing model`);
      assert(m.title !== undefined, `Model ${i} missing title`);
    });
  });
});

// ===== Suite 4: Error Handling (3 tests) =====

describe('Suite 4: Error Handling', () => {
  it('should handle null current model gracefully', async () => {
    const emptyCollector = createMockModelInfoCollector('no-models');
    const h = createModelInfoHandler({ collector: emptyCollector });
    const response = await h(
      { messageType: 'bridge:getModelInfo', messageId: 'test-8', data: {} },
      {}
    );
    assert(response.success === true); // Should not throw
    assert(response.data.currentModel === null);
  });

  it('should wrap collector errors as ModelInfoError', async () => {
    mockCollector.throwError(new Error('Collector failed'));
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-9', data: {} },
      {}
    );
    assert(response.success === false);
    assert(response.error.code === -32603);
  });

  it('should handle missing provider/model fields', async () => {
    // Mock collector returns incomplete data
    mockCollector.availableModels = [{ provider: 'test' }]; // Missing model/title
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-10', data: {} },
      {}
    );
    // Should not throw; handler should gracefully handle incomplete data
    assert(typeof response === 'object');
  });
});

// ===== Suite 5: Metrics & Logging (2 tests) =====

describe('Suite 5: Metrics & Logging', () => {
  it('should record query latency metric', async () => {
    await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-11', data: {} },
      {}
    );
    const latencies = mockMetrics.getLatencies('model_info_query');
    assert(latencies.length > 0, 'Should record latency metric');
    assert(latencies[0].latency >= 0, 'Latency should be non-negative');
  });

  it('should log model info query', async () => {
    mockCollector.resetCallTracker();
    await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-12', data: {} },
      {}
    );
    const infoLogs = mockLogger.getLogs('info');
    assert(infoLogs.some(l => l.message.includes('Retrieved model info')));
  });
});

// ===== Suite 6: Performance (2 tests) =====

describe('Suite 6: Performance', () => {
  it('should complete query within reasonable time', async () => {
    const startTime = performance.now();
    await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-13', data: {} },
      {}
    );
    const elapsed = performance.now() - startTime;
    assert(elapsed < 500, `Query took ${elapsed}ms (should be <500ms)`);
  });

  it('should handle concurrent requests', async () => {
    const promises = [
      handler({ messageType: 'bridge:getModelInfo', messageId: 'test-14a', data: {} }, {}),
      handler({ messageType: 'bridge:getModelInfo', messageId: 'test-14b', data: {} }, {}),
      handler({ messageType: 'bridge:getModelInfo', messageId: 'test-14c', data: {} }, {})
    ];
    const responses = await Promise.all(promises);
    assert(responses.every(r => r.success === true));
  });
});

// ===== Suite 7: Response Structure (4 tests) =====

describe('Suite 7: Response Structure', () => {
  it('should include success flag', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-15', data: {} },
      {}
    );
    assert(typeof response.success === 'boolean');
  });

  it('should include messageId in response', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-16', data: {} },
      {}
    );
    assert(response.messageId === 'test-16');
  });

  it('should include lastUpdate timestamp', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-17', data: {} },
      {}
    );
    assert(response.data.lastUpdate !== undefined);
    assert(new Date(response.data.lastUpdate).getTime() > 0);
  });

  it('should include queryLatency', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-18', data: {} },
      {}
    );
    assert(typeof response.data.queryLatency === 'number');
    assert(response.data.queryLatency >= 0);
  });
});

// ===== Suite 8: Collector Integration (3 tests) =====

describe('Suite 8: Collector Integration', () => {
  it('should call GetCurrentModelAsync', async () => {
    mockCollector.resetCallTracker();
    await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-19', data: {} },
      {}
    );
    assert(mockCollector.wasCalled('GetCurrentModelAsync'));
  });

  it('should call GetAvailableModelsAsync', async () => {
    mockCollector.resetCallTracker();
    await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-20', data: {} },
      {}
    );
    assert(mockCollector.wasCalled('GetAvailableModelsAsync'));
  });

  it('should call GetModelCapabilitiesAsync for current provider', async () => {
    mockCollector.resetCallTracker();
    await handler(
      { messageType: 'bridge:getModelInfo', messageId: 'test-21', data: {} },
      {}
    );
    assert(
      mockCollector.getCallCount('GetModelCapabilitiesAsync') >= 1,
      'Should query capabilities for current model'
    );
  });
});

// ===== Suite 9: Edge Cases (3 tests) =====

describe('Suite 9: Edge Cases', () => {
  it('should handle invalid message type', async () => {
    const response = await handler(
      { messageType: 'bridge:unknownType', messageId: 'test-22', data: {} },
      {}
    );
    assert(response.success === false);
    assert(response.error.code === -32603);
  });

  it('should handle missing messageId', async () => {
    const response = await handler(
      { messageType: 'bridge:getModelInfo', data: {} },
      {}
    );
    // Should not throw; messageId may be undefined or null
    assert(typeof response === 'object');
  });

  it('should normalize empty available models', async () => {
    const emptyCollector = createMockModelInfoCollector('no-models');
    const h = createModelInfoHandler({ collector: emptyCollector });
    const response = await h(
      { messageType: 'bridge:getModelInfo', messageId: 'test-23', data: {} },
      {}
    );
    assert(response.data.availableModels !== null);
    assert(Array.isArray(response.data.availableModels));
  });
});

console.log('\n✓ All model-info-handler tests completed');
