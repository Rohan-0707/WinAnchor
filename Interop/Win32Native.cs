using System.Runtime.InteropServices;
using System.Text;

namespace InScreenApp.Interop;

internal static class Win32Native
{
    internal const int HwndTopmost = -1;
    internal const int HwndNotopmost = -2;

    internal const uint SwpNomove = 0x0002;
    internal const uint SwpNosize = 0x0001;
    internal const uint SwpNoactivate = 0x0010;
    internal const uint SwpShowwindow = 0x0040;

    internal const int GwlExstyle = -20;
    internal const int WsExTopmost = 0x00000008;
    internal const int WsExToolwindow = 0x00000080;

    internal const uint GaRoot = 2;

    internal const int SwRestore = 9;
    internal const int SwShowna = 8;
    internal const int SwShownormal = 1;
    internal const int SwShowmaximized = 3;

    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint VkT = 0x54;

    internal const int WmHotkey = 0x0312;
    internal const int WmSyscommand = 0x0112;
    internal const int ScMove = 0xF010;
    internal const int Htcaption = 0x0002;

    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCmd;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    internal static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    internal static WindowPlacement CreateWindowPlacement() =>
        new() { Length = Marshal.SizeOf<WindowPlacement>() };

    internal static IntPtr ResolveRootWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr root = GetAncestor(handle, GaRoot);
        return root != IntPtr.Zero ? root : handle;
    }

    internal static string GetWindowTitle(IntPtr hwnd)
    {
        var builder = new StringBuilder(512);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        string title = builder.ToString();
        return string.IsNullOrWhiteSpace(title) ? "(Untitled window)" : title;
    }

    internal static bool IsSameWindow(IntPtr left, IntPtr right) =>
        left != IntPtr.Zero && right != IntPtr.Zero &&
        ResolveRootWindow(left) == ResolveRootWindow(right);

    internal static bool IsWindowTopmost(IntPtr hwnd)
    {
        nint exStyle = GetWindowLongPtr(hwnd, GwlExstyle);
        return ((int)exStyle & WsExTopmost) != 0;
    }
}
