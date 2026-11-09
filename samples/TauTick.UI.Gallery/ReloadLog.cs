using System.Text.RegularExpressions;
using Avalonia.Logging;

namespace TauTick.UI.Gallery;

// Observe the selected tool. "Reload activity" is not a claim that an intended edit rendered.
internal sealed class ReloadLog : ILogSink
{
    public static event Action<string>? Activity;
    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Information && (area == "HotAvalonia" || level >= LogEventLevel.Warning);
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Log(level, area, source, messageTemplate, []);
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        var index = 0;
        var message = Regex.Replace(messageTemplate, @"\{[^}]+\}", _ => index < propertyValues.Length ? propertyValues[index++]?.ToString() ?? "null" : "?");
        Console.WriteLine($"{DateTimeOffset.UtcNow:O} [{area}/{level}] {message}");
        if (area != "HotAvalonia") return;
        if (level >= LogEventLevel.Error) Activity?.Invoke("Reload error: " + message.Split('\n')[0]);
        else if (message.StartsWith("Reloading") || message.StartsWith("Recompiling"))
            Activity?.Invoke("Reload activity: " + message + " Inspect the intended result; reset if state was recreated.");
    }
}
