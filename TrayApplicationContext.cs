namespace VoiceNotifier;
internal sealed class TrayApplicationContext : Form
{
    private readonly NotifyIcon _tray; private readonly ContextMenuStrip _trayMenu; private readonly AppConfig _config; private readonly AppLogger _log; private readonly VoiceApiClient _api; private readonly SpeechService _speech; private readonly System.Threading.Timer _timer; private int _running; private DateTime _lastSpeak;
    private readonly string _diagnosticPath;
    private readonly System.Windows.Forms.Timer _uiHeartbeat;
    private readonly RegisteredWaitHandle _showWindowRegistration;
    private bool _isExiting;
    public TrayApplicationContext(EventWaitHandle showWindowSignal)
    {
        MaximizeBox = false; MinimizeBox = false; WindowState = FormWindowState.Minimized; ShowInTaskbar = false;
        Text = "VoiceNotifier 状态"; ClientSize = new Size(360, 160); StartPosition = FormStartPosition.CenterScreen;
        var dir = AppContext.BaseDirectory; _diagnosticPath = Path.Combine(dir, "startup-diagnostic.log"); _config = AppConfig.Load(Path.Combine(dir, "config.ini")); _log = new AppLogger(Path.Combine(dir, "logs"), _config.LogRetentionDays, _config.LogMaxSizeMb); _api = new VoiceApiClient(_config.TimeoutSeconds); _speech = new SpeechService(_config);
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("立即检查", null, (_, _) => _ = PollAsync());
        _trayMenu.Items.Add("打开配置文件夹", null, (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true }));
        _trayMenu.Items.Add("退出", null, (_, _) => Exit());
        var iconPath = Path.Combine(dir, "appMain.ico");
        _tray = new NotifyIcon();
        _tray.Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        _tray.Text = "语音播报程序";
        _tray.ContextMenuStrip = _trayMenu;
        _tray.Visible = true;
        _timer = new System.Threading.Timer(_ => _ = PollAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _uiHeartbeat = new System.Windows.Forms.Timer { Interval = 3000 };
        _uiHeartbeat.Tick += (_, _) => File.AppendAllText(_diagnosticPath, $"{DateTime.Now:O} ui-heartbeat{Environment.NewLine}");
        var status = new Label { AutoSize = false, Text = "语音播报程序正在后台运行。\r\n\r\n如果托盘图标未显示，可再次运行 VoiceNotifier.exe 打开此窗口。", Location = new Point(20, 20), Size = new Size(320, 68) };
        var hideButton = new Button { Text = "隐藏窗口", Location = new Point(140, 105), Size = new Size(90, 30) };
        var exitButton = new Button { Text = "退出程序", Location = new Point(240, 105), Size = new Size(90, 30) };
        hideButton.Click += (_, _) => HideControlWindow(); exitButton.Click += (_, _) => Exit();
        Controls.Add(status); Controls.Add(hideButton); Controls.Add(exitButton);
        Load += (_, _) => { File.AppendAllText(_diagnosticPath, $"{DateTime.Now:O} form-load{Environment.NewLine}"); HideControlWindow(); _uiHeartbeat.Start(); if (_config.Enabled) _timer.Change(0, Timeout.Infinite); };
        Shown += (_, _) => File.AppendAllText(_diagnosticPath, $"{DateTime.Now:O} form-shown{Environment.NewLine}");
        FormClosed += (_, _) => File.AppendAllText(_diagnosticPath, $"{DateTime.Now:O} form-closed{Environment.NewLine}");
        FormClosing += (_, e) => { if (!_isExiting) { e.Cancel = true; HideControlWindow(); } };
        _showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(showWindowSignal, (_, _) => ShowControlWindow(), null, Timeout.Infinite, false);
    }
    private async Task PollAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) != 0 || !_config.Enabled) return;
        bool success = false;
        try { var messages = await _api.GetMessagesAsync(_config.Url, CancellationToken.None); success = true; if (messages.Count > 0 && (_config.CooldownSeconds == 0 || DateTime.Now - _lastSpeak >= TimeSpan.FromSeconds(_config.CooldownSeconds))) { foreach (var message in messages) _speech.Speak(message); _lastSpeak = DateTime.Now; } _tray.Text = "语音播报程序：服务正常"; }
        catch (Exception ex) { _log.Error(ex); _tray.Text = "语音播报程序：服务不可用，正在重试"; }
        finally { Volatile.Write(ref _running, 0); _timer.Change(TimeSpan.FromSeconds(success ? _config.IntervalSeconds : _config.RetrySeconds), Timeout.InfiniteTimeSpan); }
    }
    private void ShowControlWindow()
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => { ShowInTaskbar = true; WindowState = FormWindowState.Normal; Show(); Activate(); BringToFront(); });
    }
    private void HideControlWindow() { ShowInTaskbar = false; Hide(); }
    private void Exit() { _isExiting = true; Close(); }
    protected override void Dispose(bool disposing) { if (disposing) { File.AppendAllText(_diagnosticPath, $"{DateTime.Now:O} form-dispose{Environment.NewLine}"); _showWindowRegistration.Unregister(null); _uiHeartbeat.Dispose(); _timer.Dispose(); _tray.Visible = false; _tray.Dispose(); _speech.Dispose(); _api.Dispose(); } base.Dispose(disposing); }
}
