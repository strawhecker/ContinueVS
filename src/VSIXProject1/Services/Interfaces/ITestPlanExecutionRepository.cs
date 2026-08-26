using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Repository interface for persisting and retrieving TestPlanExecution records.
    /// Enables separate storage of execution history from immutable TestPlan definitions.
    /// </summary>
    public interface ITestPlanExecutionRepository
    {
        /// <summary>
        /// Saves a TestPlanExecution record to persistent storage.
        /// Execution history is stored separately from the TestPlan definition.
        /// </summary>
        /// <param name="execution">The execution record to save.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the asynchronous save operation.</returns>
        Task SaveTestPlanExecutionAsync(TestPlanExecution execution, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the latest TestPlanExecution record for a given plan ID.
        /// </summary>
        /// <param name="planId">The TestPlan ID to load execution history for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The TestPlanExecution record, or null if not found.</returns>
        Task<TestPlanExecution?> LoadTestPlanExecutionAsync(string planId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all execution history records for a given plan ID.
        /// Enables replaying and comparing multiple executions of the same plan.
        /// </summary>
        /// <param name="planId">The TestPlan ID to retrieve execution history for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of TestPlanExecution records (oldest first). Empty list if none found.</returns>
        Task<List<TestPlanExecution>> GetExecutionHistoryAsync(string planId, CancellationToken cancellationToken = default);
    }
}
