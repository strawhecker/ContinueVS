using Xunit;

namespace ContinueVS.Tests.Collections
{
    /// <summary>
    /// Collection definition for tests that require Visual Studio Interop assemblies.
    /// These tests may be skipped in environments where Microsoft.VisualStudio.Interop
    /// is not available (e.g., CI/CD without VS SDK installed).
    /// </summary>
    [CollectionDefinition("RequiresVisualStudioInterop", DisableParallelization = true)]
    public class VisualStudioInteropCollection
    {
        // This class has no tests; it's just used to define a collection
    }
}
