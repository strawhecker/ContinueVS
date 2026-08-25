using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for processing debug instructions using LLM interpretation.
    /// Converts vague user instructions into ordered internal phases (strategy attempts).
    /// </summary>
    public interface IInstructionProcessorService
    {
        /// <summary>
        /// Generates ordered internal phases from a debug instruction.
        /// Interprets the user's free-text request via LLM and produces a TestPlan.
        /// </summary>
        /// <param name="instruction">The debug instruction to process.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>A TestPlan containing ordered internal phases.</returns>
        /// <exception cref="ArgumentNullException">Thrown if instruction is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if LLM interpretation fails or produces invalid phases.</exception>
        Task<TestPlan> GenerateInternalPhasesAsync(DebugInstruction instruction, CancellationToken cancellationToken = default);
    }
}
