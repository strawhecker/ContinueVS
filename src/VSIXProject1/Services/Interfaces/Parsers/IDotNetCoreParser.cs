#nullable enable

namespace ContinueVS.Services.Interfaces.Parsers
{
    /// <summary>
    /// Marker interface for .NET Core stack trace parser.
    /// Specializes in parsing modern .NET Core exception formats with async context support.
    /// </summary>
    public interface IDotNetCoreParser : IStackTraceParser
    {
    }
}
