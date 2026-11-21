namespace GreSimulation.Core.Tests;

public class AnswerChoiceWordsTests
{
    private static GreServices Services => TestContent.Services;

    [Fact]
    public void BundledGroupsValidateAndResolveToBankEntries()
    {
        var choices = Services.AnswerChoices;
        Assert.Empty(choices.Validate());
        Assert.NotEmpty(choices.Set.Groups);
        Assert.Equal(choices.Set.Groups.Sum(g => g.Poles.Sum(p => p.Words.Count)), choices.Words.Count);
        Assert.All(choices.Words, w => Assert.True(choices.Count(w) >= 0));
        Assert.Contains(choices.Words, w => choices.Count(w) >= 10);
        Assert.Contains(choices.Set.Groups, g => g.Poles.Count == 2);
    }

    private static readonly IReadOnlyList<VocabularyWord> Bank =
    [
        new() { Word = "laconic", PartOfSpeech = "adj.", Chinese = "言簡意賅的；簡潔的" },
        new() { Word = "terse", PartOfSpeech = "adj.", Chinese = "簡短的；簡潔的" },
        new() { Word = "verbose", PartOfSpeech = "adj.", Chinese = "冗長的；囉嗦的" },
        new() { Word = "mitigate", PartOfSpeech = "v.", Chinese = "減輕；緩和" },
        new() { Word = "exacerbate", PartOfSpeech = "v.", Chinese = "使惡化；加劇" },
        new() { Word = "acumen", PartOfSpeech = "n.", Chinese = "敏銳；精明" },
        new() { Word = "Abstract", PartOfSpeech = "adj./n.", Chinese = "adj. 抽象的；n. 摘要" },
        new() { Word = "unrelated", PartOfSpeech = "adj.", Chinese = "無關的" },
    ];

    private static AnswerChoiceWords Sample() => new(new AnswerChoiceWordSet
    {
        Groups =
        [
            new() { Id = "brevity", Title = "簡潔 ↔ 冗長", Poles = [new() { Label = "簡潔", Words = ["laconic", "terse"] }, new() { Label = "冗長", Words = ["verbose"] }] },
            new() { Id = "worse", Title = "減輕 ↔ 加劇", Poles = [new() { Label = "減輕", Words = ["mitigate"] }, new() { Label = "加劇", Words = ["exacerbate"] }] },
            new() { Id = "insight", Title = "敏銳", Poles = [new() { Label = "敏銳", Words = ["acumen", "abstract"] }] },
        ],
        Counts = new() { ["laconic"] = 21, ["terse"] = 4, ["verbose"] = 6, ["mitigate"] = 13, ["exacerbate"] = 2, ["acumen"] = 13, ["abstract"] = 1 },
    }, Bank);

    [Fact]
    public void LookupsBadgesAndShortGlosses()
    {
        var choices = Sample();
        Assert.Empty(choices.Validate());
        Assert.Equal(["laconic", "terse", "verbose", "mitigate", "exacerbate", "acumen", "Abstract"], choices.Words.Select(w => w.Word));
        var verbose = Bank[2];
        Assert.Equal(("brevity", 1), (choices.Group(verbose)!.Value.Group.Id, choices.Group(verbose)!.Value.Pole));
        Assert.Null(choices.Group(Bank[7]));
        Assert.Equal("In 21 answer choices", choices.CountText(Bank[0]));
        Assert.Equal("In 1 answer choice", choices.CountText(Bank[6]));
        Assert.Null(choices.CountText(Bank[7]));
        Assert.Equal("言簡意賅的", AnswerChoiceWords.ShortGloss(Bank[0]));
        Assert.Equal("抽象的", AnswerChoiceWords.ShortGloss(Bank[6]));
    }

    [Fact]
    public void OrdersKeepGroupsTogetherOrSortByFrequency()
    {
        var choices = Sample();
        for (var seed = 0; seed < 20; seed++)
        {
            var ordered = choices.Ordered(choices.Words, AnswerChoiceOrder.Groups, new Random(seed)).Select(w => w.Word).ToList();
            var brevity = ordered.IndexOf("laconic");
            Assert.Equal(["laconic", "terse", "verbose"], ordered.Skip(brevity).Take(3));
            var worse = ordered.IndexOf("mitigate");
            Assert.Equal("exacerbate", ordered[worse + 1]);
        }
        Assert.Equal(["laconic", "acumen", "mitigate", "verbose", "terse", "exacerbate", "Abstract"],
            choices.Ordered(choices.Words, AnswerChoiceOrder.Frequency, new Random(1)).Select(w => w.Word));
        Assert.Equal(7, choices.Ordered(choices.Words, AnswerChoiceOrder.Shuffled, new Random(1)).Distinct().Count());
    }

    [Fact]
    public void ValidationCatchesBrokenGroups()
    {
        var broken = new AnswerChoiceWords(new AnswerChoiceWordSet
        {
            Groups =
            [
                new() { Id = "a", Poles = [new() { Words = ["laconic", "missing"] }] },
                new() { Id = "a", Poles = [new() { Words = ["laconic"] }, new() { Words = [] }] },
            ],
            Counts = new() { ["laconic"] = 1 },
        }, Bank);
        Assert.Equal(
        [
            "Answer-choice group IDs must be unique.",
            "An answer-choice word is in more than one group.",
            "1 answer-choice words are not in the vocabulary bank, e.g. [\"missing\"].",
            "1 answer-choice words have no count, e.g. [\"missing\"].",
            "Answer-choice group a needs one or two non-empty poles.",
        ], broken.Validate());
    }

    [Fact]
    public void FlashcardSessionDrawsFromTheAnswerChoiceDeck()
    {
        var settings = new KeyValueStore(null);
        var session = new FlashcardSession(Bank, settings, new Random(4), Sample());
        Assert.Equal(FlashcardWordSet.AllVocabulary, session.WordSet);
        session.OnAppear();
        Assert.Equal(8, session.EligibleWords.Count);
        Assert.False(session.ShowsAnswerChoiceOptions);

        session.WordSet = FlashcardWordSet.AnswerChoices;
        Assert.Equal("answerChoices", settings.GetString(FlashcardSession.WordSetStorageKey));
        Assert.True(session.ShowsAnswerChoiceOptions);
        Assert.Equal(7, session.Deck.Count);
        Assert.Equal("New deck", session.RestartButtonTitle);
        Assert.StartsWith("7 words that appear most often", session.AnswerChoiceSummary);
        Assert.Contains("3 meaning groups", session.AnswerChoiceSummary);

        session.PartOfSpeech = AnswerChoicePartOfSpeech.Adjectives;
        Assert.Equal(["laconic", "terse", "verbose", "Abstract"], session.EligibleWords.Select(w => w.Word));
        session.PartOfSpeech = AnswerChoicePartOfSpeech.Nouns;
        Assert.Equal(["acumen", "Abstract"], session.EligibleWords.Select(w => w.Word));
        session.PartOfSpeech = AnswerChoicePartOfSpeech.All;

        session.AnswerChoiceOrder = AnswerChoiceOrder.Frequency;
        Assert.Equal("frequency", settings.GetString(FlashcardSession.AnswerChoiceOrderStorageKey));
        Assert.Equal("laconic", session.CurrentCard!.Word);
        session.AnswerChoiceOrder = AnswerChoiceOrder.Shuffled;
        Assert.Equal("New shuffled deck", session.RestartButtonTitle);

        session.RevealCard();
        session.RateCurrentCard(true);
        Assert.Equal("1 mastered", session.MasteredText);

        var reopened = new FlashcardSession(Bank, settings, new Random(5), Sample());
        Assert.Equal(FlashcardWordSet.AnswerChoices, reopened.WordSet);
        Assert.Equal(AnswerChoiceOrder.Shuffled, reopened.AnswerChoiceOrder);
    }
}
