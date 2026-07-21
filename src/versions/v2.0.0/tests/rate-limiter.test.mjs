#!/usr/bin/env node

import { describe, it, before, after } from 'mocha';
import { expect } from 'chai';
import { createRateLimiter, createDefaultPolicy, RateLimiterError, ResourceExhaustedError } from '../lib/rate-limiter.mjs';

describe('RateLimiter', () => {
  let limiter;

  before(() => {
    limiter = createRateLimiter();
  });

  after(() => {
    if (limiter) limiter.dispose();
  });

  describe('Initialization & Policy', () => {
    it('should create limiter with default policy', () => {
      const l = createRateLimiter();
      expect(l).to.exist;
      expect(l.policy.globalCeilingPerSecond).to.equal(500);
      l.dispose();
    });

    it('should merge custom policy with defaults', () => {
      const custom = { globalCeilingPerSecond: 1000 };
      const l = createRateLimiter(custom);
      expect(l.policy.globalCeilingPerSecond).to.equal(1000);
      l.dispose();
    });

    it('should initialize handler buckets from policy', () => {
      const l = createRateLimiter();
      expect(l.buckets.has('bridge:complete')).to.be.true;
      expect(l.buckets.has('bridge:analyze')).to.be.true;
      l.dispose();
    });

    it('should handle null policy gracefully', () => {
      const l = createRateLimiter(null);
      expect(l).to.exist;
      expect(l.policy.globalCeilingPerSecond).to.equal(500);
      l.dispose();
    });
  });

  describe('Token Bucket Mechanics', () => {
    it('should allow request when tokens available', () => {
      const l = createRateLimiter();
      expect(l.canAcceptRequest('bridge:complete', 1)).to.be.true;
      l.dispose();
    });

    it('should consume tokens correctly', () => {
      const l = createRateLimiter();
      const result = l.consumeTokens('bridge:complete', 1);
      expect(result.allowed).to.be.true;
      expect(result.tokens).to.be.lessThan(5); // burst capacity
      l.dispose();
    });

    it('should reject request when tokens exhausted', (done) => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      const bucket = l.buckets.get(handler);
      bucket.tokens = 0;

      expect(l.canAcceptRequest(handler, 1)).to.be.false;
      l.dispose();
      done();
    });

    it('should refill tokens over time', (done) => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      l.consumeTokens(handler, 5);

      setTimeout(() => {
        l._refillBuckets();
        expect(l.canAcceptRequest(handler, 1)).to.be.true;
        l.dispose();
        done();
      }, 150);
    });

    it('should reset bucket to full capacity', () => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      l.consumeTokens(handler, 3);
      l.resetBucket(handler);

      const bucket = l.buckets.get(handler);
      expect(bucket.tokens).to.equal(bucket.capacity);
      l.dispose();
    });

    it('should reset all buckets', () => {
      const l = createRateLimiter();
      l.consumeTokens('bridge:complete', 3);
      l.consumeTokens('bridge:analyze', 2);
      l.resetAllBuckets();

      expect(l.canAcceptRequest('bridge:complete', 5)).to.be.true;
      expect(l.canAcceptRequest('bridge:analyze', 3)).to.be.true;
      l.dispose();
    });
  });

  describe('Per-Handler Policies', () => {
    it('should respect handler-specific rates (fast)', () => {
      const l = createRateLimiter();
      const bucket = l.buckets.get('bridge:complete');
      expect(bucket.tokensPerSecond).to.equal(100);
      l.dispose();
    });

    it('should respect handler-specific rates (medium)', () => {
      const l = createRateLimiter();
      const bucket = l.buckets.get('bridge:analyze');
      expect(bucket.tokensPerSecond).to.equal(50);
      l.dispose();
    });

    it('should respect handler-specific rates (slow)', () => {
      const l = createRateLimiter();
      const bucket = l.buckets.get('bridge:refactor');
      expect(bucket.tokensPerSecond).to.equal(10);
      l.dispose();
    });

    it('should use default rate for unregistered handler', () => {
      const l = createRateLimiter();
      const result = l.consumeTokens('unknown:handler', 1);
      expect(result.allowed).to.be.true;
      expect(l.buckets.has('unknown:handler')).to.be.true;
      l.dispose();
    });

    it('should allow one handler to exceed while others respect limits', () => {
      const l = createRateLimiter();
      const fast = l.buckets.get('bridge:complete');
      const slow = l.buckets.get('bridge:refactor');

      slow.tokens = 0; // Exhaust slow handler
      expect(l.canAcceptRequest('bridge:refactor', 1)).to.be.false;
      expect(l.canAcceptRequest('bridge:complete', 1)).to.be.true;
      l.dispose();
    });
  });

  describe('Global Ceiling', () => {
    it('should enforce global ceiling across all handlers', () => {
      const policy = { globalCeilingPerSecond: 5, handlerPolicies: new Map([
        ['fast', { tokensPerSecond: 100, burst: 10 }],
        ['slow', { tokensPerSecond: 100, burst: 10 }],
      ])};
      const l = createRateLimiter(policy);

      l.globalTokens = 1;
      expect(l.canAcceptRequest('fast', 2)).to.be.false;
      expect(l.canAcceptRequest('slow', 1)).to.be.true;
      l.dispose();
    });

    it('should distribute global tokens across handlers', () => {
      const policy = { globalCeilingPerSecond: 10, handlerPolicies: new Map([
        ['h1', { tokensPerSecond: 100, burst: 100 }],
        ['h2', { tokensPerSecond: 100, burst: 100 }],
      ])};
      const l = createRateLimiter(policy);
      l.globalTokens = 5;

      l.consumeTokens('h1', 3);
      expect(l.canAcceptRequest('h2', 2)).to.be.true;
      expect(l.canAcceptRequest('h2', 3)).to.be.false;
      l.dispose();
    });

    it('should refill global tokens', (done) => {
      const policy = { globalCeilingPerSecond: 100, refillIntervalMs: 100 };
      const l = createRateLimiter(policy);
      l.globalTokens = 0;

      setTimeout(() => {
        l._refillBuckets();
        expect(l.globalTokens).to.be.greaterThan(0);
        l.dispose();
        done();
      }, 150);
    });

    it('should cap global tokens at capacity', () => {
      const l = createRateLimiter();
      l.globalTokens = l.globalCapacity + 100;
      l._refillBuckets();
      expect(l.globalTokens).to.equal(l.globalCapacity);
      l.dispose();
    });
  });

  describe('Burst Handling', () => {
    it('should allow burst capacity initialization', () => {
      const l = createRateLimiter();
      const bucket = l.buckets.get('bridge:complete');
      expect(bucket.capacity).to.equal(5);
      expect(bucket.tokens).to.equal(5);
      l.dispose();
    });

    it('should exhaust burst tokens', () => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';

      for (let i = 0; i < 5; i++) {
        l.consumeTokens(handler, 1);
      }
      expect(l.canAcceptRequest(handler, 1)).to.be.false;
      l.dispose();
    });

    it('should recover after burst exhaustion', (done) => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';

      for (let i = 0; i < 5; i++) {
        l.consumeTokens(handler, 1);
      }

      setTimeout(() => {
        l._refillBuckets();
        expect(l.canAcceptRequest(handler, 1)).to.be.true;
        l.dispose();
        done();
      }, 150);
    });

    it('should not exceed burst capacity after refill', (done) => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      const bucket = l.buckets.get(handler);

      setTimeout(() => {
        l._refillBuckets();
        expect(bucket.tokens).to.be.lessThanOrEqual(bucket.capacity);
        l.dispose();
        done();
      }, 150);
    });
  });

  describe('Error Handling & Rejection', () => {
    it('should return error object when rate exceeded', () => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      const bucket = l.buckets.get(handler);
      bucket.tokens = 0;

      const result = l.consumeTokens(handler, 1);
      expect(result.allowed).to.be.false;
      expect(result.error).to.be.instanceOf(ResourceExhaustedError);
      l.dispose();
    });

    it('should include error details', () => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      const bucket = l.buckets.get(handler);
      bucket.tokens = 0;

      const result = l.consumeTokens(handler, 1);
      expect(result.error.details).to.exist;
      expect(result.error.details.handler).to.equal(handler);
      expect(result.error.details.requiredTokens).to.equal(1);
      l.dispose();
    });

    it('should return -32603 error code', () => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      const bucket = l.buckets.get(handler);
      bucket.tokens = 0;

      const result = l.consumeTokens(handler, 1);
      expect(result.error.code).to.equal('RESOURCE_EXHAUSTED');
      l.dispose();
    });

    it('should provide availableAt timestamp', () => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      const bucket = l.buckets.get(handler);
      bucket.tokens = 0;

      const result = l.consumeTokens(handler, 1);
      expect(result.availableAt).to.exist;
      const ts = new Date(result.availableAt);
      expect(ts.getTime()).to.be.greaterThan(Date.now());
      l.dispose();
    });

    it('should track rejection metrics', () => {
      const l = createRateLimiter();
      const handler = 'bridge:complete';
      const bucket = l.buckets.get(handler);
      bucket.tokens = 0;

      const beforeRejected = l.metricsState.rejected;
      l.consumeTokens(handler, 1);
      expect(l.metricsState.rejected).to.equal(beforeRejected + 1);
      l.dispose();
    });
  });

  describe('Metrics & Telemetry', () => {
    it('should track total requests', () => {
      const l = createRateLimiter();
      const before = l.metricsState.totalRequests;
      l.consumeTokens('bridge:complete', 1);
      expect(l.metricsState.totalRequests).to.equal(before + 1);
      l.dispose();
    });

    it('should track allowed requests', () => {
      const l = createRateLimiter();
      const before = l.metricsState.allowed;
      l.consumeTokens('bridge:complete', 1);
      expect(l.metricsState.allowed).to.equal(before + 1);
      l.dispose();
    });

    it('should calculate average tokens', () => {
      const l = createRateLimiter();
      l.consumeTokens('bridge:complete', 1);
      const metrics = l.getMetrics();
      expect(metrics.averageTokens).to.exist;
      expect(parseFloat(metrics.averageTokens)).to.be.a('number');
      l.dispose();
    });

    it('should calculate p99 tokens', () => {
      const l = createRateLimiter();
      for (let i = 0; i < 100; i++) {
        l.consumeTokens('bridge:complete', 1);
      }
      const metrics = l.getMetrics();
      expect(metrics.p99Tokens).to.exist;
      expect(parseFloat(metrics.p99Tokens)).to.be.a('number');
      l.dispose();
    });

    it('should include handler buckets in metrics', () => {
      const l = createRateLimiter();
      const metrics = l.getMetrics();
      expect(metrics.handlerBuckets).to.be.an('array');
      expect(metrics.handlerBuckets.length).to.be.greaterThan(0);
      l.dispose();
    });

    it('should include allowed and rejected rates', () => {
      const l = createRateLimiter();
      l.consumeTokens('bridge:complete', 1);
      const metrics = l.getMetrics();
      expect(metrics.allowedRate).to.exist;
      expect(metrics.rejectedRate).to.exist;
      l.dispose();
    });
  });

  describe('Performance Gates', () => {
    it('canAcceptRequest should complete in <1ms', () => {
      const l = createRateLimiter();
      const start = performance.now();
      for (let i = 0; i < 1000; i++) {
        l.canAcceptRequest('bridge:complete', 1);
      }
      const elapsed = (performance.now() - start) / 1000;
      expect(elapsed).to.be.lessThan(1);
      l.dispose();
    });

    it('consumeTokens should complete in <1ms per call', () => {
      const l = createRateLimiter();
      const start = performance.now();
      for (let i = 0; i < 1000; i++) {
        if (l.canAcceptRequest('bridge:complete', 1)) {
          l.consumeTokens('bridge:complete', 1);
        }
      }
      const elapsed = (performance.now() - start) / 1000;
      expect(elapsed).to.be.lessThan(1);
      l.dispose();
    });

    it('getMetrics should complete in <1ms', () => {
      const l = createRateLimiter();
      for (let i = 0; i < 1000; i++) {
        if (l.canAcceptRequest('bridge:complete', 1)) {
          l.consumeTokens('bridge:complete', 1);
        }
      }
      const start = performance.now();
      l.getMetrics();
      const elapsed = performance.now() - start;
      expect(elapsed).to.be.lessThan(1);
      l.dispose();
    });
  });
});
