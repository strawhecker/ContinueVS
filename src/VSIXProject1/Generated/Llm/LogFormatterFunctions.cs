namespace ContinueCore.Llm;
public static partial class LogFormatterFunctions
{
    public static string formatTimestamp(double timestamp)
    {
        var date = "/* unknown: new Date(timestamp) */";
        var hours = date.getUTCHours().toString().padStart(2L, "0");
        var minutes = date.getUTCMinutes().toString().padStart(2L, "0");
        var seconds = date.getUTCSeconds();
        var milliseconds = date.getUTCMilliseconds();
        var secondsFormatted = $"{seconds}.{Math.floor(milliseconds / 100L)}";
        return $"{hours}:{minutes}:{secondsFormatted.padStart(4L, "0")}";
    }
}