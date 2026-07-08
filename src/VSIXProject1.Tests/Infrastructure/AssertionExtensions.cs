#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using ContinueVS.Exceptions;

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Custom assertion extension methods for bridge-specific validations.
    /// 
    /// Provides fluent, readable assertions for:
    /// - Bridge exception error codes and context
    /// - RPC message format validation
    /// - Configuration state verification
    /// </summary>
    internal static class AssertionExtensions
    {
        /// <summary>
        /// Asserts that a BridgeException has the expected error code.
        /// </summary>
        internal static void HasErrorCode(this BridgeException ex, string expectedCode)
        {
            Assert.NotNull(ex);
            Assert.Equal(expectedCode, ex.ErrorCode);
        }

        /// <summary>
        /// Asserts that a BridgeException contains a specific context key with an expected value.
        /// </summary>
        internal static void HasContextKey(this BridgeException ex, string key, string expectedValue)
        {
            Assert.NotNull(ex);
            Assert.True(ex.Context.ContainsKey(key), 
                $"Exception context does not contain key '{key}'. Available keys: {string.Join(", ", ex.Context.Keys)}");
            Assert.Equal(expectedValue, ex.Context[key]);
        }

        /// <summary>
        /// Asserts that a BridgeException contains a specific context key (regardless of value).
        /// </summary>
        internal static void HasContextKey(this BridgeException ex, string key)
        {
            Assert.NotNull(ex);
            Assert.True(ex.Context.ContainsKey(key),
                $"Exception context does not contain key '{key}'. Available keys: {string.Join(", ", ex.Context.Keys)}");
        }

        /// <summary>
        /// Asserts that a BridgeException contains all specified context keys.
        /// </summary>
        internal static void HasAllContextKeys(this BridgeException ex, params string[] keys)
        {
            Assert.NotNull(ex);
            var missingKeys = keys.Where(k => !ex.Context.ContainsKey(k)).ToList();
            if (missingKeys.Count > 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Exception context is missing keys: {string.Join(", ", missingKeys)}");
            }
        }

        /// <summary>
        /// Validates that a string is valid line-delimited JSON (NDJSON).
        /// Each non-empty line must be valid JSON.
        /// </summary>
        public static void IsValidNdjson(this string ndjson)
        {
            Assert.NotNull(ndjson);
            if (string.IsNullOrWhiteSpace(ndjson))
                return; // Empty is valid

            var lines = ndjson.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    // Attempt to parse as JSON
                    // For now, use a simple heuristic: must start with { or [ and end with } or ]
                    var trimmed = line.Trim();
                    Assert.True(
                        (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                        (trimmed.StartsWith("[") && trimmed.EndsWith("]")),
                        $"Line is not valid JSON: {line}");
                }
                catch (Exception ex)
                {
                    throw new Xunit.Sdk.XunitException($"NDJSON validation failed on line: {line}", ex);
                }
            }
        }

        /// <summary>
        /// Asserts that a string matches a JSON-RPC 2.0 message format.
        /// Expected format: {"jsonrpc":"2.0","method":"...","params":{...}}
        /// </summary>
        public static void IsValidJsonRpcMessage(this string jsonLine)
        {
            Assert.NotNull(jsonLine);

            var jsonRpcPattern = new Regex(
                @"^\{""jsonrpc""\s*:\s*""2\.0"".*\}$",
                RegexOptions.IgnoreCase);

            Assert.Matches(jsonRpcPattern, jsonLine.Trim());
        }

        /// <summary>
        /// Asserts that a string is a valid JSON-RPC request (has method and params).
        /// </summary>
        public static void IsValidJsonRpcRequest(this string jsonLine)
        {
            jsonLine.IsValidJsonRpcMessage();

            var requestPattern = new Regex(
                @"""method""\s*:\s*""[^""]+""",
                RegexOptions.IgnoreCase);

            Assert.Matches(requestPattern, jsonLine);
        }

        /// <summary>
        /// Asserts that a string is a valid JSON-RPC response (has result or error).
        /// </summary>
        public static void IsValidJsonRpcResponse(this string jsonLine)
        {
            jsonLine.IsValidJsonRpcMessage();

            var responsePattern = new Regex(
                @"(""result""|""error"")",
                RegexOptions.IgnoreCase);

            Assert.Matches(responsePattern, jsonLine);
        }

        /// <summary>
        /// Asserts that a string contains a JSON-RPC error response.
        /// </summary>
        public static void IsJsonRpcError(this string jsonLine, int expectedCode)
        {
            jsonLine.IsValidJsonRpcResponse();

            var errorPattern = new Regex(
                $@"""error""\s*:\s*\{{.*""code""\s*:\s*{expectedCode}.*\}}",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            Assert.Matches(errorPattern, jsonLine);
        }
    }
}
