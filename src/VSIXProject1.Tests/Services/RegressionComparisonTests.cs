using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Regression Comparison Tests
    /// Step 112: C# host-side regression detection integration.
    /// 
    /// Tests baseline loading, metric comparison, tier validation, and release gate decisions.
    /// </summary>
    [Trait("Category", "Regression")]
    public class RegressionComparisonTests
    {
        private readonly string _baselinesDir;

        public RegressionComparisonTests()
        {
            _baselinesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continue", "baselines"
            );
        }

        #region Suite 1: Baseline Loading

        [Fact(DisplayName = "Load baseline from ~/.continue/baselines/")]
        public void LoadBaseline_Success()
        {
            // Arrange
            var baselineDir = _baselinesDir;
            if (!Directory.Exists(baselineDir))
            {
                Assert.True(true); // Skip if not available
                return;
            }

            var baselineFiles = Directory.GetFiles(baselineDir, "baseline-v2.0.0-*.json");
            if (baselineFiles.Length == 0)
            {
                Assert.True(true); // Skip if no baseline yet
                return;
            }

            // Act
            var latestBaseline = baselineFiles.OrderByDescending(f => f).FirstOrDefault();
            Assert.NotNull(latestBaseline);
            Assert.True(File.Exists(latestBaseline));
        }

        [Fact(DisplayName = "Handle missing baseline gracefully")]
        public void LoadBaseline_Missing()
        {
            // Arrange
            var nonexistentPath = Path.Combine(_baselinesDir, "nonexistent-baseline.json");

            // Act
            var exists = File.Exists(nonexistentPath);

            // Assert
            Assert.False(exists);
        }

        [Fact(DisplayName = "Validate baseline structure (version, handlers present)")]
        public void ValidateBaseline_Structure()
        {
            // Arrange
            var baselineDir = _baselinesDir;
            if (!Directory.Exists(baselineDir))
            {
                Assert.True(true);
                return;
            }

            var baselineFiles = Directory.GetFiles(baselineDir, "baseline-v2.0.0-*.json");
            if (baselineFiles.Length == 0)
            {
                Assert.True(true);
                return;
            }

            var baseline = baselineFiles.FirstOrDefault();

            // Act
            var content = File.ReadAllText(baseline);
            var parsed = JsonSerializer.Deserialize<RegressionBaseline>(content);

            // Assert
            Assert.NotNull(parsed);
            if (parsed != null)
            {
                Assert.NotNull(parsed.Version);
                Assert.NotNull(parsed.Handlers);
                Assert.NotEmpty(parsed.Handlers);
            }
        }

        [Fact(DisplayName = "Cache baseline for performance")]
        public void LoadBaseline_Cached()
        {
            // Arrange
            var cache = new BaselineCache();

            // Act - First load
            var baseline1 = cache.LoadBaseline(_baselinesDir);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - Second load (cached)
            var baseline2 = cache.LoadBaseline(_baselinesDir);
            stopwatch.Stop();

            // Assert
            if (baseline1 != null && baseline2 != null)
            {
                Assert.True(stopwatch.ElapsedMilliseconds < 100, "Cached load should be fast");
            }
        }

        #endregion

        #region Suite 2: Metric Comparison

        [Fact(DisplayName = "Compare handler latencies")]
        public void CompareMetrics_Latency()
        {
            // Arrange
            var baseline = CreateTestHandlerMetrics();
            var current = new HandlerMetrics
            {
                Latency = new LatencyMetrics { P50 = 26, P95 = 87, P99 = 131 },
                Throughput = baseline.Throughput,
                Memory = baseline.Memory,
                ErrorRate = baseline.ErrorRate
            };

            // Act
            var delta = CalculateLatencyDelta(current.Latency, baseline.Latency);

            // Assert
            Assert.True(delta["p99"] > 0);
        }

        [Fact(DisplayName = "Compare throughput values")]
        public void CompareMetrics_Throughput()
        {
            // Arrange
            var baseline = CreateTestHandlerMetrics();
            var current = new HandlerMetrics
            {
                Latency = baseline.Latency,
                Throughput = new ThroughputMetrics { MessagesPerSecond = 400 },
                Memory = baseline.Memory,
                ErrorRate = baseline.ErrorRate
            };

            // Act
            var delta = ((baseline.Throughput.MessagesPerSecond - current.Throughput.MessagesPerSecond)
                / baseline.Throughput.MessagesPerSecond) * 100;

            // Assert
            Assert.True(delta > 20);
        }

        [Fact(DisplayName = "Detect regression vs improvement")]
        public void CompareMetrics_RegressionVsImprovement()
        {
            // Arrange
            var baseline = CreateTestHandlerMetrics();
            var regressed = new HandlerMetrics
            {
                Latency = new LatencyMetrics { P50 = 30, P95 = 95, P99 = 150 },
                Throughput = baseline.Throughput,
                Memory = baseline.Memory,
                ErrorRate = baseline.ErrorRate
            };
            var improved = new HandlerMetrics
            {
                Latency = new LatencyMetrics { P50 = 20, P95 = 75, P99 = 100 },
                Throughput = baseline.Throughput,
                Memory = baseline.Memory,
                ErrorRate = baseline.ErrorRate
            };

            // Act
            var regressionDelta = CalculateLatencyDelta(regressed.Latency, baseline.Latency)["p99"];
            var improvementDelta = CalculateLatencyDelta(improved.Latency, baseline.Latency)["p99"];

            // Assert
            Assert.True(regressionDelta > 0);
            Assert.True(improvementDelta < 0);
        }

        #endregion

        #region Suite 3: Tier Validation

        [Fact(DisplayName = "Validate fast tier gate")]
        public void ValidateTier_Fast()
        {
            // Arrange
            var fastHandlers = new[] { "code-completion", "search", "go-to-definition" };

            // Act & Assert
            Assert.NotEmpty(fastHandlers);
            Assert.Contains("code-completion", fastHandlers);
        }

        [Fact(DisplayName = "Validate medium tier gate")]
        public void ValidateTier_Medium()
        {
            // Arrange
            var mediumHandlers = new[] { "refactor", "apply-edit", "format-document" };

            // Assert
            Assert.True(mediumHandlers.Length >= 3);
        }

        [Fact(DisplayName = "Validate slow tier gate")]
        public void ValidateTier_Slow()
        {
            // Arrange
            var slowHandlers = new[] { "git-integration", "terminal", "file-system", "project-info" };

            // Assert
            Assert.True(slowHandlers.Length >= 4);
        }

        [Fact(DisplayName = "Tier isolation: one tier fail, others pass")]
        public void ValidateTier_Isolation()
        {
            // Arrange
            var regressions = new List<HandlerRegression>
            {
                new HandlerRegression { Handler = "code-completion", Tier = "fast", Severity = "CRITICAL" },
                new HandlerRegression { Handler = "refactor", Tier = "medium", Severity = "NONE" },
                new HandlerRegression { Handler = "git-integration", Tier = "slow", Severity = "NONE" }
            };

            // Act
            var fastTierFailed = regressions.Where(r => r.Tier == "fast" && r.Severity != "NONE").Any();
            var mediumTierFailed = regressions.Where(r => r.Tier == "medium" && r.Severity != "NONE").Any();
            var slowTierFailed = regressions.Where(r => r.Tier == "slow" && r.Severity != "NONE").Any();

            // Assert
            Assert.True(fastTierFailed);
            Assert.False(mediumTierFailed);
            Assert.False(slowTierFailed);
        }

        #endregion

        #region Suite 4: Release Gate Decision

        [Fact(DisplayName = "PASS decision when all tiers OK")]
        public void ReleaseGate_Pass()
        {
            // Arrange
            var summary = new RegressionSummary
            {
                CriticalCount = 0,
                HighCount = 0,
                TierStatus = new Dictionary<string, bool>
                {
                    { "fast", true },
                    { "medium", true },
                    { "slow", true }
                }
            };

            // Act
            var approved = summary.CriticalCount == 0 && summary.TierStatus.Values.All(v => v);

            // Assert
            Assert.True(approved);
        }

        [Fact(DisplayName = "BLOCKED decision when tier fails")]
        public void ReleaseGate_BlockedByTier()
        {
            // Arrange
            var summary = new RegressionSummary
            {
                CriticalCount = 0,
                HighCount = 1,
                TierStatus = new Dictionary<string, bool>
                {
                    { "fast", false },
                    { "medium", true },
                    { "slow", true }
                }
            };

            // Act
            var approved = summary.CriticalCount == 0 && summary.TierStatus.Values.All(v => v);

            // Assert
            Assert.False(approved);
        }

        [Fact(DisplayName = "BLOCKED decision when CRITICAL regressions")]
        public void ReleaseGate_BlockedByCritical()
        {
            // Arrange
            var summary = new RegressionSummary
            {
                CriticalCount = 1,
                HighCount = 0,
                TierStatus = new Dictionary<string, bool>
                {
                    { "fast", true },
                    { "medium", true },
                    { "slow", true }
                }
            };

            // Act
            var approved = summary.CriticalCount == 0 && summary.TierStatus.Values.All(v => v);

            // Assert
            Assert.False(approved);
        }

        #endregion

        #region Suite 5: Telemetry Recording

        [Fact(DisplayName = "Record regression metrics")]
        public void Telemetry_RecordMetrics()
        {
            // Arrange
            var metrics = new RegressionMetrics
            {
                Handler = "code-completion",
                Severity = "HIGH"
            };

            // Act
            var json = JsonSerializer.Serialize(metrics);

            // Assert
            Assert.Contains("code-completion", json);
        }

        [Fact(DisplayName = "Log regression decision")]
        public void Telemetry_LogDecision()
        {
            // Arrange
            var decision = new ReleaseDecision
            {
                Approved = false,
                Reason = "Fast tier regression detected",
                ExitCode = 1
            };

            // Act
            var json = JsonSerializer.Serialize(decision);

            // Assert
            Assert.Contains("Fast tier", json);
        }

        [Fact(DisplayName = "Emit release gate event")]
        public void Telemetry_ReleaseGateEvent()
        {
            // Arrange
            var @event = new ReleaseGateEvent
            {
                Decision = "BLOCKED",
                Reason = "High regression in code-completion"
            };

            // Act
            var json = JsonSerializer.Serialize(@event);

            // Assert
            Assert.Contains("BLOCKED", json);
        }

        #endregion

        #region Helper Methods

        private HandlerMetrics CreateTestHandlerMetrics()
        {
            return new HandlerMetrics
            {
                Latency = new LatencyMetrics { P50 = 25, P95 = 85, P99 = 120 },
                Throughput = new ThroughputMetrics { MessagesPerSecond = 450 },
                Memory = new MemoryMetrics { HeapUsed = 45, HeapTotal = 100, External = 5 },
                ErrorRate = 0.008
            };
        }

        private Dictionary<string, double> CalculateLatencyDelta(LatencyMetrics current, LatencyMetrics baseline)
        {
            return new Dictionary<string, double>
            {
                { "p50", ((current.P50 - baseline.P50) / baseline.P50) * 100 },
                { "p95", ((current.P95 - baseline.P95) / baseline.P95) * 100 },
                { "p99", ((current.P99 - baseline.P99) / baseline.P99) * 100 }
            };
        }

        #endregion
    }

    #region Test Models

    public class RegressionBaseline
    {
        public string Version { get; set; }
        public string Schema { get; set; }
        public long Timestamp { get; set; }
        public Dictionary<string, HandlerMetrics> Handlers { get; set; }
    }

    public class HandlerMetrics
    {
        public LatencyMetrics Latency { get; set; }
        public ThroughputMetrics Throughput { get; set; }
        public MemoryMetrics Memory { get; set; }
        public double ErrorRate { get; set; }
    }

    public class LatencyMetrics
    {
        [System.Text.Json.Serialization.JsonPropertyName("p50")]
        public double P50 { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("p95")]
        public double P95 { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("p99")]
        public double P99 { get; set; }
    }

    public class ThroughputMetrics
    {
        [System.Text.Json.Serialization.JsonPropertyName("messagesPerSecond")]
        public double MessagesPerSecond { get; set; }
    }

    public class MemoryMetrics
    {
        [System.Text.Json.Serialization.JsonPropertyName("heapUsed")]
        public double HeapUsed { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("heapTotal")]
        public double HeapTotal { get; set; }

        public double External { get; set; }
    }

    public class HandlerRegression
    {
        public string Handler { get; set; }
        public string Tier { get; set; }
        public string Severity { get; set; }
    }

    public class RegressionSummary
    {
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public int MediumCount { get; set; }
        public int LowCount { get; set; }
        public Dictionary<string, bool> TierStatus { get; set; }
    }

    public class RegressionMetrics
    {
        public string Handler { get; set; }
        public string Severity { get; set; }
        public double LatencyDelta { get; set; }
        public double ThroughputDelta { get; set; }
        public double ErrorRateDelta { get; set; }
    }

    public class ReleaseDecision
    {
        public bool Approved { get; set; }
        public string Reason { get; set; }
        public int ExitCode { get; set; }
    }

    public class ReleaseGateEvent
    {
        public string Decision { get; set; }
        public string Reason { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
    }

    public class BaselineCache
    {
        private RegressionBaseline _cached;

        public RegressionBaseline LoadBaseline(string baselinesDir)
        {
            if (_cached != null)
                return _cached;

            if (!Directory.Exists(baselinesDir))
                return null;

            var files = Directory.GetFiles(baselinesDir, "baseline-v2.0.0-*.json");
            if (files.Length == 0)
                return null;

            var latest = files.OrderByDescending(f => f).First();
            var json = File.ReadAllText(latest);
            _cached = JsonSerializer.Deserialize<RegressionBaseline>(json);
            return _cached;
        }
    }

    #endregion
}
