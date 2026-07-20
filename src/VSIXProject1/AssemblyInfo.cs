using System.Runtime.CompilerServices;

// Allow test assembly to access internal types
[assembly: InternalsVisibleTo("VSIXProject1.Tests")]
// Also allow tests by ContinueVS namespace
[assembly: InternalsVisibleTo("ContinueVS.Tests")]
// Allow Moq dynamic proxy factory to access internal types
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

