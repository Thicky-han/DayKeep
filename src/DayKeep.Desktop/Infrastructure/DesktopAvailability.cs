using System.Runtime.InteropServices;
using System.Text;

namespace DayKeep.Infrastructure;

public static class DesktopAvailability
{
    // UOI_IO queries whether this thread's desktop is receiving input; it does not switch desktops.
    public static bool IsAvailable() => GetUserObjectInformationW(GetThreadDesktop(GetCurrentThreadId()), 6, out var receivingInput, 4, out _) && receivingInput != 0 && !ForegroundIsFullscreen();
    public static bool CoversScreen(PixelBounds window,PixelBounds screen) => window.X<=screen.X && window.Y<=screen.Y && window.X+window.Width>=screen.X+screen.Width && window.Y+window.Height>=screen.Y+screen.Height;
    private static bool ForegroundIsFullscreen()
    {
        var handle=GetForegroundWindow(); if(handle==IntPtr.Zero) return false;
        GetWindowThreadProcessId(handle,out var process); if(process==(uint)Environment.ProcessId) return false;
        var name=new StringBuilder(64); GetClassNameW(handle,name,name.Capacity);
        if(name.ToString() is "Progman" or "WorkerW") return false;
        if(!GetWindowRect(handle,out var rect)) return false;
        var bounds=System.Windows.Forms.Screen.FromHandle(handle).Bounds;
        return CoversScreen(new(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top),new(bounds.X,bounds.Y,bounds.Width,bounds.Height));
    }
    public static IntPtr ForegroundWindow => GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint threadId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformationW(IntPtr handle, int index, out int value, uint length, out uint needed);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hwnd,StringBuilder name,int count);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
}
