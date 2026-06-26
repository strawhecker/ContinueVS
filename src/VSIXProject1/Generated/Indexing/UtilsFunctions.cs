namespace ContinueCore.Indexing;
public static partial class UtilsFunctions
{
    public static string tagToString(IndexTag tag)
    {
        var result = $"{tag.directory}::{tag.branch}::{tag.artifactId}";
        if (result.length <= MAX_TABLE_NAME_LENGTH)
        {
            return result;
        }

        var dirHash = crypto.createHash("md5").update(tag.directory).digest("hex").slice(0L, 8L);
        var nonDirLength = $"{dirHash}_::{tag.branch}::{tag.artifactId}".length;
        var maxDirForTruncated = MAX_TABLE_NAME_LENGTH - nonDirLength;
        var truncatedDir = tag.directory.length > maxDirForTruncated ? tag.directory.slice(tag.directory.length - maxDirForTruncated) : tag.directory;
        return $"{dirHash}_{truncatedDir}::{tag.branch}::{tag.artifactId}";
    }
}