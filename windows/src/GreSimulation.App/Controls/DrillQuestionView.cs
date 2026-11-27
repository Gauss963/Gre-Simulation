using GreSimulation.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace GreSimulation.App.Controls;

/// <summary>
/// Port of <c>DrillQuestionView</c> (PracticeDrillView.swift): one Verbal or Quant question whose answer can be
/// changed until it is checked; then the key, wrong choices, the explanation and (Verbal) the guessing tip are shown.
/// Built in code for the element's theme. Rebuild it when the selections, the check, or the theme change; while the
/// learner types a Numeric Entry the page keeps the built boxes and only syncs their text.
/// </summary>
public sealed class DrillQuestionView
{
    private const string Georgia = "Georgia";

    private readonly GREQuestion _question;
    private readonly GREAnswer _answer;
    private readonly bool? _isCorrect;
    private readonly bool _dark;
    private readonly PracticeDrillSession? _session;

    private bool Checked => _isCorrect is not null;

    /// <summary>The built view.</summary>
    public StackPanel View { get; }

    /// <summary>The single Numeric Entry box, or the numerator and denominator boxes (null when not shown).</summary>
    public TextBox? NumericBox { get; private set; }
    public TextBox? NumeratorBox { get; private set; }
    public TextBox? DenominatorBox { get; private set; }

    /// <param name="session">Receives clicks and typing; null for a read-only review.</param>
    public DrillQuestionView(GREQuestion question, GREAnswer answer, bool? isCorrect, string? guessingTip, bool dark, PracticeDrillSession? session)
    {
        _question = question;
        _answer = answer;
        _isCorrect = isCorrect;
        _dark = dark;
        _session = Checked ? null : session;
        View = Build(guessingTip);
    }

    /// <summary>Puts the session's entry into the boxes when it differs (cleaned input, Transfer Display).</summary>
    public void SyncNumericText(PracticeDrillSession session)
    {
        static void Sync(TextBox? box, string value)
        {
            if (box is null || box.Text == value) return;
            box.Text = value;
            box.SelectionStart = value.Length;
        }
        Sync(NumericBox, session.Answer.NumericEntry);
        Sync(NumeratorBox, session.FractionPart(0));
        Sync(DenominatorBox, session.FractionPart(1));
    }

    private StackPanel Build(string? guessingTip)
    {
        var root = new StackPanel { Spacing = 16 };

        var heading = new Grid();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (DrillText.Of(_question) is { } type)
            titleRow.Children.Add(Text(type.Title(), 16, FontWeights.SemiBold, Tone.Navy));
        titleRow.Children.Add(new Border
        {
            Child = Text(_question.Difficulty.Title(), 12, FontWeights.Bold, Tone.Ink),
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(10),
            Background = Palette.Brush(Tone.BrightBlue, _dark, 0.14),
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (_question.Measure == GREMeasure.Quantitative)
        {
            var area = Text(DrillCategory.QuantArea(_question), 12, FontWeights.Normal, Tone.Secondary);
            area.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(area);
        }
        heading.Children.Add(titleRow);
        if (_isCorrect is { } correct)
        {
            var result = Text(correct ? "✓ Correct" : "✕ Incorrect", 16, FontWeights.SemiBold, correct ? Tone.Teal : Tone.Red);
            result.HorizontalAlignment = HorizontalAlignment.Right;
            heading.Children.Add(result);
        }
        root.Children.Add(heading);
        root.Children.Add(Text(_question.Format.Directions(), 13, FontWeights.SemiBold, Tone.Secondary));
        root.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle { Height = 1, Fill = Palette.Brush(Tone.Border, _dark) });

        var hasSupport = _question.Stimulus is not null || _question.Figure is not null || _question.Format == QuestionFormat.SelectInPassage;
        var response = BuildResponse();
        root.Children.Add(hasSupport ? ResponsiveRow([BuildSupport(), response], 400 + 26 + 360, 26, 18) : response);

        if (Checked) root.Children.Add(BuildFeedback(guessingTip));
        return root;
    }

    private FrameworkElement BuildSupport()
    {
        var stack = new StackPanel { Spacing = 10 };
        if (_question.Format == QuestionFormat.SelectInPassage && _question.Groups.Count > 0)
        {
            var group = _question.Groups[0];
            var sentences = new StackPanel { Spacing = 6 };
            foreach (var sentence in group.Options)
            {
                var selected = _answer.IsSelected(group.Id, sentence.Id);
                var isKey = IsKey(group, sentence);
                var button = new Button
                {
                    Content = new TextBlock { Text = sentence.Text, FontFamily = new FontFamily(Georgia), FontSize = 17, LineHeight = 28, TextWrapping = TextWrapping.Wrap },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(3),
                    Background = Fill(selected, isKey, sentence: true),
                    IsHitTestVisible = _session is not null,
                };
                AutomationProperties.SetName(button, sentence.Text);
                if (_session is { } session) button.Click += (_, _) => session.Toggle(group, sentence);
                sentences.Children.Add(button);
            }
            stack.Children.Add(sentences);
            if (!Checked) stack.Children.Add(Text(ExamText.SelectInPassageHint, 12, FontWeights.Normal, Tone.Secondary));
        }
        else if (_question.Stimulus is { } stimulus)
        {
            stack.Children.Add(new TextBlock
            {
                Text = stimulus,
                FontFamily = new FontFamily(Georgia),
                FontSize = 17,
                LineHeight = 28,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            });
        }
        if (_question.Figure is { } figure) stack.Children.Add(new QuantFigureView(figure));
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

    private FrameworkElement BuildResponse()
    {
        var stack = new StackPanel { Spacing = 18 };
        stack.Children.Add(new TextBlock
        {
            Text = _question.Prompt,
            FontFamily = new FontFamily(Georgia),
            FontSize = 18,
            FontWeight = FontWeights.Medium,
            LineHeight = 28,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        if (_question.Format == QuestionFormat.QuantitativeComparison) stack.Children.Add(BuildComparison());
        if (_question.Format == QuestionFormat.NumericEntry)
        {
            stack.Children.Add(BuildNumericEntry());
            return stack;
        }
        if (_question.Format == QuestionFormat.SelectInPassage) return stack;

        var groups = _question.Groups.Select(group =>
        {
            var column = new StackPanel { Spacing = 8 };
            if (group.Title is { } title) column.Children.Add(Text(title, 12, FontWeights.Bold, Tone.Secondary));
            foreach (var option in group.Options) column.Children.Add(Choice(group, option));
            return (FrameworkElement)column;
        }).ToList();
        if (groups.Count > 1) stack.Children.Add(ResponsiveRow(groups, groups.Count * 190, 12, 14));
        else foreach (var group in groups) stack.Children.Add(group);
        return stack;
    }

    private FrameworkElement BuildComparison()
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

        grid.Children.Add(Column(ExamText.QuantityATitle, _question.QuantityA ?? ""));
        var divider = new Microsoft.UI.Xaml.Shapes.Rectangle { Width = 1, Fill = Palette.Brush(Tone.Border, _dark), VerticalAlignment = VerticalAlignment.Stretch };
        Grid.SetColumn(divider, 1);
        grid.Children.Add(divider);
        var right = Column(ExamText.QuantityBTitle, _question.QuantityB ?? "");
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return new Border { Child = grid, CornerRadius = new CornerRadius(4), BorderBrush = Palette.Brush(Tone.Border, _dark), BorderThickness = new Thickness(1) };
    }

    private FrameworkElement BuildNumericEntry()
    {
        var fraction = PracticeDrillPool.IsFractionEntry(_question);
        var stack = new StackPanel { Spacing = 7 };
        stack.Children.Add(Text(fraction
            ? "Enter your answer as a fraction. Fractions do not need to be reduced to lowest terms."
            : "Enter your answer as an integer or a decimal.", 12, FontWeights.Bold, Tone.Secondary));

        if (Checked)
        {
            // Once checked the entry is shown as text, colored by the result.
            var ok = _isCorrect == true;
            stack.Children.Add(new Border
            {
                Child = Text(_answer.NumericEntry.Length == 0 ? " " : _answer.NumericEntry, 20, FontWeights.Normal, Tone.Ink),
                Padding = new Thickness(14, 8, 14, 8),
                MinWidth = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(3),
                Background = Palette.Brush(ok ? Tone.Teal : Tone.Red, _dark, ok ? 0.2 : 0.14),
                BorderBrush = Palette.Brush(ok ? Tone.Teal : Tone.Red, _dark),
                BorderThickness = new Thickness(1.5),
            });
            return stack;
        }

        var session = _session;
        if (fraction)
        {
            var panel = new StackPanel { Spacing = 6, Width = 140, HorizontalAlignment = HorizontalAlignment.Left };
            NumeratorBox = NumberBox(120, "Numerator");
            DenominatorBox = NumberBox(120, "Denominator");
            NumeratorBox.Text = Part(0);
            DenominatorBox.Text = Part(1);
            NumeratorBox.IsEnabled = DenominatorBox.IsEnabled = session is not null;
            if (session is not null)
            {
                NumeratorBox.TextChanged += (_, _) => session.SetFractionPart(0, NumeratorBox.Text);
                DenominatorBox.TextChanged += (_, _) => session.SetFractionPart(1, DenominatorBox.Text);
            }
            panel.Children.Add(NumeratorBox);
            panel.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle { Width = 120, Height = 2, Fill = Palette.Brush(Tone.Navy, _dark), HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(DenominatorBox);
            stack.Children.Add(panel);
        }
        else
        {
            NumericBox = NumberBox(220, "Answer");
            NumericBox.HorizontalAlignment = HorizontalAlignment.Left;
            NumericBox.Text = _answer.NumericEntry;
            NumericBox.IsEnabled = session is not null;
            if (session is not null) NumericBox.TextChanged += (_, _) => session.SetNumericEntryText(NumericBox.Text);
            stack.Children.Add(NumericBox);
        }
        return stack;
    }

    private string Part(int index)
    {
        var parts = _answer.NumericEntry.Split('/');
        return index < parts.Length ? parts[index] : "";
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

    private bool IsKey(ChoiceGroup group, ChoiceOption option) =>
        _question.CorrectSelections.TryGetValue(group.Id, out var key) && key.Contains(option.Id);

    private FrameworkElement Choice(ChoiceGroup group, ChoiceOption option)
    {
        var selected = _answer.IsSelected(group.Id, option.Id);
        var isKey = IsKey(group, option);
        var single = group.MaximumSelections == 1;

        var row = new Grid { ColumnSpacing = 11 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        // Segoe Fluent Icons: radio on/off, checkbox on/off.
        var glyph = single ? (selected ? "" : "") : (selected ? "" : "");
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, Foreground = Palette.Brush(selected ? Tone.Blue : Tone.Secondary, _dark), VerticalAlignment = VerticalAlignment.Top });
        var label = new TextBlock { Text = option.Text, FontSize = 16, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Brush(Tone.Ink, _dark) };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        if (Checked && (isKey || selected))
        {
            var mark = Text(isKey ? "✓" : "✕", 16, FontWeights.Bold, isKey ? Tone.Teal : Tone.Red);
            Grid.SetColumn(mark, 2);
            row.Children.Add(mark);
        }

        var emphasized = selected || (Checked && isKey);
        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(11),
            CornerRadius = new CornerRadius(5),
            Background = Fill(selected, isKey, sentence: false),
            BorderBrush = Stroke(selected, isKey),
            BorderThickness = new Thickness(emphasized ? 2 : 1),
            IsHitTestVisible = _session is not null,
        };
        AutomationProperties.SetName(button, option.Text);
        if (_session is { } session) button.Click += (_, _) => session.Toggle(group, option);
        return button;
    }

    private Brush Fill(bool selected, bool isKey, bool sentence)
    {
        if (!Checked)
            return selected ? Palette.Brush(Tone.BrightBlue, _dark, sentence ? 0.28 : 0.16)
                : sentence ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : Palette.Brush(Tone.Input, _dark);
        if (isKey) return Palette.Brush(Tone.Teal, _dark, 0.2);
        if (selected) return Palette.Brush(Tone.Red, _dark, 0.14);
        return sentence ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : Palette.Brush(Tone.Input, _dark);
    }

    private Brush Stroke(bool selected, bool isKey)
    {
        if (!Checked) return Palette.Brush(selected ? Tone.BrightBlue : Tone.Border, _dark);
        if (isKey) return Palette.Brush(Tone.Teal, _dark);
        if (selected) return Palette.Brush(Tone.Red, _dark);
        return Palette.Brush(Tone.Border, _dark);
    }

    private FrameworkElement BuildFeedback(string? guessingTip)
    {
        var correct = _isCorrect == true;
        var stack = new StackPanel { Spacing = 10 };
        var answers = new StackPanel { Spacing = 3 };
        var tone = correct ? Tone.Teal : Tone.Ink;
        answers.Children.Add(Text($"Your answer: {PracticeDrillPool.AnswerText(_answer, _question)}", 14, FontWeights.Normal, tone));
        answers.Children.Add(Text($"Correct answer: {PracticeDrillPool.CorrectAnswerText(_question)}", 14, FontWeights.SemiBold, tone));
        stack.Children.Add(answers);

        var explanation = new StackPanel { Spacing = 4 };
        explanation.Children.Add(Text(PracticeDrillSession.ExplanationHeading, 12, FontWeights.Bold, Tone.Secondary));
        var body = Text(_question.Explanation, 14, FontWeights.Normal, Tone.Ink);
        body.IsTextSelectionEnabled = true;
        explanation.Children.Add(body);
        stack.Children.Add(new Border { Child = explanation, Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Background = Palette.Brush(Tone.Input, _dark) });

        if (_question.Measure == GREMeasure.Verbal && guessingTip is not null)
        {
            var tip = new StackPanel { Spacing = 2 };
            tip.Children.Add(Text("💡 " + PracticeDrillSession.GuessingTipHeading, 12, FontWeights.SemiBold, Tone.Ink));
            var tipBody = Text(guessingTip, 14, FontWeights.Normal, Tone.Secondary);
            tipBody.IsTextSelectionEnabled = true;
            tip.Children.Add(tipBody);
            stack.Children.Add(new Border { Child = tip, Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Background = Palette.Brush(Tone.Orange, _dark, 0.08) });
        }

        if (!correct)
        {
            if (DrillText.Of(_question) is { } type) stack.Children.Add(Text("◎ " + type.Tip(), 12, FontWeights.Normal, Tone.Secondary));
            if (_question.Measure == GREMeasure.Quantitative && DrillCategory.Of(_question) is { } area)
                stack.Children.Add(Text("ƒ " + area.Tip, 12, FontWeights.Normal, Tone.Secondary));
        }
        if (_question.Source is { } source)
            stack.Children.Add(Text($"{source.Title} · {source.Detail}", 11, FontWeights.Normal, Tone.Secondary));
        return stack;
    }

    private TextBlock Text(string text, double size, Windows.UI.Text.FontWeight weight, Tone tone) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = Palette.Brush(tone, _dark),
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>Parts side by side when the row is at least <paramref name="minimumWidth"/> wide, otherwise stacked (SwiftUI ViewThatFits).</summary>
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
