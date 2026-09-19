using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Base tagged delta vector for the append-only JSONL session log (gap83).
    /// Each line in the log is a single JSON object whose first-class "type"
    /// property discriminates how the reader should deserialize and apply it.
    /// <paramref name="SessionId"/> routes the line to the correct session file.
    /// </summary>
    public abstract class SessionDelta
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string? SessionId { get; set; }
    }

    /// <summary>
    /// Session header delta. Carries id/title/createdAt/mode so a session can be
    /// reconstructed from the log. Multiple init lines are allowed; replay keeps
    /// the LAST one (so a mid-stream mode change persists without rewriting).
    /// </summary>
    public class SessionDeltaInit : SessionDelta
    {
        public SessionDeltaInit()
        {
            Type = "init";
        }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("createdAt")]
        public System.DateTime? CreatedAt { get; set; }

        [JsonProperty("mode")]
        public int Mode { get; set; }
    }

    /// <summary>
    /// Append a new message. The message is a full ChatMessage JSON object.
    /// </summary>
    public class SessionDeltaAdd : SessionDelta
    {
        public SessionDeltaAdd()
        {
            Type = "add";
        }

        [JsonProperty("messageId")]
        public string? MessageId { get; set; }

        [JsonProperty("message")]
        public ChatMessage? Message { get; set; }
    }

    /// <summary>
    /// Update an existing message in place. Carries a full message payload with the
    /// same id; the reader copies its mutable fields onto the existing instance so
    /// the O(1) dictionary reference (and INotifyPropertyChanged) is preserved.
    /// </summary>
    public class SessionDeltaUpdate : SessionDelta
    {
        public SessionDeltaUpdate()
        {
            Type = "update";
        }

        [JsonProperty("messageId")]
        public string? MessageId { get; set; }

        [JsonProperty("message")]
        public ChatMessage? Message { get; set; }
    }

    /// <summary>
    /// Hard-delete a message from the live session view. The line stays in the log
    /// (append-only, bytes never erased) but the message is removed from index+list.
    /// </summary>
    public class SessionDeltaDelete : SessionDelta
    {
        public SessionDeltaDelete()
        {
            Type = "delete";
        }

        [JsonProperty("messageId")]
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Soft-delete (tombstone) a message: sets IsDeleted=true, retains bytes so the
    /// user can undelete (gap80/gap81). The entry stays in the LLM-excluded view.
    /// </summary>
    public class SessionDeltaSoftDelete : SessionDelta
    {
        public SessionDeltaSoftDelete()
        {
            Type = "softDelete";
        }

        [JsonProperty("messageId")]
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Clear a tombstone: sets IsDeleted=false (gap81 undelete).
    /// </summary>
    public class SessionDeltaUndelete : SessionDelta
    {
        public SessionDeltaUndelete()
        {
            Type = "undelete";
        }

        [JsonProperty("messageId")]
        public string? MessageId { get; set; }
    }
}
