using System.Globalization;

namespace GreSimulation.Core;

/// <summary>Sidebar pages (ContentView.swift <c>AppPage</c>).</summary>
public enum AppPage
{
    Dashboard,
    Vocabulary,
    History,
    Resources,
}

public static class AppPageText
{
    public static string Title(this AppPage page) => page switch
    {
        AppPage.Dashboard => "Practice Tests",
        AppPage.Vocabulary => "Vocabulary",
        AppPage.History => "Score History",
        _ => "Official Resources",
    };

    /// <summary>The SF Symbol used on the Mac (pick a matching Segoe Fluent icon).</summary>
    public static string MacSymbol(this AppPage page) => page switch
    {
        AppPage.Dashboard => "rectangle.and.pencil.and.ellipsis",
        AppPage.Vocabulary => "text.book.closed",
        AppPage.History => "chart.line.uptrend.xyaxis",
        _ => "checkmark.seal",
    };

    public const string BrandTitle = "GRE Simulator";
    public const string BrandSubtitle = "Windows";
    public const string SidebarFootnote = "Independent GRE® practice simulator";
}

/// <summary>Text and data of DashboardView.swift.</summary>
public static class Dashboard
{
    public const string HeroTitle = "Practice the current GRE format";
    public const string HeroText = "Timed sections, section-level adaptation, mark and review, an on-screen calculator, and transparent score estimates — designed for focused rehearsal on Windows.";
    public const string StartFullLength = "Start full-length test";
    public const string HeroClock = "1:58";
    public const string HeroClockCaption = "5 sections";
    public const string LatestResultTitle = "LATEST RESULT";
    public const string ChooseSessionTitle = "Choose a session";
    public const string BeginButton = "Begin";
    public const string FormatTitle = "Current computer-delivered format";
    public const string AccuracyNotice = "The bank combines authorized source-derived items with clearly labeled original parameterized practice. Exact source details appear only after a timed session ends. This app is not affiliated with or endorsed by ETS.";

    public static string BankSummary(QuestionBank questions, VocabularyBank vocabulary) =>
        $"{questions.All.Count} questions · {vocabulary.Words.Count} vocabulary entries";

    /// <summary>Session cards in display order, with the Mac's SF Symbol names.</summary>
    public static IReadOnlyList<(ExamMode Mode, string MacSymbol)> SessionCards { get; } =
    [
        (ExamMode.FullLength, "timer"),
        (ExamMode.VerbalPractice, "text.quote"),
        (ExamMode.QuantitativePractice, "function"),
        (ExamMode.WritingPractice, "square.and.pencil"),
    ];

    /// <summary>Rows of the format table: measure, section 1, section 2.</summary>
    public static IReadOnlyList<(string Measure, string Section1, string Section2)> FormatRows { get; } =
    [
        ("Analytical Writing", "1 issue task · 30 min", "—"),
        ("Verbal Reasoning", "12 questions · 18 min", "15 questions · 23 min"),
        ("Quantitative Reasoning", "12 questions · 21 min", "15 questions · 26 min"),
    ];

    public static IReadOnlyList<string> FormatHeaders { get; } = ["Measure", "Section 1", "Section 2"];

    /// <summary>Latest-result chips: Verbal, Quant, and Combined (only when both scores exist). "—" for NS.</summary>
    public static IReadOnlyList<(string Title, string Value)> LatestScoreChips(ExamResult result)
    {
        var chips = new List<(string, string)>
        {
            ("Verbal", ScoreOrDash(result.VerbalScore)),
            ("Quant", ScoreOrDash(result.QuantitativeScore)),
        };
        if (result.CombinedScore is int combined) chips.Add(("Combined", combined.ToString(CultureInfo.InvariantCulture)));
        return chips;
    }

    internal static string ScoreOrDash(int? score) => score?.ToString(CultureInfo.InvariantCulture) ?? "—";
}

/// <summary>A history card (LibraryViews.swift <c>HistoryView.historyCard</c>).</summary>
public sealed record HistoryCard(ExamResult Result)
{
    public string Title => Result.Mode.Title();
    public string DateText(CultureInfo? culture = null) => DisplayDates.NumericDateTime(Result.CompletedAt, culture);
    public string DurationText => HistoryScreen.Duration(Result.ElapsedSeconds);
    /// <summary>"Review answers" opens the saved report; otherwise show "Score-only record".</summary>
    public bool CanReviewAnswers => Result.QuestionReviews is { Count: > 0 };
    public IReadOnlyList<(string Title, string Value, string Subtitle)> Metrics
    {
        get
        {
            var metrics = new List<(string, string, string)>
            {
                ("Verbal", Dashboard.ScoreOrDash(Result.VerbalScore), $"{Result.VerbalCorrect}/{Result.VerbalTotal} correct"),
                ("Quant", Dashboard.ScoreOrDash(Result.QuantitativeScore), $"{Result.QuantitativeCorrect}/{Result.QuantitativeTotal} correct"),
            };
            if (Result.WritingEstimate is double writing) metrics.Add(("Writing", SwiftText.FormatFixed(writing, 1), "practice signal"));
            return metrics;
        }
    }

    /// <summary>Section chips, e.g. "Verbal 2", "11/15", "· Hard".</summary>
    public IReadOnlyList<(string Name, string Score, string? Route)> SectionChips =>
        Result.Sections.Select(s => ($"{s.Measure.ShortTitle()} {s.Ordinal}", $"{s.Correct}/{s.Total}", s.Difficulty is { } d ? $"· {d.Title()}" : null)).ToList();
}

/// <summary>Text of HistoryView.</summary>
public static class HistoryScreen
{
    public const string Title = "Score history";
    public const string Subtitle = "Results stay on this device.";
    public const string ClearButton = "Clear history";
    public const string ClearTitle = "Delete every saved result?";
    public const string ClearConfirm = "Delete all results";
    public const string EmptyTitle = "No completed tests";
    public const string EmptyMessage = "Complete a timed practice session to see scores and section performance here.";
    public const string ReviewButton = "Review answers";
    public const string ScoreOnly = "Score-only record";
    public const string SavedReportHeading = "Saved score report";
    public const string SavedReportDone = "Close";

    /// <summary>"h:mm:ss" when an hour or more, else "mm:ss".</summary>
    public static string Duration(int seconds)
    {
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        var remaining = seconds % 60;
        return hours > 0
            ? $"{hours}:{SwiftText.TwoDigits(minutes)}:{SwiftText.TwoDigits(remaining)}"
            : $"{SwiftText.TwoDigits(minutes)}:{SwiftText.TwoDigits(remaining)}";
    }

    public static IReadOnlyList<HistoryCard> Cards(HistoryStore store) => store.Results.Select(r => new HistoryCard(r)).ToList();
}

public sealed record ResourceLink(string Title, string Detail, string Url);

public sealed record ResourceSection(string Title, string MacSymbol, IReadOnlyList<ResourceLink> Links);

/// <summary>Content of ResourcesView (Official Resources page).</summary>
public static class ResourcesPage
{
    public const string Title = "Authoritative preparation resources";
    public const string Subtitle = "Use official ETS material for the test specification and authentic sample items. Use third-party lists only as supplementary study aids.";
    public const string BankTitle = "Offline content bank";
    public const string BankFootnote = "Source-derived and generated items are labeled separately in the post-test answer review.";
    public const string TransparencyTitle = "Content and score transparency";
    public const string TransparencyText = "ETS uses proprietary equating that accounts for test-edition difficulty and the adaptive route. Because ETS does not publish that conversion table, this app reports a linear 130–170 practice estimate from raw accuracy. The writing estimate is a local structural heuristic, not e-rater® or an official GRE score.";
    public const string TrademarkText = "GRE®, POWERPREP®, and e-rater® are trademarks of ETS. This independent app is not affiliated with or endorsed by ETS.";

    /// <summary>The two metric tiles: (title, value, detail).</summary>
    public static IReadOnlyList<(string Title, string Value, string Detail)> BankMetrics(QuestionBank questions, VocabularyBank vocabulary) =>
    [
        ("Questions", questions.All.Count.ToString(CultureInfo.InvariantCulture), "Verbal + Quant + Writing"),
        ("Vocabulary", vocabulary.Words.Count.ToString(CultureInfo.InvariantCulture), "deduplicated entries"),
    ];

    public static IReadOnlyList<ResourceSection> Sections { get; } =
    [
        new("Official test format", "clock.badge.checkmark",
        [
            new("GRE General Test Content & Structure", "Current section counts and timing", "https://www.ets.org/gre/score-users/about/general-test/content-structure.html"),
            new("Understanding GRE Scores", "Section adaptation, raw scores, and equating", "https://www.ets.org/gre/test-takers/general-test/scores/understand-scores.html"),
            new("POWERPREP Practice Tests", "Official computer-delivered practice experience", "https://www.ets.org/gre/test-takers/general-test/prepare/powerprep.html"),
        ]),
        new("Quantitative reasoning", "function",
        [
            new("Official Quantitative Reasoning Overview", "Tested topics and all four question formats", "https://www.ets.org/gre/test-takers/general-test/prepare/content/quantitative-reasoning.html"),
            new("Official GRE Math Review", "Arithmetic, algebra, geometry, and data analysis", "https://www.ets.org/content/dam/ets-org/pdfs/gre/gre-math-review.pdf"),
            new("Khan Academy for GRE Quant", "Free instruction mapped to the ETS Math Review", "https://www.ets.org/gre/test-takers/general-test/prepare/khan-prep-videos.html"),
        ]),
        new("Verbal and vocabulary", "text.book.closed",
        [
            new("Official Verbal Reasoning Overview", "Reading comprehension, text completion, and sentence equivalence", "https://www.ets.org/gre/test-takers/general-test/prepare/content/verbal-reasoning.html"),
            new("liurui39660/3000", "Authorized vocabulary workbook used by this build", "https://github.com/liurui39660/3000"),
            new("Open English WordNet 2025", "CC BY 4.0 definitions for unmatched Gauss terms", "https://en-word.net/"),
            new("ECDICT", "MIT-licensed Chinese lexical supplement", "https://github.com/skywind3000/ECDICT"),
            new("Tatoeba Corpus", "CC0 and CC BY 2.0 FR English example sentences", "https://tatoeba.org/en/downloads"),
        ]),
    ];
}

/// <summary>
/// GRETheme colors (Theme.swift) as sRGB components 0–1, light and dark variants. System colors
/// (canvas, surface, input, border, ink) map to the Windows theme brushes.
/// </summary>
public static class GreTheme
{
    public readonly record struct Rgb(double Red, double Green, double Blue)
    {
        public byte R => (byte)Math.Round(Red * 255);
        public byte G => (byte)Math.Round(Green * 255);
        public byte B => (byte)Math.Round(Blue * 255);
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    public static Rgb HeaderNavy { get; } = new(0.055, 0.17, 0.27);
    public static Rgb ActionBlue { get; } = new(0.05, 0.32, 0.54);
    public static (Rgb Light, Rgb Dark) Navy { get; } = (new(0.055, 0.17, 0.27), new(0.66, 0.84, 0.96));
    public static (Rgb Light, Rgb Dark) Blue { get; } = (new(0.05, 0.32, 0.54), new(0.35, 0.70, 0.94));
    public static (Rgb Light, Rgb Dark) BrightBlue { get; } = (new(0.05, 0.45, 0.72), new(0.36, 0.78, 0.98));
    public static (Rgb Light, Rgb Dark) Teal { get; } = (new(0.08, 0.52, 0.56), new(0.38, 0.82, 0.79));
    public static (Rgb Light, Rgb Dark) Warning { get; } = (new(0.78, 0.34, 0.08), new(1.0, 0.64, 0.36));
}
