using System.Windows.Threading;
using Microsoft.Win32;

namespace DayKeep.Infrastructure;

public sealed class SystemEventBridge(Dispatcher dispatcher, Action refresh) : IDisposable
{
    private bool _disposed;
    public void Start()
    {
        SystemEvents.PowerModeChanged+=PowerChanged; SystemEvents.SessionSwitch+=SessionChanged;
        SystemEvents.TimeChanged+=Changed; SystemEvents.DisplaySettingsChanged+=Changed;
    }
    private void Queue() { if(!_disposed && !dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(new Action(()=> { if(!_disposed) refresh(); })); }
    private void Changed(object? sender,EventArgs args) => Queue();
    private void PowerChanged(object sender,PowerModeChangedEventArgs args) { if(args.Mode==PowerModes.Resume) Queue(); }
    private void SessionChanged(object sender,SessionSwitchEventArgs args) => Queue();
    public void Dispose()
    {
        _disposed=true; SystemEvents.PowerModeChanged-=PowerChanged; SystemEvents.SessionSwitch-=SessionChanged;
        SystemEvents.TimeChanged-=Changed; SystemEvents.DisplaySettingsChanged-=Changed;
    }
}
