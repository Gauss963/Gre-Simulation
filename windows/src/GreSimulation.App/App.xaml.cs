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
        window = new MainWindow();
        window.Activate();
    }
}
