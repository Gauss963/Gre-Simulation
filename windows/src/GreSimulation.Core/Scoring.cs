namespace GreSimulation.Core;

/// <summary>Port of <c>NumericAnswerMatcher</c> (ExamSession.swift).</summary>
public static class NumericAnswerMatcher
{
    public static bool Matches(string entered, string expected)
    {
        var enteredText = Normalize(entered);
        var expectedText = Normalize(expected);
        if (enteredText == expectedText) return true;
        if (Value(enteredText) is not double enteredValue || Value(expectedText) is not double expectedValue) return false;
        return Math.Abs(enteredValue - expectedValue) < 0.000_000_1;
    }

    private static string Normalize(string value) =>
        SwiftText.TrimWhitespaceAndNewlines(value).Replace(",", "").Replace(" ", "").ToLowerInvariant();

    private static double? Value(string text)
    {
        var pieces = text.Split('/');
        if (pieces.Length == 1) return SwiftDouble.Parse(text);
        if (pieces.Length != 2 || SwiftDouble.Parse(pieces[0]) is not double numerator
            || SwiftDouble.Parse(pieces[1]) is not double denominator || denominator == 0) return null;
        return numerator / denominator;
    }
}

/// <summary>
/// Port of <c>ScoreEstimator</c>: raw-to-scaled conversion estimate for the 27-question measures.
/// </summary>
/// <remarks>
/// ETS converts the number correct across both sections, taking the second section's difficulty into
/// account, through equating; no conversion table is published for the computer test. This estimate
/// interpolates the official paper-test conversion (35 raw points) and weights the second section by
/// route difficulty, so only a fully correct harder path reaches 170.
/// </remarks>
public static class ScoreEstimator
{
    private static readonly (double Raw, double Scaled)[] VerbalTable =
        [(0, 130), (2, 130), (5, 136), (10, 144), (15, 149), (20, 154), (25, 159), (30, 165), (35, 170)];
    private static readonly (double Raw, double Scaled)[] QuantTable =
        [(0, 130), (2, 130), (5, 134), (10, 141), (15, 146), (20, 150), (25, 155), (30, 162), (35, 170)];

    public static int Scaled(GREMeasure measure, int firstCorrect, int firstTotal, int secondCorrect, int secondTotal, QuestionDifficulty? route)
    {
        var weight = route switch
        {
            QuestionDifficulty.Hard => 1.0,
            QuestionDifficulty.Medium => 0.8,
            QuestionDifficulty.Easy => 0.45,
            _ => 1.0,
        };
        var maximum = (double)firstTotal + secondTotal;
        if (!(maximum > 0)) return 130;
        var points = firstCorrect + secondCorrect * weight;
        var raw35 = points / maximum * 35;
        var table = measure == GREMeasure.Verbal ? VerbalTable : QuantTable;
        var scaled = 130.0;
        for (var i = 0; i + 1 < table.Length; i++)
        {
            var lower = table[i];
            var upper = table[i + 1];
            if (!(raw35 >= lower.Raw)) continue;
            var fraction = Math.Min(1, (raw35 - lower.Raw) / (upper.Raw - lower.Raw));
            scaled = lower.Scaled + fraction * (upper.Scaled - lower.Scaled);
        }
        return Math.Min(170, Math.Max(130, (int)Math.Round(scaled, MidpointRounding.AwayFromZero)));
    }

    private static readonly Dictionary<int, int> VerbalPercentiles = new()
    {
        [170] = 99, [169] = 99, [168] = 98, [167] = 97, [166] = 96, [165] = 95, [164] = 93, [163] = 90, [162] = 88, [161] = 85,
        [160] = 82, [159] = 79, [158] = 76, [157] = 72, [156] = 68, [155] = 64, [154] = 59, [153] = 54, [152] = 48, [151] = 43,
        [150] = 39, [149] = 34, [148] = 30, [147] = 27, [146] = 24, [145] = 21, [144] = 18, [143] = 16, [142] = 14, [141] = 11,
        [140] = 10, [139] = 8, [138] = 6, [137] = 5, [136] = 4, [135] = 3, [134] = 2, [133] = 2, [132] = 1, [131] = 1,
    };

    private static readonly Dictionary<int, int> QuantPercentiles = new()
    {
        [170] = 89, [169] = 85, [168] = 80, [167] = 75, [166] = 72, [165] = 67, [164] = 63, [163] = 60, [162] = 57, [161] = 53,
        [160] = 50, [159] = 47, [158] = 45, [157] = 42, [156] = 39, [155] = 37, [154] = 34, [153] = 31, [152] = 29, [151] = 26,
        [150] = 23, [149] = 21, [148] = 19, [147] = 16, [146] = 14, [145] = 12, [144] = 10, [143] = 9, [142] = 7, [141] = 6,
        [140] = 5, [139] = 4, [138] = 3, [137] = 2, [136] = 2, [135] = 1, [134] = 1, [133] = 1,
    };

    private static readonly Dictionary<double, int> WritingPercentiles = new()
    {
        [6.0] = 99, [5.5] = 98, [5.0] = 93, [4.5] = 85, [4.0] = 63, [3.5] = 40, [3.0] = 16, [2.5] = 7, [2.0] = 3, [1.5] = 1, [1.0] = 1,
    };

    /// <summary>Percentile ranks (ETS GRE Table 1A, test takers July 2022 – June 2025); null when not listed.</summary>
    public static int? Percentile(int score, GREMeasure measure) =>
        (measure == GREMeasure.Verbal ? VerbalPercentiles : QuantPercentiles).TryGetValue(score, out var value) ? value : null;

    public static int? WritingPercentile(double score) =>
        WritingPercentiles.TryGetValue(score, out var value) ? value : null;
}
