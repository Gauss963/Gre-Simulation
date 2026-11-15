using Microsoft.UI.Xaml;
using GreSimulation.Core;

namespace GreSimulation.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Gre Simulation {AppInfo.Version}";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
    }
}
