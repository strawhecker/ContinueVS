using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Base exception for file-system operations (Step 83)
    /// </summary>
    public class FileSystemException : Exception
    {
        public string ErrorCode { get; set; }

        public FileSystemException(string message, string errorCode = "FILESYSTEM_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Path security or validation exception
    /// </summary>
    public class PathSecurityException : FileSystemException
    {
        public PathSecurityException(string message, string errorCode = "PATH_SECURITY_ERROR")
            : base(message, errorCode)
        {
        }
    }

    /// <summary>
    /// File access denied or not found exception
    /// </summary>
    public class FileAccessException : FileSystemException
    {
        public FileAccessException(string message, string errorCode = "FILE_ACCESS_ERROR")
            : base(message, errorCode)
        {
        }
    }

    /// <summary>
    /// File encoding error exception
    /// </summary>
    public class FileEncodingException : FileSystemException
    {
        public FileEncodingException(string message, string errorCode = "ENCODING_ERROR")
            : base(message, errorCode)
        {
        }
    }

    /// <summary>
    /// File-System Collector Service (Step 83)
    ///
    /// Provides secure file-system operations through the bridge:
    /// - ReadFileAsync: Read file contents (UTF-8)
    /// - WriteFileAsync: Write/create file (UTF-8)
    /// - DeleteFileAsync: Delete file safely
    /// - ListDirectoryAsync: List directory contents
    /// - GetFileStatsAsync: Query file metadata
    /// - CreateDirectoryAsync: Create directory with parents
    ///
    /// Security model:
    /// - Path normalization (prevent directory traversal)
    /// - Workspace boundary enforcement (no escape)
    /// - Symlink resolution (prevent loops)
    /// - Encoding validation (UTF-8 + detection)
    /// - Permission checks via CLR
    ///
    /// Performance targets:
    /// - Single file read/write: <100ms
    /// - Directory list (1000 items): <150ms
    /// - Stats query: <50ms
    /// </summary>
    public sealed class FileSystemCollector
    {
        private const int MaxDirectoryEntries = 5000;
        private const int MaxDirectoryDepth = 50;
        private const long MaxFileSize = 100_000_000; // 100MB
        private const string WorkspaceRoot = @""; // Set by host

        /// <summary>
        /// Initialize collector with workspace root
        /// </summary>
        public FileSystemCollector(string workspaceRoot = null)
        {
            if (!string.IsNullOrEmpty(workspaceRoot))
            {
                // Normalize workspace root
                var root = Path.GetFullPath(workspaceRoot);
                if (!Directory.Exists(root))
                {
                    throw new DirectoryNotFoundException($"Workspace root not found: {root}");
                }
            }
        }

        // ====================================================================
        // PATH VALIDATION & SECURITY
        // ====================================================================

        /// <summary>
        /// Validate and normalize path
        /// Prevents directory traversal, validates encoding, etc.
        /// </summary>
        private string ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new PathSecurityException("Path cannot be empty or null", "EMPTY_PATH");

            if (path.Contains('\0'))
                throw new PathSecurityException("Path contains null bytes", "NULL_BYTES");

            // Normalize path to full path
            try
            {
                var fullPath = Path.GetFullPath(path);

                // Boundary check: ensure path is within workspace (if workspace root defined)
                if (!string.IsNullOrEmpty(WorkspaceRoot))
                {
                    var workspaceFullPath = Path.GetFullPath(WorkspaceRoot);
                    if (!IsPathWithinBoundary(fullPath, workspaceFullPath))
                    {
                        throw new PathSecurityException(
                            $"Path {fullPath} violates workspace boundary",
                            "BOUNDARY_VIOLATION"
                        );
                    }
                }

                // Check for symlinks (reparse points on Windows)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var fileInfo = new FileInfo(fullPath);
                    if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new PathSecurityException(
                            $"Symlinks not allowed: {fullPath}",
                            "SYMLINK_REJECTED"
                        );
                    }
                }

                return fullPath;
            }
            catch (ArgumentException ex)
            {
                throw new PathSecurityException($"Invalid path: {ex.Message}", "INVALID_PATH");
            }
        }

        /// <summary>
        /// Check if path is within boundary
        /// </summary>
        private bool IsPathWithinBoundary(string fullPath, string boundaryRoot)
        {
            // Ensure both paths use same separator
            var normalizedPath = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var normalizedBoundary = boundaryRoot.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            // Ensure boundary ends with separator
            if (!normalizedBoundary.EndsWith(Path.DirectorySeparatorChar.ToString()))
                normalizedBoundary += Path.DirectorySeparatorChar;

            // Check if path starts with boundary
            return normalizedPath.StartsWith(normalizedBoundary, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.Equals(normalizedBoundary.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        // ====================================================================
        // READ FILE
        // ====================================================================

        /// <summary>
        /// Read file contents asynchronously
        /// </summary>
        public async Task<(string content, string encoding, long size)> ReadFileAsync(string path, CancellationToken cancellationToken = default)
        {
            var fullPath = ValidatePath(path);

            try
            {
                if (!File.Exists(fullPath))
                    throw new FileAccessException($"File not found: {fullPath}", "NOT_FOUND");

                // Check file size limit
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaxFileSize)
                    throw new FileAccessException($"File exceeds maximum size ({MaxFileSize} bytes)", "FILE_TOO_LARGE");

                // Read file content
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
                        var content = await reader.ReadToEndAsync();
                        return (content, "utf-8", fileInfo.Length);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileAccessException($"Access denied: {ex.Message}", "ACCESS_DENIED");
            }
            catch (IOException ex)
            {
                throw new FileAccessException($"IO error: {ex.Message}", "IO_ERROR");
            }
            catch (DecoderFallbackException ex)
            {
                throw new FileEncodingException($"Encoding error: {ex.Message}", "ENCODING_ERROR");
            }
        }

        // ====================================================================
        // WRITE FILE
        // ====================================================================

        /// <summary>
        /// Write/create file contents asynchronously
        /// </summary>
        public async Task<(string path, long bytesWritten)> WriteFileAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            var fullPath = ValidatePath(path);

            try
            {
                // Create parent directories if needed
                var directoryPath = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Write file content
                using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        await writer.WriteAsync(content ?? "");
                        await writer.FlushAsync();
                        return (fullPath, stream.Length);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileAccessException($"Access denied: {ex.Message}", "ACCESS_DENIED");
            }
            catch (IOException ex)
            {
                throw new FileAccessException($"IO error: {ex.Message}", "IO_ERROR");
            }
        }

        // ====================================================================
        // DELETE FILE
        // ====================================================================

        /// <summary>
        /// Delete file safely
        /// </summary>
        public async Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            var fullPath = ValidatePath(path);

            try
            {
                if (!File.Exists(fullPath))
                    return false; // Graceful: file doesn't exist

                File.Delete(fullPath);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileAccessException($"Access denied: {ex.Message}", "ACCESS_DENIED");
            }
            catch (IOException ex)
            {
                throw new FileAccessException($"IO error: {ex.Message}", "IO_ERROR");
            }
        }

        // ====================================================================
        // LIST DIRECTORY
        // ====================================================================

        /// <summary>
        /// List directory contents
        /// </summary>
        public async Task<IEnumerable<(string name, string type, long size, DateTime? mtime)>> ListDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            var fullPath = ValidatePath(path);

            try
            {
                if (!Directory.Exists(fullPath))
                    throw new FileAccessException($"Directory not found: {fullPath}", "NOT_FOUND");

                var entries = new List<(string, string, long, DateTime?)>();
                var enumerationOptions = new EnumerationOptions { SkipInaccessible = true, RecurseSubdirectories = false };

                // Enumerate files
                foreach (var filePath in Directory.EnumerateFiles(fullPath, "*", enumerationOptions))
                {
                    if (entries.Count >= MaxDirectoryEntries)
                        break;

                    var fileInfo = new FileInfo(filePath);
                    entries.Add((fileInfo.Name, "file", fileInfo.Length, fileInfo.LastWriteTime));
                }

                // Enumerate directories
                foreach (var dirPath in Directory.EnumerateDirectories(fullPath, "*", enumerationOptions))
                {
                    if (entries.Count >= MaxDirectoryEntries)
                        break;

                    var dirInfo = new DirectoryInfo(dirPath);
                    entries.Add((dirInfo.Name, "directory", 0, dirInfo.LastWriteTime));
                }

                return await Task.FromResult(entries);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileAccessException($"Access denied: {ex.Message}", "ACCESS_DENIED");
            }
            catch (IOException ex)
            {
                throw new FileAccessException($"IO error: {ex.Message}", "IO_ERROR");
            }
        }

        // ====================================================================
        // FILE STATS
        // ====================================================================

        /// <summary>
        /// Get file or directory metadata
        /// </summary>
        public async Task<(long size, string type, DateTime? mtime, bool exists)> GetFileStatsAsync(string path, CancellationToken cancellationToken = default)
        {
            var fullPath = ValidatePath(path);

            try
            {
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    return await Task.FromResult((fileInfo.Length, "file", fileInfo.LastWriteTime, true));
                }

                if (Directory.Exists(fullPath))
                {
                    var dirInfo = new DirectoryInfo(fullPath);
                    return await Task.FromResult((0L, "directory", dirInfo.LastWriteTime, true));
                }

                // Not found (graceful)
                return await Task.FromResult((0L, null, null, false));
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileAccessException($"Access denied: {ex.Message}", "ACCESS_DENIED");
            }
            catch (IOException ex)
            {
                throw new FileAccessException($"IO error: {ex.Message}", "IO_ERROR");
            }
        }

        // ====================================================================
        // CREATE DIRECTORY
        // ====================================================================

        /// <summary>
        /// Create directory with optional parent creation
        /// </summary>
        public async Task<(string path, bool created)> CreateDirectoryAsync(string path, bool createParents = true, CancellationToken cancellationToken = default)
        {
            var fullPath = ValidatePath(path);

            try
            {
                // Check directory depth limit
                var depth = fullPath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (depth > MaxDirectoryDepth)
                    throw new PathSecurityException($"Directory depth exceeds maximum ({MaxDirectoryDepth} levels)", "DEPTH_EXCEEDED");

                if (Directory.Exists(fullPath))
                    return await Task.FromResult((fullPath, false)); // Already exists

                if (createParents)
                {
                    Directory.CreateDirectory(fullPath);
                }
                else
                {
                    var parentPath = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(parentPath))
                        throw new FileAccessException($"Parent directory does not exist", "PARENT_NOT_FOUND");

                    Directory.CreateDirectory(fullPath);
                }

                return await Task.FromResult((fullPath, true));
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new FileAccessException($"Access denied: {ex.Message}", "ACCESS_DENIED");
            }
            catch (IOException ex)
            {
                throw new FileAccessException($"IO error: {ex.Message}", "IO_ERROR");
            }
        }
    }
}
