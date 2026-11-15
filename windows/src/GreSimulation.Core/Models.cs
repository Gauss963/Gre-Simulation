using System.Text.Json.Serialization;

namespace GreSimulation.Core;

// Port of Models.swift. JSON names and enum raw values match the Swift Codable encoding exactly
// (see GreJson for the serializer options).

public enum GREMeasure
{
    AnalyticalWriting,
    Verbal,
    Quantitative,
}

public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard,
}

public enum QuestionFormat
{
    SingleChoice,
    MultipleChoice,
    TextCompletion,
    SentenceEquivalence,
    NumericEntry,
    QuantitativeComparison,
    SelectInPassage,
    Essay,
}

public enum ExamMode
{
    FullLength,
    VerbalPractice,
    QuantitativePractice,
    WritingPractice,
}

public enum QuantFigureKind
{
    Table,
    Bar,
    GroupedBar,
    Line,
    Pie,
    Scatter,
    Histogram,
    BoxPlot,
    NormalCurve,
    Venn,
}

/// <summary>Display text and raw values for the model enums (Swift computed properties).</summary>
public static class ModelText
{
    /// <summary>The Swift rawValue (also the JSON value), for example "analyticalWriting".</summary>
    public static string RawValue(this GREMeasure measure) => measure switch
    {
        GREMeasure.AnalyticalWriting => "analyticalWriting",
        GREMeasure.Verbal => "verbal",
        _ => "quantitative",
    };

    public static string Title(this GREMeasure measure) => measure switch
    {
        GREMeasure.AnalyticalWriting => "Analytical Writing",
        GREMeasure.Verbal => "Verbal Reasoning",
        _ => "Quantitative Reasoning",
    };

    public static string ShortTitle(this GREMeasure measure) => measure switch
    {
        GREMeasure.AnalyticalWriting => "Writing",
        GREMeasure.Verbal => "Verbal",
        _ => "Quant",
    };

    public static string RawValue(this QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "easy",
        QuestionDifficulty.Medium => "medium",
        _ => "hard",
    };

    public static string Title(this QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "Easy",
        QuestionDifficulty.Medium => "Medium",
        _ => "Hard",
    };

    /// <summary>Swift's "\(format)" (the case name), for example "numericEntry".</summary>
    public static string RawValue(this QuestionFormat format) => JsonNamingPolicyCamel(format.ToString());

    public static string Directions(this QuestionFormat format) => format switch
    {
        QuestionFormat.SingleChoice => "Select one answer choice.",
        QuestionFormat.MultipleChoice => "Select one or more answer choices, as directed.",
        QuestionFormat.TextCompletion => "For each blank, select one entry from the corresponding column of choices.",
        QuestionFormat.SentenceEquivalence => "Select the two answer choices that, when used to complete the sentence, fit the meaning of the sentence as a whole and produce completed sentences that are alike in meaning.",
        QuestionFormat.NumericEntry => "Enter your answer as an integer or a decimal.",
        QuestionFormat.QuantitativeComparison => "Compare Quantity A and Quantity B, using additional information centered above the two quantities if it is given.",
        QuestionFormat.SelectInPassage => "Click on the sentence in the passage that meets the requirements stated in the question.",
        _ => "Write a response in which you discuss the extent to which you agree or disagree with the statement and explain your reasoning.",
    };

    public static string RawValue(this ExamMode mode) => JsonNamingPolicyCamel(mode.ToString());

    public static string Title(this ExamMode mode) => mode switch
    {
        ExamMode.FullLength => "Full-length Practice Test",
        ExamMode.VerbalPractice => "Verbal Reasoning Measure",
        ExamMode.QuantitativePractice => "Quantitative Reasoning Measure",
        _ => "Analytical Writing",
    };

    public static string Subtitle(this ExamMode mode) => mode switch
    {
        ExamMode.FullLength => "5 sections · 1 hr 58 min · Writing first, then Verbal and Quant in any order",
        ExamMode.VerbalPractice => "2 adaptive sections · 27 questions · 41 minutes",
        ExamMode.QuantitativePractice => "2 adaptive sections · 27 questions · 47 minutes",
        _ => "Analyze an Issue · 30 minutes",
    };

    private static string JsonNamingPolicyCamel(string name) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);
}

public sealed record ChoiceOption
{
    public required string Id { get; init; }
    public required string Text { get; init; }
}

public sealed record ChoiceGroup
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public required IReadOnlyList<ChoiceOption> Options { get; init; }
    public required int MaximumSelections { get; init; }
}

public sealed record QuestionSource
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required bool IsAuthorizedSourceItem { get; init; }
}

public sealed record QuantFigurePoint
{
    public string? Label { get; init; }
    public double? X { get; init; }
    public double? Value { get; init; }
    public double? Low { get; init; }
    public double? Q1 { get; init; }
    public double? Median { get; init; }
    public double? Q3 { get; init; }
    public double? High { get; init; }
}

public sealed record QuantFigureSeries
{
    public required string Name { get; init; }
    public required IReadOnlyList<QuantFigurePoint> Points { get; init; }

    [JsonIgnore] public string Id => Name;
}

public sealed record QuantFigureAnnotation
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }

    [JsonIgnore] public string Id => $"{Label}-{SwiftText.Describe(X)}-{SwiftText.Describe(Y)}";
}

public sealed record QuantFigure
{
    public required QuantFigureKind Kind { get; init; }
    public required string Title { get; init; }
    public string? Caption { get; init; }
    public string? XAxisTitle { get; init; }
    public string? YAxisTitle { get; init; }
    public IReadOnlyList<string>? Headers { get; init; }
    public IReadOnlyList<IReadOnlyList<string>>? Rows { get; init; }
    public required IReadOnlyList<QuantFigureSeries> Series { get; init; }
    public IReadOnlyList<QuantFigureAnnotation>? Annotations { get; init; }
}

/// <summary>One question (Swift <c>GREQuestion</c>). Immutable once loaded.</summary>
public sealed class GREQuestion
{
    public required string Id { get; init; }
    public required GREMeasure Measure { get; init; }
    public required QuestionDifficulty Difficulty { get; init; }
    public required QuestionFormat Format { get; init; }
    public string? Stimulus { get; init; }
    public required string Prompt { get; init; }
    public string? QuantityA { get; init; }
    public string? QuantityB { get; init; }
    public required IReadOnlyList<ChoiceGroup> Groups { get; init; }
    /// <summary>Group id → the set of correct option ids (JSON: object of arrays).</summary>
    public required IReadOnlyDictionary<string, HashSet<string>> CorrectSelections { get; init; }
    public required HashSet<string> AcceptedNumericAnswers { get; init; }
    public required string Explanation { get; init; }
    public required string ContentArea { get; init; }
    public QuestionSource? Source { get; init; }
    public QuantFigure? Figure { get; init; }
    /// <summary>Questions that share a passage or data display; they are delivered together.</summary>
    [JsonPropertyName("setID")] public string? SetID { get; init; }
    /// <summary>Analytical Writing: the task-specific instructions printed under the issue statement.</summary>
    public string? Instructions { get; init; }

    [JsonIgnore] public bool IsScored => Format != QuestionFormat.Essay;

    /// <summary>Reading passages and data displays shared by several questions form one set.</summary>
    [JsonIgnore] public string SetKey => SetID ?? (Stimulus is not null ? "stim:" + Stimulus : Id);

    public override string ToString() => Id;
}

/// <summary>A response (Swift <c>GREAnswer</c>, a value type: always copy with <see cref="Clone"/> before changing).</summary>
public sealed class GREAnswer
{
    public Dictionary<string, HashSet<string>> Selections { get; init; } = new();
    public string NumericEntry { get; set; } = "";
    public string EssayText { get; set; } = "";

    [JsonIgnore]
    public bool IsEmpty =>
        Selections.Values.All(s => s.Count == 0)
        && NumericEntry.Trim().Length == 0
        && EssayText.Trim().Length == 0;

    public GREAnswer Clone() => new()
    {
        Selections = Selections.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value)),
        NumericEntry = NumericEntry,
        EssayText = EssayText,
    };

    public bool IsSelected(string groupId, string optionId) =>
        Selections.TryGetValue(groupId, out var set) && set.Contains(optionId);
}

/// <summary>A timed section of a form (Swift <c>ExamSection</c>).</summary>
public sealed class ExamSection
{
    public required string Id { get; init; }
    public required GREMeasure Measure { get; init; }
    public required int Ordinal { get; init; }
    public required int DurationSeconds { get; init; }
    public required IReadOnlyList<GREQuestion> Questions { get; init; }
    public QuestionDifficulty? AdaptiveDifficulty { get; init; }

    public string DisplayTitle => Measure == GREMeasure.AnalyticalWriting
        ? Measure.Title()
        : $"{Measure.Title()} · Section {Ordinal}";
}

public sealed record SectionSummary
{
    public required GREMeasure Measure { get; init; }
    public required int Ordinal { get; init; }
    public QuestionDifficulty? Difficulty { get; init; }
    public required int Correct { get; init; }
    public required int Total { get; init; }
    public required int Answered { get; init; }
}

public sealed class QuestionReviewRecord
{
    [JsonPropertyName("sectionID")] public required string SectionID { get; init; }
    public required int SectionOrdinal { get; init; }
    public QuestionDifficulty? SectionDifficulty { get; init; }
    public required int QuestionNumber { get; init; }
    public required GREQuestion Question { get; init; }
    public required GREAnswer Answer { get; init; }
    public required bool IsCorrect { get; init; }

    [JsonIgnore] public string Id => $"{SectionID}-{Question.Id}";
}

/// <summary>A finished test as saved in score history (Swift <c>ExamResult</c>).</summary>
public sealed class ExamResult
{
    public required Guid Id { get; init; }
    /// <summary>Encoded like Swift's default Date strategy: seconds since 2001-01-01 UTC.</summary>
    public required DateTimeOffset CompletedAt { get; init; }
    public required ExamMode Mode { get; init; }
    public int? VerbalScore { get; init; }
    public int? QuantitativeScore { get; init; }
    public double? WritingEstimate { get; init; }
    public required int VerbalCorrect { get; init; }
    public required int VerbalTotal { get; init; }
    public required int QuantitativeCorrect { get; init; }
    public required int QuantitativeTotal { get; init; }
    public required int ElapsedSeconds { get; init; }
    public required IReadOnlyList<SectionSummary> Sections { get; init; }
    public IReadOnlyList<QuestionReviewRecord>? QuestionReviews { get; init; }
    public string? WritingSummary { get; init; }

    [JsonIgnore]
    public int? CombinedScore => VerbalScore is int v && QuantitativeScore is int q ? v + q : null;
}

public sealed class VocabularyWord
{
    public required string Word { get; init; }
    public string Pronunciation { get; init; } = "";
    public string Definition { get; init; } = "";
    public string Chinese { get; init; } = "";
    public IReadOnlyList<string> Synonyms { get; init; } = [];
    public string Example { get; init; } = "";
    public string? ExampleSource { get; init; }
    [JsonPropertyName("exampleSourceURL")] public string? ExampleSourceURL { get; init; }
    public IReadOnlyList<string> Sources { get; init; } = [];
    public bool IsHighFrequency { get; init; }
    public string? PartOfSpeech { get; init; }

    [JsonIgnore] public string Id => Word;

    public override string ToString() => Word;
}
