using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DayKeep.Infrastructure;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xDA7;
    private readonly HwndSource _source;
    private readonly Action _toggle;
    public bool Registered { get; }

    public HotkeyService(Window window, Action toggle)
    {
        _toggle = toggle;
        _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!;
        _source.AddHook(HandleMessage);
        Registered = RegisterHotKey(_source.Handle, HotkeyId, 0x0001 | 0x0002 | 0x4000, 0x44);
    }

    private IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == HotkeyId) { _toggle(); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (Registered) UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(HandleMessage);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
