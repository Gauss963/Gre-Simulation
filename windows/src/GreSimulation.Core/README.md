# GreSimulation.Core — API guide for the Windows UI

Platform-neutral port of the Mac app's logic (`Gre Simulation/*.swift`). Everything except drawing
and platform services lives here; the WinUI project binds to these types. Namespace: `GreSimulation.Core`.
All screen texts below are verbatim from the Mac app — use them instead of retyping strings.

## 1. Startup and shutdown

```csharp
var services = GreServices.Load(
    resourceDirectory: Path.Combine(AppContext.BaseDirectory, "Resources"),   // copy of the Mac Resources folder
    dataDirectory: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gre Simulation"));
IssueEssayScorer.SpellChecker = new WindowsSpellChecker(); // optional, see §8
```

* `Load` parses ~16 MB of JSON (≈1 s): call it off the UI thread (`await Task.Run(...)`) behind a splash.
* `GreServices` exposes `Questions` (QuestionBank), `Forms` (FormBuilder), `Vocabulary` (VocabularyBank),
  `Pronunciations` (WordPronunciationLibrary), `History` (HistoryStore), `Settings` (KeyValueStore).
* Data folder files: `Preferences.json` (all UserDefaults/@AppStorage keys, same names as the Mac) and
  `ScoreHistory-v2.json` (same JSON as the Mac's `Application Support/Gre Simulation/ScoreHistory-v2.json`,
  so a Mac history file can be copied in). Every change is written immediately and atomically — there is
  nothing to "save on exit". Dispose `services` on exit (closes the audio pack).
* `services.Validate()` returns the Mac Debug-launch validation issues (empty = sound). Tests cover it.

## 2. Screen → type map

| Mac view (Swift) | C# |
|---|---|
| `ContentView` sidebar (`AppPage`) | `AppPage` enum + `AppPageText` (`Title()`, `MacSymbol()`, brand strings) |
| `DashboardView` | static `Dashboard` (texts, `SessionCards`, `FormatRows`, `LatestScoreChips(result)`, `BankSummary(...)`), `ExamMode.Title()/Subtitle()`, latest result = `services.History.Results.FirstOrDefault()` |
| `ExamContainerView`, `SectionDirectionsView`, `TestInterfaceView`, `QuestionView` | `ExamSession` (+ `ExamText` strings) |
| `ReviewView` | `ExamSession.ReviewRows` + `ReviewScreenState` |
| `HelpView` | `question.ContentArea`, `question.Format.Directions()`, `ExamText.Help*` |
| `CalculatorView` / `FloatingCalculator` | `ExamSession.Calculator` (`CalculatorModel`) |
| `EssayEditor` | a plain `TextBox` + `ExamSession.UpdateEssay` / `EssayWordCount` (§5.4) |
| `QuantFigureView` | `QuantFigureLayout` (§6) |
| `ScoreReportView` | `ScoreReport` |
| `VocabularyView` (Browse) | `VocabularyBrowser` |
| `FlashcardStudyView` | `FlashcardSession` (+ `FlashcardProgressStore`) |
| `HistoryView` | `HistoryStore` + `HistoryCard` + static `HistoryScreen` texts |
| `ResourcesView` | static `ResourcesPage` (+ `Questions.SourceSummary`) |
| `GRETheme` colors | `GreTheme` (sRGB light/dark pairs; system colors → Windows theme brushes) |

All observable types derive from CommunityToolkit `ObservableObject` (INotifyPropertyChanged). Several raise
`PropertyChanged` with an **empty property name** after a state change, meaning "all properties changed";
both `{x:Bind}` (Mode=OneWay) and `{Binding}` refresh every binding on that object. Use them on the UI thread only.

## 3. Content types (Models.cs)

`GREQuestion` (Id, Measure, Difficulty, Format, Stimulus?, Prompt, QuantityA/B?, Groups, CorrectSelections,
AcceptedNumericAnswers, Explanation, ContentArea, Source?, Figure?, SetID?, Instructions?), `ChoiceGroup`
(Id, Title?, Options, MaximumSelections), `ChoiceOption`, `GREAnswer` (Selections, NumericEntry, EssayText,
`IsSelected(groupId, optionId)`), `ExamSection` (`DisplayTitle`), `SectionSummary`, `QuestionReviewRecord`,
`ExamResult`, `VocabularyWord`, `QuantFigure*`. Enums: `GREMeasure`, `QuestionDifficulty`, `QuestionFormat`,
`ExamMode`, `QuantFigureKind` with `Title()`, `ShortTitle()`, `Directions()`, `Subtitle()`, `RawValue()`.
`GreJson.Options` reads/writes exactly the Swift Codable JSON (camelCase keys, raw-value enums, nil omitted,
`Date` = seconds since 2001‑01‑01, `UUID` upper-case).

## 4. Starting a test (Dashboard → exam)

`var session = services.StartExam(mode);` builds the form exactly like the Mac (Writing first; Verbal 12 q/18 min
then 15/23; Quant 12/21 then 15/26; random valid section order; exposure rotation saved). Show the exam
full-window; on "Exit Test" confirm with `ExamText.ExitTest*` and simply drop the session (nothing is saved).

## 5. ExamSession

### 5.1 Phases
`Phase`: `Directions` → (`BeginSection()`) → `Testing` → (`FinishSection()` / time-out) → `Directions` of the
next section … → `Completed`. Event `Completed` fires once with the `ExamResult`.

* **Directions screen**: header `DirectionsHeaderTitle`; title `CurrentSection.DisplayTitle`; `SectionDirections`;
  bullet list `ExamText.SectionRules` (not for Writing); `SectionTaskCountText`, `SectionDurationText`;
  `ExamText.TimingBegins`; button `ExamText.BeginSection` → `BeginSection()`.
* **Testing screen**: header `TestingHeaderTitle`; toolbar `QuestionPositionText`; Calculator button only if
  `ShowsCalculatorButton` (toggles `IsCalculatorPresented`); clock button title `ClockButtonTitle` (toggles
  `IsTimeVisible`; the time is forced visible in the last 5 minutes); Mark button `MarkButtonTitle` →
  `ToggleMark()`; Review; Help; "Exit Section". Progress bar `Progress` (0–1). Footer: Back (`CanGoBack`,
  `GoBack()`), `AnswerRecordedText`, and Next (`GoNext()`) or "Review Section" when `IsLastQuestion`.
* **Exit Section** (toolbar or Review): confirm with `ExamText.ExitSectionTitle`, message `ExitSectionMessage`,
  buttons `ExamText.ExitSectionConfirm` → `ConfirmExitSection()` / `ExamText.ExitSectionCancel`.
* **Completed**: show `services.ReportForCompletedExam(session.Result!)`; its "Save & Close" →
  `services.History.Add(result)` then navigate to Score History (as the Mac does).

### 5.2 Timer — the UI must tick
Run a `DispatcherQueueTimer` (0.5 s, like the Mac) while `Phase == Testing` and call `session.Tick()`.
`SecondsRemaining` = ceil(deadline − now); at 0 the section finishes by itself. `TimeText` is "mm:ss".
`Tick(DateTimeOffset now)` exists for tests; the constructor accepts a `TimeProvider`.

### 5.3 Question area (`CurrentQuestion`, `CurrentAnswer`)
* Directions line: `CurrentQuestion.Format.Directions()`.
* Supporting panel when `HasSupportingMaterial`: `Stimulus` text, or for `SelectInPassage` the sentences
  `Groups[0].Options` (click → `ToggleSelection(group.Id, option.Id, 1)`, highlight `CurrentAnswer.IsSelected`,
  hint `ExamText.SelectInPassageHint`); plus the figure (§6).
* Prompt; for `QuantitativeComparison` the two columns `ExamText.QuantityATitle/BTitle` with `QuantityA/B`.
* Choices (all formats except NumericEntry, SelectInPassage, Essay): for each group (`Title` above it if not
  null; side by side when several groups) each option is a radio when `MaximumSelections == 1`, else a check
  box; click → `ToggleSelection(group.Id, option.Id, group.MaximumSelections)`.
* Numeric Entry: hint `NumericEntryHint`. If `IsFractionEntry`, two boxes bound to `FractionPart(0/1)`, typing
  → `SetFractionPart(index, text)`; else one box bound to `CurrentAnswer.NumericEntry`, typing →
  `SetNumericEntryText(text)` (cleans like the Mac: digits, leading minus, one point, max 10 chars). Re-read the
  property after setting (the cleaned text may differ from what was typed).
* Status per question: `Status(question)` → `QuestionStatus` (`Title()`).

### 5.4 Essay (Analytical Writing)
Prompt (`CurrentQuestion.Prompt`), `EssayInstructions`, "Response" label and `$"{EssayWordCount} words"`.
Editor: a multi-line `TextBox` (AcceptsReturn, TextWrapping, **IsSpellCheckEnabled=false,
IsTextPredictionEnabled=false**, Georgia 16), `TextChanged` → `session.UpdateEssay(textBox.Text)`.
`UpdateEssay` stores "\n" line endings like the Mac (WinUI uses "\r"); do not push the stored text back into
the TextBox while typing. Toolbar Cut / Paste / Undo / Redo → `TextBox.CutSelectionToClipboard()`,
`PasteFromClipboard()`, `Undo()`, `Redo()` (TextBox is plain text, matching the Mac's paste-as-plain-text).

### 5.5 Review sheet
`var review = new ReviewScreenState(session);` (selection starts at the current question). Rows:
`session.ReviewRows` (`Number`, `StatusTitle`, `IsMarked` → check mark, `IsCurrent`, `IsEncountered` → dim
when false); click sets `review.Selection`. "Go to Question" enabled by `review.CanGoToQuestion` →
`review.GoToQuestion()` then close; "Return" closes; footer `session.ReviewFooterText` and "Exit Section".
Instructions text: `ReviewScreenState.Instructions`.

### 5.6 Calculator (Quant sections only)
Show when `session.ShowsCalculator`; floating, draggable (drag offset is UI state). Keys: render
`CalculatorModel.KeyRows`; each press → `Calculator.Input(key)` (keys are the exact labels, e.g. "÷", "×", "−",
"±", "√", "M+"). Bind `Display`, `Expression` (small line above), `HasMemory` ("M" badge). Styling:
`IsEqualsKey` (action blue), `IsInputKey` (input background). "Transfer Display" enabled by
`session.CanTransferFromCalculator` → `session.TransferCalculatorDisplay()`. Close → `IsCalculatorPresented = false`.
The calculator keeps its state for the whole test (one per session).

## 6. Quant figures — `new QuantFigureLayout(question.Figure)`
Title, optional Caption above; `XAxisTitle` centered below; `YAxisTitle` beside the y axis; reserve
`MinimumHeight`. `ShowsLegend` / `SeriesNames` (series i → chart palette color i).

* **Table**: `TableHeaders` (header row), `TableRows`; column 0 cells semibold; `IsAlternateRow(rowIndex)` →
  alternate background; cells min width 82, min height 38; `AccessibleTableText` for screen readers.
* **Bar / GroupedBar / Histogram**: `CategoryData` (Series, Label, Value), `Categories` (x order); y axis from
  `YAxis` (`LowerBound`, `UpperBound`, `TickValues`, `Label(v)`, `Fraction(v)` → 0–1 height; grid line at every
  tick, labels on the right). Value label above each bar: `BarValueLabel(value)`. `IsGrouped` → bars of
  different series side by side; plain Bar stacks series at one x (Swift Charts default; bank bar figures have
  one series); `BarsFillCategory`
  for histograms (no gaps, single bright-blue color).
* **Line**: `CoordinateData` per series, lines 2.5 wide with points; y axis `YAxis`; x scale `XAxis`.
* **Scatter**: points (size 70), except series where `IsTrendSeries(name)` → dashed line (2 wide, dash 6/4).
* **NormalCurve**: lines only, y axis hidden; use `NormalCurveYRange` and `XAxis` for scaling; legend shown.
* **Pie**: `PieCharts` (one donut per series; `ShowsTitle` → series name above). Slices have `StartDegrees`/
  `SweepDegrees` clockwise from 12 o'clock and `PaletteIndex` into `PiePaletteNames`
  (brightBlue, teal, orange, purple, pink, indigo); inner radius ratio 0.42, 1-pt inset; legend grid of
  (color dot, label) in two columns below.
* **BoxPlot**: `BoxPlotBoxes` (Name, Low, Q1, Median, Q3, High); each row: name (58 wide, right-aligned), then a
  48-tall area; x = `BoxPlotPosition(value, rowWidth)`; whisker line low–high with 22-tall caps, box q1–q3
  32 tall (fill brightBlue 22 %, stroke blue 2), median line 2 wide navy. Under the rows:
  `BoxPlotMinimumLabel()`, `BoxPlotScaleCaption`, `BoxPlotMaximumLabel()`.
* **Venn**: two circles of `VennDiameter(w, h)` centered at `VennLeftCenter`/`VennRightCenter` (fractions of the
  area; blue 20 % and teal 18 % fills), then each of `VennAnnotations` centered at (x·w, y·h): `Label` (caption)
  over `Value` (headline). Accessibility: `VennAccessibilityLabel`.

## 7. Score report, history, dashboard
* `ScoreReport`: `Heading`, `DoneTitle`, `Subtitle()`, `EstimateNotice`, `ScoreCards` (Title, ScoreText — "NS"
  when null —, RawText, Range, `PercentileText`), `WritingSummary` (under `WritingFeedbackTitle`), `SectionRows`
  (`Title`, `RouteText` badge, `ScoreText`, `Fraction` progress when `HasScore`), review: `HasReviewRecords`
  (else `ReviewUnavailableTitle/Message`), `ReviewIntro`, `ReviewGroups` (collapsible; `Title`, `CorrectText`;
  items: `Heading`, ✓/✗ `IsCorrect`, `Question.Prompt` (3 lines), `Question.ContentArea`, `Question.Explanation`,
  `GuessingTip` under `ScoreReport.GuessingTipHeading`, `Question.Figure`, `YourAnswerText`,
  `CorrectAnswerText`, `SourceText` with book/wand icon by `IsAuthorizedSourceItem`), `Methodology*` texts.
* History: `services.History.Results` (newest first); per result `new HistoryCard(result)` (`Title`, `DateText()`,
  `DurationText`, `Metrics`, `SectionChips`, `CanReviewAnswers` → open `services.ReportForSavedResult(result)`
  as a full-window overlay, else `HistoryScreen.ScoreOnly`). Clear (shown when there are results): confirm
  `HistoryScreen.ClearTitle` / `ClearConfirm` → `services.History.Clear()`. Empty state: `HistoryScreen.EmptyTitle/EmptyMessage`.
* Dashboard: see §2; latest result card uses `DisplayDates.AbbreviatedDate` and `Dashboard.LatestScoreChips`.

## 8. Vocabulary and pronunciation
* Browse: `var browser = services.CreateVocabularyBrowser();` bind `SearchText`, `SelectedSource`
  (`SourceChoices`), `FilteredWords`, `CountText`; tapping a card → `ToggleRevealed(word)`, `IsRevealed(word)` /
  `RevealedWordIDs`. Card back shows Definition, Chinese (teal), Synonyms joined " · ", Example (italic) with
  `ExampleSource` (a link when `ExampleSourceURL` is set), Sources joined " · ".
* Flash cards: `var cards = services.CreateFlashcards();` call `OnAppear()` / `OnDisappear()`. Settings:
  `SelectedSource`, `StudyMode` (persisted; changing it starts a new deck), and when `ShowsDeckOptions`:
  `DeckSize` (`FlashcardText.AllDeckSizes`, `Title()`), `UnmasteredOnly`. Show by `Screen`: `Card` (front/back by
  `IsRevealed`, progress via `DeckProgress*` or `InfiniteProgress*` + `FlashcardSession.InfiniteHint`),
  `DeckCompletion` (`DeckCompletionText`, "Review missed cards" when `CanReviewMissedCards` →
  `ReviewMissedCards()`, "Start another deck" → `StartNewDeck()`), `InfiniteCompletion` (`InfiniteCompletionText`,
  "Start a fixed deck" → `StartFixedDeck()`), `EmptyDeck` (`EmptyDeckText`). Actions: `RevealCard()` (tap/Space),
  `RateCurrentCard(false)` "Study again" (←) / `RateCurrentCard(true)` "Know it" (→) — the UI only offers rating
  when revealed; `StartNewDeck()` ("New shuffled deck"/"Restart loop" = `RestartButtonTitle`); reset menu →
  confirm `ResetTitle`/`ResetMessage` → `ResetProgress()`. Header counts: `AvailableText`, `MasteredText`.
* Audio: `services.Pronunciations.Clip(word.Word)` returns the .m4a bytes (or null). Play them (MediaPlayer +
  `InMemoryRandomAccessStream`); "V" toggles play/stop; show Stop while playing; stop when
  `FlashcardSession.StopSpeechRequested` fires. If `Clip` is null, speak the word with Windows speech synthesis
  (Mac: best en-US voice, rate 0.42).
* Spell checking for essay scores: implement `ISpellChecker.CountMisspellings(text)` with the Windows
  spell-checking API (`ISpellCheckerFactory`, "en-US", count every reported error) and assign
  `IssueEssayScorer.SpellChecker` at startup.

## 9. Not ported / differences (and why)
* **Spell checker** (`NSSpellChecker`) is a platform service: without an `ISpellChecker` no misspellings are
  counted, so essay estimates can be higher than on the Mac for misspelled essays.
* **Speech and audio playback** (AVSpeechSynthesizer/AVAudioPlayer) — UI/platform; Core supplies the clip bytes.
* **Essay editor commands** — native TextBox features on Windows (no Core logic exists on the Mac either).
* **Swift Charts automatic layout**: the x axis of line/scatter/normal-curve charts, legend placement and series
  colors are chosen by Swift Charts on the Mac; `XAxis`/`NormalCurveYRange` use the app's own y-axis rule as an
  approximation. The y-axis rule, bar labels, box plot, Venn and pie geometry are exact ports.
* **Locale formatting** of dates/numbers uses .NET cultures (`DisplayDates`, `AxisNumber`), which may differ in
  small details from Foundation's.
* **Debug launch arguments** (`-selfTest`, `-exportVerbal`, `-qaQuestion`, `-completeExam`, `-beginSection`,
  `-start…Practice`, `-open…`) are not ported; the self-test is the xunit suite.
* Random shuffles use .NET `Random`, so a given form differs from any Mac form (the rules are identical).
* Swift `Character` rules are reproduced with .NET text elements/runes; `lowercased()` uses .NET's simple
  case mapping — only exotic Unicode input could behave differently.

## 10. Maintenance
Content that the Mac app writes as Swift literals (`VerbalQuestionBank.swift`, `QuantitativeQuestionBank.swift`,
`QuestionBank.writingQuestion`, `VocabularyBank.curatedWords`) is embedded from `Data/*.json`. After changing
those Swift files run `tests/GreSimulation.Core.Tests/Reference/SwiftExport/export.sh` on a Mac with Xcode; it
compiles copies of the Mac sources and also refreshes the Swift reference values the tests compare against.
`MacSourceParityTests` fails when the embedded copies drift from the Swift files. All other content is read
from the Resources folder at run time.
