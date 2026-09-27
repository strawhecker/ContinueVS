using System.Linq;
using ContinueVS.Core.Types;
using Xunit;

namespace ContinueVS.Tests.Core.Types
{
    /// <summary>
    /// Enforces the ToolAccess invariants: every registered built-in tool must be classified
    /// (fail-closed — nothing reaches the roster without explicit list membership), and known
    /// mutating/command tools must never be classified as Read.
    /// </summary>
    public class ToolAccessTests
    {
        [Fact]
        public void EveryBuiltInTool_IsClassified_FailClosed()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();

            Assert.NotEmpty(tools);
            foreach (var tool in tools)
            {
                Assert.True(
                    ToolAccess.LevelFor(tool.Name) != ToolAccessLevel.None,
                    $"Tool '{tool.Name}' is not classified in ToolAccess. Add it to Read, Write, or Debug " +
                    "so it is not silently hidden from every mode.");
            }
        }

        [Theory]
        [InlineData("run_terminal_command")]
        [InlineData("create_new_file")]
        [InlineData("create_folder")]
        [InlineData("edit_file")]
        [InlineData("single_find_and_replace")]
        [InlineData("ide_build")]
        [InlineData("git_commit")]
        public void KnownMutators_NeverClassifiedAsRead(string toolName)
        {
            // A mutating/command tool in Read would leak arbitrary effects into read-only modes.
            Assert.NotEqual(ToolAccessLevel.Read, ToolAccess.LevelFor(toolName));
        }

        [Fact]
        public void UnclassifiedTool_NotAvailableInAnyMode()
        {
            foreach (var mode in new[] { ChatMode.Ask, ChatMode.Bare, ChatMode.Plan, ChatMode.Reason, ChatMode.Agent, ChatMode.Debug })
            {
                Assert.False(ToolAccess.IsAvailableInMode("not_a_real_tool", mode), "Unclassified tool leaked into a mode");
            }
        }

        [Fact]
        public void RunTerminalCommand_ExcludedFromAllReadOnlyModes()
        {
            foreach (var mode in new[] { ChatMode.Ask, ChatMode.Bare, ChatMode.Plan, ChatMode.Reason })
            {
                Assert.False(
                    ToolAccess.IsAvailableInMode("run_terminal_command", mode),
                    $"run_terminal_command must not be available in read-only mode {mode}");
            }
        }

        [Fact]
        public void WritePlanAndUpdatePlan_AreReadAccess()
        {
            // Blast-radius principle: bounded writes to the user-bound plan artifact, no path control.
            Assert.Equal(ToolAccessLevel.Read, ToolAccess.LevelFor("write_plan"));
            Assert.Equal(ToolAccessLevel.Read, ToolAccess.LevelFor("update_plan"));
            Assert.Equal(ToolAccessLevel.Read, ToolAccess.LevelFor("read_plan"));
        }
    }
}
