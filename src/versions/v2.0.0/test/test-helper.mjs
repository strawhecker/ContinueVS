/**
 * test-helper.mjs
 * 
 * Async utilities, retry logic, polling, and assertion helpers for Node.js bridge tests.
 * Provides a consistent interface for testing async operations with timeouts and retries.
 * 
 * Usage:
 *   import { retryAsync, waitFor, assertEventFired } from './test-helper.mjs';
 *   
 *   await retryAsync(async () => {
 *     await transport.connect();
 *   }, { maxAttempts: 3, delayMs: 100 });
 * 
 *   await waitFor(() => transport.isConnected(), { timeoutMs: 5000 });
 */

/**
 * Retries an async operation with exponential backoff.
 * @param {Function} operation - Async function to retry
 * @param {Object} options - Configuration options
 * @param {number} [options.maxAttempts=3] - Maximum number of attempts
 * @param {number} [options.delayMs=100] - Initial delay in milliseconds
 * @param {number} [options.backoffMultiplier=2.0] - Exponential backoff multiplier
 * @returns {Promise} Resolves when operation succeeds
 * @throws {AggregateError} If all attempts fail
 */
export async function retryAsync(operation, options = {}) {
  const {
    maxAttempts = 3,
    delayMs = 100,
    backoffMultiplier = 2.0,
  } = options;

  if (typeof operation !== 'function') {
    throw new TypeError('operation must be a function');
  }
  if (maxAttempts < 1) {
    throw new RangeError('maxAttempts must be at least 1');
  }
  if (delayMs < 0) {
    throw new RangeError('delayMs must be non-negative');
  }

  const errors = [];
  let currentDelay = delayMs;

  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    try {
      return await operation();
    } catch (err) {
      errors.push(err);

      // Only delay if not on the last attempt
      if (attempt < maxAttempts - 1) {
        await delay(currentDelay);
        currentDelay = Math.floor(currentDelay * backoffMultiplier);
      }
    }
  }

  // All attempts failed
  const err = new AggregateError(
    errors,
    `Retry exhausted after ${maxAttempts} attempts`
  );
  throw err;
}

/**
 * Polls for a synchronous condition to become true.
 * @param {Function} condition - Predicate function returning boolean
 * @param {Object} options - Configuration options
 * @param {number} [options.timeoutMs=5000] - Maximum wait time in milliseconds
 * @param {number} [options.pollIntervalMs=50] - Interval between polls
 * @returns {Promise} Resolves when condition becomes true
 * @throws {TimeoutError} If condition not met within timeout
 */
export async function waitFor(condition, options = {}) {
  const {
    timeoutMs = 5000,
    pollIntervalMs = 50,
  } = options;

  if (typeof condition !== 'function') {
    throw new TypeError('condition must be a function');
  }
  if (timeoutMs < 0) {
    throw new RangeError('timeoutMs must be non-negative');
  }
  if (pollIntervalMs < 1) {
    throw new RangeError('pollIntervalMs must be at least 1');
  }

  const startTime = Date.now();

  while (Date.now() - startTime < timeoutMs) {
    if (condition()) {
      return; // Condition met
    }
    await delay(pollIntervalMs);
  }

  throw new Error(`Condition not met within ${timeoutMs}ms timeout`);
}

/**
 * Polls for an async condition to become true.
 * @param {Function} condition - Async predicate function returning Promise<boolean>
 * @param {Object} options - Configuration options
 * @param {number} [options.timeoutMs=5000] - Maximum wait time in milliseconds
 * @param {number} [options.pollIntervalMs=50] - Interval between polls
 * @returns {Promise} Resolves when condition becomes true
 * @throws {TimeoutError} If condition not met within timeout
 */
export async function waitForAsync(condition, options = {}) {
  const {
    timeoutMs = 5000,
    pollIntervalMs = 50,
  } = options;

  if (typeof condition !== 'function') {
    throw new TypeError('condition must be a function');
  }
  if (timeoutMs < 0) {
    throw new RangeError('timeoutMs must be non-negative');
  }
  if (pollIntervalMs < 1) {
    throw new RangeError('pollIntervalMs must be at least 1');
  }

  const startTime = Date.now();

  while (Date.now() - startTime < timeoutMs) {
    if (await condition()) {
      return; // Condition met
    }
    await delay(pollIntervalMs);
  }

  throw new Error(`Condition not met within ${timeoutMs}ms timeout`);
}

/**
 * Asserts that a promise completes within a timeout.
 * @param {Promise} promise - The promise to wait for
 * @param {Object} options - Configuration options
 * @param {number} [options.timeoutMs=5000] - Maximum wait time
 * @returns {Promise} Resolves with the promise result if completed within timeout
 * @throws {TimeoutError} If promise does not complete within timeout
 */
export async function assertCompletes(promise, options = {}) {
  const { timeoutMs = 5000 } = options;

  if (!promise || typeof promise.then !== 'function') {
    throw new TypeError('promise must be a Promise');
  }

  const timeoutPromise = new Promise((_, reject) =>
    setTimeout(
      () => reject(new Error(`Promise did not complete within ${timeoutMs}ms`)),
      timeoutMs
    )
  );

  return Promise.race([promise, timeoutPromise]);
}

/**
 * Asserts that a promise rejects with an error of the expected type.
 * @param {Promise} promise - The promise to wait for
 * @param {Function} expectedErrorType - Expected error constructor (e.g., TypeError, Error)
 * @param {Object} options - Configuration options
 * @param {number} [options.timeoutMs=5000] - Maximum wait time
 * @returns {Promise<Error>} Resolves with the thrown error if it matches the type
 * @throws {AssertionError} If promise resolves or error type does not match
 */
export async function assertRejects(promise, expectedErrorType, options = {}) {
  const { timeoutMs = 5000 } = options;

  if (!promise || typeof promise.then !== 'function') {
    throw new TypeError('promise must be a Promise');
  }
  if (typeof expectedErrorType !== 'function') {
    throw new TypeError('expectedErrorType must be a constructor');
  }

  const timeoutError = new Error(`Promise did not reject within ${timeoutMs}ms`);
  const timeoutPromise = new Promise((_, reject) =>
    setTimeout(() => reject(timeoutError), timeoutMs)
  );

  try {
    await Promise.race([promise, timeoutPromise]);
    throw new AssertionError('Promise resolved; expected rejection');
  } catch (err) {
    if (err instanceof timeoutError.constructor && err.message === timeoutError.message) {
      throw err; // Re-throw timeout
    }
    if (!(err instanceof expectedErrorType)) {
      throw new AssertionError(
        `Expected error of type ${expectedErrorType.name}, got ${err.constructor.name}`
      );
    }
    return err;
  }
}

/**
 * Asserts that an event is fired by an EventEmitter within a timeout.
 * @param {EventEmitter} emitter - The event emitter to listen to
 * @param {string} eventName - The event name to listen for
 * @param {Object} options - Configuration options
 * @param {number} [options.timeoutMs=5000] - Maximum wait time
 * @returns {Promise<any>} Resolves with the event data if fired within timeout
 * @throws {TimeoutError} If event is not fired within timeout
 */
export async function assertEventFired(emitter, eventName, options = {}) {
  const { timeoutMs = 5000 } = options;

  if (!emitter || typeof emitter.on !== 'function') {
    throw new TypeError('emitter must be an EventEmitter');
  }
  if (typeof eventName !== 'string') {
    throw new TypeError('eventName must be a string');
  }

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      emitter.removeListener(eventName, handler);
      reject(new Error(`Event "${eventName}" not fired within ${timeoutMs}ms`));
    }, timeoutMs);

    const handler = (data) => {
      clearTimeout(timer);
      emitter.removeListener(eventName, handler);
      resolve(data);
    };

    emitter.on(eventName, handler);
  });
}

/**
 * Creates a promise that resolves after a specified delay.
 * @param {number} ms - Delay in milliseconds
 * @returns {Promise} Resolves after the delay
 */
export function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

/**
 * Creates a deferred promise (promise with external resolve/reject).
 * @returns {Object} Object with { promise, resolve, reject }
 */
export function createDeferred() {
  let resolve, reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

/**
 * Custom AssertionError for test assertions.
 */
export class AssertionError extends Error {
  constructor(message) {
    super(message);
    this.name = 'AssertionError';
  }
}

export default {
  retryAsync,
  waitFor,
  waitForAsync,
  assertCompletes,
  assertRejects,
  assertEventFired,
  delay,
  createDeferred,
  AssertionError,
};
