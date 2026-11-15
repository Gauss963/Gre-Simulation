namespace GreSimulation.Core;

/// <summary>
/// Port of FormBuilder.swift: builds sections that follow the current GRE General Test.
/// </summary>
/// <remarks>
/// ETS: Analytical Writing is always first; the four Verbal and Quantitative sections follow in any
/// order (each measure's first section before its second). Verbal: about half Reading Comprehension,
/// with passages kept together; Text Completion first, then reading, Sentence Equivalence, and more
/// reading (official practice test ordering). Quant: Quantitative Comparison first (about a third),
/// then discrete questions and one Data Interpretation set. Section 1 is of average difficulty; the
/// second section's difficulty depends on section 1 performance.
/// </remarks>
public sealed class FormBuilder
{
    public enum Level { Average, Easy, Medium, Hard }

    private readonly QuestionBank _bank;
    private readonly List<GREQuestion> _verbalPool;
    private readonly List<GREQuestion> _quantPool;
    private readonly List<List<GREQuestion>> _readingSets;
    private readonly List<List<GREQuestion>> _dataSets;

    public FormBuilder(QuestionBank bank)
    {
        _bank = bank;
        _verbalPool = bank.All.Where(q => q.Measure == GREMeasure.Verbal && IsOfficialShape(q)).ToList();
        _quantPool = bank.All.Where(q => q.Measure == GREMeasure.Quantitative && IsOfficialShape(q)).ToList();
        // Reading sets: questions sharing a passage, in authored order.
        _readingSets = _verbalPool.Where(IsReading).GroupBy(q => q.SetKey).Select(g => g.ToList()).ToList();
        // Data Interpretation sets: questions sharing one data display (multiple choice or numeric entry).
        _dataSets = _quantPool
            .Where(q => q.Figure is not null && q.Format is QuestionFormat.SingleChoice or QuestionFormat.MultipleChoice or QuestionFormat.NumericEntry)
            .GroupBy(q => $"{q.Stimulus ?? ""}|{q.Figure?.Title ?? ""}")
            .Where(g => g.Count() >= 2)
            .Select(g => g.ToList())
            .ToList();
    }

    public QuestionBank Bank => _bank;

    // MARK: - Pools

    private static bool IsOfficialShape(GREQuestion question)
    {
        var options = question.Groups.Count > 0 ? question.Groups[0].Options.Count : 0;
        return (question.Measure, question.Format) switch
        {
            (GREMeasure.Verbal, QuestionFormat.SingleChoice) => options == 5,
            (GREMeasure.Verbal, QuestionFormat.MultipleChoice) => options == 3,
            (GREMeasure.Verbal, QuestionFormat.SentenceEquivalence) => options == 6,
            (GREMeasure.Quantitative, QuestionFormat.SingleChoice) => options == 5,
            (GREMeasure.Quantitative, QuestionFormat.MultipleChoice) => options >= 3,
            _ => true,
        };
    }

    private static bool IsReading(GREQuestion q) =>
        q.Stimulus is not null && q.Format is QuestionFormat.SingleChoice or QuestionFormat.MultipleChoice or QuestionFormat.SelectInPassage;

    // MARK: - Difficulty

    private static QuestionDifficulty[] Weight(Level level) => level switch
    {
        Level.Average => [QuestionDifficulty.Medium, QuestionDifficulty.Easy, QuestionDifficulty.Hard],
        Level.Easy => [QuestionDifficulty.Easy, QuestionDifficulty.Medium, QuestionDifficulty.Hard],
        Level.Medium => [QuestionDifficulty.Medium, QuestionDifficulty.Easy, QuestionDifficulty.Hard],
        _ => [QuestionDifficulty.Hard, QuestionDifficulty.Medium, QuestionDifficulty.Easy],
    };

    /// <summary>Target difficulty for each slot: section 1 mixes levels around average; section 2 leans to its route.</summary>
    internal static QuestionDifficulty Target(int slot, Level level)
    {
        QuestionDifficulty[] pattern = level switch
        {
            Level.Average => [QuestionDifficulty.Easy, QuestionDifficulty.Medium, QuestionDifficulty.Medium, QuestionDifficulty.Hard],
            Level.Easy => [QuestionDifficulty.Easy, QuestionDifficulty.Easy, QuestionDifficulty.Medium],
            Level.Medium => [QuestionDifficulty.Medium, QuestionDifficulty.Medium, QuestionDifficulty.Easy, QuestionDifficulty.Hard],
            _ => [QuestionDifficulty.Hard, QuestionDifficulty.Hard, QuestionDifficulty.Medium],
        };
        return pattern[slot % pattern.Length];
    }

    // MARK: - Sections

    public IReadOnlyList<GREQuestion> VerbalSection(int count, Level level, IReadOnlySet<string> excluding, bool remember = true)
    {
        var used = new HashSet<string>(excluding);
        var picker = new Picker(this, level, GREMeasure.Verbal);
        int[] tcBlanks = count == 12 ? [1, 2, 2, 3] : [1, 1, 2, 2, 3];
        var seCount = count == 12 ? 2 : 3;
        const int firstReading = 2;
        var laterReading = count - tcBlanks.Length - seCount - firstReading;

        var questions = new List<GREQuestion>();
        for (var slot = 0; slot < tcBlanks.Length; slot++)
        {
            var blanks = tcBlanks[slot];
            if (picker.PickDiscrete(_verbalPool.Where(q => q.Format == QuestionFormat.TextCompletion && q.Groups.Count == blanks), slot, used) is { } q)
                questions.Add(q);
        }
        questions.AddRange(picker.PickReading(firstReading, _readingSets, longAllowed: false, used));
        for (var slot = 0; slot < seCount; slot++)
        {
            if (picker.PickDiscrete(_verbalPool.Where(q => q.Format == QuestionFormat.SentenceEquivalence), slot + 4, used) is { } q)
                questions.Add(q);
        }
        questions.AddRange(picker.PickReading(laterReading, _readingSets, longAllowed: count == 15, used));
        if (remember) _bank.RememberExposure(questions, GREMeasure.Verbal);
        return questions;
    }

    public IReadOnlyList<GREQuestion> QuantSection(int count, Level level, IReadOnlySet<string> excluding, bool remember = true)
    {
        var used = new HashSet<string>(excluding);
        var picker = new Picker(this, level, GREMeasure.Quantitative);
        var qcCount = count == 12 ? 4 : 5;
        var dataCount = count == 12 ? 2 : 3;
        QuestionFormat[] discrete = count == 12
            ? [QuestionFormat.SingleChoice, QuestionFormat.SingleChoice, QuestionFormat.MultipleChoice, QuestionFormat.NumericEntry, QuestionFormat.SingleChoice, QuestionFormat.NumericEntry]
            : [QuestionFormat.SingleChoice, QuestionFormat.SingleChoice, QuestionFormat.MultipleChoice, QuestionFormat.NumericEntry, QuestionFormat.SingleChoice, QuestionFormat.NumericEntry, QuestionFormat.SingleChoice];

        var questions = new List<GREQuestion>();
        for (var slot = 0; slot < qcCount; slot++)
        {
            if (picker.PickDiscrete(_quantPool.Where(q => q.Format == QuestionFormat.QuantitativeComparison), slot, used) is { } q)
                questions.Add(q);
        }
        var rest = new List<GREQuestion>();
        for (var slot = 0; slot < discrete.Length; slot++)
        {
            var format = discrete[slot];
            var pool = _quantPool.Where(q => q.Format == format
                && !(q.Figure is not null && format != QuestionFormat.QuantitativeComparison && q.Stimulus is not null));
            if (picker.PickDiscrete(pool, slot + qcCount, used) is { } q) rest.Add(q);
        }
        var data = picker.PickDataSet(dataCount, _dataSets, used);
        // The Data Interpretation set sits among the later discrete questions, as on official forms.
        var insertAt = Math.Min(rest.Count, Math.Max(1, rest.Count - 2));
        rest.InsertRange(insertAt, data);
        questions.AddRange(rest);
        if (remember) _bank.RememberExposure(questions, GREMeasure.Quantitative);
        return questions;
    }

    private static readonly (GREMeasure Measure, int Ordinal)[][] Orders =
    [
        [(GREMeasure.Verbal, 1), (GREMeasure.Quantitative, 1), (GREMeasure.Verbal, 2), (GREMeasure.Quantitative, 2)],
        [(GREMeasure.Quantitative, 1), (GREMeasure.Verbal, 1), (GREMeasure.Quantitative, 2), (GREMeasure.Verbal, 2)],
        [(GREMeasure.Verbal, 1), (GREMeasure.Verbal, 2), (GREMeasure.Quantitative, 1), (GREMeasure.Quantitative, 2)],
        [(GREMeasure.Quantitative, 1), (GREMeasure.Quantitative, 2), (GREMeasure.Verbal, 1), (GREMeasure.Verbal, 2)],
        [(GREMeasure.Verbal, 1), (GREMeasure.Quantitative, 1), (GREMeasure.Quantitative, 2), (GREMeasure.Verbal, 2)],
        [(GREMeasure.Quantitative, 1), (GREMeasure.Verbal, 1), (GREMeasure.Verbal, 2), (GREMeasure.Quantitative, 2)],
    ];

    /// <summary>The four measure sections in a random valid order (Analytical Writing is always first).</summary>
    public IReadOnlyList<(GREMeasure Measure, int Ordinal)> SectionOrder() => Orders[_bank.Random.Next(Orders.Length)];

    // MARK: - Picking

    private sealed class Picker
    {
        private readonly FormBuilder _owner;
        private readonly Level _level;
        private readonly IReadOnlySet<string> _recent;

        public Picker(FormBuilder owner, Level level, GREMeasure measure)
        {
            _owner = owner;
            _level = level;
            _recent = owner._bank.RecentQuestionIDs(measure);
        }

        private List<GREQuestion> Rank(List<GREQuestion> pool, QuestionDifficulty target)
        {
            var order = new[] { target }.Concat(Weight(_level).Where(d => d != target));
            var bank = _owner._bank;
            return order.SelectMany(level =>
            {
                var tier = pool.Where(q => q.Difficulty == level).ToList();
                return bank.Shuffled(tier.Where(q => !_recent.Contains(q.Id)))
                    .Concat(bank.Shuffled(tier.Where(q => _recent.Contains(q.Id))));
            }).ToList();
        }

        public GREQuestion? PickDiscrete(IEnumerable<GREQuestion> pool, int slot, HashSet<string> used)
        {
            var target = Target(slot, _level);
            var choice = Rank(pool.Where(q => !used.Contains(q.Id)).ToList(), target).FirstOrDefault();
            if (choice is null) return null;
            used.Add(choice.Id);
            return choice;
        }

        public List<GREQuestion> PickReading(int total, List<List<GREQuestion>> sets, bool longAllowed, HashSet<string> used)
        {
            var picked = new List<GREQuestion>();
            var usedLong = false;
            while (picked.Count < total)
            {
                var remaining = total - picked.Count;
                var target = Target(picked.Count, _level);
                var wantLong = longAllowed && !usedLong && remaining >= 3;
                var candidates = sets.Where(set => !set.Any(q => used.Contains(q.Id)) && (wantLong ? IsLong(set) : !IsLong(set)));
                var ranked = _owner._bank.Shuffled(candidates).OrderBy(set => Distance(set[0].Difficulty, target)).ToList();
                var set = ranked.FirstOrDefault() ?? sets.FirstOrDefault(s => !s.Any(q => used.Contains(q.Id)));
                if (set is null) break;
                var take = Math.Min(remaining, IsLong(set) ? 4 : Math.Min(3, set.Count));
                if (IsLong(set)) usedLong = true;
                picked.AddRange(set.Take(take));
                foreach (var question in set) used.Add(question.Id);
            }
            return picked;
        }

        public List<GREQuestion> PickDataSet(int size, List<List<GREQuestion>> sets, HashSet<string> used)
        {
            var target = _level == Level.Hard ? QuestionDifficulty.Hard : (_level == Level.Easy ? QuestionDifficulty.Easy : QuestionDifficulty.Medium);
            var candidates = sets.Where(s => s.Count >= size && !s.Any(q => used.Contains(q.Id)));
            var set = _owner._bank.Shuffled(candidates)
                .OrderByDescending(s => s.Count(q => q.Difficulty == target))
                .FirstOrDefault();
            if (set is null) return [];
            var chosen = _owner._bank.Shuffled(set).Take(size).ToList();
            foreach (var question in set) used.Add(question.Id);
            return chosen;
        }

        /// <summary>Swift: stimulus.split(separator: " ").count &gt; 300 (empty pieces omitted).</summary>
        private static bool IsLong(List<GREQuestion> set) =>
            (set.FirstOrDefault()?.Stimulus?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0) > 300;

        private static int Distance(QuestionDifficulty a, QuestionDifficulty b) => Math.Abs((int)a - (int)b);
    }

    // MARK: - Validation

    /// <summary>Port of <c>FormBuilder.validate()</c>: builds 320 sections without remembering them.</summary>
    public IReadOnlyList<string> Validate()
    {
        var issues = new HashSet<string>();
        var none = new HashSet<string>();
        for (var run = 0; run < 20; run++)
        {
            foreach (var level in new[] { Level.Average, Level.Easy, Level.Medium, Level.Hard })
            {
                foreach (var count in new[] { 12, 15 })
                {
                    var verbal = VerbalSection(count, level, none, remember: false);
                    var quant = QuantSection(count, level, none, remember: false);
                    if (verbal.Count != count) issues.Add($"Verbal {count}-question section built {verbal.Count} questions.");
                    if (quant.Count != count) issues.Add($"Quant {count}-question section built {quant.Count} questions.");
                    var reading = verbal.Count(q => q.Stimulus is not null);
                    if (Math.Abs(reading * 2 - count) > 2) issues.Add($"Verbal section has {reading} reading questions of {count}.");
                    if (verbal.Select(q => q.Id).Distinct().Count() != verbal.Count || quant.Select(q => q.Id).Distinct().Count() != quant.Count)
                        issues.Add("A section repeats a question.");
                }
            }
        }
        return issues.OrderBy(i => i, StringComparer.Ordinal).ToList();
    }
}
