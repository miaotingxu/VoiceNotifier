using System.Threading;

namespace VoiceNotifier;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var diagnosticPath = Path.Combine(AppContext.BaseDirectory, "startup-diagnostic.log");
        try
        {
            using var mutex = new Mutex(true, "Global\\VoiceNotifier.SingleInstance", out bool created);
            using var showWindowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VoiceNotifier.ShowWindow", out _);
            File.AppendAllText(diagnosticPath, $"{DateTime.Now:O} mutex-created={created}{Environment.NewLine}");
            if (!created)
            {
                showWindowSignal.Set();
                return;
            }
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => File.AppendAllText(diagnosticPath, $"{DateTime.Now:O} thread-exception={e.Exception}{Environment.NewLine}");
            AppDomain.CurrentDomain.UnhandledException += (_, e) => File.AppendAllText(diagnosticPath, $"{DateTime.Now:O} unhandled={e.ExceptionObject}{Environment.NewLine}");
            TaskScheduler.UnobservedTaskException += (_, e) => { File.AppendAllText(diagnosticPath, $"{DateTime.Now:O} task-exception={e.Exception}{Environment.NewLine}"); e.SetObserved(); };
            File.AppendAllText(diagnosticPath, $"{DateTime.Now:O} winforms-initialized{Environment.NewLine}");
            using var trayContext = new TrayApplicationContext(showWindowSignal);
            File.AppendAllText(diagnosticPath, $"{DateTime.Now:O} tray-context-created{Environment.NewLine}");
            Application.Run(trayContext);
            File.AppendAllText(diagnosticPath, $"{DateTime.Now:O} message-loop-exited{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup-error.log"), $"{DateTime.Now:O}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
    }
}
