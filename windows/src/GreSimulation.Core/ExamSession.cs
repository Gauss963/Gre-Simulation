using CommunityToolkit.Mvvm.ComponentModel;

namespace GreSimulation.Core;

public enum ExamPhase
{
    Directions,
    Testing,
    Completed,
}

public enum QuestionStatus
{
    NotEncountered,
    NotAnswered,
    Incomplete,
    Answered,
}

public static class QuestionStatusText
{
    public static string Title(this QuestionStatus status) => status switch
    {
        QuestionStatus.NotEncountered => "Not Encountered",
        QuestionStatus.NotAnswered => "Not Answered",
        QuestionStatus.Incomplete => "Incomplete",
        _ => "Answered",
    };
}

/// <summary>
/// Port of <c>ExamSession</c> (ExamSession.swift) plus the non-drawing state logic of ExamView.swift
/// (numeric/fraction entry cleaning, essay word count, calculator transfer, dialog texts).
/// Backs the whole exam flow: directions → testing → (next section …) → completed.
/// </summary>
/// <remarks>
/// The UI must call <see cref="Tick()"/> regularly while <see cref="Phase"/> is Testing (the Mac app
/// ticks every 0.5 s); time is read from the <see cref="TimeProvider"/>, so a late tick never loses time.
/// When <see cref="Phase"/> becomes Completed, <see cref="Result"/> holds the score report; add it to
/// <see cref="HistoryStore"/> when the user chooses Save &amp; Close.
/// </remarks>
public sealed class ExamSession : ObservableObject
{
    private readonly FormBuilder _forms;
    private readonly QuestionBank _bank;
    private readonly TimeProvider _time;
    private readonly DateTimeOffset _startedAt;
    private DateTimeOffset? _sectionDeadline;
    private readonly HashSet<string> _completedSectionIDs = [];
    private readonly List<SectionSummary> _sectionSummaries = [];

    private List<ExamSection> _sections;
    private int _currentSectionIndex;
    private int _currentQuestionIndex;
    private readonly Dictionary<string, GREAnswer> _answers = [];
    private readonly HashSet<string> _markedQuestionIDs = [];
    private readonly HashSet<string> _encounteredIDs = [];
    private ExamPhase _phase = ExamPhase.Directions;
    private int _secondsRemaining;
    private bool _isTimeVisible = true;
    private bool _isCalculatorPresented;
    private ExamResult? _result;

    public ExamMode Mode { get; }

    /// <summary>Kept for the whole test, like the on-screen calculator in the Quantitative sections.</summary>
    public CalculatorModel Calculator { get; } = new();

    /// <summary>Raised once when the exam completes (after <see cref="Result"/> is set).</summary>
    public event EventHandler<ExamResult>? Completed;

    public ExamSession(ExamMode mode, FormBuilder forms, TimeProvider? timeProvider = null)
    {
        Mode = mode;
        _forms = forms;
        _bank = forms.Bank;
        _time = timeProvider ?? TimeProvider.System;
        _startedAt = _time.GetUtcNow();
        _sections = MakeSections(mode);
    }

    // MARK: - State

    public IReadOnlyList<ExamSection> Sections => _sections;
    public int CurrentSectionIndex => _currentSectionIndex;

    public int CurrentQuestionIndex
    {
        get => _currentQuestionIndex;
        set
        {
            _currentQuestionIndex = value;
            _encounteredIDs.Add(CurrentQuestion.Id);
            NotifyAll();
        }
    }

    public IReadOnlyDictionary<string, GREAnswer> Answers => _answers;
    public IReadOnlySet<string> MarkedQuestionIDs => _markedQuestionIDs;
    public IReadOnlySet<string> EncounteredIDs => _encounteredIDs;
    public ExamPhase Phase => _phase;
    public int SecondsRemaining => _secondsRemaining;

    public bool IsTimeVisible
    {
        get => _isTimeVisible;
        set { if (SetProperty(ref _isTimeVisible, value)) NotifyAll(); }
    }

    public bool IsCalculatorPresented
    {
        get => _isCalculatorPresented;
        set { if (SetProperty(ref _isCalculatorPresented, value)) NotifyAll(); }
    }

    public ExamResult? Result => _result;

    public ExamSection CurrentSection => _sections[_currentSectionIndex];
    public GREQuestion CurrentQuestion => CurrentSection.Questions[_currentQuestionIndex];
    public GREAnswer CurrentAnswer => _answers.GetValueOrDefault(CurrentQuestion.Id) ?? new GREAnswer();
    public bool IsCurrentMarked => _markedQuestionIDs.Contains(CurrentQuestion.Id);
    public int AnsweredCount => CurrentSection.Questions.Count(q => Status(q) == QuestionStatus.Answered);
    public int MarkedCount => CurrentSection.Questions.Count(q => _markedQuestionIDs.Contains(q.Id));
    public bool CanGoBack => _currentQuestionIndex > 0;
    public bool IsLastQuestion => _currentQuestionIndex == CurrentSection.Questions.Count - 1;
    public double Progress => (double)(_currentQuestionIndex + 1) / Math.Max(1, CurrentSection.Questions.Count);
    public string SectionPosition => $"Section {_currentSectionIndex + 1} of {_sections.Count}";

    /// <summary>The clock reappears during the last five minutes of a section, as on the GRE.</summary>
    public bool IsClockForcedVisible => _secondsRemaining <= 300;

    // MARK: - Exam-view state (ExamView.swift)

    /// <summary>Remaining time as "mm:ss".</summary>
    public string TimeText => $"{SwiftText.TwoDigits(_secondsRemaining / 60)}:{SwiftText.TwoDigits(_secondsRemaining % 60)}";
    /// <summary>The toolbar clock button title: the time, or "Show Time" while hidden.</summary>
    public string ClockButtonTitle => IsTimeVisible || IsClockForcedVisible ? TimeText : "Show Time";
    public string MarkButtonTitle => IsCurrentMarked ? "Marked" : "Mark";
    public string QuestionPositionText => $"Question {_currentQuestionIndex + 1} of {CurrentSection.Questions.Count}";
    /// <summary>Header title on the directions screen.</summary>
    public string DirectionsHeaderTitle => $"{SectionPosition} · General Test";
    /// <summary>Header title while testing (also the Review screen subtitle).</summary>
    public string TestingHeaderTitle => $"{SectionPosition} · {CurrentSection.DisplayTitle}";
    public string SectionDirections => ExamText.SectionDirections(CurrentSection.Measure);
    public string SectionTaskCountText => $"{CurrentSection.Questions.Count} {(CurrentSection.Questions.Count == 1 ? "task" : "questions")}";
    public string SectionDurationText => $"{CurrentSection.DurationSeconds / 60} minutes";
    public bool ShowsCalculatorButton => CurrentSection.Measure == GREMeasure.Quantitative;
    /// <summary>The floating calculator is shown only while testing a Quant section.</summary>
    public bool ShowsCalculator => IsCalculatorPresented && _phase == ExamPhase.Testing && CurrentSection.Measure == GREMeasure.Quantitative;
    public string AnswerRecordedText => CurrentAnswer.IsEmpty ? "Not answered" : "Answer recorded";
    /// <summary>Message of the "Exit this section?" confirmation.</summary>
    public string ExitSectionMessage => $"You have answered {AnsweredCount} of {CurrentSection.Questions.Count} questions. Once you exit this section, you cannot return to it.";
    public string ReviewFooterText => $"Answered {AnsweredCount} of {CurrentSection.Questions.Count} · Marked {MarkedCount}";
    /// <summary>The essay instructions: the prompt's own, or the default Issue instructions.</summary>
    public string EssayInstructions => CurrentQuestion.Instructions ?? ExamText.DefaultIssueInstructions;
    /// <summary>Live word count under the essay editor (split on whitespace and punctuation).</summary>
    public int EssayWordCount => CountEssayWords(CurrentAnswer.EssayText);
    public bool HasSupportingMaterial => CurrentQuestion.Stimulus is not null || CurrentQuestion.Figure is not null;

    /// <summary>Fraction answers use two boxes (numerator over denominator); others use one box.</summary>
    public bool IsFractionEntry => IsFractionQuestion(CurrentQuestion);
    public string NumericEntryHint => IsFractionEntry
        ? "Enter your answer as a fraction. Fractions do not need to be reduced to lowest terms."
        : "Enter your answer as an integer or a decimal.";

    /// <summary>Transfer Display is enabled only for decimal Numeric Entry questions.</summary>
    public bool CanTransferFromCalculator =>
        CurrentQuestion.Format == QuestionFormat.NumericEntry && !CurrentQuestion.AcceptedNumericAnswers.All(a => a.Contains('/'));

    // MARK: - Actions

    /// <summary>Begin Section: starts the clock and shows question 1.</summary>
    public void BeginSection()
    {
        _secondsRemaining = CurrentSection.DurationSeconds;
        _sectionDeadline = _time.GetUtcNow().AddSeconds(_secondsRemaining);
        _phase = ExamPhase.Testing;
        _currentQuestionIndex = 0;
        _encounteredIDs.Add(CurrentQuestion.Id);
        NotifyAll();
    }

    /// <summary>Updates the clock; when time runs out the section is finished automatically.</summary>
    public void Tick() => Tick(_time.GetUtcNow());

    public void Tick(DateTimeOffset now)
    {
        if (_phase != ExamPhase.Testing || _sectionDeadline is not { } deadline) return;
        var remaining = Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds));
        if (remaining != _secondsRemaining)
        {
            _secondsRemaining = remaining;
            NotifyAll();
        }
        if (_secondsRemaining == 0) FinishSection();
    }

    public void GoBack()
    {
        if (!CanGoBack) return;
        CurrentQuestionIndex -= 1;
    }

    public void GoNext()
    {
        if (IsLastQuestion) return;
        CurrentQuestionIndex += 1;
    }

    /// <summary>Questions that have not been reached yet cannot be opened from the Review screen.</summary>
    public void GoToQuestion(int index)
    {
        if (index < 0 || index >= CurrentSection.Questions.Count || !_encounteredIDs.Contains(CurrentSection.Questions[index].Id)) return;
        CurrentQuestionIndex = index;
    }

    public void ToggleMark()
    {
        if (!_markedQuestionIDs.Remove(CurrentQuestion.Id)) _markedQuestionIDs.Add(CurrentQuestion.Id);
        NotifyAll();
    }

    /// <summary>Selects or clears an option (radio when maximum is 1; otherwise up to maximum checks).</summary>
    public void ToggleSelection(string groupID, string optionID, int maximum)
    {
        var answer = CurrentAnswer.Clone();
        var selections = answer.Selections.GetValueOrDefault(groupID) ?? [];
        if (selections.Contains(optionID)) selections.Remove(optionID);
        else if (maximum == 1) selections = [optionID];
        else if (selections.Count < maximum) selections.Add(optionID);
        answer.Selections[groupID] = selections;
        _answers[CurrentQuestion.Id] = answer;
        NotifyAll();
    }

    /// <summary>Stores the Numeric Entry text as given (use <see cref="SetNumericEntryText"/> for typed input).</summary>
    public void UpdateNumericEntry(string text)
    {
        var answer = CurrentAnswer.Clone();
        answer.NumericEntry = text;
        _answers[CurrentQuestion.Id] = answer;
        NotifyAll();
    }

    /// <summary>The single decimal box: keeps digits, a leading minus, and one decimal point (10 characters).</summary>
    public void SetNumericEntryText(string typed) => UpdateNumericEntry(CleanNumber(typed, allowDecimal: true));

    /// <summary>Text of the numerator (0) or denominator (1) box.</summary>
    public string FractionPart(int index)
    {
        var parts = CurrentAnswer.NumericEntry.Split('/');
        return index >= 0 && index < parts.Length ? parts[index] : "";
    }

    /// <summary>Typing in the numerator (0) or denominator (1) box (digits and a leading minus only).</summary>
    public void SetFractionPart(int index, string typed)
    {
        var parts = CurrentAnswer.NumericEntry.Split('/').ToList();
        while (parts.Count < 2) parts.Add("");
        parts[index] = CleanNumber(typed, allowDecimal: false);
        UpdateNumericEntry(parts.All(p => p.Length == 0) ? "" : string.Join("/", parts));
    }

    /// <summary>Transfer Display: copies the calculator display into the Numeric Entry box when allowed.</summary>
    public void TransferCalculatorDisplay()
    {
        var value = Calculator.TransferText;
        if (CanTransferFromCalculator && value.Length > 0) UpdateNumericEntry(value);
    }

    /// <summary>Stores the essay text. Line breaks are normalized to "\n" as on the Mac (WinUI TextBox uses "\r").</summary>
    public void UpdateEssay(string text)
    {
        var answer = CurrentAnswer.Clone();
        answer.EssayText = NormalizeLineEndings(text);
        _answers[CurrentQuestion.Id] = answer;
        NotifyAll();
    }

    /// <summary>Exit Section (confirmed) or time up: scores the section, routes the second section, moves on.</summary>
    public void FinishSection()
    {
        if (_phase != ExamPhase.Testing || _completedSectionIDs.Contains(CurrentSection.Id)) return;
        var section = CurrentSection;
        _completedSectionIDs.Add(section.Id);
        _sectionDeadline = null;

        var scored = section.Questions.Where(q => q.IsScored).ToList();
        var correct = scored.Count(IsCorrect);
        var answered = section.Questions.Count(q => !(_answers.GetValueOrDefault(q.Id) ?? new GREAnswer()).IsEmpty);
        _sectionSummaries.Add(new SectionSummary
        {
            Measure = section.Measure,
            Ordinal = section.Ordinal,
            Difficulty = section.AdaptiveDifficulty,
            Correct = correct,
            Total = scored.Count,
            Answered = answered,
        });

        if (section.Ordinal == 1 && section.Measure is GREMeasure.Verbal or GREMeasure.Quantitative)
            ConfigureAdaptiveSecondSection(section.Measure, correct, scored.Count);

        if (_currentSectionIndex + 1 < _sections.Count)
        {
            _currentSectionIndex += 1;
            _currentQuestionIndex = 0;
            _encounteredIDs.Add(CurrentQuestion.Id);
            _phase = ExamPhase.Directions;
            NotifyAll();
        }
        else
        {
            CompleteExam();
        }
    }

    /// <summary>Exit Section from the confirmation dialog: also closes the calculator (as on the Mac).</summary>
    public void ConfirmExitSection()
    {
        IsCalculatorPresented = false;
        FinishSection();
    }

    public bool IsCorrect(GREQuestion question)
    {
        var answer = _answers.GetValueOrDefault(question.Id) ?? new GREAnswer();
        return IsCorrect(question, answer);
    }

    public static bool IsCorrect(GREQuestion question, GREAnswer answer)
    {
        if (question.Format == QuestionFormat.NumericEntry)
            return question.AcceptedNumericAnswers.Any(expected => NumericAnswerMatcher.Matches(answer.NumericEntry, expected));
        if (question.Format == QuestionFormat.Essay) return false;
        return question.CorrectSelections.All(pair =>
            (answer.Selections.GetValueOrDefault(pair.Key) ?? []).SetEquals(pair.Value));
    }

    public QuestionStatus Status(GREQuestion question)
    {
        if (!_encounteredIDs.Contains(question.Id)) return QuestionStatus.NotEncountered;
        var answer = _answers.GetValueOrDefault(question.Id) ?? new GREAnswer();
        if (answer.IsEmpty) return QuestionStatus.NotAnswered;
        switch (question.Format)
        {
            case QuestionFormat.NumericEntry:
                var parts = answer.NumericEntry.Split('/');
                return parts.Any(p => p.Trim(' ', '\t').Length == 0) ? QuestionStatus.Incomplete : QuestionStatus.Answered;
            case QuestionFormat.Essay:
                return QuestionStatus.Answered;
            case QuestionFormat.SentenceEquivalence:
                return (answer.Selections.GetValueOrDefault("main")?.Count ?? 0) == 2 ? QuestionStatus.Answered : QuestionStatus.Incomplete;
            default:
                var allGroups = question.Groups.All(g => (answer.Selections.GetValueOrDefault(g.Id)?.Count ?? 0) > 0);
                return allGroups ? QuestionStatus.Answered : QuestionStatus.Incomplete;
        }
    }

    /// <summary>Rows of the Review screen for the current section.</summary>
    public IReadOnlyList<ReviewRow> ReviewRows =>
        CurrentSection.Questions.Select((q, i) => new ReviewRow(i, i + 1, Status(q), _markedQuestionIDs.Contains(q.Id), i == _currentQuestionIndex)).ToList();

    // MARK: - Routing and results

    private void ConfigureAdaptiveSecondSection(GREMeasure measure, int firstSectionCorrect, int total)
    {
        var targetIndex = _sections.FindIndex(s => s.Measure == measure && s.Ordinal == 2);
        if (targetIndex < 0) return;
        var accuracy = total == 0 ? 0 : (double)firstSectionCorrect / total;
        var route = accuracy >= 2.0 / 3.0 ? QuestionDifficulty.Hard : (accuracy >= 0.4 ? QuestionDifficulty.Medium : QuestionDifficulty.Easy);
        var level = route == QuestionDifficulty.Hard ? FormBuilder.Level.Hard : (route == QuestionDifficulty.Easy ? FormBuilder.Level.Easy : FormBuilder.Level.Medium);
        var used = _sections.Where(s => s.Measure == measure && s.Ordinal == 1).SelectMany(s => s.Questions.Select(q => q.Id)).ToHashSet();
        var questions = measure == GREMeasure.Verbal
            ? _forms.VerbalSection(15, level, used)
            : _forms.QuantSection(15, level, used);
        _sections[targetIndex] = new ExamSection
        {
            Id = $"{measure.RawValue()}-2-{route.RawValue()}",
            Measure = measure,
            Ordinal = 2,
            DurationSeconds = measure == GREMeasure.Verbal ? 23 * 60 : 26 * 60,
            Questions = questions,
            AdaptiveDifficulty = route,
        };
    }

    private void CompleteExam()
    {
        _phase = ExamPhase.Completed;
        var writingQuestion = _sections.FirstOrDefault(s => s.Measure == GREMeasure.AnalyticalWriting)?.Questions.FirstOrDefault();
        var essay = writingQuestion is not null ? _answers.GetValueOrDefault(writingQuestion.Id)?.EssayText ?? "" : "";
        var hasEssay = SwiftText.TrimWhitespaceAndNewlines(essay).Length > 0;
        var essayReport = writingQuestion is not null ? IssueEssayScorer.Score(essay, writingQuestion.Prompt) : null;
        var questionReviews = _sections
            .Where(s => _completedSectionIDs.Contains(s.Id))
            .SelectMany(section => section.Questions
                .Select((question, index) => (question, index))
                .Where(item => item.question.IsScored)
                .Select(item => new QuestionReviewRecord
                {
                    SectionID = section.Id,
                    SectionOrdinal = section.Ordinal,
                    SectionDifficulty = section.AdaptiveDifficulty,
                    QuestionNumber = item.index + 1,
                    Question = item.question,
                    Answer = (_answers.GetValueOrDefault(item.question.Id) ?? new GREAnswer()).Clone(),
                    IsCorrect = IsCorrect(item.question),
                }))
            .ToList();
        var verbal = Totals(GREMeasure.Verbal);
        var quant = Totals(GREMeasure.Quantitative);
        var now = _time.GetUtcNow();

        _result = new ExamResult
        {
            Id = Guid.NewGuid(),
            CompletedAt = now,
            Mode = Mode,
            VerbalScore = ScaledScore(GREMeasure.Verbal),
            QuantitativeScore = ScaledScore(GREMeasure.Quantitative),
            WritingEstimate = hasEssay ? essayReport?.Score : null,
            VerbalCorrect = verbal.Correct,
            VerbalTotal = verbal.Total,
            QuantitativeCorrect = quant.Correct,
            QuantitativeTotal = quant.Total,
            ElapsedSeconds = Math.Max(0, (int)(now - _startedAt).TotalSeconds),
            Sections = _sectionSummaries.ToList(),
            QuestionReviews = questionReviews,
            WritingSummary = hasEssay ? essayReport?.Summary : null,
        };
        NotifyAll();
        Completed?.Invoke(this, _result);
    }

    /// <summary>No score (null) when no question of the measure was answered, as ETS reports "NS".</summary>
    private int? ScaledScore(GREMeasure measure)
    {
        var summaries = _sectionSummaries.Where(s => s.Measure == measure).ToList();
        if (summaries.Count == 0 || !summaries.Any(s => s.Answered > 0)) return null;
        var first = summaries.FirstOrDefault(s => s.Ordinal == 1);
        var second = summaries.FirstOrDefault(s => s.Ordinal == 2);
        return ScoreEstimator.Scaled(
            measure,
            first?.Correct ?? 0, first?.Total ?? 0,
            second?.Correct ?? 0, second?.Total ?? 0,
            second?.Difficulty);
    }

    private (int Correct, int Total) Totals(GREMeasure measure) =>
        _sectionSummaries.Where(s => s.Measure == measure).Aggregate((0, 0), (sum, s) => (sum.Item1 + s.Correct, sum.Item2 + s.Total));

    private ExamSection FirstSection(GREMeasure measure)
    {
        var none = new HashSet<string>();
        var questions = measure == GREMeasure.Verbal
            ? _forms.VerbalSection(12, FormBuilder.Level.Average, none)
            : _forms.QuantSection(12, FormBuilder.Level.Average, none);
        return new ExamSection
        {
            Id = $"{measure.RawValue()}-1",
            Measure = measure,
            Ordinal = 1,
            DurationSeconds = measure == GREMeasure.Verbal ? 18 * 60 : 21 * 60,
            Questions = questions,
            AdaptiveDifficulty = null,
        };
    }

    /// <summary>Placeholder until section 1 is scored; replaced by the routed second section.</summary>
    private ExamSection SecondSectionPlaceholder(GREMeasure measure, IReadOnlySet<string> excluding)
    {
        var questions = measure == GREMeasure.Verbal
            ? _forms.VerbalSection(15, FormBuilder.Level.Medium, excluding, remember: false)
            : _forms.QuantSection(15, FormBuilder.Level.Medium, excluding, remember: false);
        return new ExamSection
        {
            Id = $"{measure.RawValue()}-2-pending",
            Measure = measure,
            Ordinal = 2,
            DurationSeconds = measure == GREMeasure.Verbal ? 23 * 60 : 26 * 60,
            Questions = questions,
            AdaptiveDifficulty = QuestionDifficulty.Medium,
        };
    }

    private ExamSection WritingSection()
    {
        var prompts = _bank.WritingQuestions;
        var question = prompts.Count > 0 ? prompts[_bank.Random.Next(prompts.Count)] : _bank.WritingQuestion;
        return new ExamSection
        {
            Id = "writing-1",
            Measure = GREMeasure.AnalyticalWriting,
            Ordinal = 1,
            DurationSeconds = 30 * 60,
            Questions = [question],
            AdaptiveDifficulty = null,
        };
    }

    private List<ExamSection> MakeSections(ExamMode mode)
    {
        List<ExamSection> Measure(GREMeasure m)
        {
            var first = FirstSection(m);
            return [first, SecondSectionPlaceholder(m, first.Questions.Select(q => q.Id).ToHashSet())];
        }
        switch (mode)
        {
            case ExamMode.FullLength:
                var verbal = Measure(GREMeasure.Verbal);
                var quant = Measure(GREMeasure.Quantitative);
                var ordered = _forms.SectionOrder().Select(pair => (pair.Measure == GREMeasure.Verbal ? verbal : quant)[pair.Ordinal - 1]);
                return [WritingSection(), .. ordered];
            case ExamMode.VerbalPractice:
                return Measure(GREMeasure.Verbal);
            case ExamMode.QuantitativePractice:
                return Measure(GREMeasure.Quantitative);
            default:
                return [WritingSection()];
        }
    }

    // MARK: - Helpers

    /// <summary>Digits, a leading minus sign, and (outside fraction boxes) one decimal point; at most 10 characters.</summary>
    public static string CleanNumber(string value, bool allowDecimal)
    {
        var result = new System.Text.StringBuilder();
        foreach (var character in SwiftText.Characters(value))
        {
            if (SwiftText.IsNumber(character)) result.Append(character);
            else if (character is "-" or "−" && result.Length == 0) result.Append('-');
            else if (allowDecimal && character == "." && !result.ToString().Contains('.')) result.Append('.');
        }
        var text = result.ToString();
        return SwiftText.CharacterCount(text) <= 10 ? text : string.Concat(SwiftText.Characters(text).Take(10));
    }

    /// <summary>Essay word count shown under the editor (Swift: split on whitespace or punctuation).</summary>
    public static int CountEssayWords(string text) =>
        SwiftText.Split(text, c => SwiftText.IsWhitespace(c) || SwiftText.IsPunctuation(c)).Count;

    public static bool IsFractionQuestion(GREQuestion question) =>
        question.AcceptedNumericAnswers.Count > 0 && question.AcceptedNumericAnswers.All(a => a.Contains('/'));

    public static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private void NotifyAll() => OnPropertyChanged(string.Empty);

    /// <summary>Debug helper ported from <c>completeWithCorrectAnswersForTesting()</c>.</summary>
    internal void CompleteWithCorrectAnswersForTesting()
    {
        var safetyCounter = 0;
        while (_phase != ExamPhase.Completed && safetyCounter < 10)
        {
            safetyCounter += 1;
            if (_phase == ExamPhase.Directions) BeginSection();
            foreach (var question in CurrentSection.Questions)
            {
                _encounteredIDs.Add(question.Id);
                if (question.Format == QuestionFormat.Essay)
                {
                    _answers[question.Id] = new GREAnswer
                    {
                        EssayText = string.Concat(Enumerable.Repeat("A clear position should examine evidence, acknowledge reasonable limits, and explain why the conclusion follows. ", 35)),
                    };
                }
                else if (question.Format == QuestionFormat.NumericEntry)
                {
                    _answers[question.Id] = new GREAnswer { NumericEntry = question.AcceptedNumericAnswers.Order(StringComparer.Ordinal).FirstOrDefault() ?? "" };
                }
                else
                {
                    _answers[question.Id] = new GREAnswer
                    {
                        Selections = question.CorrectSelections.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value)),
                    };
                }
            }
            FinishSection();
        }
    }
}

/// <summary>One row of the Review screen.</summary>
public sealed record ReviewRow(int Index, int Number, QuestionStatus Status, bool IsMarked, bool IsCurrent)
{
    public string StatusTitle => Status.Title();
    public bool IsEncountered => Status != QuestionStatus.NotEncountered;
}

/// <summary>
/// The Review sheet's own state (ExamView.swift <c>ReviewView</c>): a selected row, opened at the current
/// question; Go to Question is disabled for Not Encountered rows.
/// </summary>
public sealed class ReviewScreenState : ObservableObject
{
    private readonly ExamSession _session;
    private int? _selection;

    public ReviewScreenState(ExamSession session)
    {
        _session = session;
        _selection = session.CurrentQuestionIndex;
    }

    public int? Selection
    {
        get => _selection;
        set
        {
            if (SetProperty(ref _selection, value)) OnPropertyChanged(nameof(CanGoToQuestion));
        }
    }

    public bool CanGoToQuestion =>
        _selection is int index && index >= 0 && index < _session.CurrentSection.Questions.Count
        && _session.Status(_session.CurrentSection.Questions[index]) != QuestionStatus.NotEncountered;

    /// <summary>Go to Question: jumps (if allowed); the UI then dismisses the sheet.</summary>
    public void GoToQuestion()
    {
        if (_selection is int index) _session.GoToQuestion(index);
    }

    public const string Instructions = "Select a question and choose Go to Question. You cannot go to questions that are Not Encountered.";
}
