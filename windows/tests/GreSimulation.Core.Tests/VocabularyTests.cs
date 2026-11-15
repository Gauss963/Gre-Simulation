namespace GreSimulation.Core.Tests;

public class VocabularyTests
{
    private static GreServices Services => TestContent.Services;

    [Fact]
    public void ValidateReportsNoIssues() => Assert.Empty(Services.Vocabulary.Validate(Services.Pronunciations));

    [Fact]
    public void CuratedEntriesOverrideBundledOnes()
    {
        var words = Services.Vocabulary.Words;
        Assert.Equal(words.Count, words.Select(w => w.Id).Distinct().Count());
        Assert.Equal(45, Services.Vocabulary.CuratedWords.Count);
        var abate = words.Single(w => w.Word == "abate");
        Assert.Equal("The wind finally abated near dawn.", abate.Example);
        Assert.Equal("v.", abate.PartOfSpeech);
        Assert.Contains("Original app notes", abate.Sources);
        Assert.Contains("subside", abate.Synonyms);
        Assert.Equal(abate.Sources.Order(StringComparer.Ordinal), abate.Sources);
        Assert.Contains("Original app notes", Services.Vocabulary.SourceOptions);
    }

    [Fact]
    public void WordsAreSortedCaseInsensitively()
    {
        var words = Services.Vocabulary.Words.Select(w => w.Word).ToList();
        Assert.Equal("abandon", words.First(w => w.StartsWith("aban", StringComparison.OrdinalIgnoreCase)));
        for (var i = 1; i < words.Count; i++)
            Assert.True(string.Compare(words[i - 1], words[i], StringComparison.InvariantCultureIgnoreCase) <= 0, $"{words[i - 1]} > {words[i]}");
    }

    [Theory]
    [InlineData("abate", 7207)]
    [InlineData("Ubiquitous", 8826)]
    [InlineData("  laconic ", 7818)]
    [InlineData("aback", 7205)]
    public void PronunciationClipsAreReadFromThePack(string word, int length)
    {
        var clip = Services.Pronunciations.Clip(word);
        Assert.NotNull(clip);
        Assert.Equal(length, clip.Length);
        Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(clip, 4, 4)); // an .m4a (MP4) file
    }

    [Fact]
    public void MissingWordsHaveNoClip()
    {
        Assert.Null(Services.Pronunciations.Clip("notarealgreword"));
        Assert.False(Services.Pronunciations.HasClip(""));
        using var empty = new WordPronunciationLibrary(Path.GetTempPath());
        Assert.Null(empty.Clip("abate"));
        Assert.Equal(0, empty.ClipCount);
    }

    [Fact]
    public void ConcurrentClipReads()
    {
        var words = Services.Vocabulary.Words.Take(200).Select(w => w.Word).ToList();
        Parallel.ForEach(words, w => Assert.NotNull(Services.Pronunciations.Clip(w)));
    }

    [Fact]
    public void BrowserFiltersBySourceAndQuery()
    {
        var browser = Services.CreateVocabularyBrowser();
        Assert.Equal(Services.Vocabulary.Words.Count, browser.FilteredWords.Count);
        Assert.Equal(VocabularyBank.AllSources, browser.SourceChoices[0]);
        browser.SearchText = "  ABATE ";
        Assert.Contains(browser.FilteredWords, w => w.Word == "abate");
        Assert.All(browser.FilteredWords, w => Assert.True(
            w.Word.Contains("abate", StringComparison.OrdinalIgnoreCase) || w.Definition.Contains("abate", StringComparison.OrdinalIgnoreCase)
            || w.Synonyms.Any(s => s.Contains("abate", StringComparison.OrdinalIgnoreCase))));
        browser.SearchText = "減弱";
        Assert.Contains(browser.FilteredWords, w => w.Word == "abate");
        browser.SearchText = "";
        browser.SelectedSource = "Original app notes";
        Assert.Equal(45, browser.FilteredWords.Count);
        Assert.Equal("45 words", browser.CountText);
        var word = browser.FilteredWords[0];
        browser.ToggleRevealed(word);
        Assert.True(browser.IsRevealed(word));
        browser.ToggleRevealed(word);
        Assert.False(browser.IsRevealed(word));
    }
}

public class FlashcardTests
{
    private static IReadOnlyList<VocabularyWord> Words(int count) =>
        Enumerable.Range(0, count).Select(i => new VocabularyWord { Word = $"w{i:000}", Sources = [i % 2 == 0 ? "even" : "odd"] }).ToList();

    [Fact]
    public void FixedDeckRatingAndCompletion()
    {
        var settings = new KeyValueStore(null);
        var session = new FlashcardSession(Words(30), settings, new Random(1));
        Assert.Equal(FlashcardScreen.None, session.Screen);
        session.OnAppear();
        Assert.Equal(FlashcardScreen.Card, session.Screen);
        Assert.Equal(20, session.Deck.Count);
        Assert.Equal("Card 1 of 20", session.DeckProgressTitle);
        var first = session.CurrentCard!;
        session.RateCurrentCard(true); // allowed even unrevealed via API, as the key handler guards reveal
        Assert.Contains(first.Id, session.Progress.Mastered);
        Assert.Equal(["w" + first.Id[1..]], settings.GetStringArray(FlashcardProgressStore.StorageKey));
        for (var i = 1; i < 20; i++) { session.RevealCard(); session.RateCurrentCard(i % 2 == 0); }
        Assert.Equal(FlashcardScreen.DeckCompletion, session.Screen);
        Assert.Equal("You knew 10 cards and marked 10 for another review.", session.DeckCompletionText);
        Assert.True(session.CanReviewMissedCards);
        session.ReviewMissedCards();
        Assert.Equal(10, session.Deck.Count);

        session.StartNewDeck();
        Assert.Equal(20, session.EligibleWords.Count); // unmastered only
        session.UnmasteredOnly = false;
        session.DeckSize = FlashcardDeckSize.All;
        session.StartNewDeck();
        Assert.Equal(30, session.Deck.Count);
        session.SelectedSource = "even";
        Assert.Equal(15, session.EligibleWords.Count);
    }

    [Fact]
    public void InfiniteLoopRequeuesMissedCardsAndPersistsMode()
    {
        var settings = new KeyValueStore(null);
        var session = new FlashcardSession(Words(100), settings, new Random(2));
        session.StudyMode = FlashcardMode.Infinite;
        Assert.Equal("infinite", settings.GetString(FlashcardSession.ModeStorageKey));
        Assert.Equal(100, session.Deck.Count);
        Assert.Equal(100, session.InfiniteTotal);
        var missed = session.CurrentCard!;
        session.RevealCard();
        session.RateCurrentCard(false);
        var position = session.Deck.ToList().IndexOf(missed);
        Assert.InRange(position, 19, 29); // target min(max(99/4, 15), 40) = 24, jitter 19…28
        Assert.Equal(100, session.Deck.Count);

        while (session.CurrentCard is not null) { session.RevealCard(); session.RateCurrentCard(true); }
        Assert.Equal(FlashcardScreen.InfiniteCompletion, session.Screen);
        Assert.Equal("You worked through all 100 cards in this loop until each one was marked Know it.", session.InfiniteCompletionText);

        var reopened = new FlashcardSession(Words(100), settings);
        Assert.Equal(FlashcardMode.Infinite, reopened.StudyMode);
        reopened.OnAppear();
        Assert.Equal(FlashcardScreen.EmptyDeck, reopened.Screen);
        reopened.ResetProgress();
        Assert.Equal(100, reopened.Deck.Count);
        Assert.False(settings.Contains(FlashcardProgressStore.StorageKey));
    }

    [Fact]
    public void RequeueGapStaysWithinBounds()
    {
        var session = new FlashcardSession(Words(1), new KeyValueStore(null), new Random(3));
        for (var i = 0; i < 200; i++)
        {
            Assert.Equal(3, session.RequeueGap(3)); // short queue: to the end
            Assert.InRange(session.RequeueGap(400), 32, 48); // target 40
            Assert.InRange(session.RequeueGap(100), 20, 30); // target 25
        }
    }
}
