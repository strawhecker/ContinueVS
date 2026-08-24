#nullable enable

namespace ContinueVS.Services.Interfaces.Parsers
{
    /// <summary>
    /// Marker interface for .NET Framework stack trace parser.
    /// Specializes in parsing classic .NET Framework exception formats.
    /// </summary>
    public interface IDotNetFrameworkParser : IStackTraceParser
    {
    }
}
