using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace GreSimulation.App.Controls;

/// <summary>
/// The score report (ScoreReportView.swift), shown full-window after a test or reopened from Score History.
/// The host decides what happens on <see cref="CloseRequested"/> (raised by the Done / Save &amp; Close button).
/// </summary>
public sealed partial class ScoreReportView : UserControl
{
    private readonly ScoreReport _report;

    /// <summary>A saved result reopened from Score History ("Saved score report", button "Close").</summary>
    public ScoreReportView(ExamResult result) : this(SavedReport(result))
    {
    }

    /// <summary>A prepared report, for example <see cref="GreServices.ReportForCompletedExam"/> ("Save &amp; Close").</summary>
    public ScoreReportView(ScoreReport report)
    {
        _report = report;
        InitializeComponent();

        HeadingText.Text = report.Heading;
        SubtitleText.Text = report.Subtitle();
        EstimateInfo.Message = ScoreReport.EstimateNotice;
        DoneButton.Content = report.DoneTitle;
        AutomationProperties.SetName(DoneButton, report.DoneTitle);
        if (report.DoneTitle == HistoryScreen.SavedReportDone)
            DoneButton.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = VirtualKey.Escape });

        if (report.WritingSummary is { } summary)
        {
            WritingCard.Visibility = Visibility.Visible;
            WritingTitle.Text = ScoreReport.WritingFeedbackTitle;
            WritingText.Text = summary;
        }
        MethodologyTitle.Text = ScoreReport.MethodologyTitle;
        MethodologyScores.Text = ScoreReport.MethodologyScores;
        MethodologyWriting.Text = ScoreReport.MethodologyWriting;

        _sectionsHeaderCount = SectionsPanel.Children.Count;
        _reviewHeaderCount = ReviewPanel.Children.Count;
        BuildDynamicContent();
        ActualThemeChanged += (_, _) => BuildDynamicContent();
        Loaded += (_, _) =>
        {
            // The theme is only known once the view is in the window.
            if (Palette.IsDark(this) != _dark) BuildDynamicContent();
        };
    }

    /// <summary>Raised by the Done / Save &amp; Close button.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The report being shown.</summary>
    public ScoreReport Report => _report;

    private readonly int _sectionsHeaderCount;
    private readonly int _reviewHeaderCount;
    private bool _dark;

    private static ScoreReport SavedReport(ExamResult result) => AppServices.IsReady
        ? AppServices.Services.ReportForSavedResult(result)
        : new ScoreReport(result, null, HistoryScreen.SavedReportHeading, HistoryScreen.SavedReportDone);

    private void OnDoneClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void BuildDynamicContent()
    {
        _dark = Palette.IsDark(this);
        BuildScoreCards();
        BuildSections();
        BuildReview();
    }

    // MARK: - Score cards

    private void BuildScoreCards()
    {
        ScoreCardsGrid.Children.Clear();
        ScoreCardsGrid.ColumnDefinitions.Clear();
        var cards = _report.ScoreCards;
        for (var i = 0; i < cards.Count; i++)
        {
            ScoreCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var card = cards[i];
            var stack = new StackPanel { Spacing = 9 };

            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = Text(card.Title, 14, FontWeights.SemiBold, Tone.Ink);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            var range = Text(card.Range, 12, FontWeights.Normal, Tone.Secondary);
            Grid.SetColumn(range, 1);
            titleRow.Children.Add(title);
            titleRow.Children.Add(range);
            stack.Children.Add(titleRow);

            stack.Children.Add(Text(card.ScoreText, 42, FontWeights.Bold, Tone.Blue));
            stack.Children.Add(Text(card.RawText, 12, FontWeights.Normal, Tone.Secondary));
            if (card.PercentileText is { } percentile) stack.Children.Add(Text(percentile, 12, FontWeights.Bold, Tone.Teal));

            var border = Card(stack);
            AutomationProperties.SetName(border, $"{card.Title}: {card.ScoreText}. {card.RawText}. {card.PercentileText}");
            Grid.SetColumn(border, i);
            ScoreCardsGrid.Children.Add(border);
        }
        ScoreCardsGrid.Visibility = cards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // MARK: - Section performance

    private void BuildSections()
    {
        while (SectionsPanel.Children.Count > _sectionsHeaderCount) SectionsPanel.Children.RemoveAt(SectionsPanel.Children.Count - 1);
        foreach (var row in _report.SectionRows)
        {
            var stack = new StackPanel { Spacing = 8 };
            var line = new Grid { ColumnSpacing = 8 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = Text(row.Title, 14, FontWeights.SemiBold, Tone.Ink);
            title.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(title);
            if (row.RouteText is { } route)
            {
                var badge = new Border
                {
                    Background = Palette.Brush(Tone.BrightBlue, _dark, 0.1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 4, 8, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = Text(route, 12, FontWeights.Bold, Tone.Blue),
                };
                Grid.SetColumn(badge, 1);
                line.Children.Add(badge);
            }
            var score = row.HasScore
                ? Text(row.ScoreText, 15, FontWeights.SemiBold, Tone.Ink)
                : Text(row.ScoreText, 12, FontWeights.SemiBold, Tone.Teal);
            score.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(score, 3);
            line.Children.Add(score);
            stack.Children.Add(line);

            if (row.HasScore)
            {
                var progress = new ProgressBar { Minimum = 0, Maximum = 1, Value = row.Fraction, Foreground = Palette.Brush(Tone.Teal, _dark) };
                AutomationProperties.SetName(progress, $"{row.Title}: {row.ScoreText}");
                stack.Children.Add(progress);
            }

            SectionsPanel.Children.Add(new Border
            {
                Child = stack,
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(8),
                Background = Palette.Brush(Tone.Canvas, _dark, 0.75),
            });
        }
    }

    // MARK: - Answer review

    private void BuildReview()
    {
        while (ReviewPanel.Children.Count > _reviewHeaderCount) ReviewPanel.Children.RemoveAt(ReviewPanel.Children.Count - 1);
        if (!_report.HasReviewRecords)
        {
            var empty = new StackPanel { Spacing = 8, MinHeight = 180, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(0, 24, 0, 24) };
            empty.Children.Add(new FontIcon { Glyph = "", FontSize = 40, Foreground = Palette.Brush(Tone.Secondary, _dark) });
            var title = Text(ScoreReport.ReviewUnavailableTitle, 18, FontWeights.SemiBold, Tone.Ink);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            var message = Text(ScoreReport.ReviewUnavailableMessage, 13, FontWeights.Normal, Tone.Secondary);
            message.HorizontalAlignment = HorizontalAlignment.Center;
            message.TextAlignment = TextAlignment.Center;
            empty.Children.Add(title);
            empty.Children.Add(message);
            ReviewPanel.Children.Add(empty);
            return;
        }

        ReviewPanel.Children.Add(Text(ScoreReport.ReviewIntro, 13, FontWeights.Normal, Tone.Secondary));
        foreach (var group in _report.ReviewGroups)
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(Text(group.Title, 15, FontWeights.SemiBold, Tone.Ink));
            var count = Text(group.CorrectText, 12, FontWeights.Normal, Tone.Secondary);
            count.VerticalAlignment = VerticalAlignment.Center;
            count.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(count, 1);
            header.Children.Add(count);

            var expander = new Expander
            {
                Header = header,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(expander, $"{group.Title}, {group.CorrectText}");
            // Items (with figures) are built the first time the group opens, like a lazy DisclosureGroup.
            var built = false;
            expander.Expanding += (_, _) =>
            {
                if (built) return;
                built = true;
                var items = new StackPanel { Spacing = 10 };
                foreach (var item in group.Items) items.Children.Add(BuildReviewItem(item));
                expander.Content = items;
            };
            ReviewPanel.Children.Add(expander);
        }
    }

    private FrameworkElement BuildReviewItem(ReviewItem item)
    {
        var question = item.Question;
        var stack = new StackPanel { Spacing = 8 };
        var indent = new Thickness(27, 0, 0, 0);

        stack.Children.Add(Text(item.Heading, 11, FontWeights.SemiBold, Tone.Blue));

        var top = new Grid { ColumnSpacing = 8 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(19) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new FontIcon
        {
            Glyph = item.IsCorrect ? "" : "",
            FontSize = 16,
            Foreground = Palette.Brush(item.IsCorrect ? Tone.Teal : Tone.Red, _dark),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
        };
        AutomationProperties.SetName(icon, item.IsCorrect ? "Correct" : "Incorrect");
        top.Children.Add(icon);
        var prompt = Text(question.Prompt, 13, FontWeights.SemiBold, Tone.Ink);
        prompt.MaxLines = 3;
        prompt.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(prompt, 1);
        top.Children.Add(prompt);
        var area = Text(question.ContentArea, 11, FontWeights.Normal, Tone.Secondary);
        area.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(area, 2);
        top.Children.Add(area);
        stack.Children.Add(top);

        var explanation = Text(question.Explanation, 12, FontWeights.Normal, Tone.Secondary);
        explanation.Margin = indent;
        explanation.IsTextSelectionEnabled = true;
        stack.Children.Add(explanation);

        if (item.GuessingTip is { } tip)
        {
            var tipGrid = new Grid { ColumnSpacing = 6 };
            tipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tipGrid.Children.Add(new FontIcon
            {
                Glyph = "",
                FontSize = 13,
                Foreground = Palette.Brush(Tone.Orange, _dark),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
            });
            var tipText = new StackPanel { Spacing = 2 };
            tipText.Children.Add(Text(ScoreReport.GuessingTipHeading, 12, FontWeights.SemiBold, Tone.Ink));
            var body = Text(tip, 12, FontWeights.Normal, Tone.Secondary);
            body.IsTextSelectionEnabled = true;
            tipText.Children.Add(body);
            Grid.SetColumn(tipText, 1);
            tipGrid.Children.Add(tipText);
            stack.Children.Add(new Border
            {
                Child = tipGrid,
                Padding = new Thickness(8),
                Margin = indent,
                CornerRadius = new CornerRadius(6),
                Background = Palette.Brush(Tone.Orange, _dark, 0.08),
            });
        }

        if (question.Figure is { } figure)
        {
            var frame = new Grid { Margin = new Thickness(27, 6, 0, 6) };
            frame.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 680 });
            frame.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            frame.Children.Add(new QuantFigureView(figure));
            stack.Children.Add(frame);
        }

        var answers = new StackPanel { Spacing = 3, Margin = indent };
        var answerTone = item.IsCorrect ? Tone.Teal : Tone.Ink;
        answers.Children.Add(Text(item.YourAnswerText, 12, FontWeights.Normal, answerTone));
        answers.Children.Add(Text(item.CorrectAnswerText, 12, FontWeights.SemiBold, answerTone));
        stack.Children.Add(answers);

        if (item.SourceText is { } source)
        {
            var sourceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = indent };
            sourceRow.Children.Add(new FontIcon
            {
                Glyph = item.IsAuthorizedSourceItem ? "" : "",
                FontSize = 11,
                Foreground = Palette.Brush(Tone.Blue, _dark),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sourceRow.Children.Add(Text(source, 11, FontWeights.Normal, Tone.Blue));
            stack.Children.Add(sourceRow);
        }

        return new Border
        {
            Child = stack,
            Padding = new Thickness(11),
            CornerRadius = new CornerRadius(6),
            Background = Palette.Brush(Tone.Input, _dark),
        };
    }

    // MARK: - Helpers

    private TextBlock Text(string text, double size, Windows.UI.Text.FontWeight weight, Tone tone) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = Palette.Brush(tone, _dark),
        TextWrapping = TextWrapping.Wrap,
    };

    private static Border Card(UIElement child) => new()
    {
        Child = child,
        Style = (Style)Application.Current.Resources["CardStyle"],
    };
}
