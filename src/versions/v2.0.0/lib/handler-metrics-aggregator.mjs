#!/usr/bin/env node

/**
 * Handler Metrics Aggregator for ContinueVS Bridge (Step 109)
 *
 * Collects, persists, and queries handler metrics over time.
 * Bridges real-time profiling (Step 96) with historical regression analysis (Step 98/112).
 * Non-invasive collection: subscribes to ProfilerHandler snapshots every 5 seconds.
 * Persistence: `~/.continue/metrics/` with daily rotation and versioning.
 * Queries: historical data by handler, time range, metric type.
 * Analytics: rolling averages, variance tracking, anomaly detection.
 *
 * @module handler-metrics-aggregator
 */

import path from 'path';
import { fileURLToPath } from 'url';
import { MetricsStore } from './metrics-historical-store.mjs';
import { TrendAnalyzer } from './metrics-trend-analyzer.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * Custom error class for aggregator operations.
 */
export class AggregatorError extends Error {
  constructor(message, code = 'AGGREGATOR_ERROR', details = null) {
    super(message);
    this.name = 'AggregatorError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Custom error class for query operations.
 */
export class QueryError extends Error {
  constructor(message, code = 'QUERY_ERROR', details = null) {
    super(message);
    this.name = 'QueryError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Custom error class for persistence operations.
 */
export class PersistenceError extends Error {
  constructor(message, code = 'PERSISTENCE_ERROR', details = null) {
    super(message);
    this.name = 'PersistenceError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Snapshot structure interface.
 * @typedef {Object} MetricsSnapshot
 * @property {number} timestamp - Milliseconds since epoch
 * @property {Array<HandlerMetricData>} handlers - Per-handler metrics
 * @property {Object} [metadata] - System info (memory, CPU, etc.)
 */

/**
 * Handler metric data structure.
 * @typedef {Object} HandlerMetricData
 * @property {string} name - Handler message type (e.g., 'bridge:search')
 * @property {Object} latency - Latency percentiles in milliseconds
 * @property {number} latency.p50 - 50th percentile (median)
 * @property {number} latency.p95 - 95th percentile
 * @property {number} latency.p99 - 99th percentile
 * @property {number} errorRate - Error rate as decimal (0.0-1.0)
 * @property {number} requestCount - Total requests processed
 * @property {number} timeoutCount - Number of timed-out requests
 * @property {number} [cacheHitRate] - Optional cache hit rate (0.0-1.0)
 */

/**
 * Query options structure.
 * @typedef {Object} QueryOptions
 * @property {number} [since] - Start timestamp (milliseconds)
 * @property {number} [until] - End timestamp (milliseconds)
 * @property {number} [limit] - Max results to return
 * @property {string} [aggregation] - 'latest' or 'average' (default: 'latest')
 */

/**
 * Snapshot collector: manages collection lifecycle.
 */
class SnapshotCollector {
  constructor(options = {}) {
    this.collectionInterval = options.collectionInterval || 5000; // 5 seconds
    this.bufferSize = options.bufferSize || 100; // Number of snapshots to buffer
    this.buffer = [];
    this.collectionTimer = null;
    this.lastSnapshotTime = 0;
    this.profilerRpc = options.profilerRpc; // RPC interface to ProfilerHandler
    this.store = options.store; // MetricsStore instance
    this.logger = options.logger;
    this.isCollecting = false;
  }

  /**
   * Start collecting snapshots from ProfilerHandler.
   */
  async start() {
    if (this.isCollecting) {
      throw new AggregatorError('Collection already started', 'ALREADY_STARTED');
    }

    this.isCollecting = true;
    this.logger?.log?.('[Aggregator] Starting snapshot collection...');

    // Schedule periodic collection
    this.collectionTimer = setInterval(async () => {
      try {
        await this._collectSnapshot();
      } catch (err) {
        this.logger?.error?.(`[Aggregator] Snapshot collection failed: ${err.message}`);
      }
    }, this.collectionInterval);

    // Initial snapshot
    try {
      await this._collectSnapshot();
    } catch (err) {
      this.logger?.error?.(`[Aggregator] Initial snapshot failed: ${err.message}`);
    }
  }

  /**
   * Stop collecting and persist remaining data.
   */
  async stop() {
    if (!this.isCollecting) {
      return;
    }

    this.isCollecting = false;
    if (this.collectionTimer) {
      clearInterval(this.collectionTimer);
      this.collectionTimer = null;
    }

    this.logger?.log?.('[Aggregator] Stopping snapshot collection...');

    // Persist any remaining buffered snapshots
    if (this.buffer.length > 0) {
      try {
        await this._persistBuffer();
      } catch (err) {
        this.logger?.error?.(`[Aggregator] Final persistence failed: ${err.message}`);
      }
    }
  }

  /**
   * Collect single snapshot from ProfilerHandler.
   */
  async _collectSnapshot() {
    if (!this.profilerRpc) {
      this.logger?.warn?.('[Aggregator] ProfilerHandler RPC not available');
      return;
    }

    try {
      // Call ProfilerHandler to get real-time metrics
      const snapshot = await this.profilerRpc.getProfilerData?.();
      if (!snapshot) {
        return;
      }

      this.lastSnapshotTime = Date.now();
      const wrappedSnapshot = {
        timestamp: this.lastSnapshotTime,
        handlers: snapshot.handlers || [],
        metadata: {
          collectionInterval: this.collectionInterval,
          bufferSize: this.bufferSize
        }
      };

      this.buffer.push(wrappedSnapshot);

      // Persist if buffer is full
      if (this.buffer.length >= this.bufferSize) {
        await this._persistBuffer();
      }
    } catch (err) {
      this.logger?.error?.(`[Aggregator] Collection snapshot error: ${err.message}`);
    }
  }

  /**
   * Persist buffered snapshots to disk.
   */
  async _persistBuffer() {
    if (!this.store || this.buffer.length === 0) {
      return;
    }

    try {
      for (const snapshot of this.buffer) {
        await this.store.append(snapshot);
      }
      this.logger?.log?.(`[Aggregator] Persisted ${this.buffer.length} snapshots`);
      this.buffer = [];
    } catch (err) {
      throw new PersistenceError(
        `Failed to persist snapshots: ${err.message}`,
        'PERSIST_FAILED',
        { originalError: err.message }
      );
    }
  }
}

/**
 * Main aggregator class: collects, stores, and queries metrics.
 */
export class HandlerMetricsAggregator {
  constructor(options = {}) {
    this.logger = options.logger;
    this.metricsCollector = options.metricsCollector; // Optional host-side collector
    this.collectionInterval = options.collectionInterval || 5000;
    this.bufferSize = options.bufferSize || 100;
    this.retentionDays = options.retentionDays || 7;

    // Initialize storage and analysis
    this.store = new MetricsStore({
      baseDir: options.baseDir,
      logger: this.logger
    });
    this.trendAnalyzer = new TrendAnalyzer({
      logger: this.logger
    });

    // Initialize collector (requires ProfilerHandler RPC)
    this.collector = new SnapshotCollector({
      collectionInterval: this.collectionInterval,
      bufferSize: this.bufferSize,
      profilerRpc: options.profilerRpc,
      store: this.store,
      logger: this.logger
    });

    this.isInitialized = false;
  }

  /**
   * Initialize aggregator (must be called before start).
   */
  async initialize() {
    if (this.isInitialized) {
      return;
    }

    try {
      await this.store.initialize();
      this.isInitialized = true;
      this.logger?.log?.('[Aggregator] Initialized successfully');
    } catch (err) {
      throw new AggregatorError(
        `Initialization failed: ${err.message}`,
        'INIT_FAILED',
        { originalError: err.message }
      );
    }
  }

  /**
   * Start collection and persistence.
   */
  async start() {
    if (!this.isInitialized) {
      await this.initialize();
    }

    try {
      await this.collector.start();
      this.logger?.log?.('[Aggregator] Collection started');
    } catch (err) {
      throw new AggregatorError(
        `Failed to start collection: ${err.message}`,
        'START_FAILED',
        { originalError: err.message }
      );
    }
  }

  /**
   * Stop collection gracefully.
   */
  async stop() {
    try {
      await this.collector.stop();
      this.logger?.log?.('[Aggregator] Collection stopped');
    } catch (err) {
      this.logger?.error?.(`[Aggregator] Stop failed: ${err.message}`);
    }
  }

  /**
   * Query historical metrics by handler name and time range.
   * @param {string} handlerName - Handler message type (e.g., 'bridge:search')
   * @param {QueryOptions} options - Query criteria
   * @returns {Promise<Array<HandlerMetricData>>} - Matching metrics
   */
  async queryMetrics(handlerName, options = {}) {
    if (!handlerName || typeof handlerName !== 'string') {
      throw new QueryError('Handler name must be a non-empty string', 'INVALID_HANDLER');
    }

    try {
      const snapshots = await this.store.read({
        handlerName,
        since: options.since,
        until: options.until,
        limit: options.limit
      });

      if (!snapshots || snapshots.length === 0) {
        return [];
      }

      // Extract metrics for this handler from all snapshots
      const metrics = snapshots
        .flatMap(snap => snap.handlers || [])
        .filter(h => h.name === handlerName);

      if (options.aggregation === 'average' && metrics.length > 0) {
        return [this._aggregateMetrics(metrics)];
      }

      return metrics;
    } catch (err) {
      throw new QueryError(
        `Query failed for handler '${handlerName}': ${err.message}`,
        'QUERY_FAILED',
        { handlerName, options }
      );
    }
  }

  /**
   * Get most recent snapshot.
   */
  async getLatestSnapshot() {
    try {
      const snapshots = await this.store.read({ limit: 1 });
      return snapshots && snapshots.length > 0 ? snapshots[0] : null;
    } catch (err) {
      throw new QueryError(
        `Failed to get latest snapshot: ${err.message}`,
        'LATEST_FAILED'
      );
    }
  }

  /**
   * Get trend analysis for a handler.
   * @param {string} handlerName - Handler message type
   * @param {number} [window] - Number of samples for rolling average (default 5)
   * @returns {Promise<Object>} - Trend report
   */
  async getTrend(handlerName, window = 5) {
    try {
      const metrics = await this.queryMetrics(handlerName, { limit: window * 2 });

      if (metrics.length === 0) {
        return {
          handler: handlerName,
          direction: 'UNKNOWN',
          confidence: 0,
          message: 'No metrics available'
        };
      }

      return this.trendAnalyzer.generateTrend(metrics, window);
    } catch (err) {
      throw new QueryError(
        `Trend analysis failed for handler '${handlerName}': ${err.message}`,
        'TREND_FAILED',
        { handlerName }
      );
    }
  }

  /**
   * Detect anomalies in metrics.
   * @param {string} handlerName - Handler message type
   * @param {number} [threshold] - Standard deviation threshold (default 2)
   * @returns {Promise<Array>} - Anomaly reports
   */
  async detectAnomalies(handlerName, threshold = 2) {
    try {
      const metrics = await this.queryMetrics(handlerName, { limit: 100 });

      if (metrics.length < 5) {
        return []; // Need minimum data for anomaly detection
      }

      const p99Values = metrics.map(m => m.latency?.p99 || 0);
      const anomalies = this.trendAnalyzer.detectAnomalies(
        p99Values[p99Values.length - 1],
        p99Values,
        threshold
      );

      return anomalies || [];
    } catch (err) {
      this.logger?.error?.(`[Aggregator] Anomaly detection failed: ${err.message}`);
      return [];
    }
  }

  /**
   * Get storage statistics.
   */
  async getStorageStats() {
    try {
      return await this.store.getStorageStats?.();
    } catch (err) {
      this.logger?.error?.(`[Aggregator] Storage stats failed: ${err.message}`);
      return null;
    }
  }

  /**
   * Cleanup old snapshots.
   * @param {number} [olderThanDays] - Delete snapshots older than N days (default 7)
   */
  async cleanup(olderThanDays = this.retentionDays) {
    try {
      const before = Date.now() - olderThanDays * 24 * 60 * 60 * 1000;
      await this.store.cleanup(before);
      this.logger?.log?.(`[Aggregator] Cleaned up snapshots older than ${olderThanDays} days`);
    } catch (err) {
      this.logger?.error?.(`[Aggregator] Cleanup failed: ${err.message}`);
    }
  }

  /**
   * Aggregate multiple metric snapshots into single average.
   */
  _aggregateMetrics(metrics) {
    if (metrics.length === 0) {
      return null;
    }

    const count = metrics.length;
    const p50Values = metrics.map(m => m.latency?.p50 || 0);
    const p95Values = metrics.map(m => m.latency?.p95 || 0);
    const p99Values = metrics.map(m => m.latency?.p99 || 0);
    const errorRates = metrics.map(m => m.errorRate || 0);
    const requestCounts = metrics.map(m => m.requestCount || 0);

    return {
      name: metrics[0].name,
      latency: {
        p50: p50Values.reduce((a, b) => a + b, 0) / count,
        p95: p95Values.reduce((a, b) => a + b, 0) / count,
        p99: p99Values.reduce((a, b) => a + b, 0) / count
      },
      errorRate: errorRates.reduce((a, b) => a + b, 0) / count,
      requestCount: requestCounts.reduce((a, b) => a + b, 0),
      sampleCount: count,
      aggregationType: 'average'
    };
  }
}

/**
 * Factory function to create aggregator instance.
 * @param {Object} config - Configuration object
 * @param {Object} [logger] - Optional logger
 * @param {Object} [metricsCollector] - Optional host-side metrics collector
 * @returns {HandlerMetricsAggregator} - Initialized aggregator
 */
export function createHandlerMetricsAggregator(config = {}, logger = null, metricsCollector = null) {
  return new HandlerMetricsAggregator({
    logger,
    metricsCollector,
    collectionInterval: config.collectionInterval,
    bufferSize: config.bufferSize,
    retentionDays: config.retentionDays,
    baseDir: config.baseDir,
    profilerRpc: config.profilerRpc
  });
}
