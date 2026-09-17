namespace VoiceNotifier;
internal sealed class TrayApplicationContext : Form
{
    private enum ServiceState { Unknown, Running, Stopped }
    private readonly NotifyIcon _tray; private readonly ContextMenuStrip _trayMenu; private readonly AppConfig _config; private readonly AppLogger _log; private readonly VoiceApiClient _api; private readonly SpeechService _speech; private readonly System.Threading.Timer _timer; private int _running; private DateTime _lastSpeak;
    private readonly string _diagnosticPath;
    private readonly System.Windows.Forms.Timer _uiHeartbeat;
    private readonly RegisteredWaitHandle _showWindowRegistration;
    private bool _isExiting;
    private ServiceState _serviceState = ServiceState.Unknown;
    private int _pollCount;
    public TrayApplicationContext(EventWaitHandle showWindowSignal)
    {
        MaximizeBox = false; MinimizeBox = false; WindowState = FormWindowState.Minimized; ShowInTaskbar = false;
        Text = "VoiceNotifier 状态"; ClientSize = new Size(360, 160); StartPosition = FormStartPosition.CenterScreen;
        var dir = AppContext.BaseDirectory; _diagnosticPath = Path.Combine(dir, "startup-diagnostic.log"); _config = AppConfig.Load(Path.Combine(dir, "config.ini")); _log = new AppLogger(Path.Combine(dir, "logs"), _config.LogRetentionDays, _config.LogMaxSizeMb, _config.LogLevel); _api = new VoiceApiClient(_config.TimeoutSeconds); _speech = new SpeechService(_config);
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
        var pollStarted = DateTime.Now;
        var pollNumber = Interlocked.Increment(ref _pollCount);
        var serviceAvailable = false;
        var nextDelay = _config.RetrySeconds;
        try
        {
            _log.Trace($"开始第 {pollNumber} 次轮询");
            if (!Uri.TryCreate(_config.Url, UriKind.Absolute, out var endpoint) || endpoint.Port <= 0)
            {
                _log.Error("接口地址无效，无法检测服务端口：" + _config.Url);
                _tray.Text = "语音播报程序：配置错误";
                return;
            }
            _log.Trace("准备检测服务端口：" + endpoint.Host + ":" + endpoint.Port);
            serviceAvailable = await _api.CanConnectAsync(endpoint, CancellationToken.None);
            var previous = _serviceState;
            var current = serviceAvailable ? ServiceState.Running : ServiceState.Stopped;
            _serviceState = current;
            if (previous != current && previous != ServiceState.Unknown)
            {
                var prompt = current == ServiceState.Running ? "服务连接成功" : "服务断开";
                _speech.Speak(prompt);
                _log.Info($"服务状态变化：{previous} -> {current}，已播报“{prompt}”");
            }
            if (!serviceAvailable)
            {
                _log.Error($"服务端口不可连接：{endpoint.Host}:{endpoint.Port}");
                _tray.Text = "语音播报程序：服务不可用，正在重试";
                return;
            }
            _tray.Text = "语音播报程序：服务正常";
            _log.Debug("接口地址：GET " + _config.Url);
            var messages = await _api.GetMessagesAsync(_config.Url, CancellationToken.None);
            nextDelay = _config.IntervalSeconds;
            if (messages.Count > 0 && (_config.CooldownSeconds == 0 || DateTime.Now - _lastSpeak >= TimeSpan.FromSeconds(_config.CooldownSeconds)))
            {
                foreach (var message in messages) _speech.Speak(message);
                _lastSpeak = DateTime.Now;
                _log.Info($"接口返回 {messages.Count} 条有效播报数据");
            }
        }
        catch (Exception ex) { _log.Error(ex); _tray.Text = serviceAvailable ? "语音播报程序：接口异常，正在重试" : "语音播报程序：服务不可用，正在重试"; }
        finally
        {
            _log.Trace($"本次轮询完成，耗时 {(DateTime.Now - pollStarted).TotalMilliseconds:0} 毫秒");
            Volatile.Write(ref _running, 0);
            _timer.Change(TimeSpan.FromSeconds(nextDelay), Timeout.InfiniteTimeSpan);
        }
    }
    private void ShowControlWindow()
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => { ShowInTaskbar = true; WindowState = FormWindowState.Normal; Show(); Activate(); BringToFront(); });
    }
    private void HideControlWindow() { ShowInTaskbar = false; Hide(); }
    private void Exit() { _isExiting = true; Close(); }
    public void LogCrash(Exception ex) => _log.Crash(ex);
    protected override void Dispose(bool disposing) { if (disposing) { File.AppendAllText(_diagnosticPath, $"{DateTime.Now:O} form-dispose{Environment.NewLine}"); _showWindowRegistration.Unregister(null); _uiHeartbeat.Dispose(); _timer.Dispose(); _tray.Visible = false; _tray.Dispose(); _speech.Dispose(); _api.Dispose(); } base.Dispose(disposing); }
}
