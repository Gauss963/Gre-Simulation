using GreSimulation.App.Controls;
using GreSimulation.App.Exam;
using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace GreSimulation.App.Pages;

/// <summary>Practice Tests (DashboardView.swift): start a full test or a single measure, see the latest result.</summary>
public sealed partial class DashboardPage : Page
{
    private GreServices? _services;
    private readonly List<FrameworkElement> _sessionCards = [];
    private int _sessionColumns;

    public DashboardPage()
    {
        InitializeComponent();
        HeroTitle.Text = Dashboard.HeroTitle;
        HeroText.Text = Dashboard.HeroText;
        StartFullButton.Content = Dashboard.StartFullLength;
        HeroClock.Text = Dashboard.HeroClock;
        HeroClockCaption.Text = Dashboard.HeroClockCaption;
        LatestTitle.Text = Dashboard.LatestResultTitle;
        ChooseTitle.Text = Dashboard.ChooseSessionTitle;
        FormatTitle.Text = Dashboard.FormatTitle;
        AccuracyNotice.Text = Dashboard.AccuracyNotice;
        BuildFormatTable();
        ActualThemeChanged += (_, _) =>
        {
            if (_services is not null) BuildSessionCards();
        };
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_services is not null) return;
        try
        {
            _services = await AppServices.Ready;
        }
        catch (Exception exception)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            LoadErrorBar.Message = exception.Message;
            LoadErrorBar.IsOpen = true;
            return;
        }
        LoadingPanel.Visibility = Visibility.Collapsed;
        ContentScroller.Visibility = Visibility.Visible;
        BankSummaryText.Text = Dashboard.BankSummary(_services.Questions, _services.Vocabulary);
        ShowLatestResult(_services.History.Results.FirstOrDefault());
        BuildSessionCards();
    }

    private void ShowLatestResult(ExamResult? latest)
    {
        if (latest is null)
        {
            LatestCard.Visibility = Visibility.Collapsed;
            return;
        }
        LatestCard.Visibility = Visibility.Visible;
        LatestDate.Text = DisplayDates.AbbreviatedDate(latest.CompletedAt);
        while (LatestPanel.Children.Count > 2) LatestPanel.Children.RemoveAt(LatestPanel.Children.Count - 1);
        foreach (var (title, value) in Dashboard.LatestScoreChips(latest))
        {
            var chip = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            chip.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"], Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            chip.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeights.Bold });
            AutomationProperties.SetName(chip, $"{title} {value}");
            LatestPanel.Children.Add(chip);
        }
    }

    // MARK: - Session cards

    private void BuildSessionCards()
    {
        var dark = Palette.IsDark(this);
        _sessionCards.Clear();
        SessionGrid.Children.Clear();
        foreach (var (mode, _) in Dashboard.SessionCards)
        {
            var (glyph, accent) = mode switch
            {
                ExamMode.FullLength => ("", Tone.Blue),
                ExamMode.VerbalPractice => ("", Tone.Teal),
                ExamMode.QuantitativePractice => ("", Tone.Indigo),
                _ => ("", Tone.Orange),
            };
            var stack = new StackPanel { Spacing = 13 };
            stack.Children.Add(new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(9),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Palette.Brush(accent, dark, 0.10),
                Child = new FontIcon { Glyph = glyph, FontSize = 20, Foreground = Palette.Brush(accent, dark) },
            });
            stack.Children.Add(new TextBlock { Text = mode.Title(), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = mode.Subtitle(), TextWrapping = TextWrapping.Wrap, Foreground = Palette.Brush(Tone.Secondary, dark) });

            var begin = new Button { Content = Dashboard.BeginButton, Padding = new Thickness(18, 8, 18, 8), FontWeight = FontWeights.SemiBold };
            AutomationProperties.SetName(begin, $"{Dashboard.BeginButton} {mode.Title()}");
            begin.Click += (_, _) => Start(mode);

            var layout = new Grid { MinHeight = 170 };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(stack);
            begin.Margin = new Thickness(0, 13, 0, 0);
            Grid.SetRow(begin, 2);
            layout.Children.Add(begin);

            var card = new Border { Child = layout, Style = (Style)Application.Current.Resources["CardStyle"] };
            _sessionCards.Add(card);
            SessionGrid.Children.Add(card);
        }
        _sessionColumns = 0;
        ArrangeSessionCards(SessionGrid.ActualWidth);
    }

    private void OnSessionGridSizeChanged(object sender, SizeChangedEventArgs e) => ArrangeSessionCards(e.NewSize.Width);

    /// <summary>LazyVGrid(.adaptive(minimum: 250), spacing: 16): as many columns of at least 250 as fit.</summary>
    private void ArrangeSessionCards(double width)
    {
        if (_sessionCards.Count == 0) return;
        var columns = width <= 0 ? _sessionCards.Count : Math.Clamp((int)((width + 16) / (250 + 16)), 1, _sessionCards.Count);
        if (columns == _sessionColumns) return;
        _sessionColumns = columns;
        SessionGrid.ColumnDefinitions.Clear();
        SessionGrid.RowDefinitions.Clear();
        for (var c = 0; c < columns; c++) SessionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var rows = (_sessionCards.Count + columns - 1) / columns;
        for (var r = 0; r < rows; r++) SessionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < _sessionCards.Count; i++)
        {
            Grid.SetColumn(_sessionCards[i], i % columns);
            Grid.SetRow(_sessionCards[i], i / columns);
        }
    }

    // MARK: - Format overview

    private void BuildFormatTable()
    {
        for (var c = 0; c < 3; c++) FormatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var secondary = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

        void AddRow(int row, IReadOnlyList<string> cells, bool header)
        {
            FormatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var c = 0; c < cells.Count; c++)
            {
                var text = new TextBlock { Text = cells[c], FontSize = 13, TextWrapping = TextWrapping.Wrap };
                if (header) text.FontWeight = FontWeights.SemiBold;
                else if (c > 0) text.Foreground = secondary;
                Grid.SetRow(text, row);
                Grid.SetColumn(text, c);
                FormatGrid.Children.Add(text);
            }
        }

        AddRow(0, Dashboard.FormatHeaders, header: true);
        FormatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var divider = new Rectangle { Height = 1, Fill = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] };
        Grid.SetRow(divider, 1);
        Grid.SetColumnSpan(divider, 3);
        FormatGrid.Children.Add(divider);
        var rowIndex = 2;
        foreach (var (measure, first, second) in Dashboard.FormatRows)
            AddRow(rowIndex++, [measure, first, second], header: false);
    }

    // MARK: - Starting a test

    private void OnStartFullClick(object sender, RoutedEventArgs e) => Start(ExamMode.FullLength);

    private void Start(ExamMode mode)
    {
        if (_services is null) return;
        try
        {
            var session = _services.StartExam(mode);
            MainWindow.Current.ShowExam(new ExamView(session, _services));
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            LoadErrorBar.Title = "The test could not be started";
            LoadErrorBar.Message = exception.Message;
            LoadErrorBar.IsOpen = true;
        }
    }
}
