using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace GreSimulation.Core;

/// <summary>
/// Swift's <c>Double(String)</c>: the whole string must be a C-locale <c>strtod</c> number (decimal,
/// hexadecimal, <c>inf</c>/<c>infinity</c>/<c>nan</c>), no surrounding whitespace, and out-of-range
/// values are rejected rather than rounded to infinity or zero.
/// </summary>
public static partial class SwiftDouble
{
    [GeneratedRegex(@"^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalPattern();

    [GeneratedRegex(@"^([+-]?)0[xX]([0-9a-fA-F]*)(?:\.([0-9a-fA-F]*))?(?:[pP]([+-]?[0-9]+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex HexPattern();

    public static double? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text) || char.IsWhiteSpace(text[0])) return null;
        var lower = text.ToLowerInvariant();
        var unsigned = lower.TrimStart('+', '-');
        if (unsigned.Length == lower.Length - 1 || unsigned.Length == lower.Length)
        {
            var negative = lower.StartsWith('-');
            if (unsigned is "inf" or "infinity") return negative ? double.NegativeInfinity : double.PositiveInfinity;
            if (unsigned == "nan") return double.NaN;
        }
        if (DecimalPattern().IsMatch(text))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
            if (double.IsInfinity(value)) return null; // ERANGE overflow
            if (value == 0 && text.Any(c => c is >= '1' and <= '9') && MantissaHasNonZero(text)) return null; // ERANGE underflow
            return value;
        }
        var hex = HexPattern().Match(text);
        if (hex.Success && (hex.Groups[2].Length + hex.Groups[3].Length) > 0)
        {
            var digits = hex.Groups[2].Value + hex.Groups[3].Value;
            var mantissa = BigInteger.Parse("0" + digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var exponent = (hex.Groups[4].Success ? int.Parse(hex.Groups[4].Value, CultureInfo.InvariantCulture) : 0) - 4 * hex.Groups[3].Length;
            var value = (double)mantissa * Math.Pow(2, exponent);
            if (double.IsInfinity(value)) return null;
            return hex.Groups[1].Value == "-" ? -value : value;
        }
        return null;
    }

    private static bool MantissaHasNonZero(string text)
    {
        var end = text.IndexOfAny(['e', 'E']);
        var mantissa = end < 0 ? text : text[..end];
        return mantissa.Any(c => c is >= '1' and <= '9');
    }
}

/// <summary>Text helpers that follow Swift's Character (grapheme cluster) semantics and C formatting.</summary>
public static class SwiftText
{
    /// <summary>Swift <c>trimmingCharacters(in: .whitespacesAndNewlines)</c>.</summary>
    public static string TrimWhitespaceAndNewlines(string text) => text.Trim();

    /// <summary>Splits into Swift Characters (extended grapheme clusters).</summary>
    public static IEnumerable<string> Characters(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) yield return (string)enumerator.Current;
    }

    /// <summary>Swift <c>String.count</c> (number of Characters).</summary>
    public static int CharacterCount(string text) => new StringInfo(text).LengthInTextElements;

    private static Rune First(string character) => Rune.GetRuneAt(character, 0);

    public static bool IsWhitespace(string character) => Rune.IsWhiteSpace(First(character));
    public static bool IsPunctuation(string character) => Rune.IsPunctuation(First(character));
    public static bool IsNumber(string character) => Rune.IsNumber(First(character));

    /// <summary>Swift <c>Character.isLetter</c> (the Alphabetic property of the first scalar).</summary>
    public static bool IsLetter(string character)
    {
        var rune = First(character);
        return Rune.IsLetter(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.LetterNumber;
    }

    /// <summary>Normalization used by QuestionBank.validate: drop whitespace and punctuation Characters.</summary>
    public static string StripWhitespaceAndPunctuation(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in Characters(text))
        {
            if (!IsWhitespace(character) && !IsPunctuation(character)) builder.Append(character);
        }
        return builder.ToString();
    }

    /// <summary>Splits on Characters matching <paramref name="isSeparator"/>, omitting empty pieces (Swift <c>split(whereSeparator:)</c>).</summary>
    public static List<string> Split(string text, Func<string, bool> isSeparator)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        foreach (var character in Characters(text))
        {
            if (isSeparator(character))
            {
                if (current.Length > 0) pieces.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        if (current.Length > 0) pieces.Add(current.ToString());
        return pieces;
    }

    /// <summary>
    /// C <c>printf("%.Nf")</c> as used by Swift's <c>String(format:)</c>: exact binary value,
    /// rounded half-to-even, a minus sign kept for negative values that round to zero.
    /// </summary>
    public static string FormatFixed(double value, int decimals)
    {
        if (double.IsNaN(value)) return "nan";
        if (double.IsInfinity(value)) return value > 0 ? "inf" : "-inf";
        var negative = value < 0 || (value == 0 && double.IsNegative(value));
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
        var exponentBits = (int)((bits >> 52) & 0x7FF);
        var fraction = bits & 0xFFFFFFFFFFFFFL;
        BigInteger mantissa = exponentBits == 0 ? fraction : fraction | (1L << 52);
        var exponent = exponentBits == 0 ? -1074 : exponentBits - 1075;

        var scaled = mantissa * BigInteger.Pow(10, decimals);
        BigInteger rounded;
        if (exponent >= 0)
        {
            rounded = scaled << exponent;
        }
        else
        {
            var denominator = BigInteger.One << -exponent;
            var quotient = BigInteger.DivRem(scaled, denominator, out var remainder);
            var twice = remainder * 2;
            rounded = twice > denominator || (twice == denominator && !quotient.IsEven) ? quotient + 1 : quotient;
        }
        var digits = rounded.ToString(CultureInfo.InvariantCulture);
        if (decimals > 0)
        {
            digits = digits.PadLeft(decimals + 1, '0');
            digits = digits[..^decimals] + "." + digits[^decimals..];
        }
        return negative ? "-" + digits : digits;
    }

    /// <summary>Swift <c>String(format: "%02d", value)</c>.</summary>
    public static string TwoDigits(int value) => value.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Swift's <c>"\(double)"</c> description (for example 1.0, 0.25, 1e-05).</summary>
    public static string Describe(double value)
    {
        if (double.IsNaN(value)) return "nan";
        if (double.IsInfinity(value)) return value > 0 ? "inf" : "-inf";
        var magnitude = Math.Abs(value);
        if (magnitude != 0 && (magnitude < 1e-4 || magnitude >= 1e16))
        {
            var shortest = value.ToString("R", CultureInfo.InvariantCulture);
            if (!shortest.Contains('E')) return shortest;
            var parts = shortest.Split('E');
            var exponent = int.Parse(parts[1], CultureInfo.InvariantCulture);
            return $"{parts[0]}e{(exponent < 0 ? "-" : "+")}{Math.Abs(exponent):00}";
        }
        var plain = value.ToString("R", CultureInfo.InvariantCulture);
        return plain.Contains('.') ? plain : plain + ".0";
    }

    /// <summary>Swift <c>Double.rounded()</c> (to nearest, ties away from zero).</summary>
    public static double Rounded(double value) => Math.Round(value, MidpointRounding.AwayFromZero);
}
