namespace ContinueCore.Indexing;
public enum IndexResultType
{
    // TS value: "compute"
    Compute,
    // TS value: "del"
    Delete,
    // TS value: "addTag"
    AddTag,
    // TS value: "removeTag"
    RemoveTag,
    // TS value: "updateLastUpdated"
    UpdateLastUpdated
}