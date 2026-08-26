namespace ContinueVS.Core.Enums
{
    /// <summary>
    /// Categorizes the type of error encountered during build, test, or execution.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// C# or project compilation error.
        /// </summary>
        Compilation,

        /// <summary>
        /// Unit test failure (assertion, exception, or timeout).
        /// </summary>
        TestFailure,

        /// <summary>
        /// Unhandled exception or runtime error.
        /// </summary>
        Exception,

        /// <summary>
        /// Unknown or unparseable error.
        /// </summary>
        Unknown
    }
}
