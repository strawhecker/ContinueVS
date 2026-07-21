#!/usr/bin/env node

/**
 * Metrics Trend Analyzer for ContinueVS Bridge (Step 109)
 *
 * Statistical analysis of metrics over time.
 * Calculates rolling averages, variance, anomaly detection, and trends.
 * Feeds regression detector (Step 98) and E2E baseline scenarios (Step 110).
 *
 * @module metrics-trend-analyzer
 */

/**
 * Custom error class for analysis operations.
 */
export class AnalysisError extends Error {
  constructor(message, code = 'ANALYSIS_ERROR', details = null) {
    super(message);
    this.name = 'AnalysisError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Trend analyzer: statistical analysis and anomaly detection.
 */
export class TrendAnalyzer {
  constructor(options = {}) {
    this.logger = options.logger;
    this.anomalyThreshold = options.anomalyThreshold || 2; // Standard deviations
    this.minSamples = options.minSamples || 5;
  }

  /**
   * Calculate rolling average for latency percentile.
   * @param {number[]} latencies - Array of p99 latency values (milliseconds)
   * @param {number} window - Window size (number of samples)
   * @returns {number[]} - Rolling averages
   */
  calculateRollingAverage(latencies, window = 5) {
    if (!Array.isArray(latencies) || latencies.length === 0) {
      return [];
    }

    if (window < 2 || window > latencies.length) {
      throw new AnalysisError(
        `Window size ${window} invalid for ${latencies.length} samples`,
        'INVALID_WINDOW'
      );
    }

    const results = [];
    for (let i = window - 1; i < latencies.length; i++) {
      const windowSlice = latencies.slice(i - window + 1, i + 1);
      const avg = windowSlice.reduce((a, b) => a + b, 0) / window;
      results.push(avg);
    }

    return results;
  }

  /**
   * Calculate variance and standard deviation.
   * @param {number[]} samples - Array of values
   * @returns {Object} - { mean, variance, stdDev }
   */
  calculateVariance(samples) {
    if (!Array.isArray(samples) || samples.length === 0) {
      throw new AnalysisError('Samples array required', 'INVALID_SAMPLES');
    }

    const mean = samples.reduce((a, b) => a + b, 0) / samples.length;
    const variance = samples.reduce((sum, val) => {
      return sum + Math.pow(val - mean, 2);
    }, 0) / samples.length;

    return {
      mean,
      variance,
      stdDev: Math.sqrt(variance),
      count: samples.length
    };
  }

  /**
   * Detect anomalies: values outside mean ± (threshold × stdDev).
   * @param {number} currentValue - Current measurement
   * @param {number[]} history - Historical values (including current)
   * @param {number} threshold - Standard deviation threshold (default 2)
   * @returns {Object|null} - Anomaly report or null if no anomaly
   */
  detectAnomalies(currentValue, history, threshold = 2) {
    if (!Array.isArray(history) || history.length < this.minSamples) {
      return null; // Not enough data
    }

    try {
      // Calculate statistics on history (excluding current if it's not in history)
      const stats = this.calculateVariance(history);

      // Check if current is anomalous
      const lowerBound = stats.mean - threshold * stats.stdDev;
      const upperBound = stats.mean + threshold * stats.stdDev;
      const isAnomalous = currentValue < lowerBound || currentValue > upperBound;

      if (!isAnomalous) {
        return null;
      }

      return {
        current: currentValue,
        mean: stats.mean,
        stdDev: stats.stdDev,
        threshold,
        lowerBound,
        upperBound,
        deviation: Math.abs(currentValue - stats.mean) / stats.stdDev,
        type: currentValue > upperBound ? 'HIGH' : 'LOW',
        severity: this._calculateSeverity(currentValue, stats, threshold)
      };
    } catch (err) {
      this.logger?.error?.(`[Analyzer] Anomaly detection error: ${err.message}`);
      return null;
    }
  }

  /**
   * Calculate anomaly severity (CRITICAL, HIGH, MEDIUM, LOW).
   */
  _calculateSeverity(currentValue, stats, threshold) {
    const deviation = Math.abs(currentValue - stats.mean) / stats.stdDev;

    if (deviation > threshold * 3) {
      return 'CRITICAL';
    } else if (deviation > threshold * 2) {
      return 'HIGH';
    } else if (deviation > threshold * 1.5) {
      return 'MEDIUM';
    }
    return 'LOW';
  }

  /**
   * Generate trend report for handler metrics.
   * @param {Array} metrics - Array of handler metric snapshots
   * @param {number} window - Rolling window size
   * @returns {Object} - Trend report with direction, slope, confidence
   */
  generateTrend(metrics, window = 5) {
    if (!Array.isArray(metrics) || metrics.length === 0) {
      return {
        direction: 'UNKNOWN',
        confidence: 0,
        message: 'No metrics available'
      };
    }

    try {
      // Extract p99 latencies
      const p99Values = metrics
        .map(m => m.latency?.p99 || 0)
        .filter(v => v > 0);

      if (p99Values.length < 3) {
        return {
          direction: 'UNKNOWN',
          confidence: 0,
          message: 'Insufficient data'
        };
      }

      // Calculate rolling averages
      const rollingAvg = this.calculateRollingAverage(p99Values, Math.min(window, 5));

      if (rollingAvg.length < 2) {
        return {
          direction: 'UNKNOWN',
          confidence: 0,
          message: 'Cannot calculate trend'
        };
      }

      // Calculate slope (rate of change)
      const recent = rollingAvg.slice(-Math.min(5, Math.floor(rollingAvg.length / 2)));
      const slope = this._calculateSlope(recent);

      // Determine direction
      const direction = this._determineDirection(slope);

      // Calculate confidence (0-1)
      const stats = this.calculateVariance(recent);
      const coefficientOfVariation = stats.stdDev / stats.mean;
      const confidence = Math.max(0, 1 - coefficientOfVariation);

      return {
        handler: metrics[0].name || 'unknown',
        direction,
        slope,
        confidence: Math.round(confidence * 100) / 100,
        currentP99: p99Values[p99Values.length - 1],
        rollingAverage: Math.round(rollingAvg[rollingAvg.length - 1] * 100) / 100,
        mean: Math.round(stats.mean * 100) / 100,
        variance: Math.round(stats.variance * 100) / 100,
        sampleCount: p99Values.length,
        recommendation: this._getRecommendation(direction, confidence)
      };
    } catch (err) {
      this.logger?.error?.(`[Analyzer] Trend generation error: ${err.message}`);
      return {
        direction: 'ERROR',
        confidence: 0,
        message: err.message
      };
    }
  }

  /**
   * Calculate slope using linear regression.
   */
  _calculateSlope(values) {
    if (values.length < 2) {
      return 0;
    }

    const n = values.length;
    const xMean = (n - 1) / 2;
    const yMean = values.reduce((a, b) => a + b, 0) / n;

    let numerator = 0;
    let denominator = 0;

    for (let i = 0; i < n; i++) {
      numerator += (i - xMean) * (values[i] - yMean);
      denominator += Math.pow(i - xMean, 2);
    }

    return denominator !== 0 ? numerator / denominator : 0;
  }

  /**
   * Determine direction from slope.
   */
  _determineDirection(slope) {
    if (Math.abs(slope) < 0.1) {
      return 'STABLE';
    } else if (slope > 0.1) {
      return 'INCREASING';
    }
    return 'DECREASING';
  }

  /**
   * Get recommendation based on trend.
   */
  _getRecommendation(direction, confidence) {
    if (direction === 'INCREASING' && confidence > 0.7) {
      return 'INVESTIGATE_REGRESSION';
    } else if (direction === 'DECREASING' && confidence > 0.7) {
      return 'IMPROVEMENT_DETECTED';
    } else if (direction === 'STABLE') {
      return 'NORMAL';
    }
    return 'INSUFFICIENT_DATA';
  }

  /**
   * Compare two metric samples for regression.
   * @param {Object} current - Current metrics
   * @param {Object} baseline - Baseline metrics
   * @param {number} tolerancePercent - Tolerance threshold (default 10%)
   * @returns {Object} - Comparison result
   */
  compareMetrics(current, baseline, tolerancePercent = 10) {
    if (!current || !baseline) {
      return {
        hasRegression: false,
        reason: 'Missing metrics'
      };
    }

    const currentP99 = current.latency?.p99 || 0;
    const baselineP99 = baseline.latency?.p99 || 0;

    if (baselineP99 === 0) {
      return {
        hasRegression: false,
        reason: 'No baseline p99'
      };
    }

    const percentChange = ((currentP99 - baselineP99) / baselineP99) * 100;
    const hasRegression = percentChange > tolerancePercent;

    return {
      hasRegression,
      percentChange: Math.round(percentChange * 100) / 100,
      tolerance: tolerancePercent,
      baseline: Math.round(baselineP99 * 100) / 100,
      current: Math.round(currentP99 * 100) / 100,
      severity: this._calculateRegressionSeverity(percentChange),
      recommendation: hasRegression
        ? `Review performance: p99 increased by ${Math.round(percentChange)}%`
        : 'Performance within tolerance'
    };
  }

  /**
   * Calculate regression severity.
   */
  _calculateRegressionSeverity(percentChange) {
    const absChange = Math.abs(percentChange);
    if (absChange > 50) {
      return 'CRITICAL';
    } else if (absChange > 25) {
      return 'HIGH';
    } else if (absChange > 10) {
      return 'MEDIUM';
    }
    return 'LOW';
  }

  /**
   * Calculate trend over multiple metrics samples.
   * Returns trending data for dashboard/reporting.
   */
  calculateMetricsTrending(metricsHistory, window = 5) {
    if (!Array.isArray(metricsHistory) || metricsHistory.length === 0) {
      return null;
    }

    const trending = [];

    for (const metric of metricsHistory) {
      if (!metric.latency?.p99) continue;

      trending.push({
        timestamp: metric.timestamp || Date.now(),
        p99: metric.latency.p99,
        p95: metric.latency.p95,
        p50: metric.latency.p50,
        errorRate: metric.errorRate || 0,
        requestCount: metric.requestCount || 0
      });
    }

    // Calculate rolling averages
    const p99Values = trending.map(t => t.p99);
    const rollingAvg = this.calculateRollingAverage(p99Values, Math.min(window, p99Values.length));

    return {
      samples: trending,
      rollingAverage: rollingAvg,
      latest: trending[trending.length - 1],
      trend: this.generateTrend(metricsHistory, window)
    };
  }
}
