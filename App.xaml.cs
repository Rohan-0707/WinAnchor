using System.Windows;
using InScreenApp.Services;

namespace InScreenApp;

public partial class App : Application
{
    internal static PinnedWindowController PinController { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        SessionEnding += (_, _) => PinController.ReleaseAllPins(restoreBounds: true);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => PinController.ReleaseAllPins(restoreBounds: true);
        Exit += (_, _) => PinController.ReleaseAllPins(restoreBounds: true);
    }
}
