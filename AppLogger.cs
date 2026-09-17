namespace VoiceNotifier;
internal sealed class AppLogger
{
    private readonly string _baseDir; private readonly long _maxBytes; private readonly int _retentionDays; private readonly int _minimum; private readonly object _sync = new(); private string _normalPath; private string _errorPath;
    public AppLogger(string baseDir, int retentionDays, int maxFileSizeMb, string level) { _baseDir = baseDir; _retentionDays = retentionDays; _maxBytes = Math.Max(1, maxFileSizeMb) * 1024L * 1024L; _minimum = level.ToLowerInvariant() switch { "trace" => 0, "debug" => 1, "info" => 2, "error" => 3, _ => 4 }; Directory.CreateDirectory(baseDir); _normalPath = NewPath(false); _errorPath = NewPath(true); Cleanup(); }
    public void Trace(string message) => Write(0, "Trace", message, false);
    public void Debug(string message) => Write(1, "Debug", message, false);
    public void Info(string message) => Write(2, "Info", message, false);
    public void Error(Exception ex) => Write(3, "Error", ex.ToString(), true);
    public void Error(string message) => Write(3, "Error", message, true);
    public void Crash(Exception ex) => Write(3, "Error", "捕获到崩溃异常：" + ex, true);
    private string NewPath(bool error) => Path.Combine(_baseDir, $"VoiceNotifier-{(error ? "error-" : "")}{DateTime.Now:yyyy-MM-dd-HHmmss}.log");
    private void Write(int level, string name, string message, bool error) { if (level < _minimum) return; try { lock (_sync) { var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{name}] {message}{Environment.NewLine}"; ref var path = ref error ? ref _errorPath : ref _normalPath; if (File.Exists(path) && new FileInfo(path).Length + System.Text.Encoding.UTF8.GetByteCount(line) > _maxBytes) path = NewPath(error); File.AppendAllText(path, line); Cleanup(); } } catch { } }
    private void Cleanup() { try { var cutoff = DateTime.Now.AddDays(-_retentionDays); foreach (var file in Directory.EnumerateFiles(_baseDir, "VoiceNotifier-*.log")) if (File.GetLastWriteTime(file) < cutoff) File.Delete(file); } catch { } }
}
