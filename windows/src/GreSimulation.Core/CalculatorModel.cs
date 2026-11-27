using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GreSimulation.Core;

/// <summary>
/// Port of <c>CalculatorModel</c> (CalculatorView.swift): the GRE on-screen calculator with order of
/// operations, parentheses, memory, square root, CE/C, ± and Transfer Display for Numeric Entry.
/// Bind <see cref="Display"/>, <see cref="Expression"/>, <see cref="HasMemory"/>; send every key with <see cref="Input"/>.
/// </summary>
/// <summary>What the floating calculator panel works with: a practice test or a Quant practice page.</summary>
public interface ICalculatorHost
{
    CalculatorModel Calculator { get; }
    /// <summary>Transfer Display is enabled only for decimal Numeric Entry questions.</summary>
    bool CanTransferFromCalculator { get; }
    /// <summary>Copies the display into the Numeric Entry box when allowed.</summary>
    void TransferCalculatorDisplay();
    void CloseCalculator();
}

public sealed class CalculatorModel : ObservableObject
{
    /// <summary>Key layout of the official GRE calculator (rows of key labels, as on the Mac).</summary>
    public static IReadOnlyList<IReadOnlyList<string>> KeyRows { get; } =
    [
        ["MR", "MC", "M+", "(", ")"],
        ["7", "8", "9", "÷", "C"],
        ["4", "5", "6", "×", "CE"],
        ["1", "2", "3", "−", "√"],
        ["±", "0", ".", "+", "="],
    ];

    private static readonly string[] Operators = ["+", "−", "×", "÷"];

    private string _display = "0";
    private string _expression = "";
    private double _memory;
    private bool _hasError;
    private readonly List<string> _tokens = [];
    private string _entry = "";
    private bool _justEvaluated;

    public string Display { get => _display; private set => SetProperty(ref _display, value); }
    /// <summary>The pending expression shown above the display (tokens joined by spaces).</summary>
    public string Expression { get => _expression; private set => SetProperty(ref _expression, value); }

    public double Memory
    {
        get => _memory;
        private set
        {
            if (SetProperty(ref _memory, value)) OnPropertyChanged(nameof(HasMemory));
        }
    }

    /// <summary>Show the "M" indicator when memory is not zero.</summary>
    public bool HasMemory => Memory != 0;

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value)) OnPropertyChanged(nameof(TransferText));
        }
    }

    /// <summary>Key category for styling: "=" is the action key, digits and "." are input keys.</summary>
    public static bool IsEqualsKey(string key) => key == "=";
    public static bool IsInputKey(string key) => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _) || key == ".";

    public void Input(string key)
    {
        if (HasError && key != "C") return;
        if (string.CompareOrdinal(key, "0") >= 0 && string.CompareOrdinal(key, "9") <= 0)
        {
            Digit(key);
        }
        else
        {
            switch (key)
            {
                case ".": Decimal(); break;
                case "+" or "−" or "×" or "÷": Binary(key); break;
                case "(": OpenParenthesis(); break;
                case ")": CloseParenthesis(); break;
                case "=": Evaluate(); break;
                case "C": Clear(); break;
                case "CE": _entry = ""; break;
                case "±": Negate(); break;
                case "√": SquareRoot(); break;
                case "MC": Memory = 0; break;
                case "MR": _entry = Format(Memory); break;
                case "M+":
                    var total = Memory + CurrentValue;
                    if (Math.Abs(total) > 99_999_999) { _tokens.Clear(); _entry = ""; HasError = true; Display = "ERROR"; }
                    else Memory = total;
                    break;
            }
        }
        Refresh();
    }

    public double CurrentValue => SwiftDouble.Parse(_entry.Length == 0 ? Display : _entry) ?? 0;

    /// <summary>The value written into a Numeric Entry box by Transfer Display ("" after an error).</summary>
    public string TransferText => HasError ? "" : Display;

    private void Digit(string digit)
    {
        if (_justEvaluated) { _tokens.Clear(); _entry = ""; _justEvaluated = false; }
        if (_entry == "0") _entry = digit;
        else if (_entry.Count(char.IsDigit) < 8) _entry += digit;
    }

    private void Decimal()
    {
        if (_justEvaluated) { _tokens.Clear(); _entry = ""; _justEvaluated = false; }
        if (_entry.Length == 0) _entry = "0.";
        else if (!_entry.Contains('.')) _entry += ".";
    }

    private void Binary(string op)
    {
        _justEvaluated = false;
        if (_entry.Length > 0)
        {
            _tokens.Add(_entry);
            _entry = "";
        }
        else if (_tokens.Count > 0 && Operators.Contains(_tokens[^1]))
        {
            _tokens.RemoveAt(_tokens.Count - 1);
        }
        else if (_tokens.Count == 0 || _tokens[^1] == "(")
        {
            _tokens.Add(Display);
        }
        _tokens.Add(op);
    }

    private void OpenParenthesis()
    {
        if (_justEvaluated) { _tokens.Clear(); _justEvaluated = false; }
        if (_entry.Length != 0) return;
        _tokens.Add("(");
    }

    private void CloseParenthesis()
    {
        var opens = _tokens.Count(t => t == "(");
        var closes = _tokens.Count(t => t == ")");
        if (opens <= closes) return;
        if (_entry.Length > 0) { _tokens.Add(_entry); _entry = ""; }
        _tokens.Add(")");
    }

    private void Evaluate()
    {
        if (_entry.Length > 0) { _tokens.Add(_entry); _entry = ""; }
        while (_tokens.Count > 0 && (Operators.Contains(_tokens[^1]) || _tokens[^1] == "(")) _tokens.RemoveAt(_tokens.Count - 1);
        var missing = _tokens.Count(t => t == "(") - _tokens.Count(t => t == ")");
        for (var i = 0; i < missing; i++) _tokens.Add(")");
        if (_tokens.Count == 0) return;
        if (Evaluate(_tokens) is double value && double.IsFinite(value) && Math.Abs(value) <= 99_999_999)
        {
            _entry = Format(value);
            _tokens.Clear();
            _justEvaluated = true;
        }
        else
        {
            _tokens.Clear(); _entry = "";
            HasError = true;
            Display = "ERROR";
        }
    }

    private void Clear()
    {
        _tokens.Clear(); _entry = ""; Display = "0"; HasError = false; _justEvaluated = false;
    }

    private void Negate()
    {
        if (_entry.Length == 0) _entry = Display;
        if (_entry == "0") return;
        _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
    }

    private void SquareRoot()
    {
        var value = CurrentValue;
        if (!(value >= 0)) { _tokens.Clear(); _entry = ""; HasError = true; Display = "ERROR"; return; }
        _entry = Format(Math.Sqrt(value));
    }

    private void Refresh()
    {
        if (HasError) return;
        if (_entry.Length > 0)
        {
            Display = _entry;
        }
        else if (_tokens.LastOrDefault(t => SwiftDouble.Parse(t) is not null) is { } last)
        {
            Display = last;
        }
        else if (_tokens.Count == 0 && !_justEvaluated)
        {
            Display = "0";
        }
        Expression = string.Join(" ", _tokens);
        OnPropertyChanged(nameof(TransferText));
    }

    /// <summary>Shows at most 8 digits, rounding the decimal part to fit (as the GRE calculator does).</summary>
    public static string Format(double value)
    {
        if (SwiftText.Rounded(value) == value && Math.Abs(value) <= 99_999_999)
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        var integerDigits = Math.Max(1, ((long)Math.Floor(Math.Abs(value))).ToString(CultureInfo.InvariantCulture).Length);
        var decimals = Math.Max(0, 8 - integerDigits);
        var text = SwiftText.FormatFixed(value, decimals);
        if (text.Contains('.'))
        {
            text = text.TrimEnd('0');
            if (text.EndsWith('.')) text = text[..^1];
        }
        return text == "-0" ? "0" : text;
    }

    /// <summary>Shunting-yard evaluation with standard precedence; null on a malformed expression or ÷0.</summary>
    public static double? Evaluate(IReadOnlyList<string> tokens)
    {
        var output = new List<double>();
        var operators = new List<string>();
        static int? Precedence(string op) => op switch { "+" or "−" => 1, "×" or "÷" => 2, _ => null };

        bool Apply()
        {
            if (operators.Count == 0) return false;
            var op = operators[^1];
            operators.RemoveAt(operators.Count - 1);
            if (output.Count < 2) return false;
            var b = output[^1];
            var a = output[^2];
            output.RemoveRange(output.Count - 2, 2);
            switch (op)
            {
                case "+": output.Add(a + b); break;
                case "−": output.Add(a - b); break;
                case "×": output.Add(a * b); break;
                case "÷":
                    if (b == 0) return false;
                    output.Add(a / b);
                    break;
                default: return false;
            }
            return true;
        }

        foreach (var token in tokens)
        {
            if (SwiftDouble.Parse(token) is double number)
            {
                output.Add(number);
            }
            else if (token == "(")
            {
                operators.Add(token);
            }
            else if (token == ")")
            {
                while (operators.Count > 0 && operators[^1] != "(")
                {
                    if (!Apply()) return null;
                }
                if (operators.Count == 0) return null;
                var popped = operators[^1];
                operators.RemoveAt(operators.Count - 1);
                if (popped != "(") return null;
            }
            else if (Precedence(token) is int p)
            {
                while (operators.Count > 0 && Precedence(operators[^1]) is int q && q >= p)
                {
                    if (!Apply()) return null;
                }
                operators.Add(token);
            }
        }
        while (operators.Count > 0)
        {
            if (!Apply()) return null;
        }
        return output.Count == 1 ? output[0] : null;
    }
}
