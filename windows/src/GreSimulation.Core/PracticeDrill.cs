using CommunityToolkit.Mvvm.ComponentModel;

namespace GreSimulation.Core;

/// <summary>Port of <c>DrillType</c> (PracticeDrill.swift): seven Verbal and four Quant question types.</summary>
public enum DrillType
{
    TextCompletion1,
    TextCompletion2,
    TextCompletion3,
    SentenceEquivalence,
    ReadingSingle,
    ReadingMultiple,
    ReadingSelectSentence,
    QuantitativeComparison,
    QuantSingle,
    QuantMultiple,
    NumericEntry,
}

public static class DrillText
{
    public static IReadOnlyList<DrillType> AllTypes { get; } = Enum.GetValues<DrillType>();

    public static GREMeasure Measure(this DrillType type) => type >= DrillType.QuantitativeComparison ? GREMeasure.Quantitative : GREMeasure.Verbal;

    public static IReadOnlyList<DrillType> Types(GREMeasure measure) => AllTypes.Where(t => t.Measure() == measure).ToList();

    public static string Title(this DrillType type) => type switch
    {
        DrillType.TextCompletion1 => "Text Completion · 1 blank",
        DrillType.TextCompletion2 => "Text Completion · 2 blanks",
        DrillType.TextCompletion3 => "Text Completion · 3 blanks",
        DrillType.SentenceEquivalence => "Sentence Equivalence",
        DrillType.ReadingSingle => "Reading · select one",
        DrillType.ReadingMultiple => "Reading · select all that apply",
        DrillType.ReadingSelectSentence => "Reading · select a sentence",
        DrillType.QuantitativeComparison => "Quantitative Comparison",
        DrillType.QuantSingle => "Multiple choice · select one",
        DrillType.QuantMultiple => "Multiple choice · select one or more",
        _ => "Numeric Entry",
    };

    public static string ShortTitle(this DrillType type) => type switch
    {
        DrillType.TextCompletion1 => "TC 1 空",
        DrillType.TextCompletion2 => "TC 2 空",
        DrillType.TextCompletion3 => "TC 3 空",
        DrillType.SentenceEquivalence => "SE 同義句",
        DrillType.ReadingSingle => "閱讀單選",
        DrillType.ReadingMultiple => "閱讀多選",
        DrillType.ReadingSelectSentence => "閱讀選句",
        DrillType.QuantitativeComparison => "QC 比較",
        DrillType.QuantSingle => "單選",
        DrillType.QuantMultiple => "多選",
        _ => "填數字",
    };

    /// <summary>A one-line strategy reminder, shown after a wrong answer.</summary>
    public static string Tip(this DrillType type) => type switch
    {
        DrillType.TextCompletion1 =>
            "先遮住選項，用 although、because、分號等訊號字決定空格方向，自己想一個簡單字，再找最接近的選項。",
        DrillType.TextCompletion2 or DrillType.TextCompletion3 =>
            "先做線索最明確的空格，再用它推其他空格；每空都要單獨成立，最後整句代回去讀一次。",
        DrillType.SentenceEquivalence =>
            "答案是兩個讓句意相同的字：先預測空格意思，再找成對的近義選項；落單的字、方向相反的同義對都要刪。",
        DrillType.ReadingSingle =>
            "先判斷題型（主旨、細節、推論、目的、態度），回原文找證據；過度極端、超出範圍、把說法顛倒的選項先刪。",
        DrillType.ReadingMultiple =>
            "每個選項都要在文中找到證據才選，沒有證據就不選；可能只有一個正確，也可能全部正確，全對才給分。",
        DrillType.ReadingSelectSentence =>
            "把題目要的功能（提出反例、轉折、證據）先想清楚，再逐句比對；答案常是 however、but 等轉折句。",
        DrillType.QuantitativeComparison =>
            "先化簡兩邊再比較；代入 0、1、負數、分數、很大的數測試，只要出現兩種不同結果就選 D（無法判斷）。",
        DrillType.QuantSingle =>
            "先估算刪掉不可能的選項；卡住時把選項代回題目，或代入簡單的數字驗算。",
        DrillType.QuantMultiple =>
            "每個選項都要逐一檢查，正確的可能不只一個；先找出範圍的上下限，再判斷哪些選項落在範圍內。",
        _ =>
            "沒有選項可以猜，算完要檢查單位、四捨五入的位數與題目問的量；分數答案不必約分。",
    };

    /// <summary>The Swift raw value ("textCompletion1", …).</summary>
    public static string RawValue(this DrillType type) => char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];

    public static DrillType? Of(GREQuestion question) => (question.Measure, question.Format) switch
    {
        (GREMeasure.Verbal, QuestionFormat.TextCompletion) => question.Groups.Count switch
        {
            1 => DrillType.TextCompletion1,
            2 => DrillType.TextCompletion2,
            _ => DrillType.TextCompletion3,
        },
        (GREMeasure.Verbal, QuestionFormat.SentenceEquivalence) => DrillType.SentenceEquivalence,
        (GREMeasure.Verbal, QuestionFormat.SingleChoice) => DrillType.ReadingSingle,
        (GREMeasure.Verbal, QuestionFormat.MultipleChoice) => DrillType.ReadingMultiple,
        (GREMeasure.Verbal, QuestionFormat.SelectInPassage) => DrillType.ReadingSelectSentence,
        (GREMeasure.Quantitative, QuestionFormat.QuantitativeComparison) => DrillType.QuantitativeComparison,
        (GREMeasure.Quantitative, QuestionFormat.SingleChoice) => DrillType.QuantSingle,
        (GREMeasure.Quantitative, QuestionFormat.MultipleChoice) => DrillType.QuantMultiple,
        (GREMeasure.Quantitative, QuestionFormat.NumericEntry) => DrillType.NumericEntry,
        _ => null,
    };
}

/// <summary>Port of <c>DrillCategory</c>: what accuracy is tracked by (question type for Verbal, content area for Quant).</summary>
public sealed record DrillCategory(string Id, string Title, string Tip)
{
    public static IReadOnlyList<DrillCategory> QuantAreas { get; } =
    [
        new("Arithmetic", "Arithmetic 算術",
            "整數性質、因數倍數、百分比與比例最常考：先確認是不是整數、會不會是負數或 0，百分比要看清楚是「誰的」百分之幾。"),
        new("Algebra", "Algebra 代數",
            "列式後先化簡再解；不等式乘除負數要變號，選項可以直接代回驗證。"),
        new("Geometry", "Geometry 幾何",
            "圖形不一定按比例，只相信標示的數字與條件；把已知角度、邊長標在圖上，找出直角三角形或相似三角形。"),
        new("Data Analysis", "Data Analysis 資料分析",
            "先讀清楚圖表標題、單位與總數；平均數、中位數、機率與排列組合各有固定公式，計算前先估算大概答案。"),
    ];

    /// <summary>The four official Quant content areas; narrower tags in the imported items are folded in.</summary>
    public static string QuantArea(GREQuestion question) => question.ContentArea switch
    {
        "Algebra" => "Algebra",
        "Geometry" => "Geometry",
        "Data Analysis" or "Probability" or "Combinatorics" => "Data Analysis",
        _ => "Arithmetic",
    };

    public static DrillCategory? Of(GREQuestion question)
    {
        if (question.Measure == GREMeasure.Quantitative)
        {
            var area = QuantArea(question);
            return QuantAreas.First(a => a.Id == area);
        }
        return DrillText.Of(question) is { } type ? ForType(type) : null;
    }

    public static IReadOnlyList<DrillCategory> All(GREMeasure measure) =>
        measure == GREMeasure.Quantitative ? QuantAreas : DrillText.Types(measure).Select(ForType).ToList();

    private static DrillCategory ForType(DrillType type) => new(type.RawValue(), type.ShortTitle(), type.Tip());
}

/// <summary>The type filter of a practice page (Swift <c>PracticeDrillView.TypeFilter</c>).</summary>
public sealed record DrillTypeFilter(string Id, string Title, IReadOnlySet<DrillType>? Types)
{
    private static readonly DrillTypeFilter AllTypes = new("all", "All question types", null);

    private static DrillTypeFilter Single(DrillType type) => new(type.RawValue(), type.Title(), new HashSet<DrillType> { type });

    private static readonly IReadOnlyList<DrillTypeFilter> VerbalMenu =
    [
        AllTypes,
        new("tc", "Text Completion (all)", new HashSet<DrillType> { DrillType.TextCompletion1, DrillType.TextCompletion2, DrillType.TextCompletion3 }),
        Single(DrillType.TextCompletion1),
        Single(DrillType.TextCompletion2),
        Single(DrillType.TextCompletion3),
        new("se", "Sentence Equivalence", new HashSet<DrillType> { DrillType.SentenceEquivalence }),
        new("rc", "Reading Comprehension (all)", new HashSet<DrillType> { DrillType.ReadingSingle, DrillType.ReadingMultiple, DrillType.ReadingSelectSentence }),
        Single(DrillType.ReadingSingle),
        Single(DrillType.ReadingMultiple),
        Single(DrillType.ReadingSelectSentence),
    ];

    private static readonly IReadOnlyList<DrillTypeFilter> QuantMenu =
    [
        AllTypes,
        Single(DrillType.QuantitativeComparison),
        Single(DrillType.QuantSingle),
        Single(DrillType.QuantMultiple),
        Single(DrillType.NumericEntry),
    ];

    /// <summary>The picker entries in order.</summary>
    public static IReadOnlyList<DrillTypeFilter> Menu(GREMeasure measure) => measure == GREMeasure.Quantitative ? QuantMenu : VerbalMenu;
}

/// <summary>Port of the <c>PracticeDrill</c> enum: the practice pools and answer helpers.</summary>
public sealed class PracticeDrillPool
{
    public PracticeDrillPool(QuestionBank bank)
    {
        Bank = bank;
        Verbal = bank.All.Where(q => DrillText.Of(q)?.Measure() == GREMeasure.Verbal).ToList();
        Quantitative = bank.All.Where(q => DrillText.Of(q)?.Measure() == GREMeasure.Quantitative).ToList();
        foreach (var question in Verbal.Concat(Quantitative)) QuestionsByID.TryAdd(question.Id, question);
        SetMembers = Verbal.Concat(Quantitative).Where(q => q.SetID is not null || q.Stimulus is not null)
            .GroupBy(q => q.SetKey)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GREQuestion>)g.ToList(), StringComparer.Ordinal);
    }

    public QuestionBank Bank { get; }
    public IReadOnlyList<GREQuestion> Verbal { get; }
    public IReadOnlyList<GREQuestion> Quantitative { get; }

    public IReadOnlyList<GREQuestion> Questions(GREMeasure measure) => measure == GREMeasure.Quantitative ? Quantitative : Verbal;

    public Dictionary<string, GREQuestion> QuestionsByID { get; } = new(StringComparer.Ordinal);

    /// <summary>Every question of a passage or data set, in bank order.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<GREQuestion>> SetMembers { get; }

    public static bool IsComplete(GREAnswer answer, GREQuestion question)
    {
        if (question.Format == QuestionFormat.NumericEntry)
        {
            var parts = answer.NumericEntry.Split('/');
            return answer.NumericEntry.Length > 0 && !parts.Any(p => p.Trim().Length == 0 || p == "-" || p == ".");
        }
        return question.Groups.All(group =>
        {
            var count = answer.Selections.TryGetValue(group.Id, out var set) ? set.Count : 0;
            return question.Format switch
            {
                QuestionFormat.SentenceEquivalence => count == 2,
                QuestionFormat.MultipleChoice => count >= 1,
                _ => count == 1,
            };
        });
    }

    public static bool IsCorrect(GREAnswer answer, GREQuestion question)
    {
        if (question.Format == QuestionFormat.NumericEntry)
            return question.AcceptedNumericAnswers.Any(expected => NumericAnswerMatcher.Matches(answer.NumericEntry, expected));
        return question.CorrectSelections.All(pair =>
            (answer.Selections.TryGetValue(pair.Key, out var chosen) ? chosen : []).SetEquals(pair.Value));
    }

    /// <summary>Your answer as text, blank by blank.</summary>
    public static string AnswerText(GREAnswer answer, GREQuestion question) =>
        question.Format == QuestionFormat.NumericEntry
            ? (answer.NumericEntry.Length == 0 ? "No answer" : answer.NumericEntry)
            : SelectionText(answer.Selections, question);

    public static string CorrectAnswerText(GREQuestion question) =>
        question.Format == QuestionFormat.NumericEntry
            ? string.Join(" or ", question.AcceptedNumericAnswers.Order(StringComparer.Ordinal))
            : SelectionText(question.CorrectSelections.ToDictionary(p => p.Key, p => p.Value), question);

    private static string SelectionText(IReadOnlyDictionary<string, HashSet<string>> selections, GREQuestion question)
    {
        var parts = question.Groups.Select(group =>
        {
            var chosen = group.Options.Where(o => selections.TryGetValue(group.Id, out var set) && set.Contains(o.Id)).Select(o => o.Text).ToList();
            if (chosen.Count == 0) return null;
            return question.Format == QuestionFormat.SelectInPassage
                ? string.Concat(chosen.Select(text => $"“{text}”"))
                : string.Join(", ", chosen);
        }).OfType<string>().ToList();
        return parts.Count == 0 ? "No answer" : string.Join(" · ", parts);
    }

    /// <summary>The hint next to the disabled Check Answer button.</summary>
    public static string CompletionHint(GREQuestion question) => question.Format switch
    {
        QuestionFormat.SentenceEquivalence => "Select exactly two answer choices.",
        QuestionFormat.TextCompletion when question.Groups.Count > 1 => "Select one entry for each blank.",
        QuestionFormat.MultipleChoice => "Select every answer choice that applies.",
        QuestionFormat.SelectInPassage => "Click the sentence in the passage.",
        QuestionFormat.NumericEntry => IsFractionEntry(question) ? "Enter a numerator and a denominator." : "Enter your answer.",
        _ => "Select one answer choice.",
    };

    /// <summary>Fraction answers use two boxes (numerator over denominator).</summary>
    public static bool IsFractionEntry(GREQuestion question) =>
        question.AcceptedNumericAnswers.Count > 0 && question.AcceptedNumericAnswers.All(a => a.Contains('/'));
}

/// <summary>Port of <c>PracticeDrillStore</c>: saved attempts for one measure.</summary>
public sealed class PracticeDrillStore
{
    public sealed class Record
    {
        public required Guid Id { get; init; }
        public required string QuestionID { get; init; }
        public required DateTimeOffset Date { get; init; }
        public required Dictionary<string, List<string>> Selections { get; init; }
        public string? NumericEntry { get; init; }
        public required bool IsCorrect { get; init; }

        public GREAnswer Answer => new()
        {
            Selections = Selections.ToDictionary(p => p.Key, p => p.Value.ToHashSet()),
            NumericEntry = NumericEntry ?? "",
        };
    }

    public sealed record CategoryAccuracy(DrillCategory Category, int Correct, int Total)
    {
        public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;
    }

    public static string StorageKey(GREMeasure measure) =>
        measure == GREMeasure.Quantitative ? "gre-simulation.quant-drill.v1" : "gre-simulation.verbal-drill.v1";

    public const int RecordLimit = 5_000;

    private readonly KeyValueStore _store;
    private readonly List<Record> _records;

    public PracticeDrillStore(PracticeDrillPool pool, GREMeasure measure, KeyValueStore store)
    {
        Pool = pool;
        Measure = measure;
        _store = store;
        _records = store.GetData(StorageKey(measure)) is { } data
            ? (Try(() => GreJson.Deserialize<List<Record>>(data)) ?? [])
            : [];
    }

    private static T? Try<T>(Func<T?> read) where T : class
    {
        try { return read(); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    public PracticeDrillPool Pool { get; }
    public GREMeasure Measure { get; }
    public IReadOnlyList<GREQuestion> Questions => Pool.Questions(Measure);

    public GREQuestion? Question(Record record) => Pool.QuestionsByID.GetValueOrDefault(record.QuestionID);

    /// <summary>Newest first; attempts on questions no longer in the bank are left out.</summary>
    public IReadOnlyList<Record> History => Enumerable.Reverse(_records).Where(r => Question(r) is not null).ToList();

    public Dictionary<string, Record> LatestByQuestion()
    {
        var latest = new Dictionary<string, Record>(StringComparer.Ordinal);
        foreach (var record in _records.Where(r => Question(r) is not null)) latest[record.QuestionID] = record;
        return latest;
    }

    public int PracticedCount => LatestByQuestion().Count;
    public int MissedCount => LatestByQuestion().Values.Count(r => !r.IsCorrect);

    /// <summary>Accuracy over each practiced question's most recent attempt.</summary>
    public double? RecentAccuracy
    {
        get
        {
            var latest = LatestByQuestion().Values;
            return latest.Count == 0 ? null : (double)latest.Count(r => r.IsCorrect) / latest.Count;
        }
    }

    public IReadOnlyList<CategoryAccuracy> AccuracyByCategory
    {
        get
        {
            var sums = new Dictionary<string, (int Correct, int Total)>(StringComparer.Ordinal);
            foreach (var record in LatestByQuestion().Values)
            {
                if (Question(record) is not { } question || DrillCategory.Of(question) is not { } category) continue;
                var current = sums.GetValueOrDefault(category.Id);
                sums[category.Id] = (current.Correct + (record.IsCorrect ? 1 : 0), current.Total + 1);
            }
            return DrillCategory.All(Measure).Where(c => sums.ContainsKey(c.Id))
                .Select(c => new CategoryAccuracy(c, sums[c.Id].Correct, sums[c.Id].Total)).ToList();
        }
    }

    /// <summary>The two lowest-accuracy categories with at least five answered questions.</summary>
    public IReadOnlyList<DrillCategory> WeakCategories =>
        AccuracyByCategory.Where(a => a.Total >= 5).OrderBy(a => a.Accuracy).Take(2).Select(a => a.Category).ToList();

    public Record? LastResult(GREQuestion question) => LatestByQuestion().GetValueOrDefault(question.Id);

    public bool RecordAttempt(GREQuestion question, GREAnswer answer, DateTimeOffset? now = null)
    {
        var correct = PracticeDrillPool.IsCorrect(answer, question);
        _records.Add(new Record
        {
            Id = Guid.NewGuid(),
            QuestionID = question.Id,
            Date = now ?? DateTimeOffset.Now,
            Selections = answer.Selections.ToDictionary(p => p.Key, p => p.Value.Order(StringComparer.Ordinal).ToList()),
            NumericEntry = question.Format == QuestionFormat.NumericEntry ? answer.NumericEntry : null,
            IsCorrect = correct,
        });
        if (_records.Count > RecordLimit) _records.RemoveRange(0, _records.Count - RecordLimit);
        _store.SetData(StorageKey(Measure), GreJson.SerializeToUtf8Bytes(_records));
        // Practice tests then prefer questions you have not just seen here.
        Pool.Bank.RememberExposure([question], Measure);
        return correct;
    }

    /// <summary>The questions matching the filters; <paramref name="missedOnly"/> keeps those whose latest attempt was wrong.</summary>
    public IReadOnlyList<GREQuestion> Candidates(IReadOnlySet<DrillType>? types, string? area, QuestionDifficulty? difficulty, bool missedOnly)
    {
        var latest = LatestByQuestion();
        return Questions.Where(q =>
            DrillText.Of(q) is { } type
            && (types is null || types.Contains(type))
            && (area is null || DrillCategory.QuantArea(q) == area)
            && (difficulty is null || q.Difficulty == difficulty)
            && (!missedOnly || (latest.TryGetValue(q.Id, out var r) && !r.IsCorrect))).ToList();
    }

    /// <summary>
    /// The next question: the rest of the current passage or data set first; then unpracticed questions (the weakest
    /// categories first when <paramref name="focus"/> is set), starting a set at its first unpracticed question; then
    /// missed questions, oldest first; then the least recently practiced.
    /// </summary>
    public GREQuestion? Next(GREQuestion? current, IReadOnlySet<DrillType>? types, string? area, QuestionDifficulty? difficulty,
        IReadOnlySet<string>? focus, bool missedOnly, Random random)
    {
        var pool = Candidates(types, area, difficulty, missedOnly).Where(q => q.Id != current?.Id).ToList();
        if (pool.Count == 0) return null;
        var latest = LatestByQuestion();
        var poolIDs = pool.Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
        bool IsOpen(GREQuestion q) => missedOnly || !latest.ContainsKey(q.Id);

        if (current is not null && Pool.SetMembers.TryGetValue(current.SetKey, out var members))
        {
            var index = members.ToList().FindIndex(q => q.Id == current.Id);
            if (index >= 0 && members.Skip(index + 1).FirstOrDefault(q => poolIDs.Contains(q.Id) && IsOpen(q)) is { } following)
                return following;
        }

        var open = pool.Where(IsOpen).ToList();
        if (open.Count > 0)
        {
            var preferred = focus is null ? open : open.Where(q => DrillCategory.Of(q) is { } c && focus.Contains(c.Id)).ToList();
            if (preferred.Count == 0) preferred = open;
            var pick = preferred[random.Next(preferred.Count)];
            if (Pool.SetMembers.TryGetValue(pick.SetKey, out var set) && set.FirstOrDefault(q => poolIDs.Contains(q.Id) && IsOpen(q)) is { } first)
                return first;
            return pick;
        }
        return pool.OrderBy(q => latest.TryGetValue(q.Id, out var r) && r.IsCorrect ? 1 : 0)
            .ThenBy(q => latest.TryGetValue(q.Id, out var r) ? r.Date : DateTimeOffset.MinValue)
            .First();
    }
}

/// <summary>
/// The state behind <c>PracticeDrillView</c> (PracticeDrillView.swift) for one measure: one question at a time, Check
/// Answer (Ctrl+Enter on Windows, ⌘↩ on the Mac), Try Again, Next Question, the stats header, the history, and for
/// Quant the on-screen calculator.
/// </summary>
public sealed class PracticeDrillSession : ObservableObject, ICalculatorHost
{
    private readonly Random _random;
    private DrillTypeFilter _filter;
    private string? _area;
    private QuestionDifficulty? _difficulty;
    private bool _focusWeak;
    private bool _missedOnly;
    private bool _showsCalculator;
    private GREQuestion? _current;
    private GREAnswer _answer = new();
    private bool? _isCorrect;

    public PracticeDrillSession(PracticeDrillStore store, Random? random = null)
    {
        Store = store;
        _random = random ?? Random.Shared;
        _filter = DrillTypeFilter.Menu(store.Measure)[0];
    }

    public PracticeDrillStore Store { get; }
    public GREMeasure Measure => Store.Measure;
    public bool IsQuant => Measure == GREMeasure.Quantitative;

    public string Title => IsQuant ? "Quant practice" : "Verbal practice";
    public string Subtitle => IsQuant
        ? "Answer one question at a time, then check it to see the correct answer and the explanation. Quantitative Comparison, multiple choice (one or more answers), and Numeric Entry are all included, with the on-screen calculator as on the real test."
        : "Answer one question at a time, then check it to see the correct answer, the explanation, and the guessing strategy. Text Completion, Sentence Equivalence, and every Reading Comprehension format are included; reading questions on the same passage come one after another.";
    public const string ExplanationHeading = "詳解";
    public const string GuessingTipHeading = "猜題法：單字不熟也能縮小範圍";
    public string AccuracyCaption => $"Accuracy by {(IsQuant ? "content area" : "question type")} (latest attempt of each question)";
    public string FocusTitle => IsQuant ? "Focus on weak areas" : "Focus on weak types";
    public string FocusHelp => CanFocusWeak
        ? $"Unpracticed questions from your two weakest {(IsQuant ? "content areas" : "question types")} come first."
        : $"Available after five answers in a {(IsQuant ? "content area" : "question type")}.";

    /// <summary>The Area picker's entries (Quant only): null is All areas.</summary>
    public static IReadOnlyList<string?> AreaChoices { get; } = [null, .. DrillCategory.QuantAreas.Select(a => a.Id)];

    // MARK: - Filters (changing one loads a new question)

    public DrillTypeFilter Filter
    {
        get => _filter;
        set { if (SetProperty(ref _filter, value)) LoadNext(null); }
    }

    public string? Area
    {
        get => _area;
        set { if (SetProperty(ref _area, value)) LoadNext(null); }
    }

    public QuestionDifficulty? Difficulty
    {
        get => _difficulty;
        set { if (SetProperty(ref _difficulty, value)) LoadNext(null); }
    }

    public bool FocusWeak
    {
        get => _focusWeak;
        set { if (SetProperty(ref _focusWeak, value)) LoadNext(null); }
    }

    public bool MissedOnly
    {
        get => _missedOnly;
        set { if (SetProperty(ref _missedOnly, value)) LoadNext(null); }
    }

    public bool CanFocusWeak => Store.WeakCategories.Count > 0;

    // MARK: - Calculator (Quant)

    public CalculatorModel Calculator { get; } = new();

    public bool ShowsCalculator
    {
        get => _showsCalculator;
        set => SetProperty(ref _showsCalculator, value && IsQuant);
    }

    public bool CanTransferFromCalculator =>
        _current is { Format: QuestionFormat.NumericEntry } q && !PracticeDrillPool.IsFractionEntry(q) && !IsChecked;

    public void TransferCalculatorDisplay()
    {
        var value = Calculator.TransferText;
        if (CanTransferFromCalculator && value.Length > 0) SetNumericEntryText(value);
    }

    public void CloseCalculator() => ShowsCalculator = false;

    // MARK: - State

    public GREQuestion? Current => _current;
    public GREAnswer Answer => _answer;
    /// <summary>null until checked.</summary>
    public bool? IsCorrect => _isCorrect;
    public bool IsChecked => _isCorrect is not null;
    public int SessionCorrect { get; private set; }
    public int SessionTotal { get; private set; }

    public bool CanCheck => _current is { } q && !IsChecked && PracticeDrillPool.IsComplete(_answer, q);

    public string ActionHint => _current is not { } q ? ""
        : PracticeDrillPool.IsComplete(_answer, q) ? "Ready to check · Ctrl+Enter" : PracticeDrillPool.CompletionHint(q);

    public string? LastTimeText => _current is { } q && !IsChecked && Store.LastResult(q) is { } last
        ? $"Last time {(last.IsCorrect ? "correct" : "incorrect")} · {last.Date.ToLocalTime():MMM d, yyyy}"
        : null;

    public string EmptyTitle => _missedOnly ? "No missed questions" : "No matching questions";
    public string EmptyText => _missedOnly
        ? "Every question you missed has since been answered correctly. Turn off Missed only to keep practicing."
        : "Choose a different question type, content area, or difficulty.";

    public string PracticedText => $"{Store.PracticedCount} / {Store.Questions.Count}";
    public string AccuracyText => Store.RecentAccuracy is { } a ? $"{Math.Round(a * 100, MidpointRounding.AwayFromZero):0}%" : "—";
    public string SessionText => SessionTotal == 0 ? "—" : $"{SessionCorrect} / {SessionTotal}";
    public string MissedText => $"{Store.MissedCount}";

    public string? WeakestText => Store.WeakCategories.Count > 0 ? $"Weakest: {Store.WeakCategories[0].Title} — {Store.WeakCategories[0].Tip}" : null;

    /// <summary>The history row's detail line.</summary>
    public string HistoryDetail(PracticeDrillStore.Record record)
    {
        if (Store.Question(record) is not { } question) return "";
        var parts = new List<string> { DrillText.Of(question)?.Title() ?? "" };
        if (IsQuant) parts.Add(DrillCategory.QuantArea(question));
        parts.Add(question.Difficulty.Title());
        parts.Add(record.Date.ToLocalTime().ToString("MMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture));
        return string.Join(" · ", parts);
    }

    // MARK: - Actions

    /// <summary>Call when the page appears.</summary>
    public void OnAppear()
    {
        if (_current is null) LoadNext(null);
    }

    /// <summary>Clicking a choice or a sentence (ignored once checked).</summary>
    public void Toggle(ChoiceGroup group, ChoiceOption option)
    {
        if (IsChecked || _current is null) return;
        if (!_answer.Selections.TryGetValue(group.Id, out var selections)) _answer.Selections[group.Id] = selections = [];
        if (selections.Contains(option.Id)) selections.Remove(option.Id);
        else if (group.MaximumSelections == 1) { selections.Clear(); selections.Add(option.Id); }
        else if (selections.Count < group.MaximumSelections) selections.Add(option.Id);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Typing in the single Numeric Entry box (digits, a leading minus, one decimal point).</summary>
    public void SetNumericEntryText(string typed)
    {
        if (IsChecked) return;
        var cleaned = ExamSession.CleanNumber(typed, allowDecimal: true);
        if (cleaned == _answer.NumericEntry) return;
        _answer.NumericEntry = cleaned;
        OnPropertyChanged(string.Empty);
    }

    public string FractionPart(int index)
    {
        var parts = _answer.NumericEntry.Split('/');
        return index < parts.Length ? parts[index] : "";
    }

    /// <summary>Typing in the numerator (0) or denominator (1) box.</summary>
    public void SetFractionPart(int index, string typed)
    {
        if (IsChecked) return;
        var parts = _answer.NumericEntry.Split('/').ToList();
        while (parts.Count < 2) parts.Add("");
        parts[index] = ExamSession.CleanNumber(typed, allowDecimal: false);
        var value = parts.All(p => p.Length == 0) ? "" : string.Join("/", parts);
        if (value == _answer.NumericEntry) return;
        _answer.NumericEntry = value;
        OnPropertyChanged(string.Empty);
    }

    public void Check()
    {
        if (!CanCheck || _current is not { } question) return;
        var correct = Store.RecordAttempt(question, _answer);
        _isCorrect = correct;
        SessionTotal += 1;
        if (correct) SessionCorrect += 1;
        OnPropertyChanged(string.Empty);
    }

    public void TryAgain() => Load(_current);

    /// <summary>Next Question / Skip.</summary>
    public void Next() => LoadNext(_current);

    public void Load(GREQuestion? question)
    {
        _current = question;
        _answer = new GREAnswer();
        _isCorrect = null;
        OnPropertyChanged(string.Empty);
    }

    private void LoadNext(GREQuestion? after)
    {
        var focus = _focusWeak && Store.WeakCategories.Count > 0 ? Store.WeakCategories.Select(c => c.Id).ToHashSet() : null;
        Load(Store.Next(after, _filter.Types, _area, _difficulty, focus, _missedOnly, _random));
    }
}
