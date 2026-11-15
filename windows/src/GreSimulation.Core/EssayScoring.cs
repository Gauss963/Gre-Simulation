namespace GreSimulation.Core;

/// <summary>
/// Counts misspelled words for the essay feature model. The Mac app uses <c>NSSpellChecker</c>
/// (en_US); on Windows the App should supply one backed by the Windows spell-checking API
/// (ISpellCheckerFactory, "en-US"). Without one, no spelling errors are counted.
/// </summary>
public interface ISpellChecker
{
    /// <summary>Number of misspelled words found scanning <paramref name="text"/> from the start (no wrap).</summary>
    int CountMisspellings(string text);
}

/// <summary>
/// Port of <c>IssueEssayScorer</c> (EssayScoring.swift): scores an Analyze an Issue response on the
/// official 0–6 scale by averaging a rubric rating and a feature-model rating, reported to the
/// nearest half point. It is a practice estimate, not an ETS score.
/// </summary>
public static class IssueEssayScorer
{
    public sealed record Report(double Score, int RubricRating, int FeatureRating, string Summary);

    /// <summary>Set by the App at startup to count spelling errors like the Mac app does.</summary>
    public static ISpellChecker? SpellChecker { get; set; }

    private static readonly HashSet<string> StopWords =
    [
        "the", "a", "an", "and", "or", "but", "of", "to", "in", "on", "at", "for", "with", "is", "are", "was",
        "were", "be", "been", "it", "this", "that", "these", "those", "i", "you", "we", "they", "he", "she",
        "as", "by", "from", "so", "if", "not", "do", "does", "did", "have", "has", "had", "will", "would",
        "can", "could", "should", "their", "its", "our", "more", "most", "some", "such", "than", "which", "who",
    ];

    private static readonly string[] PositionMarkers =
    [
        "i agree", "i disagree", "i believe", "in my view", "i contend", "i would argue", "this essay argues",
        "i maintain", "my position", "to a large extent", "only partially", "largely agree", "largely disagree",
    ];
    private static readonly string[] ConcessionMarkers =
    [
        "admittedly", "granted", "some might argue", "critics", "opponents", "on the other hand", "although",
        "however", "nevertheless", "it is true that", "one could object", "counterargument", "skeptics",
    ];
    private static readonly string[] ExampleMarkers = ["for example", "for instance", "consider", "such as", "in the case of", "to illustrate", "history shows"];
    private static readonly string[] Transitions =
    [
        "first", "second", "third", "furthermore", "moreover", "in addition", "consequently", "therefore", "thus",
        "as a result", "in contrast", "similarly", "finally", "in conclusion", "ultimately", "because", "since",
    ];

    public static Report Score(string essay, string prompt, ISpellChecker? spellChecker = null)
    {
        spellChecker ??= SpellChecker;
        var text = SwiftText.TrimWhitespaceAndNewlines(essay);
        var words = Tokens(text);
        if (words.Count < 15) return new Report(0, 0, 0, "Blank or too short to evaluate (0).");
        var promptTokens = Tokens(prompt);
        if (CopiedRatio(words, promptTokens) > 0.6) return new Report(0, 0, 0, "The response mainly copies the prompt (0).");
        var lowered = text.ToLowerInvariant();
        var content = words.Where(w => !StopWords.Contains(w) && SwiftText.CharacterCount(w) > 2).ToList();
        var promptKeys = promptTokens.Where(w => !StopWords.Contains(w) && SwiftText.CharacterCount(w) > 3).ToHashSet();
        var contentSet = content.ToHashSet();
        var relevance = contentSet.Count(promptKeys.Contains);
        if (relevance == 0) return new Report(0, 0, 0, "The response does not address the issue (0).");

        var paragraphs = text.Split('\n').Count(line => Tokens(line).Count >= 25);
        var sentences = SwiftText.Split(text, c => c is "." or "!" or "?").Select(s => Tokens(s).Count).Where(n => n >= 3).ToList();
        var hasPosition = PositionMarkers.Any(m => lowered.Contains(m, StringComparison.Ordinal));
        var concessions = ConcessionMarkers.Count(m => lowered.Contains(m, StringComparison.Ordinal));
        var examples = ExampleMarkers.Count(m => lowered.Contains(m, StringComparison.Ordinal));
        var transitionCount = Transitions.Count(m => lowered.Contains(m, StringComparison.Ordinal));
        var diversity = (double)contentSet.Count / Math.Max(1, content.Count);
        var averageLength = (double)content.Sum(SwiftText.CharacterCount) / Math.Max(1, content.Count);
        double variety;
        if (sentences.Count <= 3)
        {
            variety = 0.2;
        }
        else
        {
            var mean = (double)sentences.Sum() / sentences.Count;
            var sd = Math.Sqrt(sentences.Select(n => Math.Pow(n - mean, 2)).Sum() / sentences.Count);
            variety = Math.Min(1, sd / 7);
        }
        var misspelled = spellChecker?.CountMisspellings(text) ?? 0;
        var errorRate = (double)misspelled / words.Count * 100;

        // Rater 1: rubric reading (position, development, organization, language control).
        var rubric = 1.0;
        if (words.Count >= 120) rubric += 1;
        if (hasPosition || relevance >= 3) rubric += 0.5;
        if (paragraphs >= 3) rubric += 0.5;
        if (paragraphs >= 4 && words.Count >= 300) rubric += 0.5;
        if (examples >= 1) rubric += 0.5;
        if (examples >= 2 && words.Count >= 350) rubric += 0.5;
        if (concessions >= 1) rubric += 0.5;
        if (concessions >= 2 && transitionCount >= 4) rubric += 0.5;
        if (words.Count >= 450 && diversity > 0.55 && variety > 0.5) rubric += 0.5;
        if (errorRate > 4) rubric -= 1; else if (errorRate > 2) rubric -= 0.5;
        if (words.Count < 150) rubric = Math.Min(rubric, 2);
        else if (words.Count < 250) rubric = Math.Min(rubric, 3);
        else if (words.Count < 350) rubric = Math.Min(rubric, 4);
        var rubricRating = (int)Math.Max(1, Math.Min(6, SwiftText.Rounded(rubric)));

        // Rater 2: feature model (length, vocabulary, sentence variety, cohesion, mechanics).
        var lengthScore = Math.Min(1, words.Count / 550.0);
        var vocabulary = Math.Min(1, 0.6 * Math.Min(1, diversity / 0.62) + 0.4 * Math.Min(1, (averageLength - 4.2) / 2.2));
        var cohesion = Math.Min(1, transitionCount / 6.0);
        var mechanics = Math.Max(0, 1 - errorRate / 5);
        var organization = Math.Min(1, paragraphs / 5.0);
        var feature = 1 + 5 * (0.32 * lengthScore + 0.2 * vocabulary + 0.13 * variety + 0.12 * cohesion + 0.13 * mechanics + 0.1 * organization);
        var featureRating = (int)Math.Max(1, Math.Min(6, SwiftText.Rounded(feature)));

        var average = (rubricRating + featureRating) / 2.0;
        var score = SwiftText.Rounded(average * 2) / 2;
        var level = (int)Math.Floor(score) switch
        {
            6 => "Outstanding: insightful position, compelling development, superior control of language.",
            5 => "Strong: thoughtful position, logically sound development, well organized.",
            4 => "Adequate: clear position with relevant support; generally well controlled language.",
            3 => "Limited: vague or limited development; some lapses in organization or control.",
            2 => "Seriously flawed: unclear position, little relevant support, frequent problems.",
            _ => "Fundamentally deficient: little evidence of an organized response.",
        };
        var summary = $"{level} Ratings {rubricRating} and {featureRating} averaged to {SwiftText.FormatFixed(score, 1)}. {words.Count} words · {paragraphs} paragraphs · {examples} example signals · {concessions} counterargument signals · {misspelled} spelling errors.";
        return new Report(score, rubricRating, featureRating, summary);
    }

    private static List<string> Tokens(string text) =>
        SwiftText.Split(text.ToLowerInvariant(), c => !SwiftText.IsLetter(c) && c != "'");

    private static double CopiedRatio(List<string> response, List<string> prompt)
    {
        static HashSet<string> Grams(List<string> words)
        {
            var grams = new HashSet<string>();
            if (words.Count < 4) return grams;
            for (var i = 0; i <= words.Count - 4; i++) grams.Add(string.Join(" ", words.GetRange(i, 4)));
            return grams;
        }
        var r = Grams(response);
        var p = Grams(prompt);
        return (double)r.Count(p.Contains) / Math.Max(1, r.Count);
    }
}
