using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DayKeep.Core;
using DayKeep.Infrastructure;
using DayKeep.Localization;

namespace DayKeep;

public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showRegistration;
    private TrayService? _tray;
    private bool _ownsMutex;
    private SystemEventBridge? _systemEvents;
    private EventWaitHandle? _testExit;
    private RegisteredWaitHandle? _testExitRegistration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--live-reminder-test")
        {
            LiveReminderRunner.Run(this,e.Args[1]);
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--smoke-test")
        {
            SmokeRunner.Run(this, e.Args[1]);
            return;
        }

        var background=e.Args.Contains("--background");
        var testDirectory=e.Args.Length>=2 && e.Args[0]=="--instance-test" ? Path.GetFullPath(e.Args[1]) : null;
        if(testDirectory is not null) Directory.CreateDirectory(testDirectory);
        var settings=testDirectory is null ? new SettingsStore() : new SettingsStore(Path.Combine(testDirectory,"ui.json"));
        Localizer.SetLanguage(settings.Load().Language);
        var suffix=testDirectory is null ? "" : ".Test."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(testDirectory)))[..12];
        var prefix="Local\\DayKeep.Desktop"+suffix;
        _instanceMutex = new Mutex(false,prefix+".Instance");
        try { _ownsMutex=_instanceMutex.WaitOne(0); } catch(AbandonedMutexException) { _ownsMutex=true; }
        if (!_ownsMutex)
        {
            if(!background)
            {
                for(var attempt=0;attempt<10;attempt++)
                {
                    try { using var signal=EventWaitHandle.OpenExisting(prefix+".Show"); signal.Set(); break; }
                    catch(WaitHandleCannotBeOpenedException) { if(attempt==9) MessageBox.Show(Localizer.T("日守正在启动，请稍后再次打开。"),"DayKeep"); else Thread.Sleep(100); }
                }
            }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset,prefix+".Show");
        var window = testDirectory is null ? new MainWindow(settings) : new MainWindow(settings,new TaskService(new TaskRepository(Path.Combine(testDirectory,"tasks.db"))));
        window.ShowActivated=!background;
        MainWindow = window;
        var showSignals=0;
        void Observe() { if(testDirectory is not null) File.WriteAllText(Path.Combine(testDirectory,"instance.json"),JsonSerializer.Serialize(new { process=Environment.ProcessId,visible=window.IsVisible,showSignals,exitEvent=prefix+".Exit",background,hotkeyRegistered=window.GlobalHotkeyRegistered,monitors=System.Windows.Forms.Screen.AllScreens.Length,dpi=System.Windows.Media.VisualTreeHelper.GetDpi(window).PixelsPerInchX })); }
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(new Action(()=> { window.ShowFromTray(); showSignals++; Observe(); })), null, Timeout.Infinite, false);
        _tray = new TrayService(window.ShowFromTray, window.HideToTray, window.ShowReminderPreview, ExitApp);
        if(background) new WindowInteropHelper(window).EnsureHandle(); else window.Show();
        window.EnableReminders();
        _systemEvents=new SystemEventBridge(Dispatcher,()=>
        {
            window.Reminders?.RefreshEnvironment();
            foreach(Window open in Windows) if(open.IsVisible) WindowPlacement.EnsureVisible(open);
        });
        _systemEvents.Start();
        if(testDirectory is not null)
        {
            _testExit=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+".Exit");
            _testExitRegistration=ThreadPool.RegisterWaitForSingleObject(_testExit,(_,_)=>Dispatcher.BeginInvoke(ExitApp),null,Timeout.Infinite,true);
        }
        Observe();
    }

    public void ExitApp()
    {
        if (MainWindow is MainWindow window) window.AllowClose = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showRegistration?.Unregister(null);
        _systemEvents?.Dispose();
        _testExitRegistration?.Unregister(null); _testExit?.Dispose();
        _showEvent?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
