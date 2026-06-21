using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using InScreenApp.Interop;
using InScreenApp.Models;

namespace InScreenApp.Services;

public sealed class PinnedWindowController
{
    private const int DefaultMiniWidth = 560;
    private const int DefaultMiniHeight = 315;
    private const int MinWidth = 280;
    private const int MinHeight = 158;

    private readonly Dictionary<IntPtr, PinnedState> _pinned = new();
    private readonly DispatcherTimer _watcher;

    public PinnedWindowController()
    {
        _watcher = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _watcher.Tick += Watcher_Tick;
    }

    public event EventHandler<IntPtr>? PinReleased;
    public event EventHandler? PinStateChanged;

    public bool HasPinnedWindows => _pinned.Count > 0;

    public IReadOnlyList<PinnedWindowEntry> PinnedWindows =>
        _pinned.Values
            .Select(state => state.ToEntry())
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool IsPinned(IntPtr handle)
    {
        handle = Win32Native.ResolveRootWindow(handle);
        return _pinned.ContainsKey(handle) && Win32Native.IsWindow(handle);
    }

    public bool PinWindow(IntPtr targetHandle)
    {
        targetHandle = Win32Native.ResolveRootWindow(targetHandle);
        if (targetHandle == IntPtr.Zero || !Win32Native.IsWindow(targetHandle))
        {
            return false;
        }

        if (_pinned.ContainsKey(targetHandle))
        {
            return false;
        }

        var state = new PinnedState { Handle = targetHandle };
        state.OriginalPlacement = Win32Native.CreateWindowPlacement();
        if (Win32Native.GetWindowPlacement(targetHandle, ref state.OriginalPlacement))
        {
            state.HasOriginalPlacement = true;
        }

        state.WasFullscreen = IsFullscreenWindow(targetHandle, state.OriginalPlacement);
        state.StackIndex = _pinned.Count;

        EnsureVisible(targetHandle);
        RestoreFromFullscreen(targetHandle);
        MoveToInScreenBounds(targetHandle, state.StackIndex);
        SetTopmost(targetHandle, topmost: true);

        _pinned[targetHandle] = state;

        if (!_watcher.IsEnabled)
        {
            _watcher.Start();
        }

        PinStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ReleasePin(IntPtr handle, bool restoreBounds)
    {
        handle = Win32Native.ResolveRootWindow(handle);
        if (!_pinned.Remove(handle, out PinnedState? state))
        {
            return;
        }

        ReleaseWindowState(state, restoreBounds);

        if (_pinned.Count == 0)
        {
            _watcher.Stop();
        }

        PinReleased?.Invoke(this, handle);
        PinStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReleasePins(IEnumerable<IntPtr> handles, bool restoreBounds)
    {
        foreach (IntPtr handle in handles.Select(Win32Native.ResolveRootWindow).Distinct().ToList())
        {
            if (!_pinned.Remove(handle, out PinnedState? state))
            {
                continue;
            }

            ReleaseWindowState(state, restoreBounds);
            PinReleased?.Invoke(this, handle);
        }

        if (_pinned.Count == 0)
        {
            _watcher.Stop();
        }

        PinStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReleaseAllPins(bool restoreBounds, bool forceFullscreen = false)
    {
        if (_pinned.Count == 0)
        {
            return;
        }

        foreach (PinnedState state in _pinned.Values.ToList())
        {
            ReleaseWindowState(state, restoreBounds, forceFullscreen);
            PinReleased?.Invoke(this, state.Handle);
        }

        _pinned.Clear();
        _watcher.Stop();
        PinStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MovePinnedWindow(IntPtr handle, int deltaX, int deltaY)
    {
        handle = Win32Native.ResolveRootWindow(handle);
        if (!_pinned.ContainsKey(handle) ||
            !Win32Native.GetWindowRect(handle, out Win32Native.Rect rect))
        {
            return;
        }

        Win32Native.SetWindowPos(
            handle,
            new IntPtr(Win32Native.HwndTopmost),
            rect.Left + deltaX,
            rect.Top + deltaY,
            rect.Width,
            rect.Height,
            Win32Native.SwpNoactivate | Win32Native.SwpShowwindow);
    }

    public void ResetPinnedWindowPosition(IntPtr handle)
    {
        handle = Win32Native.ResolveRootWindow(handle);
        if (!_pinned.TryGetValue(handle, out PinnedState? state))
        {
            return;
        }

        MoveToInScreenBounds(handle, state.StackIndex);
        SetTopmost(handle, topmost: true);
    }

    public void DragPinnedWindow(IntPtr handle)
    {
        handle = Win32Native.ResolveRootWindow(handle);
        if (!_pinned.ContainsKey(handle))
        {
            return;
        }

        Win32Native.ReleaseCapture();
        _ = Win32Native.SendMessage(
            handle,
            Win32Native.WmSyscommand,
            (IntPtr)(Win32Native.ScMove + Win32Native.Htcaption),
            IntPtr.Zero);
    }

    public void ResizePinnedWindow(IntPtr handle, double scaleFactor)
    {
        handle = Win32Native.ResolveRootWindow(handle);
        if (!_pinned.ContainsKey(handle) ||
            !Win32Native.GetWindowRect(handle, out Win32Native.Rect rect))
        {
            return;
        }

        int newWidth = Math.Clamp((int)(rect.Width * scaleFactor), MinWidth, 1920);
        int newHeight = Math.Clamp((int)(rect.Height * scaleFactor), MinHeight, 1080);

        Win32Native.SetWindowPos(
            handle,
            new IntPtr(Win32Native.HwndTopmost),
            rect.Left,
            rect.Top,
            newWidth,
            newHeight,
            Win32Native.SwpNoactivate | Win32Native.SwpShowwindow);
    }

    public bool TryGetPinnedBounds(IntPtr handle, out WindowRect bounds)
    {
        handle = Win32Native.ResolveRootWindow(handle);
        if (!_pinned.ContainsKey(handle) ||
            !Win32Native.GetWindowRect(handle, out Win32Native.Rect rect))
        {
            bounds = default;
            return false;
        }

        bounds = new WindowRect
        {
            Left = rect.Left,
            Top = rect.Top,
            Width = rect.Width,
            Height = rect.Height
        };
        return true;
    }

    private static void ReleaseWindowState(PinnedState state, bool restoreBounds, bool forceFullscreen = false)
    {
        IntPtr handle = state.Handle;
        if (!Win32Native.IsWindow(handle))
        {
            return;
        }

        SetTopmost(handle, topmost: false);

        if (!restoreBounds)
        {
            return;
        }

        if (forceFullscreen || state.WasFullscreen)
        {
            RestoreFullscreen(handle, state);
            return;
        }

        if (state.HasOriginalPlacement)
        {
            Win32Native.WindowPlacement placement = state.OriginalPlacement;
            placement.Length = Marshal.SizeOf<Win32Native.WindowPlacement>();
            Win32Native.SetWindowPlacement(handle, ref placement);
        }
    }

    private static void RestoreFullscreen(IntPtr handle, PinnedState state)
    {
        if (state.HasOriginalPlacement)
        {
            Win32Native.WindowPlacement placement = state.OriginalPlacement;
            placement.Length = Marshal.SizeOf<Win32Native.WindowPlacement>();
            placement.ShowCmd = Win32Native.SwShowmaximized;
            if (Win32Native.SetWindowPlacement(handle, ref placement))
            {
                return;
            }
        }

        Win32Native.ShowWindow(handle, Win32Native.SwShowmaximized);
    }

    private static bool IsFullscreenWindow(IntPtr hwnd, Win32Native.WindowPlacement placement)
    {
        if (Win32Native.IsZoomed(hwnd) || placement.ShowCmd == Win32Native.SwShowmaximized)
        {
            return true;
        }

        if (!Win32Native.GetWindowRect(hwnd, out Win32Native.Rect rect))
        {
            return false;
        }

        Rect workArea = SystemParameters.WorkArea;
        return rect.Width >= workArea.Width * 0.9 &&
               rect.Height >= workArea.Height * 0.9;
    }

    private static void RestoreFromFullscreen(IntPtr hwnd)
    {
        if (Win32Native.IsZoomed(hwnd))
        {
            Win32Native.ShowWindow(hwnd, Win32Native.SwRestore);
            return;
        }

        if (!Win32Native.GetWindowRect(hwnd, out Win32Native.Rect rect))
        {
            return;
        }

        Rect workArea = SystemParameters.WorkArea;
        if (rect.Width >= workArea.Width * 0.9 && rect.Height >= workArea.Height * 0.9)
        {
            Win32Native.ShowWindow(hwnd, Win32Native.SwRestore);
        }
    }

    private static void MoveToInScreenBounds(IntPtr handle, int stackIndex)
    {
        var workArea = SystemParameters.WorkArea;
        int offset = stackIndex * 36;
        int left = (int)workArea.Right - DefaultMiniWidth - 24 - offset;
        int top = (int)workArea.Bottom - DefaultMiniHeight - 24 - offset;

        Win32Native.SetWindowPos(
            handle,
            IntPtr.Zero,
            left,
            top,
            DefaultMiniWidth,
            DefaultMiniHeight,
            Win32Native.SwpNoactivate | Win32Native.SwpShowwindow);
    }

    private static void EnsureVisible(IntPtr hwnd)
    {
        if (Win32Native.IsIconic(hwnd))
        {
            Win32Native.ShowWindow(hwnd, Win32Native.SwRestore);
        }
        else
        {
            Win32Native.ShowWindow(hwnd, Win32Native.SwShowna);
        }
    }

    private static void SetTopmost(IntPtr hwnd, bool topmost)
    {
        Win32Native.SetWindowPos(
            hwnd,
            new IntPtr(topmost ? Win32Native.HwndTopmost : Win32Native.HwndNotopmost),
            0,
            0,
            0,
            0,
            Win32Native.SwpNomove | Win32Native.SwpNosize | Win32Native.SwpNoactivate);
    }

    private void Watcher_Tick(object? sender, EventArgs e)
    {
        if (_pinned.Count == 0)
        {
            _watcher.Stop();
            return;
        }

        bool changed = false;
        foreach (IntPtr handle in _pinned.Keys.ToList())
        {
            if (!Win32Native.IsWindow(handle))
            {
                _pinned.Remove(handle);
                PinReleased?.Invoke(this, handle);
                changed = true;
                continue;
            }

            if (Win32Native.IsIconic(handle))
            {
                Win32Native.ShowWindow(handle, Win32Native.SwRestore);
                SetTopmost(handle, topmost: true);
            }
            else if (!Win32Native.IsWindowTopmost(handle))
            {
                SetTopmost(handle, topmost: true);
            }
        }

        if (_pinned.Count == 0)
        {
            _watcher.Stop();
        }

        if (changed)
        {
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class PinnedState
    {
        public required IntPtr Handle { get; init; }

        public Win32Native.WindowPlacement OriginalPlacement;

        public bool HasOriginalPlacement;

        public bool WasFullscreen;

        public int StackIndex;

        public PinnedWindowEntry ToEntry()
        {
            Win32Native.GetWindowThreadProcessId(Handle, out uint processId);
            string processName;

            try
            {
                processName = System.Diagnostics.Process.GetProcessById((int)processId).ProcessName;
            }
            catch
            {
                processName = $"pid:{processId}";
            }

            return new PinnedWindowEntry
            {
                Handle = Handle,
                Title = Win32Native.GetWindowTitle(Handle),
                ProcessName = processName
            };
        }
    }
}
