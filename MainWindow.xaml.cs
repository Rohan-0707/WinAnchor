using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using InScreenApp.Interop;
using InScreenApp.Models;
using InScreenApp.Services;

namespace InScreenApp;

public partial class MainWindow : Window
{
    private const int HotkeyId = 9000;

    private readonly PinnedWindowController _pinController = App.PinController;
    private HwndSource? _hwndSource;
    private bool _hotkeyRegistered;
    private bool _isShuttingDown;

    public MainWindow()
    {
        InitializeComponent();
        _pinController.PinStateChanged += (_, _) => UpdatePinnedDisplay();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
        _hwndSource.AddHook(WndProc);
        RegisterHotKey();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isShuttingDown)
        {
            PrepareShutdown(unpinAll: true);
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterHotkey();
        base.OnClosed(e);
    }

    private void RegisterHotKey()
    {
        if (_hwndSource is null || _hotkeyRegistered)
        {
            return;
        }

        if (!Win32Native.RegisterHotKey(
                _hwndSource.Handle,
                HotkeyId,
                Win32Native.ModControl | Win32Native.ModAlt,
                Win32Native.VkT))
        {
            StatusText.Text = "Global hotkey could not be registered.";
            return;
        }

        _hotkeyRegistered = true;
    }

    private void UnregisterHotkey()
    {
        if (_hwndSource is null || !_hotkeyRegistered)
        {
            return;
        }

        Win32Native.UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        _hotkeyRegistered = false;
    }

    private void UnregisterHotkeyHook()
    {
        if (_hwndSource is null)
        {
            return;
        }

        UnregisterHotkey();
        _hwndSource.RemoveHook(WndProc);
        _hwndSource = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Native.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleForegroundWindowPin();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        RequestShutdown();

    private void PinNewWindowButton_Click(object sender, RoutedEventArgs e) => OpenPinPicker();

    private void OpenPinPicker()
    {
        var picker = new WindowPickerWindow(GetExcludedHandles)
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedWindow is null)
        {
            return;
        }

        PinSelectedWindow(picker.SelectedWindow);
    }

    private void UnpinButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_pinController.HasPinnedWindows)
        {
            return;
        }

        var picker = new UnpinPickerWindow(_pinController)
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedWindows.Count == 0)
        {
            return;
        }

        PinFeedback.PlayUnpinSound();
        _pinController.ReleasePins(
            picker.SelectedWindows.Select(window => window.Handle),
            restoreBounds: true);
        UpdatePinnedDisplay();
        StatusText.Text = picker.SelectedWindows.Count == 1
            ? $"Unpinned: {picker.SelectedWindows[0].Title}"
            : $"Unpinned {picker.SelectedWindows.Count} windows.";
    }

    private void PinnedWindowsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdateControlStates();

    private void PositionPadButton_Click(object sender, RoutedEventArgs e)
    {
        IntPtr? handle = GetSelectedPinnedHandle();
        if (handle is null)
        {
            StatusText.Text = "Select a pinned window first.";
            return;
        }

        if (sender is not System.Windows.Controls.Button button || button.Tag is not string direction)
        {
            return;
        }

        const int step = 28;

        if (direction == "Center")
        {
            _pinController.ResetPinnedWindowPosition(handle.Value);
            StatusText.Text = "Window position reset.";
            return;
        }

        (int deltaX, int deltaY) = direction switch
        {
            "N" => (0, -step),
            "S" => (0, step),
            "W" => (-step, 0),
            "E" => (step, 0),
            "NW" => (-step, -step),
            "NE" => (step, -step),
            "SW" => (-step, step),
            "SE" => (step, step),
            _ => (0, 0)
        };

        _pinController.MovePinnedWindow(handle.Value, deltaX, deltaY);
    }

    private void PinnedItemMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not PinnedWindowEntry entry)
        {
            return;
        }

        PinFeedback.PlayUnpinSound();
        _pinController.ReleasePin(entry.Handle, restoreBounds: true);
        UpdatePinnedDisplay();
        StatusText.Text = $"Unpinned: {entry.Title}";
    }

    private void SmallerButton_Click(object sender, RoutedEventArgs e)
    {
        IntPtr? handle = GetSelectedPinnedHandle();
        if (handle is null)
        {
            return;
        }

        _pinController.ResizePinnedWindow(handle.Value, 0.9);
    }

    private void LargerButton_Click(object sender, RoutedEventArgs e)
    {
        IntPtr? handle = GetSelectedPinnedHandle();
        if (handle is null)
        {
            return;
        }

        _pinController.ResizePinnedWindow(handle.Value, 1.1);
    }

    private void ToggleForegroundWindowPin()
    {
        IntPtr targetHandle = Win32Native.ResolveRootWindow(Win32Native.GetForegroundWindow());
        IntPtr selfHandle = new WindowInteropHelper(this).Handle;

        if (!IsValidExternalWindow(targetHandle, selfHandle))
        {
            StatusText.Text = "Hotkey ignored — no valid target window.";
            return;
        }

        if (_pinController.IsPinned(targetHandle))
        {
            PinFeedback.PlayUnpinSound();
            _pinController.ReleasePin(targetHandle, restoreBounds: true);
            UpdatePinnedDisplay();
            StatusText.Text = $"Unpinned via hotkey: {Win32Native.GetWindowTitle(targetHandle)}";
            return;
        }

        PinHandle(targetHandle, $"Pinned via hotkey: {Win32Native.GetWindowTitle(targetHandle)}");
    }

    private void PinSelectedWindow(WindowInfo window)
    {
        if (_pinController.IsPinned(window.Handle))
        {
            StatusText.Text = $"{window.Title} is already pinned.";
            return;
        }

        PinHandle(window.Handle, $"Pinned: {window.Title}");
    }

    private void PinHandle(IntPtr handle, string successMessage)
    {
        if (!_pinController.PinWindow(handle))
        {
            if (_pinController.IsPinned(handle))
            {
                StatusText.Text = "That window is already pinned.";
            }
            else
            {
                StatusText.Text = $"Failed to pin window (Win32 error {Marshal.GetLastWin32Error()}).";
            }

            return;
        }

        PinFeedback.PlayPinSound();
        UpdatePinnedDisplay();
        StatusText.Text = successMessage;
    }

    public void ReleaseAllPins()
    {
        _pinController.ReleaseAllPins(restoreBounds: true);
        UpdatePinnedDisplay();
    }

    private void PrepareShutdown(bool unpinAll)
    {
        if (unpinAll && _pinController.HasPinnedWindows)
        {
            PinFeedback.PlayUnpinSound();
            _pinController.ReleaseAllPins(restoreBounds: true, forceFullscreen: true);
            UpdatePinnedDisplay();
        }

        UnregisterHotkeyHook();
    }

    private IEnumerable<IntPtr> GetExcludedHandles()
    {
        yield return new WindowInteropHelper(this).Handle;

        foreach (IntPtr handle in _pinController.PinnedWindows.Select(window => window.Handle))
        {
            yield return handle;
        }
    }

    private IntPtr? GetSelectedPinnedHandle()
    {
        if (PinnedWindowsList.SelectedItem is PinnedWindowEntry entry)
        {
            return entry.Handle;
        }

        return _pinController.PinnedWindows.LastOrDefault()?.Handle;
    }

    private void UpdatePinnedDisplay()
    {
        IReadOnlyList<PinnedWindowEntry> pinned = _pinController.PinnedWindows;
        PinnedWindowsList.ItemsSource = pinned;

        if (pinned.Count == 0)
        {
            PinnedCountText.Text = "0 pinned";
        }
        else if (pinned.Count == 1)
        {
            PinnedCountText.Text = "1 pinned";
        }
        else
        {
            PinnedCountText.Text = $"{pinned.Count} pinned";
        }

        UpdateControlStates();
    }

    private void UpdateControlStates()
    {
        bool hasPinned = _pinController.HasPinnedWindows;
        bool hasSelection = PinnedWindowsList.SelectedItem is not null;

        UnpinButton.IsEnabled = hasPinned;
        SmallerButton.IsEnabled = hasPinned && (hasSelection || _pinController.PinnedWindows.Count == 1);
        LargerButton.IsEnabled = hasPinned && (hasSelection || _pinController.PinnedWindows.Count == 1);
        PositionPadPanel.IsEnabled = hasPinned && (hasSelection || _pinController.PinnedWindows.Count == 1);
        PositionPadPanel.Opacity = PositionPadPanel.IsEnabled ? 1.0 : 0.45;
    }

    private static bool IsValidExternalWindow(IntPtr handle, IntPtr selfHandle)
    {
        handle = Win32Native.ResolveRootWindow(handle);

        if (handle == IntPtr.Zero || handle == selfHandle || !Win32Native.IsWindow(handle))
        {
            return false;
        }

        if (!Win32Native.IsWindowVisible(handle))
        {
            return false;
        }

        int exStyle = (int)Win32Native.GetWindowLongPtr(handle, Win32Native.GwlExstyle);
        return (exStyle & Win32Native.WsExToolwindow) == 0;
    }

    public void RequestShutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        PrepareShutdown(unpinAll: true);
        Application.Current.Shutdown();
    }
}
