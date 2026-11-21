using CommunityToolkit.Mvvm.ComponentModel;

namespace GreSimulation.Core;

/// <summary>Port of <c>FlashcardProgressStore</c>: mastered word ids, saved sorted under <c>gre-simulation.flashcards.mastered.v1</c>.</summary>
public sealed class FlashcardProgressStore : ObservableObject
{
    public const string StorageKey = "gre-simulation.flashcards.mastered.v1";

    private readonly KeyValueStore _store;
    private HashSet<string> _mastered;

    public FlashcardProgressStore(KeyValueStore store)
    {
        _store = store;
        _mastered = (store.GetStringArray(StorageKey) ?? []).ToHashSet();
    }

    public IReadOnlySet<string> Mastered => _mastered;

    public void SetMastered(string wordID, bool isMastered)
    {
        if (isMastered) _mastered.Add(wordID); else _mastered.Remove(wordID);
        _store.SetStringArray(StorageKey, _mastered.Order(StringComparer.Ordinal));
        OnPropertyChanged(nameof(Mastered));
    }

    public void Reset()
    {
        _mastered = [];
        _store.Remove(StorageKey);
        OnPropertyChanged(nameof(Mastered));
    }
}

/// <summary>Deck sizes (raw value 0 = All).</summary>
public enum FlashcardDeckSize
{
    Twenty = 20,
    Fifty = 50,
    OneHundred = 100,
    All = 0,
}

/// <summary>Which words a flash-card deck draws from.</summary>
public enum FlashcardWordSet
{
    AllVocabulary,
    AnswerChoices,
}

public enum FlashcardMode
{
    Deck,
    Infinite,
}

public static class FlashcardText
{
    public static string Title(this FlashcardDeckSize size) => size == FlashcardDeckSize.All ? "All" : $"{(int)size} cards";
    public static string Title(this FlashcardMode mode) => mode == FlashcardMode.Deck ? "Fixed deck" : "Infinite loop";
    /// <summary>The @AppStorage raw value ("deck" / "infinite").</summary>
    public static string RawValue(this FlashcardMode mode) => mode == FlashcardMode.Deck ? "deck" : "infinite";
    public static string Title(this FlashcardWordSet set) => set == FlashcardWordSet.AllVocabulary ? "All vocabulary" : "Answer-choice words";
    /// <summary>The @AppStorage raw value ("allVocabulary" / "answerChoices").</summary>
    public static string RawValue(this FlashcardWordSet set) => set == FlashcardWordSet.AllVocabulary ? "allVocabulary" : "answerChoices";
    public static IReadOnlyList<FlashcardWordSet> AllWordSets { get; } = [FlashcardWordSet.AllVocabulary, FlashcardWordSet.AnswerChoices];
    public static IReadOnlyList<FlashcardDeckSize> AllDeckSizes { get; } =
        [FlashcardDeckSize.Twenty, FlashcardDeckSize.Fifty, FlashcardDeckSize.OneHundred, FlashcardDeckSize.All];
}

/// <summary>What the flash-card area shows.</summary>
public enum FlashcardScreen
{
    /// <summary>Nothing started yet.</summary>
    None,
    /// <summary>A card (front or back, see <see cref="FlashcardSession.IsRevealed"/>).</summary>
    Card,
    /// <summary>"Every card mastered" (infinite loop finished).</summary>
    InfiniteCompletion,
    /// <summary>"Deck complete" summary.</summary>
    DeckCompletion,
    /// <summary>"No matching cards".</summary>
    EmptyDeck,
}

/// <summary>
/// The state behind <c>FlashcardStudyView</c> (FlashcardView.swift): fixed shuffled decks or an infinite
/// loop where "Study again" cards return after about 15–40 other cards; mastery persists.
/// Keyboard on the Mac: Space reveals, V plays the word, ← Study again, → Know it (when revealed).
/// Speech is a platform service: the UI plays <see cref="WordPronunciationLibrary.Clip"/> and must stop
/// playback when <see cref="StopSpeechRequested"/> fires (rating a card, new deck, leaving the view).
/// </summary>
public sealed class FlashcardSession : ObservableObject
{
    public const string ModeStorageKey = "gre-simulation.flashcards.mode";
    public const string WordSetStorageKey = "gre-simulation.flashcards.wordSet";
    public const string AnswerChoiceOrderStorageKey = "gre-simulation.flashcards.answerChoiceOrder";

    private readonly IReadOnlyList<VocabularyWord> _words;
    private readonly KeyValueStore _store;
    private readonly Random _random;
    private string _selectedSource = VocabularyBank.AllSources;
    private FlashcardDeckSize _deckSize = FlashcardDeckSize.Twenty;
    private bool _unmasteredOnly = true;
    private FlashcardMode _studyMode;
    private List<VocabularyWord> _deck = [];
    private int _currentIndex;
    private bool _isRevealed;
    private readonly HashSet<string> _masteredThisRound = [];
    private readonly HashSet<string> _missedThisRound = [];
    private bool _hasStarted;
    private int _infiniteTotal;
    private FlashcardWordSet _wordSet;
    private AnswerChoiceOrder _answerChoiceOrder;
    private AnswerChoicePartOfSpeech _partOfSpeech = AnswerChoicePartOfSpeech.All;

    public FlashcardProgressStore Progress { get; }

    /// <summary>Fires when any playing pronunciation must stop.</summary>
    public event EventHandler? StopSpeechRequested;

    public FlashcardSession(IReadOnlyList<VocabularyWord> words, KeyValueStore store, Random? random = null,
        AnswerChoiceWords? answerChoices = null)
    {
        _words = words;
        AnswerChoices = answerChoices ?? new AnswerChoiceWords(new AnswerChoiceWordSet(), words);
        _wordSet = store.GetString(WordSetStorageKey) == "answerChoices" ? FlashcardWordSet.AnswerChoices : FlashcardWordSet.AllVocabulary;
        _answerChoiceOrder = AnswerChoiceText.ParseOrder(store.GetString(AnswerChoiceOrderStorageKey));
        _store = store;
        _random = random ?? Random.Shared;
        Progress = new FlashcardProgressStore(store);
        _studyMode = store.GetString(ModeStorageKey) == "infinite" ? FlashcardMode.Infinite : FlashcardMode.Deck;
    }

    /// <summary>The grouped answer-choice words (the Answer-choice words deck, card badges and meaning groups).</summary>
    public AnswerChoiceWords AnswerChoices { get; }

    // MARK: - Settings

    /// <summary>Persisted like @AppStorage; changing it starts a new deck.</summary>
    public FlashcardWordSet WordSet
    {
        get => _wordSet;
        set
        {
            if (!SetProperty(ref _wordSet, value)) return;
            _store.SetString(WordSetStorageKey, value.RawValue());
            StartNewDeck();
        }
    }

    /// <summary>Persisted like @AppStorage; changing it starts a new deck.</summary>
    public AnswerChoiceOrder AnswerChoiceOrder
    {
        get => _answerChoiceOrder;
        set
        {
            if (!SetProperty(ref _answerChoiceOrder, value)) return;
            _store.SetString(AnswerChoiceOrderStorageKey, value.RawValue());
            StartNewDeck();
        }
    }

    /// <summary>Answer-choice words only; changing it starts a new deck.</summary>
    public AnswerChoicePartOfSpeech PartOfSpeech
    {
        get => _partOfSpeech;
        set { if (SetProperty(ref _partOfSpeech, value)) StartNewDeck(); }
    }

    /// <summary>Part of speech and Order replace Source for the Answer-choice words deck.</summary>
    public bool ShowsAnswerChoiceOptions => _wordSet == FlashcardWordSet.AnswerChoices;

    public string AnswerChoiceSummary =>
        $"{AnswerChoices.Words.Count} words that appear most often as Text Completion and Sentence Equivalence answer choices, with a few opposites added, sorted into {AnswerChoices.Set.Groups.Count} meaning groups. The back of each card shows its group: near-synonyms and the opposite side.";

    public string SelectedSource
    {
        get => _selectedSource;
        set { if (SetProperty(ref _selectedSource, value)) NotifyAll(); }
    }

    public FlashcardDeckSize DeckSize
    {
        get => _deckSize;
        set { if (SetProperty(ref _deckSize, value)) NotifyAll(); }
    }

    public bool UnmasteredOnly
    {
        get => _unmasteredOnly;
        set { if (SetProperty(ref _unmasteredOnly, value)) NotifyAll(); }
    }

    /// <summary>Persisted like @AppStorage; changing it starts a new deck (as onChange does on the Mac).</summary>
    public FlashcardMode StudyMode
    {
        get => _studyMode;
        set
        {
            if (!SetProperty(ref _studyMode, value)) return;
            _store.SetString(ModeStorageKey, value.RawValue());
            StartNewDeck();
        }
    }

    /// <summary>Deck size and Unmastered only are shown only in Fixed deck mode.</summary>
    public bool ShowsDeckOptions => _studyMode == FlashcardMode.Deck;

    // MARK: - State

    public IReadOnlyList<VocabularyWord> Deck => _deck;
    public int CurrentIndex => _currentIndex;
    public bool IsRevealed => _isRevealed;
    public bool HasStarted => _hasStarted;
    public int InfiniteTotal => _infiniteTotal;
    public int MasteredThisRoundCount => _masteredThisRound.Count;
    public int MissedThisRoundCount => _missedThisRound.Count;

    private IReadOnlyList<VocabularyWord> DeckWords => _wordSet == FlashcardWordSet.AnswerChoices ? AnswerChoices.Words : _words;

    public IReadOnlyList<VocabularyWord> EligibleWords => DeckWords.Where(item =>
    {
        var matchesSource = _wordSet == FlashcardWordSet.AnswerChoices
            ? _partOfSpeech.Matches(item)
            : _selectedSource == VocabularyBank.AllSources || item.Sources.Contains(_selectedSource);
        var skipsMastered = _unmasteredOnly || _studyMode == FlashcardMode.Infinite;
        var matchesProgress = !skipsMastered || !Progress.Mastered.Contains(item.Id);
        return matchesSource && matchesProgress;
    }).ToList();

    public VocabularyWord? CurrentCard => _currentIndex < _deck.Count ? _deck[_currentIndex] : null;

    public int OverallMasteredCount => DeckWords.Count(w => Progress.Mastered.Contains(w.Id));

    public FlashcardScreen Screen =>
        CurrentCard is not null ? FlashcardScreen.Card
        : _hasStarted && _studyMode == FlashcardMode.Infinite && _infiniteTotal > 0 ? FlashcardScreen.InfiniteCompletion
        : _hasStarted && _deck.Count > 0 ? FlashcardScreen.DeckCompletion
        : _hasStarted ? FlashcardScreen.EmptyDeck
        : FlashcardScreen.None;

    // Display text
    public string AvailableText => $"{EligibleWords.Count} available";
    public string MasteredText => $"{OverallMasteredCount} mastered";
    public string RestartButtonTitle => _studyMode == FlashcardMode.Infinite ? "Restart loop"
        : _wordSet == FlashcardWordSet.AnswerChoices && _answerChoiceOrder != AnswerChoiceOrder.Shuffled ? "New deck"
        : "New shuffled deck";
    public string DeckProgressTitle => $"Card {_currentIndex + 1} of {_deck.Count}";
    public string DeckProgressDetail => $"{_masteredThisRound.Count} known · {_missedThisRound.Count} review";
    /// <summary>Fixed-deck progress bar: value / total.</summary>
    public double DeckProgressValue => _currentIndex;
    public double DeckProgressTotal => Math.Max(_deck.Count, 1);
    public string InfiniteProgressDetail => $"{_masteredThisRound.Count} of {_infiniteTotal} mastered · {_deck.Count} left";
    public double InfiniteProgressValue => _masteredThisRound.Count;
    public double InfiniteProgressTotal => Math.Max(_infiniteTotal, 1);
    public const string InfiniteHint = "Cards marked Study again return after about 15–40 other cards, so you can check real recall.";
    public string DeckCompletionText => $"You knew {_masteredThisRound.Count} cards and marked {_missedThisRound.Count} for another review.";
    public bool CanReviewMissedCards => _missedThisRound.Count > 0;
    public string InfiniteCompletionText => $"You worked through all {_infiniteTotal} cards in this loop until each one was marked Know it.";
    public string EmptyDeckText => _unmasteredOnly || _studyMode == FlashcardMode.Infinite
        ? "Every matching card is mastered. Switch to a fixed deck with Unmastered only off, or reset mastery progress."
        : _wordSet == FlashcardWordSet.AnswerChoices ? "Choose a different part of speech to build a deck."
        : "Choose a different source to build a deck.";
    public const string ResetTitle = "Reset all flash-card progress?";
    public const string ResetMessage = "Every saved mastered word will return to the study pool.";

    // MARK: - Actions

    /// <summary>Call when the view appears (the Mac starts a deck the first time).</summary>
    public void OnAppear()
    {
        if (!_hasStarted) StartNewDeck();
    }

    /// <summary>Call when the view disappears.</summary>
    public void OnDisappear() => StopSpeechRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Tap the card / Space / Reveal answer.</summary>
    public void RevealCard()
    {
        if (CurrentCard is null || _isRevealed) return;
        _isRevealed = true;
        NotifyAll();
    }

    /// <summary>Know it (→) when <paramref name="asMastered"/>, Study again (←) otherwise. Only when revealed.</summary>
    public void RateCurrentCard(bool asMastered)
    {
        if (CurrentCard is not { } card) return;
        StopSpeechRequested?.Invoke(this, EventArgs.Empty);
        Progress.SetMastered(card.Id, asMastered);
        if (asMastered)
        {
            _masteredThisRound.Add(card.Id);
            _missedThisRound.Remove(card.Id);
        }
        else
        {
            _missedThisRound.Add(card.Id);
            _masteredThisRound.Remove(card.Id);
        }
        if (_studyMode == FlashcardMode.Infinite) AdvanceInfiniteLoop(asMastered ? null : card);
        else _currentIndex += 1;
        _isRevealed = false;
        NotifyAll();
    }

    /// <summary>Removes the current card; a missed card returns after enough other cards to test real recall.</summary>
    private void AdvanceInfiniteLoop(VocabularyWord? missed)
    {
        var queue = _deck.ToList();
        queue.RemoveAt(_currentIndex);
        if (missed is not null) queue.Insert(RequeueGap(queue.Count), missed);
        _deck = queue;
        _currentIndex = 0;
    }

    /// <summary>
    /// About a quarter of the remaining queue, kept between 15 and 40 cards with light jitter;
    /// a short queue sends the card to the end so every other card is seen first.
    /// </summary>
    internal int RequeueGap(int remaining)
    {
        var target = Math.Min(Math.Max(remaining / 4, 15), 40);
        var jittered = _random.Next(target * 4 / 5, target * 6 / 5 + 1);
        return Math.Min(remaining, jittered);
    }

    /// <summary>New shuffled deck / Restart loop (also after changing mode or resetting).</summary>
    public void StartNewDeck()
    {
        StopSpeechRequested?.Invoke(this, EventArgs.Empty);
        var shuffled = _wordSet == FlashcardWordSet.AnswerChoices
            ? AnswerChoices.Ordered(EligibleWords, _answerChoiceOrder, _random)
            : Shuffle(EligibleWords);
        var limit = _deckSize == FlashcardDeckSize.All || _studyMode == FlashcardMode.Infinite
            ? shuffled.Count
            : Math.Min((int)_deckSize, shuffled.Count);
        _deck = shuffled.Take(limit).ToList();
        _infiniteTotal = _studyMode == FlashcardMode.Infinite ? _deck.Count : 0;
        _currentIndex = 0;
        _isRevealed = false;
        _masteredThisRound.Clear();
        _missedThisRound.Clear();
        _hasStarted = true;
        NotifyAll();
    }

    /// <summary>Review missed cards (deck completion screen).</summary>
    public void ReviewMissedCards()
    {
        StopSpeechRequested?.Invoke(this, EventArgs.Empty);
        var missed = DeckWords.Where(w => _missedThisRound.Contains(w.Id)).ToList();
        _deck = _wordSet == FlashcardWordSet.AnswerChoices ? AnswerChoices.Ordered(missed, _answerChoiceOrder, _random) : Shuffle(missed);
        _infiniteTotal = 0;
        _currentIndex = 0;
        _isRevealed = false;
        _masteredThisRound.Clear();
        _missedThisRound.Clear();
        _hasStarted = true;
        NotifyAll();
    }

    /// <summary>"Start a fixed deck" on the infinite completion screen.</summary>
    public void StartFixedDeck() => StudyMode = FlashcardMode.Deck;

    /// <summary>Reset mastery progress (after confirmation), then a new deck.</summary>
    public void ResetProgress()
    {
        Progress.Reset();
        StartNewDeck();
    }

    private List<VocabularyWord> Shuffle(IEnumerable<VocabularyWord> items)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    private void NotifyAll() => OnPropertyChanged(string.Empty);
}

/// <summary>The Browse tab of the Vocabulary screen (LibraryViews.swift <c>VocabularyView</c>).</summary>
public sealed class VocabularyBrowser : ObservableObject
{
    private readonly VocabularyBank _bank;
    private string _searchText = "";
    private string _selectedSource = VocabularyBank.AllSources;
    private readonly HashSet<string> _revealed = [];

    public VocabularyBrowser(VocabularyBank bank) => _bank = bank;

    public const string Title = "High-frequency vocabulary lab";
    public const string Subtitle = "Browse the authorized offline word bank or work through shuffled flash-card sessions with persistent mastery progress.";
    public const string SearchPlaceholder = "Search words, meanings, or synonyms";
    public const string Footnote = "Word entries retain source tags after deduplication. Chinese definitions use Taiwan Traditional Chinese. Definitions may be compacted for card display; consult the authorized source material for complete study notes.";
    public const string RevealHint = "Click to reveal meaning";

    /// <summary>"All sources" followed by every source tag.</summary>
    public IReadOnlyList<string> SourceChoices => [VocabularyBank.AllSources, .. _bank.SourceOptions];

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) NotifyFilter(); }
    }

    public string SelectedSource
    {
        get => _selectedSource;
        set { if (SetProperty(ref _selectedSource, value)) NotifyFilter(); }
    }

    public IReadOnlyList<VocabularyWord> FilteredWords
    {
        get
        {
            var query = SwiftText.TrimWhitespaceAndNewlines(_searchText).ToLowerInvariant();
            return _bank.Words.Where(w =>
            {
                var matchesSource = _selectedSource == VocabularyBank.AllSources || w.Sources.Contains(_selectedSource);
                var matchesQuery = query.Length == 0
                    || w.Word.ToLowerInvariant().Contains(query, StringComparison.Ordinal)
                    || w.Definition.ToLowerInvariant().Contains(query, StringComparison.Ordinal)
                    || w.Chinese.Contains(query, StringComparison.Ordinal)
                    || w.Synonyms.Any(s => s.ToLowerInvariant().Contains(query, StringComparison.Ordinal));
                return matchesSource && matchesQuery;
            }).ToList();
        }
    }

    public string CountText => $"{FilteredWords.Count} words";

    /// <summary>Ids of the cards currently showing their meaning.</summary>
    public IReadOnlySet<string> RevealedWordIDs => _revealed;

    public bool IsRevealed(VocabularyWord word) => _revealed.Contains(word.Id);

    /// <summary>Tapping a card flips it between hidden and revealed.</summary>
    public void ToggleRevealed(VocabularyWord word)
    {
        if (!_revealed.Remove(word.Id)) _revealed.Add(word.Id);
        OnPropertyChanged(nameof(RevealedWordIDs));
    }

    private void NotifyFilter()
    {
        OnPropertyChanged(nameof(FilteredWords));
        OnPropertyChanged(nameof(CountText));
    }
}
