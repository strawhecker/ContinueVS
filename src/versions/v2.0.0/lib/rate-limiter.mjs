#!/usr/bin/env node

/**
 * Rate Limiter for RPC Calls (Step 107)
 *
 * Implements a token bucket algorithm to throttle RPC request throughput,
 * prevent bridge overload, and ensure fair resource distribution across handlers.
 *
 * Responsibilities:
 * 1. **Token Bucket Management**: Track tokens per handler type with configurable rates
 * 2. **Per-Handler Policies**: Different rates for fast vs. slow handlers (completion: 100/s, analysis: 10/s)
 * 3. **Global Ceiling**: Bridge-wide max throughput (e.g., 500 RPC/s) to prevent cascading failure
 * 4. **Burst Allowance**: Allow temporary token overflow for spike handling
 * 5. **Metrics Collection**: Track allowed/rejected/queued requests, token availability
 * 6. **Graceful Degradation**: Optional logger/metrics injection (no-op if null)
 *
 * Architecture:
 * ```
 * RateLimiterPolicy (config)
 *   ├─ globalCeilingPerSecond: 500 (bridge-wide max)
 *   ├─ handlerPolicies: Map<messageType, {tokensPerSecond, burst}>
 *   ├─ defaultTokensPerSecond: 20 (fallback for unregistered)
 *   ├─ defaultBurstMultiplier: 2 (burst = rate * 2)
 *   └─ refillIntervalMs: 100 (check refill every 100ms)
 *
 * RateLimiter (instance)
 *   ├─ canAcceptRequest(messageType, tokens?) → boolean
 *   ├─ consumeTokens(messageType, amount) → { allowed: boolean, tokens, availableAt? }
 *   ├─ getMetrics() → {totalRequests, allowed, rejected, queued, averageTokens, p99Tokens, tokensPerSecond}
 *   ├─ resetBucket(messageType) → void
 *   ├─ dispose() → void (stop refill loop)
 *   └─ (private) _refillBuckets() → void (background task)
 *
 * Token Bucket Flow:
 *   1. canAcceptRequest(type, tokens=1) checks if tokens available
 *   2. If yes → return true (caller will consumeTokens)
 *   3. If no → return false + recordMetric(rejected)
 *   4. consumeTokens(type, amount) deducts from bucket
 *   5. Every 100ms: refill = min(bucket + rate/10, capacity)
 * ```
 *
 * @module src/versions/v2.0.0/lib/rate-limiter.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 47: MiddlewareChain (pre-dispatch throttle hook)
 *   - Step 64: TimeoutManager (complements timeout enforcement)
 *   - Step 71: HandlerRegistry (per-handler policy registration)
 *   - Step 72–74: Middleware (logging, validation, error recovery)
 *   - Step 98: Performance tests (throughput baselines)
 *   - Step 99: Stress tests (load testing with rate limits)
 */

// ===== ERROR CLASSES =====

/**
 * Base error for rate limiter operations
 */
export class RateLimiterError extends Error {
  constructor(message, code = 'RATE_LIMITER_ERROR', details = {}) {
    super(message);
    this.name = 'RateLimiterError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Thrown when a request exceeds rate limit
 */
export class ResourceExhaustedError extends RateLimiterError {
  constructor(message, details = {}) {
    super(message, 'RESOURCE_EXHAUSTED', details);
    this.name = 'ResourceExhaustedError';
  }
}

// ===== RATE LIMITER POLICY MODEL =====

/**
 * Configuration model for rate limiter policies
 * @typedef {Object} RateLimiterPolicy
 * @property {number} globalCeilingPerSecond - Bridge-wide max throughput (e.g., 500)
 * @property {Map<string, {tokensPerSecond: number, burst: number}>} handlerPolicies - Per-handler rates
 * @property {number} defaultTokensPerSecond - Fallback rate for unregistered handlers (default 20)
 * @property {number} defaultBurstMultiplier - Burst allowance multiplier (default 2, so burst = rate * 2)
 * @property {number} refillIntervalMs - Token refill check interval in ms (default 100)
 */

/**
 * Create default RateLimiterPolicy
 * @returns {RateLimiterPolicy}
 */
export function createDefaultPolicy() {
  return {
    globalCeilingPerSecond: 500,
    handlerPolicies: new Map([
      ['bridge:complete', { tokensPerSecond: 100, burst: 5 }],     // Fast (completion)
      ['bridge:analyze', { tokensPerSecond: 50, burst: 3 }],       // Medium (analysis)
      ['bridge:refactor', { tokensPerSecond: 10, burst: 2 }],      // Slow (refactor)
      ['bridge:getEditorState', { tokensPerSecond: 100, burst: 5 }], // Fast (state)
      ['bridge:search', { tokensPerSecond: 50, burst: 3 }],        // Medium (search)
    ]),
    defaultTokensPerSecond: 20,
    defaultBurstMultiplier: 2,
    refillIntervalMs: 100,
  };
}

// ===== BUCKET STATE =====

/**
 * Internal state for a single handler's token bucket
 * @internal
 */
class TokenBucket {
  constructor(tokensPerSecond, burst, refillIntervalMs) {
    this.tokensPerSecond = tokensPerSecond;
    this.capacity = burst; // Max tokens in bucket
    this.tokens = burst;   // Start with full burst
    this.refillIntervalMs = refillIntervalMs;
    this.lastRefillAt = Date.now();
    this.refillPerInterval = (tokensPerSecond / 1000) * refillIntervalMs;
  }

  canConsume(amount = 1) {
    return this.tokens >= amount;
  }

  consume(amount = 1) {
    if (!this.canConsume(amount)) {
      return false;
    }
    this.tokens -= amount;
    return true;
  }

  refillIfNeeded() {
    const now = Date.now();
    const timeSinceLastRefill = now - this.lastRefillAt;

    if (timeSinceLastRefill >= this.refillIntervalMs) {
      const intervalsElapsed = Math.floor(timeSinceLastRefill / this.refillIntervalMs);
      const tokensToAdd = intervalsElapsed * this.refillPerInterval;
      this.tokens = Math.min(this.tokens + tokensToAdd, this.capacity);
      this.lastRefillAt = now;
    }
  }

  getTokensUntilRefill() {
    const now = Date.now();
    const timeSinceLastRefill = now - this.lastRefillAt;
    const msUntilNextRefill = this.refillIntervalMs - timeSinceLastRefill;
    return Math.max(0, Math.ceil(msUntilNextRefill));
  }

  reset() {
    this.tokens = this.capacity;
    this.lastRefillAt = Date.now();
  }
}

// ===== RATE LIMITER CLASS =====

/**
 * Token bucket rate limiter for RPC calls
 */
export class RateLimiter {
  /**
   * Create a new rate limiter
   * @param {RateLimiterPolicy} policy - Configuration policy
   * @param {Object} logger - Optional logger (null-safe)
   * @param {Object} metrics - Optional metrics collector (null-safe)
   */
  constructor(policy, logger = null, metrics = null) {
    this.policy = policy || createDefaultPolicy();
    this.logger = logger;
    this.metrics = metrics;

    // Initialize handler buckets
    this.buckets = new Map();
    this._initializeBuckets();

    // Global ceiling tracking
    this.globalTokens = this.policy.globalCeilingPerSecond;
    this.globalCapacity = this.policy.globalCeilingPerSecond;
    this.globalLastRefillAt = Date.now();
    this.globalRefillPerInterval = (this.policy.globalCeilingPerSecond / 1000) * this.policy.refillIntervalMs;

    // Metrics state
    this.metricsState = {
      totalRequests: 0,
      allowed: 0,
      rejected: 0,
      queued: 0,
      tokenSnapshots: [], // For p99 calculation
    };

    // Refill loop
    this.refillIntervalId = null;
    this._startRefillLoop();
  }

  /**
   * Initialize token buckets for all configured handlers
   * @private
   */
  _initializeBuckets() {
    if (this.policy.handlerPolicies) {
      for (const [handlerType, config] of this.policy.handlerPolicies.entries()) {
        const burst = config.burst || (config.tokensPerSecond * this.policy.defaultBurstMultiplier);
        this.buckets.set(
          handlerType,
          new TokenBucket(config.tokensPerSecond, burst, this.policy.refillIntervalMs)
        );
      }
    }
  }

  /**
   * Get or create bucket for a handler type
   * @private
   */
  _getBucket(messageType) {
    let bucket = this.buckets.get(messageType);
    if (!bucket) {
      const burst = this.policy.defaultTokensPerSecond * this.policy.defaultBurstMultiplier;
      bucket = new TokenBucket(this.policy.defaultTokensPerSecond, burst, this.policy.refillIntervalMs);
      this.buckets.set(messageType, bucket);
    }
    return bucket;
  }

  /**
   * Start background refill loop
   * @private
   */
  _startRefillLoop() {
    this.refillIntervalId = setInterval(() => {
      this._refillBuckets();
    }, this.policy.refillIntervalMs);
  }

  /**
   * Refill all buckets on timer tick
   * @private
   */
  _refillBuckets() {
    // Refill individual handler buckets
    for (const bucket of this.buckets.values()) {
      bucket.refillIfNeeded();
    }

    // Refill global ceiling
    const now = Date.now();
    const timeSinceLastRefill = now - this.globalLastRefillAt;
    if (timeSinceLastRefill >= this.policy.refillIntervalMs) {
      const intervalsElapsed = Math.floor(timeSinceLastRefill / this.policy.refillIntervalMs);
      const tokensToAdd = intervalsElapsed * this.globalRefillPerInterval;
      this.globalTokens = Math.min(this.globalTokens + tokensToAdd, this.globalCapacity);
      this.globalLastRefillAt = now;
    }
  }

  /**
   * Check if a request can be accepted without consuming tokens
   * @param {string} messageType - Handler message type
   * @param {number} tokens - Tokens required (default 1)
   * @returns {boolean} true if request can be accepted
   */
  canAcceptRequest(messageType, tokens = 1) {
    const bucket = this._getBucket(messageType);
    bucket.refillIfNeeded();

    // Check both handler-specific and global limits
    const handlerHasTokens = bucket.canConsume(tokens);
    const globalHasTokens = this.globalTokens >= tokens;

    return handlerHasTokens && globalHasTokens;
  }

  /**
   * Consume tokens and return result
   * @param {string} messageType - Handler message type
   * @param {number} amount - Tokens to consume (default 1)
   * @returns {Object} { allowed: boolean, tokens: number, availableAt?: string }
   */
  consumeTokens(messageType, amount = 1) {
    this.metricsState.totalRequests++;

    const bucket = this._getBucket(messageType);
    bucket.refillIfNeeded();

    const handlerHasTokens = bucket.canConsume(amount);
    const globalHasTokens = this.globalTokens >= amount;

    if (handlerHasTokens && globalHasTokens) {
      bucket.consume(amount);
      this.globalTokens -= amount;
      this.metricsState.allowed++;
      this.metricsState.tokenSnapshots.push(bucket.tokens);
      return { allowed: true, tokens: bucket.tokens };
    }

    this.metricsState.rejected++;
    const refillMs = Math.max(
      bucket.getTokensUntilRefill(),
      Math.ceil((this.policy.refillIntervalMs * amount) / this.globalRefillPerInterval)
    );

    const error = new ResourceExhaustedError(
      `Rate limit exceeded for handler: ${messageType}`,
      {
        handler: messageType,
        currentTokens: Math.floor(bucket.tokens),
        requiredTokens: amount,
        refillsInMs: refillMs,
        availableAt: new Date(Date.now() + refillMs).toISOString(),
      }
    );

    if (this.logger) {
      this.logger.warn?.(`Rate limit rejected: ${messageType} (${refillMs}ms until refill)`);
    }

    return {
      allowed: false,
      tokens: bucket.tokens,
      availableAt: new Date(Date.now() + refillMs).toISOString(),
      error,
    };
  }

  /**
   * Get current metrics
   * @returns {Object} Metrics snapshot
   */
  getMetrics() {
    const p99 = this._calculatePercentile(this.metricsState.tokenSnapshots, 99);
    const average =
      this.metricsState.tokenSnapshots.length > 0
        ? this.metricsState.tokenSnapshots.reduce((a, b) => a + b, 0) / this.metricsState.tokenSnapshots.length
        : 0;

    return {
      totalRequests: this.metricsState.totalRequests,
      allowed: this.metricsState.allowed,
      rejected: this.metricsState.rejected,
      queued: this.metricsState.queued,
      allowedRate: this.metricsState.totalRequests > 0 
        ? (this.metricsState.allowed / this.metricsState.totalRequests * 100).toFixed(2) 
        : '0.00',
      rejectedRate: this.metricsState.totalRequests > 0 
        ? (this.metricsState.rejected / this.metricsState.totalRequests * 100).toFixed(2) 
        : '0.00',
      averageTokens: average.toFixed(2),
      p99Tokens: p99.toFixed(2),
      globalTokensAvailable: this.globalTokens.toFixed(2),
      globalCapacity: this.globalCapacity,
      handlerBuckets: Array.from(this.buckets.entries()).map(([type, bucket]) => ({
        handler: type,
        tokens: bucket.tokens.toFixed(2),
        capacity: bucket.capacity,
        tokensPerSecond: bucket.tokensPerSecond,
      })),
    };
  }

  /**
   * Reset a handler's token bucket to full capacity
   * @param {string} messageType - Handler message type
   */
  resetBucket(messageType) {
    const bucket = this._getBucket(messageType);
    bucket.reset();
    if (this.logger) {
      this.logger.debug?.(`Rate limiter bucket reset: ${messageType}`);
    }
  }

  /**
   * Reset all buckets to full capacity
   */
  resetAllBuckets() {
    for (const bucket of this.buckets.values()) {
      bucket.reset();
    }
    this.globalTokens = this.globalCapacity;
    this.globalLastRefillAt = Date.now();
    if (this.logger) {
      this.logger.debug?.('Rate limiter all buckets reset');
    }
  }

  /**
   * Dispose rate limiter (stop refill loop)
   */
  dispose() {
    if (this.refillIntervalId !== null) {
      clearInterval(this.refillIntervalId);
      this.refillIntervalId = null;
    }
  }

  /**
   * Calculate percentile from array of values
   * @private
   */
  _calculatePercentile(values, percentile) {
    if (values.length === 0) return 0;
    const sorted = [...values].sort((a, b) => a - b);
    const index = Math.ceil((percentile / 100) * sorted.length) - 1;
    return sorted[Math.max(0, index)];
  }
}

// ===== FACTORY FUNCTION =====

/**
 * Create a new rate limiter instance
 * @param {RateLimiterPolicy} policy - Configuration policy (uses default if not provided)
 * @param {Object} logger - Optional logger instance
 * @param {Object} metrics - Optional metrics collector instance
 * @returns {RateLimiter}
 */
export function createRateLimiter(policy = null, logger = null, metrics = null) {
  const finalPolicy = policy || createDefaultPolicy();
  return new RateLimiter(finalPolicy, logger, metrics);
}

export default createRateLimiter;
