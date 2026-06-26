namespace ContinueCore.Indexing;
public enum AddRemoveResultType
{
    // TS value: "add"
    Add,
    // TS value: "remove"
    Remove,
    // TS value: "updateNewVersion"
    UpdateNewVersion,
    // TS value: "updateOldVersion"
    UpdateOldVersion,
    // TS value: "updateLastUpdated"
    UpdateLastUpdated,
    // TS value: "compute"
    Compute
}