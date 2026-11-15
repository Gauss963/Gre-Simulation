using System.Globalization;

namespace GreSimulation.Core;

/// <summary>Date formats used by the Mac screens (local time).</summary>
public static class DisplayDates
{
    /// <summary>Score report header: <c>.formatted(date: .abbreviated, time: .shortened)</c>, e.g. "Nov 15, 2026 at 11:27 PM".</summary>
    public static string AbbreviatedDateShortTime(DateTimeOffset date, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var local = date.ToLocalTime();
        return $"{local.ToString("MMM d, yyyy", culture)} at {local.ToString(culture.DateTimeFormat.ShortTimePattern, culture)}";
    }

    /// <summary>History card: <c>.dateTime.year().month().day().hour().minute()</c>, e.g. "11/15/2026, 11:27 PM".</summary>
    public static string NumericDateTime(DateTimeOffset date, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var local = date.ToLocalTime();
        return $"{local.ToString(culture.DateTimeFormat.ShortDatePattern, culture)}, {local.ToString(culture.DateTimeFormat.ShortTimePattern, culture)}";
    }

    /// <summary>Dashboard latest result: <c>.dateTime.month(.abbreviated).day().year()</c>, e.g. "Nov 15, 2026".</summary>
    public static string AbbreviatedDate(DateTimeOffset date, CultureInfo? culture = null) =>
        date.ToLocalTime().ToString("MMM d, yyyy", culture ?? CultureInfo.CurrentCulture);
}

/// <summary>One score card at the top of the report.</summary>
public sealed record ScoreCard(string Title, string ScoreText, string RawText, string Range, int? Percentile)
{
    /// <summary>"82th percentile" (the Mac app always appends "th"), or null.</summary>
    public string? PercentileText => Percentile is int p ? $"{p}th percentile" : null;
}

/// <summary>One row of "Section performance".</summary>
public sealed record SectionPerformanceRow(SectionSummary Section)
{
    public string Title => $"{Section.Measure.Title()} · Section {Section.Ordinal}";
    /// <summary>"Hard route" etc., or null for section 1 / Writing.</summary>
    public string? RouteText => Section.Difficulty is { } d ? $"{d.Title()} route" : null;
    public bool HasScore => Section.Total > 0;
    public string ScoreText => Section.Total > 0 ? $"{Section.Correct} / {Section.Total}" : "Response recorded";
    /// <summary>Progress bar fraction (only when <see cref="HasScore"/>).</summary>
    public double Fraction => Section.Total > 0 ? (double)Section.Correct / Section.Total : 0;
}

/// <summary>One reviewed question of the answer review.</summary>
public sealed record ReviewItem(QuestionReviewRecord Record, string? GuessingTip)
{
    public GREQuestion Question => Record.Question;
    public bool IsCorrect => Record.IsCorrect;
    public string Heading => $"Section {Record.SectionOrdinal} · Question {Record.QuestionNumber}";
    public string YourAnswerText => $"Your answer: {ScoreReport.AnswerText(Record.Question, Record.Answer)}";
    public string CorrectAnswerText => $"Correct answer: {ScoreReport.CorrectAnswerText(Record.Question)}";
    /// <summary>"title · detail", or null when the question has no source.</summary>
    public string? SourceText => Question.Source is { } s ? $"{s.Title} · {s.Detail}" : null;
    /// <summary>The Mac shows a closed book for authorized source items and a wand for generated ones.</summary>
    public bool IsAuthorizedSourceItem => Question.Source?.IsAuthorizedSourceItem == true;
}

/// <summary>A collapsible group of the answer review (Verbal, then Quant).</summary>
public sealed record ReviewGroup(GREMeasure Measure, IReadOnlyList<ReviewItem> Items)
{
    public string Title => Measure.Title();
    public string CorrectText => $"{Items.Count(i => i.IsCorrect)} / {Items.Count} correct";
}

/// <summary>
/// Everything ScoreReportView.swift displays, for a result just finished ("Practice test complete",
/// button "Save &amp; Close") or reopened from history ("Saved score report", button "Close").
/// </summary>
public sealed class ScoreReport
{
    public const string GuessingTipHeading = "猜題法：單字不熟也能縮小範圍";
    public const string EstimateNotice = "Scores below are practice estimates, not official GRE scores.";
    public const string WritingFeedbackTitle = "Analytical Writing feedback";
    public const string ReviewIntro = "Saved questions, your answers, correct answers, and explanations remain available after the session ends.";
    public const string ReviewUnavailableTitle = "Detailed review unavailable";
    public const string ReviewUnavailableMessage = "This result was saved before version 1.1.0, when question and answer details were not retained.";
    public const string MethodologyTitle = "How this estimate works";
    public const string MethodologyScores = "As on the real GRE, the second section of each measure is chosen by first-section performance (below 40% Easy, 40–66% Medium, 67% or above Hard). Correct answers on a harder second section count for more, and the weighted raw score is converted to 130–170 with conversion tables modeled on ETS's published paper-test tables. ETS's actual equating is proprietary, so an official score may differ. A measure with no answered questions reports NS, as ETS does. Percentiles use ETS's published interpretive data.";
    public const string MethodologyWriting = "ETS scores the Issue essay 0–6 by averaging a trained reader and the e-rater engine. This simulator likewise averages two ratings: a rubric reading (position, development, examples, counterarguments, organization) and a feature model (length, vocabulary, sentence variety, cohesion, mechanics). Off-topic, copied, or blank responses score 0. It cannot judge reasoning quality as a trained reader can.";

    public ScoreReport(ExamResult result, QuestionBank? bank, string heading = "Practice test complete", string doneTitle = "Save & Close")
    {
        Result = result;
        Heading = heading;
        DoneTitle = doneTitle;

        var cards = new List<ScoreCard>();
        if (result.VerbalTotal > 0)
            cards.Add(new ScoreCard("Verbal Reasoning", result.VerbalScore?.ToString(CultureInfo.InvariantCulture) ?? "NS",
                $"{result.VerbalCorrect} of {result.VerbalTotal} correct", "130–170",
                result.VerbalScore is int v ? ScoreEstimator.Percentile(v, GREMeasure.Verbal) : null));
        if (result.QuantitativeTotal > 0)
            cards.Add(new ScoreCard("Quantitative Reasoning", result.QuantitativeScore?.ToString(CultureInfo.InvariantCulture) ?? "NS",
                $"{result.QuantitativeCorrect} of {result.QuantitativeTotal} correct", "130–170",
                result.QuantitativeScore is int q ? ScoreEstimator.Percentile(q, GREMeasure.Quantitative) : null));
        if (result.WritingEstimate is double writing)
            cards.Add(new ScoreCard("Analytical Writing", SwiftText.FormatFixed(writing, 1),
                "Two ratings averaged, half-point scale", "0–6", ScoreEstimator.WritingPercentile(writing)));
        ScoreCards = cards;

        SectionRows = result.Sections.Select(s => new SectionPerformanceRow(s)).ToList();
        var records = result.QuestionReviews ?? [];
        ReviewGroups = new[] { GREMeasure.Verbal, GREMeasure.Quantitative }
            .Select(m => new ReviewGroup(m, records.Where(r => r.Question.Measure == m)
                .Select(r => new ReviewItem(r, bank?.GuessingTip(r.Question))).ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();
        HasReviewRecords = records.Count > 0;
    }

    public ExamResult Result { get; }
    public string Heading { get; }
    public string DoneTitle { get; }
    public string Subtitle(CultureInfo? culture = null) => $"{Result.Mode.Title()} · {DisplayDates.AbbreviatedDateShortTime(Result.CompletedAt, culture)}";
    public IReadOnlyList<ScoreCard> ScoreCards { get; }
    /// <summary>Shown under "Analytical Writing feedback" when not null.</summary>
    public string? WritingSummary => Result.WritingSummary;
    public IReadOnlyList<SectionPerformanceRow> SectionRows { get; }
    public bool HasReviewRecords { get; }
    public IReadOnlyList<ReviewGroup> ReviewGroups { get; }

    /// <summary>"Your answer" text: the numeric entry, "N-word response", selected option texts joined by " · ", or "Not answered".</summary>
    public static string AnswerText(GREQuestion question, GREAnswer answer)
    {
        if (question.Format == QuestionFormat.NumericEntry)
            return answer.NumericEntry.Length == 0 ? "Not answered" : answer.NumericEntry;
        if (question.Format == QuestionFormat.Essay)
        {
            var words = SwiftText.Split(answer.EssayText, SwiftText.IsWhitespace).Count;
            return words == 0 ? "Not answered" : $"{words}-word response";
        }
        var values = question.Groups.SelectMany(group =>
        {
            var selected = answer.Selections.GetValueOrDefault(group.Id) ?? [];
            return group.Options.Where(o => selected.Contains(o.Id)).Select(o => o.Text);
        }).ToList();
        return values.Count == 0 ? "Not answered" : string.Join(" · ", values);
    }

    /// <summary>"Correct answer" text: accepted numeric answers (sorted) joined by " or ", or keyed option texts joined by " · ".</summary>
    public static string CorrectAnswerText(GREQuestion question)
    {
        if (question.Format == QuestionFormat.NumericEntry)
            return string.Join(" or ", question.AcceptedNumericAnswers.Order(StringComparer.Ordinal));
        return string.Join(" · ", question.Groups.SelectMany(group =>
        {
            var keyed = question.CorrectSelections.GetValueOrDefault(group.Id) ?? [];
            return group.Options.Where(o => keyed.Contains(o.Id)).Select(o => o.Text);
        }));
    }
}
