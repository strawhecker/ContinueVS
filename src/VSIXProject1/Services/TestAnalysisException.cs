using System;

namespace ContinueVS.Services
{
    /// <summary>
    /// Exception thrown when test failure analysis exceeds maximum iterations (5).
    /// </summary>
    public class TestAnalysisException : Exception
    {
        public int IterationCount { get; }

        public TestAnalysisException(string message, int iterationCount)
            : base(message)
        {
            IterationCount = iterationCount;
        }

        public TestAnalysisException(string message, int iterationCount, Exception innerException)
            : base(message, innerException)
        {
            IterationCount = iterationCount;
        }
    }
}
