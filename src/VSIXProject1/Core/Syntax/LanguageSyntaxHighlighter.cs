using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ContinueVS.Core.Syntax
{
    /// <summary>
    /// Provides syntax highlighting colors and token type classification for multiple programming languages.
    /// Supports C#, JavaScript, Python, TypeScript, Java, Go, Rust, SQL, and others.
    /// </summary>
    public static class LanguageSyntaxHighlighter
    {
        /// <summary>
        /// Token type classification for syntax highlighting.
        /// </summary>
        public enum TokenType
        {
            Default,
            Keyword,
            String,
            Comment,
            Number,
            Operator,
            Type,
            Function,
            Variable
        }

        /// <summary>
        /// Color scheme for a programming language.
        /// Maps token types to foreground colors.
        /// </summary>
        public class ColorScheme
        {
            public Brush Keyword { get; set; } = new SolidColorBrush(Color.FromRgb(0, 0, 255)); // Blue
            public Brush String { get; set; } = new SolidColorBrush(Color.FromRgb(163, 21, 21)); // Red
            public Brush Comment { get; set; } = new SolidColorBrush(Color.FromRgb(0, 128, 0)); // Green
            public Brush Number { get; set; } = new SolidColorBrush(Color.FromRgb(255, 128, 0)); // Orange
            public Brush Operator { get; set; } = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // Black
            public Brush Type { get; set; } = new SolidColorBrush(Color.FromRgb(0, 128, 128)); // Teal
            public Brush Function { get; set; } = new SolidColorBrush(Color.FromRgb(128, 0, 128)); // Purple
            public Brush Variable { get; set; } = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // Black
            public Brush Default { get; set; } = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // Black

            public Brush GetBrush(TokenType type) => type switch
            {
                TokenType.Keyword => Keyword,
                TokenType.String => String,
                TokenType.Comment => Comment,
                TokenType.Number => Number,
                TokenType.Operator => Operator,
                TokenType.Type => Type,
                TokenType.Function => Function,
                TokenType.Variable => Variable,
                _ => Default
            };
        }

        private static readonly Dictionary<string, ColorScheme> ColorSchemes = new()
        {
            { "csharp", new ColorScheme() },
            { "c#", new ColorScheme() },
            { "javascript", new ColorScheme() },
            { "js", new ColorScheme() },
            { "python", new ColorScheme() },
            { "py", new ColorScheme() },
            { "typescript", new ColorScheme() },
            { "ts", new ColorScheme() },
            { "java", new ColorScheme() },
            { "go", new ColorScheme() },
            { "rust", new ColorScheme() },
            { "sql", new ColorScheme() },
            { "html", new ColorScheme() },
            { "xml", new ColorScheme() },
            { "json", new ColorScheme() },
            { "cpp", new ColorScheme() },
            { "c++", new ColorScheme() }
        };

        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
            "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
            "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
            "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while", "yield",
            "async", "await", "from", "join", "let", "orderby", "select", "where", "group", "into"
        };

        private static readonly HashSet<string> JavaScriptKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "arguments", "await", "boolean", "break", "byte", "case", "catch", "char", "class",
            "const", "continue", "debugger", "default", "delete", "do", "double", "else", "enum", "eval",
            "export", "extends", "false", "final", "finally", "float", "for", "function", "goto", "if",
            "implements", "import", "in", "instanceof", "int", "interface", "let", "long", "native", "new",
            "null", "package", "private", "protected", "public", "return", "short", "static", "super",
            "switch", "synchronized", "this", "throw", "throws", "transient", "true", "try", "typeof",
            "var", "void", "volatile", "while", "with", "yield", "async", "of"
        };

        private static readonly HashSet<string> PythonKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue",
            "def", "del", "elif", "else", "except", "finally", "for", "from", "global", "if", "import",
            "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while",
            "with", "yield"
        };

        /// <summary>
        /// Gets the color scheme for a given language.
        /// Returns default scheme if language not recognized.
        /// </summary>
        public static ColorScheme GetColorScheme(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return new ColorScheme();
            }

            return ColorSchemes.TryGetValue(language ?? string.Empty, out var scheme) ? scheme : new ColorScheme();
        }

        /// <summary>
        /// Classifies a token based on language-specific keyword patterns.
        /// Simple heuristic-based approach (can be extended with regex-based tokenization).
        /// </summary>
        public static TokenType ClassifyToken(string token, string? language)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return TokenType.Default;
            }

            // Check for numeric literals
            if (char.IsDigit(token[0]))
            {
                return TokenType.Number;
            }

            // Check for quoted strings
            if ((token.StartsWith("\"") && token.EndsWith("\"")) ||
                (token.StartsWith("'") && token.EndsWith("'")))
            {
                return TokenType.String;
            }

            // Check for operators
            if (token.Length <= 3 && token.Any(c => "+-*/%<>=!&|^".IndexOf(c) >= 0))
            {
                return TokenType.Operator;
            }

            // Check for comments
            if (token.StartsWith("//") || token.StartsWith("#") || token.StartsWith("--"))
            {
                return TokenType.Comment;
            }

            // Check for keywords based on language
            if (!string.IsNullOrWhiteSpace(language) && language != null)
            {
                if (language.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("c#", StringComparison.OrdinalIgnoreCase))
                {
                    return CSharpKeywords.Contains(token) ? TokenType.Keyword : TokenType.Variable;
                }

                if (language.Equals("javascript", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("js", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("typescript", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("ts", StringComparison.OrdinalIgnoreCase))
                {
                    return JavaScriptKeywords.Contains(token) ? TokenType.Keyword : TokenType.Variable;
                }

                if (language.Equals("python", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("py", StringComparison.OrdinalIgnoreCase))
                {
                    return PythonKeywords.Contains(token) ? TokenType.Keyword : TokenType.Variable;
                }
            }

            return TokenType.Variable;
        }

        /// <summary>
        /// Gets brush color for a token based on language and token type.
        /// </summary>
        public static Brush GetTokenBrush(string token, string? language, TokenType? overrideType = null)
        {
            var scheme = GetColorScheme(language);
            var tokenType = overrideType ?? ClassifyToken(token, language);
            return scheme.GetBrush(tokenType);
        }
    }
}
