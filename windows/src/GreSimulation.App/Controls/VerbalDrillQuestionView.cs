using GreSimulation.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GreSimulation.App.Controls;

/// <summary>
/// Port of <c>VerbalDrillQuestionView</c> (VerbalDrillView.swift): one Verbal question whose choices can be
/// changed until it is checked; then the key, wrong choices, the explanation and the guessing tip are shown.
/// Built in code for the element's theme; rebuild it when the answer, the check, or the theme changes.
/// </summary>
public static class VerbalDrillQuestionView
{
    private const string Georgia = "Georgia";

    /// <param name="toggle">Called when a choice or sentence is clicked; null for a read-only review.</param>
    public static StackPanel Build(GREQuestion question, GREAnswer answer, bool? isCorrect, string? guessingTip, bool dark,
        Action<ChoiceGroup, ChoiceOption>? toggle)
    {
        var checkedAnswer = isCorrect is not null;
        var root = new StackPanel { Spacing = 16 };

        // Type, difficulty, result
        var heading = new Grid();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (VerbalDrillText.Of(question) is { } type)
            titleRow.Children.Add(Text(type.Title(), 16, FontWeights.SemiBold, Tone.Navy, dark));
        titleRow.Children.Add(new Border
        {
            Child = Text(question.Difficulty.Title(), 12, FontWeights.Bold, Tone.Ink, dark),
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(10),
            Background = Palette.Brush(Tone.BrightBlue, dark, 0.14),
            VerticalAlignment = VerticalAlignment.Center,
        });
        heading.Children.Add(titleRow);
        if (isCorrect is { } correct)
        {
            var result = Text(correct ? "✓ Correct" : "✕ Incorrect", 16, FontWeights.SemiBold, correct ? Tone.Teal : Tone.Red, dark);
            result.HorizontalAlignment = HorizontalAlignment.Right;
            heading.Children.Add(result);
        }
        root.Children.Add(heading);
        root.Children.Add(Text(question.Format.Directions(), 13, FontWeights.SemiBold, Tone.Secondary, dark));
        root.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle { Height = 1, Fill = Palette.Brush(Tone.Border, dark) });

        var hasPassage = question.Stimulus is not null || question.Format == QuestionFormat.SelectInPassage;
        var response = BuildResponse(question, answer, checkedAnswer, dark, toggle);
        root.Children.Add(hasPassage
            ? ResponsiveRow([BuildPassage(question, answer, checkedAnswer, dark, toggle), response], 400 + 26 + 360, 26, 18)
            : response);

        if (checkedAnswer) root.Children.Add(BuildFeedback(question, answer, isCorrect == true, guessingTip, dark));
        return root;
    }

    private static FrameworkElement BuildPassage(GREQuestion question, GREAnswer answer, bool checkedAnswer, bool dark,
        Action<ChoiceGroup, ChoiceOption>? toggle)
    {
        var stack = new StackPanel { Spacing = 6 };
        if (question.Format == QuestionFormat.SelectInPassage && question.Groups.Count > 0)
        {
            var group = question.Groups[0];
            foreach (var sentence in group.Options)
            {
                var selected = answer.IsSelected(group.Id, sentence.Id);
                var isKey = question.CorrectSelections.TryGetValue(group.Id, out var key) && key.Contains(sentence.Id);
                var button = new Button
                {
                    Content = new TextBlock { Text = sentence.Text, FontFamily = new FontFamily(Georgia), FontSize = 17, LineHeight = 28, TextWrapping = TextWrapping.Wrap },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(3),
                    Background = Fill(selected, isKey, checkedAnswer, dark, sentence: true),
                    IsHitTestVisible = toggle is not null && !checkedAnswer,
                };
                AutomationProperties.SetName(button, sentence.Text);
                if (toggle is not null) button.Click += (_, _) => toggle(group, sentence);
                stack.Children.Add(button);
            }
            if (!checkedAnswer) stack.Children.Add(Text(ExamText.SelectInPassageHint, 12, FontWeights.Normal, Tone.Secondary, dark));
        }
        else if (question.Stimulus is { } stimulus)
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
        return new Border
        {
            Child = stack,
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(6),
            Background = Palette.Brush(Tone.Canvas, dark),
            BorderBrush = Palette.Brush(Tone.Border, dark),
            BorderThickness = new Thickness(1),
        };
    }

    private static FrameworkElement BuildResponse(GREQuestion question, GREAnswer answer, bool checkedAnswer, bool dark,
        Action<ChoiceGroup, ChoiceOption>? toggle)
    {
        var stack = new StackPanel { Spacing = 18 };
        stack.Children.Add(new TextBlock
        {
            Text = question.Prompt,
            FontFamily = new FontFamily(Georgia),
            FontSize = 18,
            FontWeight = FontWeights.Medium,
            LineHeight = 28,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        if (question.Format == QuestionFormat.SelectInPassage) return stack;

        var groups = question.Groups.Select(group =>
        {
            var column = new StackPanel { Spacing = 8 };
            if (group.Title is { } title) column.Children.Add(Text(title, 12, FontWeights.Bold, Tone.Secondary, dark));
            foreach (var option in group.Options) column.Children.Add(Choice(question, group, option, answer, checkedAnswer, dark, toggle));
            return (FrameworkElement)column;
        }).ToList();
        if (groups.Count > 1) stack.Children.Add(ResponsiveRow(groups, groups.Count * 190, 12, 14));
        else foreach (var group in groups) stack.Children.Add(group);
        return stack;
    }

    private static FrameworkElement Choice(GREQuestion question, ChoiceGroup group, ChoiceOption option, GREAnswer answer, bool checkedAnswer,
        bool dark, Action<ChoiceGroup, ChoiceOption>? toggle)
    {
        var selected = answer.IsSelected(group.Id, option.Id);
        var isKey = question.CorrectSelections.TryGetValue(group.Id, out var key) && key.Contains(option.Id);
        var single = group.MaximumSelections == 1;

        var row = new Grid { ColumnSpacing = 11 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        // Segoe Fluent Icons: radio on/off, checkbox on/off.
        var glyph = single ? (selected ? "" : "") : (selected ? "" : "");
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, Foreground = Palette.Brush(selected ? Tone.Blue : Tone.Secondary, dark), VerticalAlignment = VerticalAlignment.Top });
        var label = new TextBlock { Text = option.Text, FontSize = 16, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Brush(Tone.Ink, dark) };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        if (checkedAnswer && (isKey || selected))
        {
            var mark = Text(isKey ? "✓" : "✕", 16, FontWeights.Bold, isKey ? Tone.Teal : Tone.Red, dark);
            Grid.SetColumn(mark, 2);
            row.Children.Add(mark);
        }

        var emphasized = selected || (checkedAnswer && isKey);
        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(11),
            CornerRadius = new CornerRadius(5),
            Background = Fill(selected, isKey, checkedAnswer, dark, sentence: false),
            BorderBrush = Stroke(selected, isKey, checkedAnswer, dark),
            BorderThickness = new Thickness(emphasized ? 2 : 1),
            IsHitTestVisible = toggle is not null && !checkedAnswer,
        };
        AutomationProperties.SetName(button, option.Text);
        if (toggle is not null) button.Click += (_, _) => toggle(group, option);
        return button;
    }

    private static Brush Fill(bool selected, bool isKey, bool checkedAnswer, bool dark, bool sentence)
    {
        if (!checkedAnswer)
            return selected ? Palette.Brush(Tone.BrightBlue, dark, sentence ? 0.28 : 0.16)
                : sentence ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : Palette.Brush(Tone.Input, dark);
        if (isKey) return Palette.Brush(Tone.Teal, dark, 0.2);
        if (selected) return Palette.Brush(Tone.Red, dark, 0.14);
        return sentence ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : Palette.Brush(Tone.Input, dark);
    }

    private static Brush Stroke(bool selected, bool isKey, bool checkedAnswer, bool dark)
    {
        if (!checkedAnswer) return Palette.Brush(selected ? Tone.BrightBlue : Tone.Border, dark);
        if (isKey) return Palette.Brush(Tone.Teal, dark);
        if (selected) return Palette.Brush(Tone.Red, dark);
        return Palette.Brush(Tone.Border, dark);
    }

    private static FrameworkElement BuildFeedback(GREQuestion question, GREAnswer answer, bool correct, string? guessingTip, bool dark)
    {
        var stack = new StackPanel { Spacing = 10 };
        var answers = new StackPanel { Spacing = 3 };
        var tone = correct ? Tone.Teal : Tone.Ink;
        answers.Children.Add(Text($"Your answer: {VerbalDrillPool.AnswerText(answer.Selections, question)}", 14, FontWeights.Normal, tone, dark));
        answers.Children.Add(Text($"Correct answer: {VerbalDrillPool.AnswerText(question.CorrectSelections.ToDictionary(p => p.Key, p => p.Value), question)}", 14, FontWeights.SemiBold, tone, dark));
        stack.Children.Add(answers);

        var explanation = new StackPanel { Spacing = 4 };
        explanation.Children.Add(Text(VerbalDrillSession.ExplanationHeading, 12, FontWeights.Bold, Tone.Secondary, dark));
        var body = Text(question.Explanation, 14, FontWeights.Normal, Tone.Ink, dark);
        body.IsTextSelectionEnabled = true;
        explanation.Children.Add(body);
        stack.Children.Add(new Border { Child = explanation, Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Background = Palette.Brush(Tone.Input, dark) });

        if (guessingTip is not null)
        {
            var tip = new StackPanel { Spacing = 2 };
            tip.Children.Add(Text("💡 " + VerbalDrillSession.GuessingTipHeading, 12, FontWeights.SemiBold, Tone.Ink, dark));
            var tipBody = Text(guessingTip, 14, FontWeights.Normal, Tone.Secondary, dark);
            tipBody.IsTextSelectionEnabled = true;
            tip.Children.Add(tipBody);
            stack.Children.Add(new Border { Child = tip, Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Background = Palette.Brush(Tone.Orange, dark, 0.08) });
        }

        if (!correct && VerbalDrillText.Of(question) is { } type)
            stack.Children.Add(Text("◎ " + type.Tip(), 12, FontWeights.Normal, Tone.Secondary, dark));
        if (question.Source is { } source)
            stack.Children.Add(Text($"{source.Title} · {source.Detail}", 11, FontWeights.Normal, Tone.Secondary, dark));
        return stack;
    }

    private static TextBlock Text(string text, double size, Windows.UI.Text.FontWeight weight, Tone tone, bool dark) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = Palette.Brush(tone, dark),
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
