namespace VoiceNotifier;
internal sealed class AppLogger
{
    private readonly string _baseDir; private readonly long _maxBytes; private readonly int _retentionDays; private readonly object _sync = new(); private string _path;
    public AppLogger(string baseDir, int retentionDays, int maxFileSizeMb) { _baseDir = baseDir; _retentionDays = retentionDays; _maxBytes = Math.Max(1, maxFileSizeMb) * 1024L * 1024L; Directory.CreateDirectory(baseDir); _path = NewPath(); Cleanup(); }
    public void Info(string message) => Write("信息", message);
    public void Error(Exception ex) => Write("错误", ex.Message);
    private string NewPath() => Path.Combine(_baseDir, "VoiceNotifier-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".log");
    private void Write(string level, string message) { try { lock (_sync) { var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}"; if (File.Exists(_path) && new FileInfo(_path).Length + System.Text.Encoding.UTF8.GetByteCount(line) > _maxBytes) _path = NewPath(); File.AppendAllText(_path, line); Cleanup(); } } catch { } }
    private void Cleanup() { try { var cutoff = DateTime.Now.AddDays(-_retentionDays); foreach (var file in Directory.EnumerateFiles(_baseDir, "VoiceNotifier-*.log")) if (File.GetLastWriteTime(file) < cutoff) File.Delete(file); } catch { } }
}
