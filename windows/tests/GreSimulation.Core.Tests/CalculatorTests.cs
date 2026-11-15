namespace GreSimulation.Core.Tests;

public class CalculatorTests
{
    private static string Press(CalculatorModel calc, params string[] keys)
    {
        foreach (var key in keys) calc.Input(key);
        return calc.Display;
    }

    [Fact]
    public void SelfTestChecks()
    {
        var calc = new CalculatorModel();
        Assert.Equal("14", Press(calc, "2", "+", "3", "×", "4", "="));
        calc.Input("C");
        Assert.Equal("20", Press(calc, "(", "2", "+", "3", ")", "×", "4", "="));
        calc.Input("C");
        Assert.Equal("ERROR", Press(calc, "1", "÷", "0", "="));
        calc.Input("C");
        Assert.Equal("99999999", Press(calc, "9", "9", "9", "9", "9", "9", "9", "9", "9"));
        Assert.Equal("ERROR", Press(calc, "×", "1", "0", "="));
        calc.Input("C");
        Assert.Equal("1.4142136", Press(calc, "2", "√"));
        calc.Input("C");
        Assert.Equal("6", Press(calc, "5", "M+", "C", "MR", "+", "1", "="));
        Assert.Equal("0.3333333", Press(calc, "C", "1", "÷", "3", "="));
    }

    [Fact]
    public void KeySequencesMatchSwift()
    {
        // calc.15 sent the key " 2" (with a space), which the space-separated record cannot carry; see UnknownKeysAreIgnored.
        foreach (var (key, value) in TestContent.SwiftReference.Where(p => p.Key.StartsWith("calc.") && p.Key != "calc.15"))
        {
            var arrow = value.IndexOf(" => ", StringComparison.Ordinal);
            var keys = value[..arrow].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var expected = value[(arrow + 4)..];
            var calc = new CalculatorModel();
            foreach (var k in keys) calc.Input(k);
            var actual = $"{calc.Display} | {calc.Expression} | {SwiftText.Describe(calc.Memory)} | {(calc.HasError ? "true" : "false")} | {calc.TransferText}";
            Assert.True(expected == actual, $"{key} [{string.Join(" ", keys)}]: Swift {expected}, C# {actual}");
        }
    }

    [Fact]
    public void UnknownKeysAreIgnored()
    {
        var calc = new CalculatorModel();
        Assert.Equal("7", Press(calc, "7", "=", " 2"));
    }

    [Fact]
    public void FormatMatchesSwift()
    {
        foreach (var (key, value) in TestContent.SwiftReference.Where(p => p.Key.StartsWith("fmt.")))
        {
            var number = SwiftDouble.Parse(key["fmt.".Length..])!.Value;
            Assert.True(value == CalculatorModel.Format(number), $"{key}: Swift {value}, C# {CalculatorModel.Format(number)}");
        }
    }

    [Fact]
    public void PrintfRoundingIsExact()
    {
        Assert.Equal("12345678", SwiftText.FormatFixed(12345678.5, 0));
        Assert.Equal("2", SwiftText.FormatFixed(2.5, 0));
        Assert.Equal("4", SwiftText.FormatFixed(3.5, 0));
        Assert.Equal("1234567.4", SwiftText.FormatFixed(1234567.45, 1));
        Assert.Equal("-0.000", SwiftText.FormatFixed(-0.0001, 3));
        Assert.Equal("4.5", SwiftText.FormatFixed(4.5, 1));
    }

    [Fact]
    public void PropertyChangesAreRaised()
    {
        var calc = new CalculatorModel();
        var changed = new List<string?>();
        calc.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        calc.Input("5");
        calc.Input("M+");
        Assert.Contains(nameof(CalculatorModel.Display), changed);
        Assert.Contains(nameof(CalculatorModel.HasMemory), changed);
        Assert.True(calc.HasMemory);
        Assert.Equal(5, CalculatorModel.KeyRows.Count);
    }
}
