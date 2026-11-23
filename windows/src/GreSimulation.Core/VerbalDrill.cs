using CommunityToolkit.Mvvm.ComponentModel;

namespace GreSimulation.Core;

/// <summary>Port of <c>VerbalDrillType</c> (VerbalDrill.swift): the seven Verbal question types the practice page covers.</summary>
public enum VerbalDrillType
{
    TextCompletion1,
    TextCompletion2,
    TextCompletion3,
    SentenceEquivalence,
    ReadingSingle,
    ReadingMultiple,
    ReadingSelectSentence,
}

public static class VerbalDrillText
{
    public static IReadOnlyList<VerbalDrillType> AllTypes { get; } = Enum.GetValues<VerbalDrillType>();

    public static string Title(this VerbalDrillType type) => type switch
    {
        VerbalDrillType.TextCompletion1 => "Text Completion · 1 blank",
        VerbalDrillType.TextCompletion2 => "Text Completion · 2 blanks",
        VerbalDrillType.TextCompletion3 => "Text Completion · 3 blanks",
        VerbalDrillType.SentenceEquivalence => "Sentence Equivalence",
        VerbalDrillType.ReadingSingle => "Reading · select one",
        VerbalDrillType.ReadingMultiple => "Reading · select all that apply",
        _ => "Reading · select a sentence",
    };

    public static string ShortTitle(this VerbalDrillType type) => type switch
    {
        VerbalDrillType.TextCompletion1 => "TC 1 空",
        VerbalDrillType.TextCompletion2 => "TC 2 空",
        VerbalDrillType.TextCompletion3 => "TC 3 空",
        VerbalDrillType.SentenceEquivalence => "SE 同義句",
        VerbalDrillType.ReadingSingle => "閱讀單選",
        VerbalDrillType.ReadingMultiple => "閱讀多選",
        _ => "閱讀選句",
    };

    /// <summary>A one-line strategy reminder, shown after a wrong answer and for the weakest type.</summary>
    public static string Tip(this VerbalDrillType type) => type switch
    {
        VerbalDrillType.TextCompletion1 =>
            "先遮住選項，用 although、because、分號等訊號字決定空格方向，自己想一個簡單字，再找最接近的選項。",
        VerbalDrillType.TextCompletion2 or VerbalDrillType.TextCompletion3 =>
            "先做線索最明確的空格，再用它推其他空格；每空都要單獨成立，最後整句代回去讀一次。",
        VerbalDrillType.SentenceEquivalence =>
            "答案是兩個讓句意相同的字：先預測空格意思，再找成對的近義選項；落單的字、方向相反的同義對都要刪。",
        VerbalDrillType.ReadingSingle =>
            "先判斷題型（主旨、細節、推論、目的、態度），回原文找證據；過度極端、超出範圍、把說法顛倒的選項先刪。",
        VerbalDrillType.ReadingMultiple =>
            "每個選項都要在文中找到證據才選，沒有證據就不選；可能只有一個正確，也可能全部正確，全對才給分。",
        _ =>
            "把題目要的功能（提出反例、轉折、證據）先想清楚，再逐句比對；答案常是 however、but 等轉折句。",
    };

    /// <summary>The Swift raw value ("textCompletion1", …).</summary>
    public static string RawValue(this VerbalDrillType type) => char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];

    public static VerbalDrillType? Of(GREQuestion question)
    {
        if (question.Measure != GREMeasure.Verbal) return null;
        return question.Format switch
        {
            QuestionFormat.TextCompletion => question.Groups.Count switch
            {
                1 => VerbalDrillType.TextCompletion1,
                2 => VerbalDrillType.TextCompletion2,
                _ => VerbalDrillType.TextCompletion3,
            },
            QuestionFormat.SentenceEquivalence => VerbalDrillType.SentenceEquivalence,
            QuestionFormat.SingleChoice => VerbalDrillType.ReadingSingle,
            QuestionFormat.MultipleChoice => VerbalDrillType.ReadingMultiple,
            QuestionFormat.SelectInPassage => VerbalDrillType.ReadingSelectSentence,
            _ => null,
        };
    }
}

/// <summary>The type filter of the practice page (Swift <c>VerbalDrillView.TypeFilter</c>).</summary>
public sealed record VerbalDrillFilter(string Id, string Title, IReadOnlySet<VerbalDrillType>? Types)
{
    public static VerbalDrillFilter All { get; } = new("all", "All question types", null);

    private static VerbalDrillFilter Single(VerbalDrillType type) => new(type.RawValue(), type.Title(), new HashSet<VerbalDrillType> { type });

    /// <summary>The picker entries in order.</summary>
    public static IReadOnlyList<VerbalDrillFilter> Menu { get; } =
    [
        All,
        new("tc", "Text Completion (all)", new HashSet<VerbalDrillType> { VerbalDrillType.TextCompletion1, VerbalDrillType.TextCompletion2, VerbalDrillType.TextCompletion3 }),
        Single(VerbalDrillType.TextCompletion1),
        Single(VerbalDrillType.TextCompletion2),
        Single(VerbalDrillType.TextCompletion3),
        new("se", "Sentence Equivalence", new HashSet<VerbalDrillType> { VerbalDrillType.SentenceEquivalence }),
        new("rc", "Reading Comprehension (all)", new HashSet<VerbalDrillType> { VerbalDrillType.ReadingSingle, VerbalDrillType.ReadingMultiple, VerbalDrillType.ReadingSelectSentence }),
        Single(VerbalDrillType.ReadingSingle),
        Single(VerbalDrillType.ReadingMultiple),
        Single(VerbalDrillType.ReadingSelectSentence),
    ];
}

/// <summary>Port of the <c>VerbalDrill</c> enum: the practice pool and answer helpers.</summary>
public sealed class VerbalDrillPool
{
    public VerbalDrillPool(QuestionBank bank)
    {
        Bank = bank;
        Questions = bank.All.Where(q => VerbalDrillText.Of(q) is not null).ToList();
        foreach (var question in Questions) QuestionsByID.TryAdd(question.Id, question);
        SetMembers = Questions.Where(q => q.SetID is not null || q.Stimulus is not null)
            .GroupBy(q => q.SetKey)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GREQuestion>)g.ToList(), StringComparer.Ordinal);
    }

    public QuestionBank Bank { get; }

    /// <summary>Every deliverable Verbal question (retired items are already excluded from the bank).</summary>
    public IReadOnlyList<GREQuestion> Questions { get; }

    public Dictionary<string, GREQuestion> QuestionsByID { get; } = new(StringComparer.Ordinal);

    /// <summary>Every question of a passage set, in bank order.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<GREQuestion>> SetMembers { get; }

    public static bool IsComplete(GREAnswer answer, GREQuestion question) => question.Groups.All(group =>
    {
        var count = answer.Selections.TryGetValue(group.Id, out var set) ? set.Count : 0;
        return question.Format switch
        {
            QuestionFormat.SentenceEquivalence => count == 2,
            QuestionFormat.MultipleChoice => count >= 1,
            _ => count == 1,
        };
    });

    public static bool IsCorrect(GREAnswer answer, GREQuestion question) => question.CorrectSelections.All(pair =>
        (answer.Selections.TryGetValue(pair.Key, out var chosen) ? chosen : []).SetEquals(pair.Value));

    /// <summary>The chosen options as text, blank by blank.</summary>
    public static string AnswerText(IReadOnlyDictionary<string, HashSet<string>> selections, GREQuestion question)
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
        _ => "Select one answer choice.",
    };
}

/// <summary>Port of <c>VerbalDrillStore</c>: saved practice attempts under <c>gre-simulation.verbal-drill.v1</c>.</summary>
public sealed class VerbalDrillStore
{
    public sealed class Record
    {
        public required Guid Id { get; init; }
        public required string QuestionID { get; init; }
        public required DateTimeOffset Date { get; init; }
        public required Dictionary<string, List<string>> Selections { get; init; }
        public required bool IsCorrect { get; init; }

        public GREAnswer Answer => new() { Selections = Selections.ToDictionary(p => p.Key, p => p.Value.ToHashSet()) };
    }

    public sealed record TypeAccuracy(VerbalDrillType Type, int Correct, int Total)
    {
        public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;
    }

    public const string StorageKey = "gre-simulation.verbal-drill.v1";
    public const int RecordLimit = 5_000;

    private readonly KeyValueStore _store;
    private readonly List<Record> _records;

    public VerbalDrillStore(VerbalDrillPool pool, KeyValueStore store)
    {
        Pool = pool;
        _store = store;
        _records = store.GetData(StorageKey) is { } data
            ? (Try(() => GreJson.Deserialize<List<Record>>(data)) ?? [])
            : [];
    }

    private static T? Try<T>(Func<T?> read) where T : class
    {
        try { return read(); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    public VerbalDrillPool Pool { get; }

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

    public IReadOnlyList<TypeAccuracy> AccuracyByType
    {
        get
        {
            var sums = new Dictionary<VerbalDrillType, (int Correct, int Total)>();
            foreach (var record in LatestByQuestion().Values)
            {
                if (Question(record) is not { } question || VerbalDrillText.Of(question) is not { } type) continue;
                var current = sums.GetValueOrDefault(type);
                sums[type] = (current.Correct + (record.IsCorrect ? 1 : 0), current.Total + 1);
            }
            return VerbalDrillText.AllTypes.Where(sums.ContainsKey).Select(t => new TypeAccuracy(t, sums[t].Correct, sums[t].Total)).ToList();
        }
    }

    /// <summary>The two lowest-accuracy types with at least five answered questions.</summary>
    public IReadOnlyList<VerbalDrillType> WeakTypes =>
        AccuracyByType.Where(a => a.Total >= 5).OrderBy(a => a.Accuracy).Take(2).Select(a => a.Type).ToList();

    public Record? LastResult(GREQuestion question) => LatestByQuestion().GetValueOrDefault(question.Id);

    public bool RecordAttempt(GREQuestion question, GREAnswer answer, DateTimeOffset? now = null)
    {
        var correct = VerbalDrillPool.IsCorrect(answer, question);
        _records.Add(new Record
        {
            Id = Guid.NewGuid(),
            QuestionID = question.Id,
            Date = now ?? DateTimeOffset.Now,
            Selections = answer.Selections.ToDictionary(p => p.Key, p => p.Value.Order(StringComparer.Ordinal).ToList()),
            IsCorrect = correct,
        });
        if (_records.Count > RecordLimit) _records.RemoveRange(0, _records.Count - RecordLimit);
        _store.SetData(StorageKey, GreJson.SerializeToUtf8Bytes(_records));
        // Practice tests then prefer questions you have not just seen here.
        Pool.Bank.RememberExposure([question], GREMeasure.Verbal);
        return correct;
    }

    /// <summary>The questions matching the filters; <paramref name="missedOnly"/> keeps those whose latest attempt was wrong.</summary>
    public IReadOnlyList<GREQuestion> Candidates(IReadOnlySet<VerbalDrillType>? types, QuestionDifficulty? difficulty, bool missedOnly)
    {
        var latest = LatestByQuestion();
        return Pool.Questions.Where(q =>
            VerbalDrillText.Of(q) is { } type
            && (types is null || types.Contains(type))
            && (difficulty is null || q.Difficulty == difficulty)
            && (!missedOnly || (latest.TryGetValue(q.Id, out var r) && !r.IsCorrect))).ToList();
    }

    /// <summary>
    /// The next question: the rest of the current passage first; then unpracticed questions (the weakest types
    /// first when <paramref name="focus"/> is set), starting a passage at its first unpracticed question; then
    /// missed questions, oldest first; then the least recently practiced.
    /// </summary>
    public GREQuestion? Next(GREQuestion? current, IReadOnlySet<VerbalDrillType>? types, QuestionDifficulty? difficulty,
        IReadOnlySet<VerbalDrillType>? focus, bool missedOnly, Random random)
    {
        var pool = Candidates(types, difficulty, missedOnly).Where(q => q.Id != current?.Id).ToList();
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
            var preferred = focus is null ? open : open.Where(q => VerbalDrillText.Of(q) is { } t && focus.Contains(t)).ToList();
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
/// The state behind <c>VerbalDrillView</c> (VerbalDrillView.swift): one question at a time, Check Answer
/// (Ctrl+Enter on Windows, ⌘↩ on the Mac), Try Again, Next Question, the stats header and the history.
/// </summary>
public sealed class VerbalDrillSession : ObservableObject
{
    private readonly Random _random;
    private VerbalDrillFilter _filter = VerbalDrillFilter.All;
    private QuestionDifficulty? _difficulty;
    private bool _focusWeak;
    private bool _missedOnly;
    private GREQuestion? _current;
    private GREAnswer _answer = new();
    private bool? _isCorrect;

    public VerbalDrillSession(VerbalDrillStore store, Random? random = null)
    {
        Store = store;
        _random = random ?? Random.Shared;
    }

    public VerbalDrillStore Store { get; }

    public const string Title = "Verbal practice";
    public const string Subtitle = "Answer one question at a time, then check it to see the correct answer, the explanation, and the guessing strategy. Text Completion, Sentence Equivalence, and every Reading Comprehension format are included; reading questions on the same passage come one after another.";
    public const string ExplanationHeading = "詳解";
    public const string GuessingTipHeading = "猜題法：單字不熟也能縮小範圍";
    public const string AccuracyCaption = "Accuracy by question type (latest attempt of each question)";

    // MARK: - Filters (changing one loads a new question)

    public VerbalDrillFilter Filter
    {
        get => _filter;
        set { if (SetProperty(ref _filter, value)) LoadNext(null); }
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

    public bool CanFocusWeak => Store.WeakTypes.Count > 0;

    // MARK: - State

    public GREQuestion? Current => _current;
    public GREAnswer Answer => _answer;
    /// <summary>null until checked.</summary>
    public bool? IsCorrect => _isCorrect;
    public bool IsChecked => _isCorrect is not null;
    public int SessionCorrect { get; private set; }
    public int SessionTotal { get; private set; }

    public bool CanCheck => _current is { } q && !IsChecked && VerbalDrillPool.IsComplete(_answer, q);

    public string ActionHint => _current is not { } q ? ""
        : VerbalDrillPool.IsComplete(_answer, q) ? "Ready to check · Ctrl+Enter" : VerbalDrillPool.CompletionHint(q);

    public string? LastTimeText => _current is { } q && !IsChecked && Store.LastResult(q) is { } last
        ? $"Last time {(last.IsCorrect ? "correct" : "incorrect")} · {last.Date.ToLocalTime():MMM d, yyyy}"
        : null;

    public string EmptyTitle => _missedOnly ? "No missed questions" : "No matching questions";
    public string EmptyText => _missedOnly
        ? "Every question you missed has since been answered correctly. Turn off Missed only to keep practicing."
        : "Choose a different question type or difficulty.";

    public string PracticedText => $"{Store.PracticedCount} / {Store.Pool.Questions.Count}";
    public string AccuracyText => Store.RecentAccuracy is { } a ? $"{Math.Round(a * 100, MidpointRounding.AwayFromZero):0}%" : "—";
    public string SessionText => SessionTotal == 0 ? "—" : $"{SessionCorrect} / {SessionTotal}";
    public string MissedText => $"{Store.MissedCount}";

    public string? WeakestText => Store.WeakTypes.Count > 0 ? $"Weakest: {Store.WeakTypes[0].ShortTitle()} — {Store.WeakTypes[0].Tip()}" : null;

    public string YourAnswerText => _current is { } q ? $"Your answer: {VerbalDrillPool.AnswerText(_answer.Selections, q)}" : "";
    public string CorrectAnswerText => _current is { } q ? $"Correct answer: {VerbalDrillPool.AnswerText(q.CorrectSelections.ToDictionary(p => p.Key, p => p.Value), q)}" : "";

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
        var focus = _focusWeak && Store.WeakTypes.Count > 0 ? Store.WeakTypes.ToHashSet() : null;
        Load(Store.Next(after, _filter.Types, _difficulty, focus, _missedOnly, _random));
    }
}
