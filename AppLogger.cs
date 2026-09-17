namespace VoiceNotifier;
internal sealed class AppLogger
{
    private readonly string _path;
    public AppLogger(string baseDir) { Directory.CreateDirectory(baseDir); _path = Path.Combine(baseDir, "VoiceNotifier-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log"); }
    public void Info(string message) => Write("信息", message);
    public void Error(Exception ex) => Write("错误", ex.Message);
    private void Write(string level, string message) { try { File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}"); } catch { } }
}
