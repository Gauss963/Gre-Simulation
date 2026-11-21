using System.Globalization;
using System.Text.RegularExpressions;

namespace GreSimulation.Core;

/// <summary>
/// Port of <c>AnswerChoiceGroup</c> (AnswerChoiceWords.swift): a meaning group of words that often appear
/// as Text Completion and Sentence Equivalence answer choices — one or two poles of near-synonyms, and when
/// there are two, the poles are opposites.
/// </summary>
public sealed class AnswerChoiceGroup
{
    public sealed class Pole
    {
        public string Label { get; init; } = "";
        public IReadOnlyList<string> Words { get; init; } = [];
    }

    public required string Id { get; init; }
    public string Title { get; init; } = "";
    public IReadOnlyList<Pole> Poles { get; init; } = [];
}

/// <summary><c>Resources/AnswerChoiceWords.json</c>: the groups and each word's count in TC/SE options.</summary>
public sealed class AnswerChoiceWordSet
{
    public IReadOnlyList<AnswerChoiceGroup> Groups { get; init; } = [];
    public Dictionary<string, int> Counts { get; init; } = [];
}

/// <summary>Parts of speech the Answer-choice words deck can be narrowed to.</summary>
public enum AnswerChoicePartOfSpeech
{
    All,
    Adjectives,
    Verbs,
    Nouns,
}

/// <summary>How an Answer-choice words deck is ordered.</summary>
public enum AnswerChoiceOrder
{
    Groups,
    Frequency,
    Shuffled,
}

public static class AnswerChoiceText
{
    public static IReadOnlyList<AnswerChoicePartOfSpeech> AllPartsOfSpeech { get; } =
        [AnswerChoicePartOfSpeech.All, AnswerChoicePartOfSpeech.Adjectives, AnswerChoicePartOfSpeech.Verbs, AnswerChoicePartOfSpeech.Nouns];

    public static IReadOnlyList<AnswerChoiceOrder> AllOrders { get; } =
        [AnswerChoiceOrder.Groups, AnswerChoiceOrder.Frequency, AnswerChoiceOrder.Shuffled];

    public static string Title(this AnswerChoicePartOfSpeech kind) => kind switch
    {
        AnswerChoicePartOfSpeech.Adjectives => "Adjectives",
        AnswerChoicePartOfSpeech.Verbs => "Verbs",
        AnswerChoicePartOfSpeech.Nouns => "Nouns",
        _ => "All parts of speech",
    };

    public static string Title(this AnswerChoiceOrder order) => order switch
    {
        AnswerChoiceOrder.Frequency => "Most frequent first",
        AnswerChoiceOrder.Shuffled => "Shuffled",
        _ => "Meaning groups together",
    };

    /// <summary>The @AppStorage raw value ("groups" / "frequency" / "shuffled").</summary>
    public static string RawValue(this AnswerChoiceOrder order) => order switch
    {
        AnswerChoiceOrder.Frequency => "frequency",
        AnswerChoiceOrder.Shuffled => "shuffled",
        _ => "groups",
    };

    public static AnswerChoiceOrder ParseOrder(string? raw) => raw switch
    {
        "frequency" => AnswerChoiceOrder.Frequency,
        "shuffled" => AnswerChoiceOrder.Shuffled,
        _ => AnswerChoiceOrder.Groups,
    };

    /// <summary><c>adj./n.</c> matches both Adjectives and Nouns.</summary>
    public static bool Matches(this AnswerChoicePartOfSpeech kind, VocabularyWord word)
    {
        var tag = kind switch
        {
            AnswerChoicePartOfSpeech.Adjectives => "adj",
            AnswerChoicePartOfSpeech.Verbs => "v",
            AnswerChoicePartOfSpeech.Nouns => "n",
            _ => null,
        };
        if (tag is null) return true;
        return (word.PartOfSpeech ?? "").Split('/').Select(part => part.Trim('.', ' ')).Contains(tag);
    }
}

/// <summary>Port of the <c>AnswerChoiceWords</c> enum: the grouped words resolved against the vocabulary bank.</summary>
public sealed class AnswerChoiceWords
{
    private readonly Dictionary<string, VocabularyWord> _bankByKey;
    private readonly Dictionary<string, (AnswerChoiceGroup Group, int Pole)> _placement = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _position = new(StringComparer.Ordinal);

    public AnswerChoiceWords(string resourceDirectory, VocabularyBank bank)
        : this(BundledResource.Decode<AnswerChoiceWordSet>(resourceDirectory, "AnswerChoiceWords") ?? new AnswerChoiceWordSet(), bank)
    {
    }

    public AnswerChoiceWords(AnswerChoiceWordSet set, VocabularyBank bank) : this(set, bank.Words) { }

    public AnswerChoiceWords(AnswerChoiceWordSet set, IEnumerable<VocabularyWord> bankWords)
    {
        Set = set;
        _bankByKey = new(StringComparer.Ordinal);
        foreach (var word in bankWords) _bankByKey.TryAdd(word.Word.ToLowerInvariant(), word);
        foreach (var group in set.Groups)
            for (var index = 0; index < group.Poles.Count; index++)
                foreach (var word in group.Poles[index].Words)
                    _placement.TryAdd(word.ToLowerInvariant(), (group, index));
        Words = set.Groups.SelectMany(g => g.Poles.SelectMany(p => p.Words))
            .Select(w => _bankByKey.GetValueOrDefault(w.ToLowerInvariant()))
            .OfType<VocabularyWord>().ToList();
        for (var i = 0; i < Words.Count; i++) _position.TryAdd(Words[i].Id, i);
    }

    public AnswerChoiceWordSet Set { get; }

    /// <summary>Every grouped word, in group order (pole by pole), as its vocabulary-bank entry.</summary>
    public IReadOnlyList<VocabularyWord> Words { get; }

    public VocabularyWord? Entry(string word) => _bankByKey.GetValueOrDefault(word.ToLowerInvariant());

    public int Count(VocabularyWord word) => Set.Counts.GetValueOrDefault(word.Word.ToLowerInvariant());

    public (AnswerChoiceGroup Group, int Pole)? Group(VocabularyWord word) =>
        _placement.TryGetValue(word.Word.ToLowerInvariant(), out var placement) ? placement : null;

    /// <summary>The front-of-card badge ("In 21 answer choices"); null when the word is not counted.</summary>
    public string? CountText(VocabularyWord word)
    {
        var count = Count(word);
        return count > 0 ? $"In {count} answer {(count == 1 ? "choice" : "choices")}" : null;
    }

    /// <summary>The first sense of a gloss, for the short list of group members on the card back.</summary>
    public static string ShortGloss(VocabularyWord word)
    {
        var gloss = Regex.Replace(word.Chinese, @"^(adj|adv|v|n)\.\s*", "");
        return gloss.Split('；')[0].Trim();
    }

    /// <summary>
    /// Orders an eligible deck: whole groups in shuffled order (each group pole by pole, so the opposites
    /// come back to back), most frequent first, or fully shuffled.
    /// </summary>
    public List<VocabularyWord> Ordered(IEnumerable<VocabularyWord> eligible, AnswerChoiceOrder order, Random random)
    {
        var list = eligible.ToList();
        switch (order)
        {
            case AnswerChoiceOrder.Shuffled:
                return Shuffle(list, random);
            case AnswerChoiceOrder.Frequency:
                var comparer = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
                return list.OrderByDescending(Count).ThenBy(w => w.Word, comparer).ToList();
            default:
                var byGroup = list.GroupBy(w => Group(w)?.Group.Id ?? "").ToList();
                return Shuffle(byGroup, random)
                    .SelectMany(g => g.OrderBy(w => _position.GetValueOrDefault(w.Id)))
                    .ToList();
        }
    }

    private static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    /// <summary>Port of <c>AnswerChoiceWords.validate()</c>.</summary>
    public IReadOnlyList<string> Validate()
    {
        var issues = new List<string>();
        if (Set.Groups.Count == 0) issues.Add("Answer-choice word groups did not load.");
        if (Set.Groups.Select(g => g.Id).Distinct().Count() != Set.Groups.Count) issues.Add("Answer-choice group IDs must be unique.");
        var listed = Set.Groups.SelectMany(g => g.Poles.SelectMany(p => p.Words)).Select(w => w.ToLowerInvariant()).ToList();
        if (listed.Distinct().Count() != listed.Count) issues.Add("An answer-choice word is in more than one group.");
        var missing = listed.Where(w => !_bankByKey.ContainsKey(w)).ToList();
        if (missing.Count > 0) issues.Add($"{missing.Count} answer-choice words are not in the vocabulary bank, e.g. {SwiftArray(missing.Take(3))}.");
        var uncounted = listed.Where(w => !Set.Counts.ContainsKey(w)).ToList();
        if (uncounted.Count > 0) issues.Add($"{uncounted.Count} answer-choice words have no count, e.g. {SwiftArray(uncounted.Take(3))}.");
        foreach (var group in Set.Groups.Where(g => g.Poles.Count is < 1 or > 2 || g.Poles.Any(p => p.Words.Count == 0)))
            issues.Add($"Answer-choice group {group.Id} needs one or two non-empty poles.");
        return issues;
    }

    private static string SwiftArray(IEnumerable<string> items) => "[" + string.Join(", ", items.Select(i => $"\"{i}\"")) + "]";
}
