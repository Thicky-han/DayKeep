using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DayKeep.Infrastructure;

public readonly record struct PixelBounds(int X,int Y,int Width,int Height);
public static class WindowPlacement
{
    public static PixelBounds? Bounds(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;
        return handle!=IntPtr.Zero && GetWindowRect(handle,out var rect) ? new(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top) : null;
    }
    public static PixelBounds Fit(PixelBounds window, PixelBounds work)
    {
        var width=Math.Min(window.Width,work.Width); var height=Math.Min(window.Height,work.Height);
        return new(Math.Clamp(window.X,work.X,work.X+work.Width-width),Math.Clamp(window.Y,work.Y,work.Y+work.Height-height),width,height);
    }
    public static void EnsureVisible(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==IntPtr.Zero || !GetWindowRect(handle,out var rect)) return;
        var area=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        if(window is MainWindow main && !main.IsCompact && area.Width/System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX<1060) { main.SetCompact(true); return; }
        var original=new PixelBounds(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
        var fitted=Fit(original,new(area.X,area.Y,area.Width,area.Height));
        if(fitted!=original) SetWindowPos(handle,IntPtr.Zero,fitted.X,fitted.Y,fitted.Width,fitted.Height,0x0014);
    }
    public static void WatchDpi(Window window)
    {
        var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source?.AddHook((IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)=>
        { if(message==0x02E0) window.Dispatcher.BeginInvoke(new Action(()=>EnsureVisible(window))); return IntPtr.Zero; });
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
}
