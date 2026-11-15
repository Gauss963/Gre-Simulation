using System.Globalization;
using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using ResourceTexts = GreSimulation.Core.ResourcesPage;

namespace GreSimulation.App.Pages;

/// <summary>
/// Official Resources: the offline content bank summary, official ETS and source links (opened in the
/// default browser), and the score-transparency notice. Port of ResourcesView (LibraryViews.swift).
/// </summary>
public sealed partial class ResourcesPage : Page
{
    private bool loaded;

    public ResourcesPage()
    {
        InitializeComponent();
        HeadingText.Text = ResourceTexts.Title;
        SubheadingText.Text = ResourceTexts.Subtitle;
        BankTitle.Text = ResourceTexts.BankTitle;
        BankFootnote.Text = ResourceTexts.BankFootnote;
        TransparencyTitle.Text = ResourceTexts.TransparencyTitle;
        TransparencyText.Text = ResourceTexts.TransparencyText;
        TrademarkText.Text = ResourceTexts.TrademarkText;
        Sections.ItemsSource = ResourceTexts.Sections.Select(section => new ResourceSectionCard(section)).ToList();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (loaded) return;
        GreServices services;
        try
        {
            services = await AppServices.Ready;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return;
        }
        if (loaded) return;
        loaded = true;
        BankMetrics.ItemsSource = ResourceTexts.BankMetrics(services.Questions, services.Vocabulary)
            .Select(metric => new ResourceMetric(metric.Title, metric.Value, metric.Detail))
            .ToList();
        SourceRows.ItemsSource = services.Questions.SourceSummary
            .Select(item => new SourceSummaryRow(item.Title, item.Count.ToString(CultureInfo.CurrentCulture)))
            .ToList();
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private async void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}

public sealed class ResourceMetric(string title, string value, string detail)
{
    public string Title { get; } = title;
    public string Value { get; } = value;
    public string Detail { get; } = detail;
}

public sealed class SourceSummaryRow(string title, string count)
{
    public string Title { get; } = title;
    public string Count { get; } = count;
}

public sealed class ResourceLinkRow(ResourceLink link, bool isLast)
{
    public string Title { get; } = link.Title;
    public string Detail { get; } = link.Detail;
    public string Url { get; } = link.Url;
    public Visibility DividerVisibility { get; } = isLast ? Visibility.Collapsed : Visibility.Visible;
}

/// <summary>A titled list of links (the Mac's resourceSection).</summary>
public sealed class ResourceSectionCard
{
    public ResourceSectionCard(ResourceSection section)
    {
        Title = section.Title;
        Glyph = section.MacSymbol switch
        {
            "clock.badge.checkmark" => "", // Recent (clock)
            "function" => "",              // Calculator
            "text.book.closed" => "",      // Dictionary
            _ => "",
        };
        Links = section.Links.Select((link, index) => new ResourceLinkRow(link, index == section.Links.Count - 1)).ToList();
    }

    public string Title { get; }
    public string Glyph { get; }
    public List<ResourceLinkRow> Links { get; }
}
