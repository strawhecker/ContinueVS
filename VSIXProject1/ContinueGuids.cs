using System;

namespace ContinueVS
{
    /// <summary>
    /// Shared GUID and command-ID constants referenced by all command handlers.
    /// Values must stay in sync with ContinueCommands.vsct.
    /// </summary>
    internal static class ContinueGuids
    {
        public const string PackageGuidString = "a4507790-4510-4158-b192-87339e7b1a34";
        public const string CmdSetGuidString  = "6b8f0a3c-2d4e-4f1a-9e5b-3c7d8a1f2b0e";

        public static readonly Guid PackageGuid = new Guid(PackageGuidString);
        public static readonly Guid CmdSetGuid  = new Guid(CmdSetGuidString);
    }

    /// <summary>
    /// Integer command IDs — must stay in sync with the IDSymbol values in ContinueCommands.vsct.
    /// </summary>
    internal static class ContinueCommandIds
    {
        public const int ShowContinuePanel    = 0x0100;
        public const int AskContinue          = 0x0101;
        public const int ExplainCode          = 0x0102;
        public const int FixCode              = 0x0103;
        public const int AddComment           = 0x0104;
        public const int ContextAskContinue   = 0x0105;
        public const int ContextExplainCode   = 0x0106;
        public const int ContextFixCode       = 0x0107;
        public const int ContextAddComment    = 0x0108;
    }
}
