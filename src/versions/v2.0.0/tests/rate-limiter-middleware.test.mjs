#!/usr/bin/env node

import { describe, it, before, after } from 'mocha';
import { expect } from 'chai';
import { createRateLimiter } from '../lib/rate-limiter.mjs';
import { createRateLimiterMiddleware } from '../lib/rate-limiter-middleware.mjs';

describe('RateLimiterMiddleware', () => {
  let limiter;
  let middleware;

  before(() => {
    limiter = createRateLimiter();
    middleware = createRateLimiterMiddleware(limiter);
  });

  after(() => {
    if (limiter) limiter.dispose();
  });

  describe('Middleware Integration', () => {
    it('should create middleware successfully', () => {
      expect(middleware).to.be.a('function');
      expect(middleware.name).to.equal('rateLimiterMiddleware');
    });

    it('should attach rate limiter reference', () => {
      expect(middleware.rateLimiter).to.equal(limiter);
    });

    it('should have correct name property', () => {
      expect(middleware.name).to.equal('rateLimiterMiddleware');
    });

    it('should handle null message gracefully', async () => {
      const nextCalled = [];
      const next = async (msg) => {
        nextCalled.push(true);
        return { success: true };
      };

      const result = await middleware(null, next);
      expect(nextCalled.length).to.equal(1);
    });

    it('should pass through valid message to next', async () => {
      const nextCalled = [];
      const next = async (msg) => {
        nextCalled.push(msg);
        return { success: true };
      };

      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };
      const result = await middleware(message, next);

      expect(nextCalled.length).to.equal(1);
      expect(nextCalled[0]).to.equal(message);
    });
  });

  describe('Rate Limiting Enforcement', () => {
    it('should reject when rate exceeded', async () => {
      const l = createRateLimiter();
      const m = createRateLimiterMiddleware(l);
      const bucket = l.buckets.get('bridge:complete');
      bucket.tokens = 0;

      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };
      const result = await m(message, next);

      expect(result.data.success).to.be.false;
      expect(result.data.error.code).to.equal(-32603);
      l.dispose();
    });

    it('should return JSON-RPC error format', async () => {
      const l = createRateLimiter();
      const m = createRateLimiterMiddleware(l);
      const bucket = l.buckets.get('bridge:complete');
      bucket.tokens = 0;

      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };
      const result = await m(message, next);

      expect(result.messageId).to.equal('msg-1');
      expect(result.messageType).to.equal('error');
      expect(result.data.error.message).to.include('Rate limit exceeded');
      l.dispose();
    });

    it('should include error details', async () => {
      const l = createRateLimiter();
      const m = createRateLimiterMiddleware(l, { includeDetailsInError: true });
      const bucket = l.buckets.get('bridge:complete');
      bucket.tokens = 0;

      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };
      const result = await m(message, next);

      expect(result.data.error.data).to.exist;
      expect(result.data.error.data.handler).to.equal('bridge:complete');
      expect(result.data.error.data.refillsInMs).to.exist;
      l.dispose();
    });

    it('should omit details when disabled', async () => {
      const l = createRateLimiter();
      const m = createRateLimiterMiddleware(l, { includeDetailsInError: false });
      const bucket = l.buckets.get('bridge:complete');
      bucket.tokens = 0;

      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };
      const result = await m(message, next);

      expect(result.data.error.data).to.not.exist;
      l.dispose();
    });

    it('should allow request when rate limit OK', async () => {
      const l = createRateLimiter();
      const m = createRateLimiterMiddleware(l);
      let nextCalled = false;

      const next = async (msg) => {
        nextCalled = true;
        return { success: true, data: 'response' };
      };

      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };
      const result = await m(message, next);

      expect(nextCalled).to.be.true;
      expect(result.data).to.equal('response');
      l.dispose();
    });
  });

  describe('Metrics Recording', () => {
    it('should record allowed requests', async () => {
      const l = createRateLimiter();
      const mockMetrics = {
        recordAllowed: (type) => { mockMetrics.allowedCount = (mockMetrics.allowedCount || 0) + 1; }
      };
      l.metrics = mockMetrics;

      const m = createRateLimiterMiddleware(l, { recordMetrics: true });
      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      await m(message, next);
      expect(mockMetrics.allowedCount).to.equal(1);
      l.dispose();
    });

    it('should record rejected requests', async () => {
      const l = createRateLimiter();
      const mockMetrics = {
        recordRejected: (type) => { mockMetrics.rejectedCount = (mockMetrics.rejectedCount || 0) + 1; }
      };
      l.metrics = mockMetrics;
      const bucket = l.buckets.get('bridge:complete');
      bucket.tokens = 0;

      const m = createRateLimiterMiddleware(l, { recordMetrics: true });
      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      await m(message, next);
      expect(mockMetrics.rejectedCount).to.equal(1);
      l.dispose();
    });

    it('should skip metrics if disabled', async () => {
      const l = createRateLimiter();
      const mockMetrics = {
        recordAllowed: (type) => { mockMetrics.count = (mockMetrics.count || 0) + 1; }
      };
      l.metrics = mockMetrics;

      const m = createRateLimiterMiddleware(l, { recordMetrics: false });
      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      await m(message, next);
      expect(mockMetrics.count || 0).to.equal(0);
      l.dispose();
    });
  });

  describe('Error Handling', () => {
    it('should catch errors from next middleware', async () => {
      const l = createRateLimiter();
      const m = createRateLimiterMiddleware(l);

      const next = async (msg) => {
        throw new Error('Next middleware failed');
      };

      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      try {
        await m(message, next);
        expect.fail('Should have thrown');
      } catch (error) {
        expect(error.message).to.equal('Next middleware failed');
      }
      l.dispose();
    });

    it('should record error metrics', async () => {
      const l = createRateLimiter();
      const mockMetrics = {
        recordError: (type) => { mockMetrics.errorCount = (mockMetrics.errorCount || 0) + 1; }
      };
      l.metrics = mockMetrics;

      const m = createRateLimiterMiddleware(l, { recordMetrics: true });
      const next = async (msg) => {
        throw new Error('Test error');
      };

      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      try {
        await m(message, next);
      } catch (error) {
        // Expected
      }

      expect(mockMetrics.errorCount).to.equal(1);
      l.dispose();
    });
  });

  describe('Logger Integration', () => {
    it('should log rejection when logger available', async () => {
      const l = createRateLimiter();
      const logged = [];
      l.logger = {
        warn: (msg) => { logged.push(msg); }
      };
      const bucket = l.buckets.get('bridge:complete');
      bucket.tokens = 0;

      const m = createRateLimiterMiddleware(l);
      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      await m(message, next);
      expect(logged.length).to.be.greaterThan(0);
      l.dispose();
    });

    it('should log allowed request when logger available', async () => {
      const l = createRateLimiter();
      const logged = [];
      l.logger = {
        debug: (msg) => { logged.push(msg); }
      };

      const m = createRateLimiterMiddleware(l);
      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      await m(message, next);
      expect(logged.length).to.be.greaterThan(0);
      l.dispose();
    });

    it('should handle null logger gracefully', async () => {
      const l = createRateLimiter();
      l.logger = null;

      const m = createRateLimiterMiddleware(l);
      const next = async (msg) => ({ success: true });
      const message = { messageType: 'bridge:complete', messageId: 'msg-1' };

      const result = await m(message, next);
      expect(result.data).to.equal('success');
      l.dispose();
    });
  });
});
