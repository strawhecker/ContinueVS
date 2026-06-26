namespace ContinueCore.Util;
public enum ContinueErrorReason
{
    // TS value: "find_and_replace_identical_old_and_new_strings"
    FindAndReplaceIdenticalOldAndNewStrings,
    // TS value: "find_and_replace_missing_old_string"
    FindAndReplaceMissingOldString,
    // TS value: "find_and_replace_non_first_empty_old_string"
    FindAndReplaceNonFirstEmptyOldString,
    // TS value: "find_and_replace_missing_new_string"
    FindAndReplaceMissingNewString,
    // TS value: "find_and_replace_invalid_replace_all"
    FindAndReplaceInvalidReplaceAll,
    // TS value: "find_and_replace_old_string_not_found"
    FindAndReplaceOldStringNotFound,
    // TS value: "find_and_replace_multiple_occurrences"
    FindAndReplaceMultipleOccurrences,
    // TS value: "find_and_replace_missing_filepath"
    FindAndReplaceMissingFilepath,
    // TS value: "multi_edit_edits_array_required"
    MultiEditEditsArrayRequired,
    // TS value: "multi_edit_edits_array_empty"
    MultiEditEditsArrayEmpty,
    // TS value: "multi_edit_subsequent_edits_on_creation"
    MultiEditSubsequentEditsOnCreation,
    // TS value: "multi_edit_empty_old_string_not_first"
    MultiEditEmptyOldStringNotFirst,
    // TS value: "edit_tool_file_not_yet_read"
    EditToolFileNotRead,
    // TS value: "file_already_exists"
    FileAlreadyExists,
    // TS value: "file_not_found"
    FileNotFound,
    // TS value: "file_write_error"
    FileWriteError,
    // TS value: "file_is_security_concern"
    FileIsSecurityConcern,
    // TS value: "parent_directory_not_found"
    ParentDirectoryNotFound,
    // TS value: "file_too_large"
    FileTooLarge,
    // TS value: "path_resolution_failed"
    PathResolutionFailed,
    // TS value: "invalid_line_number"
    InvalidLineNumber,
    // TS value: "directory_not_found"
    DirectoryNotFound,
    // TS value: "command_execution_failed"
    CommandExecutionFailed,
    // TS value: "command_not_available_in_remote"
    CommandNotAvailableInRemote,
    // TS value: "search_execution_failed"
    SearchExecutionFailed,
    // TS value: "rule_not_found"
    RuleNotFound,
    // TS value: "skill_not_found"
    SkillNotFound,
    // TS value: "unspecified"
    Unspecified,
    // TS value: "unknown"
    Unknown
}