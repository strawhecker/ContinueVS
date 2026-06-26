namespace ContinueCore.Util;
public static partial class TextFunctions
{
    public static string replaceEscapedCharacters(string str)
    {
        return str.replaceAll("/* unknown: /\\\\(n|t|r|\\\\|\"|')/g */", "/* untranslatable arrow body */");
    }

    public static string escapeForSVG(string text)
    {
        return text.replace("/* unknown: /&/g */", "&amp;").replace("/* unknown: /</g */", "&lt;").replace("/* unknown: />/g */", "&gt;").replace("/* unknown: /\"/g */", "&quot;").replace("/* unknown: /'/g */", "&apos;").replace("/* unknown: /\\n/g */", "\\\\n").replace("/* unknown: /\\t/g */", "\\\\t").replace("/* unknown: /\\r/g */", "\\\\r");
    }

    public static string kebabOfStr(string str)
    {
        return str.replace("/* unknown: /([a-z0-9])([A-Z])/g */", "$1-$2").replace("/* unknown: /[\\s_]+/g */", "-").toLowerCase();
    }

    public static string kebabOfThemeStr(string str)
    {
        return str.toLowerCase().replace("/* unknown: /[\\s_]+/g */", "-").replace("/* unknown: /\\(|\\)/g */", "");
    }
}