using System.ComponentModel;
using System.Runtime.CompilerServices;
using GreSimulation.App.Controls;
using GreSimulation.App.Services;
using GreSimulation.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Core;

namespace GreSimulation.App.Pages;

/// <summary>
/// The Vocabulary page: Browse (searchable word cards) and Flash Cards (fixed decks or an infinite loop with
/// spaced "Study again" cards, persistent mastery, and pronunciation). Port of VocabularyView and
/// FlashcardStudyView. Keyboard in Flash Cards: Space reveals, V plays the word, Left = Study again, Right = Know it.
/// </summary>
public sealed partial class VocabularyPage : Page
{
    private const double BrowseCardMinimumWidth = 285;
    private const double BrowseCardSpacing = 14;

    private GreServices? services;
    private VocabularyBrowser? browser;
    private FlashcardSession? session;
    private PronunciationPlayer? player;
    private readonly Dictionary<string, BrowseCardItem> browseItems = new(StringComparer.Ordinal);
    private List<BrowseRow> rows = [];
    private int browseColumns;
    private bool refreshing;
    private string? shownCardId;
    private bool shownCardRevealed;

    public VocabularyPage()
    {
        InitializeComponent();
        HeadingText.Text = VocabularyBrowser.Title;
        SubheadingText.Text = VocabularyBrowser.Subtitle;
        SearchBox.PlaceholderText = VocabularyBrowser.SearchPlaceholder;
        BrowseFootnote.Text = VocabularyBrowser.Footnote;
        InfiniteHintText.Text = FlashcardSession.InfiniteHint;
        FlashModeBox.ItemsSource = new List<string> { FlashcardMode.Deck.Title(), FlashcardMode.Infinite.Title() };
        DeckSizeBox.ItemsSource = FlashcardText.AllDeckSizes.Select(size => size.Title()).ToList();
        FlashWordSetBox.ItemsSource = FlashcardText.AllWordSets.Select(set => set.Title()).ToList();
        FlashPartOfSpeechBox.ItemsSource = AnswerChoiceText.AllPartsOfSpeech.Select(kind => kind.Title()).ToList();
        FlashOrderBox.ItemsSource = AnswerChoiceText.AllOrders.Select(order => order.Title()).ToList();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (services is not null) return;
        GreServices loaded;
        try
        {
            loaded = await AppServices.Ready;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return;
        }
        if (services is not null) return;
        services = loaded;
        player = new PronunciationPlayer(loaded.Pronunciations);
        player.StateChanged += OnSpeechStateChanged;
        browser = loaded.CreateVocabularyBrowser();
        BrowseSourceBox.ItemsSource = browser.SourceChoices.ToList();
        BrowseSourceBox.SelectedItem = browser.SelectedSource;
        FlashSourceBox.ItemsSource = browser.SourceChoices.ToList();
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        ShowStudyMode();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        EndFlashcards();
        if (player is not null)
        {
            player.StateChanged -= OnSpeechStateChanged;
            player.Dispose();
            player = null;
        }
    }

    // MARK: - Study mode (Browse / Flash Cards)

    private bool IsFlashCardsSelected => ReferenceEquals(StudyModeBar.SelectedItem, FlashCardsItem);

    private void OnStudyModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => ShowStudyMode();

    private void ShowStudyMode()
    {
        if (services is null) return;
        if (IsFlashCardsSelected)
        {
            BrowsePanel.Visibility = Visibility.Collapsed;
            FlashPanel.Visibility = Visibility.Visible;
            StartFlashcards();
        }
        else
        {
            // Like the Mac, leaving the flash cards ends the session; returning starts a fresh deck.
            EndFlashcards();
            FlashPanel.Visibility = Visibility.Collapsed;
            BrowsePanel.Visibility = Visibility.Visible;
            RefreshBrowse();
        }
    }

    // MARK: - Browse

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (browser is null) return;
        browser.SearchText = sender.Text;
        RefreshBrowse();
    }

    private void OnBrowseSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (browser is null || BrowseSourceBox.SelectedItem is not string source) return;
        browser.SelectedSource = source;
        RefreshBrowse();
    }

    private void RefreshBrowse()
    {
        if (browser is null || BrowsePanel.Visibility != Visibility.Visible) return;
        var words = browser.FilteredWords;
        WordCountText.Text = browser.CountText;
        var items = words.Select(word =>
        {
            if (!browseItems.TryGetValue(word.Id, out var item))
            {
                item = new BrowseCardItem(word);
                browseItems[word.Id] = item;
            }
            item.IsRevealed = browser.IsRevealed(word);
            return item;
        }).ToList();
        browseColumns = ColumnCount(WordRows.ActualWidth);
        rows = items.Chunk(browseColumns).Select(chunk => new BrowseRow(chunk)).ToList();
        WordRows.ItemsSource = rows;
    }

    private static int ColumnCount(double width)
    {
        if (width <= 0 || double.IsNaN(width)) return 2;
        return Math.Max(1, (int)Math.Floor((width + BrowseCardSpacing) / (BrowseCardMinimumWidth + BrowseCardSpacing)));
    }

    private void OnWordRowsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ColumnCount(e.NewSize.Width) != browseColumns) RefreshBrowse();
    }

    private void OnWordRowPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not Grid grid || args.Index < 0 || args.Index >= rows.Count) return;
        var row = rows[args.Index];
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        for (var column = 0; column < browseColumns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var template = (DataTemplate)Resources["BrowseCardTemplate"];
        for (var index = 0; index < row.Items.Count; index++)
        {
            var presenter = new ContentPresenter
            {
                ContentTemplate = template,
                Content = row.Items[index],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };
            Grid.SetColumn(presenter, index);
            grid.Children.Add(presenter);
        }
    }

    private void OnBrowseCardClick(object sender, RoutedEventArgs e)
    {
        if (browser is null || (sender as FrameworkElement)?.DataContext is not BrowseCardItem item) return;
        browser.ToggleRevealed(item.Word);
        item.IsRevealed = browser.IsRevealed(item.Word);
    }

    private async void OnExampleSourceClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    // MARK: - Flash cards

    private void StartFlashcards()
    {
        if (services is null || session is not null) return;
        session = services.CreateFlashcards();
        session.PropertyChanged += OnSessionChanged;
        session.StopSpeechRequested += OnStopSpeechRequested;
        shownCardId = null;
        refreshing = true;
        FlashSourceBox.SelectedItem = session.SelectedSource;
        refreshing = false;
        session.OnAppear();
        RefreshFlashcards();
        RestoreKeyboardFocus();
    }

    private void EndFlashcards()
    {
        if (session is null) return;
        session.OnDisappear();
        session.PropertyChanged -= OnSessionChanged;
        session.StopSpeechRequested -= OnStopSpeechRequested;
        session = null;
        player?.Stop();
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e) => RefreshFlashcards();

    private void OnStopSpeechRequested(object? sender, EventArgs e) => player?.Stop();

    private void RefreshFlashcards()
    {
        if (session is not { } cards) return;
        refreshing = true;
        try
        {
            if (!Equals(FlashSourceBox.SelectedItem, cards.SelectedSource)) FlashSourceBox.SelectedItem = cards.SelectedSource;
            FlashModeBox.SelectedIndex = cards.StudyMode == FlashcardMode.Deck ? 0 : 1;
            FlashWordSetBox.SelectedIndex = IndexOf(FlashcardText.AllWordSets, cards.WordSet);
            FlashPartOfSpeechBox.SelectedIndex = IndexOf(AnswerChoiceText.AllPartsOfSpeech, cards.PartOfSpeech);
            FlashOrderBox.SelectedIndex = IndexOf(AnswerChoiceText.AllOrders, cards.AnswerChoiceOrder);
            var answerChoiceOptions = Show(cards.ShowsAnswerChoiceOptions);
            FlashPartOfSpeechBox.Visibility = answerChoiceOptions;
            FlashOrderBox.Visibility = answerChoiceOptions;
            AnswerChoiceSummaryText.Visibility = answerChoiceOptions;
            AnswerChoiceSummaryText.Text = cards.AnswerChoiceSummary;
            FlashSourceBox.Visibility = Show(!cards.ShowsAnswerChoiceOptions);
            DeckSizeBox.SelectedIndex = IndexOf(FlashcardText.AllDeckSizes, cards.DeckSize);
            UnmasteredSwitch.IsOn = cards.UnmasteredOnly;
            var deckOptions = cards.ShowsDeckOptions ? Visibility.Visible : Visibility.Collapsed;
            DeckSizeBox.Visibility = deckOptions;
            UnmasteredSwitch.Visibility = deckOptions;

            AvailableText.Text = cards.AvailableText;
            MasteredText.Text = cards.MasteredText;
            RestartButton.Content = cards.RestartButtonTitle;

            var screen = cards.Screen;
            var showsCard = screen == FlashcardScreen.Card;
            var isInfinite = cards.StudyMode == FlashcardMode.Infinite;
            DeckProgressPanel.Visibility = Show(showsCard && !isInfinite);
            InfiniteProgressPanel.Visibility = Show(showsCard && isInfinite);
            CardHost.Visibility = Show(showsCard);
            RatingControls.Visibility = Show(showsCard);
            DeckCompletionPanel.Visibility = Show(screen == FlashcardScreen.DeckCompletion);
            InfiniteCompletionPanel.Visibility = Show(screen == FlashcardScreen.InfiniteCompletion);
            EmptyDeckPanel.Visibility = Show(screen == FlashcardScreen.EmptyDeck);

            if (showsCard && cards.CurrentCard is { } card)
            {
                if (isInfinite)
                {
                    InfiniteProgressDetail.Text = cards.InfiniteProgressDetail;
                    InfiniteProgressBar.Maximum = cards.InfiniteProgressTotal;
                    InfiniteProgressBar.Value = cards.InfiniteProgressValue;
                }
                else
                {
                    DeckProgressTitle.Text = cards.DeckProgressTitle;
                    DeckProgressDetail.Text = cards.DeckProgressDetail;
                    DeckProgressBar.Maximum = cards.DeckProgressTotal;
                    DeckProgressBar.Value = cards.DeckProgressValue;
                }
                if (shownCardId != card.Id || shownCardRevealed != cards.IsRevealed) ShowCard(card, cards.IsRevealed);
                StudyAgainButton.Visibility = Show(cards.IsRevealed);
                KnowItButton.Visibility = Show(cards.IsRevealed);
                RevealButton.Visibility = Show(!cards.IsRevealed);
            }
            else
            {
                shownCardId = null;
            }

            DeckCompletionText.Text = cards.DeckCompletionText;
            ReviewMissedButton.Visibility = Show(cards.CanReviewMissedCards);
            InfiniteCompletionText.Text = cards.InfiniteCompletionText;
            EmptyDeckText.Text = cards.EmptyDeckText;
            UpdateListenButtons();
        }
        finally
        {
            refreshing = false;
        }
    }

    private void ShowCard(VocabularyWord word, bool revealed)
    {
        shownCardId = word.Id;
        shownCardRevealed = revealed;
        CardFront.Visibility = Show(!revealed);
        CardBack.Visibility = Show(revealed);

        // Front
        FrontHighFrequency.Visibility = Show(word.IsHighFrequency);
        var answerChoices = session?.AnswerChoices;
        var countText = answerChoices?.CountText(word);
        FrontAnswerChoiceText.Text = countText ?? "";
        FrontAnswerChoice.Visibility = Show(countText is not null);
        FrontWord.Text = word.Word;
        var detail = string.Join("  ", new[] { word.Pronunciation, word.PartOfSpeech ?? "" }.Where(part => part.Length > 0));
        FrontDetail.Text = detail;
        FrontDetail.Visibility = Show(detail.Length > 0);

        // Back
        BackWord.Text = word.Word;
        BackPronunciation.Text = word.Pronunciation.Length > 0 ? "   " + word.Pronunciation : "";
        BackPartOfSpeech.Text = word.PartOfSpeech is { Length: > 0 } partOfSpeech ? "   " + partOfSpeech : "";
        BackHighFrequency.Visibility = Show(word.IsHighFrequency);
        BackDefinition.Text = word.Definition;
        BackChinese.Text = word.Chinese;
        BackChineseBlock.Visibility = Show(word.Chinese.Length > 0);
        ShowMeaningGroup(word, answerChoices);
        BackSynonyms.Text = string.Join(" · ", word.Synonyms);
        BackSynonymsBlock.Visibility = Show(word.Synonyms.Count > 0);
        BackExample.Text = word.Example;
        BackExampleBlock.Visibility = Show(word.Example.Length > 0);
        var source = word.ExampleSource ?? "";
        var hasLink = source.Length > 0 && Uri.TryCreate(word.ExampleSourceURL, UriKind.Absolute, out _);
        BackExampleLink.Visibility = Show(hasLink);
        BackExampleLink.Tag = hasLink ? word.ExampleSourceURL : null;
        BackExampleLinkText.Text = source;
        BackExampleSource.Text = source;
        BackExampleSource.Visibility = Show(source.Length > 0 && !hasLink);
        BackSources.Text = string.Join(" · ", word.Sources);
        BackSources.Visibility = Show(word.Sources.Count > 0);
    }

    /// <summary>The meaningGroup block of the Mac card back: same side (near-synonyms) and the opposite side.</summary>
    private void ShowMeaningGroup(VocabularyWord word, AnswerChoiceWords? answerChoices)
    {
        MeaningGroupPoles.Children.Clear();
        if (answerChoices?.Group(word) is not { } placement)
        {
            MeaningGroupBlock.Visibility = Visibility.Collapsed;
            return;
        }
        MeaningGroupBlock.Visibility = Visibility.Visible;
        MeaningGroupTitle.Text = "  " + placement.Group.Title;
        // The card's own side first, then the opposite side.
        var sideOrder = new[] { placement.Pole }.Concat(Enumerable.Range(0, placement.Group.Poles.Count).Where(i => i != placement.Pole));
        foreach (var index in sideOrder)
        {
            var side = placement.Group.Poles[index];
            var isOwnSide = index == placement.Pole;
            var panel = new StackPanel { Spacing = 3 };
            panel.Children.Add(new TextBlock
            {
                Text = (isOwnSide ? "Same side · " : "Opposite · ") + side.Label,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Palette.Brush(MeaningGroupBlock, isOwnSide ? Tone.Teal : Tone.Warning),
            });
            var members = new TextBlock { FontSize = 14, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            for (var i = 0; i < side.Words.Count; i++)
            {
                var member = side.Words[i];
                if (i > 0) members.Inlines.Add(new Run { Text = "  ·  " });
                var name = new Run { Text = member, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
                if (string.Equals(member, word.Word, StringComparison.OrdinalIgnoreCase))
                    members.Inlines.Add(new Underline { Inlines = { name } });
                else
                    members.Inlines.Add(name);
                if (answerChoices.Entry(member) is { } entry)
                    members.Inlines.Add(new Run
                    {
                        Text = " " + AnswerChoiceWords.ShortGloss(entry),
                        Foreground = Palette.Brush(MeaningGroupBlock, Tone.Secondary),
                    });
            }
            panel.Children.Add(members);
            MeaningGroupPoles.Children.Add(panel);
        }
    }

    private void UpdateListenButtons()
    {
        if (session?.CurrentCard is not { } card) return;
        var speaking = player?.IsSpeakingWord(card) == true;
        var glyph = speaking ? "" : "";
        var name = speaking ? "Stop pronunciation" : $"Listen to {card.Word}";
        var help = speaking ? "Stop pronunciation (V)" : $"Listen to {card.Word} (V)";
        FrontListenIcon.Glyph = glyph;
        FrontListenText.Text = speaking ? "Stop" : "Listen";
        BackListenIcon.Glyph = glyph;
        foreach (var button in new[] { FrontListenButton, BackListenButton })
        {
            AutomationProperties.SetName(button, name);
            ToolTipService.SetToolTip(button, help);
        }
    }

    private void OnSpeechStateChanged(object? sender, EventArgs e) => UpdateListenButtons();

    private void OnFlashSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || FlashSourceBox.SelectedItem is not string source) return;
        session.SelectedSource = source;
    }

    private void OnFlashWordSetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || FlashWordSetBox.SelectedIndex < 0) return;
        var set = FlashcardText.AllWordSets[FlashWordSetBox.SelectedIndex];
        if (session.WordSet == set) return;
        session.WordSet = set; // starts a new deck, like onChange(of: wordSet)
        RestoreKeyboardFocus();
    }

    private void OnFlashPartOfSpeechChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || FlashPartOfSpeechBox.SelectedIndex < 0) return;
        session.PartOfSpeech = AnswerChoiceText.AllPartsOfSpeech[FlashPartOfSpeechBox.SelectedIndex];
        RestoreKeyboardFocus();
    }

    private void OnFlashOrderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || FlashOrderBox.SelectedIndex < 0) return;
        session.AnswerChoiceOrder = AnswerChoiceText.AllOrders[FlashOrderBox.SelectedIndex];
        RestoreKeyboardFocus();
    }

    private void OnFlashModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || FlashModeBox.SelectedIndex < 0) return;
        var mode = FlashModeBox.SelectedIndex == 0 ? FlashcardMode.Deck : FlashcardMode.Infinite;
        if (session.StudyMode == mode) return;
        session.StudyMode = mode; // starts a new deck, like onChange(of: studyMode)
        RestoreKeyboardFocus();
    }

    private void OnDeckSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || session is null || DeckSizeBox.SelectedIndex < 0) return;
        session.DeckSize = FlashcardText.AllDeckSizes[DeckSizeBox.SelectedIndex];
    }

    private void OnUnmasteredToggled(object sender, RoutedEventArgs e)
    {
        if (refreshing || session is null) return;
        session.UnmasteredOnly = UnmasteredSwitch.IsOn;
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        session?.StartNewDeck();
        RestoreKeyboardFocus();
    }

    private void OnReviewMissedClick(object sender, RoutedEventArgs e)
    {
        session?.ReviewMissedCards();
        RestoreKeyboardFocus();
    }

    private void OnStartFixedDeckClick(object sender, RoutedEventArgs e)
    {
        session?.StartFixedDeck();
        RestoreKeyboardFocus();
    }

    private void OnRevealClick(object sender, RoutedEventArgs e) => RevealCard();

    private void OnStudyAgainClick(object sender, RoutedEventArgs e) => RateCard(asMastered: false);

    private void OnKnowItClick(object sender, RoutedEventArgs e) => RateCard(asMastered: true);

    private void OnListenClick(object sender, RoutedEventArgs e)
    {
        if (session?.CurrentCard is { } card) TogglePronunciation(card);
    }

    private void OnCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (session is null || session.IsRevealed) return;
        // Taps on the Listen button or the source link must not flip the card.
        for (var element = e.OriginalSource as DependencyObject; element is not null && !ReferenceEquals(element, CardBorder);
             element = VisualTreeHelper.GetParent(element))
        {
            if (element is ButtonBase) return;
        }
        RevealCard();
    }

    private async void OnResetProgressClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = FlashcardSession.ResetTitle,
            Content = FlashcardSession.ResetMessage,
            PrimaryButtonText = "Reset mastery progress",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            session?.ResetProgress();
            RestoreKeyboardFocus();
        }
    }

    private void RevealCard()
    {
        session?.RevealCard();
        RestoreKeyboardFocus();
    }

    private void RateCard(bool asMastered)
    {
        if (session is null || !session.IsRevealed) return;
        session.RateCurrentCard(asMastered);
        RestoreKeyboardFocus();
    }

    private void TogglePronunciation(VocabularyWord word)
    {
        player?.Toggle(word);
        RestoreKeyboardFocus();
    }

    /// <summary>Handled on the way down (PreviewKeyDown) so the page ScrollViewer does not take the arrow keys.</summary>
    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || session is null || FlashPanel.Visibility != Visibility.Visible) return;
        // Typing in a field or operating a picker keeps its own keys.
        if (e.OriginalSource is TextBox or ComboBox or ComboBoxItem or ToggleSwitch or SelectorBarItem or AutoSuggestBox) return;
        if (IsDown(VirtualKey.Control) || IsDown(VirtualKey.Menu) || IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) return;
        var card = session.CurrentCard;
        if (card is null) return;
        switch (e.Key)
        {
            case VirtualKey.Space when !session.IsRevealed && e.OriginalSource is not ButtonBase:
                RevealCard();
                e.Handled = true;
                break;
            case VirtualKey.V:
                TogglePronunciation(card);
                e.Handled = true;
                break;
            case VirtualKey.Left when session.IsRevealed:
                RateCard(asMastered: false);
                e.Handled = true;
                break;
            case VirtualKey.Right when session.IsRevealed:
                RateCard(asMastered: true);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Keeps Space, V and the arrows working after a click (the Mac's restoreKeyboardFocus).</summary>
    private void RestoreKeyboardFocus()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (session is not null && CardHost.Visibility == Visibility.Visible) CardHost.Focus(FocusState.Programmatic);
        });
    }

    private static bool IsDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static int IndexOf<T>(IReadOnlyList<T> list, T value)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(list[index], value)) return index;
        }
        return -1;
    }
}

/// <summary>One row of the Browse grid (the Mac's adaptive LazyVGrid, minimum card width 285).</summary>
public sealed class BrowseRow(IReadOnlyList<BrowseCardItem> items)
{
    public IReadOnlyList<BrowseCardItem> Items { get; } = items;
}

/// <summary>A Browse card: a word and whether its meaning is showing.</summary>
public sealed class BrowseCardItem : INotifyPropertyChanged
{
    private bool isRevealed;

    public BrowseCardItem(VocabularyWord word)
    {
        Word = word;
        HeaderDetail = string.Concat(
            word.Pronunciation.Length > 0 ? "   " + word.Pronunciation : "",
            word.PartOfSpeech is { Length: > 0 } partOfSpeech ? "   " + partOfSpeech : "");
        SynonymsText = string.Join(" · ", word.Synonyms);
        SourcesText = string.Join(" · ", word.Sources);
        ExampleSourceText = word.ExampleSource ?? "";
        var hasSource = word.Example.Length > 0 && ExampleSourceText.Length > 0;
        var hasLink = hasSource && Uri.TryCreate(word.ExampleSourceURL, UriKind.Absolute, out _);
        ExampleSourceUrl = hasLink ? word.ExampleSourceURL! : "";
        ChineseVisibility = Show(word.Chinese.Length > 0);
        SynonymsVisibility = Show(word.Synonyms.Count > 0);
        ExampleVisibility = Show(word.Example.Length > 0);
        ExampleLinkVisibility = Show(hasLink);
        ExampleSourceTextVisibility = Show(hasSource && !hasLink);
        SourcesVisibility = Show(word.Sources.Count > 0);
    }

    public VocabularyWord Word { get; }
    public string HeaderDetail { get; }
    public string SynonymsText { get; }
    public string SourcesText { get; }
    public string ExampleSourceText { get; }
    public string ExampleSourceUrl { get; }
    public Visibility ChineseVisibility { get; }
    public Visibility SynonymsVisibility { get; }
    public Visibility ExampleVisibility { get; }
    public Visibility ExampleLinkVisibility { get; }
    public Visibility ExampleSourceTextVisibility { get; }
    public Visibility SourcesVisibility { get; }

    public bool IsRevealed
    {
        get => isRevealed;
        set
        {
            if (isRevealed == value) return;
            isRevealed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RevealedVisibility));
            OnPropertyChanged(nameof(HiddenVisibility));
            OnPropertyChanged(nameof(EyeGlyph));
            OnPropertyChanged(nameof(AccessibleName));
        }
    }

    public Visibility RevealedVisibility => Show(isRevealed);
    public Visibility HiddenVisibility => Show(!isRevealed);
    /// <summary>Segoe Fluent "View" (eye) when revealed, "Hide" otherwise.</summary>
    public string EyeGlyph => isRevealed ? "" : "";
    public string AccessibleName => isRevealed ? $"{Word.Word}: {Word.Definition}" : $"{Word.Word}. Click to reveal meaning";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
