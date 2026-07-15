using ContinueVS.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// File-System Collector Test Suite (Step 83)
    ///
    /// Tests:
    /// - All 6 operations (read, write, delete, list, stats, mkdir)
    /// - Error handling (access denied, not found, encoding)
    /// - Security (path traversal, boundary enforcement)
    /// - Performance assertions
    /// - Edge cases (large files, special characters, empty directories)
    /// </summary>
    public class FileSystemCollectorTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly FileSystemCollector _collector;

        public FileSystemCollectorTests()
        {
            // Create temporary test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), $"FileSystemCollectorTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);

            // Initialize collector with test workspace
            _collector = new FileSystemCollector(_testDirectory);
        }

        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        private string GetTestPath(string relativePath)
        {
            return Path.Combine(_testDirectory, relativePath);
        }

        // ====================================================================
        // SUITE 1: READ OPERATIONS
        // ====================================================================

        [Fact]
        public async Task ReadFileAsync_WithValidUtf8File_ReturnsContent()
        {
            // Arrange
            var testPath = GetTestPath("test.txt");
            var content = "Hello, World!";
            File.WriteAllText(testPath, content);

            // Act
            var (readContent, encoding, size) = await _collector.ReadFileAsync(testPath);

            // Assert
            Assert.Equal(content, readContent);
            Assert.Equal("utf-8", encoding);
            Assert.Equal(content.Length, size);
        }

        [Fact]
        public async Task ReadFileAsync_WithMissingFile_ThrowsFileAccessException()
        {
            // Arrange
            var testPath = GetTestPath("nonexistent.txt");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<FileAccessException>(
                () => _collector.ReadFileAsync(testPath)
            );
            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ReadFileAsync_WithLargeFile_ReturnsContent()
        {
            // Arrange
            var testPath = GetTestPath("large.txt");
            var content = new string('x', 10 * 1024 * 1024); // 10MB
            File.WriteAllText(testPath, content);

            // Act
            var start = DateTime.UtcNow;
            var (readContent, _, size) = await _collector.ReadFileAsync(testPath);
            var duration = DateTime.UtcNow - start;

            // Assert
            Assert.Equal(content.Length, readContent.Length);
            Assert.True(duration.TotalMilliseconds < 5000, "Read should complete in <5 seconds");
        }

        [Fact]
        public async Task ReadFileAsync_WithSpecialCharacters_ReturnsContent()
        {
            // Arrange
            var testPath = GetTestPath("file with spaces & special.txt");
            var content = "Special content!";
            File.WriteAllText(testPath, content);

            // Act
            var (readContent, _, _) = await _collector.ReadFileAsync(testPath);

            // Assert
            Assert.Equal(content, readContent);
        }

        // ====================================================================
        // SUITE 2: WRITE OPERATIONS
        // ====================================================================

        [Fact]
        public async Task WriteFileAsync_WithNewFile_CreatesFile()
        {
            // Arrange
            var testPath = GetTestPath("new.txt");
            var content = "New content";

            // Act
            var (_, bytesWritten) = await _collector.WriteFileAsync(testPath, content);

            // Assert
            Assert.True(File.Exists(testPath));
            Assert.Equal(content, File.ReadAllText(testPath));
            Assert.Equal(content.Length, bytesWritten);
        }

        [Fact]
        public async Task WriteFileAsync_WithExistingFile_OverwritesFile()
        {
            // Arrange
            var testPath = GetTestPath("overwrite.txt");
            File.WriteAllText(testPath, "Old content");
            var newContent = "New content";

            // Act
            await _collector.WriteFileAsync(testPath, newContent);

            // Assert
            Assert.Equal(newContent, File.ReadAllText(testPath));
        }

        [Fact]
        public async Task WriteFileAsync_WithNestedPath_CreatesParentDirectories()
        {
            // Arrange
            var testPath = GetTestPath("deep/nested/dir/file.txt");
            var content = "Nested content";

            // Act
            await _collector.WriteFileAsync(testPath, content);

            // Assert
            Assert.True(File.Exists(testPath));
            Assert.Equal(content, File.ReadAllText(testPath));
        }

        // ====================================================================
        // SUITE 3: DELETE OPERATIONS
        // ====================================================================

        [Fact]
        public async Task DeleteFileAsync_WithExistingFile_DeletesFile()
        {
            // Arrange
            var testPath = GetTestPath("delete.txt");
            File.WriteAllText(testPath, "content");

            // Act
            var deleted = await _collector.DeleteFileAsync(testPath);

            // Assert
            Assert.True(deleted);
            Assert.False(File.Exists(testPath));
        }

        [Fact]
        public async Task DeleteFileAsync_WithMissingFile_ReturnsFalse()
        {
            // Arrange
            var testPath = GetTestPath("nonexistent.txt");

            // Act
            var deleted = await _collector.DeleteFileAsync(testPath);

            // Assert
            Assert.False(deleted);
        }

        // ====================================================================
        // SUITE 4: DIRECTORY OPERATIONS
        // ====================================================================

        [Fact]
        public async Task ListDirectoryAsync_WithFiles_ReturnsFileList()
        {
            // Arrange
            var testDir = GetTestPath("mydir");
            Directory.CreateDirectory(testDir);
            File.WriteAllText(Path.Combine(testDir, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(testDir, "file2.txt"), "content2");

            // Act
            var entries = await _collector.ListDirectoryAsync(testDir);
            var fileList = entries.ToList();

            // Assert
            Assert.Equal(2, fileList.Count);
            Assert.All(fileList, e => Assert.Equal("file", e.type));
        }

        [Fact]
        public async Task ListDirectoryAsync_WithEmptyDirectory_ReturnsEmptyList()
        {
            // Arrange
            var testDir = GetTestPath("emptydir");
            Directory.CreateDirectory(testDir);

            // Act
            var entries = await _collector.ListDirectoryAsync(testDir);

            // Assert
            Assert.Empty(entries);
        }

        [Fact]
        public async Task ListDirectoryAsync_WithMissingDirectory_ThrowsException()
        {
            // Arrange
            var testDir = GetTestPath("nonexistent");

            // Act & Assert
            await Assert.ThrowsAsync<FileAccessException>(
                () => _collector.ListDirectoryAsync(testDir)
            );
        }

        [Fact]
        public async Task CreateDirectoryAsync_WithNewPath_CreatesDirectory()
        {
            // Arrange
            var testDir = GetTestPath("newdir");

            // Act
            var (_, created) = await _collector.CreateDirectoryAsync(testDir);

            // Assert
            Assert.True(created);
            Assert.True(Directory.Exists(testDir));
        }

        [Fact]
        public async Task CreateDirectoryAsync_WithNestedPath_CreatesWithParents()
        {
            // Arrange
            var testDir = GetTestPath("deep/nested/dir");

            // Act
            var (_, created) = await _collector.CreateDirectoryAsync(testDir, createParents: true);

            // Assert
            Assert.True(created);
            Assert.True(Directory.Exists(testDir));
        }

        [Fact]
        public async Task CreateDirectoryAsync_WithExistingDirectory_ReturnsFalse()
        {
            // Arrange
            var testDir = GetTestPath("existing");
            Directory.CreateDirectory(testDir);

            // Act
            var (_, created) = await _collector.CreateDirectoryAsync(testDir);

            // Assert
            Assert.False(created);
        }

        // ====================================================================
        // SUITE 5: STATS OPERATIONS
        // ====================================================================

        [Fact]
        public async Task GetFileStatsAsync_WithFile_ReturnsFileMetadata()
        {
            // Arrange
            var testPath = GetTestPath("stats.txt");
            File.WriteAllText(testPath, "content");

            // Act
            var (size, type, mtime, exists) = await _collector.GetFileStatsAsync(testPath);

            // Assert
            Assert.True(exists);
            Assert.Equal("file", type);
            Assert.Equal(7, size);
            Assert.NotNull(mtime);
        }

        [Fact]
        public async Task GetFileStatsAsync_WithDirectory_ReturnsDirectoryMetadata()
        {
            // Arrange
            var testDir = GetTestPath("statsdir");
            Directory.CreateDirectory(testDir);

            // Act
            var (size, type, mtime, exists) = await _collector.GetFileStatsAsync(testDir);

            // Assert
            Assert.True(exists);
            Assert.Equal("directory", type);
        }

        [Fact]
        public async Task GetFileStatsAsync_WithMissingPath_ReturnsFalseForExists()
        {
            // Arrange
            var testPath = GetTestPath("nonexistent");

            // Act
            var (size, type, mtime, exists) = await _collector.GetFileStatsAsync(testPath);

            // Assert
            Assert.False(exists);
            Assert.Null(type);
        }

        // ====================================================================
        // SUITE 6: SECURITY & ERROR HANDLING
        // ====================================================================

        [Fact]
        public async Task ValidatePath_WithTraversalAttempt_ThrowsPathSecurityException()
        {
            // Arrange
            var maliciousPath = Path.Combine(_testDirectory, "..", "..", "etc", "passwd");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<PathSecurityException>(
                () => _collector.ReadFileAsync(maliciousPath)
            );
            Assert.NotNull(ex);
        }

        [Fact]
        public async Task ValidatePath_WithNullBytes_ThrowsPathSecurityException()
        {
            // Arrange
            var badPath = _testDirectory + "\0malicious";

            // Act & Assert
            await Assert.ThrowsAsync<PathSecurityException>(
                () => _collector.ReadFileAsync(badPath)
            );
        }

        [Fact]
        public async Task ValidatePath_WithEmptyPath_ThrowsPathSecurityException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<PathSecurityException>(
                () => _collector.ReadFileAsync("")
            );
        }

        [Fact]
        public async Task CreateDirectoryAsync_WithDepthExceeded_ThrowsPathSecurityException()
        {
            // Arrange
            var deepPath = GetTestPath(string.Join("/", Enumerable.Repeat("a", 51)));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<PathSecurityException>(
                () => _collector.CreateDirectoryAsync(deepPath, createParents: true)
            );
            Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Operations_WithConcurrentAccess_AllSucceed()
        {
            // Arrange
            var paths = new[] { "file1.txt", "file2.txt", "file3.txt" };
            foreach (var p in paths)
            {
                File.WriteAllText(GetTestPath(p), "initial");
            }

            // Act
            var tasks = paths.Select(p => _collector.ReadFileAsync(GetTestPath(p))).ToArray();
            await Task.WhenAll(tasks);

            // Assert
            Assert.All(tasks, t => Assert.Equal("initial", t.Result.content));
        }
    }
}
