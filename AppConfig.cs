using System.Text;

namespace VoiceNotifier;

internal sealed class AppConfig
{
    public string Url { get; private set; } = "http://localhost:8099/wms/extendAPI/getBroadcastVoice";
    public int IntervalSeconds { get; private set; } = 2;
    public int TimeoutSeconds { get; private set; } = 10;
    public int RetrySeconds { get; private set; } = 5;
    public bool Enabled { get; private set; } = true;
    public int Rate { get; private set; }
    public int Volume { get; private set; } = 100;
    public int CooldownSeconds { get; private set; }

    public static AppConfig Load(string path)
    {
        var c = new AppConfig();
        if (!File.Exists(path)) return c;
        string section = "";
        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = raw.Trim(); if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1].Trim().ToLowerInvariant(); continue; }
            var pos = line.IndexOf('='); if (pos < 1) continue;
            var key = line[..pos].Trim(); var value = line[(pos + 1)..].Trim();
            if (section == "url" && key.Equals("getVoice", StringComparison.OrdinalIgnoreCase)) c.Url = value;
            if (section == "polling") { if (key.Equals("intervalSeconds", StringComparison.OrdinalIgnoreCase)) c.IntervalSeconds = Positive(value, 2); if (key.Equals("requestTimeoutSeconds", StringComparison.OrdinalIgnoreCase)) c.TimeoutSeconds = Positive(value, 10); if (key.Equals("retryIntervalSeconds", StringComparison.OrdinalIgnoreCase)) c.RetrySeconds = Positive(value, 5); if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase)) c.Enabled = bool.TryParse(value, out var b) && b; }
            if (section == "speech") { if (key.Equals("rate", StringComparison.OrdinalIgnoreCase)) c.Rate = Integer(value, 0, -10, 10); if (key.Equals("volume", StringComparison.OrdinalIgnoreCase)) c.Volume = Integer(value, 100, 0, 100); if (key.Equals("cooldownSeconds", StringComparison.OrdinalIgnoreCase)) c.CooldownSeconds = PositiveOrZero(value); }
        }
        return c;
    }
    private static int Positive(string s, int d) => int.TryParse(s, out var n) && n > 0 ? n : d;
    private static int PositiveOrZero(string s) => int.TryParse(s, out var n) && n >= 0 ? n : 0;
    private static int Integer(string s, int d, int min, int max) => int.TryParse(s, out var n) ? Math.Clamp(n, min, max) : d;
}
