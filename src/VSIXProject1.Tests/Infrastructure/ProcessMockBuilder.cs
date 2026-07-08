#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Moq;

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Fluent builder for creating mock Process objects with configured behavior.
    /// 
    /// Simplifies construction of Process mocks for testing process-based operations
    /// (startup, exit codes, I/O streams, signal handling).
    /// 
    /// Usage:
    ///   var mockProcess = new ProcessMockBuilder()
    ///       .WithFileName("node")
    ///       .WithArguments("core-server.js")
    ///       .WithExitCode(0)
    ///       .WithStdoutLine("Server started")
    ///       .Build();
    /// </summary>
    public class ProcessMockBuilder
    {
        private string _fileName = "node";
        private string _arguments = "";
        private int _exitCode = 0;
        private bool _hasExited = false;
        private readonly List<string> _stdoutLines = new List<string>();
        private readonly List<string> _stderrLines = new List<string>();
        private int _id = 1234;
        private bool _enableRaisingEvents = false;

        /// <summary>
        /// Sets the executable file name or path.
        /// </summary>
        public ProcessMockBuilder WithFileName(string fileName)
        {
            _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            return this;
        }

        /// <summary>
        /// Sets the command-line arguments.
        /// </summary>
        public ProcessMockBuilder WithArguments(string arguments)
        {
            _arguments = arguments ?? "";
            return this;
        }

        /// <summary>
        /// Sets the exit code (process completion status).
        /// </summary>
        public ProcessMockBuilder WithExitCode(int exitCode)
        {
            _exitCode = exitCode;
            return this;
        }

        /// <summary>
        /// Marks the process as already exited.
        /// </summary>
        public ProcessMockBuilder WithHasExited(bool hasExited = true)
        {
            _hasExited = hasExited;
            return this;
        }

        /// <summary>
        /// Adds a line to the standard output stream.
        /// </summary>
        public ProcessMockBuilder WithStdoutLine(string line)
        {
            _stdoutLines.Add(line ?? "");
            return this;
        }

        /// <summary>
        /// Adds multiple lines to the standard output stream.
        /// </summary>
        public ProcessMockBuilder WithStdoutLines(params string[] lines)
        {
            foreach (var line in lines)
            {
                _stdoutLines.Add(line ?? "");
            }
            return this;
        }

        /// <summary>
        /// Adds a line to the standard error stream.
        /// </summary>
        public ProcessMockBuilder WithStderrLine(string line)
        {
            _stderrLines.Add(line ?? "");
            return this;
        }

        /// <summary>
        /// Adds multiple lines to the standard error stream.
        /// </summary>
        public ProcessMockBuilder WithStderrLines(params string[] lines)
        {
            foreach (var line in lines)
            {
                _stderrLines.Add(line ?? "");
            }
            return this;
        }

        /// <summary>
        /// Sets the process ID.
        /// </summary>
        public ProcessMockBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        /// <summary>
        /// Sets whether the process should raise events (e.g., Exited).
        /// </summary>
        public ProcessMockBuilder WithEnableRaisingEvents(bool enable = true)
        {
            _enableRaisingEvents = enable;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured Mock&lt;Process&gt; instance.
        /// </summary>
        public Mock<Process> Build()
        {
            var mock = new Mock<Process>();

            // File name and arguments
            mock.Setup(p => p.StartInfo.FileName)
                .Returns(_fileName);

            mock.Setup(p => p.StartInfo.Arguments)
                .Returns(_arguments);

            // Exit code
            mock.Setup(p => p.ExitCode)
                .Returns(_exitCode);

            // HasExited
            mock.Setup(p => p.HasExited)
                .Returns(_hasExited);

            // Process ID
            mock.Setup(p => p.Id)
                .Returns(_id);

            // EnableRaisingEvents
            mock.SetupProperty(p => p.EnableRaisingEvents, _enableRaisingEvents);

            // Standard streams (read-only for output)
            var stdoutReader = new StringReader(string.Join(Environment.NewLine, _stdoutLines));
            var stderrReader = new StringReader(string.Join(Environment.NewLine, _stderrLines));

            mock.Setup(p => p.StandardOutput)
                .Returns(new StreamReader(new MemoryStream(
                    Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, _stdoutLines)))));

            mock.Setup(p => p.StandardError)
                .Returns(new StreamReader(new MemoryStream(
                    Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, _stderrLines)))));

            // Standard input (writable)
            mock.Setup(p => p.StandardInput)
                .Returns(new StreamWriter(new MemoryStream()));

            // Start method (no-op)
            mock.Setup(p => p.Start())
                .Returns(true);

            // Kill method (no-op)
            mock.Setup(p => p.Kill())
                .Callback(() => mock.Setup(p => p.HasExited).Returns(true));

            // WaitForExit
            mock.Setup(p => p.WaitForExit(It.IsAny<int>()))
                .Returns(_hasExited);

            mock.Setup(p => p.WaitForExit())
                .Callback(() => { /* No-op */ });

            // Close
            mock.Setup(p => p.Close())
                .Callback(() => mock.Setup(p => p.HasExited).Returns(true));

            // Dispose
            mock.Setup(p => p.Dispose())
                .Callback(() => { /* No-op */ });

            return mock;
        }

        /// <summary>
        /// Builds a mock Process that simulates a successfully running server.
        /// </summary>
        public static Mock<Process> CreateRunningServerMock()
        {
            return new ProcessMockBuilder()
                .WithFileName("node")
                .WithArguments("core-server.js")
                .WithExitCode(0)
                .WithHasExited(false)
                .WithStdoutLine("Server started")
                .WithId(5678)
                .Build();
        }

        /// <summary>
        /// Builds a mock Process that simulates a failed startup.
        /// </summary>
        public static Mock<Process> CreateFailedStartupMock()
        {
            return new ProcessMockBuilder()
                .WithFileName("node")
                .WithArguments("core-server.js")
                .WithExitCode(1)
                .WithHasExited(true)
                .WithStderrLine("Error: Module not found")
                .Build();
        }

        /// <summary>
        /// Builds a mock Process that simulates a normal exit.
        /// </summary>
        public static Mock<Process> CreateNormalExitMock()
        {
            return new ProcessMockBuilder()
                .WithFileName("node")
                .WithArguments("core-server.js")
                .WithExitCode(0)
                .WithHasExited(true)
                .WithStdoutLine("Server shut down gracefully")
                .Build();
        }
    }
}
