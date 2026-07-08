#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Abstract base class for unit tests providing shared lifecycle management,
    /// mock creation, and custom assertions.
    /// 
    /// Subclasses should:
    /// - Inherit from this base and call base.Dispose() in their cleanup
    /// - Use CreateMock&lt;T&gt;() for consistent mock creation
    /// - Use assertion helpers for bridge-specific validations
    /// </summary>
    public abstract class TestFixtureBase : IDisposable
    {
        /// <summary>
        /// Registry of mocks created by this fixture for unified cleanup.
        /// </summary>
        private readonly List<object> _mocks = new List<object>();

        /// <summary>
        /// Flag to track disposal state and prevent double-dispose.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Creates a new Moq Mock&lt;T&gt; and registers it for cleanup.
        /// </summary>
        /// <typeparam name="T">The type to mock (typically an interface)</typeparam>
        /// <returns>A configured Mock&lt;T&gt; instance</returns>
        protected Mock<T> CreateMock<T>() where T : class
        {
            var mock = new Mock<T>(MockBehavior.Strict);
            _mocks.Add(mock);
            return mock;
        }

        /// <summary>
        /// Creates a new Moq Mock&lt;T&gt; with loose behavior and registers it for cleanup.
        /// </summary>
        /// <typeparam name="T">The type to mock (typically an interface)</typeparam>
        /// <returns>A configured Mock&lt;T&gt; instance with Default behavior</returns>
        protected Mock<T> CreateLooseMock<T>() where T : class
        {
            var mock = new Mock<T>(MockBehavior.Default);
            _mocks.Add(mock);
            return mock;
        }

        /// <summary>
        /// Checks if npm is available in the current environment.
        /// </summary>
        /// <returns>true if npm --version succeeds; false otherwise</returns>
        protected bool IsNpmAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo("npm", "--version")
                {
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(1000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Asserts that an action throws an exception of the expected type
        /// and that the exception message contains the expected substring.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="expectedExceptionType">The expected exception type</param>
        /// <param name="expectedMessageSubstring">A substring expected in the exception message</param>
        protected void AssertThrowsAndMessageContains(
            Action action,
            Type expectedExceptionType,
            string expectedMessageSubstring)
        {
            var ex = Assert.Throws(expectedExceptionType, action);
            Assert.NotNull(ex);
            Assert.Contains(expectedMessageSubstring, ex.Message ?? string.Empty);
        }

        /// <summary>
        /// Asserts that an async action throws an exception of the expected type
        /// and that the exception message contains the expected substring.
        /// </summary>
        /// <param name="action">The async action to execute</param>
        /// <param name="expectedExceptionType">The expected exception type</param>
        /// <param name="expectedMessageSubstring">A substring expected in the exception message</param>
        protected async System.Threading.Tasks.Task AssertThrowsAndMessageContainsAsync(
            Func<System.Threading.Tasks.Task> action,
            Type expectedExceptionType,
            string expectedMessageSubstring)
        {
            var ex = await Assert.ThrowsAsync(expectedExceptionType, action);
            Assert.NotNull(ex);
            Assert.Contains(expectedMessageSubstring, ex.Message ?? string.Empty);
        }

        /// <summary>
        /// Disposes all registered mocks and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for subclass cleanup.
        /// Subclasses should override this method to clean up their own resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Clear mock registry (Moq doesn't require explicit cleanup)
                _mocks.Clear();
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizer to ensure cleanup if Dispose() is not called.
        /// </summary>
        ~TestFixtureBase()
        {
            Dispose(false);
        }
    }
}
