# Rate Limiter Guide (Step 107)

## Overview

The Rate Limiter implements a **token bucket algorithm** to throttle RPC request throughput, prevent bridge overload, and ensure fair resource distribution across handlers. It provides per-handler rate policies and a global ceiling to maintain stability under load.

## Architecture

### Token Bucket Algorithm

Tokens are generated at a fixed rate per handler (e.g., 100 tokens/second for completion handler). Requests consume 1 token. When tokens are exhausted, requests are rejected with a rate-limit error.

```
Time:  0ms    100ms   200ms   300ms
       |-------|-------|-------|
Refill:   +10    +10     +10     +10 tokens (per second rate = 100, interval = 100ms)
```

**Characteristics**:
- **Deterministic**: Refill timing is predictable (±5% variance)
- **Burst-tolerant**: Allows temporary spikes via burst capacity (capacity = rate * multiplier)
- **Fair**: Per-handler isolation prevents one handler from starving others
- **Global ceiling**: Bridge-wide max throughput (default 500 RPC/s)

### Policy Model

```javascript
{
  globalCeilingPerSecond: 500,        // Bridge-wide max throughput
  handlerPolicies: {
    'bridge:complete': { 
      tokensPerSecond: 100,           // Rate: 100 requests/second
      burst: 5                        // Burst: allow up to 5 simultaneous
    },
    'bridge:analyze': { 
      tokensPerSecond: 50,            // Rate: 50 requests/second  
      burst: 3
    },
    'bridge:refactor': { 
      tokensPerSecond: 10,            // Rate: 10 requests/second
      burst: 2
    }
  },
  defaultTokensPerSecond: 20,         // Fallback for unregistered handlers
  defaultBurstMultiplier: 2,          // burst = rate * 2
  refillIntervalMs: 100               // Check refill every 100ms
}
```

## Usage

### Basic Instantiation

```javascript
import { createRateLimiter, createDefaultPolicy } from './lib/rate-limiter.mjs';

// Create with default policy
const limiter = createRateLimiter();

// Or with custom policy
const policy = {
  globalCeilingPerSecond: 1000,
  handlerPolicies: new Map([
    ['bridge:complete', { tokensPerSecond: 200, burst: 10 }],
  ]),
  defaultTokensPerSecond: 50,
  refillIntervalMs: 100,
};
const limiter = createRateLimiter(policy);
```

### Checking & Consuming Tokens

```javascript
// Check without consuming
if (limiter.canAcceptRequest('bridge:complete', 1)) {
  // Proceed
}

// Consume and get result
const result = limiter.consumeTokens('bridge:complete', 1);
if (result.allowed) {
  console.log(`Request accepted, ${result.tokens} tokens remaining`);
} else {
  console.log(`Rate limit exceeded, available at: ${result.availableAt}`);
  console.log(`Error details:`, result.error.details);
}
```

### Middleware Integration

```javascript
import { createRateLimiterMiddleware } from './lib/rate-limiter-middleware.mjs';

const limiter = createRateLimiter();
const middleware = createRateLimiterMiddleware(limiter, {
  includeDetailsInError: true,
  recordMetrics: true,
});

middlewareChain.use(middleware);
```

### Metrics

```javascript
const metrics = limiter.getMetrics();
console.log(metrics);
// {
//   totalRequests: 1000,
//   allowed: 950,
//   rejected: 50,
//   allowedRate: "95.00",
//   rejectedRate: "5.00",
//   averageTokens: "4.23",
//   p99Tokens: "0.15",
//   globalTokensAvailable: "450.50",
//   globalCapacity: 500,
//   handlerBuckets: [...]
// }
```

## Integration Points

### Step 47: MiddlewareChain
The rate limiter middleware hooks into the MiddlewareChain as a pre-dispatch filter:
```
incoming message → RateLimiterMiddleware → [MessageLoggingMiddleware → ValidationMiddleware → ...] → handler
```

### Step 71: HandlerRegistry
Per-handler policies are registered at startup:
```javascript
const registry = createHandlerRegistry();
const rateLimiter = createRateLimiter(registry.getRateLimitPolicies());
registry.setRateLimiter(rateLimiter);
```

### Step 72–74: Middleware Layers
The rate limiter works alongside:
- **MessageLoggingMiddleware**: Logs requests before/after rate limiting
- **RequestValidationMiddleware**: Validates schema after rate check passes
- **ErrorRecoveryMiddleware**: Handles rate limit errors gracefully

### Step 98: Performance Tests
Baseline throughput measurements with rate limiter enabled:
```
Scenario: 1000 requests, 100ms distribution
Expected: 500 allowed (global ceiling), ~500 rejected
```

### Step 99: Stress Tests
Multi-handler load testing:
```
Scenario 1: Concurrent handlers at their rate limits
- completion: 100/s → should be allowed
- analysis: 50/s → should be allowed
- refactor: 10/s → should be allowed
- Total: 160/s, but global ceiling = 500/s ✓ All pass

Scenario 2: Burst recovery
- completion handler: 5 burst tokens consumed
- Wait 100ms → should recover 1 token
- Remaining burst tokens should match expected refill
```

## Error Response Format

When rate limit exceeded, JSON-RPC error -32603 is returned:

```json
{
  "messageId": "msg-123",
  "messageType": "error",
  "data": {
    "success": false,
    "error": {
      "code": -32603,
      "message": "Rate limit exceeded for handler: bridge:complete",
      "data": {
        "handler": "bridge:complete",
        "currentTokens": 0,
        "requiredTokens": 1,
        "refillsInMs": 105,
        "availableAt": "2024-01-20T10:30:45.105Z",
        "globalCeiling": 500
      }
    }
  }
}
```

## Performance Characteristics

| Operation | Target | Notes |
|-----------|--------|-------|
| `canAcceptRequest()` | <1ms per call | O(1) lookup, no allocation |
| `consumeTokens()` | <1ms per call | O(1) token deduction + metrics |
| `getMetrics()` | <1ms | p99 calculation amortized |
| Refill loop | 100ms intervals | Background task, non-blocking |
| Memory per handler | ~500 bytes | TokenBucket instance |
| Total overhead | <5MB for 1000 handlers | Negligible at bridge scale |

## Configuration Tuning

### High-Throughput Scenarios
```javascript
const policy = {
  globalCeilingPerSecond: 10000,
  handlerPolicies: new Map([
    ['bridge:complete', { tokensPerSecond: 5000, burst: 50 }],
  ]),
  defaultTokensPerSecond: 1000,
  defaultBurstMultiplier: 3,
};
```

### Low-Latency Scenarios
```javascript
const policy = {
  globalCeilingPerSecond: 100,
  handlerPolicies: new Map([
    ['bridge:complete', { tokensPerSecond: 50, burst: 2 }],
  ]),
  defaultTokensPerSecond: 10,
  defaultBurstMultiplier: 1,
  refillIntervalMs: 10,  // More frequent refill for tighter control
};
```

### Testing / Development
```javascript
const policy = {
  globalCeilingPerSecond: 100000,  // Essentially unlimited
  defaultTokensPerSecond: 100000,
  defaultBurstMultiplier: 100,
};
```

## Troubleshooting

### Requests Being Rejected
**Symptom**: High rejection rate even with light load

**Diagnosis**:
1. Check policy configuration: `limiter.policy.globalCeilingPerSecond`
2. Verify handler rate: `limiter.getMetrics().handlerBuckets`
3. Check if burst capacity is too small: `burst < 5` for most handlers

**Solution**:
- Increase `tokensPerSecond` for the handler
- Increase `burst` capacity (burst = rate * 2 recommended)
- Increase global ceiling if bottleneck is bridge-wide

### High Token Accumulation
**Symptom**: Handler has many tokens but requests still throttled

**Diagnosis**: Global ceiling is exhausted while handler bucket has tokens

**Solution**:
- Increase `globalCeilingPerSecond`
- Check other handlers aren't consuming all global tokens
- Monitor `globalTokensAvailable` in metrics

### Burst Not Recovering
**Symptom**: After burst consumed, tokens don't refill quickly

**Diagnosis**: Refill interval too large or rate too low

**Solution**:
- Decrease `refillIntervalMs` (default 100ms)
- Increase `tokensPerSecond` for handler
- Increase `burst` multiplier

## API Reference

### RateLimiter

```javascript
class RateLimiter {
  // Check without consuming
  canAcceptRequest(messageType, tokens = 1) → boolean

  // Consume tokens
  consumeTokens(messageType, amount = 1) → {
    allowed: boolean,
    tokens: number,
    availableAt?: string,
    error?: ResourceExhaustedError
  }

  // Get metrics snapshot
  getMetrics() → {
    totalRequests: number,
    allowed: number,
    rejected: number,
    allowedRate: string,
    rejectedRate: string,
    averageTokens: string,
    p99Tokens: string,
    globalTokensAvailable: string,
    globalCapacity: number,
    handlerBuckets: Array
  }

  // Reset specific handler bucket
  resetBucket(messageType) → void

  // Reset all buckets
  resetAllBuckets() → void

  // Cleanup resources
  dispose() → void
}
```

### Middleware

```javascript
function createRateLimiterMiddleware(rateLimiter, options = {}) → Function
  // Returns middleware hook: (message, next) → Promise<result>

  // Options:
  // - includeDetailsInError: boolean (default true)
  // - recordMetrics: boolean (default true)
```

### Error Classes

```javascript
class RateLimiterError extends Error
class ResourceExhaustedError extends RateLimiterError
  // details: { handler, currentTokens, requiredTokens, refillsInMs, availableAt }
```

## Related Steps

- **Step 47**: MiddlewareChain (middleware framework)
- **Step 64**: TimeoutManager (complements timeout enforcement)
- **Step 71**: HandlerRegistry (per-handler policy registration)
- **Step 72–74**: Middleware layers (logging, validation, recovery)
- **Step 98**: Performance tests (baseline with rate limiter)
- **Step 99**: Stress tests (multi-handler load scenarios)
- **Step 108**: CircuitBreaker (complements rate limiter)
- **Step 109**: MetricsAggregator (consumes rate limiter metrics)
