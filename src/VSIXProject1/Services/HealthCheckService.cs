using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Health check service stub. Legacy bridge-based health checks have been removed.
    /// This stub provides no-op implementations for backward compatibility.
    /// </summary>
    public sealed class HealthCheckService
    {
        public HealthCheckService()
        {
        }

        public async Task<bool> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            // No-op health check in modern architecture
            await Task.CompletedTask;
            return true;
        }

        public bool GetCurrentStatus()
        {
            return true;
        }
    }
}
