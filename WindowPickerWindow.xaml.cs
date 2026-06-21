using System.Windows;
using System.Windows.Controls;
using InScreenApp.Models;
using InScreenApp.Services;

namespace InScreenApp;

public partial class WindowPickerWindow : Window
{
    private readonly Func<IEnumerable<IntPtr>> _excludeHandles;

    public WindowInfo? SelectedWindow { get; private set; }

    public WindowPickerWindow(Func<IEnumerable<IntPtr>> excludeHandles)
    {
        _excludeHandles = excludeHandles;
        InitializeComponent();
        WindowList.SelectionChanged += (_, _) =>
            PinButton.IsEnabled = WindowList.SelectedItem is WindowInfo;
        Loaded += (_, _) => RefreshWindows();
    }

    private void RefreshWindows()
    {
        WindowList.ItemsSource = WindowEnumerator.GetOpenWindows(_excludeHandles());
        PinButton.IsEnabled = false;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshWindows();

    private void PinButton_Click(object sender, RoutedEventArgs e) => PinSelected();

    private void WindowList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => PinSelected();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PinSelected()
    {
        if (WindowList.SelectedItem is not WindowInfo selected)
        {
            return;
        }

        SelectedWindow = selected;
        DialogResult = true;
        Close();
    }
}
