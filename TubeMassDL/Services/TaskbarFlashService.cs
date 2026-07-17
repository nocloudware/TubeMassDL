using System.Runtime.InteropServices;
using System.Windows;

namespace TubeMassDL.Services;

public static class TaskbarFlashService
{
    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    public static void Flash(Window window)
    {
        var wih = new System.Windows.Interop.WindowInteropHelper(window);
        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = wih.Handle,
            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount = uint.MaxValue,
            dwTimeout = 0
        };
        FlashWindowEx(ref info);
    }

    public static void StopFlashing(Window window)
    {
        var wih = new System.Windows.Interop.WindowInteropHelper(window);
        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = wih.Handle,
            dwFlags = 0,
            uCount = 0
        };
        FlashWindowEx(ref info);
    }
}
