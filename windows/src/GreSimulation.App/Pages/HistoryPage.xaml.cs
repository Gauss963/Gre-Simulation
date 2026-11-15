using System.ComponentModel;
using GreSimulation.App.Controls;
using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GreSimulation.App.Pages;

/// <summary>
/// Score History: every saved result (newest first) with its scores and section routes, "Review answers" to
/// reopen the saved score report, and Clear history. Port of HistoryView (LibraryViews.swift).
/// </summary>
public sealed partial class HistoryPage : Page
{
    private GreServices? services;

    public HistoryPage()
    {
        InitializeComponent();
        HeadingText.Text = HistoryScreen.Title;
        SubheadingText.Text = HistoryScreen.Subtitle;
        ClearButtonText.Text = HistoryScreen.ClearButton;
        EmptyTitle.Text = HistoryScreen.EmptyTitle;
        EmptyMessage.Text = HistoryScreen.EmptyMessage;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (services is not null) return;
        GreServices loaded;
        try
        {
            loaded = await AppServices.Ready;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return;
        }
        if (services is not null) return;
        services = loaded;
        services.History.PropertyChanged += OnHistoryChanged;
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        Reload();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (services is not null) services.History.PropertyChanged -= OnHistoryChanged;
    }

    private void OnHistoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess) Reload();
        else DispatcherQueue.TryEnqueue(Reload);
    }

    private void Reload()
    {
        if (services is null) return;
        var results = services.History.Results;
        var hasResults = results.Count > 0;
        ClearButton.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasResults ? Visibility.Collapsed : Visibility.Visible;
        ResultsList.ItemsSource = results.Select(result => new HistoryCardItem(result)).ToList();
    }

    private async void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (services is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = HistoryScreen.ClearTitle,
            PrimaryButtonText = HistoryScreen.ClearConfirm,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) services.History.Clear();
    }

    private void OnReviewAnswersClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HistoryCardItem item && item.Card.CanReviewAnswers) OpenReport(item.Card.Result);
    }

    /// <summary>Shows the saved score report over the page ("Saved score report" / "Close" on the Mac).</summary>
    private void OpenReport(ExamResult result)
    {
        ReportPresenter.Content = ReportHost.Create(result, CloseReport);
        ReportOverlay.Visibility = Visibility.Visible;
        PageScroller.Visibility = Visibility.Collapsed;
    }

    private void CloseReport()
    {
        ReportOverlay.Visibility = Visibility.Collapsed;
        ReportPresenter.Content = null;
        PageScroller.Visibility = Visibility.Visible;
    }
}

/// <summary>A score tile: title, value, and subtitle (e.g. "Verbal", "158", "18/27 correct").</summary>
public sealed class HistoryMetric(string title, string value, string subtitle)
{
    public string Title { get; } = title;
    public string Value { get; } = value;
    public string Subtitle { get; } = subtitle;
}

/// <summary>A section chip: "Verbal 2" then "11/15 · Hard".</summary>
public sealed class HistoryChip(string name, string detail)
{
    public string Name { get; } = name;
    public string Detail { get; } = detail;
}

/// <summary>Display data for one saved result, from Core's <see cref="HistoryCard"/>.</summary>
public sealed class HistoryCardItem
{
    public HistoryCardItem(ExamResult result)
    {
        Card = new HistoryCard(result);
        Metrics = Card.Metrics.Select(metric => new HistoryMetric(metric.Title, metric.Value, metric.Subtitle)).ToList();
        Chips = Card.SectionChips
            .Select(chip => new HistoryChip(chip.Name, "  " + chip.Score + (chip.Route is null ? "" : "  " + chip.Route)))
            .ToList();
    }

    public HistoryCard Card { get; }
    public string Title => Card.Title;
    public string DateText => Card.DateText();
    public string DurationText => Card.DurationText;
    public List<HistoryMetric> Metrics { get; }
    public List<HistoryChip> Chips { get; }
    public Visibility ReviewVisibility => Card.CanReviewAnswers ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ScoreOnlyVisibility => Card.CanReviewAnswers ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SectionsVisibility => Chips.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
}
