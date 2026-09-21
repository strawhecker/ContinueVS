#nullable enable

using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class ToolCallDescriptionBuilderTests
    {
        private static ToolCall Call(string name, IDictionary<string, object>? args = null)
            => new ToolCall { Name = name, Id = "id1", Arguments = args };

        // ---- read_file / view_file / open_file ----

        [Fact]
        public void Build_ReadFile_IncludesPath()
        {
            var call = Call("read_file", new Dictionary<string, object> { ["filepath"] = "src/foo.cs" });
            Assert.Equal("read src/foo.cs", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_ViewFile_UsesViewVerb()
        {
            var call = Call("view_file", new Dictionary<string, object> { ["filepath"] = "src/foo.cs" });
            Assert.Equal("view src/foo.cs", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_ReadFile_WhenPathMissing_ReturnsLeadingVerbAlone()
        {
            var call = Call("read_file");
            Assert.Equal("read ", ToolCallDescriptionBuilder.Build(call));
        }

        // ---- read_file_range ----

        [Fact]
        public void Build_ReadFileRange_IncludesLineRange()
        {
            var call = Call("read_file_range", new Dictionary<string, object>
            {
                ["filepath"] = "src/foo.cs",
                ["startLine"] = 10,
                ["endLine"] = 20
            });
            Assert.Equal("read src/foo.cs lines 10-20", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_ReadFileRange_NoStartEnd_UsesRangeMarker()
        {
            var call = Call("read_file_range", new Dictionary<string, object> { ["filepath"] = "src/foo.cs" });
            Assert.Equal("read src/foo.cs (range)", ToolCallDescriptionBuilder.Build(call));
        }

        // ---- run_terminal_command ----

        [Fact]
        public void Build_RunTerminalCommand_IncludesCommand()
        {
            var call = Call("run_terminal_command", new Dictionary<string, object> { ["command"] = "git status --short" });
            Assert.Equal("run: git status --short", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_RunTerminalCommand_LongCommand_IsTruncated()
        {
            var longCmd = new string('a', 200);
            var call = Call("run_terminal_command", new Dictionary<string, object> { ["command"] = longCmd });
            var result = ToolCallDescriptionBuilder.Build(call);
            Assert.StartsWith("run: ", result);
            // "run: " (5) + 80 truncated chars + ellipsis
            Assert.True(result.Length <= 86, $"Expected <= 86, got {result.Length}");
        }

        // ---- edit ----

        [Fact]
        public void Build_EditFile_IncludesSectionMarker()
        {
            var call = Call("edit_file", new Dictionary<string, object> { ["filepath"] = "src/foo.cs" });
            Assert.Equal("edit src/foo.cs (§ old\u2192new)", ToolCallDescriptionBuilder.Build(call));
        }

        // ---- create ----

        [Fact]
        public void Build_CreateNewFile_IncludesPath()
        {
            var call = Call("create_new_file", new Dictionary<string, object> { ["filepath"] = "src/new.cs" });
            Assert.Equal("create src/new.cs", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_CreateFolder_IncludesDir()
        {
            var call = Call("create_folder", new Dictionary<string, object> { ["folderpath"] = "src/lib" });
            Assert.Equal("create src/lib", ToolCallDescriptionBuilder.Build(call));
        }

        // ---- ls ----

        [Fact]
        public void Build_Ls_IncludesDir_NonRecursive()
        {
            var call = Call("ls", new Dictionary<string, object> { ["dirPath"] = "src" });
            Assert.Equal("list src", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_Ls_IncludesDir_Recursive()
        {
            var call = Call("ls", new Dictionary<string, object> { ["dirPath"] = "src", ["recursive"] = true });
            Assert.Equal("list src (recursive)", ToolCallDescriptionBuilder.Build(call));
        }

        // ---- search / grep / glob ----

        [Fact]
        public void Build_FileGlobSearch_IncludesPattern()
        {
            var call = Call("file_glob_search", new Dictionary<string, object> { ["pattern"] = "**/*.cs" });
            Assert.Equal("glob **/*.cs", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_GrepSearch_IncludesPattern()
        {
            var call = Call("grep_search", new Dictionary<string, object> { ["query"] = "ToolName" });
            Assert.Equal("search for \"ToolName\"", ToolCallDescriptionBuilder.Build(call));
        }

        // ---- git ----

        [Theory]
        [InlineData("git_status", "git status")]
        [InlineData("git_diff", "git diff")]
        [InlineData("git_log", "git log")]
        [InlineData("git_commit", "git commit")]
        [InlineData("view_diff", "view diff")]
        [InlineData("get_problems", "fetch compiler problems")]
        [InlineData("run_pytest", "run tests")]
        public void Build_FixedTools_ReturnStaticLabel(string toolName, string expected)
        {
            Assert.Equal(expected, ToolCallDescriptionBuilder.Build(Call(toolName)));
        }

        // ---- write_plan / ask_user ----

        [Fact]
        public void Build_WritePlan_IncludesTitle()
        {
            var call = Call("write_plan", new Dictionary<string, object> { ["title"] = "Refactor auth" });
            Assert.Equal("write plan: Refactor auth", ToolCallDescriptionBuilder.Build(call));
        }

        [Fact]
        public void Build_AskUser_IncludesQuestion()
        {
            var call = Call("ask_user", new Dictionary<string, object> { ["question"] = "Continue?" });
            Assert.Equal("ask user: Continue?", ToolCallDescriptionBuilder.Build(call));
        }

        // ---- unknown fallback ----

        [Fact]
        public void Build_UnknownTool_ReturnsCompactSnapshot_NoThrow()
        {
            var call = Call("my_custom_tool", new Dictionary<string, object>
            {
                ["alpha"] = "first",
                ["beta"] = 42,
                ["gamma"] = "third",
                ["delta"] = "fourth" // beyond Take(3)
            });
            var result = ToolCallDescriptionBuilder.Build(call);
            Assert.StartsWith("my_custom_tool (", result);
            Assert.Contains("alpha=first", result);
            Assert.Contains("beta=42", result);
            // Only 3 args are taken (Take(3)); "gamma" alphabetically sorts 4th, so absent.
            Assert.DoesNotContain("gamma", result);
        }

        [Fact]
        public void Build_UnknownTool_NoArgs_ReturnsName()
        {
            Assert.Equal("my_custom_tool", ToolCallDescriptionBuilder.Build(Call("my_custom_tool")));
        }

        [Fact]
        public void Build_UnknownTool_LongValue_IsTruncated()
        {
            var longVal = new string('x', 100);
            var call = Call("my_tool", new Dictionary<string, object> { ["p"] = longVal });
            var result = ToolCallDescriptionBuilder.Build(call);
            Assert.Contains("\u2026", result);
        }

        // ---- null guards ----

        [Fact]
        public void Build_NullCall_ReturnsFallback()
        {
            Assert.Equal("tool call", ToolCallDescriptionBuilder.Build((ToolCall)null!));
        }

        [Fact]
        public void Build_NullArguments_DoesNotThrow()
        {
            var result = ToolCallDescriptionBuilder.Build(Call("read_file", null));
            Assert.NotNull(result);
        }

        [Fact]
        public void Build_NullOrEmptyName_ReturnsFallback()
        {
            Assert.Equal("tool call", ToolCallDescriptionBuilder.Build(Call("")));
            Assert.Equal("tool call", ToolCallDescriptionBuilder.Build(Call(null!)));
        }

        // ---- determinism ----

        [Fact]
        public void Build_IsDeterministic_ForSameInput()
        {
            var args = new Dictionary<string, object>
            {
                ["pattern"] = "**/*.cs",
                ["dirPath"] = "src"
            };
            var a = ToolCallDescriptionBuilder.Build(Call("file_glob_search", args));
            var b = ToolCallDescriptionBuilder.Build(Call("file_glob_search", args));
            Assert.Equal(a, b);
        }

        // ---- ChatMessage wiring ----

        [Fact]
        public void ChatMessage_ToolCallDescription_IsJsonIgnored()
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = ChatMessageRole.Tool,
                Content = "some tool output",
                ToolName = "read_file",
                ToolCallDescription = "read src/foo.cs"
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            // Display-only: never serialized onto the wire / into the LLM payload.
            Assert.DoesNotContain(nameof(ChatMessage.ToolCallDescription), json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ChatMessage_ToolCallDescription_RoundTripsInMemory()
        {
            var msg = new ChatMessage { ToolCallDescription = "read src/foo.cs" };
            Assert.Equal("read src/foo.cs", msg.ToolCallDescription);
        }
    }
}
