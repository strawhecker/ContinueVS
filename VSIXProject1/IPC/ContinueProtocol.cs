using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace ContinueVS.IPC
{
    // -------------------------------------------------------------------------
    // Wire types — must stay in sync with Continue's core protocol definition.
    // See: https://github.com/continuedev/continue/blob/main/core/protocol/index.ts
    // -------------------------------------------------------------------------

    /// <summary>
    /// The outer envelope for every message exchanged with the continue-binary.
    /// </summary>
    internal sealed class Message
    {
        [JsonProperty("messageType")] public string MessageType { get; set; } = "";
        [JsonProperty("messageId")]   public string MessageId   { get; set; } = "";
        [JsonProperty("data")]        public JToken? Data       { get; set; }
    }

    // -------------------------------------------------------------------------
    // IDE → Core  (requests the extension sends to the binary)
    // -------------------------------------------------------------------------

    internal sealed class FileContents
    {
        [JsonProperty("filepath")] public string Filepath { get; set; } = "";
        [JsonProperty("contents")] public string Contents { get; set; } = "";
    }

    internal sealed class RangeInFile
    {
        [JsonProperty("filepath")]  public string Filepath { get; set; } = "";
        [JsonProperty("range")]     public Range  Range    { get; set; } = new Range();
    }

    internal sealed class Range
    {
        [JsonProperty("start")] public Position Start { get; set; } = new Position();
        [JsonProperty("end")]   public Position End   { get; set; } = new Position();
    }

    internal sealed class Position
    {
        [JsonProperty("line")]      public int Line      { get; set; }
        [JsonProperty("character")] public int Character { get; set; }
    }

    internal sealed class IdeInfo
    {
        [JsonProperty("ideType")]    public string IdeType    { get; set; } = "visualstudio";
        [JsonProperty("name")]       public string Name       { get; set; } = "Visual Studio";
        [JsonProperty("version")]    public string Version    { get; set; } = "17.0";
        [JsonProperty("remoteName")] public string RemoteName { get; set; } = "";
        [JsonProperty("extensionVersion")] public string ExtensionVersion { get; set; } = "1.0.0";
    }

    // -------------------------------------------------------------------------
    // Autocomplete
    // -------------------------------------------------------------------------

    internal sealed class AutocompleteInput
    {
        [JsonProperty("completionId")] public string CompletionId { get; set; } = "";
        [JsonProperty("filepath")]     public string Filepath     { get; set; } = "";
        [JsonProperty("pos")]          public Position Pos        { get; set; } = new Position();
        [JsonProperty("recentlyEditedFiles")] public List<FileContents> RecentlyEditedFiles { get; set; } = new List<FileContents>();
        [JsonProperty("recentlyEditedRanges")] public List<RangeInFile> RecentlyEditedRanges { get; set; } = new List<RangeInFile>();
        [JsonProperty("clipboardText")] public string ClipboardText { get; set; } = "";
    }

    internal sealed class AutocompleteOutcome
    {
        [JsonProperty("accepted")]     public bool    Accepted     { get; set; }
        [JsonProperty("time")]         public double  Time         { get; set; }
        [JsonProperty("completion")]   public string  Completion   { get; set; } = "";
        [JsonProperty("prefix")]       public string  Prefix       { get; set; } = "";
        [JsonProperty("prompt")]       public string  Prompt       { get; set; } = "";
        [JsonProperty("modelProvider")]public string  ModelProvider{ get; set; } = "";
        [JsonProperty("modelName")]    public string  ModelName    { get; set; } = "";
        [JsonProperty("completionId")] public string  CompletionId { get; set; } = "";
        [JsonProperty("numTokens")]    public int     NumTokens    { get; set; }
    }

    // -------------------------------------------------------------------------
    // Chat / context
    // -------------------------------------------------------------------------

    internal sealed class ContextItem
    {
        [JsonProperty("name")]        public string Name        { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("content")]     public string Content     { get; set; } = "";
        [JsonProperty("id")]          public ContextItemId Id   { get; set; } = new ContextItemId();
    }

    internal sealed class ContextItemId
    {
        [JsonProperty("providerTitle")] public string ProviderTitle { get; set; } = "";
        [JsonProperty("itemId")]        public string ItemId        { get; set; } = "";
    }

    // -------------------------------------------------------------------------
    // Apply-to-file diff message
    // -------------------------------------------------------------------------

    internal sealed class ApplyToFileParams
    {
        [JsonProperty("filepath")] public string Filepath { get; set; } = "";
        [JsonProperty("text")]     public string Text     { get; set; } = "";
    }
}
