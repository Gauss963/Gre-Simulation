using System.ComponentModel;
using GreSimulation.App.Controls;
using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace GreSimulation.App.Exam;

/// <summary>
/// The full-window practice test (ExamView.swift <c>ExamContainerView</c>), driven by an <see cref="ExamSession"/>.
/// Show it with <c>MainWindow.Current.ShowExam(new ExamView(session))</c>. Exit Test discards the session;
/// at the end the score report's Save &amp; Close adds the result to Score History and opens that page.
/// </summary>
public sealed partial class ExamView : UserControl
{
    private const string Georgia = "Georgia";

    private readonly ExamSession _session;
    private readonly GreServices _services;
    private readonly DispatcherQueueTimer _timer;
    private CalculatorPanel? _calculator;
    private string? _renderedKey;
    private bool _syncing;
    private bool _left;
    private bool _dark;

    // Open dialog, and the phase/section it belongs to (closed if the section ends underneath it, e.g. time up).
    private ContentDialog? _openDialog;
    private string? _dialogContext;
    private bool _dialogCancelled;

    // Controls of the rendered question, updated in place as the answer changes.
    private readonly List<ChoiceControl> _choices = [];
    private readonly List<(ChoiceOption Option, Button Button)> _sentences = [];
    private ToggleButton? _lastChecked;
    private TextBox? _numericBox;
    private TextBox? _numeratorBox;
    private TextBox? _denominatorBox;
    private TextBox? _essayBox;
    private TextBlock? _wordCountText;

    private sealed record ChoiceControl(ChoiceGroup Group, ChoiceOption Option, ToggleButton Toggle, Border Card);

    /// <summary>Shows <paramref name="session"/> using the app's loaded services.</summary>
    public ExamView(ExamSession session) : this(session, AppServices.Services)
    {
    }

    public ExamView(ExamSession session, GreServices services)
    {
        _session = session;
        _services = services;
        InitializeComponent();

        TimingBeginsText.Text = ExamText.TimingBegins;
        BeginSectionButton.Content = ExamText.BeginSection;

        // The Mac app ticks every 0.5 s; time comes from the session's clock, so a late tick loses nothing.
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(0.5);
        _timer.Tick += (_, _) => _session.Tick();

        _session.PropertyChanged += OnSessionChanged;
        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();
        ActualThemeChanged += (_, _) =>
        {
            _renderedKey = null;
            Refresh();
        };
        OverlayCanvas.SizeChanged += (_, _) =>
        {
            if (_calculator is { Visibility: Visibility.Visible } calculator)
                calculator.MoveTo(Canvas.GetLeft(calculator), Canvas.GetTop(calculator));
        };
        Refresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_left) return;
        _timer.Start();
        _session.Tick();
        if (_session.Phase == ExamPhase.Directions) BeginSectionButton.Focus(FocusState.Programmatic);
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    // MARK: - Rendering

    private void Refresh()
    {
        if (_left) return;
        var phase = _session.Phase;
        CloseDialogIfStale();

        if (phase == ExamPhase.Completed)
        {
            ShowReport();
            return;
        }

        _dark = Palette.IsDark(this);
        var key = $"{phase}|{_session.CurrentSectionIndex}|{_session.CurrentQuestionIndex}|{_session.CurrentQuestion.Id}|{_dark}";
        var changed = key != _renderedKey;
        _renderedKey = key;

        DirectionsView.Visibility = phase == ExamPhase.Directions ? Visibility.Visible : Visibility.Collapsed;
        TestingView.Visibility = phase == ExamPhase.Testing ? Visibility.Visible : Visibility.Collapsed;

        if (phase == ExamPhase.Directions)
        {
            HeaderTitle.Text = _session.DirectionsHeaderTitle;
            if (changed) RenderDirections();
        }
        else
        {
            HeaderTitle.Text = _session.TestingHeaderTitle;
            UpdateToolbar();
            if (changed) RenderQuestion();
            UpdateAnswerState();
        }
        UpdateCalculator();
    }

    private void RenderDirections()
    {
        DirectionsSectionTitle.Text = _session.CurrentSection.DisplayTitle;
        DirectionsText.Text = _session.SectionDirections;
        TaskCountText.Text = _session.SectionTaskCountText;
        DurationText.Text = _session.SectionDurationText;
        RulesPanel.Children.Clear();
        if (_session.CurrentSection.Measure != GREMeasure.AnalyticalWriting)
        {
            string[] glyphs = ["", "", ""];
            for (var i = 0; i < ExamText.SectionRules.Count; i++)
            {
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(new FontIcon { Glyph = glyphs[i % glyphs.Length], FontSize = 14, Foreground = Palette.Brush(Tone.Secondary, _dark), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) });
                var text = Text(ExamText.SectionRules[i], 13, FontWeights.Normal, Tone.Secondary);
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                RulesPanel.Children.Add(row);
            }
        }
        RulesPanel.Visibility = RulesPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DirectionsView.ChangeView(null, 0, null, true);
        if (IsLoaded) BeginSectionButton.Focus(FocusState.Programmatic);
    }

    private void UpdateToolbar()
    {
        QuestionPositionText.Text = _session.QuestionPositionText;
        CalculatorButton.Visibility = _session.ShowsCalculatorButton ? Visibility.Visible : Visibility.Collapsed;
        ClockButton.Label = _session.ClockButtonTitle;
        AutomationProperties.SetName(ClockButton, _session.IsTimeVisible || _session.IsClockForcedVisible
            ? $"Time remaining {_session.TimeText}. Hide time" : "Show Time");
        MarkButton.Label = _session.MarkButtonTitle;
        MarkButton.IsChecked = _session.IsCurrentMarked;
        SectionProgress.Value = _session.Progress;
        BackButton.IsEnabled = _session.CanGoBack;
        NextButton.Content = _session.IsLastQuestion ? "Review Section" : "Next";
        AnswerStatusText.Text = _session.AnswerRecordedText;
        AnswerStatusText.Foreground = Palette.Brush(_session.CurrentAnswer.IsEmpty ? Tone.Secondary : Tone.Teal, _dark);
    }

    private void RenderQuestion()
    {
        _choices.Clear();
        _sentences.Clear();
        _lastChecked = null;
        _numericBox = _numeratorBox = _denominatorBox = _essayBox = null;
        _wordCountText = null;
        QuestionHost.Children.Clear();

        var question = _session.CurrentQuestion;
        var directions = Text(question.Format.Directions(), 13, FontWeights.SemiBold, Tone.Navy);
        directions.Margin = new Thickness(0, 0, 0, 2);
        QuestionHost.Children.Add(directions);
        QuestionHost.Children.Add(new Rectangle { Height = 1, Fill = Palette.Brush(Tone.Border, _dark) });

        if (question.Format == QuestionFormat.Essay)
        {
            QuestionHost.Children.Add(BuildEssay(question));
        }
        else if (_session.HasSupportingMaterial)
        {
            // ViewThatFits: passage and answers side by side when both fit (410 + 28 + 370), otherwise stacked.
            QuestionHost.Children.Add(ResponsiveRow([BuildSupportingPanel(question), BuildResponsePanel(question)], 410 + 28 + 370, 28, 20));
        }
        else
        {
            QuestionHost.Children.Add(BuildResponsePanel(question));
        }
        QuestionScroller.ChangeView(null, 0, null, true);
    }

    private FrameworkElement BuildSupportingPanel(GREQuestion question)
    {
        var stack = new StackPanel { Spacing = 14 };
        if (question.Format == QuestionFormat.SelectInPassage && question.Groups.Count > 0)
        {
            var group = question.Groups[0];
            var sentences = new StackPanel { Spacing = 6 };
            foreach (var option in group.Options)
            {
                var button = new Button
                {
                    Content = new TextBlock { Text = option.Text, FontFamily = new FontFamily(Georgia), FontSize = 17, LineHeight = 28, TextWrapping = TextWrapping.Wrap },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                };
                button.Click += (_, _) => _session.ToggleSelection(group.Id, option.Id, 1);
                _sentences.Add((option, button));
                sentences.Children.Add(button);
            }
            stack.Children.Add(sentences);
            stack.Children.Add(Text(ExamText.SelectInPassageHint, 12, FontWeights.Normal, Tone.Secondary));
        }
        else if (question.Stimulus is { } stimulus)
        {
            var passage = new TextBlock
            {
                Text = stimulus,
                FontFamily = new FontFamily(Georgia),
                FontSize = 17,
                LineHeight = 28,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            stack.Children.Add(passage);
        }
        if (question.Figure is { } figure) stack.Children.Add(new QuantFigureView(figure));

        return new Border
        {
            Child = stack,
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(6),
            Background = Palette.Brush(Tone.Canvas, _dark),
            BorderBrush = Palette.Brush(Tone.Border, _dark),
            BorderThickness = new Thickness(1),
        };
    }

    private FrameworkElement BuildResponsePanel(GREQuestion question)
    {
        var stack = new StackPanel { Spacing = 19 };
        var prompt = new TextBlock
        {
            Text = question.Prompt,
            FontFamily = new FontFamily(Georgia),
            FontSize = 18,
            FontWeight = FontWeights.Medium,
            LineHeight = 28,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        stack.Children.Add(prompt);

        if (question.Format == QuestionFormat.QuantitativeComparison) stack.Children.Add(BuildComparison(question));

        if (question.Format == QuestionFormat.NumericEntry) stack.Children.Add(BuildNumericEntry());
        else if (question.Format != QuestionFormat.SelectInPassage) stack.Children.Add(BuildChoiceGroups(question));
        return stack;
    }

    private FrameworkElement BuildComparison(GREQuestion question)
    {
        var grid = new Grid { MinHeight = 105 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        FrameworkElement Column(string title, string value)
        {
            var column = new StackPanel { Spacing = 11, Padding = new Thickness(14), VerticalAlignment = VerticalAlignment.Center };
            var titleText = Text(title, 12, FontWeights.Bold, Tone.Secondary);
            titleText.HorizontalAlignment = HorizontalAlignment.Center;
            var valueText = Text(value, 20, FontWeights.SemiBold, Tone.Ink);
            valueText.HorizontalAlignment = HorizontalAlignment.Center;
            valueText.TextAlignment = TextAlignment.Center;
            valueText.IsTextSelectionEnabled = true;
            column.Children.Add(titleText);
            column.Children.Add(valueText);
            AutomationProperties.SetName(column, $"{title}: {value}");
            return column;
        }

        grid.Children.Add(Column(ExamText.QuantityATitle, question.QuantityA ?? ""));
        var divider = new Rectangle { Width = 1, Fill = Palette.Brush(Tone.Border, _dark), VerticalAlignment = VerticalAlignment.Stretch };
        Grid.SetColumn(divider, 1);
        grid.Children.Add(divider);
        var right = Column(ExamText.QuantityBTitle, question.QuantityB ?? "");
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return new Border
        {
            Child = grid,
            CornerRadius = new CornerRadius(4),
            BorderBrush = Palette.Brush(Tone.Border, _dark),
            BorderThickness = new Thickness(1),
        };
    }

    private FrameworkElement BuildChoiceGroups(GREQuestion question)
    {
        var groups = question.Groups.Select(group => BuildChoiceGroup(question, group)).ToList();
        if (groups.Count <= 1)
        {
            var single = new StackPanel { Spacing = 14 };
            foreach (var group in groups) single.Children.Add(group);
            return single;
        }
        // Text completion: the blanks' columns side by side when they fit, otherwise stacked.
        return ResponsiveRow(groups, groups.Count * 190, 12, 14);
    }

    private FrameworkElement BuildChoiceGroup(GREQuestion question, ChoiceGroup group)
    {
        var stack = new StackPanel { Spacing = 8 };
        if (group.Title is { } title) stack.Children.Add(Text(title, 12, FontWeights.Bold, Tone.Secondary));
        foreach (var option in group.Options)
        {
            var label = new TextBlock { Text = option.Text, FontSize = 16, TextWrapping = TextWrapping.Wrap };
            ToggleButton toggle = group.MaximumSelections == 1
                ? new RadioButton { Content = label, GroupName = $"{question.Id}|{group.Id}" }
                : new CheckBox { Content = label };
            toggle.HorizontalAlignment = HorizontalAlignment.Stretch;
            toggle.MinWidth = 0;
            AutomationProperties.SetName(toggle, option.Text);

            toggle.Checked += (_, _) =>
            {
                if (_syncing) return;
                _lastChecked = toggle;
                if (!_session.CurrentAnswer.IsSelected(group.Id, option.Id))
                    _session.ToggleSelection(group.Id, option.Id, group.MaximumSelections);
            };
            toggle.Unchecked += (_, _) =>
            {
                if (_syncing) return;
                if (_session.CurrentAnswer.IsSelected(group.Id, option.Id))
                    _session.ToggleSelection(group.Id, option.Id, group.MaximumSelections);
            };
            if (toggle is RadioButton)
            {
                // Clicking the selected answer again clears it, as in the Mac app.
                toggle.Click += (_, _) =>
                {
                    if (ReferenceEquals(_lastChecked, toggle))
                    {
                        _lastChecked = null;
                        return;
                    }
                    if (_session.CurrentAnswer.IsSelected(group.Id, option.Id))
                        _session.ToggleSelection(group.Id, option.Id, group.MaximumSelections);
                };
            }

            var card = new Border { Child = toggle, CornerRadius = new CornerRadius(5) };
            _choices.Add(new ChoiceControl(group, option, toggle, card));
            stack.Children.Add(card);
        }
        return stack;
    }

    private FrameworkElement BuildNumericEntry()
    {
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(Text(_session.NumericEntryHint, 12, FontWeights.Bold, Tone.Secondary));
        if (_session.IsFractionEntry)
        {
            var fraction = new StackPanel { Spacing = 6, Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            _numeratorBox = NumberBox(120, "Numerator");
            _denominatorBox = NumberBox(120, "Denominator");
            _numeratorBox.Text = _session.FractionPart(0);
            _denominatorBox.Text = _session.FractionPart(1);
            _numeratorBox.TextChanged += (_, _) =>
            {
                if (!_syncing && IsAnswering(QuestionFormat.NumericEntry) && _numeratorBox is { } box) _session.SetFractionPart(0, box.Text);
            };
            _denominatorBox.TextChanged += (_, _) =>
            {
                if (!_syncing && IsAnswering(QuestionFormat.NumericEntry) && _denominatorBox is { } box) _session.SetFractionPart(1, box.Text);
            };
            fraction.Children.Add(_numeratorBox);
            fraction.Children.Add(new Rectangle { Width = 120, Height = 2, Fill = Palette.Brush(Tone.Navy, _dark), HorizontalAlignment = HorizontalAlignment.Center });
            fraction.Children.Add(_denominatorBox);
            stack.Children.Add(fraction);
        }
        else
        {
            _numericBox = NumberBox(220, "Answer");
            _numericBox.HorizontalAlignment = HorizontalAlignment.Left;
            _numericBox.Text = _session.CurrentAnswer.NumericEntry;
            _numericBox.TextChanged += (_, _) =>
            {
                if (!_syncing && IsAnswering(QuestionFormat.NumericEntry) && _numericBox is { } box) _session.SetNumericEntryText(box.Text);
            };
            stack.Children.Add(_numericBox);
        }
        return stack;
    }

    private static TextBox NumberBox(double width, string name)
    {
        var box = new TextBox
        {
            Width = width,
            FontSize = 20,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } },
        };
        AutomationProperties.SetName(box, name);
        return box;
    }

    // MARK: - Essay (EssayEditor.swift)

    private FrameworkElement BuildEssay(GREQuestion question)
    {
        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(new TextBlock
        {
            Text = question.Prompt,
            FontFamily = new FontFamily(Georgia),
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            LineHeight = 28,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        var instructions = Text(_session.EssayInstructions, 16, FontWeights.Normal, Tone.Secondary);
        instructions.FontFamily = new FontFamily(Georgia);
        instructions.LineHeight = 24;
        stack.Children.Add(instructions);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Text("Response", 12, FontWeights.Bold, Tone.Secondary));
        _wordCountText = Text("", 12, FontWeights.Normal, Tone.Secondary);
        Grid.SetColumn(_wordCountText, 1);
        header.Children.Add(_wordCountText);
        stack.Children.Add(header);

        // A plain-text word processor like the GRE's: no spell check, grammar check, or text prediction.
        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            FontFamily = new FontFamily(Georgia),
            FontSize = 16,
            MinHeight = 380,
            MaxHeight = 620,
            Padding = new Thickness(10, 10, 10, 10),
            BorderThickness = new Thickness(1.5),
            Text = _session.CurrentAnswer.EssayText,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        AutomationProperties.SetName(editor, "Response");
        editor.TextChanged += (_, _) =>
        {
            // TextChanged is raised after the fact; ignore it once this editor's section is over.
            if (!_syncing && ReferenceEquals(_essayBox, editor) && IsAnswering(QuestionFormat.Essay)) _session.UpdateEssay(editor.Text);
        };
        _essayBox = editor;

        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        commands.Children.Add(EditorButton("Cut", "", () => editor.CutSelectionToClipboard()));
        commands.Children.Add(EditorButton("Paste", "", () => editor.PasteFromClipboard()));
        commands.Children.Add(EditorButton("Undo", "", () => { if (editor.CanUndo) editor.Undo(); }));
        commands.Children.Add(EditorButton("Redo", "", () => { if (editor.CanRedo) editor.Redo(); }));

        var editorStack = new StackPanel { Spacing = 8 };
        editorStack.Children.Add(commands);
        editorStack.Children.Add(editor);
        stack.Children.Add(editorStack);
        return stack;

        Button EditorButton(string title, string glyph, Action action)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12 });
            content.Children.Add(new TextBlock { Text = title, FontSize = 12 });
            var button = new Button { Content = content, Padding = new Thickness(10, 4, 10, 5) };
            AutomationProperties.SetName(button, title);
            button.Click += (_, _) =>
            {
                action();
                editor.Focus(FocusState.Programmatic);
            };
            return button;
        }
    }

    // MARK: - Answer state

    private void UpdateAnswerState()
    {
        var answer = _session.CurrentAnswer;
        _syncing = true;
        try
        {
            foreach (var choice in _choices)
            {
                var selected = answer.IsSelected(choice.Group.Id, choice.Option.Id);
                if (choice.Toggle.IsChecked != selected) choice.Toggle.IsChecked = selected;
                choice.Card.Background = selected ? Palette.Brush(Tone.BrightBlue, _dark, 0.16) : Palette.Brush(Tone.Input, _dark);
                choice.Card.BorderBrush = selected ? Palette.Brush(Tone.BrightBlue, _dark) : Palette.Brush(Tone.Border, _dark);
                choice.Card.BorderThickness = new Thickness(selected ? 2 : 1);
                choice.Card.Padding = selected ? new Thickness(10, 3, 10, 3) : new Thickness(11, 4, 11, 4);
            }
            foreach (var (option, button) in _sentences)
            {
                var selected = answer.IsSelected(_session.CurrentQuestion.Groups[0].Id, option.Id);
                button.Background = selected ? Palette.Brush(Tone.BrightBlue, _dark, 0.28) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                AutomationProperties.SetName(button, selected ? $"Selected: {option.Text}" : option.Text);
            }
            if (_numericBox is not null) SyncText(_numericBox, answer.NumericEntry);
            if (_numeratorBox is not null) SyncText(_numeratorBox, _session.FractionPart(0));
            if (_denominatorBox is not null) SyncText(_denominatorBox, _session.FractionPart(1));
            if (_wordCountText is not null) _wordCountText.Text = $"{_session.EssayWordCount} words";
        }
        finally
        {
            _syncing = false;
        }
    }

    private bool IsAnswering(QuestionFormat format) =>
        !_left && _session.Phase == ExamPhase.Testing && _session.CurrentQuestion.Format == format;

    /// <summary>Shows the session's (cleaned) text in a box without losing the caret.</summary>
    private static void SyncText(TextBox box, string value)
    {
        if (box.Text == value) return;
        var caret = box.SelectionStart;
        box.Text = value;
        box.SelectionStart = Math.Min(caret, value.Length);
    }

    // MARK: - Calculator

    private void UpdateCalculator()
    {
        if (_session.ShowsCalculator)
        {
            if (_calculator is null)
            {
                _calculator = new CalculatorPanel(_session);
                OverlayCanvas.Children.Add(_calculator);
                // Top-right corner below the toolbar, as on the Mac (120 pt from the top, 24 from the right).
                Canvas.SetLeft(_calculator, Math.Max(0, OverlayCanvas.ActualWidth - _calculator.Width - 24));
                Canvas.SetTop(_calculator, 66);
            }
            _calculator.Visibility = Visibility.Visible;
            _calculator.Refresh();
        }
        else if (_calculator is not null)
        {
            _calculator.Visibility = Visibility.Collapsed;
        }
    }

    // MARK: - Score report

    private void ShowReport()
    {
        _timer.Stop();
        if (_calculator is not null) _calculator.Visibility = Visibility.Collapsed;
        ExamHeader.Visibility = Visibility.Collapsed;
        DirectionsView.Visibility = Visibility.Collapsed;
        TestingView.Visibility = Visibility.Collapsed;
        ReportHost.Visibility = Visibility.Visible;
        if (ReportHost.Content is not null || _session.Result is not { } result) return;

        var report = new ScoreReportView(_services.ReportForCompletedExam(result));
        report.CloseRequested += (_, _) =>
        {
            // Save & Close: keep the result, then show Score History (ContentView.swift examDestination).
            _services.History.Add(result);
            Leave("history");
        };
        ReportHost.Content = report;
    }

    private void Leave(string? navigateToTag)
    {
        if (_left) return;
        _left = true;
        _timer.Stop();
        _session.PropertyChanged -= OnSessionChanged;
        _openDialog?.Hide();
        MainWindow.Current.CloseExam(navigateToTag);
    }

    // MARK: - Commands

    private void OnBeginSectionClick(object sender, RoutedEventArgs e) => _session.BeginSection();

    private void OnBackClick(object sender, RoutedEventArgs e) => _session.GoBack();

    private async void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_session.IsLastQuestion) await ShowReviewAsync();
        else _session.GoNext();
    }

    private void OnCalculatorClick(object sender, RoutedEventArgs e) => _session.IsCalculatorPresented = !_session.IsCalculatorPresented;

    private void OnClockClick(object sender, RoutedEventArgs e) => _session.IsTimeVisible = !_session.IsTimeVisible;

    private void OnMarkClick(object sender, RoutedEventArgs e) => _session.ToggleMark();

    private async void OnReviewClick(object sender, RoutedEventArgs e) => await ShowReviewAsync();

    private async void OnHelpClick(object sender, RoutedEventArgs e) => await ShowHelpAsync();

    private async void OnExitSectionClick(object sender, RoutedEventArgs e) => await ConfirmExitSectionAsync();

    private async void OnExitTestClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = ExamText.ExitTestTitle,
            Content = ExamText.ExitTestMessage,
            PrimaryButtonText = ExamText.ExitTestConfirm,
            CloseButtonText = ExamText.ExitTestCancel,
            DefaultButton = ContentDialogButton.Close,
        };
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary) Leave(null);
    }

    private async Task ConfirmExitSectionAsync()
    {
        if (_session.Phase != ExamPhase.Testing) return;
        var dialog = new ContentDialog
        {
            Title = ExamText.ExitSectionTitle,
            Content = new TextBlock { Text = _session.ExitSectionMessage, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = ExamText.ExitSectionConfirm,
            CloseButtonText = ExamText.ExitSectionCancel,
            DefaultButton = ContentDialogButton.Close,
        };
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary) _session.ConfirmExitSection();
    }

    private async Task ShowHelpAsync()
    {
        var question = _session.CurrentQuestion;
        var content = new StackPanel { Spacing = 14, MinWidth = 420 };
        content.Children.Add(Text(question.ContentArea, 15, FontWeights.SemiBold, Tone.Blue));
        content.Children.Add(new TextBlock { Text = question.Format.Directions(), TextWrapping = TextWrapping.Wrap, LineHeight = 22 });
        if (question.Measure == GREMeasure.Quantitative) content.Children.Add(Text(ExamText.HelpQuantNote, 13, FontWeights.Normal, Tone.Secondary));
        content.Children.Add(Text(ExamText.HelpFootnote, 12, FontWeights.Normal, Tone.Secondary));
        var dialog = new ContentDialog
        {
            Title = ExamText.HelpTitle,
            Content = content,
            CloseButtonText = "Done",
            DefaultButton = ContentDialogButton.Close,
        };
        await ShowDialogAsync(dialog);
    }

    /// <summary>The Review screen (ReviewView): pick an encountered question to go to, or exit the section.</summary>
    private async Task ShowReviewAsync()
    {
        if (_session.Phase != ExamPhase.Testing) return;
        var review = new ReviewScreenState(_session);

        var title = new StackPanel { Spacing = 3 };
        title.Children.Add(new TextBlock { Text = "Review", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        title.Children.Add(Text(_session.TestingHeaderTitle, 13, FontWeights.Normal, Tone.Secondary));

        var content = new StackPanel { Spacing = 8, MinWidth = 560 };
        content.Children.Add(Text(ReviewScreenState.Instructions, 12, FontWeights.Normal, Tone.Secondary));
        content.Children.Add(ReviewRowGrid("Number", "Status", "Marked", header: true, dim: false));
        content.Children.Add(new Rectangle { Height = 1, Fill = Palette.Brush(Tone.Border, _dark) });

        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, Height = 330 };
        AutomationProperties.SetName(list, "Questions in this section");
        foreach (var row in _session.ReviewRows)
        {
            var grid = ReviewRowGrid(row.Number.ToString(), row.StatusTitle, row.IsMarked ? "✓" : "", header: false, dim: !row.IsEncountered);
            if (row.IsCurrent) grid.Background = Palette.Brush(Tone.BrightBlue, _dark, 0.1);
            var item = new ListViewItem { Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(item, $"Question {row.Number}, {row.StatusTitle}{(row.IsMarked ? ", marked" : "")}{(row.IsCurrent ? ", current" : "")}");
            list.Items.Add(item);
        }
        content.Children.Add(list);
        content.Children.Add(new Rectangle { Height = 1, Fill = Palette.Brush(Tone.Border, _dark) });
        content.Children.Add(Text(_session.ReviewFooterText, 12, FontWeights.Normal, Tone.Secondary));

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Go to Question",
            SecondaryButtonText = ExamText.ExitSectionConfirm,
            CloseButtonText = "Return",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 760.0;
        list.SelectedIndex = review.Selection ?? -1;
        dialog.IsPrimaryButtonEnabled = review.CanGoToQuestion;
        list.SelectionChanged += (_, _) =>
        {
            review.Selection = list.SelectedIndex >= 0 ? list.SelectedIndex : null;
            dialog.IsPrimaryButtonEnabled = review.CanGoToQuestion;
        };
        var wentToQuestion = false;
        list.IsDoubleTapEnabled = true;
        list.DoubleTapped += (_, _) =>
        {
            if (!review.CanGoToQuestion) return;
            wentToQuestion = true;
            dialog.Hide();
        };

        var result = await ShowDialogAsync(dialog);
        if (result == ContentDialogResult.Primary || (wentToQuestion && !_dialogCancelled)) review.GoToQuestion();
        else if (result == ContentDialogResult.Secondary) await ConfirmExitSectionAsync();
    }

    private Grid ReviewRowGrid(string number, string status, string marked, bool header, bool dim)
    {
        var grid = new Grid { ColumnSpacing = 24, Padding = new Thickness(6, 7, 6, 7) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        var weight = header ? FontWeights.Bold : FontWeights.Normal;
        var tone = dim ? Tone.Secondary : Tone.Ink;
        string[] values = [number, status, marked];
        for (var i = 0; i < values.Length; i++)
        {
            var text = Text(values[i], 14, weight, tone);
            text.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }
        return grid;
    }

    // MARK: - Dialogs

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_openDialog is not null || XamlRoot is null || _left) return ContentDialogResult.None;
        dialog.XamlRoot = XamlRoot;
        if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out var style) && style is Style dialogStyle)
            dialog.Style = dialogStyle;
        dialog.RequestedTheme = ActualTheme;
        _openDialog = dialog;
        _dialogContext = DialogContext();
        _dialogCancelled = false;
        try
        {
            var result = await dialog.ShowAsync();
            return _dialogCancelled ? ContentDialogResult.None : result;
        }
        finally
        {
            _openDialog = null;
        }
    }

    private string DialogContext() => $"{_session.Phase}|{_session.CurrentSectionIndex}";

    /// <summary>Closes an open dialog when its section has ended (time ran out), so its choice cannot apply to the next section.</summary>
    private void CloseDialogIfStale()
    {
        if (_openDialog is null || _dialogContext == DialogContext()) return;
        _dialogCancelled = true;
        _openDialog.Hide();
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

    /// <summary>Lays parts out side by side when the row is at least <paramref name="minimumWidth"/> wide, otherwise stacked (SwiftUI ViewThatFits).</summary>
    private static Grid ResponsiveRow(IReadOnlyList<FrameworkElement> parts, double minimumWidth, double columnSpacing, double rowSpacing)
    {
        var grid = new Grid { ColumnSpacing = columnSpacing, RowSpacing = rowSpacing };
        foreach (var part in parts) grid.Children.Add(part);

        void Arrange(bool horizontal)
        {
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var i = 0; i < parts.Count; i++)
            {
                if (horizontal) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                else grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(parts[i], horizontal ? i : 0);
                Grid.SetRow(parts[i], horizontal ? 0 : i);
            }
        }

        var isHorizontal = true;
        Arrange(true);
        grid.SizeChanged += (_, e) =>
        {
            var fits = e.NewSize.Width >= minimumWidth;
            if (fits == isHorizontal) return;
            isHorizontal = fits;
            Arrange(fits);
        };
        return grid;
    }
}
