/**
 * Message Priority Queue Module (Step 65)
 *
 * Provides a binary heap–based priority queue for buffering WebView messages before
 * handler dispatch. Decouples WebView I/O from handler processing, supports
 * prioritization, integrates backpressure, and collects metrics for middleware.
 *
 * Core Classes & Functions:
 * - MessagePriorityQueue — Binary heap-based queue with O(log n) operations
 * - PriorityLevel — Enum for priority levels (CRITICAL=1, NORMAL=2, LOW=3)
 * - MessageQueueError — Exception class for queue errors
 * - createMessagePriorityQueue() — Factory for queue instantiation
 *
 * Public API:
 * - insert(message, priority?) → boolean
 * - extract() → message|null
 * - peek() → message|null
 * - size() → number
 * - isFull() → boolean
 * - isPaused() → boolean
 * - pause() → void
 * - resume() → void
 * - getMetrics() → QueueMetrics
 * - clear() → void
 * - dispose() → void
 *
 * @module src/versions/v2.0.0/lib/message-priority-queue.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 63 (protocol adapter), 64 (timeout manager), 71 (handler dispatcher),
 *                72-74 (middleware), 75 (integration tests)
 */

// ===== PRIORITY LEVEL ENUM =====

/**
 * Priority levels for WebView messages.
 * Higher numeric value = lower priority.
 * @enum {number}
 */
export const PriorityLevel = Object.freeze({
  CRITICAL: 1,  // User interaction (click, keypress)
  NORMAL: 2,    // Regular handler requests
  LOW: 3        // Background tasks, telemetry
});

// ===== CUSTOM ERROR CLASSES =====

/**
 * Thrown when queue operation fails (overflow, invalid input, etc.)
 */
export class MessageQueueError extends Error {
  constructor(message, code = 'QUEUE_ERROR', details = {}) {
    super(message);
    this.name = 'MessageQueueError';
    this.code = code;
    this.details = details;
  }
}

// ===== MESSAGE WRAPPER =====

/**
 * Internal wrapper for queued messages with metadata.
 * @internal
 */
class QueuedMessage {
  constructor(message, priority, enqueuedAt, sequenceNum) {
    this.message = message;
    this.priority = priority;
    this.enqueuedAt = enqueuedAt;
    this.sequenceNum = sequenceNum;
    this.handlerId = null;
  }

  getWaitTime() {
    return Date.now() - this.enqueuedAt;
  }
}

// ===== MESSAGE PRIORITY QUEUE CLASS =====

/**
 * Binary heap–based priority queue for message buffering.
 *
 * Implements a min-heap where index 0 = highest priority (lowest numeric value).
 * Supports pause/resume backpressure, metrics collection, and graceful degradation.
 *
 * Time Complexity:
 * - insert: O(log n)
 * - extract: O(log n)
 * - peek: O(1)
 * - size: O(1)
 *
 * Space Complexity: O(n) where n = maxSize
 */
export class MessagePriorityQueue {
  /**
   * Create a new priority queue.
   * @param {number} maxSize - Maximum queue capacity
   * @param {number} defaultPriority - Default priority level (PriorityLevel.*)
   * @param {Object} logger - Optional logger instance (must have debug, info, warn, error)
   * @param {Object} metrics - Optional metrics collector (must have track, gauge methods)
   * @throws {MessageQueueError} if maxSize is invalid
   */
  constructor(maxSize, defaultPriority, logger = null, metrics = null) {
    if (!Number.isInteger(maxSize) || maxSize < 1) {
      throw new MessageQueueError(
        'maxSize must be a positive integer',
        'INVALID_MAX_SIZE',
        { maxSize }
      );
    }

    if (!Object.values(PriorityLevel).includes(defaultPriority)) {
      throw new MessageQueueError(
        'defaultPriority must be a valid PriorityLevel',
        'INVALID_DEFAULT_PRIORITY',
        { defaultPriority }
      );
    }

    this._maxSize = maxSize;
    this._defaultPriority = defaultPriority;
    this._logger = logger || this._createSilentLogger();
    this._metrics = metrics || this._createSilentMetrics();

    // Heap array: index 0 is highest priority (root)
    this._heap = [];

    // Backpressure state
    this._paused = false;

    // Sequence counter for FIFO ordering within same priority
    this._sequenceNum = 0;

    // Metrics tracking
    this._insertionCount = 0;
    this._dequeueCount = 0;
    this._latencies = []; // circular buffer (max 10k)
    this._maxLatencyBuffer = 10000;

    this._logger.debug('[MessagePriorityQueue] Initialized', {
      maxSize,
      defaultPriority,
      hasMetrics: Boolean(metrics)
    });
  }

  /**
   * Insert a message into the queue with optional priority.
   *
   * @param {*} message - Message object to queue
   * @param {number} priority - Priority level (defaults to this._defaultPriority)
   * @returns {boolean} true if inserted, false if queue full and backpressured
   * @throws {MessageQueueError} if message is null or invalid
   */
  insert(message, priority = undefined) {
    // Validation
    if (message === null || message === undefined) {
      throw new MessageQueueError(
        'Message cannot be null or undefined',
        'INVALID_MESSAGE',
        { message }
      );
    }

    if (priority !== undefined && !Object.values(PriorityLevel).includes(priority)) {
      throw new MessageQueueError(
        'Priority must be a valid PriorityLevel',
        'INVALID_PRIORITY',
        { priority }
      );
    }

    const actualPriority = priority ?? this._defaultPriority;

    // Check queue capacity
    if (this._heap.length >= this._maxSize) {
      this._logger.warn('[MessagePriorityQueue] Queue full, backpressured', {
        depth: this._heap.length,
        maxSize: this._maxSize
      });
      return false; // Caller should handle backpressure
    }

    // Create queued message with sequence number for FIFO within priority
    const queuedMsg = new QueuedMessage(message, actualPriority, Date.now(), this._sequenceNum++);

    // Add to heap and bubble up
    this._heap.push(queuedMsg);
    this._bubbleUp(this._heap.length - 1);

    // Track metrics
    this._insertionCount++;
    this._metrics.track('queue_insert', 1);
    this._metrics.gauge('queue_depth', this._heap.length);

    this._logger.debug('[MessagePriorityQueue] Message inserted', {
      depth: this._heap.length,
      priority: actualPriority
    });

    return true;
  }

  /**
   * Extract the highest-priority message from the queue.
   * Respects pause state: if paused, returns null without removing.
   *
   * @returns {*} The message object, or null if queue empty or paused
   */
  extract() {
    if (this._paused || this._heap.length === 0) {
      return null;
    }

    // Extract root (highest priority)
    const root = this._heap[0];
    const lastItem = this._heap.pop();

    // Replace root and bubble down if queue not empty
    if (this._heap.length > 0) {
      this._heap[0] = lastItem;
      this._bubbleDown(0);
    }

    // Track metrics
    const waitTime = root.getWaitTime();
    this._dequeueCount++;
    this._recordLatency(waitTime);
    this._metrics.track('queue_extract', 1);
    this._metrics.gauge('queue_depth', this._heap.length);
    this._metrics.track('queue_wait_time', waitTime);

    this._logger.debug('[MessagePriorityQueue] Message extracted', {
      depth: this._heap.length,
      priority: root.priority,
      waitTime
    });

    return root.message;
  }

  /**
   * Peek at the highest-priority message without removing it.
   *
   * @returns {*} The message object, or null if queue empty
   */
  peek() {
    return this._heap.length > 0 ? this._heap[0].message : null;
  }

  /**
   * Get current queue depth.
   *
   * @returns {number} Number of messages in queue
   */
  size() {
    return this._heap.length;
  }

  /**
   * Check if queue is at capacity.
   *
   * @returns {boolean} true if full
   */
  isFull() {
    return this._heap.length >= this._maxSize;
  }

  /**
   * Check if queue is paused (extractions blocked).
   *
   * @returns {boolean} true if paused
   */
  isPaused() {
    return this._paused;
  }

  /**
   * Pause the queue (halt extractions for backpressure).
   * Insertions are still allowed.
   *
   * @returns {void}
   */
  pause() {
    this._paused = true;
    this._logger.info('[MessagePriorityQueue] Queue paused');
  }

  /**
   * Resume the queue (enable extractions).
   *
   * @returns {void}
   */
  resume() {
    this._paused = false;
    this._logger.info('[MessagePriorityQueue] Queue resumed');
  }

  /**
   * Get queue metrics for monitoring.
   *
   * @returns {Object} QueueMetrics object:
   *   - depth: current queue size
   *   - insertCount: total messages inserted
   *   - dequeueCount: total messages extracted
   *   - avgWaitTime: average message wait time (ms)
   *   - p99WaitTime: 99th percentile wait time (ms)
   *   - maxSize: queue capacity
   *   - isPaused: backpressure state
   */
  getMetrics() {
    const avgWaitTime = this._latencies.length > 0
      ? this._latencies.reduce((a, b) => a + b, 0) / this._latencies.length
      : 0;

    const p99WaitTime = this._latencies.length > 0
      ? this._calculatePercentile(this._latencies, 99)
      : 0;

    return {
      depth: this._heap.length,
      insertCount: this._insertionCount,
      dequeueCount: this._dequeueCount,
      avgWaitTime: Math.round(avgWaitTime * 100) / 100,
      p99WaitTime: Math.round(p99WaitTime * 100) / 100,
      maxSize: this._maxSize,
      isPaused: this._paused
    };
  }

  /**
   * Clear all messages from the queue.
   *
   * @returns {void}
   */
  clear() {
    const clearedCount = this._heap.length;
    this._heap = [];
    this._metrics.gauge('queue_depth', 0);
    this._logger.info('[MessagePriorityQueue] Queue cleared', { clearedCount });
  }

  /**
   * Dispose of the queue (cleanup resources).
   *
   * @returns {void}
   */
  dispose() {
    this.clear();
    this._latencies = [];
    this._logger.info('[MessagePriorityQueue] Queue disposed');
  }

  // ===== PRIVATE HEAP METHODS =====

  /**
   * Bubble up element at index to maintain heap property.
   * Uses sequence number to preserve FIFO for equal priorities.
   * @private
   */
  _bubbleUp(index) {
    const element = this._heap[index];

    while (index > 0) {
      const parentIndex = Math.floor((index - 1) / 2);
      const parent = this._heap[parentIndex];

      // Compare priority first, then sequence number for FIFO
      if (element.priority > parent.priority ||
          (element.priority === parent.priority && element.sequenceNum > parent.sequenceNum)) {
        break; // Heap property satisfied
      }

      this._heap[index] = parent;
      index = parentIndex;
    }

    this._heap[index] = element;
  }

  /**
   * Bubble down element at index to maintain heap property.
   * @private
   */
  _bubbleDown(index) {
    const element = this._heap[index];
    const length = this._heap.length;
    const halfLength = Math.floor(length / 2);

    while (index < halfLength) {
      let childIndex = (index * 2) + 1;
      let child = this._heap[childIndex];

      const rightIndex = childIndex + 1;
      // Choose child with lower priority, or if equal, lower sequence number
      if (rightIndex < length) {
        const rightChild = this._heap[rightIndex];
        if (rightChild.priority < child.priority ||
            (rightChild.priority === child.priority && rightChild.sequenceNum < child.sequenceNum)) {
          childIndex = rightIndex;
          child = rightChild;
        }
      }

      // Compare with element using priority first, then sequence number for FIFO
      if (element.priority < child.priority ||
          (element.priority === child.priority && element.sequenceNum < child.sequenceNum)) {
        break; // Heap property satisfied
      }

      this._heap[index] = child;
      index = childIndex;
    }

    this._heap[index] = element;
  }

  // ===== PRIVATE METRICS METHODS =====

  /**
   * Record wait time in circular buffer.
   * @private
   */
  _recordLatency(waitTime) {
    this._latencies.push(waitTime);
    if (this._latencies.length > this._maxLatencyBuffer) {
      this._latencies.shift();
    }
  }

  /**
   * Calculate percentile from sorted array.
   * @private
   */
  _calculatePercentile(arr, percentile) {
    if (arr.length === 0) return 0;

    const sorted = [...arr].sort((a, b) => a - b);
    const index = Math.ceil((percentile / 100) * sorted.length) - 1;
    return sorted[Math.max(0, index)];
  }

  // ===== PRIVATE STUB LOGGERS/METRICS =====

  /**
   * Create a silent logger (no-op).
   * @private
   */
  _createSilentLogger() {
    return {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {}
    };
  }

  /**
   * Create a silent metrics collector (no-op).
   * @private
   */
  _createSilentMetrics() {
    return {
      track: () => {},
      gauge: () => {}
    };
  }
}

// ===== FACTORY FUNCTION =====

/**
 * Factory function to create a MessagePriorityQueue.
 *
 * @param {number} maxSize - Maximum queue capacity (default: 1000)
 * @param {number} defaultPriority - Default priority level (default: PriorityLevel.NORMAL)
 * @param {Object} logger - Optional logger instance
 * @param {Object} metrics - Optional metrics collector
 * @returns {MessagePriorityQueue} New queue instance
 * @throws {MessageQueueError} if parameters are invalid
 *
 * @example
 * const queue = createMessagePriorityQueue(
 *   1000,                          // maxSize
 *   PriorityLevel.NORMAL,          // defaultPriority
 *   logger,                        // optional
 *   metrics                        // optional
 * );
 *
 * // Insert a critical message
 * const inserted = queue.insert({type: 'search', data: {...}}, PriorityLevel.CRITICAL);
 *
 * // Extract highest priority
 * const msg = queue.extract();
 *
 * // Backpressure: pause when handler backlog is high
 * if (handlerBacklog > 100) queue.pause();
 * else queue.resume();
 *
 * // Monitor metrics
 * const {depth, p99WaitTime} = queue.getMetrics();
 */
export function createMessagePriorityQueue(
  maxSize = 1000,
  defaultPriority = PriorityLevel.NORMAL,
  logger = null,
  metrics = null
) {
  return new MessagePriorityQueue(maxSize, defaultPriority, logger, metrics);
}

/**
 * @typedef {Object} QueueMetrics
 * @property {number} depth - Current queue size
 * @property {number} insertCount - Total messages inserted
 * @property {number} dequeueCount - Total messages extracted
 * @property {number} avgWaitTime - Average message wait time (ms)
 * @property {number} p99WaitTime - 99th percentile wait time (ms)
 * @property {number} maxSize - Queue capacity
 * @property {boolean} isPaused - Backpressure state
 */
