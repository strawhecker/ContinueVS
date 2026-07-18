import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import {
  createStreamingResponseHandler,
  StreamingError,
  InvalidStreamingRequestError,
  ModelNotFoundError,
  StreamInterruptedError
} from '../lib/streaming-response-handler.mjs';

// Mock utilities
function createMockIpcClient() {
  let streamActive = false;
  return {
    hasModel: async (title) => title !== 'missing-model',
    streamChat: (title, messages, onChunk, onError, onComplete) => {
      streamActive = true;
      const simulateStream = async () => {
        try {
          if (title === 'error-model') {
            onError(new Error('Model error'));
            return;
          }
          
          const chunks = ['Hello', ' ', 'world', '!'];
          for (const chunk of chunks) {
            if (!streamActive) break;
            onChunk({ role: 'assistant', content: chunk, done: false });
            await new Promise(r => setTimeout(r, 10));
          }
          onChunk({ role: 'assistant', content: '', done: true });
          onComplete();
        } catch (err) {
          onError(err);
        }
      };
      simulateStream().catch(onError);
    }
  };
}

function createMockLogger() {
  const logs = [];
  return {
    debug: (msg) => logs.push({ level: 'debug', msg }),
    info: (msg) => logs.push({ level: 'info', msg }),
    warn: (msg) => logs.push({ level: 'warn', msg }),
    error: (msg) => logs.push({ level: 'error', msg }),
    getLogs: () => logs
  };
}

function createMockMetrics() {
  const events = [];
  const latencies = [];
  return {
    recordLatency: (metric, value, status) => latencies.push({ metric, value, status }),
    recordEvent: (event, data) => events.push({ event, data }),
    getLatencies: () => latencies,
    getEvents: () => events
  };
}

describe('Streaming-Response Handler', () => {
  let handler;
  let mockIpc;
  let mockLogger;
  let mockMetrics;

  beforeEach(() => {
    mockIpc = createMockIpcClient();
    mockLogger = createMockLogger();
    mockMetrics = createMockMetrics();
    handler = createStreamingResponseHandler({
      logger: mockLogger,
      metrics: mockMetrics,
      ipcClient: () => mockIpc
    });
  });

  describe('Suite 1: Initialization & Defaults', () => {
    it('creates handler without options', () => {
      const h = createStreamingResponseHandler();
      expect(h).to.be.a('function');
    });

    it('accepts logger option', () => {
      const logger = { debug: () => {}, info: () => {}, warn: () => {}, error: () => {} };
      const h = createStreamingResponseHandler({ logger });
      expect(h).to.be.a('function');
    });

    it('accepts metrics option', () => {
      const metrics = { recordLatency: () => {}, recordEvent: () => {} };
      const h = createStreamingResponseHandler({ metrics });
      expect(h).to.be.a('function');
    });

    it('accepts ipcClient option', () => {
      const ipc = () => ({ streamChat: () => {} });
      const h = createStreamingResponseHandler({ ipcClient: ipc });
      expect(h).to.be.a('function');
    });
  });

  describe('Suite 2: Request Validation', () => {
    it('rejects null message', async () => {
      try {
        await handler(null);
        expect.fail('Should throw InvalidStreamingRequestError');
      } catch (err) {
        expect(err).to.be.instanceof(InvalidStreamingRequestError);
      }
    });

    it('rejects message without data', async () => {
      try {
        await handler({ messageId: 'test' });
        expect.fail('Should throw InvalidStreamingRequestError');
      } catch (err) {
        expect(err).to.be.instanceof(InvalidStreamingRequestError);
      }
    });

    it('rejects empty title', async () => {
      try {
        await handler({ data: { title: '', messages: [{ role: 'user', content: 'hi' }] } });
        expect.fail('Should throw InvalidStreamingRequestError');
      } catch (err) {
        expect(err).to.be.instanceof(InvalidStreamingRequestError);
        expect(err.field).to.equal('title');
      }
    });

    it('rejects non-array messages', async () => {
      try {
        await handler({ data: { title: 'gpt-4', messages: 'not-array' } });
        expect.fail('Should throw InvalidStreamingRequestError');
      } catch (err) {
        expect(err).to.be.instanceof(InvalidStreamingRequestError);
        expect(err.field).to.equal('messages');
      }
    });

    it('rejects empty messages array', async () => {
      try {
        await handler({ data: { title: 'gpt-4', messages: [] } });
        expect.fail('Should throw InvalidStreamingRequestError');
      } catch (err) {
        expect(err).to.be.instanceof(InvalidStreamingRequestError);
        expect(err.field).to.equal('messages.length');
      }
    });

    it('rejects message without role', async () => {
      try {
        await handler({ data: { title: 'gpt-4', messages: [{ content: 'hi' }] } });
        expect.fail('Should throw InvalidStreamingRequestError');
      } catch (err) {
        expect(err).to.be.instanceof(InvalidStreamingRequestError);
        expect(err.field).to.include('role');
      }
    });

    it('rejects message without content', async () => {
      try {
        await handler({ data: { title: 'gpt-4', messages: [{ role: 'user' }] } });
        expect.fail('Should throw InvalidStreamingRequestError');
      } catch (err) {
        expect(err).to.be.instanceof(InvalidStreamingRequestError);
        expect(err.field).to.include('content');
      }
    });
  });

  describe('Suite 3: Happy Path Streaming', () => {
    it('streams chunks successfully', async () => {
      const result = await handler({
        messageId: 'test-1',
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      expect(result).to.have.property('role', 'assistant');
      expect(result).to.have.property('content', 'Hello world!');
      expect(result).to.have.property('chunks', 4);
      expect(result).to.have.property('latency');
      expect(result).to.have.property('estimatedTokens');
    });

    it('records metrics on success', async () => {
      await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      const latencies = mockMetrics.getLatencies();
      expect(latencies).to.have.length.greaterThan(0);
      expect(latencies[0].status).to.equal('success');
    });

    it('logs info on successful completion', async () => {
      await handler({
        messageId: 'test-2',
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      const logs = mockLogger.getLogs();
      const infos = logs.filter(l => l.level === 'info');
      expect(infos.length).to.be.greaterThan(0);
    });
  });

  describe('Suite 4: Chunk Forwarding', () => {
    it('forwards chunks via onChunk callback', async () => {
      const forwardedChunks = [];
      await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      }, {
        onChunk: (data) => forwardedChunks.push(data)
      });
      expect(forwardedChunks).to.have.length.greaterThan(0);
      expect(forwardedChunks[0]).to.have.property('sequenceNumber', 0);
      expect(forwardedChunks[0]).to.have.property('chunk');
    });

    it('includes sequence numbers in forwarded chunks', async () => {
      const forwardedChunks = [];
      await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      }, {
        onChunk: (data) => forwardedChunks.push(data)
      });
      for (let i = 0; i < forwardedChunks.length; i++) {
        expect(forwardedChunks[i].sequenceNumber).to.equal(i);
      }
    });

    it('handles onChunk callback errors gracefully', async () => {
      const result = await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      }, {
        onChunk: () => {
          throw new Error('Callback error');
        }
      });
      expect(result).to.have.property('content');
    });
  });

  describe('Suite 5: Error Handling', () => {
    it('throws ModelNotFoundError for missing model', async () => {
      try {
        await handler({
          data: { title: 'missing-model', messages: [{ role: 'user', content: 'hi' }] }
        });
        expect.fail('Should throw ModelNotFoundError');
      } catch (err) {
        expect(err).to.be.instanceof(ModelNotFoundError);
      }
    });

    it('throws StreamInterruptedError on stream error', async () => {
      try {
        await handler({
          data: { title: 'error-model', messages: [{ role: 'user', content: 'hi' }] }
        });
        expect.fail('Should throw StreamInterruptedError');
      } catch (err) {
        expect(err).to.be.instanceof(StreamInterruptedError);
      }
    });

    it('records error metrics on failure', async () => {
      try {
        await handler({
          data: { title: 'missing-model', messages: [{ role: 'user', content: 'hi' }] }
        });
      } catch (err) {
        // Expected
      }
      const events = mockMetrics.getEvents();
      expect(events.some(e => e.event.includes('error'))).to.be.true;
    });

    it('logs errors appropriately', async () => {
      try {
        await handler({
          data: { title: 'missing-model', messages: [{ role: 'user', content: 'hi' }] }
        });
      } catch (err) {
        // Expected
      }
      const logs = mockLogger.getLogs();
      const errors = logs.filter(l => l.level === 'error' || l.level === 'warn');
      expect(errors.length).to.be.greaterThan(0);
    });
  });

  describe('Suite 6: Token Estimation', () => {
    it('estimates tokens from content', async () => {
      const result = await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      expect(result.estimatedTokens).to.be.greaterThan(0);
    });

    it('accumulates token estimates across chunks', async () => {
      const forwardedChunks = [];
      const result = await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      }, {
        onChunk: (data) => forwardedChunks.push(data)
      });
      expect(result.estimatedTokens).to.equal(3); // "Hello world!" ~ 12 chars / 4
    });
  });

  describe('Suite 7: Latency Tracking', () => {
    it('records latency in result', async () => {
      const result = await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      expect(result.latency).to.be.a('number');
      expect(result.latency).to.be.greaterThan(0);
    });

    it('records latency metrics', async () => {
      await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      const latencies = mockMetrics.getLatencies();
      expect(latencies.some(l => l.value > 0)).to.be.true;
    });
  });

  describe('Suite 8: Request IDs', () => {
    it('logs request ID if provided', async () => {
      await handler({
        messageId: 'req-123',
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      const logs = mockLogger.getLogs();
      expect(logs.some(l => l.msg.includes('req-123'))).to.be.true;
    });

    it('handles missing request ID gracefully', async () => {
      const result = await handler({
        data: { title: 'gpt-4', messages: [{ role: 'user', content: 'hi' }] }
      });
      expect(result).to.have.property('content');
    });
  });
});
