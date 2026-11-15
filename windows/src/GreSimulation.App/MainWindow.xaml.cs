using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using GreSimulation.Core;

namespace GreSimulation.App;

public sealed partial class MainWindow : Window
{
    /// <summary>The app's only window. Pages use it to start and leave practice tests.</summary>
    public static MainWindow Current { get; private set; } = null!;

    private static readonly Dictionary<string, Type> Pages = new()
    {
        ["dashboard"] = typeof(Pages.DashboardPage),
        ["vocabulary"] = typeof(Pages.VocabularyPage),
        ["history"] = typeof(Pages.HistoryPage),
        ["resources"] = typeof(Pages.ResourcesPage),
    };

    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 760));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 640;
        }
        BrandSubtitle.Text = $"2026 · Windows · {AppInfo.Version}";
        Navigate("dashboard");
    }

    /// <summary>Selects a sidebar page by its tag (for example "history").</summary>
    public void Navigate(string tag, object? parameter = null)
    {
        var item = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (item is null) return;
        if (!ReferenceEquals(Nav.SelectedItem, item))
        {
            pendingParameter = parameter;
            Nav.SelectedItem = item; // raises SelectionChanged, which navigates
        }
        else
        {
            ContentFrame.Navigate(Pages[tag], parameter, new SuppressNavigationTransitionInfo());
        }
    }

    private object? pendingParameter;

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag } && Pages.TryGetValue(tag, out var page))
        {
            ContentFrame.Navigate(page, pendingParameter, new EntranceNavigationTransitionInfo());
            pendingParameter = null;
        }
    }

    /// <summary>Shows a practice test full-window, hiding the sidebar (like the Mac app's exam mode).</summary>
    public void ShowExam(UIElement examView)
    {
        ExamHost.Content = examView;
        ExamHost.Visibility = Visibility.Visible;
        Nav.Visibility = Visibility.Collapsed;
    }

    /// <summary>Leaves the practice test and returns to the sidebar, optionally opening a page.</summary>
    public void CloseExam(string? navigateToTag = null)
    {
        ExamHost.Content = null;
        ExamHost.Visibility = Visibility.Collapsed;
        Nav.Visibility = Visibility.Visible;
        if (navigateToTag is not null) Navigate(navigateToTag);
    }
}
