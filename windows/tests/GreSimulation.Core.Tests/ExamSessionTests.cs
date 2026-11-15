namespace GreSimulation.Core.Tests;

public class ExamSessionTests
{
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void FormBuilderValidateReportsNoIssues() => Assert.Empty(TestContent.Services.Forms.Validate());

    [Fact]
    public void FullLengthFormsMatchTheSelfTestLayoutAndPerfectRunsReach170()
    {
        using var services = TestContent.Fresh();
        var retired = services.Questions.RetiredQuestionReasons;
        for (var run = 0; run < 12; run++)
        {
            var session = services.StartExam(ExamMode.FullLength);
            Assert.Equal(GREMeasure.AnalyticalWriting, session.Sections[0].Measure);
            Assert.Equal(30, session.Sections[0].DurationSeconds / 60);
            Assert.Equal(5, session.Sections.Count);
            foreach (var section in session.Sections.Where(s => s.Measure != GREMeasure.AnalyticalWriting))
            {
                Assert.Equal(section.Ordinal == 1 ? 12 : 15, section.Questions.Count);
                var expectedMinutes = section.Measure == GREMeasure.Verbal ? (section.Ordinal == 1 ? 18 : 23) : (section.Ordinal == 1 ? 21 : 26);
                Assert.Equal(expectedMinutes, section.DurationSeconds / 60);
                Assert.DoesNotContain(section.Questions, q => retired.ContainsKey(q.Id));
            }
            foreach (var measure in new[] { GREMeasure.Verbal, GREMeasure.Quantitative })
                Assert.Equal([1, 2], session.Sections.Where(s => s.Measure == measure).Select(s => s.Ordinal));

            session.CompleteWithCorrectAnswersForTesting();
            Assert.Equal(ExamPhase.Completed, session.Phase);
            var result = session.Result!;
            Assert.Equal(170, result.VerbalScore);
            Assert.Equal(170, result.QuantitativeScore);
            Assert.Equal(result.VerbalTotal + result.QuantitativeTotal, result.QuestionReviews!.Count);
            Assert.Equal(27, result.VerbalTotal);
            Assert.Equal(27, result.QuantitativeTotal);
            Assert.Equal(5, result.Sections.Count);
            Assert.All(result.Sections.Where(s => s.Ordinal == 2), s => Assert.Equal(QuestionDifficulty.Hard, s.Difficulty));
            Assert.DoesNotContain(result.QuestionReviews!, r => retired.ContainsKey(r.Question.Id));
        }
    }

    [Fact]
    public void FirstSectionsHaveTheOfficialShape()
    {
        var session = TestContent.Services.StartExam(ExamMode.FullLength);
        var verbal = session.Sections.First(s => s.Measure == GREMeasure.Verbal);
        Assert.Equal(QuestionFormat.TextCompletion, verbal.Questions[0].Format);
        Assert.InRange(verbal.Questions.Count(q => q.Stimulus is not null), 5, 7);
        Assert.Equal(2, verbal.Questions.Count(q => q.Format == QuestionFormat.SentenceEquivalence));
        var quant = session.Sections.First(s => s.Measure == GREMeasure.Quantitative);
        Assert.All(quant.Questions.Take(4), q => Assert.Equal(QuestionFormat.QuantitativeComparison, q.Format));
        // One Data Interpretation set (two questions sharing a display) among the later discrete questions.
        Assert.Contains(quant.Questions.Skip(4).Where(q => q.Figure is not null).GroupBy(q => q.Figure!.Title), g => g.Count() >= 2);
    }

    [Fact]
    public void BlankMeasureReportsNoScore()
    {
        var blank = TestContent.Services.StartExam(ExamMode.VerbalPractice);
        blank.BeginSection(); blank.FinishSection(); blank.BeginSection(); blank.FinishSection();
        Assert.Equal(ExamPhase.Completed, blank.Phase);
        Assert.Null(blank.Result!.VerbalScore);
        Assert.Equal(QuestionDifficulty.Easy, blank.Result.Sections[1].Difficulty);
        Assert.Equal("verbal-2-easy", blank.Sections[1].Id);
        Assert.Null(blank.Result.WritingEstimate);
    }

    [Fact]
    public void TimerFinishesTheSectionWhenTimeRunsOut()
    {
        var time = new ManualTime(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
        var session = new ExamSession(ExamMode.QuantitativePractice, TestContent.Services.Forms, time);
        Assert.Equal(ExamPhase.Directions, session.Phase);
        session.BeginSection();
        Assert.Equal(21 * 60, session.SecondsRemaining);
        Assert.Equal("21:00", session.TimeText);
        Assert.False(session.IsClockForcedVisible);
        time.Now = time.Now.AddSeconds(16 * 60 + 0.4);
        session.Tick();
        Assert.Equal(300, session.SecondsRemaining); // ceil(299.6)
        Assert.True(session.IsClockForcedVisible);
        session.IsTimeVisible = false;
        Assert.Equal("05:00", session.ClockButtonTitle);
        time.Now = time.Now.AddSeconds(300);
        session.Tick();
        Assert.Equal(ExamPhase.Directions, session.Phase);
        Assert.Equal(1, session.CurrentSectionIndex);
        Assert.Equal(26 * 60, session.CurrentSection.DurationSeconds);
        Assert.Equal("Section 2 of 2", session.SectionPosition);
    }

    [Fact]
    public void NavigationMarkingAndStatus()
    {
        var session = TestContent.Services.StartExam(ExamMode.VerbalPractice);
        session.BeginSection();
        Assert.False(session.CanGoBack);
        Assert.Equal(QuestionStatus.NotEncountered, session.Status(session.CurrentSection.Questions[3]));
        session.GoToQuestion(3); // not encountered yet: ignored
        Assert.Equal(0, session.CurrentQuestionIndex);
        Assert.Equal(QuestionStatus.NotAnswered, session.Status(session.CurrentQuestion));

        var question = session.CurrentQuestion;
        var group = question.Groups[0];
        session.ToggleSelection(group.Id, group.Options[0].Id, group.MaximumSelections);
        Assert.Equal(question.Groups.Count == 1 ? QuestionStatus.Answered : QuestionStatus.Incomplete, session.Status(question));
        Assert.Equal("Answer recorded", session.AnswerRecordedText);

        session.ToggleMark();
        Assert.True(session.IsCurrentMarked);
        Assert.Equal("Marked", session.MarkButtonTitle);
        session.GoNext(); session.GoNext();
        Assert.Equal(2, session.CurrentQuestionIndex);
        session.GoToQuestion(0);
        Assert.Equal(0, session.CurrentQuestionIndex);
        Assert.Equal(1, session.MarkedCount);
        var rows = session.ReviewRows;
        Assert.True(rows[0].IsMarked);
        Assert.True(rows[0].IsCurrent);
        Assert.Equal("Not Encountered", rows[5].StatusTitle);

        var review = new ReviewScreenState(session) { Selection = 5 };
        Assert.False(review.CanGoToQuestion);
        review.Selection = 2;
        Assert.True(review.CanGoToQuestion);
        review.GoToQuestion();
        Assert.Equal(2, session.CurrentQuestionIndex);
        Assert.StartsWith("You have answered ", session.ExitSectionMessage);
    }

    [Fact]
    public void SelectionRulesFollowMaximum()
    {
        var session = TestContent.Services.StartExam(ExamMode.VerbalPractice);
        session.BeginSection();
        session.ToggleSelection("g", "a", 1);
        session.ToggleSelection("g", "b", 1);
        Assert.Equal(["b"], session.CurrentAnswer.Selections["g"]);
        session.ToggleSelection("m", "a", 2);
        session.ToggleSelection("m", "b", 2);
        session.ToggleSelection("m", "c", 2);
        Assert.Equal(new HashSet<string> { "a", "b" }, session.CurrentAnswer.Selections["m"]);
        session.ToggleSelection("m", "a", 2);
        Assert.Equal(["b"], session.CurrentAnswer.Selections["m"]);
    }

    [Fact]
    public void NumericAndFractionEntryAreCleanedLikeTheMacApp()
    {
        Assert.Equal("-12.53", ExamSession.CleanNumber("−12.5.3x", allowDecimal: true));
        Assert.Equal("125", ExamSession.CleanNumber("1-2.5", allowDecimal: false));
        Assert.Equal("1234567890", ExamSession.CleanNumber("123456789012", allowDecimal: true));

        var services = TestContent.Services;
        var session = services.StartExam(ExamMode.QuantitativePractice);
        session.BeginSection();
        while (session.CurrentQuestion.Format != QuestionFormat.NumericEntry && !session.IsLastQuestion) session.GoNext();
        if (session.CurrentQuestion.Format != QuestionFormat.NumericEntry) return;
        session.SetFractionPart(1, "4");
        Assert.Equal("/4", session.CurrentAnswer.NumericEntry);
        Assert.Equal(QuestionStatus.Incomplete, session.Status(session.CurrentQuestion));
        session.SetFractionPart(0, "3");
        Assert.Equal("3/4", session.CurrentAnswer.NumericEntry);
        Assert.Equal("3", session.FractionPart(0));
        Assert.Equal("4", session.FractionPart(1));
        Assert.Equal(QuestionStatus.Answered, session.Status(session.CurrentQuestion));
        session.SetFractionPart(0, ""); session.SetFractionPart(1, "");
        Assert.Equal("", session.CurrentAnswer.NumericEntry);

        session.SetNumericEntryText("2.50");
        Assert.Equal("2.50", session.CurrentAnswer.NumericEntry);
        foreach (var key in new[] { "1", "÷", "4", "=" }) session.Calculator.Input(key);
        session.TransferCalculatorDisplay();
        Assert.Equal(session.CanTransferFromCalculator ? "0.25" : "2.50", session.CurrentAnswer.NumericEntry);
    }

    [Fact]
    public void EssayWordCountAndLineEndings()
    {
        Assert.Equal(5, ExamSession.CountEssayWords("Hello, world! It's—fine."));
        Assert.Equal(0, ExamSession.CountEssayWords("  \n "));
        var session = TestContent.Services.StartExam(ExamMode.WritingPractice);
        session.BeginSection();
        Assert.Single(session.Sections);
        Assert.Equal("1 task", session.SectionTaskCountText);
        session.UpdateEssay("One two.\rThree\r\nfour");
        Assert.Equal("One two.\nThree\nfour", session.CurrentAnswer.EssayText);
        Assert.Equal(4, session.EssayWordCount);
        Assert.Equal(QuestionStatus.Answered, session.Status(session.CurrentQuestion));
        Assert.Equal(session.CurrentQuestion.Instructions ?? ExamText.DefaultIssueInstructions, session.EssayInstructions);
        session.ConfirmExitSection();
        Assert.Equal(ExamPhase.Completed, session.Phase);
        Assert.Null(session.Result!.VerbalScore);
        Assert.Equal(0, session.Result.WritingEstimate); // too short to evaluate
        Assert.NotNull(session.Result.WritingSummary);
    }

    [Fact]
    public void AdaptiveRoutingThresholds()
    {
        static QuestionDifficulty Route(int correct)
        {
            var session = TestContent.Services.StartExam(ExamMode.QuantitativePractice);
            session.BeginSection();
            var questions = session.CurrentSection.Questions;
            for (var i = 0; i < correct; i++)
            {
                session.CurrentQuestionIndex = i;
                var q = questions[i];
                if (q.Format == QuestionFormat.NumericEntry) session.UpdateNumericEntry(q.AcceptedNumericAnswers.First());
                else foreach (var (group, options) in q.CorrectSelections) foreach (var o in options) session.ToggleSelection(group, o, 99);
            }
            session.FinishSection();
            return session.Sections[1].AdaptiveDifficulty!.Value;
        }
        Assert.Equal(QuestionDifficulty.Easy, Route(4));   // 33%
        Assert.Equal(QuestionDifficulty.Medium, Route(5)); // 42%
        Assert.Equal(QuestionDifficulty.Medium, Route(7)); // 58%
        Assert.Equal(QuestionDifficulty.Hard, Route(8));   // 67%
    }

    [Fact]
    public void CorrectnessRequiresExactSets()
    {
        var question = TestContent.Services.Questions.All.First(q => q.Format == QuestionFormat.SentenceEquivalence);
        var key = question.CorrectSelections["main"];
        var answer = new GREAnswer { Selections = new() { ["main"] = [key.First()] } };
        Assert.False(ExamSession.IsCorrect(question, answer));
        answer.Selections["main"] = [.. key];
        Assert.True(ExamSession.IsCorrect(question, answer));
    }
}
