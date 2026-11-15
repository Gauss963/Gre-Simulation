using System.Numerics;
using GreSimulation.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace GreSimulation.App.Exam;

/// <summary>
/// The floating GRE calculator (CalculatorView.swift) for one <see cref="ExamSession"/>. It lives on a
/// <see cref="Canvas"/> and can be dragged by its title bar. Keys go to <see cref="ExamSession.Calculator"/>;
/// when the panel has focus the keyboard works too (digits, + − × ÷, parentheses, Enter =, Esc C, Backspace CE).
/// </summary>
public sealed partial class CalculatorPanel : UserControl
{
    private readonly ExamSession _session;
    private Point? _dragStart;
    private double _dragLeft;
    private double _dragTop;

    private static readonly Dictionary<string, string> KeyNames = new()
    {
        ["MR"] = "Memory recall",
        ["MC"] = "Memory clear",
        ["M+"] = "Memory add",
        ["("] = "Open parenthesis",
        [")"] = "Close parenthesis",
        ["÷"] = "Divide",
        ["×"] = "Multiply",
        ["−"] = "Subtract",
        ["+"] = "Add",
        ["C"] = "Clear",
        ["CE"] = "Clear entry",
        ["√"] = "Square root",
        ["±"] = "Change sign",
        ["."] = "Decimal point",
        ["="] = "Equals",
    };

    public CalculatorPanel(ExamSession session)
    {
        _session = session;
        InitializeComponent();
        IsTabStop = true;
        AutomationProperties.SetName(this, "Calculator");
        Shadow = new ThemeShadow();
        Translation = new Vector3(0, 0, 32);
        BuildKeys();
        _session.Calculator.PropertyChanged += (_, _) => UpdateDisplay();
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => Focus(FocusState.Pointer)), true);
        KeyDown += OnKeyDown;
        UpdateDisplay();
        Refresh();
    }

    /// <summary>Updates Transfer Display for the current question (enabled for decimal Numeric Entry only).</summary>
    public void Refresh() => TransferButton.IsEnabled = _session.CanTransferFromCalculator;

    private void BuildKeys()
    {
        var rows = CalculatorModel.KeyRows;
        for (var r = 0; r < rows.Count; r++)
        {
            KeyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var c = 0; c < rows[r].Count; c++)
            {
                if (r == 0) KeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var key = rows[r][c];
                var styleKey = CalculatorModel.IsEqualsKey(key) ? "CalculatorEqualsKeyStyle"
                    : CalculatorModel.IsInputKey(key) ? "CalculatorKeyStyle"
                    : "CalculatorFunctionKeyStyle";
                var button = new Button { Content = key, Style = (Style)Resources[styleKey] };
                AutomationProperties.SetName(button, KeyNames.GetValueOrDefault(key, key));
                button.Click += (_, _) => _session.Calculator.Input(key);
                Grid.SetRow(button, r);
                Grid.SetColumn(button, c);
                KeyGrid.Children.Add(button);
            }
        }
    }

    private void UpdateDisplay()
    {
        var calculator = _session.Calculator;
        ExpressionText.Text = calculator.Expression.Length == 0 ? " " : calculator.Expression;
        DisplayText.Text = calculator.Display;
        MemoryBadge.Visibility = calculator.HasMemory ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(DisplayText, $"Display {calculator.Display}");
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => _session.IsCalculatorPresented = false;

    private void OnTransferClick(object sender, RoutedEventArgs e) => _session.TransferCalculatorDisplay();

    // MARK: - Keyboard

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        var key = KeyFor(e.Key, shift);
        if (key is null) return;
        _session.Calculator.Input(key);
        e.Handled = true;
    }

    private static string? KeyFor(VirtualKey key, bool shift)
    {
        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9) return ((int)(key - VirtualKey.NumberPad0)).ToString();
        if (!shift && key >= VirtualKey.Number0 && key <= VirtualKey.Number9) return ((int)(key - VirtualKey.Number0)).ToString();
        return (key, shift) switch
        {
            (VirtualKey.Number9, true) => "(",
            (VirtualKey.Number0, true) => ")",
            (VirtualKey.Number8, true) => "×",
            (VirtualKey.Multiply, _) => "×",
            (VirtualKey.Add, _) => "+",
            ((VirtualKey)187, true) => "+",   // = / + key
            ((VirtualKey)187, false) => "=",
            (VirtualKey.Subtract, _) => "−",
            ((VirtualKey)189, false) => "−",  // - key
            (VirtualKey.Divide, _) => "÷",
            ((VirtualKey)191, false) => "÷",  // / key
            (VirtualKey.Decimal, _) => ".",
            ((VirtualKey)190, false) => ".",  // . key
            (VirtualKey.Enter, _) => "=",
            (VirtualKey.Escape, _) => "C",
            (VirtualKey.Back, _) => "CE",
            (VirtualKey.Delete, _) => "CE",
            _ => null,
        };
    }

    // MARK: - Dragging (the title bar moves the panel on its Canvas, kept inside the canvas bounds)

    private void OnDragPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Parent is not Canvas canvas) return;
        _dragStart = e.GetCurrentPoint(canvas).Position;
        _dragLeft = Canvas.GetLeft(this);
        _dragTop = Canvas.GetTop(this);
        DragHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnDragMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragStart is not { } start || Parent is not Canvas canvas) return;
        var point = e.GetCurrentPoint(canvas).Position;
        MoveTo(_dragLeft + point.X - start.X, _dragTop + point.Y - start.Y);
        e.Handled = true;
    }

    private void OnDragReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragStart = null;
        DragHandle.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnDragCaptureLost(object sender, PointerRoutedEventArgs e) => _dragStart = null;

    /// <summary>Places the panel at (left, top) on its canvas, keeping it fully visible.</summary>
    public void MoveTo(double left, double top)
    {
        if (Parent is not Canvas canvas) return;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : 480;
        Canvas.SetLeft(this, Math.Max(0, Math.Min(left, canvas.ActualWidth - width)));
        Canvas.SetTop(this, Math.Max(0, Math.Min(top, canvas.ActualHeight - height)));
    }
}
