using System.ComponentModel;
using System.Globalization;
using GreSimulation.App.Controls;
using GreSimulation.App.Exam;
using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GreSimulation.App.Pages;

/// <summary>
/// Verbal Practice and Quant Practice: one question at a time with Check Answer (Ctrl+Enter), the key, the explanation
/// (and for Verbal the guessing tip), accuracy by question type or content area, an answer history that reopens each
/// attempt, and for Quant the floating calculator. Navigate with the measure as the parameter.
/// Port of PracticeDrillView (PracticeDrillView.swift).
/// </summary>
public sealed partial class PracticePage : Page
{
    private static readonly QuestionDifficulty?[] Difficulties = [null, QuestionDifficulty.Easy, QuestionDifficulty.Medium, QuestionDifficulty.Hard];

    private GREMeasure measure = GREMeasure.Verbal;
    private GreServices? services;
    private PracticeDrillSession? session;
    private DrillQuestionView? questionView;
    private CalculatorPanel? calculator;
    private bool refreshing;
    private bool showsAllHistory;
    private string? renderedKey;

    public PracticePage()
    {
        InitializeComponent();
        DifficultyBox.ItemsSource = Difficulties.Select(d => d?.Title() ?? "All").ToList();
        AreaBox.ItemsSource = PracticeDrillSession.AreaChoices.Select(a => a ?? "All areas").ToList();
        SetContentVisible(false);
        Loaded += OnLoaded;
        ActualThemeChanged += (_, _) => { renderedKey = null; Refresh(); };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is GREMeasure parameter) measure = parameter;
        var isQuant = measure == GREMeasure.Quantitative;
        TypeBox.ItemsSource = DrillTypeFilter.Menu(measure).Select(f => f.Title).ToList();
        AreaBox.Visibility = Show(isQuant);
        CalculatorButton.Visibility = Show(isQuant);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (services is not null) return;
        try
        {
            services = await AppServices.Ready;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return;
        }
        session = services.CreatePracticeDrill(measure);
        session.PropertyChanged += OnSessionChanged;
        HeadingText.Text = session.Title;
        SubheadingText.Text = session.Subtitle;
        AccuracyCaption.Text = session.AccuracyCaption;
        FocusSwitch.Header = session.FocusTitle;
        session.OnAppear();
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        Refresh();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (session is not null) session.PropertyChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void SetContentVisible(bool visible)
    {
        var show = Show(visible);
        QuestionCard.Visibility = show;
        ActionsCard.Visibility = show;
        EmptyCard.Visibility = Visibility.Collapsed;
        HistoryCard.Visibility = Visibility.Collapsed;
        LastTimeText.Visibility = Visibility.Collapsed;
    }

    private void Refresh()
    {
        if (session is not { } drill || services is null) return;
        refreshing = true;
        try
        {
            TypeBox.SelectedIndex = DrillTypeFilter.Menu(measure).ToList().IndexOf(drill.Filter);
            AreaBox.SelectedIndex = PracticeDrillSession.AreaChoices.ToList().IndexOf(drill.Area);
            DifficultyBox.SelectedIndex = Array.IndexOf(Difficulties, drill.Difficulty);
            FocusSwitch.IsOn = drill.FocusWeak;
            FocusSwitch.IsEnabled = drill.CanFocusWeak;
            ToolTipService.SetToolTip(FocusSwitch, drill.FocusHelp);
            MissedSwitch.IsOn = drill.MissedOnly;

            PracticedText.Text = drill.PracticedText;
            AccuracyText.Text = drill.AccuracyText;
            SessionText.Text = drill.SessionText;
            MissedText.Text = drill.MissedText;
            RefreshAccuracy(drill);
            RefreshCalculator(drill);

            var question = drill.Current;
            QuestionCard.Visibility = Show(question is not null);
            ActionsCard.Visibility = Show(question is not null);
            EmptyCard.Visibility = Show(question is null);
            EmptyTitle.Text = drill.EmptyTitle;
            EmptyText.Text = drill.EmptyText;
            LastTimeText.Text = drill.LastTimeText ?? "";
            LastTimeText.Visibility = Show(drill.LastTimeText is not null);

            if (question is not null)
            {
                var dark = Palette.IsDark(this);
                // Typing a Numeric Entry does not rebuild the question (the boxes keep focus); their text is synced.
                var key = $"{question.Id}|{drill.IsCorrect}|{string.Join(";", drill.Answer.Selections.Select(p => p.Key + "=" + string.Join(",", p.Value.Order())))}|{dark}";
                if (key != renderedKey)
                {
                    var isNewQuestion = renderedKey?.Split('|')[0] != question.Id;
                    renderedKey = key;
                    questionView = new DrillQuestionView(question, drill.Answer, drill.IsCorrect, services.Questions.GuessingTip(question), dark, drill);
                    QuestionHost.Children.Clear();
                    QuestionHost.Children.Add(questionView.View);
                    if (isNewQuestion) PageScroller.ChangeView(null, 0, null, true);
                }
                questionView?.SyncNumericText(drill);

                if (drill.IsCorrect is { } correct)
                {
                    ActionText.Text = correct ? "✓ Correct" : "✕ Incorrect";
                    ActionText.FontSize = 20;
                    ActionText.FontWeight = FontWeights.Bold;
                    ActionText.Foreground = Palette.Brush(this, correct ? Tone.Teal : Tone.Red);
                    SecondaryButton.Content = "Try Again";
                    PrimaryButton.Content = "Next Question";
                    PrimaryButton.IsEnabled = true;
                }
                else
                {
                    ActionText.Text = drill.ActionHint;
                    ActionText.FontSize = 12;
                    ActionText.FontWeight = FontWeights.Normal;
                    ActionText.Foreground = Palette.Brush(this, Tone.Secondary);
                    SecondaryButton.Content = "Skip";
                    PrimaryButton.Content = "Check Answer";
                    PrimaryButton.IsEnabled = drill.CanCheck;
                }
            }
            else
            {
                renderedKey = null;
                questionView = null;
            }
            RefreshHistory(drill);
        }
        finally
        {
            refreshing = false;
        }
    }

    private void RefreshCalculator(PracticeDrillSession drill)
    {
        CalculatorButtonText.Text = drill.ShowsCalculator ? "Hide Calculator" : "Calculator";
        if (drill.ShowsCalculator)
        {
            if (calculator is null)
            {
                calculator = new CalculatorPanel(drill);
                OverlayCanvas.Children.Add(calculator);
                calculator.UpdateLayout();
                Canvas.SetLeft(calculator, Math.Max(0, OverlayCanvas.ActualWidth - calculator.Width - 30));
                Canvas.SetTop(calculator, 90);
            }
            calculator.Visibility = Visibility.Visible;
            calculator.Refresh();
        }
        else if (calculator is not null)
        {
            calculator.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshAccuracy(PracticeDrillSession drill)
    {
        var entries = drill.Store.AccuracyByCategory.OrderBy(a => a.Accuracy).ToList();
        AccuracyPanel.Visibility = Show(entries.Count > 0);
        AccuracyChips.Children.Clear();
        var weak = drill.Store.WeakCategories.Select(c => c.Id).ToHashSet();
        foreach (var entry in entries)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new TextBlock { Text = entry.Category.Title, FontSize = 12, FontWeight = FontWeights.SemiBold });
            row.Children.Add(new TextBlock
            {
                Text = $"{Math.Round(entry.Accuracy * 100, MidpointRounding.AwayFromZero):0}%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Palette.Brush(this, entry.Accuracy >= 0.8 ? Tone.Teal : entry.Accuracy >= 0.6 ? Tone.Warning : Tone.Red),
            });
            row.Children.Add(new TextBlock { Text = $"· {entry.Total}", FontSize = 11, Foreground = Palette.Brush(this, Tone.Secondary) });
            var isWeak = weak.Contains(entry.Category.Id);
            AccuracyChips.Children.Add(new Border
            {
                Child = row,
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(12),
                Background = isWeak ? Palette.Brush(this, Tone.Red, 0.08) : Palette.Brush(this, Tone.Input),
                BorderBrush = isWeak ? Palette.Brush(this, Tone.Red, 0.35) : null,
                BorderThickness = new Thickness(isWeak ? 1 : 0),
            });
        }
        WeakestText.Text = drill.WeakestText is { } text ? "💡 " + text : "";
        WeakestText.Visibility = Show(drill.WeakestText is not null);
    }

    private void RefreshHistory(PracticeDrillSession drill)
    {
        var history = drill.Store.History;
        HistoryCard.Visibility = Show(history.Count > 0);
        ShowAllButton.Visibility = Show(history.Count > 10);
        ShowAllButton.Content = showsAllHistory ? "Show recent" : $"Show all {history.Count}";
        HistoryRows.Children.Clear();
        foreach (var record in showsAllHistory ? history : history.Take(10))
        {
            if (drill.Store.Question(record) is not { } question) continue;
            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock
            {
                Text = record.IsCorrect ? "✓" : "✕",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Palette.Brush(this, record.IsCorrect ? Tone.Teal : Tone.Red),
            });
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock { Text = question.Prompt, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 });
            text.Children.Add(new TextBlock { Text = drill.HistoryDetail(record), FontSize = 12, Foreground = Palette.Brush(this, Tone.Secondary) });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            var chevron = new FontIcon { Glyph = "", FontSize = 12, Foreground = Palette.Brush(this, Tone.Secondary) };
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);
            var button = new Button
            {
                Content = grid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                Background = Palette.Brush(this, Tone.Input),
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{(record.IsCorrect ? "Correct" : "Incorrect")}: {question.Prompt}");
            button.Click += (_, _) => ShowReview(record, question);
            HistoryRows.Children.Add(button);
        }
    }

    /// <summary>The Mac review sheet: the attempt as it was checked, with Practice Again and Done.</summary>
    private async void ShowReview(PracticeDrillStore.Record record, GREQuestion question)
    {
        if (services is null || session is null) return;
        var view = new DrillQuestionView(question, record.Answer, record.IsCorrect, services.Questions.GuessingTip(question), Palette.IsDark(this), null);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = record.Date.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture),
            Content = new ScrollViewer { Content = view.View, MaxHeight = 640, Padding = new Thickness(0, 0, 14, 0) },
            PrimaryButtonText = "Practice Again",
            CloseButtonText = "Done",
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ActualTheme,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 1000.0;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) session.Load(question);
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || TypeBox.SelectedIndex < 0) return;
        session.Filter = DrillTypeFilter.Menu(measure)[TypeBox.SelectedIndex];
    }

    private void OnAreaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || AreaBox.SelectedIndex < 0) return;
        session.Area = PracticeDrillSession.AreaChoices[AreaBox.SelectedIndex];
    }

    private void OnDifficultyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || DifficultyBox.SelectedIndex < 0) return;
        session.Difficulty = Difficulties[DifficultyBox.SelectedIndex];
    }

    private void OnFocusToggled(object sender, RoutedEventArgs e)
    {
        if (refreshing || session is null) return;
        session.FocusWeak = FocusSwitch.IsOn;
    }

    private void OnMissedToggled(object sender, RoutedEventArgs e)
    {
        if (refreshing || session is null) return;
        session.MissedOnly = MissedSwitch.IsOn;
    }

    private void OnCalculatorClick(object sender, RoutedEventArgs e)
    {
        if (session is not null) session.ShowsCalculator = !session.ShowsCalculator;
    }

    private void OnPrimaryClick(object sender, RoutedEventArgs e)
    {
        if (session is null) return;
        if (session.IsChecked) session.Next();
        else session.Check();
    }

    private void OnSecondaryClick(object sender, RoutedEventArgs e)
    {
        if (session is null) return;
        if (session.IsChecked) session.TryAgain();
        else session.Next();
    }

    private void OnShowAllClick(object sender, RoutedEventArgs e)
    {
        showsAllHistory = !showsAllHistory;
        if (session is not null) RefreshHistory(session);
    }

    /// <summary>Smoke test: answers the current question (first choices, two for Sentence Equivalence, or a number) and checks it.</summary>
    internal bool AnswerAndCheckForSmokeTest()
    {
        if (session?.Current is not { } question) return false;
        if (question.Format == QuestionFormat.NumericEntry)
        {
            if (PracticeDrillPool.IsFractionEntry(question)) { session.SetFractionPart(0, "1"); session.SetFractionPart(1, "2"); }
            else session.SetNumericEntryText("12.5");
        }
        foreach (var group in question.Groups)
            foreach (var option in group.Options.Take(question.Format == QuestionFormat.SentenceEquivalence ? 2 : 1))
                session.Toggle(group, option);
        session.Check();
        return session.IsChecked;
    }

    /// <summary>Smoke test: the next question.</summary>
    internal void NextForSmokeTest() => session?.Next();

    /// <summary>Smoke test: opens or closes the calculator (Quant).</summary>
    internal void ToggleCalculatorForSmokeTest()
    {
        if (session is not null) session.ShowsCalculator = !session.ShowsCalculator;
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
