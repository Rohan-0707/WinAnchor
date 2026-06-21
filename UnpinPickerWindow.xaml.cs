using System.Windows;
using System.Windows.Controls;
using InScreenApp.Models;
using InScreenApp.Services;

namespace InScreenApp;

public partial class UnpinPickerWindow : Window
{
    private readonly PinnedWindowController _controller;

    public IReadOnlyList<PinnedWindowEntry> SelectedWindows { get; private set; } = [];

    public UnpinPickerWindow(PinnedWindowController controller)
    {
        _controller = controller;
        InitializeComponent();

        PinnedList.ItemsSource = _controller.PinnedWindows;
        PinnedList.SelectionChanged += (_, _) =>
            UnpinSelectedButton.IsEnabled = PinnedList.SelectedItems.Count > 0;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UnpinSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = PinnedList.SelectedItems.Cast<PinnedWindowEntry>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        SelectedWindows = selected;
        DialogResult = true;
        Close();
    }
}
