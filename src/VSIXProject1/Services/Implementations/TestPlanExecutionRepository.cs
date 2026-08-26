using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// File-based repository for TestPlanExecution records.
    /// Stores execution history in separate files from TestPlan definitions.
    /// Uses JSON serialization for persistence and supports multi-run history tracking.
    /// </summary>
    public class TestPlanExecutionRepository : ITestPlanExecutionRepository
    {
        private readonly IConfigService _configService;

        /// <summary>
        /// Initializes the repository with configuration service for base directory resolution.
        /// </summary>
        /// <param name="configService">Configuration service for base path resolution.</param>
        /// <exception cref="ArgumentNullException">Thrown if configService is null.</exception>
        public TestPlanExecutionRepository(IConfigService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Saves a TestPlanExecution record to disk in JSON format.
        /// Creates <baseDir>/executions/<planId>/execution.json
        /// </summary>
        public async Task SaveTestPlanExecutionAsync(TestPlanExecution execution, CancellationToken cancellationToken = default)
        {
            if (execution == null)
            {
                throw new ArgumentNullException(nameof(execution));
            }

            var filePath = GetExecutionFilePath(execution.PlanId);
            var directory = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(execution, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(filePath, json), cancellationToken);
        }

        /// <summary>
        /// Loads the most recent TestPlanExecution record for a plan ID.
        /// Returns null if no execution record exists.
        /// </summary>
        public async Task<TestPlanExecution?> LoadTestPlanExecutionAsync(string planId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(planId))
            {
                throw new ArgumentException("Plan ID cannot be null or empty.", nameof(planId));
            }

            var filePath = GetExecutionFilePath(planId);

            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = await Task.Run(() => File.ReadAllText(filePath), cancellationToken);
            var execution = JsonConvert.DeserializeObject<TestPlanExecution>(json);
            return execution;
        }

        /// <summary>
        /// Retrieves all execution history for a plan (future expansion: supports versioning).
        /// Currently returns list with single latest execution for compatibility.
        /// </summary>
        public async Task<List<TestPlanExecution>> GetExecutionHistoryAsync(string planId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(planId))
            {
                throw new ArgumentException("Plan ID cannot be null or empty.", nameof(planId));
            }

            var execution = await LoadTestPlanExecutionAsync(planId, cancellationToken);
            var history = new List<TestPlanExecution>();

            if (execution != null)
            {
                history.Add(execution);
            }

            return history;
        }

        /// <summary>
        /// Resolves the execution file path for a plan ID using default Continue directory.
        /// </summary>
        private string GetExecutionFilePath(string planId)
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continueVS");
            var executionDir = Path.Combine(baseDir, "executions", planId);
            return Path.Combine(executionDir, "execution.json");
        }
    }
}

