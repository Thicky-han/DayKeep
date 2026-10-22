using System.Runtime.InteropServices;

namespace DayKeep.Infrastructure;

public static class DesktopAvailability
{
    // UOI_IO queries whether this thread's desktop is receiving input; it does not switch desktops.
    public static bool IsAvailable() => GetUserObjectInformationW(GetThreadDesktop(GetCurrentThreadId()), 6, out var receivingInput, 4, out _) && receivingInput != 0;
    public static IntPtr ForegroundWindow => GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint threadId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformationW(IntPtr handle, int index, out int value, uint length, out uint needed);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
