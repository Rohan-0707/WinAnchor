using System.Diagnostics;
using System.Runtime.InteropServices;
using InScreenApp.Interop;
using InScreenApp.Models;

namespace InScreenApp.Services;

internal static class WindowEnumerator
{
    public static IReadOnlyList<WindowInfo> GetOpenWindows(IEnumerable<IntPtr> excludeHandles)
    {
        var excluded = new HashSet<IntPtr>(excludeHandles.Select(Win32Native.ResolveRootWindow));
        var currentProcessId = (uint)Process.GetCurrentProcess().Id;
        var windows = new List<WindowInfo>();
        var seen = new HashSet<IntPtr>();

        Win32Native.EnumWindows((hwnd, _) =>
        {
            IntPtr root = Win32Native.ResolveRootWindow(hwnd);
            if (root == IntPtr.Zero || seen.Contains(root) || excluded.Contains(root))
            {
                return true;
            }

            if (!IsListableWindow(root, currentProcessId))
            {
                return true;
            }

            seen.Add(root);
            windows.Add(CreateWindowInfo(root));
            return true;
        }, IntPtr.Zero);

        return windows
            .OrderBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsListableWindow(IntPtr handle, uint currentProcessId)
    {
        if (!Win32Native.IsWindow(handle) || !Win32Native.IsWindowVisible(handle))
        {
            return false;
        }

        Win32Native.GetWindowThreadProcessId(handle, out uint processId);
        if (processId == currentProcessId)
        {
            return false;
        }

        if (!Win32Native.GetWindowRect(handle, out Win32Native.Rect rect) ||
            rect.Width < 120 || rect.Height < 80)
        {
            return false;
        }

        int exStyle = (int)Win32Native.GetWindowLongPtr(handle, Win32Native.GwlExstyle);
        if ((exStyle & Win32Native.WsExToolwindow) != 0)
        {
            return false;
        }

        string title = Win32Native.GetWindowTitle(handle);
        return !string.IsNullOrWhiteSpace(title) && title != "(Untitled window)";
    }

    private static WindowInfo CreateWindowInfo(IntPtr handle)
    {
        Win32Native.GetWindowThreadProcessId(handle, out uint processId);
        string processName;

        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            processName = $"pid:{processId}";
        }

        return new WindowInfo
        {
            Handle = handle,
            Title = Win32Native.GetWindowTitle(handle),
            ProcessName = processName
        };
    }
}
