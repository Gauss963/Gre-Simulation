using GreSimulation.App.Services;
using Microsoft.UI.Xaml;

namespace GreSimulation.App;

public partial class App : Application
{
    private MainWindow? window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => CrashLog.Write(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppServices.Start(); // loads the question bank off the UI thread while the window opens
        window = new MainWindow();
        window.Closed += (_, _) => AppServices.Shutdown();
        window.Activate();
    }
}
