namespace GreSimulation.Core;

/// <summary>
/// Port of QuestionBank.swift (+ VerbalQuestionBank.swift and QuantitativeQuestionBank.swift, whose
/// Swift literals are embedded as SwiftLiteralQuestions.json). Loads every bundled bank, drops retired
/// items, and remembers recently delivered questions (exposure rotation) in the <see cref="KeyValueStore"/>.
/// </summary>
public sealed class QuestionBank
{
    private readonly KeyValueStore _store;
    private readonly Random _random;
    private readonly object _exposureGate = new();

    /// <summary>The default Analyze an Issue task ("aw-001").</summary>
    public GREQuestion WritingQuestion { get; }
    public IReadOnlyList<GREQuestion> VerbalQuestions { get; }
    public IReadOnlyList<GREQuestion> QuantitativeQuestions { get; }
    public IReadOnlyList<GREQuestion> ExpandedQuestions { get; }
    public IReadOnlyList<GREQuestion> Authorized20260720Questions { get; }
    public IReadOnlyList<GREQuestion> Authorized20260720Part2Questions { get; }
    public IReadOnlyList<GREQuestion> BalancedQuantQuestions { get; }
    /// <summary>Issue topics with official instruction types and Select-in-Passage reading sets.</summary>
    public IReadOnlyList<GREQuestion> Additions2026 { get; }

    /// <summary>Imported items found broken (id → reason). Kept in the source files but never delivered.</summary>
    public IReadOnlyDictionary<string, string> RetiredQuestionReasons { get; }

    /// <summary>Verbal question id → how to narrow down or guess the answer (Taiwan Traditional Chinese).</summary>
    public IReadOnlyDictionary<string, string> GuessingTips { get; }

    /// <summary>Every deliverable Verbal and Quant question (retired items removed).</summary>
    public IReadOnlyList<GREQuestion> All { get; }

    /// <summary>Every Analyze an Issue task (aw-001 first).</summary>
    public IReadOnlyList<GREQuestion> WritingQuestions { get; }

    public QuestionBank(string resourceDirectory, KeyValueStore store, Random? random = null)
    {
        _store = store;
        _random = random ?? Random.Shared;

        var literal = BundledResource.DecodeEmbedded<List<GREQuestion>>("SwiftLiteralQuestions.json");
        WritingQuestion = literal.First(q => q.Id == "aw-001");
        VerbalQuestions = literal.Where(q => q.Id != "aw-001" && q.Measure == GREMeasure.Verbal).ToList();
        QuantitativeQuestions = literal.Where(q => q.Id != "aw-001" && q.Measure == GREMeasure.Quantitative).ToList();

        List<GREQuestion> Load(string name) => BundledResource.Decode<List<GREQuestion>>(resourceDirectory, name) ?? [];
        ExpandedQuestions = Load("ExpandedQuestions");
        Authorized20260720Questions = Load("Authorized20260720Questions");
        Authorized20260720Part2Questions = Load("Authorized20260720Part2Questions");
        BalancedQuantQuestions = Load("BalancedQuantQuestions");
        Additions2026 = Load("GRE2026Additions");
        RetiredQuestionReasons = BundledResource.Decode<Dictionary<string, string>>(resourceDirectory, "RetiredQuestions") ?? new();
        GuessingTips = BundledResource.Decode<Dictionary<string, string>>(resourceDirectory, "VerbalGuessingTips") ?? new();

        All = VerbalQuestions.Concat(QuantitativeQuestions).Concat(ExpandedQuestions)
            .Concat(Authorized20260720Questions).Concat(Authorized20260720Part2Questions)
            .Concat(BalancedQuantQuestions).Concat(Additions2026.Where(q => q.Measure != GREMeasure.AnalyticalWriting))
            .Where(q => !RetiredQuestionReasons.ContainsKey(q.Id))
            .ToList();

        WritingQuestions = new[] { WritingQuestion }
            .Concat(Authorized20260720Questions.Where(q => q.Measure == GREMeasure.AnalyticalWriting))
            .Concat(Authorized20260720Part2Questions.Where(q => q.Measure == GREMeasure.AnalyticalWriting))
            .Concat(Additions2026.Where(q => q.Measure == GREMeasure.AnalyticalWriting))
            .ToList();
    }

    internal Random Random => _random;

    /// <summary>Question counts per source title (Official Resources screen), sorted by title.</summary>
    public IReadOnlyList<(string Title, int Count)> SourceSummary =>
        All.GroupBy(q => q.Source?.Title ?? "Original app question bank")
            .Select(g => (g.Key, g.Count()))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToList();

    /// <summary>The guessing tip shown under a Verbal explanation, if any.</summary>
    public string? GuessingTip(GREQuestion question) =>
        question.Measure == GREMeasure.Verbal && GuessingTips.TryGetValue(question.Id, out var tip) ? tip : null;

    /// <summary>
    /// Picks <paramref name="count"/> questions of a measure, avoiding recent exposure and (for Quant)
    /// recently seen template families; with no difficulty, about a third from each level.
    /// Remembers what it returns. (Swift <c>questions(for:difficulty:count:excluding:)</c>.)
    /// </summary>
    public IReadOnlyList<GREQuestion> Questions(GREMeasure measure, QuestionDifficulty? difficulty, int count, IReadOnlySet<string>? excluding = null)
    {
        var available = All.Where(q => q.Measure == measure && (excluding is null || !excluding.Contains(q.Id))).ToList();
        var recent = RecentQuestionIDs(measure);
        var recentFamilies = RecentQuestionFamilies(measure);
        List<GREQuestion> ExposureOrdered(IEnumerable<GREQuestion> candidates) =>
            DiversifiedExposureOrder(candidates.ToList(), recent, recentFamilies);

        if (difficulty is null)
        {
            var each = Math.Max(1, count / 3);
            var selected = new List<GREQuestion>();
            var selectedScenarios = new HashSet<string>();
            foreach (var level in Enum.GetValues<QuestionDifficulty>())
            {
                var levelCandidates = available.Where(q => q.Difficulty == level).ToList();
                var ordered = ExposureOrdered(levelCandidates.Where(q => !selectedScenarios.Contains(ScenarioFamily(q))))
                    .Concat(ExposureOrdered(levelCandidates.Where(q => selectedScenarios.Contains(ScenarioFamily(q)))));
                var levelSelection = ordered.Take(each).ToList();
                selected.AddRange(levelSelection);
                selectedScenarios.UnionWith(levelSelection.Select(ScenarioFamily));
            }
            if (selected.Count < count)
            {
                var used = selected.Select(q => q.Id).ToHashSet();
                selected.AddRange(ExposureOrdered(available.Where(q => !used.Contains(q.Id))).Take(count - selected.Count));
            }
            var mixed = Shuffled(selected).Take(count).ToList();
            RememberExposure(mixed, measure);
            return mixed;
        }

        var picked = ExposureOrdered(available.Where(q => q.Difficulty == difficulty)).Take(count).ToList();
        if (picked.Count < count)
        {
            var used = picked.Select(q => q.Id).ToHashSet();
            var fallback = available
                .Where(q => !used.Contains(q.Id))
                .OrderBy(q => recent.Contains(q.Id) ? 1 : 0)
                .ThenBy(q => Distance(q.Difficulty, difficulty.Value));
            picked.AddRange(fallback.Take(count - picked.Count));
        }
        var result = Shuffled(picked);
        RememberExposure(result, measure);
        return result;
    }

    /// <summary>Port of <c>QuestionBank.validate()</c>; an empty list means the bank is sound.</summary>
    public IReadOnlyList<string> Validate()
    {
        var issues = new List<string>();
        var ids = new[] { WritingQuestion.Id }.Concat(All.Select(q => q.Id)).ToList();
        if (ids.Distinct().Count() != ids.Count) issues.Add("Question IDs must be unique.");

        // Boilerplate that only restated the answer; every Verbal item now has a real explanation.
        string[] boilerplate = ["請依文章中的明示資訊、語意關係", "能使句子的轉折、因果與語氣關係一致", "代入後，句子的文法、轉折、因果與語氣關係一致"];
        foreach (var question in All)
        {
            if (question.Prompt.Trim().Length == 0) issues.Add($"{question.Id} has an empty prompt.");
            if (question.Measure == GREMeasure.Verbal && boilerplate.Any(b => question.Explanation.Contains(b, StringComparison.Ordinal)))
                issues.Add($"{question.Id} has only a boilerplate explanation.");
            if (question.Measure == GREMeasure.Verbal && string.IsNullOrEmpty(GuessingTips.GetValueOrDefault(question.Id)))
                issues.Add($"{question.Id} has no guessing tip.");
            if (question.Format == QuestionFormat.Essay)
            {
                continue;
            }
            else if (question.Format == QuestionFormat.NumericEntry)
            {
                if (question.AcceptedNumericAnswers.Count == 0) issues.Add($"{question.Id} has no numeric answer.");
            }
            else
            {
                var optionIDs = question.Groups.SelectMany(g => g.Options.Select(o => o.Id)).ToHashSet();
                var correctIDs = question.CorrectSelections.Values.SelectMany(s => s).ToHashSet();
                if (!correctIDs.IsSubsetOf(optionIDs)) issues.Add($"{question.Id} has an invalid choice key.");
                if (correctIDs.Count == 0) issues.Add($"{question.Id} has no correct choice.");
            }
            if (question.Figure is { } figure)
            {
                if (figure.Title.Trim().Length == 0) issues.Add($"{question.Id} has an untitled figure.");
                if (figure.Series.Select(s => s.Name).Distinct().Count() != figure.Series.Count)
                    issues.Add($"{question.Id} has duplicate figure-series names.");
                switch (figure.Kind)
                {
                    case QuantFigureKind.Table:
                        if (figure.Headers is not { Count: > 0 } || figure.Rows is not { Count: > 0 })
                            issues.Add($"{question.Id} has an empty table.");
                        break;
                    case QuantFigureKind.Venn:
                        if (figure.Annotations is not { Count: > 0 }) issues.Add($"{question.Id} has an empty Venn diagram.");
                        break;
                    case QuantFigureKind.BoxPlot:
                        var incomplete = figure.Series.SelectMany(s => s.Points).Any(p =>
                            p.Low is null || p.Q1 is null || p.Median is null || p.Q3 is null || p.High is null);
                        if (figure.Series.Count == 0 || incomplete) issues.Add($"{question.Id} has an incomplete box plot.");
                        break;
                    default:
                        if (figure.Series.Count == 0 || figure.Series.Any(s => s.Points.Count == 0))
                            issues.Add($"{question.Id} has an empty chart series.");
                        break;
                }
            }
        }

        var signatures = All.Select(q =>
        {
            var parts = new[] { q.Figure?.Title, q.Stimulus, q.Prompt, q.QuantityA, q.QuantityB }.Where(p => p is not null);
            return SwiftText.StripWhitespaceAndPunctuation(string.Join("|", parts).ToLowerInvariant());
        }).ToList();
        if (signatures.Distinct(StringComparer.Ordinal).Count() != signatures.Count)
            issues.Add("Question content must be unique after normalization.");

        foreach (var measure in new[] { GREMeasure.Verbal, GREMeasure.Quantitative })
        {
            var count = All.Count(q => q.Measure == measure);
            if (count < 27) issues.Add($"{measure.Title()} needs at least 27 questions; found {count}.");
            foreach (var difficulty in Enum.GetValues<QuestionDifficulty>())
            {
                var levelCount = All.Count(q => q.Measure == measure && q.Difficulty == difficulty);
                if (levelCount < 15) issues.Add($"{measure.Title()} {difficulty.Title()} pool needs 15 questions; found {levelCount}.");
            }
        }
        // The 1:1 balance applies to the core bank; the 2026 format additions supply Reading
        // Comprehension sets (including Select-in-Passage) that the core bank lacked.
        var formatAdditionIDs = Additions2026.Select(q => q.Id).ToHashSet();
        var core = All.Where(q => !formatAdditionIDs.Contains(q.Id)).ToList();
        var verbalCount = core.Count(q => q.Measure == GREMeasure.Verbal);
        var quantitativeCount = core.Count(q => q.Measure == GREMeasure.Quantitative);
        if (quantitativeCount < verbalCount)
            issues.Add($"Quantitative pool must match or exceed Verbal; found {quantitativeCount} versus {verbalCount}.");
        return issues;
    }

    private static int Distance(QuestionDifficulty lhs, QuestionDifficulty rhs) => Math.Abs((int)lhs - (int)rhs);

    // MARK: - Exposure

    public IReadOnlySet<string> RecentQuestionIDs(GREMeasure measure) =>
        (_store.GetStringArray(ExposureKey(measure)) ?? []).ToHashSet();

    public void RememberExposure(IReadOnlyList<GREQuestion> questions, GREMeasure measure)
    {
        if (questions.Count == 0) return;
        lock (_exposureGate)
        {
            var key = ExposureKey(measure);
            var selectedIDs = questions.Select(q => q.Id).ToList();
            var selectedSet = selectedIDs.ToHashSet();
            var history = (_store.GetStringArray(key) ?? []).Where(id => !selectedSet.Contains(id)).ToList();
            history.AddRange(selectedIDs);
            var poolCount = All.Count(q => q.Measure == measure);
            var limit = Math.Min(800, Math.Max(120, poolCount / 2));
            _store.SetStringArray(key, history.Skip(Math.Max(0, history.Count - limit)));

            if (measure != GREMeasure.Quantitative) return;
            var familyKey = FamilyExposureKey(measure);
            var selectedFamilies = questions.Select(SelectionFamily).ToList();
            var selectedFamilySet = selectedFamilies.ToHashSet();
            var familyHistory = (_store.GetStringArray(familyKey) ?? []).Where(f => !selectedFamilySet.Contains(f)).ToList();
            familyHistory.AddRange(selectedFamilies);
            _store.SetStringArray(familyKey, familyHistory.Skip(Math.Max(0, familyHistory.Count - 48)));
        }
    }

    public static string ExposureKey(GREMeasure measure) => $"gre-simulation.question-exposure.v1.{measure.RawValue()}";

    public IReadOnlySet<string> RecentQuestionFamilies(GREMeasure measure) =>
        measure != GREMeasure.Quantitative
            ? new HashSet<string>()
            : (_store.GetStringArray(FamilyExposureKey(measure)) ?? []).ToHashSet();

    public static string FamilyExposureKey(GREMeasure measure) => $"gre-simulation.question-family-exposure.v1.{measure.RawValue()}";

    /// <summary>Balanced-quant ids "bq-&lt;scenario&gt;-&lt;n&gt;-&lt;variant&gt;" share a family across n.</summary>
    public static string SelectionFamily(GREQuestion question)
    {
        var components = question.Id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return components.Length == 4 && components[0] == "bq"
            ? string.Join("-", components[0], components[1], components[3])
            : question.Id;
    }

    public static string ScenarioFamily(GREQuestion question)
    {
        var components = question.Id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return components.Length == 4 && components[0] == "bq"
            ? string.Join("-", components[0], components[1])
            : question.Id;
    }

    /// <summary>Round-robin over template families, unseen questions first, recently seen families last.</summary>
    public List<GREQuestion> DiversifiedExposureOrder(IReadOnlyList<GREQuestion> candidates, IReadOnlySet<string> recentIDs, IReadOnlySet<string> recentFamilies)
    {
        var buckets = candidates.GroupBy(SelectionFamily).ToDictionary(
            g => g.Key,
            g => Shuffled(g.Where(q => !recentIDs.Contains(q.Id)))
                .Concat(Shuffled(g.Where(q => recentIDs.Contains(q.Id)))).ToList());
        var families = Shuffled(buckets.Keys).OrderBy(f => recentFamilies.Contains(f) ? 1 : 0).ToList();
        var largest = buckets.Values.Select(b => b.Count).DefaultIfEmpty(0).Max();
        var result = new List<GREQuestion>(candidates.Count);
        for (var offset = 0; offset < largest; offset++)
        {
            foreach (var family in families)
            {
                var bucket = buckets[family];
                if (offset < bucket.Count) result.Add(bucket[offset]);
            }
        }
        return result;
    }

    internal List<T> Shuffled<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
