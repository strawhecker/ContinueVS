#nullable enable

using System;
using System.Collections.Generic;
using ContinueVS.Services.Utilities;
using Xunit;

namespace ContinueVS.Tests.Services.Utilities
{
    public class ToolArgumentParserTests
    {
        [Fact]
        public void GetStringArg_WithValidString_ReturnsValue()
        {
            var args = new Dictionary<string, object?> { { "name", "test_value" } };
            var result = ToolArgumentParser.GetStringArg(args, "name");
            Assert.Equal("test_value", result);
        }

        [Fact]
        public void GetStringArg_WithMissingRequired_ThrowsKeyNotFoundException()
        {
            var args = new Dictionary<string, object?>();
            Assert.Throws<KeyNotFoundException>(() => 
                ToolArgumentParser.GetStringArg(args, "name"));
        }

        [Fact]
        public void GetStringArg_WithDefaultValue_ReturnsDefault()
        {
            var args = new Dictionary<string, object?>();
            var result = ToolArgumentParser.GetStringArg(args, "name", "default");
            Assert.Equal("default", result);
        }

        [Fact]
        public void GetStringArg_WithNullArgs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                ToolArgumentParser.GetStringArg(null!, "name"));
        }

        [Fact]
        public void GetIntArg_WithValidInt_ReturnsValue()
        {
            var args = new Dictionary<string, object?> { { "count", 42 } };
            var result = ToolArgumentParser.GetIntArg(args, "count");
            Assert.Equal(42, result);
        }

        [Fact]
        public void GetIntArg_WithStringInt_ParsesAndReturns()
        {
            var args = new Dictionary<string, object?> { { "count", "123" } };
            var result = ToolArgumentParser.GetIntArg(args, "count");
            Assert.Equal(123, result);
        }

        [Fact]
        public void GetIntArg_WithDefaultValue_ReturnsDefault()
        {
            var args = new Dictionary<string, object?>();
            var result = ToolArgumentParser.GetIntArg(args, "count", 99);
            Assert.Equal(99, result);
        }

        [Fact]
        public void GetBoolArg_WithStringTrue_ReturnsTrue()
        {
            var args = new Dictionary<string, object?> { { "enabled", "true" } };
            var result = ToolArgumentParser.GetBoolArg(args, "enabled");
            Assert.True(result);
        }

        [Fact]
        public void GetBoolArg_WithStringFalse_ReturnsFalse()
        {
            var args = new Dictionary<string, object?> { { "enabled", "false" } };
            var result = ToolArgumentParser.GetBoolArg(args, "enabled");
            Assert.False(result);
        }

        [Fact]
        public void GetBoolArg_WithNativeBool_ReturnsValue()
        {
            var args = new Dictionary<string, object?> { { "enabled", true } };
            var result = ToolArgumentParser.GetBoolArg(args, "enabled");
            Assert.True(result);
        }

        [Fact]
        public void GetBoolArg_WithDefaultValue_ReturnsDefault()
        {
            var args = new Dictionary<string, object?>();
            var result = ToolArgumentParser.GetBoolArg(args, "enabled", false);
            Assert.False(result);
        }

        [Fact]
        public void GetBoolArg_WithInvalidString_ThrowsFormatException()
        {
            var args = new Dictionary<string, object?> { { "enabled", "maybe" } };
            Assert.Throws<FormatException>(() => 
                ToolArgumentParser.GetBoolArg(args, "enabled"));
        }

        [Fact]
        public void GetArrayArg_WithValidList_ReturnsElements()
        {
            var items = new List<string> { "a", "b", "c" };
            var args = new Dictionary<string, object?> { { "items", items } };
            var result = ToolArgumentParser.GetArrayArg<string>(args, "items");
            Assert.Equal(3, result.Count);
            Assert.Contains("a", result);
        }

        [Fact]
        public void GetArrayArg_WithMissingRequired_ThrowsKeyNotFoundException()
        {
            var args = new Dictionary<string, object?>();
            Assert.Throws<KeyNotFoundException>(() => 
                ToolArgumentParser.GetArrayArg<string>(args, "items"));
        }

        [Fact]
        public void GetObjectArg_WithValidDictionary_ReturnsObject()
        {
            var obj = new Dictionary<string, object?> { { "key", "value" } };
            var args = new Dictionary<string, object?> { { "config", obj } };
            var result = ToolArgumentParser.GetObjectArg(args, "config");
            Assert.NotNull(result);
            Assert.Equal("value", result["key"]);
        }

        [Fact]
        public void GetObjectArg_WithMissingRequired_ThrowsKeyNotFoundException()
        {
            var args = new Dictionary<string, object?>();
            Assert.Throws<KeyNotFoundException>(() => 
                ToolArgumentParser.GetObjectArg(args, "config"));
        }

        [Fact]
        public void GetStringArg_WithWrongType_ThrowsFormatException()
        {
            var args = new Dictionary<string, object?> { { "name", 42 } };
            Assert.Throws<FormatException>(() => 
                ToolArgumentParser.GetStringArg(args, "name"));
        }

        [Fact]
        public void GetIntArg_WithInvalidString_ThrowsFormatException()
        {
            var args = new Dictionary<string, object?> { { "count", "not_a_number" } };
            Assert.Throws<FormatException>(() => 
                ToolArgumentParser.GetIntArg(args, "count"));
        }
    }
}
