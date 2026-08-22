#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ContinueVS.Services.Utilities
{
    /// <summary>
    /// Utility for extracting and validating tool arguments with type safety.
    /// Throws descriptive exceptions for missing or invalid parameters.
    /// </summary>
    public static class ToolArgumentParser
    {
        /// <summary>
        /// Extract a required or optional string argument.
        /// </summary>
        /// <param name="args">Dictionary of tool arguments</param>
        /// <param name="name">Parameter name</param>
        /// <param name="defaultValue">Optional default value if parameter is missing</param>
        /// <returns>String value or default</returns>
        /// <exception cref="ArgumentNullException">If args or name is null</exception>
        /// <exception cref="KeyNotFoundException">If required parameter is missing</exception>
        public static string GetStringArg(Dictionary<string, object?> args, string name, string? defaultValue = null)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args), "Tool arguments dictionary cannot be null");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Parameter name cannot be null or empty");

            if (!args.ContainsKey(name))
            {
                if (defaultValue != null)
                    return defaultValue;
                throw new KeyNotFoundException($"Required string parameter '{name}' not found in tool arguments");
            }

            var value = args[name];
            if (value == null)
            {
                if (defaultValue != null)
                    return defaultValue;
                throw new ArgumentNullException(name, $"Parameter '{name}' is null and no default provided");
            }

            if (!(value is string))
                throw new FormatException($"Parameter '{name}' is not a string; got {value.GetType().Name}");

            return (string)value;
        }

        /// <summary>
        /// Extract a required or optional integer argument.
        /// </summary>
        /// <param name="args">Dictionary of tool arguments</param>
        /// <param name="name">Parameter name</param>
        /// <param name="defaultValue">Optional default value if parameter is missing</param>
        /// <returns>Integer value or default</returns>
        /// <exception cref="ArgumentNullException">If args or name is null</exception>
        /// <exception cref="KeyNotFoundException">If required parameter is missing</exception>
        /// <exception cref="FormatException">If value cannot be parsed as int</exception>
        public static int GetIntArg(Dictionary<string, object?> args, string name, int? defaultValue = null)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args), "Tool arguments dictionary cannot be null");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Parameter name cannot be null or empty");

            if (!args.ContainsKey(name))
            {
                if (defaultValue.HasValue)
                    return defaultValue.Value;
                throw new KeyNotFoundException($"Required integer parameter '{name}' not found in tool arguments");
            }

            var value = args[name];
            if (value == null)
            {
                if (defaultValue.HasValue)
                    return defaultValue.Value;
                throw new ArgumentNullException(name, $"Parameter '{name}' is null and no default provided");
            }

            // Handle if already an int
            if (value is int intVal)
                return intVal;

            // Try to parse as string
            if (value is string strVal)
            {
                if (!int.TryParse(strVal, out int result))
                    throw new FormatException($"Parameter '{name}' cannot be parsed as integer; got '{strVal}'");
                return result;
            }

            // Try to parse long or other numeric types
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Parameter '{name}' cannot be converted to integer; got {value.GetType().Name}", ex);
            }
        }

        /// <summary>
        /// Extract a required or optional boolean argument.
        /// Accepts "true"/"false" strings (case-insensitive) or native bool.
        /// </summary>
        /// <param name="args">Dictionary of tool arguments</param>
        /// <param name="name">Parameter name</param>
        /// <param name="defaultValue">Optional default value if parameter is missing</param>
        /// <returns>Boolean value or default</returns>
        /// <exception cref="ArgumentNullException">If args or name is null</exception>
        /// <exception cref="KeyNotFoundException">If required parameter is missing</exception>
        /// <exception cref="FormatException">If value cannot be parsed as bool</exception>
        public static bool GetBoolArg(Dictionary<string, object?> args, string name, bool? defaultValue = null)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args), "Tool arguments dictionary cannot be null");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Parameter name cannot be null or empty");

            if (!args.ContainsKey(name))
            {
                if (defaultValue.HasValue)
                    return defaultValue.Value;
                throw new KeyNotFoundException($"Required boolean parameter '{name}' not found in tool arguments");
            }

            var value = args[name];
            if (value == null)
            {
                if (defaultValue.HasValue)
                    return defaultValue.Value;
                throw new ArgumentNullException(name, $"Parameter '{name}' is null and no default provided");
            }

            // Handle if already a bool
            if (value is bool boolVal)
                return boolVal;

            // Handle string values
            if (value is string strVal)
            {
                if (strVal.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (strVal.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;
                throw new FormatException($"Parameter '{name}' cannot be parsed as boolean; got '{strVal}' (expected 'true' or 'false')");
            }

            throw new FormatException($"Parameter '{name}' is not a boolean or string; got {value.GetType().Name}");
        }

        /// <summary>
        /// Extract an array parameter and return as a generic List.
        /// </summary>
        /// <typeparam name="T">Element type of the array</typeparam>
        /// <param name="args">Dictionary of tool arguments</param>
        /// <param name="name">Parameter name</param>
        /// <returns>List of elements</returns>
        /// <exception cref="ArgumentNullException">If args or name is null</exception>
        /// <exception cref="KeyNotFoundException">If required parameter is missing</exception>
        /// <exception cref="FormatException">If value is not an array or element types don't match</exception>
        public static List<T> GetArrayArg<T>(Dictionary<string, object?> args, string name)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args), "Tool arguments dictionary cannot be null");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Parameter name cannot be null or empty");

            if (!args.ContainsKey(name))
                throw new KeyNotFoundException($"Required array parameter '{name}' not found in tool arguments");

            var value = args[name];
            if (value == null)
                throw new ArgumentNullException(name, $"Parameter '{name}' is null");

            // Handle if already a List<T>
            if (value is List<T> listVal)
                return listVal;

            // Handle if it's an array
            if (value is T[] arrayVal)
                return arrayVal.ToList();

            // Handle if it's an IEnumerable<T>
            if (value is IEnumerable<T> enumVal)
                return enumVal.ToList();

            throw new FormatException($"Parameter '{name}' is not an array or list; got {value.GetType().Name}");
        }

        /// <summary>
        /// Extract a nested object parameter.
        /// </summary>
        /// <param name="args">Dictionary of tool arguments</param>
        /// <param name="name">Parameter name</param>
        /// <returns>Dictionary representing the object</returns>
        /// <exception cref="ArgumentNullException">If args or name is null</exception>
        /// <exception cref="KeyNotFoundException">If required parameter is missing</exception>
        /// <exception cref="FormatException">If value is not an object/dictionary</exception>
        public static Dictionary<string, object?> GetObjectArg(Dictionary<string, object?> args, string name)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args), "Tool arguments dictionary cannot be null");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Parameter name cannot be null or empty");

            if (!args.ContainsKey(name))
                throw new KeyNotFoundException($"Required object parameter '{name}' not found in tool arguments");

            var value = args[name];
            if (value == null)
                throw new ArgumentNullException(name, $"Parameter '{name}' is null");

            // Handle if already a Dictionary<string, object?>
            if (value is Dictionary<string, object?> dictVal)
                return dictVal;

            throw new FormatException($"Parameter '{name}' is not an object/dictionary; got {value.GetType().Name}");
        }
    }
}
