using GreSimulation.App.Services;
using Microsoft.UI.Xaml;

namespace GreSimulation.App;

public partial class App : Application
{
    private MainWindow? window;

    public App()
    {
        InitializeComponent();
        // Missing resources are otherwise reported only as "XAML parsing failed."
        DebugSettings.XamlResourceReferenceFailed += (_, e) => CrashLog.Write(new InvalidOperationException("XAML resource reference failed: " + e.Message));
        DebugSettings.BindingFailed += (_, e) => CrashLog.Write(new InvalidOperationException("Binding failed: " + e.Message));
        UnhandledException += (_, e) =>
        {
            CrashLog.Write(e.Exception);
            if (SmokeTest.Enabled)
            {
                SmokeTest.RecordFailure("unhandled", e.Exception);
                e.Handled = true;
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppServices.Start(); // loads the question bank off the UI thread while the window opens
        window = new MainWindow();
        window.Closed += (_, _) => AppServices.Shutdown();
        window.Activate();

        SmokeTest.Configure(Environment.GetCommandLineArgs());
        if (SmokeTest.Enabled) _ = SmokeTest.RunAsync(window);
    }
}
