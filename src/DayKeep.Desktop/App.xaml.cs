using System.Threading;
using System.Windows;
using DayKeep.Infrastructure;

namespace DayKeep;

public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showRegistration;
    private TrayService? _tray;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        {
            SmokeRunner.Run(this, e.Args[1]);
            return;
        }

        _instanceMutex = new Mutex(true, "Local\\DayKeep.Desktop.Instance", out _ownsMutex);
        if (!_ownsMutex)
        {
            try { using var signal = EventWaitHandle.OpenExisting("Local\\DayKeep.Desktop.Show"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { MessageBox.Show("日守正在启动，请稍后再次打开。", "DayKeep"); }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DayKeep.Desktop.Show");
        var window = new MainWindow(new SettingsStore());
        MainWindow = window;
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(window.ShowFromTray), null, Timeout.Infinite, false);
        _tray = new TrayService(window.ShowFromTray, window.HideToTray, window.ShowReminderPreview, ExitApp);
        window.Show();
    }

    public void ExitApp()
    {
        if (MainWindow is MainWindow window) window.AllowClose = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showRegistration?.Unregister(null);
        _showEvent?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
